using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 로그인 화면과 캐릭터 화면을 OnGUI로 그리는 임시 화면. NetworkManagerHUD를 대신한다.
/// <para>
/// 흐름<br/>
/// - 로그인(회원가입) → 캐릭터 만들기·들어가기 → 들어가기에 성공하면 숨긴다.<br/>
/// - 접속이 끊기면(다른 곳에서 로그인해 쫓겨난 경우 포함) 로그인 화면으로 돌아가 이유를 보여 준다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Base 씬의 NetworkManager 오브젝트에 붙인다. NetworkManager의 Authenticator 칸에 InvokeAuthenticator가 있어야 한다.<br/>
/// - 이 화면은 InvokeAuthenticator의 Request 함수와 이벤트만 쓴다. 꾸민 화면을 새로 만들 때도 같은 함수와 이벤트를 쓰면 된다.<br/>
/// - 에디터에서는 Host 버튼이 보인다. Skip Login In Editor가 켜져 있으면 Host만 눌러 바로 들어간다.
/// </para>
/// </summary>
public class AccountScreen : MonoBehaviour
{
	enum Page { Login, Characters, Hidden }

	[Tooltip("화면을 그릴 영역. 왼쪽 위 모서리 위치와 너비")]
	[SerializeField] Rect area = new Rect(10, 40, 300, 9999);

	InvokeAuthenticator authenticator;
	Page page = Page.Login;

	string loginId = "";
	string password = "";
	// 캐릭터를 만들 때 쓸 닉네임. 언어가 C 하나뿐이라 입력칸도 하나만 둔다
	string nickname = "";
	// 화면 아래에 보여 줄 실패 이유나 안내 문장
	string message = "";

	// 서버 응답을 기다리는 동안 버튼을 막는다. 같은 요청을 두 번 보내지 않기 위함
	bool waiting;
	// 로그인한 계정의 캐릭터 목록. 로그인·캐릭터 만들기 결과를 받을 때마다 바꾼다
	CharacterSummary[] characters = Array.Empty<CharacterSummary>();
	// 서버가 보낸 끊김 이유. 서버는 알림을 보내고 잠시 뒤에 끊으므로, 실제로 끊긴 뒤 로그인 화면에 보여 준다
	AccountFailure kickedReason;
	// 지난 프레임에 클라이언트가 켜져 있었는지. 클라이언트가 꺼지는 순간을 알아내기 위함
	bool wasClientActive;

	void Start()
	{
		authenticator = InvokeAuthenticator.Current;
		if (authenticator == null)
		{
			Debug.LogError("[AccountScreen] InvokeAuthenticator가 없습니다. Base 씬 NetworkManager의 Authenticator 칸에 연결했는지 확인하세요.");
			enabled = false;
			return;
		}

		authenticator.AuthResultReceived += OnAuthResult;
		authenticator.CreateCharacterResultReceived += OnCreateCharacterResult;
		authenticator.EnterGameResultReceived += OnEnterGameResult;
		authenticator.Kicked += OnKicked;
	}

	void OnDestroy()
	{
		if (authenticator == null) return;
		authenticator.AuthResultReceived -= OnAuthResult;
		authenticator.CreateCharacterResultReceived -= OnCreateCharacterResult;
		authenticator.EnterGameResultReceived -= OnEnterGameResult;
		authenticator.Kicked -= OnKicked;
	}

	void Update()
	{
		// Mirror의 끊김 이벤트(NetworkClient.OnDisconnectedEvent)는 NetworkManager가 접속할 때마다 덮어쓰므로, 상태를 직접 살핀다
		bool clientActive = NetworkClient.active;
		if (wasClientActive && !clientActive) OnClientStopped();
		wasClientActive = clientActive;
	}

	void OnClientStopped()
	{
		if (kickedReason != AccountFailure.None) message = kickedReason.ToMessage();
		else if (page == Page.Login && waiting) message = "서버에 접속하지 못했습니다.";
		else message = "서버와 연결이 끊겼습니다.";

		page = Page.Login;
		waiting = false;
		kickedReason = AccountFailure.None;
		characters = Array.Empty<CharacterSummary>();
	}

