using System;
using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버가 PlayerPublicInfo의 현재 장소와 닉네임을 맞게 넣고, 다른 클라이언트가 그 값을 받는지(#356) 확인하는 테스트.
/// <para>
/// 사용법<br/>
/// - 호스트 혼자: Base 씬에서 Play → eval로 <c>PlayerPublicInfoTest.StartInEditor(false)</c>. 서버가 꺼져 있으면 호스트로 켠다.<br/>
/// - 호스트 + 클라이언트: <c>PlayerPublicInfoTest.StartInEditor(true)</c>로 시작한 뒤, 시험 빌드를 <c>-playerPublicInfoTest member</c>로 실행한다. 다른 컴퓨터면 <c>-playerPublicInfoTestAddress 호스트주소</c>를 더한다.<br/>
/// - 씬에 놓지 않는다.
/// </para>
/// <para>
/// host: 로비 → 사냥터 → 로비로 옮기며 서버가 가진 Place가 도착한 장소의 이름인지, Nickname이 계정의 닉네임인지 본다. 클라이언트가 있으면 클라이언트 플레이어의 값도 서버에서 본다.<br/>
/// member: 접속해 로비에 들어온 뒤, 클라이언트 화면이 받은 자기와 호스트의 값을 본다. 사냥터로 옮겨 자기 Place가 바뀌는 것도 본다.<br/>
/// 「[PlayerPublicInfoTest][역할] 전체 통과」면 통과.
/// </para>
/// </summary>
public class PlayerPublicInfoTest : MonoBehaviour
{
	const string RoleArg = "-playerPublicInfoTest";
	const string AddressArg = "-playerPublicInfoTestAddress";
	// 씬 로드와 이동 시간을 넉넉히 기다린다
	const float TimeoutSeconds = 30f;
	// member가 사냥터에 머무는 시간. host가 값을 확인하는 시간보다 길어야 한다
	const float MemberStaySeconds = 8f;

	string role;
	int failedCount;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void CreateIfRequested()
	{
		string role = GetArg(RoleArg);
		if (role != null) Create(role);
	}

	/// <summary>에디터 Play 모드에서 eval로 부른다. withMember면 시험 빌드 클라이언트(member) 하나가 함께 하는 것으로 본다.</summary>
	public static void StartInEditor(bool withMember) => Create(withMember ? "host+member" : "host");

	static void Create(string role)
	{
		var go = new GameObject(nameof(PlayerPublicInfoTest));
		DontDestroyOnLoad(go);
		go.AddComponent<PlayerPublicInfoTest>().role = role;
	}

	IEnumerator Start()
	{
		if (role == "host" || role == "host+member") yield return RunHost(role == "host+member");
		else if (role == "member") yield return RunMember();
		else Fail($"모르는 역할: {role}");

		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
		if (role == "member") Application.Quit();
	}

	#region 호스트

	IEnumerator RunHost(bool withMember)
	{
		if (!NetworkServer.active) NetworkManager.singleton.StartHost();
		yield return Wait("호스트로 게임에 들어감", () => NetworkClient.localPlayer != null && HostScene() == PlaceScenes.Lobby);
		// PlaceTravelClient는 접속 직후 로딩을 끝낸 뒤에야 이동 요청을 받는다
		yield return new WaitForSeconds(1.5f);

		PlayerPublicInfo info = NetworkClient.localPlayer.GetComponent<PlayerPublicInfo>();
		Check("플레이어에 PlayerPublicInfo가 있음", info != null);
		if (info == null) yield break;

		Check("처음 장소는 로비", info.Place == PlaceScenes.Lobby);
		// 서버가 가진 계정의 닉네임. 호스트의 클라이언트가 서버에서 가진 연결은 NetworkServer.localConnection이다
		string expectedNickname = NetworkServer.localConnection.authenticationData is AccountSession session ? session.Nickname : "";
		Check($"닉네임이 접속한 계정의 닉네임 ('{info.Nickname}')", info.Nickname == expectedNickname);

		if (withMember)
		{
			yield return Wait("클라이언트가 로비에 들어옴", () => FindMember() != null);
			NetworkConnectionToClient member = FindMember();
			PlayerPublicInfo memberInfo = member.identity.GetComponent<PlayerPublicInfo>();
			string memberNickname = member.authenticationData is AccountSession memberSession ? memberSession.Nickname : "";
			Check($"클라이언트 플레이어의 장소는 로비 ('{memberInfo.Place}')", memberInfo.Place == PlaceScenes.Lobby);
			Check($"클라이언트 플레이어의 닉네임이 그 계정의 닉네임 ('{memberInfo.Nickname}')", memberNickname != "" && memberInfo.Nickname == memberNickname);
			Check("두 플레이어의 닉네임이 다름", memberInfo.Nickname != info.Nickname);

			// member가 사냥터로 옮기면 서버가 가진 값도 바뀐다
			yield return Wait("클라이언트가 사냥터로 옮김", () => memberInfo.Place == PlaceScenes.HuntingGround);
			Check("클라이언트가 옮겨도 닉네임은 그대로", memberInfo.Nickname == memberNickname);
			Check("호스트 플레이어의 장소는 그대로 로비", info.Place == PlaceScenes.Lobby);
		}

		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("사냥터에 도착", () => HostScene() == PlaceScenes.HuntingGround);
		Check("사냥터로 옮기면 장소가 사냥터", info.Place == PlaceScenes.HuntingGround);
		Check("옮겨도 닉네임은 그대로", info.Nickname == expectedNickname);
		// 답(TravelEndedMessage)을 받기 전의 새 요청은 무시되므로 기다린다
		yield return new WaitForSeconds(1.5f);

		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
		yield return Wait("로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
		Check("로비로 돌아오면 장소가 로비", info.Place == PlaceScenes.Lobby);

		if (withMember) yield return new WaitForSeconds(MemberStaySeconds);
	}

	// 호스트에서 로컬 플레이어는 서버의 플레이어와 같은 오브젝트라 장소 씬에 있다
	static string HostScene() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject.scene.name : null;

	// 호스트가 아닌 클라이언트의 연결. 플레이어가 아직 없으면 null
	static NetworkConnectionToClient FindMember()
	{
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
			if (!(conn is LocalConnectionToClient) && conn.identity != null) return conn;
		return null;
	}

	#endregion

	#region 클라이언트

	IEnumerator RunMember()
	{
		yield return Wait("NetworkManager", () => NetworkManager.singleton != null);
		NetworkManager.singleton.networkAddress = GetArg(AddressArg) ?? "localhost";
		NetworkManager.singleton.StartClient();
		yield return Wait("접속", () => NetworkClient.isConnected);

		NetworkClient.Send(new AccountAuthRequest { skipLogin = true });
		yield return Wait("로그인", () => NetworkClient.connection != null && NetworkClient.connection.isAuthenticated);
		InvokeAuthenticator.Current.RequestEnter(CharacterLanguages.C);
		yield return Wait("게임에 들어감", () => NetworkClient.localPlayer != null);

		// 클라이언트의 플레이어는 늘 Base 씬에 있으므로 씬 이름으로는 장소를 알 수 없다. 서버가 보낸 Place를 본다
		PlayerPublicInfo mine = NetworkClient.localPlayer.GetComponent<PlayerPublicInfo>();
		yield return Wait("내 장소가 로비로 도착", () => mine.Place == PlaceScenes.Lobby);
		Check($"내 플레이어의 씬은 Base (씬 이름으로는 장소를 모름, '{NetworkClient.localPlayer.gameObject.scene.name}')", NetworkClient.localPlayer.gameObject.scene.name != PlaceScenes.Lobby);
		Check($"내 닉네임이 도착함 ('{mine.Nickname}')", !string.IsNullOrEmpty(mine.Nickname));

		// 같은 로비에 있는 호스트의 플레이어가 보이고, 그 값도 받는다
		PlayerPublicInfo host = null;
		yield return Wait("호스트의 플레이어가 보임", () => (host = FindOther(mine)) != null);
		if (host != null)
		{
			Check($"호스트의 장소가 로비 ('{host.Place}')", host.Place == PlaceScenes.Lobby);
			Check($"호스트의 닉네임이 도착함 ('{host.Nickname}')", !string.IsNullOrEmpty(host.Nickname) && host.Nickname != mine.Nickname);
		}

		// 접속 직후 로딩이 끝나기 전의 이동 요청은 무시되므로 기다린다. host도 같은 때에 값을 확인한다
		yield return new WaitForSeconds(3f);
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("내 장소가 사냥터로 바뀜", () => mine.Place == PlaceScenes.HuntingGround);
		Check("옮겨도 내 닉네임은 그대로", !string.IsNullOrEmpty(mine.Nickname));

		yield return new WaitForSeconds(MemberStaySeconds);
	}

	// 이 화면에 보이는 다른 플레이어의 PlayerPublicInfo
	static PlayerPublicInfo FindOther(PlayerPublicInfo mine)
	{
		foreach (NetworkIdentity identity in NetworkClient.spawned.Values)
			if (identity != null && identity != NetworkClient.localPlayer && identity.TryGetComponent(out PlayerPublicInfo other) && other != mine) return other;
		return null;
	}

	#endregion

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (condition()) Pass(label);
		else Fail($"{label}: 시간 초과");
	}

	void Check(string step, bool ok)
	{
		if (failedCount > 0) return;
		if (ok) Pass(step);
		else Fail(step);
	}

	static string GetArg(string name)
	{
		string[] args = Environment.GetCommandLineArgs();
		int i = Array.IndexOf(args, name);
		return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
	}

	void Pass(string step) => Log($"통과: {step}");

	void Fail(string step)
	{
		failedCount++;
		Debug.LogError($"[PlayerPublicInfoTest][{role}] 실패: {step}");
	}

	void Log(string message) => Debug.Log($"[PlayerPublicInfoTest][{role}] {message}");
}
