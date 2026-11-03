#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버에서 로그인·회원가입 요청을 처리한다. InvokeAuthenticator가 서버를 켤 때 만들고, 끌 때 Dispose한다.
/// <para>
/// 하는 일<br/>
/// - 입력 규칙 검사(AccountRules) → AccountStore로 회원가입·로그인 확인 → 결과를 그 연결에 보낸다.<br/>
/// - 어느 계정이 어느 연결로 접속 중인지 기억한다. 같은 계정이 다시 로그인하면 먼저 접속한 연결을 끊는다.
/// </para>
/// <para>
/// DB 코드가 클라이언트 빌드에 들어가지 않도록 서버 빌드와 에디터에서만 컴파일한다.
/// </para>
/// </summary>
public class AccountServer : IDisposable
{
	readonly AccountStore store;
	// 같은 계정의 먼저 접속한 연결을 끊는 함수. 끊김 알림이 도착할 시간을 두고 끊어야 하므로 부르는 쪽(MonoBehaviour)이 정한다
	readonly Action<NetworkConnectionToClient> disconnectLater;
	// 계정 key → 그 계정으로 로그인한 연결
	readonly Dictionary<string, NetworkConnectionToClient> onlineAccounts = new Dictionary<string, NetworkConnectionToClient>();

	public AccountServer(string dbPath, Action<NetworkConnectionToClient> disconnectLater)
	{
		store = new AccountStore(dbPath);
		this.disconnectLater = disconnectLater;
	}

	/// <summary>
	/// 로그인·회원가입 요청을 처리하고 결과를 conn에 보낸다.
	/// 성공하면 conn.authenticationData에 AccountSession을 넣고 true를 돌려준다. 부르는 쪽은 그때 인증을 끝낸다(ServerAccept).
	/// </summary>
	public bool HandleAuthRequest(NetworkConnectionToClient conn, AccountAuthRequest request)
	{
		string accountKey;
		AccountFailure failure = request.isSignUp
			? TrySignUp(request.loginId, request.password, out accountKey)
			: TryLogin(request.loginId, request.password, out accountKey);

		if (failure != AccountFailure.None)
		{
			conn.Send(new AccountAuthResult { failure = failure, characters = new CharacterSummary[0] });
			return false;
		}

		KickOtherConnection(accountKey, conn);
		onlineAccounts[accountKey] = conn;
		conn.authenticationData = new AccountSession(accountKey, request.loginId);

		conn.Send(new AccountAuthResult { failure = AccountFailure.None, characters = GetSummaries(accountKey) });
		Debug.Log($"[AccountServer] {(request.isSignUp ? "회원가입" : "로그인")}: {request.loginId} (connId={conn.connectionId})");
		return true;
	}

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

	AccountFailure TrySignUp(string loginId, string password, out string accountKey)
	{
		accountKey = null;

		AccountFailure failure = AccountRules.CheckLoginId(loginId);
		if (failure == AccountFailure.None) failure = AccountRules.CheckPassword(password);
		if (failure != AccountFailure.None) return failure;

		if (store.SignUp(loginId, password, out accountKey) == SignUpResult.LoginIdTaken)
			return AccountFailure.LoginIdTaken;
		return AccountFailure.None;
	}

	AccountFailure TryLogin(string loginId, string password, out string accountKey)
	{
		accountKey = null;

		// 규칙에 맞지 않는 아이디·비밀번호로는 계정을 만들 수 없으므로, 해시를 계산하지 않고 바로 거부한다.
		// 이유를 따로 알려 주지 않는 것은 TryLogin의 다른 실패와 같다
		if (AccountRules.CheckLoginId(loginId) != AccountFailure.None || AccountRules.CheckPassword(password) != AccountFailure.None)
			return AccountFailure.WrongLoginOrPassword;

		if (!store.TryLogin(loginId, password, out accountKey))
			return AccountFailure.WrongLoginOrPassword;
		return AccountFailure.None;
	}

	// 같은 계정으로 먼저 접속한 연결이 있으면, 끊김 알림을 보내고 끊는다 (나중에 들어온 쪽을 받는다)
	void KickOtherConnection(string accountKey, NetworkConnectionToClient newConn)
	{
		if (!onlineAccounts.TryGetValue(accountKey, out NetworkConnectionToClient oldConn) || oldConn == newConn) return;

		onlineAccounts.Remove(accountKey);
		oldConn.Send(new AccountKickedMessage { reason = AccountFailure.LoggedInElsewhere });
		// 끊기 전까지 그 연결이 게임 메시지를 보내지 못하게 한다
		oldConn.isAuthenticated = false;
		disconnectLater(oldConn);
		Debug.Log($"[AccountServer] 같은 계정이 다시 로그인해서 이전 접속을 끊습니다 (connId={oldConn.connectionId})");
	}

	CharacterSummary[] GetSummaries(string accountKey)
	{
		List<CharacterRecord> characters = store.GetCharacters(accountKey);
		var summaries = new CharacterSummary[characters.Count];
		for (int i = 0; i < characters.Count; i++)
			summaries[i] = new CharacterSummary { language = characters[i].Language, nickname = characters[i].Nickname };
		return summaries;
	}
}
#endif
