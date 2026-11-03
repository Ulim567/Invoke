#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버에서 로그인·회원가입·캐릭터 만들기·들어가기 요청을 처리한다. InvokeAuthenticator가 서버를 켤 때 만들고, 끌 때 Dispose한다.
/// <para>
/// 하는 일<br/>
/// - 입력 규칙 검사(AccountRules) → AccountStore로 확인·저장 → 결과를 그 연결에 보낸다.<br/>
/// - 들어가기에 성공하면 그 연결의 AccountSession에 캐릭터를 적는다. 플레이어를 만드는 일은 부르는 쪽이 한다.<br/>
/// - 어느 계정이 어느 연결로 접속 중인지 기억한다. 같은 계정이 다시 로그인하면 먼저 접속한 연결을 끊는다.
/// </para>
/// <para>
/// DB 코드가 클라이언트 빌드에 들어가지 않도록 서버 빌드와 에디터에서만 컴파일한다.
/// </para>
/// </summary>
public class AccountServer : IDisposable
{
	readonly AccountStore store;
	// 같은 계정의 먼저 접속한 연결에서 플레이어를 없애고 잠시 뒤 끊는 함수.
	// 끊김 알림이 도착할 시간을 두고 끊어야 하므로 부르는 쪽(MonoBehaviour)이 정한다
	readonly Action<NetworkConnectionToClient> kickLater;
	// 계정 key → 그 계정으로 로그인한 연결
	readonly Dictionary<string, NetworkConnectionToClient> onlineAccounts = new Dictionary<string, NetworkConnectionToClient>();

	public AccountServer(string dbPath, Action<NetworkConnectionToClient> kickLater)
	{
		store = new AccountStore(dbPath);
		this.kickLater = kickLater;
	}

	/// <summary>
	/// 로그인·회원가입 요청을 처리하고 결과를 conn에 보낸다.
	/// 성공하면 conn.authenticationData에 AccountSession을 넣고 true를 돌려준다. 부르는 쪽은 그때 인증을 끝낸다(ServerAccept).
	/// </summary>
	public bool HandleAuthRequest(NetworkConnectionToClient conn, AccountAuthRequest request)
	{
		string accountKey;
		string nickname;
		AccountFailure failure = request.isSignUp
			? TrySignUp(request.loginId, request.password, request.nickname, out accountKey, out nickname)
			: TryLogin(request.loginId, request.password, out accountKey, out nickname);

		if (failure != AccountFailure.None)
		{
			conn.Send(new AccountAuthResult { failure = failure, characters = new CharacterSummary[0] });
			return false;
		}

		KickOtherConnection(accountKey, conn);
		onlineAccounts[accountKey] = conn;
		conn.authenticationData = new AccountSession(accountKey, AccountStore.NormalizeLoginId(request.loginId), nickname);

		conn.Send(new AccountAuthResult { failure = AccountFailure.None, nickname = nickname, characters = GetSummaries(accountKey) });
		Debug.Log($"[AccountServer] {(request.isSignUp ? "회원가입" : "로그인")}: {request.loginId} ({nickname}, connId={conn.connectionId})");
		return true;
	}

	/// <summary>
	/// 로그인한 연결의 캐릭터 만들기 요청을 처리하고, 결과와 그 계정의 지금 캐릭터 목록을 conn에 보낸다.
	/// 이미 게임에 들어간 연결, 임시 계정, 곧 끊길 연결의 요청은 만들지 않고 RequestNotAllowed를 보낸다.
	/// </summary>
	public void HandleCreateCharacter(NetworkConnectionToClient conn, CreateCharacterRequest request)
	{
		if (!(conn.authenticationData is AccountSession session)) return;
		// 무시하면 내 화면이 결과를 기다리며 멈추므로, 받을 수 없는 요청에도 결과를 보낸다
		if (session.HasEntered || session.IsTemporary || session.IsKicked)
		{
			// 캐릭터 목록은 그대로 보낸다. 빈 목록을 보내면 내 화면이 만든 캐릭터가 없어진 것으로 그린다
			CharacterSummary[] current = session.IsTemporary ? TemporaryCharacters() : GetSummaries(session.AccountKey);
			conn.Send(new CreateCharacterResultMessage { failure = AccountFailure.RequestNotAllowed, characters = current });
			return;
		}

		AccountFailure failure = ToFailure(store.CreateCharacter(session.AccountKey, request.language, out _));

		conn.Send(new CreateCharacterResultMessage { failure = failure, characters = GetSummaries(session.AccountKey) });
		if (failure == AccountFailure.None)
			Debug.Log($"[AccountServer] 캐릭터 만들기: {session.LoginId} → [{request.language}]");
	}

