using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 접속한 클라이언트가 로그인하고 캐릭터를 골라야 플레이어가 생기게 하는 Mirror 인증 컴포넌트.
/// <para>
/// 흐름<br/>
/// - 로그인: 내 화면이 RequestLogin 또는 RequestSignUp을 부르면, 서버에 아직 접속하지 않았을 때는 먼저 접속하고 요청을 보낸다.
/// 서버가 AccountServer로 확인하고 결과(성공이면 캐릭터 목록)를 보낸다. 성공하면 서버와 내 화면 모두 인증을 끝낸다.
/// 실패해도 접속을 끊지 않으므로 플레이어는 다시 시도할 수 있다.<br/>
/// - 캐릭터: 로그인한 뒤 RequestCreateCharacter로 캐릭터를 만들고, RequestEnter로 들어간다. 들어가기에 성공하면 서버가 로비에 플레이어를 만든다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Base 씬의 NetworkManager 오브젝트에 붙이고, NetworkManager의 Authenticator 칸에 넣는다.<br/>
/// - 화면은 InvokeAuthenticator.Current로 가져와 Request 함수들을 부르고, 이벤트로 결과를 받는다.<br/>
/// - 에디터에서는 Skip Login In Editor가 켜져 있으면 로그인과 캐릭터 고르기를 건너뛰고 임시 계정의 C 캐릭터로 바로 들어간다.
/// </para>
/// </summary>
public class InvokeAuthenticator : NetworkAuthenticator
{
	[Tooltip("같은 계정이 다시 로그인했을 때, 먼저 접속한 쪽에 끊김 알림을 보내고 실제로 끊기까지 기다리는 시간(초). 알림이 도착할 시간을 준다.")]
	[SerializeField] float kickDisconnectDelay = 0.5f;

	[Tooltip("에디터에서만 동작. 켜면 Host·Client로 시작할 때 로그인과 캐릭터 고르기를 건너뛰고, DB를 쓰지 않는 임시 계정의 C 캐릭터로 바로 들어간다. 로그인을 테스트할 때만 끈다.")]
	[SerializeField] bool skipLoginInEditor = true;

#if UNITY_SERVER || UNITY_EDITOR
	AccountServer accountServer;
#endif

	// 접속을 마치면 보낼 요청. 접속하기 전에 RequestLogin / RequestSignUp이 불리면 여기에 둔다
	AccountAuthRequest pendingRequest;
	bool hasPendingRequest;
	// 이번 접속을 「로그인 건너뛰기」로 했으면 true. 로그인 결과를 받으면 바로 C 캐릭터로 들어간다
	bool skippingLogin;

	/// <summary>[내 화면] 로그인·회원가입 결과를 받았을 때. 성공하면 이 이벤트 다음에 인증이 끝난다.</summary>
	public event Action<AccountAuthResult> AuthResultReceived;

	/// <summary>[내 화면] 캐릭터 만들기 결과를 받았을 때. 성공·실패 모두 그 계정의 지금 캐릭터 목록이 들어 있다.</summary>
	public event Action<CreateCharacterResultMessage> CreateCharacterResultReceived;

	/// <summary>[내 화면] 들어가기 결과를 받았을 때. 성공하면 이어서 서버가 로비에 플레이어를 만든다.</summary>
	public event Action<EnterGameResultMessage> EnterGameResultReceived;

	/// <summary>[내 화면] 서버가 곧 접속을 끊는다는 알림을 받았을 때. 받는 값은 이유 (예: LoggedInElsewhere).</summary>
	public event Action<AccountFailure> Kicked;

	/// <summary>Base 씬 NetworkManager에 붙은 InvokeAuthenticator. 없으면 null.</summary>
	public static InvokeAuthenticator Current =>
		NetworkManager.singleton != null ? NetworkManager.singleton.authenticator as InvokeAuthenticator : null;

	#region 서버

