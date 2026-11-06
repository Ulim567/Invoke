using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
/// - [클리어] 판의 보스: 호스트가 먼저 100을 주고, 2페이즈로 깎아 전체 공격 모으기를 낸 뒤(터지기 전에) 죽인다. member는 보스 몸에 자기 캐릭터를 겹쳐도 이 화면에서 따로 접촉 피해를 받지 않는지,
/// 보스 체력·피격·사망 연출, 공격 연출, 모으기 연출이 이 화면에도 오는지 본다. 호스트가 웜 소환을 직접 부르고, member는 웜이 움직이며 플레이어 쪽을 보는지, 보스가 플레이어 쪽을 보는지 본다 (#310).<br/>
/// - [포기] 판(클라이언트가 있을 때): 호스트가 보스를 40% 아래로 깎아 3페이즈로 보낸다. member는 분신 2마리, 진짜의 순간이동(10칸 간격), 같은 방향을 본 뒤 포기한다 (#310).<br/>
/// - [사냥터 부활] 판 (#310, [포기]와 [이탈] 사이): 사냥터에서 죽은 캐릭터(호스트만이면 호스트, 아니면 클라이언트)가 부활하면서 로비로 옮겨지는지, 체력이 최대인지 본다.<br/>
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
	// 호스트가 서버에서 파티원 캐릭터에게 주는 데미지. 몬스터 공격(10, 12, 15, 20, 25, 60)에 없는 값이라 구분된다
	const int MemberHitDamage = 7;

	// 한 판을 어떻게 끝내는지
	enum Round { Clear, GiveUp, Leave, HuntRespawn }

	string role;
	int failedCount;
	// 지금 던전의 RaidJudge와, 그 결과 화면 이벤트로 받은 값(받기 전에는 null)과 받은 시각
	RaidJudge watchedJudge;
	bool? result;
	float resultTime;
	// member: 클라이언트의 보스에서 받은 피격 연출 데미지(받기 전에는 0)와 사망 연출
	int bossHitDamage;
	bool bossDeathPlayed;
	// member: 클라이언트의 보스에서 받은 공격 연출(던지기·낙하·때리기·소환)과 전체 공격 모으기 연출 (#310)
	bool bossAttackPlayed;
	bool bossChargePlayed;
	// host: 보스가 전체 공격을 모으기 시작했으면 true
	bool hostChargeSeen;
	// member: 웜이 처음 보인 x(보이기 전에는 null), 그 뒤 가장 멀리 옮겨 간 거리, 서버의 IsMoving이 true였던 적이 있는지 (#310)
	float? wormFirstX;
	float wormMaxMove;
	bool wormSawMoving;
	// member: 몬스터 투사체가 이 화면에 보였는지, 보인 뒤 가장 멀리 날아간 거리 (#310)
	bool projectileSeen;
	float projectileMaxMove;
	// member: 내 캐릭터가 받은 피격 데미지 합, 표시한 데미지(MemberHitDamage)를 받았는지, 사망·숨김·부활 연출 (#310)
	int myHitSum;
	bool myMarkedHit;
	bool myDeath;
	bool myHide;
	bool myRespawn;
	// member: 서버가 보낸 시전 거부 이유 (받기 전에는 null)
	CastRejection? myCastRejection;
	// member: 플레이어 목록(PlayerRoster)에서 빠지고 들어온 횟수 (#310)
	int rosterRemoved;
	int rosterAdded;

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
		yield return HostHuntRespawn(withMember);
		if (withMember) yield return HostRound(true, Round.Leave);
	}

	static string RoundLabel(Round kind) =>
		kind == Round.Clear ? "[클리어] " : kind == Round.GiveUp ? "[포기] " : kind == Round.Leave ? "[이탈] " : "[사냥터 부활] ";

	// [사냥터 부활] 판 (#310): 레이드 밖에서 죽으면 부활할 때 로비로 옮겨지는지 본다.
	// 호스트만이면 호스트가 사냥터로 가서 죽는다. 클라이언트가 있으면 클라이언트가 사냥터로 가고, 호스트가 서버에서 그 캐릭터를 죽인다
	IEnumerator HostHuntRespawn(bool withMember)
	{
		if (failedCount > 0) yield break;
		string round = RoundLabel(Round.HuntRespawn);

		PlayerHealth target;
		if (withMember)
		{
			yield return Wait(round + "파티원이 사냥터에 도착", () => PlayersInScene(PlaceScenes.HuntingGround) == 1);
			target = FindMemberHealth();
		}
		else
		{
			// 이전 판의 이동이 끝난 뒤에 요청한다 (이동 중의 요청은 무시됨)
			yield return new WaitForSeconds(1f);
			PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
			yield return Wait(round + "호스트가 사냥터에 도착", () => HostScene() == PlaceScenes.HuntingGround);
			target = NetworkClient.localPlayer.GetComponent<PlayerHealth>();
		}
		if (target == null) { Fail(round + "죽일 캐릭터를 찾지 못함"); yield break; }

		// 사냥터의 몬스터에게 맞아 무적이 된 순간이면 피해가 무시되므로, 죽을 때까지 무적이 아닐 때마다 남은 체력만큼 준다
		yield return Wait(round + "서버에서 캐릭터가 죽음", () => KillWhenHittable(target));

		if (withMember) yield return Wait(round + "부활한 파티원이 로비로 옮겨짐", () => PlayersInScene(PlaceScenes.Lobby) == 2);
		else yield return Wait(round + "부활한 호스트가 로비로 옮겨짐", () => HostScene() == PlaceScenes.Lobby);
		Check(round + "부활한 캐릭터의 체력이 최대", target != null && !target.IsDead && target.Hp == target.MaxHp);
	}

	// [사냥터 부활] 판 클라이언트: 사냥터로 가서 서버에서 죽은 뒤, 부활하면서 로비로 옮겨지는지 이 화면에서 본다 (#310)
	IEnumerator MemberHuntRespawn()
	{
		if (failedCount > 0) yield break;
		string round = RoundLabel(Round.HuntRespawn);

		// 이전 판의 이동이 끝난 뒤에 요청한다 (이동 중의 요청은 무시됨)
		yield return new WaitForSeconds(1f);
		PlayerHealth health = NetworkClient.localPlayer.GetComponent<PlayerHealth>();
		myDeath = myRespawn = false;
		health.DeathEffectPlayed += OnMyDeath;
		health.RespawnEffectPlayed += OnMyRespawn;

		// 이 화면의 플레이어 목록 (#310): 로비에는 호스트와 나 2명, 사냥터에는 나 혼자다. 장소를 옮기면 Mirror가 호스트 캐릭터를 없애고 다시 만든다
		yield return Wait(round + "플레이어 목록에서 로비의 2명 (PlayerRoster)", () => PlayerRoster.Players.Count == 2 && CountLocalInRoster() == 1);
		rosterRemoved = rosterAdded = 0;
		PlayerRoster.PlayerRemoved += OnRosterRemoved;
		PlayerRoster.PlayerAdded += OnRosterAdded;

		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait(round + "사냥터에 도착 (로비는 내림)", () => IsLoaded(PlaceScenes.HuntingGround) && !IsLoaded(PlaceScenes.Lobby));
		yield return Wait(round + "플레이어 목록에서 호스트가 빠짐 (PlayerRemoved)", () => PlayerRoster.Players.Count == 1 && CountLocalInRoster() == 1 && rosterRemoved >= 1);
		yield return Wait(round + "내 캐릭터 사망 연출", () => myDeath);
		yield return Wait(round + "내 캐릭터 부활 연출", () => myRespawn);
		yield return Wait(round + "부활하면서 로비로 옮겨짐 (사냥터는 내림)", () => IsLoaded(PlaceScenes.Lobby) && !IsLoaded(PlaceScenes.HuntingGround));
		yield return Wait(round + "플레이어 목록에 호스트가 다시 들어옴 (PlayerAdded)", () => PlayerRoster.Players.Count == 2 && rosterAdded >= 1);
		PlayerRoster.PlayerRemoved -= OnRosterRemoved;
		PlayerRoster.PlayerAdded -= OnRosterAdded;
		// 로딩 화면이 걷힌 뒤 다시 움직일 수 있어야 한다
		yield return Wait(round + "로비에서 다시 움직일 수 있음 (CanAct)", () => health != null && health.CanAct && health.Hp == health.MaxHp);

		if (health != null)
		{
			health.DeathEffectPlayed -= OnMyDeath;
			health.RespawnEffectPlayed -= OnMyRespawn;
		}
	}

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
		// 이 화면의 플레이어 목록 (#310): 호스트는 다른 장소의 플레이어도 갖고 있으므로 IsInMyPlace로 거른 수를 본다
		int expected = withMember ? 2 : 1;
		yield return Wait(round + $"플레이어 목록에서 같은 던전의 {expected}명 (PlayerRoster)", () => CountRosterInMyPlace() == expected);

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
			else
			{
				// 클라이언트가 보스를 찾은 뒤, 체력을 40% 아래로 한 번에 깎아 3페이즈(진짜 찾기)로 보낸다 (순간이동·분신 방향 동기화, #310).
				// 한 번에 여러 문턱을 넘으면 2페이즈 전체 공격 없이 3페이즈로 간다. 클라이언트는 분신을 확인한 뒤 포기한다
				Monster boss = null;
				yield return Wait(round + "보스 생성", () => (boss = FindBossInHostScene()) != null);
				if (boss == null) yield break;
				yield return new WaitForSeconds(2f);
				boss.TakeDamage(boss.Hp - Mathf.FloorToInt(boss.MaxHp * 0.35f));
				yield return Wait(round + "3페이즈에서 분신 2마리", () => CountClonesInHostScene() == 2);
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
			// 공격이 랜덤이라 웜이 생긴다고 보장할 수 없으므로 웜 소환을 직접 한 번 부른다 (웜 위치·방향 동기화, #310)
			typeof(GatewayAI).GetMethod("SpawnWorms", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boss.GetComponent<GatewayAI>(), null);
			Check(round + "웜 소환", CountWormsInHostScene() > 0);
			// 클라이언트가 보스를 찾아 확인할 시간을 준 뒤 먼저 조금 때린다 (몬스터 체력·피격 연출 동기화, #310)
			if (withMember) yield return new WaitForSeconds(MemberCheckSeconds);

			// 공격이 랜덤이라 던지기가 나온다고 보장할 수 없으므로 직접 한 번 던진다. 투사체가 Base가 아니라 던전 씬에 생겨야 던전 플레이어와 판정된다 (#310)
			ProjectileAttack thrower = boss.GetComponent<ProjectileAttack>();
			yield return Wait(round + "보스가 던질 수 있음", () => !thrower.IsCharging);
			thrower.Fire(Vector2.right);
			yield return Wait(round + "던진 패킷이 던전 씬에 생김", () => CountInHostScene<MonsterProjectile>() > 0);

			boss.TakeDamage(FirstHitDamage);
			Check(round + $"보스에게 {FirstHitDamage} 데미지 (서버 체력)", boss.Hp == boss.MaxHp - FirstHitDamage);
			yield return new WaitForSeconds(1.5f);

			// 서버에서 파티원 캐릭터에게 피해를 주고 죽여, 그 클라이언트 화면의 체력·사망·숨김이 맞는지 member가 본다 (플레이어 체력 동기화, #310).
			// 몬스터 공격에 없는 데미지(7)로 구분한다. 레이드 중이라 부활하지 않고, 레이드가 끝날 때 부활한다
			if (withMember)
			{
				PlayerHealth memberHealth = FindMemberHealth();
				yield return Wait(round + "파티원 캐릭터가 맞을 수 있음", () => memberHealth != null && !memberHealth.IsDead && !memberHealth.IsInvincible);
				if (memberHealth == null) yield break;
				memberHealth.TakeDamage(MemberHitDamage);
				yield return new WaitForSeconds(1.5f);
				yield return Wait(round + "파티원 캐릭터의 무적이 끝남", () => !memberHealth.IsInvincible);
				// 입력(대상)을 기다리며 계속 도는 함수를 서버에서 시전해 둔 뒤 바로 죽인다. 죽으면 실행 중인 함수가 모두 끊겨야 한다 (이우림 결정, #310)
				FunctionTask waitingTask = CastWaitingFunction(memberHealth.GetComponent<FunctionExecutor>());
				Check(round + "파티원 캐릭터가 입력을 기다리는 함수를 실행 중", waitingTask != null && !waitingTask.IsEnded);
				// 보스에게 맞아 무적이 된 순간이면 피해가 무시되므로, 죽을 때까지 무적이 아닐 때마다 남은 체력만큼 준다
				yield return Wait(round + "서버에서 파티원 캐릭터가 죽음", () => KillWhenHittable(memberHealth));
				yield return Wait(round + "죽으면 실행 중인 함수가 끊김 (Interrupted)", () => waitingTask == null || (waitingTask.IsEnded && waitingTask.EndReason == FunctionTaskEndReason.Interrupted));
				// 숨김(1초)까지 기다린다
				yield return new WaitForSeconds(1.5f);
			}

			// 체력을 2페이즈 문턱(70%) 아래로 깎아 전체 공격 모으기 연출을 낸다.
			// 터지기(모으기 3초) 전에 보스를 죽인다. 전체 공격과 보스 공격이 겹쳐 모두 죽으면 클리어가 아니라 실패가 되기 때문이다
			GatewayPhases phases = boss.GetComponent<GatewayPhases>();
			hostChargeSeen = false;
			phases.ChargeEffectPlayed += OnHostCharge;
			boss.TakeDamage(boss.Hp - Mathf.FloorToInt(boss.MaxHp * 0.65f));
			yield return Wait(round + "2페이즈에서 보스가 전체 공격을 모음", () => hostChargeSeen);
			phases.ChargeEffectPlayed -= OnHostCharge;
			// 클라이언트가 모으기 연출을 받을 시간
			yield return new WaitForSeconds(0.5f);
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

	static int CountWormsInHostScene() => CountInHostScene<GroundChaseAI>();

	static int CountClonesInHostScene() => CountInHostScene<CloneGlitch>();

	static int CountInHostScene<T>() where T : Component
	{
		Scene hostScene = NetworkClient.localPlayer.gameObject.scene;
		int count = 0;
		foreach (T c in FindObjectsByType<T>(FindObjectsSortMode.None))
			if (c.gameObject.scene == hostScene) count++;
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
		yield return MemberHuntRespawn();
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
			// 호스트가 3페이즈로 보내면 분신과 순간이동한 보스를 확인한 뒤 포기한다
			yield return CheckCloneSync(round);
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
		bossAttackPlayed = false;
		bossChargePlayed = false;
		boss.HitEffectPlayed += OnBossHit;
		boss.DeathEffectPlayed += OnBossDeath;
		GatewayAI ai = boss.GetComponent<GatewayAI>();
		GatewayPhases phases = boss.GetComponent<GatewayPhases>();
		ProjectileAttack thrower = boss.GetComponent<ProjectileAttack>();
		ai.SlamEffectPlayed += OnBossAttack;
		ai.SummonEffectPlayed += OnBossAttack;
		thrower.FireEffectPlayed += OnBossThrow;
		phases.ChargeEffectPlayed += OnBossCharge;
		Check(round + "보스 체력을 서버에서 받음", boss.Hp == boss.MaxHp);
		// 이 화면의 플레이어 목록 (#310): 던전 안의 나와 호스트 2명
		yield return Wait(round + "플레이어 목록에서 던전의 2명, 내 캐릭터 1명 (PlayerRoster)", () => PlayerRoster.Players.Count == 2 && CountLocalInRoster() == 1);
		// 보스는 가장 가까운 플레이어 쪽을 본다. 이 화면에서도 그쪽을 보는지 (방향 동기화)
		// 호스트가 소환한 웜과 던진 패킷을 지금부터 지켜본다
		StartCoroutine(WatchWorm());
		StartCoroutine(WatchProjectile());
		SpriteRenderer bossSprite = boss.GetComponent<SpriteRenderer>();
		yield return Wait(round + "보스가 가장 가까운 플레이어 쪽을 봄", () => boss != null && FacesNearestPlayer(boss.transform, bossSprite));

		// 내 캐릭터의 체력·사망·숨김·부활 알림을 받는다 (플레이어 체력 동기화, #310)
		GameObject me = NetworkClient.localPlayer.gameObject;
		PlayerHealth health = me.GetComponent<PlayerHealth>();
		Rigidbody2D body = me.GetComponent<Rigidbody2D>();
		myHitSum = 0;
		myMarkedHit = myDeath = myHide = myRespawn = false;
		health.HitEffectPlayed += OnMyHit;
		health.DeathEffectPlayed += OnMyDeath;
		health.HideEffectPlayed += OnMyHide;
		health.RespawnEffectPlayed += OnMyRespawn;
		// 보스가 이미 공격했을 수 있으므로 최대 체력만 본다
		Check(round + $"내 체력을 서버에서 받음 ({health.Hp}/{health.MaxHp})", health.MaxHp > 0 && health.Hp > 0);

		// 보스 몸에 겹친 동안 서버가 준 접촉 피해만 이 화면에 보여야 한다. 받은 피격 알림의 데미지 합만큼만 체력이 줄었는지 본다
		// (이 화면의 보스 복사본이 따로 피해를 주면 합보다 더 줄어든다)
		int startHp = health.Hp;
		float until = Time.time + ContactSeconds;
		while (Time.time < until)
		{
			body.position = boss.transform.position;
			yield return new WaitForFixedUpdate();
		}
		yield return new WaitForSeconds(0.3f);
		Check(round + $"보스 몸에 겹친 동안 서버의 피격 알림만큼만 체력이 줄어듦 ({startHp} → {health.Hp}, 알림 합 {myHitSum})", health.Hp == startHp - myHitSum);
		// 호스트가 이 캐릭터를 때리기 전에 보스 몸에서 떨어진다 (접촉 피해로 먼저 죽지 않게)
		body.position = (Vector2)boss.transform.position + new Vector2(-12f, 0f);

		yield return Wait(round + $"보스 피격 연출 ({FirstHitDamage} 데미지)", () => bossHitDamage == FirstHitDamage);
		Check(round + "보스 체력이 서버와 같음", boss != null && boss.Hp == boss.MaxHp - FirstHitDamage);

		yield return Wait(round + $"서버가 준 피해({MemberHitDamage})의 피격 연출이 내 화면에 옴", () => myMarkedHit);
		Check(round + "내 체력이 줄어듦", health.Hp < health.MaxHp);
		yield return Wait(round + "내 캐릭터 사망 연출", () => myDeath);
		Check(round + "죽은 동안 움직이거나 시전할 수 없음 (CanAct false)", health.IsDead && !health.CanAct);
		yield return Wait(round + "내 캐릭터 숨김(관전) 연출", () => myHide);
		Check(round + "숨겨진 상태 (IsHidden)", health.IsHidden);

		// 죽은 채로 시전을 요청하면 서버가 CannotAct로 거부한다 (#310). SlotCaster를 거치지 않고 바로 보내 서버의 검사를 본다
		FunctionExecutorNetwork net = me.GetComponent<FunctionExecutorNetwork>();
		myCastRejection = null;
		net.CastRejected += OnMyCastRejected;
		net.RequestCast(0);
		yield return Wait(round + "죽은 채로 시전하면 서버가 거부 (CannotAct)", () => myCastRejection == CastRejection.CannotAct);
		net.CastRejected -= OnMyCastRejected;
		yield return Wait(round + "전체 공격 모으기 연출", () => bossChargePlayed);
		yield return Wait(round + "보스 사망 연출", () => bossDeathPlayed);
		// 보스는 플레이어를 발견하고 2초 뒤부터 3초마다 공격하므로, 사망 전까지 공격 연출이 한 번 이상 왔어야 한다
		Check(round + "보스 공격 연출(던지기·낙하·때리기·소환)", bossAttackPlayed);
		// 호스트가 소환한 웜: 결과 화면(3초) 동안에도 플레이어를 쫓으므로 이때 본다
		yield return CheckWormSync(round);
		Check(round + "보스가 던진 패킷이 이 화면에 보임", projectileSeen);
		Check(round + $"패킷이 이 화면에서 날아감 (최대 {projectileMaxMove:0.00}칸)", projectileMaxMove > 0.5f);
		// 레이드가 끝나면 RaidJudge가 부활을 되돌려 죽어 있던 캐릭터가 부활한다
		yield return Wait(round + "레이드가 끝나면 내 캐릭터 부활 연출", () => myRespawn);
		Check(round + "부활한 뒤 체력이 최대", health != null && !health.IsDead && health.Hp == health.MaxHp);
		if (health != null)
		{
			health.HitEffectPlayed -= OnMyHit;
			health.DeathEffectPlayed -= OnMyDeath;
			health.HideEffectPlayed -= OnMyHide;
			health.RespawnEffectPlayed -= OnMyRespawn;
		}
		if (boss != null)
		{
			boss.HitEffectPlayed -= OnBossHit;
			boss.DeathEffectPlayed -= OnBossDeath;
			ai.SlamEffectPlayed -= OnBossAttack;
			ai.SummonEffectPlayed -= OnBossAttack;
			thrower.FireEffectPlayed -= OnBossThrow;
			phases.ChargeEffectPlayed -= OnBossCharge;
		}
	}

	// 처음 맞은 데미지만 기록한다 (그 뒤 호스트가 더 때린다)
	void OnBossHit(int damage)
	{
		if (bossHitDamage == 0) bossHitDamage = damage;
	}

	void OnBossDeath() => bossDeathPlayed = true;

	void OnMyHit(int damage)
	{
		myHitSum += damage;
		if (damage == MemberHitDamage) myMarkedHit = true;
	}

	void OnMyDeath() => myDeath = true;

	void OnMyCastRejected(int slot, CastRejection reason) => myCastRejection = reason;

	// 서버에서: 무적이 아니면 남은 체력만큼 피해를 준다. 죽었으면 true (Wait에서 죽을 때까지 되풀이)
	static bool KillWhenHittable(PlayerHealth target)
	{
		if (target == null) return false;
		if (!target.IsDead && !target.IsInvincible) target.TakeDamage(target.Hp);
		return target.IsDead;
	}

	void OnRosterRemoved(PlayerHealth _) => rosterRemoved++;

	void OnRosterAdded(PlayerHealth _) => rosterAdded++;

	static int CountLocalInRoster()
	{
		int count = 0;
		foreach (PlayerHealth p in PlayerRoster.Players)
			if (PlayerRoster.IsLocal(p)) count++;
		return count;
	}

	static int CountRosterInMyPlace()
	{
		int count = 0;
		foreach (PlayerHealth p in PlayerRoster.Players)
			if (PlayerRoster.IsInMyPlace(p)) count++;
		return count;
	}

	// 서버에서: 찌르기(대상 입력을 기다림) 함수를 그 실행기로 시전한다. 대상을 넣지 않으면 계속 기다리므로, 실행 중인 작업으로 남는다 (CastSyncTest와 같은 함수)
	static FunctionTask CastWaitingFunction(FunctionExecutor executor)
	{
		if (executor == null) return null;
		FunctionData data = FunctionData.Create("RaidPartyTestWaitInput");
		data.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust,
			SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));
		if (!FunctionDataConverter.TryConvert(data, new[] { data }, out UserFunction function)) return null;
		return executor.Cast(function);
	}

	void OnMyHide() => myHide = true;

	void OnMyRespawn() => myRespawn = true;

	// 호스트에서: 호스트가 아닌 파티원(시험 빌드 클라이언트)의 서버 쪽 PlayerHealth
	static PlayerHealth FindMemberHealth()
	{
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
			if (!(conn is LocalConnectionToClient) && conn.identity != null) return conn.identity.GetComponent<PlayerHealth>();
		return null;
	}

	void OnBossAttack() => bossAttackPlayed = true;

	void OnBossThrow(Vector2 _) => bossAttackPlayed = true;

	void OnBossCharge(float _) => bossChargePlayed = true;

	void OnHostCharge(float _) => hostChargeSeen = true;

	// 웜(GroundChaseAI)이 이 화면에서도 움직이고, 쫓는 플레이어 쪽을 보는지 본다 (위치·방향 동기화, #310).
	// 웜은 플레이어 옆에 닿으면 멈추므로, 마지막 몇 초가 아니라 처음 보인 때부터 지켜본 결과로 판단한다 (WatchWorm)
	IEnumerator CheckWormSync(string round)
	{
		yield return Wait(round + "웜이 이 화면에 보임", () => wormFirstX.HasValue);
		if (!wormFirstX.HasValue) yield break;
		Check(round + $"웜이 이 화면에서 움직임 (처음 x {wormFirstX.Value:0.00}에서 최대 {wormMaxMove:0.00}칸)", wormMaxMove > 0.5f);
		Check(round + "웜의 걷는 중(IsMoving)을 서버에서 받음", wormSawMoving);
		GroundChaseAI worm = FindAnyObjectByType<GroundChaseAI>();
		if (worm != null) Check(round + "웜이 가장 가까운 플레이어 쪽을 봄", FacesNearestPlayer(worm.transform, worm.GetComponent<SpriteRenderer>()));
	}

	// 투사체(MonsterProjectile)가 이 화면에 보였는지, 보인 뒤 가장 멀리 날아간 거리를 기록한다. 투사체가 사라지면 다음 투사체를 기다린다 (#310)
	IEnumerator WatchProjectile()
	{
		projectileSeen = false;
		projectileMaxMove = 0f;
		float until = Time.realtimeSinceStartup + TimeoutSeconds * 2f;
		while (Time.realtimeSinceStartup < until && projectileMaxMove <= 0.5f)
		{
			MonsterProjectile p = FindAnyObjectByType<MonsterProjectile>();
			if (p == null) { yield return null; continue; }
			projectileSeen = true;
			Vector2 start = p.transform.position;
			while (p != null)
			{
				projectileMaxMove = Mathf.Max(projectileMaxMove, Vector2.Distance(start, p.transform.position));
				yield return null;
			}
		}
	}

	// 웜이 처음 보인 위치, 그 뒤 가장 멀리 옮겨 간 거리, IsMoving이 true였던 적이 있는지 기록한다. 던전을 나가면(웜이 사라지면) 멈춘다
	IEnumerator WatchWorm()
	{
		wormFirstX = null;
		wormMaxMove = 0f;
		wormSawMoving = false;
		GroundChaseAI worm = null;
		float until = Time.realtimeSinceStartup + TimeoutSeconds;
		while ((worm = FindAnyObjectByType<GroundChaseAI>()) == null)
		{
			if (Time.realtimeSinceStartup > until) yield break;
			yield return null;
		}
		wormFirstX = worm.transform.position.x;
		while (worm != null)
		{
			wormMaxMove = Mathf.Max(wormMaxMove, Mathf.Abs(worm.transform.position.x - wormFirstX.Value));
			if (worm.IsMoving) wormSawMoving = true;
			yield return null;
		}
	}

	// [포기] 판 클라이언트: 3페이즈에서 분신 2마리가 보이고, 진짜가 분신 자리 중 하나로 순간이동했고, 모두 같은 쪽을 보는지 본다 (#310)
	IEnumerator CheckCloneSync(string round)
	{
		Monster boss = null;
		yield return Wait(round + "보스가 이 화면에 보임", () => (boss = FindBossInDungeon()) != null);
		if (boss == null) yield break;
		yield return Wait(round + "분신 2마리가 이 화면에 보임", () => FindObjectsByType<CloneGlitch>(FindObjectsSortMode.None).Length == 2);
		yield return Wait(round + "보스가 분신 자리 중 하나로 순간이동, 모두 같은 쪽을 봄", () => boss != null && ClonesLinedUpWith(boss));
	}

	// 보스와 분신 2마리의 x가 10칸 간격(GatewayPhases Clone Slots -10, 0, +10)으로 서 있고 그림 방향이 모두 같으면 true
	static bool ClonesLinedUpWith(Monster boss)
	{
		CloneGlitch[] clones = FindObjectsByType<CloneGlitch>(FindObjectsSortMode.None);
		if (clones.Length != 2) return false;
		bool flip = boss.GetComponent<SpriteRenderer>().flipX;
		var xs = new List<float> { boss.transform.position.x };
		foreach (CloneGlitch c in clones)
		{
			if (c.GetComponent<SpriteRenderer>().flipX != flip) return false;
			xs.Add(c.transform.position.x);
		}
		xs.Sort();
		return Mathf.Abs(xs[1] - xs[0] - 10f) < 0.5f && Mathf.Abs(xs[2] - xs[1] - 10f) < 0.5f;
	}

	// target이 이 화면의 가장 가까운 플레이어 쪽을 보고 있으면 true (그림은 오른쪽을 보고 그리고 왼쪽이면 flipX). 거의 같은 x면 판단하지 않고 true
	static bool FacesNearestPlayer(Transform target, SpriteRenderer sprite)
	{
		PlayerHealth nearest = null;
		float best = float.MaxValue;
		foreach (PlayerHealth p in FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None))
		{
			float d = Mathf.Abs(p.transform.position.x - target.position.x);
			if (d < best) { best = d; nearest = p; }
		}
		if (nearest == null || best < 0.5f) return true;
		return sprite.flipX == (nearest.transform.position.x < target.position.x);
	}

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
