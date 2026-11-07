using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 연습장을 플레이어마다 따로 여는지(#301) 확인하는 테스트. 에디터 호스트 하나로, 또는 에디터 호스트와 시험 빌드 클라이언트 하나로 돌린다.
/// <para>
/// 사용법<br/>
/// - 호스트만: Base 씬에서 Play → eval로 <c>TrainingRoomTest.StartInEditor(false)</c>. 서버가 꺼져 있으면 호스트로 켠다.<br/>
/// - 호스트 + 클라이언트: <c>TrainingRoomTest.StartInEditor(true)</c>로 시작한 뒤, 시험 빌드를 <c>-trainingRoomTest member</c>로 실행한다.
/// 서버 주소는 <c>-trainingRoomTestAddress 주소</c>로 바꾼다 (기본 localhost).<br/>
/// - 인자가 없으면 아무것도 하지 않으므로 씬에 놓지 않는다.
/// </para>
/// <para>
/// - host: 입장 전에는 연습장 씬이 없음 → 입장하면 하나 열림 → (클라이언트가 있으면) 클라이언트도 입장해 연습장이 둘이 되고,
/// 서로 다른 씬이며 서로의 observer가 아님 → 로비로 나가면 내 연습장만 내림 → (클라이언트가 있으면) 클라이언트가 접속을 끊으면 그 연습장도 내림
/// → 다시 입장하면 새 씬이 열림 → 연습장 안에서 플레이어가 없어지면(<c>DespawnPlayer</c>) 내림.<br/>
/// - member: 접속 → 로비 → 연습장에 입장 → 잠시 머문 뒤 접속을 끊음.<br/>
/// - 「[TrainingRoomTest][역할] 전체 통과」면 통과.
/// </para>
/// </summary>
public class TrainingRoomTest : MonoBehaviour
{
	const string RoleArg = "-trainingRoomTest";
	const string AddressArg = "-trainingRoomTestAddress";
	// 씬 로드와 이동 시간을 넉넉히 기다린다
	const float TimeoutSeconds = 30f;
	// member가 연습장에 머무는 시간. host가 상태를 확인하고 나가는 시간보다 길어야 한다
	const float MemberStaySeconds = 10f;

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
		var go = new GameObject(nameof(TrainingRoomTest));
		DontDestroyOnLoad(go);
		go.AddComponent<TrainingRoomTest>().role = role;
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
		yield return Wait("함수 데이터 준비", () => !NetworkClient.localPlayer.TryGetComponent(out FunctionExecutorNetwork n) || n.IsDataReady);
		if (withMember) yield return Wait("클라이언트가 로비에 들어옴", () => FindMember() != null);
		// PlaceTravelClient는 데이터 준비를 다음 Update에 보고 접속 직후 로딩을 끝낸다. 그 전의 이동 요청은 무시된다
		yield return new WaitForSeconds(1f);

		Check("입장 전에는 연습장 씬이 없음", LoadedCount(PlaceScenes.TrainingRoom) == 0);

		// 첫 입장 (클라이언트가 있으면 클라이언트도 거의 같은 때에 입장을 요청하므로 불러오기 대기열도 함께 확인한다)
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.TrainingRoom);
		yield return Wait("연습장에 도착", () => HostScene() == PlaceScenes.TrainingRoom);
		Scene first = NetworkClient.localPlayer.gameObject.scene;

		if (withMember)
		{
			yield return Wait("클라이언트도 연습장에 도착", () => MemberScene().name == PlaceScenes.TrainingRoom);
			Check("연습장 씬이 둘 열림", LoadedCount(PlaceScenes.TrainingRoom) == 2);
			Check("두 플레이어는 서로 다른 연습장 씬에 있음", MemberScene() != first);
			NetworkConnectionToClient member = FindMember();
			Check("서로의 observer가 아님 (서로 보이지 않음)",
				!NetworkClient.localPlayer.observers.ContainsKey(member.connectionId) && !member.identity.observers.ContainsKey(NetworkServer.localConnection.connectionId));
		}
		else
		{
			Check("연습장 씬이 하나 열림", LoadedCount(PlaceScenes.TrainingRoom) == 1);
		}

		// PlaceTravelClient는 이동의 답(TravelEndedMessage)을 받기 전의 새 요청을 무시하므로 답이 올 때까지 기다린다
		yield return new WaitForSeconds(1.5f);

		// 나가기
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
		yield return Wait("로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
		int expectedAfterLeave = withMember ? 1 : 0;
		yield return Wait("내가 나가면 내 연습장만 내림", () => LoadedCount(PlaceScenes.TrainingRoom) == expectedAfterLeave);

		if (withMember)
		{
			Check("클라이언트는 자기 연습장에 그대로 있음", FindMember() != null && MemberScene().name == PlaceScenes.TrainingRoom);
			yield return Wait("클라이언트가 접속을 끊으면 그 연습장도 내림", () => LoadedCount(PlaceScenes.TrainingRoom) == 0);
		}

		// 다시 입장: 이전 씬이 아니라 새 씬이어야 한다
		yield return new WaitForSeconds(1.5f);
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.TrainingRoom);
		yield return Wait("다시 연습장에 도착", () => HostScene() == PlaceScenes.TrainingRoom);
		Scene second = NetworkClient.localPlayer.gameObject.scene;
		Check("다시 입장하면 새 연습장 씬이 열림", LoadedCount(PlaceScenes.TrainingRoom) == 1 && second != first);
		yield return new WaitForSeconds(1.5f);

		// 연습장 안에서 플레이어가 없어지면 (같은 계정의 중복 로그인으로 쫓겨나는 것과 같은 경로) 씬을 내린다
		var manager = (InvokeNetworkManager)NetworkManager.singleton;
		manager.DespawnPlayer(NetworkServer.localConnection);
		yield return Wait("연습장 안에서 플레이어가 없어지면 씬을 내림", () => LoadedCount(PlaceScenes.TrainingRoom) == 0);
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

	static Scene MemberScene()
	{
		NetworkConnectionToClient member = FindMember();
		return member != null ? member.identity.gameObject.scene : default;
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
		yield return Wait("게임에 들어감", () => NetworkClient.localPlayer != null && IsLoaded(PlaceScenes.Lobby));

		// 접속 직후 로딩이 끝나기 전의 이동 요청은 무시되므로 기다린다. host도 같은 때에 입장을 요청한다
		yield return new WaitForSeconds(3f);
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.TrainingRoom);
		yield return Wait("연습장에 도착 (로비는 내림)", () => IsLoaded(PlaceScenes.TrainingRoom) && !IsLoaded(PlaceScenes.Lobby));

		yield return new WaitForSeconds(MemberStaySeconds);
		Pass("연습장에서 접속을 끊음");
	}

	#endregion

	static bool IsLoaded(string sceneName) => LoadedCount(sceneName) > 0;

	static int LoadedCount(string sceneName)
	{
		int count = 0;
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (scene.isLoaded && scene.name == sceneName) count++;
		}
		return count;
	}

	IEnumerator Wait(string step, Func<bool> condition)
	{
		if (failedCount > 0) yield break;

		float until = Time.realtimeSinceStartup + TimeoutSeconds;
		while (!condition())
		{
			if (Time.realtimeSinceStartup > until)
			{
				Fail($"{step}: {TimeoutSeconds}초 안에 되지 않음");
				yield break;
			}
			yield return null;
		}
		Pass(step);
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
		Debug.LogError($"[TrainingRoomTest][{role}] 실패: {step}");
	}

	void Log(string message) => Debug.Log($"[TrainingRoomTest][{role}] {message}");
}
