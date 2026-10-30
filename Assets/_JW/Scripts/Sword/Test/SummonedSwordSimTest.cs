using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 소환검 + 검 소환·해제 스킬 테스트 (#99, #102). 씬의 아무 오브젝트에 붙이고 Play → Host. 콘솔에 「[SummonTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 검 소환(c.summon_sword) 5번: 소환검 5개, 포인터 5개(소환 순서대로 커짐). 6번째는 만들어지지 않는다.<br/>
/// 2. 슬롯에서 쓸 검: 가장 먼저 소환한 사용 가능한 검. 던지기 → 떨어짐 → 검 돌아오기, 가짜 Z 키로 줍기.<br/>
/// 3. 해제(c.free_sword): 마지막에 소환한 검이 사라지고 heap이 줄어든다. 해제한 포인터는 찾을 수 없다.<br/>
/// 4. 떨어진 소환검도 해제된다.<br/>
/// 5. 검 소환 도중 실행 중단(InterruptAll): 그 소환검은 누수 → 멈추고 반투명, 개수에 남음, 해제 대상 아님.<br/>
/// 6. 사용 가능한 소환검이 없으면 소환검 찌르기 실패. 던진 검만 있어도 해제는 된다.<br/>
/// 7. 해제할 소환검이 없으면 해제는 NoSummonedSwordError로 실패.<br/>
/// 8. 영역 비우기(Heap.Clear): 누수된 소환검까지 모두 사라진다.
/// </para>
/// 가짜 키 입력은 Game 창에 초점이 있어야 들어간다 (Unity가 화면 맨 앞에 있어야 함). 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class SummonedSwordSimTest : MonoBehaviour
{
	int failures;
	SummonedSwords swords;
	ProcessMemory memory;
	FunctionExecutor executor;

	IEnumerator Start()
	{
		while (swords == null)
		{
			yield return null;
			if (NetworkClient.localPlayer != null) swords = NetworkClient.localPlayer.GetComponent<SummonedSwords>();
		}
		memory = swords.GetComponent<ProcessMemory>();
		executor = swords.GetComponent<FunctionExecutor>();
		int heapSize = SkillTable.TryGet(PredefinedFunctionIds.SummonSword, out SkillData data) ? data.HeapSize : 1;
		yield return new WaitForSeconds(1f); // 착지 대기

		// 1. 검 소환 5번 + 6번째
		for (int i = 0; i < SummonedSwords.MaxCount; i++) yield return Cast(PredefinedFunctionIds.SummonSword, FunctionTaskEndReason.Completed);
		List<int> pointers = memory.Heap.GetGlobalPointers<SummonedSword>();
		Check("검 소환 5번 → 소환검 5개", swords.Count == 5 && swords.ReadyCount == 5);
		Check("포인터 5개, 소환 순서대로 커짐", pointers.Count == 5 && IsAscending(pointers));
		yield return Cast(PredefinedFunctionIds.SummonSword, FunctionTaskEndReason.Completed);
		Check("6번째 검 소환은 만들어지지 않음", swords.Count == 5 && memory.Heap.GetGlobalPointers<SummonedSword>().Count == 5);
		yield return new WaitForSeconds(0.5f);

		// 2. 앞에서부터 쓰기, 던지기·돌아오기·줍기
		Check("슬롯에서 쓸 검 = 첫 번째", swords.TryGetFirstReady(out SummonedSword first) && first.Pointer == pointers[0]);
		Check("첫 번째 검 던지기 (빗나감)", swords.Throw(first, null));
		Check("다음에 쓸 검 = 두 번째", swords.TryGetFirstReady(out SummonedSword next) && next.Pointer == pointers[1]);
		yield return WaitFor(() => first.State == SummonedSwordState.Dropped, 3f);
		Check("빗나가면 땅에 떨어짐", first.State == SummonedSwordState.Dropped);
		Check("검 돌아오기", swords.Recall(first) && first.State == SummonedSwordState.Ready);
		yield return new WaitForSeconds(0.5f);
		swords.Throw(first, null);
		yield return WaitFor(() => first.State == SummonedSwordState.Dropped, 3f);
		yield return PressZ();
		yield return WaitFor(() => first.State == SummonedSwordState.Ready, 1f);
		Check("Z로 떨어진 소환검 줍기", first.State == SummonedSwordState.Ready);

		// 3. 해제: 마지막에 소환한 것부터
		int last = pointers[4];
		int heapBefore = memory.HeapUsed;
		yield return Cast(PredefinedFunctionIds.FreeSword, FunctionTaskEndReason.Completed);
		Check("해제 → 4개", swords.Count == 4);
		Check("마지막에 소환한 검이 해제됨", !swords.TryGetSword(last, out _) && swords.TryGetSword(pointers[3], out _));
		Check("해제하면 heap이 줄어듦", memory.HeapUsed == heapBefore - heapSize);

		// 4. 떨어진 소환검 해제
		swords.TryGetSword(pointers[3], out SummonedSword dropped);
		swords.Throw(dropped, null);
		yield return WaitFor(() => dropped.State == SummonedSwordState.Dropped, 3f);
		yield return Cast(PredefinedFunctionIds.FreeSword, FunctionTaskEndReason.Completed);
		yield return null;
		Check("떨어진 소환검도 해제됨 → 3개", swords.Count == 3 && dropped == null);

		// 5. 검 소환 도중 실행 중단 → 누수
		FunctionTask summoning = CastTask(PredefinedFunctionIds.SummonSword);
		yield return WaitFor(() => swords.Count == 4, 2f);
		executor.InterruptAll();
		// 끊긴 작업의 누수 처리는 그 tick이 끝날 때 heap이 하므로, 누수 상태가 될 때까지 기다린다
		SummonedSword leaked = null;
		yield return WaitFor(() => (leaked = FindLeaked()) != null, 2f);
		Check("검 소환이 끊기면 소환검 누수", summoning != null && summoning.EndReason == FunctionTaskEndReason.Interrupted && leaked != null);
		SpriteRenderer leakedSprite = leaked != null ? leaked.GetComponentInChildren<SpriteRenderer>() : null;
		Check("누수되면 반투명", leakedSprite != null && leakedSprite.color.a < 1f);
		Check("누수돼도 개수에 남음 (4개, 해제할 수 있는 건 3개)", swords.Count == 4 && memory.Heap.GetGlobalPointers<SummonedSword>().Count == 3);

		// 6. 사용 가능한 검 없음: 소환검 찌르기 실패, 해제는 됨
		foreach (SummonedSword s in swords.Swords) swords.Throw(s, null);
		yield return Cast(PredefinedFunctionIds.SummonedThrust, FunctionTaskEndReason.Failed);
		yield return new WaitForSeconds(1f);
		for (int i = 0; i < 3; i++) yield return Cast(PredefinedFunctionIds.FreeSword, FunctionTaskEndReason.Completed);
		Check("던진 검만 있어도 해제됨 → 누수된 1개만 남음", swords.Count == 1 && swords.Swords.Count == 1 && leaked != null);

		// 7. 해제할 검 없음
		FunctionTask noSword = CastTask(PredefinedFunctionIds.FreeSword);
		yield return WaitFor(() => noSword == null || noSword.IsEnded, 5f);
		Check("해제할 소환검이 없으면 NoSummonedSwordError", noSword != null && noSword.IsFailed && noSword.Error is NoSummonedSwordError);
		Check("누수된 검은 해제되지 않음", leaked != null && swords.Count == 1);

		// 8. 영역 비우기
		memory.Heap.Clear();
		yield return null;
		Check("영역 비우기로 모두 사라짐", swords.Count == 0 && swords.Swords.Count == 0 && leaked == null);

		if (failures == 0) Debug.Log("[SummonTest] 전체 통과");
		else Debug.LogError($"[SummonTest] 실패 {failures}개");
	}

	SummonedSword FindLeaked()
	{
		foreach (SummonedSword s in swords.Swords) if (s != null && s.State == SummonedSwordState.Leaked) return s;
		return null;
	}

	FunctionTask CastTask(string id)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = executor.Cast(function);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	IEnumerator Cast(string id, FunctionTaskEndReason expected)
	{
		FunctionTask task = CastTask(id);
		if (task == null) yield break;
		yield return WaitFor(() => task.IsEnded, 10f);
		if (task.EndReason != expected) Check($"{id}: {expected}", false);
	}

	static IEnumerator PressZ()
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Z));
		yield return null;
		yield return null;
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
	}

	static bool IsAscending(List<int> values)
	{
		for (int i = 1; i < values.Count; i++) if (values[i] <= values[i - 1]) return false;
		return true;
	}

	static IEnumerator WaitFor(System.Func<bool> condition, float timeout)
	{
		float end = Time.time + timeout;
		while (!condition() && Time.time < end) yield return null;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[SummonTest] {label}: 통과");
		else { failures++; Debug.LogError($"[SummonTest] {label}: 실패"); }
	}
}
