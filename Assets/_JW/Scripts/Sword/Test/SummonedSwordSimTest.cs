using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 소환검 테스트 (#99). 씬의 아무 오브젝트에 붙이고 Play → Host. 콘솔에 「[SummonTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 최대 5개: 6번 소환하면 5개만 만들어지고 6번째는 NullPointer. 포인터는 소환 순서대로 커진다.<br/>
/// 2. 슬롯에서 쓸 검: 가장 먼저 소환한 사용 가능한 검 (C 배열처럼 앞에서부터). 앞의 검을 던지면 다음 검.<br/>
/// 3. 던지기 → 떨어짐 → 검 돌아오기(Recall)로 다시 사용 가능.<br/>
/// 4. 줍기: 떨어진 소환검 근처에서 가짜 Z 키 (물리 검은 손에 있으므로 소환검을 줍는다).<br/>
/// 5. 해제: Free하면 개수가 줄고, 해제한 포인터는 찾을 수 없고 다시 해제해도 false.<br/>
/// 6. 누수: 실패로 끝난 작업이 malloc한 소환검은 누수 → 그 자리에 멈추고 개수에는 남는다. 다시 소환하면 남은 자리만큼만.<br/>
/// 7. 사용 가능한 소환검이 없으면 소환검이 필요한 스킬(c.summoned_thrust)은 실패로 끝난다.<br/>
/// 8. 영역 비우기(Heap.Clear): 소환검이 모두 사라진다.
/// </para>
/// 가짜 키 입력은 Game 창에 초점이 있어야 들어간다. 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class SummonedSwordSimTest : MonoBehaviour
{
	[Tooltip("소환검 하나의 heap 크기 (테스트용)")]
	[SerializeField] int heapSize = 1;

	int failures;

	IEnumerator Start()
	{
		SummonedSwords swords = null;
		while (swords == null)
		{
			yield return null;
			if (NetworkClient.localPlayer != null) swords = NetworkClient.localPlayer.GetComponent<SummonedSwords>();
		}
		ProcessMemory memory = swords.GetComponent<ProcessMemory>();
		FunctionExecutor executor = swords.GetComponent<FunctionExecutor>();
		yield return new WaitForSeconds(1f); // 착지 대기

		// 1. 최대 5개
		int[] pointers = new int[SummonedSwords.MaxCount];
		for (int i = 0; i < pointers.Length; i++) pointers[i] = swords.Summon(null, heapSize);
		bool allMade = true, ascending = true;
		for (int i = 0; i < pointers.Length; i++)
		{
			if (pointers[i] == HeapStorage.NullPointer) allMade = false;
			if (i > 0 && pointers[i] <= pointers[i - 1]) ascending = false;
		}
		Check("5개 소환", allMade && swords.Count == 5 && swords.ReadyCount == 5);
		Check("포인터는 소환 순서대로 커짐", ascending);
		Check("6번째 소환은 NullPointer", swords.Summon(null, heapSize) == HeapStorage.NullPointer && swords.Count == 5);
		yield return new WaitForSeconds(0.5f);

		// 2. 가장 먼저 소환한 검부터
		Check("슬롯에서 쓸 검 = 첫 번째", swords.TryGetFirstReady(out SummonedSword first) && first.Pointer == pointers[0]);

		// 3. 던지기 → 떨어짐 → 돌아오기
		Check("첫 번째 검 던지기 (빗나감)", swords.Throw(first, null));
		Check("던지면 사용 가능 4개", swords.ReadyCount == 4);
		Check("다음에 쓸 검 = 두 번째", swords.TryGetFirstReady(out SummonedSword next) && next.Pointer == pointers[1]);
		Check("던진 검은 다시 못 던짐", !swords.Throw(first, null));
		yield return WaitFor(() => first.State == SummonedSwordState.Dropped, 3f);
		Check("빗나가면 땅에 떨어짐", first.State == SummonedSwordState.Dropped);
		Check("검 돌아오기", swords.Recall(first) && first.State == SummonedSwordState.Ready && swords.ReadyCount == 5);
		yield return new WaitForSeconds(0.5f);

		// 4. 줍기 (가짜 Z 키)
		swords.Throw(first, null);
		yield return WaitFor(() => first.State == SummonedSwordState.Dropped, 3f);
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.Z));
		yield return null;
		yield return null;
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
		yield return WaitFor(() => first.State == SummonedSwordState.Ready, 1f);
		Check("Z로 떨어진 소환검 줍기", first.State == SummonedSwordState.Ready);
		PhysicalSword physical = swords.GetComponent<PhysicalSword>();
		if (physical != null) Check("물리 검은 그대로 손에", physical.State == SwordState.InHand);

		// 5. 해제
		int heapBefore = memory.HeapUsed;
		Check("첫 번째 검 해제", swords.Free(pointers[0]));
		Check("해제하면 4개, heap 반환", swords.Count == 4 && memory.HeapUsed == heapBefore - heapSize);
		Check("해제한 포인터는 못 찾음", !swords.TryGetSword(pointers[0], out _));
		Check("해제한 포인터 다시 해제는 false", !swords.Free(pointers[0]));
		Check("다음에 쓸 검 = 두 번째", swords.TryGetFirstReady(out next) && next.Pointer == pointers[1]);

		// 6. 누수
		FunctionTask failedTask = (FunctionTask)FormatterServices.GetUninitializedObject(typeof(FunctionTask));
		int leakedPointer = swords.Summon(failedTask, heapSize);
		Check("작업이 소환 (5개째)", leakedPointer != HeapStorage.NullPointer && swords.Count == 5);
		EndTask(failedTask, FunctionTaskEndReason.Failed);
		memory.Heap.HandleTaskEnded(failedTask);
		SummonedSword leaked = null;
		foreach (SummonedSword s in swords.Swords) if (s.Pointer == leakedPointer) leaked = s;
		Check("실패로 끝난 작업의 소환검은 누수", leaked != null && leaked.State == SummonedSwordState.Leaked);
		SpriteRenderer leakedSprite = leaked != null ? leaked.GetComponentInChildren<SpriteRenderer>() : null;
		Check("누수되면 반투명 (SummonedSwordView)", leakedSprite != null && leakedSprite.color.a < 1f);
		Check("누수돼도 개수에 남음 (5개, 사용 가능 4개)", swords.Count == 5 && swords.ReadyCount == 4);
		Check("누수된 포인터는 해제 못함", !swords.Free(leakedPointer));
		Check("누수로 자리가 차서 더 소환 못함", swords.Summon(null, heapSize) == HeapStorage.NullPointer);

		// 7. 사용 가능한 검 없음 → 소환검 스킬 실패
		foreach (SummonedSword s in swords.Swords) swords.Throw(s, null);
		Check("모두 던지면 사용 가능 0개", swords.ReadyCount == 0);
		if (executor != null && PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.SummonedThrust, out PredefinedFunction summonedThrust))
		{
			FunctionTask task = executor.Cast(summonedThrust);
			if (task == null) Check("소환검 찌르기 시전", false);
			else
			{
				yield return WaitFor(() => task.IsEnded, 5f);
				Check("사용 가능한 소환검이 없으면 소환검 찌르기 실패", task.IsEnded && task.EndReason == FunctionTaskEndReason.Failed);
			}
		}
		yield return new WaitForSeconds(1f);

		// 8. 영역 비우기
		memory.Heap.Clear();
		yield return null;
		Check("영역 비우기로 모두 사라짐", swords.Count == 0 && swords.Swords.Count == 0 && leaked == null);

		if (failures == 0) Debug.Log("[SummonTest] 전체 통과");
		else Debug.LogError($"[SummonTest] 실패 {failures}개");
	}

	// 테스트용 가짜 작업의 끝난 이유를 넣는다. 실제로는 실행기가 정한다
	static void EndTask(FunctionTask task, FunctionTaskEndReason reason)
	{
		PropertyInfo property = typeof(FunctionTask).GetProperty(nameof(FunctionTask.EndReason));
		property.GetSetMethod(true).Invoke(task, new object[] { reason });
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
