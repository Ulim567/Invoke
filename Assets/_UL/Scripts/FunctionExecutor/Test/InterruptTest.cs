using UnityEngine;

/// <summary>
/// 실행 중단(InterruptAll) 테스트용 컴포넌트(#143). 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// - 시작: TestMallocFunction(5틱), TestLongMallocFunction(20틱), 사용자 정의 함수 안의 TestFunctionA(30틱)를 함께 시전한다. 두 malloc 함수는 1틱째에 TestSword를 힙 2칸에 올린다.<br/>
/// - 틱 7: TestMallocFunction은 이미 정상 종료해 객체가 숨은 시스템 전역에 있다. 다음 틱을 기다리는 작업 하나를 더 시전한 뒤 InterruptAll을 부른다.<br/>
/// - 확인: 진행 중이던 작업과 기다리던 작업이 모두 Interrupted, 스택 0, 끊긴 TestLongMallocFunction의 객체만 누수, 힙 사용량 4칸이 그대로 남음.<br/>
/// 남은 칸이 7칸 이상이어야 한다(스택 3칸 + 힙 4칸). 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[InterruptTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class InterruptTest : MonoBehaviour
{
	FunctionExecutor executor;
	ProcessMemory memory;
	FunctionTask finishedMalloc, longMalloc, userFunction, pending;
	// 누수된 객체 수. ObjectLeaked가 불릴 때마다 센다
	int leakedCount;
	// 기다리던 작업이 진행을 시작했는지. 진행하기 전에 끊긴 작업은 TaskStarted 없이 끝나야 한다
	bool pendingStarted;
	int failedCount;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		memory.Heap.ObjectLeaked += _ => leakedCount++;
		executor.TaskStarted += task => { if (task == pending) pendingStarted = true; };

		// 셋 다 서로 다른 스킬이라 기다리지 않고 함께 진행한다. 같은 스킬은 동시에 실행되지 않기 때문
		finishedMalloc = executor.Cast(new TestMallocFunction());
		longMalloc = executor.Cast(new TestLongMallocFunction());
		// 사용자 정의 함수의 프레임 위에 안쪽 스킬의 프레임이 쌓인 작업도 끊기는지 보기 위함
		userFunction = executor.Cast(new UserFunction("InterruptUserFunction", new TestFunctionA()));
	}

	void FixedUpdate()
	{
		int tick = executor.CurrentTick;
		switch (step)
		{
			case 0 when tick >= 7:
				Check("끊기 전: TestMallocFunction 정상 종료", finishedMalloc.EndReason == FunctionTaskEndReason.Completed, $"실제 {finishedMalloc.EndReason}");
				// TestLongMallocFunction 1칸 + InterruptUserFunction 1칸 + 그 안의 TestFunctionA 1칸
				Check("끊기 전: 스택 3", memory.StackUsed == 3, $"실제 {memory.StackUsed}");
				Check("끊기 전: 힙 4 (시스템 전역 2, 지역 2)", memory.HeapUsed == 4, $"실제 {memory.HeapUsed}, {memory.Heap.GetUsageByOwner()}");

				// 다음 틱을 기다리는 작업도 함께 끊기는지 보기 위해, 끊기 직전에 시전한다
				pending = executor.Cast(new TestFunctionB());
				executor.InterruptAll();
				// 프레임은 끊는 즉시 돌아온다. 작업이 목록에서 빠지는 것은 다음 틱이 끝날 때다
				Check("끊은 직후: 스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
				break;

			// 끊긴 작업이 목록에서 빠지며 누수 판단까지 끝난 뒤에 확인한다
			case 1 when tick >= 9:
				CheckInterrupted(longMalloc);
				CheckInterrupted(userFunction);
				CheckInterrupted(pending);
				Check("기다리던 작업: 진행을 시작하지 않음", !pendingStarted);
				Check("스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");

				// 끊긴 TestLongMallocFunction은 정상 종료가 아니라 포인터가 숨은 시스템 전역으로 옮겨지지 않는다
				Check("누수 1개 (끊긴 TestLongMallocFunction의 객체)", leakedCount == 1, $"실제 {leakedCount}개");
				HeapUsage usage = memory.Heap.GetUsageByOwner();
				Check("힙 4 그대로 (시스템 전역 2, 누수 2)", memory.HeapUsed == 4 && usage.SystemGlobals == 2 && usage.Leaked == 2, $"실제 {memory.HeapUsed}, {usage}");
				// 누수 객체도 개수에 들어간다
				Check("TestSword 2개 (누수 포함)", memory.Heap.CountObjects<TestSword>() == 2, $"실제 {memory.Heap.CountObjects<TestSword>()}개");

				if (failedCount == 0) Debug.Log("[InterruptTest] 전체 통과");
				else Debug.LogError($"[InterruptTest] 실패 {failedCount}개");
				break;

			default:
				return;
		}
		step++;
	}

	void CheckInterrupted(FunctionTask task)
	{
		Check($"{task.Name}: Interrupted로 끝남", task.EndReason == FunctionTaskEndReason.Interrupted, $"실제 {task.EndReason}");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[InterruptTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
