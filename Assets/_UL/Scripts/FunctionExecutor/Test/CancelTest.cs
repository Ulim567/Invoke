using UnityEngine;

/// <summary>
/// 슬롯 작업 취소(FunctionExecutor.Cancel) 테스트용 컴포넌트(#143). 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// - 시작: 슬롯 원본 SlotHold(안에 TestHoldMallocFunction, 힙 2칸), OtherLoop(while (true) { }), HoldInside(안에 TestFunctionB)를 시전한다.<br/>
/// - 틱 5: Cancel(SlotHold) → 그 작업만 Cancelled, 다른 작업은 계속. 스킬 TestFunctionB는 HoldInside 안에서 실행 중이지만, 슬롯에서 시전한 것이 아니라 Cancel이 false.<br/>
/// - 틱 8: SlotHold를 다시 시전하고 같은 틱에 Cancel → 다음 틱을 기다리던 작업도 Cancelled, 시작하지 않음. 한 번 더 Cancel하면 false.<br/>
/// - 틱 10: 끝난 이유, 누수 1개, 스택을 확인한다.<br/>
/// 남은 칸이 7칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[CancelTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CancelTest : MonoBehaviour
{
	// 슬롯에 들어 있는 원본처럼 쓴다. 같은 원본으로 시전한 작업만 Cancel이 끊는다
	readonly UserFunction slotHold = new UserFunction("SlotHold", new TestHoldMallocFunction());
	// 다른 함수 안에서 실행 중인 스킬과 같은 스킬의 슬롯 원본. 이 원본으로는 시전하지 않는다
	readonly TestFunctionB skillSlot = new TestFunctionB();

	FunctionExecutor executor;
	ProcessMemory memory;
	FunctionTask slotTask, otherLoop, holdInside, pendingSlotTask;
	// 누수된 객체 수. ObjectLeaked가 불릴 때마다 센다
	int leakedCount;
	// 다음 틱을 기다리다 취소된 작업이 진행을 시작했는지
	bool pendingStarted;
	int failedCount;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		memory.Heap.ObjectLeaked += _ => leakedCount++;
		executor.TaskStarted += task => { if (task == pendingSlotTask) pendingStarted = true; };

		slotTask = executor.Cast(slotHold);
		otherLoop = executor.Cast(new UserFunction("OtherLoop",
			new WhileBlock(new LiteralValue(new VariableType(VariableTypeKind.Bool), "true"))));
		holdInside = executor.Cast(new UserFunction("HoldInside", new TestFunctionB()));
	}

	void FixedUpdate()
	{
		int tick = executor.CurrentTick;
		switch (step)
		{
			case 0 when tick >= 5:
				// SlotHold 2칸 + OtherLoop 1칸 + HoldInside 2칸
				Check("취소 전: 스택 5", memory.StackUsed == 5, $"실제 {memory.StackUsed}");
				Check("취소 전: SlotHold 실행 중", executor.IsRunning(slotHold));
				Check("Cancel(SlotHold)이 true", executor.Cancel(slotHold));
				// 프레임은 끊는 즉시 돌아온다
				Check("취소 직후: 스택 3 (SlotHold 2칸 반환)", memory.StackUsed == 3, $"실제 {memory.StackUsed}");

				// 같은 스킬이 다른 함수 안에서 실행 중이면 IsRunning은 true지만, 슬롯에서 시전한 작업이 없으므로 끊지 않는다
				Check("스킬 슬롯: 다른 함수 안의 같은 스킬 때문에 IsRunning true", executor.IsRunning(skillSlot));
				Check("스킬 슬롯: Cancel이 false", !executor.Cancel(skillSlot));
				break;

			case 1 when tick >= 8:
				pendingSlotTask = executor.Cast(slotHold);
				Check("다시 시전 가능 (앞 작업이 끝나 실행 중이 아님)", pendingSlotTask != null);
				Check("다음 틱을 기다리는 작업도 Cancel이 true", executor.Cancel(slotHold));
				Check("끊을 작업이 없으면 Cancel이 false", !executor.Cancel(slotHold));
				break;

			// 끊긴 작업이 목록에서 빠지며 누수 판단까지 끝난 뒤에 확인한다
			case 2 when tick >= 10:
				CheckEnd(slotTask, FunctionTaskEndReason.Cancelled);
				CheckEnd(pendingSlotTask, FunctionTaskEndReason.Cancelled);
				Check("기다리던 작업: 진행을 시작하지 않음", !pendingStarted);
				Check("OtherLoop, HoldInside는 계속 진행", !otherLoop.IsEnded && !holdInside.IsEnded, $"OtherLoop {otherLoop.State}, HoldInside {holdInside.State}");
				Check("스택 3", memory.StackUsed == 3, $"실제 {memory.StackUsed}");
				// 취소도 정상 종료가 아니므로, 끊긴 TestHoldMallocFunction의 객체는 숨은 시스템 전역으로 가지 않고 누수가 된다
				Check("누수 1개, 힙 2", leakedCount == 1 && memory.HeapUsed == 2, $"누수 {leakedCount}개, {memory.Heap.GetUsageByOwner()}");

				if (failedCount == 0) Debug.Log("[CancelTest] 전체 통과");
				else Debug.LogError($"[CancelTest] 실패 {failedCount}개");

				// OtherLoop는 끝나지 않으므로 테스트를 마치며 끊는다
				executor.InterruptAll();
				break;

			default:
				return;
		}
		step++;
	}

	void CheckEnd(FunctionTask task, FunctionTaskEndReason expected)
	{
		Check($"{task.Name}: {expected}로 끝남", task.EndReason == expected, $"실제 {task.EndReason}");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[CancelTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
