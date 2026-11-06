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
/// → [포기] 한 판 더 시작해 포기(호스트만이면 호스트가, 클라이언트가 있으면 클라이언트가)하면 실패 결과 화면 뒤(Result Delay 이상 지나서) 모두 로비로 돌아오는지 본다
/// → [이탈] 클라이언트가 있으면 한 판 더 시작하고, 클라이언트가 던전에서 접속을 끊으면(파티원 이탈) 실패 결과 화면 뒤 로비로 돌아오는지 본다.<br/>
/// - [클리어] 판의 보스: 호스트가 먼저 100을 주고 죽인다. member는 보스 몸에 자기 캐릭터를 겹쳐도 이 화면에서 따로 접촉 피해를 받지 않는지, 보스 체력·피격·사망 연출이 서버와 같은지 본다 (#310).<br/>
/// - member: 방에 들어가 준비 → 던전 도착 → 보스 동기화 확인 → 클리어 결과 화면 이벤트 → 로비로 돌아옴 → 다시 들어가 던전에서 포기 → 실패 결과 화면 이벤트 뒤 로비로 돌아옴 → 다시 들어가 던전에 도착하면 접속을 끊는다.<br/>
/// - 결과 화면은 그 던전의 RaidJudge.ResultEffectPlayed로 확인한다 (클라이언트에서는 RaidParties가 서버의 결과로 PlayResultOnly를 부를 때 불림).<br/>
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
	// 결과 화면 이벤트부터 로비 도착까지 걸려야 하는 최소 시간. RaidJudge의 Result Delay(3초)에서 프레임 오차만 뺀다
	const float MinResultSeconds = 2.9f;
	// [클리어] 판에서 호스트가 보스를 죽이기 전에 먼저 주는 데미지. 클라이언트가 체력·피격 연출 동기화를 확인한다
	const int FirstHitDamage = 100;
	// 클라이언트가 보스를 찾고 자기 캐릭터를 보스 몸에 겹쳐 확인하는 동안 호스트가 기다리는 시간
	const float MemberCheckSeconds = 4f;
	// 클라이언트가 자기 캐릭터를 보스 몸에 겹쳐 두는 시간
	const float ContactSeconds = 1.5f;

	// 한 판을 어떻게 끝내는지
	enum Round { Clear, GiveUp, Leave }

	string role;
	int failedCount;
	// 지금 던전의 RaidJudge와, 그 결과 화면 이벤트로 받은 값(받기 전에는 null)과 받은 시각
	RaidJudge watchedJudge;
	bool? result;
	float resultTime;
	// member: 클라이언트의 보스에서 받은 피격 연출 데미지(받기 전에는 0)와 사망 연출
	int bossHitDamage;
	bool bossDeathPlayed;

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
		// 던전 씬이 불러와지는 순간 구독한다. 클라이언트는 불러오는 동안 미뤄 둔 결과 메시지를 불러온 직후 처리하므로, 코루틴에서 기다렸다 구독하면 놓칠 수 있다
		SceneManager.sceneLoaded += OnSceneLoaded;

		if (role == "host" || role == "host+member") yield return RunHost(role == "host+member");
		else if (role == "member") yield return RunMember();
		else Fail($"모르는 역할: {role}");

		SceneManager.sceneLoaded -= OnSceneLoaded;
		WatchJudge(null);
		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
		if (role == "member") Application.Quit();
	}

	void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		if (scene.name != PlaceScenes.RaidDungeon) return;
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			RaidJudge judge = root.GetComponentInChildren<RaidJudge>(true);
			if (judge == null) continue;
			WatchJudge(judge);
			return;
		}
	}

	// 새 던전의 RaidJudge 결과 화면 이벤트를 받는다. 이전 판의 결과는 버린다
	void WatchJudge(RaidJudge judge)
	{
		if (watchedJudge != null) watchedJudge.ResultEffectPlayed -= OnResult;
		watchedJudge = judge;
		result = null;
		if (watchedJudge != null) watchedJudge.ResultEffectPlayed += OnResult;
	}

	void OnResult(bool cleared)
	{
		result = cleared;
		resultTime = Time.realtimeSinceStartup;
	}

	// 실패 결과 화면 이벤트를 기다린 뒤, 결과 화면이 보이는 시간(Result Delay)이 지나서 로비에 왔는지 본다
	IEnumerator WaitFailResultThenLobby(string round, Func<bool> inLobby)
	{
		yield return Wait(round + "실패 결과 화면", () => result == false);
		yield return Wait(round + "결과 화면 뒤 로비로 돌아옴", inLobby);
		if (failedCount > 0) yield break;
		float shown = Time.realtimeSinceStartup - resultTime;
		Check($"{round}결과 화면이 {MinResultSeconds}초 이상 보인 뒤 이동 ({shown:0.00}초)", shown >= MinResultSeconds);
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
			yield return WaitFailResultThenLobby(round + "파티원이 접속을 끊으면 ", () => HostScene() == PlaceScenes.Lobby);
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
			yield return WaitFailResultThenLobby(round + who + " 포기하면 ", () => HostScene() == PlaceScenes.Lobby);
			if (withMember) yield return Wait(round + "포기한 파티원도 로비로 돌아옴", () => PlayersInScene(PlaceScenes.Lobby) == 2);
		}
		else
		{
			Monster boss = null;
			yield return Wait(round + "보스 생성", () => (boss = FindBossInHostScene()) != null);
			if (boss == null) yield break;
			// 클라이언트가 보스를 찾아 확인할 시간을 준 뒤 먼저 조금 때린다 (몬스터 체력·피격 연출 동기화, #310)
			if (withMember) yield return new WaitForSeconds(MemberCheckSeconds);
			boss.TakeDamage(FirstHitDamage);
			Check(round + $"보스에게 {FirstHitDamage} 데미지 (서버 체력)", boss.Hp == boss.MaxHp - FirstHitDamage);
			yield return new WaitForSeconds(1.5f);
			boss.TakeDamage(boss.Hp);
			yield return Wait(round + "보스를 죽이면 클리어 결과 화면", () => result == true);
			yield return Wait(round + "결과 화면 뒤 로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
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
			yield return WaitFailResultThenLobby(round + "포기하면 ", () => IsLoaded(PlaceScenes.Lobby) && !IsLoaded(PlaceScenes.RaidDungeon));
			yield break;
		}

		yield return CheckBossSync(round);
		yield return Wait(round + "서버의 결과로 클리어 결과 화면", () => result == true);
		yield return Wait(round + "로비로 돌아옴 (던전은 내림)", () => IsLoaded(PlaceScenes.Lobby) && !IsLoaded(PlaceScenes.RaidDungeon));
	}

	// [클리어] 판 클라이언트: 서버가 만든 보스의 체력·피격·사망 연출이 이 화면에도 오는지 본다 (#310).
	// 그 전에 자기 캐릭터를 보스 몸에 겹쳐, 이 화면의 보스 복사본이 따로 접촉 피해를 주지 않는지도 본다
	IEnumerator CheckBossSync(string round)
	{
		Monster boss = null;
		yield return Wait(round + "보스가 이 화면에 보임", () => (boss = FindBossInDungeon()) != null);
		if (boss == null) yield break;

		bossHitDamage = 0;
		bossDeathPlayed = false;
		boss.HitEffectPlayed += OnBossHit;
		boss.DeathEffectPlayed += OnBossDeath;
		Check(round + "보스 체력을 서버에서 받음", boss.Hp == boss.MaxHp);

		// 플레이어 체력 동기화 전이라, 서버가 준 접촉 피해는 이 화면의 체력에 보이지 않는다. 이 화면에서 따로 깎지 않았으면 그대로 최대다
		GameObject me = NetworkClient.localPlayer.gameObject;
		PlayerHealth health = me.GetComponent<PlayerHealth>();
		Rigidbody2D body = me.GetComponent<Rigidbody2D>();
		float until = Time.time + ContactSeconds;
		while (Time.time < until)
		{
			body.position = boss.transform.position;
			yield return new WaitForFixedUpdate();
		}
		Check(round + "보스 몸에 겹쳐도 이 화면에서 따로 접촉 피해를 받지 않음", health.Hp == health.MaxHp);

		yield return Wait(round + $"보스 피격 연출 ({FirstHitDamage} 데미지)", () => bossHitDamage == FirstHitDamage);
		Check(round + "보스 체력이 서버와 같음", boss != null && boss.Hp == boss.MaxHp - FirstHitDamage);
		yield return Wait(round + "보스 사망 연출", () => bossDeathPlayed);
		if (boss != null)
		{
			boss.HitEffectPlayed -= OnBossHit;
			boss.DeathEffectPlayed -= OnBossDeath;
		}
	}

	void OnBossHit(int damage) => bossHitDamage = damage;

	void OnBossDeath() => bossDeathPlayed = true;

	// 클라이언트에서는 Mirror가 네트워크 오브젝트를 Base 씬에 만들므로 씬으로 거르지 않는다.
	// 클라이언트는 같은 씬(자기 던전)의 네트워크 오브젝트만 받으므로 보이는 보스는 자기 던전의 보스다
	static Monster FindBossInDungeon()
	{
		foreach (Monster m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
			if (m.IsBoss) return m;
		return null;
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
