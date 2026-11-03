using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 함수 시전 동기화(#173) 확인용 테스트 컴포넌트. 호스트(에디터) 하나와 클라이언트(시험 빌드) 하나로 돌린다.
/// <para>
/// 사용법<br/>
/// - NetworkTest 씬의 오브젝트에 붙이고 Monster Prefab에 Leak을 넣는다. Leak은 그 씬 NetworkManager의 Registered Spawnable Prefabs에 등록되어 있어야 한다.<br/>
/// - 에디터에서 Play → Host, 시험 빌드에서 Client로 접속하면 자동으로 돈다. 평소에는 꺼 둔다.
/// </para>
/// <para>
/// 서버: 들어온 플레이어마다 옆에 몬스터 2마리를 만들고, 시험 슬롯을 넣는다(슬롯 1 Thrust(GetBug()), 슬롯 2 쿨타임 시험 스킬, 슬롯 3 대상 array 시험 스킬, 슬롯 8 비움).<br/>
/// 서버: 대상 array 시험 스킬이 읽은 몬스터 수를 확인한다(#238). 「[CastSyncTest][서버] 대상 array 시험 스킬이 몬스터 2마리를 읽음: 통과」면 통과.<br/>
/// 클라이언트: 차례로 시전하며 받은 알림과 메모리·쿨타임 값을 확인한다. 「[CastSyncTest] 클라이언트 전체 통과」면 통과.<br/>
/// 호스트 화면: 다른 플레이어의 시작 알림이 오는지, 그 플레이어의 거부 알림이 오지 않는지 확인한다. 서버 값(메모리, 몬스터 체력)은 비교용으로 로그만 남긴다.
/// </para>
/// </summary>
public class CastSyncTest : MonoBehaviour
{
	const string CooldownSkillId = "test.cooldown_function";

	[SerializeField, Tooltip("플레이어 옆에 만들 몬스터 프리팹 (Leak)")]
	GameObject monsterPrefab;

	[SerializeField, Tooltip("한 단계의 알림을 기다리는 최대 시간(초). 넘으면 실패로 본다")]
	float timeoutSeconds = 5f;

	// [서버] 시험 슬롯을 넣은 플레이어
	readonly HashSet<NetworkIdentity> preparedPlayers = new HashSet<NetworkIdentity>();
	// [호스트 화면] event를 붙인 다른 플레이어
	readonly HashSet<FunctionExecutorNetwork> watchedRemotes = new HashSet<FunctionExecutorNetwork>();
	// [클라이언트] 시험을 시작했는지
	bool clientTestStarted;
	int failedCount;

	// [서버] 대상 array 시험 스킬은 static event로 읽은 몬스터를 알려 주므로, 플레이어마다가 아니라 한 번만 붙인다
	void OnEnable() => TestTargetArrayFunction.TargetsRead += OnTargetsRead;
	void OnDisable() => TestTargetArrayFunction.TargetsRead -= OnTargetsRead;

	void Update()
	{
		if (NetworkServer.active) PrepareNewPlayers();
		if (NetworkServer.active && NetworkClient.active) WatchRemotePlayersOnHost();
		if (NetworkClient.active && !NetworkServer.active && !clientTestStarted) TryStartClientTest();
	}

	// [서버] 데이터가 준비된 플레이어마다 몬스터와 시험 슬롯을 한 번 넣는다
	void PrepareNewPlayers()
	{
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
		{
			NetworkIdentity player = conn.identity;
			if (player == null || preparedPlayers.Contains(player)) continue;
			FunctionExecutorNetwork net = player.GetComponent<FunctionExecutorNetwork>();
			if (net == null || !net.IsDataReady) continue;

			preparedPlayers.Add(player);
			Monster monster = SpawnMonsterNear(player.gameObject, 1.5f);
			// 슬롯 3의 대상 array 시험에서 고를 두 번째 몬스터
			SpawnMonsterNear(player.gameObject, 3f);
			ApplyTestSlots(net);

			FunctionExecutor executor = player.GetComponent<FunctionExecutor>();
			ProcessMemory memory = player.GetComponent<ProcessMemory>();
			executor.InputRequested += (task, input) =>
				Debug.Log($"[CastSyncTest][서버] {player.name}: 입력 대기 중 서버 메모리 {memory.Capacity},{memory.DataUsed},{memory.StackUsed},{memory.HeapUsed} (Free {memory.Free})");
			if (monster != null)
				monster.Damaged += damage => Debug.Log($"[CastSyncTest][서버] {monster.name}: 데미지 {damage}, 체력 {monster.Hp}/{monster.MaxHp}");
			Debug.Log($"[CastSyncTest][서버] {player.name}: 몬스터와 시험 슬롯을 넣음");
		}
	}

	Monster SpawnMonsterNear(GameObject player, float offsetX)
	{
		GameObject obj = NetworkSpawner.Spawn(monsterPrefab, player.transform.position + new Vector3(offsetX, 0f, 0f), Quaternion.identity, player.scene);
		return obj != null ? obj.GetComponent<Monster>() : null;
	}

	// [서버] 대상 array 시험 스킬이 읽은 몬스터. 클라이언트가 보낸 2마리가 모두 몬스터로 바뀌어 들어왔는지 확인한다
	void OnTargetsRead(List<Monster> monsters)
	{
		int count = monsters?.Count ?? 0;
		int found = 0;
		if (monsters != null)
			foreach (Monster monster in monsters)
				if (monster != null) found++;
		string names = monsters != null ? string.Join(", ", monsters.ConvertAll(m => m != null ? m.name : "null")) : "값 없음";
		Check("[서버] 대상 array 시험 스킬이 몬스터 2마리를 읽음", count == 2 && found == 2, names);
	}

	// 슬롯 1: Thrust(GetBug()), 슬롯 2: 쿨타임 20틱 시험 스킬, 슬롯 3: 대상 array 시험 스킬, 나머지 비움
	static void ApplyTestSlots(FunctionExecutorNetwork net)
	{
		FunctionData thrustGetBug = FunctionData.Create("CastSyncThrustGetBug");
		thrustGetBug.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust,
			SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));

		SlotLoadout.Entry[] slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++) slots[i] = SlotLoadout.Entry.Empty;
		slots[0] = SlotLoadout.Entry.Function(thrustGetBug.Guid);
		slots[1] = SlotLoadout.Entry.Skill(CooldownSkillId);
		slots[2] = SlotLoadout.Entry.Skill(PredefinedFunctionIds.TestTargetArrayFunction);
		net.ApplyPlayerData(slots, new List<FunctionData> { thrustGetBug }, null);
	}

	// [호스트 화면] 다른 플레이어의 알림을 지켜본다
	void WatchRemotePlayersOnHost()
	{
		foreach (FunctionExecutorNetwork net in FindObjectsByType<FunctionExecutorNetwork>(FindObjectsSortMode.None))
		{
			if (net.isLocalPlayer || watchedRemotes.Contains(net)) continue;

			watchedRemotes.Add(net);
			net.TaskStarted += (id, slot, functionName) =>
				Debug.Log($"[CastSyncTest][호스트 화면] {net.name}의 작업 {id} 시작 알림 받음 (슬롯 {slot + 1}, {functionName}): 통과");
			net.CastRejected += (slot, rejection) =>
				Debug.LogError($"[CastSyncTest][호스트 화면] {net.name}의 거부 알림이 호스트에도 옴 (슬롯 {slot + 1}, {rejection}): 실패");
		}
	}

	void TryStartClientTest()
	{
		NetworkIdentity player = NetworkClient.localPlayer;
		if (player == null) return;
		FunctionExecutorNetwork net = player.GetComponent<FunctionExecutorNetwork>();
		if (net == null || !net.IsDataReady || FindNearestMonster(player.transform.position) == null) return;

		clientTestStarted = true;
		StartCoroutine(RunClientTest(net));
	}

	IEnumerator RunClientTest(FunctionExecutorNetwork net)
	{
		// 서버가 시험 슬롯을 넣을 시간을 준다
		yield return new WaitForSeconds(1f);

		GameObject player = net.gameObject;
		FunctionExecutor executor = player.GetComponent<FunctionExecutor>();
		ProcessMemory memory = player.GetComponent<ProcessMemory>();
		GameObject monster = FindNearestMonster(player.transform.position).gameObject;

		// 받은 알림을 모아 둔다. 단계마다 기다리며 읽는다
		var accepted = new List<(int Slot, int TaskId)>();
		var rejected = new List<(int Slot, CastRejection Reason)>();
		var started = new List<int>();
		var ended = new Dictionary<int, FunctionTaskEndReason>();
		var inputs = new List<(int TaskId, string InputId, int Slot)>();
		net.CastAccepted += (slot, id) => accepted.Add((slot, id));
		net.CastRejected += (slot, reason) => rejected.Add((slot, reason));
		net.TaskStarted += (id, slot, functionName) => started.Add(id);
		net.TaskEnded += (id, reason) => ended[id] = reason;
		net.InputRequested += (id, input, slot) => inputs.Add((id, input, slot));

		// 1. 클라이언트 실행기가 멈춰 있는지 (J)
		int tickBefore = executor.CurrentTick;
		yield return new WaitForSeconds(0.5f);
		Check("클라이언트 실행기 멈춤 (TickEnabled false, CurrentTick 그대로)", !executor.TickEnabled && executor.CurrentTick == tickBefore,
			$"TickEnabled {executor.TickEnabled}, CurrentTick {tickBefore} → {executor.CurrentTick}");

		// 2. 슬롯 1 Thrust(GetBug()): 성공 → 입력 요청 → 기다리는 동안 메모리 → 몬스터 넣기 → 정상 종료
		net.RequestCast(0);
		yield return WaitFor(() => accepted.Count > 0 || rejected.Count > 0);
		Check("슬롯 1 시전 성공", accepted.Count == 1 && accepted[0].Slot == 0, $"성공 {accepted.Count}, 거부 {rejected.Count}");
		int taskId = accepted.Count > 0 ? accepted[0].TaskId : -1;

		yield return WaitFor(() => inputs.Count > 0);
		Check("입력 요청 받음 (input.get_bug, 슬롯 1)", inputs.Count == 1 && inputs[0].TaskId == taskId && inputs[0].InputId == PredefinedFunctionIds.GetBug && inputs[0].Slot == 0,
			inputs.Count > 0 ? $"작업 {inputs[0].TaskId}, {inputs[0].InputId}, 슬롯 {inputs[0].Slot + 1}" : "받지 못함");
		Check("시작 알림 받음", started.Contains(taskId), $"시작 알림 {string.Join(",", started)}");

		// 메모리 값이 도착할 시간을 준다. 서버 로그의 「입력 대기 중 서버 메모리」와 같아야 한다
		yield return new WaitForSeconds(0.5f);
		Debug.Log($"[CastSyncTest] 입력 대기 중 내 화면 메모리 {memory.Capacity},{memory.DataUsed},{memory.StackUsed},{memory.HeapUsed} (Free {memory.Free})");
		Check("입력 대기 중 내 화면의 스택이 0이 아님 (서버 값을 받음)", memory.StackUsed > 0, $"StackUsed {memory.StackUsed}");

		net.ProvideTarget(taskId, monster);
		yield return WaitFor(() => ended.ContainsKey(taskId));
		Check("몬스터를 넣은 작업이 정상 종료", ended.TryGetValue(taskId, out FunctionTaskEndReason reason1) && reason1 == FunctionTaskEndReason.Completed,
			ended.ContainsKey(taskId) ? ended[taskId].ToString() : "끝 알림 없음");
		yield return new WaitForSeconds(0.5f);
		Check("끝난 뒤 내 화면의 스택 0", memory.StackUsed == 0, $"StackUsed {memory.StackUsed}");

		// 3. 슬롯 2 쿨타임 스킬: 끝나면 내 화면의 남은 쿨타임 > 0, 바로 다시 시전하면 OnCooldown 거부
		accepted.Clear();
		rejected.Clear();
		net.RequestCast(1);
		yield return WaitFor(() => accepted.Count > 0 || rejected.Count > 0);
		int cooldownTaskId = accepted.Count > 0 ? accepted[0].TaskId : -1;
		yield return WaitFor(() => ended.ContainsKey(cooldownTaskId));
		yield return new WaitForSeconds(0.1f);
		PredefinedFunctionRegistry.TryGet(CooldownSkillId, out PredefinedFunction cooldownSkill);
		int remaining = executor.GetRemainingCooldownTicks(cooldownSkill);
		Check("쿨타임 스킬이 끝난 뒤 내 화면의 남은 쿨타임 > 0", remaining > 0, $"{remaining}틱");

		net.RequestCast(1);
		yield return WaitFor(() => rejected.Count > 0);
		Check("쿨타임 중 다시 시전하면 OnCooldown 거부", rejected.Count == 1 && rejected[0].Reason == CastRejection.OnCooldown,
			rejected.Count > 0 ? rejected[0].Reason.ToString() : "거부 알림 없음");

		// 4. 슬롯 3 대상 array 시험 스킬: 몬스터 2마리를 배열로 넘겨 시전 → 정상 종료. 읽은 몬스터 수는 서버 로그에서 확인한다 (#238)
		List<Monster> nearMonsters = null;
		yield return WaitFor(() => (nearMonsters = FindNearestMonsters(player.transform.position, 2)).Count == 2);
		GameObject[] targets = nearMonsters.ConvertAll(m => m.gameObject).ToArray();
		accepted.Clear();
		rejected.Clear();
		net.RequestCast(2, targets);
		yield return WaitFor(() => accepted.Count > 0 || rejected.Count > 0);
		Check("슬롯 3 대상 array 시전 성공", accepted.Count == 1 && accepted[0].Slot == 2, $"성공 {accepted.Count}, 거부 {rejected.Count}");
		int arrayTaskId = accepted.Count > 0 ? accepted[0].TaskId : -1;
		yield return WaitFor(() => ended.ContainsKey(arrayTaskId));
		Check("대상 array 작업이 정상 종료", ended.TryGetValue(arrayTaskId, out FunctionTaskEndReason arrayReason) && arrayReason == FunctionTaskEndReason.Completed,
			ended.ContainsKey(arrayTaskId) ? ended[arrayTaskId].ToString() : "끝 알림 없음");

		// 5. 빈 슬롯 8: NoFunction 거부
		rejected.Clear();
		net.RequestCast(7);
		yield return WaitFor(() => rejected.Count > 0);
		Check("빈 슬롯은 NoFunction 거부", rejected.Count == 1 && rejected[0].Reason == CastRejection.NoFunction,
			rejected.Count > 0 ? rejected[0].Reason.ToString() : "거부 알림 없음");

		Debug.Log(failedCount == 0 ? "[CastSyncTest] 클라이언트 전체 통과" : $"[CastSyncTest] 클라이언트 실패 {failedCount}개");
	}

	// 조건이 맞거나 시간이 다 될 때까지 기다린다. 시간이 다 되면 다음 Check가 실패로 남긴다
	IEnumerator WaitFor(Func<bool> condition)
	{
		float end = Time.time + timeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
	}

	void Check(string label, bool passed, string actual)
	{
		if (!passed) failedCount++;
		string message = $"[CastSyncTest] {label}: {(passed ? "통과" : "실패")} ({actual})";
		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}

	static Monster FindNearestMonster(Vector3 from)
	{
		Monster nearest = null;
		float best = float.MaxValue;
		foreach (Monster monster in FindObjectsByType<Monster>(FindObjectsSortMode.None))
		{
			float distance = (monster.transform.position - from).sqrMagnitude;
			if (distance < best) { best = distance; nearest = monster; }
		}
		return nearest;
	}

	// 가까운 순서로 몬스터를 count마리까지 찾는다
	static List<Monster> FindNearestMonsters(Vector3 from, int count)
	{
		List<Monster> monsters = new List<Monster>(FindObjectsByType<Monster>(FindObjectsSortMode.None));
		monsters.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
		if (monsters.Count > count) monsters.RemoveRange(count, monsters.Count - count);
		return monsters;
	}
}