	/// <summary>
	/// 로그인한 연결의 들어가기 요청을 처리한다. 그 계정에 그 언어의 캐릭터가 있으면 세션에 적고 true를 돌려준다.
	/// 부르는 쪽은 true일 때 플레이어를 만든다. 결과는 성공·실패 모두 conn에 보낸다.<br/>
	/// 이미 게임에 들어간 연결, 곧 끊길 연결의 요청은 RequestNotAllowed를 보내고 false를 돌려준다.
	/// </summary>
	public bool TryEnter(NetworkConnectionToClient conn, EnterGameRequest request)
	{
		if (!(conn.authenticationData is AccountSession session)) return false;
		// 무시하면 내 화면이 결과를 기다리며 멈추므로, 받을 수 없는 요청에도 결과를 보낸다
		if (session.HasEntered || session.IsKicked)
		{
			conn.Send(new EnterGameResultMessage { failure = AccountFailure.RequestNotAllowed });
			return false;
		}

		if (session.IsTemporary)
		{
			// 임시 계정은 DB에 캐릭터가 없으므로, 언어와 상관없이 계정 key를 캐릭터 key로 삼아 들어간다
			session.Enter(session.AccountKey);
		}
		else
		{
			CharacterRecord character = store.GetCharacters(session.AccountKey).Find(c => c.Language == request.language);
			if (character == null)
			{
				conn.Send(new EnterGameResultMessage { failure = AccountFailure.CharacterNotFound });
				return false;
			}
			session.Enter(character.Key);
		}

		conn.Send(new EnterGameResultMessage { failure = AccountFailure.None });
		Debug.Log($"[AccountServer] 들어가기: {session.LoginId} → [{request.language}] {session.Nickname} (connId={conn.connectionId})");
		return true;
	}

#if UNITY_EDITOR
	/// <summary>
	/// [에디터 전용] 「로그인 건너뛰기」: DB를 쓰지 않는 임시 계정으로 로그인시키고 결과를 보낸다.
	/// 부르는 쪽은 그다음 인증을 끝낸다(ServerAccept). 빌드한 서버에는 이 함수가 없다.
	/// </summary>
	public void AcceptTemporary(NetworkConnectionToClient conn)
	{
		string nickname = TemporaryNickname(conn);
		conn.authenticationData = new AccountSession($"editor-{conn.connectionId}", "(로그인 건너뛰기)", nickname, isTemporary: true);
		conn.Send(new AccountAuthResult { failure = AccountFailure.None, nickname = nickname, characters = TemporaryCharacters() });
		Debug.Log($"[AccountServer] 로그인 건너뛰기: 임시 계정 (connId={conn.connectionId})");
	}
#endif

	/// <summary>연결이 끊겼을 때 부른다. 그 연결이 접속 중 목록에 있으면 지운다.</summary>
	public void Forget(NetworkConnectionToClient conn)
	{
		if (!(conn.authenticationData is AccountSession session)) return;

		// 같은 계정이 다시 로그인해 목록이 이미 새 연결로 바뀌었으면, 새 연결을 지우지 않는다
		if (onlineAccounts.TryGetValue(session.AccountKey, out NetworkConnectionToClient current) && current == conn)
			onlineAccounts.Remove(session.AccountKey);
	}

