using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 소환검 포인터(Sword*)·전장 휩쓸기 테스트 (#220). Test_JW의 SwordPointerTest. 켜고 Play → Host (약 12초). 주변에 몬스터가 2마리 이상 있어야 한다.
/// <para>
/// 1. 블록코딩 malloc(Sword) → free(s): 진짜 소환검이 생겼다가 사라지고 heap이 원래대로<br/>
/// 2. 블록코딩 Sword* s = SummonSword(); FreeSword(s);: 검 소환이 포인터를 돌려주고, 그 포인터로 해제됨<br/>
/// 3. 소환검 던지기·찌르기에 두 번째 검의 포인터를 넘기면 첫 번째 검이 아니라 그 검을 씀<br/>
/// 4. 해제에 첫 번째 검의 포인터를 넘기면 그 검만 해제됨. 같은 포인터로 또 해제하면 InvalidPointerError<br/>
/// 5. 전장 휩쓸기: 고른 몬스터마다 소환검 하나씩 가서 벰. 같은 몬스터를 두 번 넣어도 한 번만 벰<br/>
/// 「[SwordPointerTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class SwordPointerSimTest : MonoBehaviour
{
	const string Tag = "[SwordPointerTest]";

	int failed;
	FunctionExecutor executor;
	ProcessMemory memory;
	SummonedSwords swords;
	readonly List<(string id, GameObject target)> hits = new List<(string, GameObject)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		memory = player.GetComponent<ProcessMemory>();
		swords = player.GetComponent<SummonedSwords>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (executor == null || memory == null || swords == null || effects == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, ProcessMemory, SummonedSwords, SkillEffectEvents가 있어야 함");
			yield break;
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target));
		VariableType swordPtr = DataDrivenSkill.SwordPointerType();
		VariableType swordStruct = VariableType.StructOf(PredefinedStructs.Sword);

		// 1. malloc(Sword) → free(s)
		int countBefore = swords.Count;
		int heapBefore = memory.HeapUsed;
		int maxCount = countBefore;
		System.Action onChanged = () => maxCount = Mathf.Max(maxCount, swords.Count);
		swords.Changed += onChanged;
		FunctionData mallocFree = FunctionData.Create("SwordPointerTest_MallocFree");
		mallocFree.Blocks.Add(BlockData.Declare(swordPtr, "s", SlotData.FromBlock(BlockData.Malloc(swordStruct))));
		mallocFree.Blocks.Add(BlockData.Free(SlotData.FromVariable("s")));
		yield return CastUser(mallocFree);
		yield return null;
		swords.Changed -= onChanged;
		Check($"malloc(Sword): 진짜 소환검이 생김 (가장 많을 때 {maxCount}개)", maxCount == countBefore + 1);
		Check($"free(s): 소환검이 사라지고 heap이 원래대로 ({swords.Count}개, heap {heapBefore} → {memory.HeapUsed})",
			swords.Count == countBefore && memory.HeapUsed == heapBefore);

		// 2. Sword* s = SummonSword(); FreeSword(s);
		maxCount = countBefore;
		swords.Changed += onChanged;
		FunctionData summonFree = FunctionData.Create("SwordPointerTest_SummonFree");
		summonFree.Blocks.Add(BlockData.Declare(swordPtr, "s", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.SummonSword))));
		summonFree.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.FreeSword, SlotData.FromVariable("s")));
		yield return CastUser(summonFree);
		yield return null;
		swords.Changed -= onChanged;
		Check($"SummonSword()가 Sword*를 돌려주고 FreeSword(s)로 해제됨 (가장 많을 때 {maxCount}개 → {swords.Count}개)",
			maxCount == countBefore + 1 && swords.Count == countBefore && memory.HeapUsed == heapBefore);

		// 사용 가능한 소환검 2개 이상 준비
		int guard = 6;
		while (swords.ReadyCount < 2 && swords.CanSummon && guard-- > 0) yield return Cast(PredefinedFunctionIds.SummonSword);
		List<int> pointers = memory.Heap.GetGlobalPointers<SummonedSword>();
		if (pointers.Count < 2 || !swords.TryGetSword(pointers[0], out SummonedSword first) || !swords.TryGetSword(pointers[1], out SummonedSword second))
		{
			Debug.LogError($"{Tag} 소환검을 2개 이상 만들지 못함 (heap 부족?). 남은 칸 {memory.Free}");
			yield break;
		}
		foreach (SummonedSword s in swords.Swords) swords.Recall(s);
		Monster target = Nearest(player, null);
		if (target == null) { Debug.LogError($"{Tag} 살아 있는 몬스터가 없음"); yield break; }

		// 3-1. 소환검 던지기에 두 번째 검을 넘김
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.SummonedThrow, RuntimeValue.FromBug(target), RuntimeValue.FromPointer(swordPtr, second.Pointer));
		Check($"소환검 던지기(target, 두 번째 검): 두 번째 검이 날아감 (첫 {first.State}, 둘째 {second.State})",
			first.State == SummonedSwordState.Ready && second.State != SummonedSwordState.Ready);
		yield return new WaitForSeconds(0.5f);
		swords.Recall(second);

		// 3-2. 소환검 찌르기에 두 번째 검을 넘김
		bool secondStabbed = false;
		System.Action<SummonedSword, GameObject> onStab = (s, t) => { if (s == second) secondStabbed = true; };
		swords.StabHit += onStab;
		FunctionTask thrust = CastTask(PredefinedFunctionIds.SummonedThrust, RuntimeValue.FromBug(target), RuntimeValue.FromPointer(swordPtr, second.Pointer));
		yield return WaitUntil(() => second.State == SummonedSwordState.Stabbing, 1f);
		Check($"소환검 찌르기(target, 두 번째 검): 두 번째 검이 찌르러 감 (첫 {first.State}, 둘째 {second.State})",
			first.State == SummonedSwordState.Ready && second.State == SummonedSwordState.Stabbing);
		yield return WaitUntil(() => thrust == null || thrust.IsEnded, 3f);
		swords.StabHit -= onStab;
		Check("소환검 찌르기: 두 번째 검이 대상에 닿음", secondStabbed);
		yield return WaitUntil(() => second.State == SummonedSwordState.Ready, 2f);

		// 4. 해제에 첫 번째 검을 넘김
		int firstPointer = first.Pointer;
		int count = swords.Count;
		yield return Cast(PredefinedFunctionIds.FreeSword, RuntimeValue.FromPointer(swordPtr, firstPointer));
		yield return null;
		Check($"해제(첫 번째 검): 그 검만 사라짐 ({count} → {swords.Count})",
			first == null && swords.Count == count - 1 && second != null && swords.TryGetSword(second.Pointer, out _));
		FunctionTask again = CastTask(PredefinedFunctionIds.FreeSword, RuntimeValue.FromPointer(swordPtr, firstPointer));
		yield return WaitUntil(() => again == null || again.IsEnded, 3f);
		Check("해제한 포인터로 또 해제 → InvalidPointerError", again != null && again.IsFailed && again.Error is InvalidPointerError);

		// 5. 전장 휩쓸기
		guard = 6;
		while (swords.ReadyCount < 2 && swords.CanSummon && guard-- > 0) yield return Cast(PredefinedFunctionIds.SummonSword);
		foreach (SummonedSword s in swords.Swords) swords.Recall(s);
		Monster a = Nearest(player, null);
		Monster b = Nearest(player, a);
		if (b == null || swords.ReadyCount < 2)
		{
			Debug.LogWarning($"{Tag} 몬스터 2마리, 소환검 2개가 있어야 전장 휩쓸기를 확인함 (소환검 {swords.ReadyCount}개)");
		}
		else
		{
			int ready = swords.ReadyCount;
			hits.Clear();
			yield return Cast(PredefinedFunctionIds.BattlefieldSweep, RuntimeValue.FromBugs(new[] { a, b, a }));
			yield return WaitUntil(() => swords.ReadyCount == ready, 2f);
			int hitA = hits.FindAll(h => h.id == PredefinedFunctionIds.BattlefieldSweep && h.target == a.gameObject).Count;
			int hitB = hits.FindAll(h => h.id == PredefinedFunctionIds.BattlefieldSweep && h.target == b.gameObject).Count;
			Check($"전장 휩쓸기 [a, b, a]: a {hitA}번, b {hitB}번 맞음 (각 1번)", hitA == 1 && hitB == 1);
			Check("전장 휩쓸기: 검이 모두 돌아옴", swords.ReadyCount == ready);
		}

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator CastUser(FunctionData data)
	{
		if (!FunctionDataConverter.TryConvert(data, System.Array.Empty<FunctionData>(), out UserFunction function))
		{
			Check($"{data.Name}: 변환", false);
			yield break;
		}
		FunctionTask task = executor.Cast(function);
		if (task == null) { Check($"{data.Name}: 시전", false); yield break; }
		yield return WaitUntil(() => task.IsEnded, 5f);
		if (task.EndReason != FunctionTaskEndReason.Completed) Check($"{data.Name}: Completed (실제 {task.EndReason} {task.Failure} {task.Error?.GetType().Name})", false);
	}

	FunctionTask CastTask(string id, params RuntimeValue[] arguments)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = executor.Cast(function, arguments);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	IEnumerator Cast(string id, params RuntimeValue[] arguments)
	{
		FunctionTask task = CastTask(id, arguments);
		if (task == null) yield break;
		yield return WaitUntil(() => task.IsEnded, 3f);
		if (task.EndReason != FunctionTaskEndReason.Completed) Check($"{id}: Completed (실제 {task.EndReason} {task.Error?.GetType().Name})", false);
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
	}

	static Monster Nearest(GameObject from, Monster except)
	{
		Monster picked = null;
		float best = float.MaxValue;
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead || m == except) continue;
			float d = Mathf.Abs(m.transform.position.x - from.transform.position.x);
			if (d < best) { best = d; picked = m; }
		}
		return picked;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패");
		}
	}
}
