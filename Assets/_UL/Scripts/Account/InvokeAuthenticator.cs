using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 접속한 클라이언트가 로그인해야 플레이어가 생기게 하는 Mirror 인증 컴포넌트.
/// <para>
/// 흐름<br/>
/// - 내 화면이 RequestLogin 또는 RequestSignUp을 부르면, 서버에 아직 접속하지 않았을 때는 먼저 접속하고 요청을 보낸다.<br/>
/// - 서버가 AccountServer로 확인하고 결과를 보낸다. 성공하면 서버와 내 화면 모두 인증을 끝내고, 그다음 NetworkManager가 플레이어를 만든다.<br/>
/// - 실패해도 접속을 끊지 않는다. 플레이어는 다시 시도할 수 있다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Base 씬의 NetworkManager 오브젝트에 붙이고, NetworkManager의 Authenticator 칸에 넣는다.<br/>
/// - 화면은 InvokeAuthenticator.Current로 가져와 RequestLogin / RequestSignUp을 부르고, AuthResultReceived / Kicked 이벤트로 결과를 받는다.
/// </para>
/// </summary>
public class InvokeAuthenticator : NetworkAuthenticator
{
	[Tooltip("같은 계정이 다시 로그인했을 때, 먼저 접속한 쪽에 끊김 알림을 보내고 실제로 끊기까지 기다리는 시간(초). 알림이 도착할 시간을 준다.")]
	[SerializeField] float kickDisconnectDelay = 0.5f;

#if UNITY_SERVER || UNITY_EDITOR
	AccountServer accountServer;
#endif

	// 접속을 마치면 보낼 요청. 접속하기 전에 RequestLogin / RequestSignUp이 불리면 여기에 둔다
	AccountAuthRequest pendingRequest;
	bool hasPendingRequest;

	/// <summary>[내 화면] 로그인·회원가입 결과를 받았을 때. 성공하면 이 이벤트 다음에 인증이 끝나고 플레이어가 생긴다.</summary>
	public event Action<AccountAuthResult> AuthResultReceived;

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
		Debug.Log($"[InvokeAuthenticator] 계정 DB를 열었습니다: {AccountStore.DefaultPath}");
#else
		Debug.LogError("[InvokeAuthenticator] 이 빌드에는 계정 DB 코드가 없어서 서버로 쓸 수 없습니다. Dedicated Server로 빌드한 서버를 쓰세요.");
#endif
	}

	public override void OnStopServer()
	{
#if UNITY_SERVER || UNITY_EDITOR
		NetworkServer.UnregisterHandler<AccountAuthRequest>();
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

		if (accountServer.HandleAuthRequest(conn, request)) ServerAccept(conn);
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
	}

	public override void OnStopClient()
	{
		NetworkClient.UnregisterHandler<AccountAuthResult>();
		NetworkClient.UnregisterHandler<AccountKickedMessage>();
		hasPendingRequest = false;
	}

	public override void OnClientAuthenticate()
	{
		// 접속이 끝났다. 접속 전에 받아 둔 요청이 있으면 이제 보낸다
		if (!hasPendingRequest) return;

		hasPendingRequest = false;
		NetworkClient.Send(pendingRequest);
	}

	/// <summary>[내 화면] 로그인을 요청한다. 서버에 접속하지 않았으면 먼저 접속한다. 결과는 AuthResultReceived로 온다.</summary>
	public void RequestLogin(string loginId, string password)
	{
		SendRequest(new AccountAuthRequest { loginId = loginId, password = password, isSignUp = false });
	}

	/// <summary>[내 화면] 회원가입을 요청한다. 성공하면 바로 로그인된다. 서버에 접속하지 않았으면 먼저 접속한다. 결과는 AuthResultReceived로 온다.</summary>
	public void RequestSignUp(string loginId, string password)
	{
		SendRequest(new AccountAuthRequest { loginId = loginId, password = password, isSignUp = true });
	}

	void SendRequest(AccountAuthRequest request)
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

	void OnAuthResult(AccountAuthResult result)
	{
		AuthResultReceived?.Invoke(result);
		if (result.Success) ClientAccept();
	}

	void OnKicked(AccountKickedMessage message)
	{
		Kicked?.Invoke(message.reason);
	}

	#endregion
}