	public void Dispose()
	{
		onlineAccounts.Clear();
		store.Dispose();
	}

	AccountFailure TrySignUp(string loginId, string password, string nickname, out string accountKey, out string savedNickname)
	{
		accountKey = null;
		savedNickname = null;

		AccountFailure failure = AccountRules.CheckLoginId(loginId);
		if (failure == AccountFailure.None) failure = AccountRules.CheckNickname(nickname);
		if (failure == AccountFailure.None) failure = AccountRules.CheckPassword(password);
		if (failure != AccountFailure.None) return failure;

		switch (store.SignUp(loginId, password, nickname, out accountKey))
		{
			case SignUpResult.LoginIdTaken: return AccountFailure.LoginIdTaken;
			case SignUpResult.NicknameTaken: return AccountFailure.NicknameTaken;
		}
		savedNickname = nickname;
		return AccountFailure.None;
	}

	AccountFailure TryLogin(string loginId, string password, out string accountKey, out string nickname)
	{
		accountKey = null;
		nickname = null;

		// 규칙에 맞지 않는 아이디·비밀번호로는 계정을 만들 수 없으므로, 해시를 계산하지 않고 바로 거부한다.
		// 이유를 따로 알려 주지 않는 것은 TryLogin의 다른 실패와 같다
		if (AccountRules.CheckLoginId(loginId) != AccountFailure.None || AccountRules.CheckPassword(password) != AccountFailure.None)
			return AccountFailure.WrongLoginOrPassword;

		if (!store.TryLogin(loginId, password, out accountKey, out nickname))
			return AccountFailure.WrongLoginOrPassword;
		return AccountFailure.None;
	}

	// 같은 계정으로 먼저 접속한 연결이 있으면, 끊김 알림을 보내고 끊는다 (나중에 들어온 쪽을 받는다)
	void KickOtherConnection(string accountKey, NetworkConnectionToClient newConn)
	{
		if (!onlineAccounts.TryGetValue(accountKey, out NetworkConnectionToClient oldConn) || oldConn == newConn) return;

		onlineAccounts.Remove(accountKey);
		// 인증은 풀지 않는다. 풀면 그 연결이 보내는 다음 게임 메시지에서 Mirror가 바로 끊어, 끊김 알림이 도착하지 못한다.
		// 대신 세션에 표시해 캐릭터 요청과 다시 로그인을 막고, 플레이어는 kickLater가 바로 없앤다
		if (oldConn.authenticationData is AccountSession oldSession) oldSession.MarkKicked();
		oldConn.Send(new AccountKickedMessage { reason = AccountFailure.LoggedInElsewhere });
		kickLater(oldConn);
		Debug.Log($"[AccountServer] 같은 계정이 다시 로그인해서 이전 접속을 끊습니다 (connId={oldConn.connectionId})");
	}

	static AccountFailure ToFailure(CreateCharacterResult result)
	{
		switch (result)
		{
			case CreateCharacterResult.UnknownLanguage: return AccountFailure.UnknownLanguage;
			case CreateCharacterResult.LanguageTaken: return AccountFailure.LanguageTaken;
			default: return AccountFailure.None;
		}
	}

	static string TemporaryNickname(NetworkConnectionToClient conn) => $"Editor{conn.connectionId}";

	// 임시 계정은 DB에 캐릭터가 없으므로 C 캐릭터 하나가 있는 것으로 보여 준다
	static CharacterSummary[] TemporaryCharacters() => new[] { new CharacterSummary { language = CharacterLanguages.C } };

	CharacterSummary[] GetSummaries(string accountKey)
	{
		List<CharacterRecord> characters = store.GetCharacters(accountKey);
		var summaries = new CharacterSummary[characters.Count];
		for (int i = 0; i < characters.Count; i++)
			summaries[i] = new CharacterSummary { language = characters[i].Language };
		return summaries;
	}
}
#endif
