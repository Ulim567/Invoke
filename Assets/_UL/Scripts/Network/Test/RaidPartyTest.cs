using System;
using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 파티 레이드(#300) 확인용 테스트. 에디터 호스트 하나로, 또는 에디터 호스트와 시험 빌드 클라이언트 하나로 돌린다.
/// <para>
/// 사용법<br/>
/// - 호스트만: Base 씬에서 Play → 이 컴포넌트를 eval로 시작한다 (<c>RaidPartyTest.StartInEditor(false)</c>). 서버가 꺼져 있으면 호스트로 켠다.<br/>
/// - 호스트 + 클라이언트: <c>RaidPartyTest.StartInEditor(true)</c>로 시작한 뒤, 시험 빌드를 <c>-raidPartyTest member</c>로 실행한다.
/// 서버 주소는 <c>-raidPartyTestAddress 주소</c>로 바꾼다 (기본 localhost).<br/>
/// - 인자가 없으면 아무것도 하지 않으므로 씬에 놓지 않는다.
/// </para>
/// <para>
/// - host: 직접 레이드 던전 요청 거절, 로비에서 보낸 레이드 포기 무시 → [클리어] 방 만들기·시작 → 던전 도착 → 보스를 바로 죽여 클리어 → 로비로 돌아옴 → 던전 내림
/// → [포기] 한 판 더 시작해 포기(호스트만이면 호스트가, 클라이언트가 있으면 클라이언트가)하면 모두 로비로 돌아오는지 본다
/// → [이탈] 클라이언트가 있으면 한 판 더 시작하고, 클라이언트가 던전에서 접속을 끊으면(파티원 이탈) 로비로 돌아오는지 본다.<br/>
/// - member: 방에 들어가 준비 → 던전 도착 → 서버의 결과 메시지 → 로비로 돌아옴 → 다시 들어가 던전에서 포기 → 로비로 돌아옴 → 다시 들어가 던전에 도착하면 접속을 끊는다.<br/>
/// - 「[RaidPartyTest][역할] 전체 통과」면 통과.
/// </para>
/// </summary>
public class RaidPartyTest : MonoBehaviour
{
	const string RoleArg = "-raidPartyTest";
	const string AddressArg = "-raidPartyTestAddress";
	const string RoomName = "RaidPartyTest";
	// 결과 화면(RaidJudge Result Delay 3초)과 이동 시간을 넉넉히 기다린다
	const float TimeoutSeconds = 30f;

	// 한 판을 어떻게 끝내는지
	enum Round { Clear, GiveUp, Leave }

	string role;
	int failedCount;
	// member: 서버의 결과 메시지를 받았다는 RaidParties의 로그를 보았으면 true
	bool resultReceived;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	static void CreateIfRequested()
	{
		string role = GetArg(RoleArg);
		if (role != null) Create(role);
	}

	/// <summary>
	/// 에디터 Play 모드에서 eval로 부른다. withMember면 시험 빌드 클라이언트(member) 하나가 함께 하는 것으로 본다.
	/// </summary>
	public static void StartInEditor(bool withMember) => Create(withMember ? "host+member" : "host");

	static void Create(string role)
	{
		var go = new GameObject(nameof(RaidPartyTest));
		DontDestroyOnLoad(go);
		go.AddComponent<RaidPartyTest>().role = role;
	}

	IEnumerator Start()
	{
		Application.logMessageReceived += OnLog;

		if (role == "host" || role == "host+member") yield return RunHost(role == "host+member");
		else if (role == "member") yield return RunMember();
		else Fail($"모르는 역할: {role}");

		Application.logMessageReceived -= OnLog;
		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
		if (role == "member") Application.Quit();
	}

	void OnLog(string message, string stackTrace, LogType type)
	{
		if (message.StartsWith("[RaidParties] 서버의 레이드 결과")) resultReceived = true;
	}

	#region 호스트

	IEnumerator RunHost(bool withMember)
	{
		if (!NetworkServer.active) NetworkManager.singleton.StartHost();
		yield return Wait("호스트로 게임에 들어감", () => NetworkClient.localPlayer != null && RaidRooms.MyName != "나" && HostScene() == PlaceScenes.Lobby);
		yield return Wait("함수 데이터 준비", () => !NetworkClient.localPlayer.TryGetComponent(out FunctionExecutorNetwork n) || n.IsDataReady);
		// PlaceTravelClient는 데이터 준비를 다음 Update에 보고 접속 직후 로딩을 끝낸다. 그 전의 이동 요청은 무시된다
		yield return new WaitForSeconds(1f);

		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.RaidDungeon);
		yield return new WaitForSeconds(1.5f);
		Check("직접 보낸 레이드 던전 이동 요청은 거절되어 로비에 남음", HostScene() == PlaceScenes.Lobby && LoadedCount(PlaceScenes.RaidDungeon) == 0);

		RaidParties.RequestGiveUp();
		yield return new WaitForSeconds(1f);
		Check("로비에서 보낸 레이드 포기는 무시되어 로비에 남음", HostScene() == PlaceScenes.Lobby && LoadedCount(PlaceScenes.RaidDungeon) == 0);

		yield return HostRound(withMember, Round.Clear);
		yield return HostRound(withMember, Round.GiveUp);
		if (withMember) yield return HostRound(true, Round.Leave);
	}

	static string RoundLabel(Round kind) => kind == Round.Clear ? "[클리어] " : kind == Round.GiveUp ? "[포기] " : "[이탈] ";

	// 한 판: 방 만들기 → (파티원 기다림) → 시작 → 던전 → 보스 처치, 레이드 포기 또는 파티원 이탈 → 로비
	IEnumerator HostRound(bool withMember, Round kind)
	{
		if (failedCount > 0) yield break;
		string round = RoundLabel(kind);

		RaidRooms.RequestCreate(RoomName, "");
		yield return Wait(round + "방 만들기", () => RaidRooms.FindRoomOf(RaidRooms.MyName) != null);
		RaidRoom room = RaidRooms.FindRoomOf(RaidRooms.MyName);
		if (room == null) yield break;
		if (withMember) yield return Wait(round + "파티원 입장·준비", () => room.Members.Count == 2 && room.Members[1].IsReady);

		RaidRooms.RequestStart();
		yield return Wait(round + "레이드 던전에 도착", () => HostScene() == PlaceScenes.RaidDungeon);
		Check(round + "던전은 하나만 불러옴", LoadedCount(PlaceScenes.RaidDungeon) == 1);
		if (withMember) yield return Wait(round + "파티원도 같은 던전에 도착", () => PlayersInHostScene() == 2);

		if (kind == Round.Leave)
		{
			yield return Wait(round + "파티원이 접속을 끊으면 로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
		}
		else if (kind == Round.GiveUp)
		{
			// 클라이언트가 있으면 클라이언트가 포기한다(서버로 보내는 메시지를 시험). 호스트만이면 호스트가 포기한다
			if (!withMember)
			{
				yield return new WaitForSeconds(1f);
				RaidParties.RequestGiveUp();
			}
			string who = withMember ? "파티원이" : "호스트가";
			yield return Wait(round + who + " 포기하면 로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
			if (withMember) yield return Wait(round + "포기한 파티원도 로비로 돌아옴", () => PlayersInScene(PlaceScenes.Lobby) == 2);
		}
		else
		{
			Monster boss = null;
			yield return Wait(round + "보스 생성", () => (boss = FindBossInHostScene()) != null);
			if (boss == null) yield break;
			boss.TakeDamage(boss.MaxHp);
			yield return Wait(round + "보스를 죽이면 결과 화면 뒤 로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
			if (withMember) yield return Wait(round + "파티원도 로비로 돌아옴", () => PlayersInScene(PlaceScenes.Lobby) == 2);
		}
		yield return Wait(round + "던전을 내림", () => LoadedCount(PlaceScenes.RaidDungeon) == 0);
	}

	// 호스트에서 로컬 플레이어는 서버의 플레이어와 같은 오브젝트라 장소 씬에 있다
	static string HostScene() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject.scene.name : null;

	static int PlayersInHostScene()
	{
		string scene = HostScene();
		Scene hostScene = NetworkClient.localPlayer.gameObject.scene;
		int count = 0;
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
			if (conn.identity != null && conn.identity.gameObject.scene == hostScene) count++;
		return scene != null ? count : 0;
	}

	static int PlayersInScene(string sceneName)
	{
		int count = 0;
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
			if (conn.identity != null && conn.identity.gameObject.scene.name == sceneName) count++;
		return count;
	}

	static Monster FindBossInHostScene()
	{
		foreach (GameObject root in NetworkClient.localPlayer.gameObject.scene.GetRootGameObjects())
		{
			RaidBossSpawner spawner = root.GetComponentInChildren<RaidBossSpawner>();
			if (spawner != null) return spawner.Boss;
		}
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
		yield return Wait("게임에 들어감", () => NetworkClient.localPlayer != null && RaidRooms.MyName != "나" && IsLoaded(PlaceScenes.Lobby));

		yield return MemberRound(Round.Clear);
		yield return MemberRound(Round.GiveUp);
		yield return MemberRound(Round.Leave);
	}

	IEnumerator MemberRound(Round kind)
	{
		if (failedCount > 0) yield break;
		string round = RoundLabel(kind);

		yield return Wait(round + "호스트의 방이 보임", () => FindRoom(RoomName) != null);
		RaidRoom room = FindRoom(RoomName);
		if (room == null) yield break;
		RaidRooms.RequestJoin(room, "");
		yield return Wait(round + "입장", () => RaidRooms.FindRoomOf(RaidRooms.MyName) == room);
		RaidRooms.RequestSetReady(true);

		yield return Wait(round + "레이드 던전에 도착 (로비는 내림)", () => IsLoaded(PlaceScenes.RaidDungeon) && !IsLoaded(PlaceScenes.Lobby));

		if (kind == Round.Leave)
		{
			// 호스트가 도착을 본 뒤에 끊는다
			yield return new WaitForSeconds(2f);
			Pass(round + "던전에서 접속을 끊음");
			yield break;
		}
		if (kind == Round.GiveUp)
		{
			// 호스트가 도착을 본 뒤에 포기한다
			yield return new WaitForSeconds(2f);
			RaidParties.RequestGiveUp();
			yield return Wait(round + "포기하면 로비로 돌아옴 (던전은 내림)", () => IsLoaded(PlaceScenes.Lobby) && !IsLoaded(PlaceScenes.RaidDungeon));
			yield break;
		}

		resultReceived = false;
		yield return Wait(round + "서버의 결과 메시지를 받음", () => resultReceived);
		yield return Wait(round + "로비로 돌아옴 (던전은 내림)", () => IsLoaded(PlaceScenes.Lobby) && !IsLoaded(PlaceScenes.RaidDungeon));
	}

	static RaidRoom FindRoom(string roomName)
	{
		foreach (RaidRoom room in RaidRooms.Rooms)
			if (room.Name == roomName) return room;
		return null;
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
		Debug.LogError($"[RaidPartyTest][{role}] 실패: {step}");
	}

	void Log(string message) => Debug.Log($"[RaidPartyTest][{role}] {message}");
}