	public override void OnStartServer()
	{
#if UNITY_SERVER || UNITY_EDITOR
		accountServer = new AccountServer(AccountStore.DefaultPath, conn => StartCoroutine(DisconnectLater(conn)));
		// 로그인하기 전에 받는 메시지이므로 인증을 요구하지 않는다
		NetworkServer.RegisterHandler<AccountAuthRequest>(OnAuthRequest, false);
		// 캐릭터 요청은 로그인한 연결만 보낼 수 있다
		NetworkServer.RegisterHandler<CreateCharacterRequest>(OnCreateCharacterRequest);
		NetworkServer.RegisterHandler<EnterGameRequest>(OnEnterGameRequest);
		Debug.Log($"[InvokeAuthenticator] 계정 DB를 열었습니다: {AccountStore.DefaultPath}");
#else
		Debug.LogError("[InvokeAuthenticator] 이 빌드에는 계정 DB 코드가 없어서 서버로 쓸 수 없습니다. Dedicated Server로 빌드한 서버를 쓰세요.");
#endif
	}

	public override void OnStopServer()
	{
#if UNITY_SERVER || UNITY_EDITOR
		NetworkServer.UnregisterHandler<AccountAuthRequest>();
		NetworkServer.UnregisterHandler<CreateCharacterRequest>();
		NetworkServer.UnregisterHandler<EnterGameRequest>();
		accountServer?.Dispose();
		accountServer = null;
#endif
	}

	public override void OnServerAuthenticate(NetworkConnectionToClient conn)
	{
		// 접속하자마자 할 일은 없다. 내 화면이 로그인 요청을 보낼 때까지 기다린다
	}

	/// <summary>[서버] 연결이 끊겼을 때 InvokeNetworkManager가 부른다. 그 계정을 접속 중 목록에서 지운다.</summary>
	public void OnServerDisconnected(NetworkConnectionToClient conn)
	{
#if UNITY_SERVER || UNITY_EDITOR
		accountServer?.Forget(conn);
#endif
	}

#if UNITY_SERVER || UNITY_EDITOR
	void OnAuthRequest(NetworkConnectionToClient conn, AccountAuthRequest request)
	{
		// 이미 로그인한 연결은 다른 계정으로 다시 로그인할 수 없다
		if (conn.isAuthenticated) return;

#if UNITY_EDITOR
		if (request.skipLogin)
		{
			accountServer.AcceptTemporary(conn);
			ServerAccept(conn);
			return;
		}
#endif

		if (accountServer.HandleAuthRequest(conn, request)) ServerAccept(conn);
	}

	void OnCreateCharacterRequest(NetworkConnectionToClient conn, CreateCharacterRequest request)
	{
		accountServer.HandleCreateCharacter(conn, request);
	}

	void OnEnterGameRequest(NetworkConnectionToClient conn, EnterGameRequest request)
	{
		if (!accountServer.TryEnter(conn, request)) return;

		if (NetworkManager.singleton is InvokeNetworkManager manager) manager.SpawnPlayer(conn);
		else Debug.LogError("[InvokeAuthenticator] InvokeNetworkManager가 없어서 플레이어를 만들 수 없습니다.");
	}
#endif

	IEnumerator DisconnectLater(NetworkConnectionToClient conn)
	{
		yield return new WaitForSeconds(kickDisconnectDelay);
		// 기다리는 사이에 스스로 끊었으면 할 일이 없다
		if (NetworkServer.connections.ContainsKey(conn.connectionId)) conn.Disconnect();
	}

	#endregion

	#region 내 화면

	public override void OnStartClient()
	{
		NetworkClient.RegisterHandler<AccountAuthResult>(OnAuthResult, false);
		NetworkClient.RegisterHandler<AccountKickedMessage>(OnKicked, false);
		NetworkClient.RegisterHandler<CreateCharacterResultMessage>(OnCreateCharacterResult);
		NetworkClient.RegisterHandler<EnterGameResultMessage>(OnEnterGameResult);
	}