	#region 그리기

	void OnGUI()
	{
		if (page == Page.Hidden) return;
		// 서버만 켠 경우에는 이 컴퓨터에서 로그인할 플레이어가 없다
		if (NetworkServer.active && !NetworkClient.active) return;

		GUILayout.BeginArea(area);

		GUI.enabled = !waiting;
		if (page == Page.Login) DrawLogin();
		else DrawCharacters();
		GUI.enabled = true;

		if (waiting) GUILayout.Label("서버 응답을 기다리는 중...");
		if (message.Length > 0) GUILayout.Label(message);

		GUILayout.EndArea();
	}

	void DrawLogin()
	{
		// 접속한 뒤에는 주소를 바꿔도 소용이 없으므로 접속하기 전에만 보여 준다
		if (!NetworkClient.active)
		{
			GUILayout.Label("서버 주소");
			NetworkManager.singleton.networkAddress = GUILayout.TextField(NetworkManager.singleton.networkAddress);
		}

		GUILayout.Label("아이디");
		loginId = GUILayout.TextField(loginId, AccountRules.LoginIdMaxLength);
		GUILayout.Label("비밀번호");
		password = GUILayout.PasswordField(password, '*', AccountRules.PasswordMaxLength);

		GUILayout.BeginHorizontal();
		if (GUILayout.Button("로그인")) Send(() => authenticator.RequestLogin(loginId, password));
		if (GUILayout.Button("회원가입")) Send(() => authenticator.RequestSignUp(loginId, password));
		GUILayout.EndHorizontal();

#if UNITY_EDITOR
		// 팀원이 에디터에서 서버와 클라이언트를 한 번에 켜는 버튼. Skip Login In Editor가 켜져 있으면 로그인을 건너뛴다
		if (!NetworkClient.active && GUILayout.Button("Host (에디터)"))
		{
			message = "";
			NetworkManager.singleton.StartHost();
		}
#endif
	}

	void DrawCharacters()
	{
		GUILayout.Label("캐릭터");

		foreach (string language in CharacterLanguages.All)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(language, GUILayout.Width(40));

			if (TryFindCharacter(language, out CharacterSummary character))
			{
				GUILayout.Label(character.nickname);
				if (GUILayout.Button("들어가기", GUILayout.Width(80))) Send(() => authenticator.RequestEnter(language));
			}
			else
			{
				nickname = GUILayout.TextField(nickname, AccountRules.NicknameMaxLength);
				if (GUILayout.Button("만들기", GUILayout.Width(80))) Send(() => authenticator.RequestCreateCharacter(language, nickname));
			}

			GUILayout.EndHorizontal();
		}
	}

	bool TryFindCharacter(string language, out CharacterSummary found)
	{
		foreach (CharacterSummary character in characters)
		{
			if (character.language != language) continue;
			found = character;
			return true;
		}
		found = default;
		return false;
	}

	void Send(Action request)
	{
		message = "";
		waiting = true;
		request();
	}

	#endregion

	#region 결과 받기

	void OnAuthResult(AccountAuthResult result)
	{
		waiting = false;
		if (!result.Success)
		{
			message = result.failure.ToMessage();
			return;
		}

		message = "";
		// 로그인한 뒤에는 화면에 비밀번호를 남겨 둘 이유가 없다
		password = "";
		characters = result.characters ?? Array.Empty<CharacterSummary>();
		page = Page.Characters;
	}

	void OnCreateCharacterResult(CreateCharacterResultMessage result)
	{
		waiting = false;
		characters = result.characters ?? Array.Empty<CharacterSummary>();
		message = result.Success ? "" : result.failure.ToMessage();
		if (result.Success) nickname = "";
	}

	void OnEnterGameResult(EnterGameResultMessage result)
	{
		waiting = false;
		if (!result.Success)
		{
			message = result.failure.ToMessage();
			return;
		}

		message = "";
		page = Page.Hidden;
	}

	void OnKicked(AccountFailure reason)
	{
		kickedReason = reason;
	}

	#endregion
}