	public override void OnStopClient()
	{
		NetworkClient.UnregisterHandler<AccountAuthResult>();
		NetworkClient.UnregisterHandler<AccountKickedMessage>();
		NetworkClient.UnregisterHandler<CreateCharacterResultMessage>();
		NetworkClient.UnregisterHandler<EnterGameResultMessage>();
		hasPendingRequest = false;
		skippingLogin = false;
	}

	public override void OnClientAuthenticate()
	{
		// 접속이 끝났다. 접속 전에 받아 둔 요청이 있으면 이제 보낸다
		if (hasPendingRequest)
		{
			hasPendingRequest = false;
			NetworkClient.Send(pendingRequest);
			return;
		}

#if UNITY_EDITOR
		// 로그인 화면을 거치지 않고(Host 버튼 등) 접속했으면, 팀원이 바로 테스트할 수 있도록 로그인을 건너뛴다
		if (skipLoginInEditor)
		{
			skippingLogin = true;
			NetworkClient.Send(new AccountAuthRequest { skipLogin = true });
		}
#endif
	}

	/// <summary>[내 화면] 로그인을 요청한다. 서버에 접속하지 않았으면 먼저 접속한다. 결과는 AuthResultReceived로 온다.</summary>
	public void RequestLogin(string loginId, string password)
	{
		SendAuthRequest(new AccountAuthRequest { loginId = loginId, password = password, isSignUp = false });
	}

	/// <summary>[내 화면] 회원가입을 요청한다. 성공하면 바로 로그인된다. 서버에 접속하지 않았으면 먼저 접속한다. 결과는 AuthResultReceived로 온다.</summary>
	public void RequestSignUp(string loginId, string password)
	{
		SendAuthRequest(new AccountAuthRequest { loginId = loginId, password = password, isSignUp = true });
	}

	/// <summary>[내 화면] 로그인한 뒤, 그 언어(CharacterLanguages)의 캐릭터를 만들어 달라고 요청한다. 결과는 CreateCharacterResultReceived로 온다.</summary>
	public void RequestCreateCharacter(string language, string nickname)
	{
		if (!IsLoggedIn()) return;
		NetworkClient.Send(new CreateCharacterRequest { language = language, nickname = nickname });
	}

	/// <summary>[내 화면] 로그인한 뒤, 그 언어(CharacterLanguages)의 캐릭터로 게임에 들어가기를 요청한다. 결과는 EnterGameResultReceived로 온다.</summary>
	public void RequestEnter(string language)
	{
		if (!IsLoggedIn()) return;
		NetworkClient.Send(new EnterGameRequest { language = language });
	}

	void SendAuthRequest(AccountAuthRequest request)
	{
		if (NetworkClient.isConnected)
		{
			if (NetworkClient.connection.isAuthenticated)
			{
				Debug.LogWarning("[InvokeAuthenticator] 이미 로그인했습니다.");
				return;
			}
			NetworkClient.Send(request);
			return;
		}

		// 접속을 마치면 OnClientAuthenticate에서 보낸다
		pendingRequest = request;
		hasPendingRequest = true;
		if (!NetworkClient.active) NetworkManager.singleton.StartClient();
	}

	bool IsLoggedIn()
	{
		if (NetworkClient.isConnected && NetworkClient.connection.isAuthenticated) return true;

		Debug.LogWarning("[InvokeAuthenticator] 로그인한 뒤에 캐릭터를 만들거나 들어갈 수 있습니다.");
		return false;
	}

	void OnAuthResult(AccountAuthResult result)
	{
		AuthResultReceived?.Invoke(result);
		if (!result.Success) return;

		ClientAccept();
		// 로그인 건너뛰기는 캐릭터 고르기도 건너뛴다
		if (skippingLogin) RequestEnter(CharacterLanguages.C);
	}

	void OnCreateCharacterResult(CreateCharacterResultMessage result)
	{
		CreateCharacterResultReceived?.Invoke(result);
	}

	void OnEnterGameResult(EnterGameResultMessage result)
	{
		EnterGameResultReceived?.Invoke(result);
	}

	void OnKicked(AccountKickedMessage message)
	{
		Kicked?.Invoke(message.reason);
	}

	#endregion
}
