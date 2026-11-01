using UnityEngine;

/// <summary>
/// 입력 대기(GetBug, InputRequested, ProvideInput), 안쪽 함수 호출(동기 호출의 프레임, 비동기 호출의 작업 수), 슬롯 직접 시전의 대상 argument(Cast(스킬, 대상)) 테스트용 컴포넌트.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고, Inspector의 Bug 칸에 씬의 몬스터 하나를 연결한다. 콘솔 로그로 결과를 확인한다.<br/>
/// 슬롯 키 대신 이 컴포넌트가 InputRequested를 받아 ProvideInput을 직접 부른다.<br/>
/// 마지막 테스트가 연결한 몬스터를 죽인다. 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[InputWaitTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class InputWaitTest : MonoBehaviour
{
	[SerializeField, Tooltip("GetBug에 넣을 몬스터. 마지막 테스트에서 죽인다")]
	Monster bug;

	[SerializeField, Min(1), Tooltip("입력 대기 테스트에서 값을 넣기 전에 기다리는 틱 수")]
	int waitTicks = 10;

	static readonly VariableType BugType = VariableType.TargetOf(TargetKind.Bug);

	// 테스트 단계. 앞 단계의 작업이 끝나야 다음 단계를 시전한다. 다른 작업의 프레임이 스택 사용량에 섞이지 않게 하기 위함
	enum Step { Wait, Immediate, NoTarget, Interrupt, Sync, Async, SlotCast, SlotCastNoTarget, Dead, Done }

	FunctionExecutor executor;
	ProcessMemory memory;
	Step step = Step.Wait;
	// 지금 단계에서 시전한 작업. null이면 다음 FixedUpdate에 지금 단계를 시전한다
	FunctionTask current;
	bool casting;
	int failedCount;

	// 입력 대기 단계: 요청을 받은 틱과 그때의 스택 사용량
	int requestTick = -1;
	int stackAtRequest;
	// TestTargetFunction이 받은 몬스터
	Monster readTarget;
	// 동기 호출 단계: 진행 중에 본 가장 큰 스택 사용량
	int maxStack;
	// 비동기 호출 단계: 이 단계에서 시작한 작업 수
	int startedTasks;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		executor.InputRequested += OnInputRequested;
		executor.TaskStarted += OnTaskStarted;
		executor.TaskEnded += OnTaskEnded;
		TestTargetFunction.TargetRead += OnTargetRead;

		if (bug == null)
		{
			Debug.LogError("[InputWaitTest] Inspector의 Bug 칸에 씬의 몬스터를 연결합니다.", this);
			step = Step.Done;
		}
	}

	void OnDestroy()
	{
		TestTargetFunction.TargetRead -= OnTargetRead;
	}

	void FixedUpdate()
	{
		if (step == Step.Done) return;

		if (!casting)
		{
			casting = true;
			CastStep();
			return;
		}

		switch (step)
		{
			case Step.Wait:
				// 일부러 waitTicks틱 동안 값을 넣지 않는다. 그동안 작업이 멈춰 있고 프레임이 그대로인지 본다
				if (requestTick < 0 || executor.CurrentTick - requestTick < waitTicks) return;

				Check("Wait: 기다리는 동안 WaitingInput 유지", current.State == FunctionTaskState.WaitingInput, $"실제 {current.State}");
				Check("Wait: 기다리는 동안 프레임이 스택에 남음", memory.StackUsed == stackAtRequest, $"요청 때 {stackAtRequest}, 지금 {memory.StackUsed}");
				Check("Wait: ProvideInput 성공", executor.ProvideInput(current, RuntimeValue.FromBug(bug)));
				requestTick = -1;
				break;

			case Step.Interrupt:
				// 기다리는 중에 실행 중단 공격을 맞은 것처럼 끊는다
				if (requestTick < 0 || executor.CurrentTick - requestTick < 2) return;

				executor.InterruptAll();
				requestTick = -1;
				break;

			case Step.Sync:
				maxStack = Mathf.Max(maxStack, memory.StackUsed);
				break;
		}
	}

	void CastStep()
	{
		readTarget = null;
		switch (step)
		{
			case Step.Wait:
			case Step.Immediate:
			case Step.NoTarget:
			case Step.Interrupt:
				current = CastGetBugFunction(step.ToString());
				break;

			case Step.Sync:
				// 사용자 정의 함수(프레임 1) 안의 동기 함수 A(프레임 1). A가 실행되는 동안 스택 2, 끝나면 0이어야 한다
				maxStack = 0;
				current = executor.Cast(new UserFunction("Sync", new TestFunctionA()));
				break;

			case Step.Async:
				// 사용자 정의 함수 안의 비동기 함수. 바깥 작업과 비동기 함수의 작업, 모두 2개가 시작해야 한다
				startedTasks = 0;
				current = executor.Cast(new UserFunction("Async", new TestAsyncFunction()));
				break;

			case Step.SlotCast:
				// 슬롯에서 스킬을 바로 시전하며 고른 몬스터를 argument로 넘긴다(SlotCaster가 키를 뗄 때 하는 일)
				current = executor.Cast(new TestTargetFunction(), RuntimeValue.FromBug(bug));
				break;

			case Step.SlotCastNoTarget:
				current = executor.Cast(new TestTargetFunction(), RuntimeValue.FromBug(null));
				break;

			case Step.Dead:
				// 몬스터를 죽인 뒤, 죽은 몬스터를 입력으로 넣어 스킬에 넘긴다
				bug.TakeDamage(bug.MaxHp);
				current = CastGetBugFunction("Dead");
				break;
		}

		if (current == null)
		{
			Check($"{step}: 시전 성공", false);
			NextStep();
		}
	}

	// Target b = GetBug(); TestTargetFunction(b);
	FunctionTask CastGetBugFunction(string name)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.Add(BlockData.Declare(BugType, "b", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));
		data.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestTargetFunction, SlotData.FromVariable("b")));

		if (FunctionDataConverter.TryConvert(data, out UserFunction function)) return executor.Cast(function);
		return null;
	}

	void OnInputRequested(FunctionTask task, PredefinedFunction requester)
	{
		if (task != current) return;

		Check($"{step}: GetBug가 요청하고 작업이 WaitingInput",
			requester.Id == PredefinedFunctionIds.GetBug && task.State == FunctionTaskState.WaitingInput,
			$"실제 {requester.Id}, {task.State}");

		switch (step)
		{
			case Step.Wait:
				// 사용자 정의 함수의 프레임(1 + 지역 변수 b 1) + GetBug의 프레임(1) = 3
				stackAtRequest = memory.StackUsed;
				Check("Wait: 요청할 때 스택 3", stackAtRequest == 3, $"실제 {stackAtRequest}");
				requestTick = executor.CurrentTick;
				break;

			case Step.Interrupt:
				requestTick = executor.CurrentTick;
				break;

			// 슬롯 키가 눌려 있지 않을 때처럼, 이 event 안에서 바로 값을 넣는다
			case Step.Immediate:
			case Step.Dead:
				Check($"{step}: event 안에서 ProvideInput 성공", executor.ProvideInput(task, RuntimeValue.FromBug(bug)));
				break;
			case Step.NoTarget:
				Check("NoTarget: event 안에서 ProvideInput 성공", executor.ProvideInput(task, RuntimeValue.FromBug(null)));
				break;
		}
	}

	void OnTaskStarted(FunctionTask task)
	{
		if (step == Step.Async) startedTasks++;
	}

	void OnTargetRead(Monster target) => readTarget = target;

	void OnTaskEnded(FunctionTask task)
	{
		switch (step)
		{
			case Step.Wait:
			case Step.Immediate:
			case Step.SlotCast:
				if (task != current) return;
				Check($"{step}: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}");
				Check($"{step}: TestTargetFunction이 넣은 몬스터를 받음", readTarget == bug, $"실제 {(readTarget == null ? "없음" : readTarget.name)}");
				break;

			case Step.NoTarget:
			case Step.SlotCastNoTarget:
			case Step.Dead:
				if (task != current) return;
				Check($"{step}: InvalidTarget으로 실패", task.IsFailed && task.Failure == FunctionFailure.InvalidTarget, $"실제 {task.EndReason}, {task.Failure}");
				Check($"{step}: TestTargetFunction은 시작하지 않음", readTarget == null);
				break;

			case Step.Interrupt:
				if (task != current) return;
				Check("Interrupt: 기다리는 중에 Interrupted로 끝남", task.EndReason == FunctionTaskEndReason.Interrupted, $"실제 {task.EndReason}");
				Check("Interrupt: 프레임을 모두 돌려줌", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
				Check("Interrupt: 끝난 작업에 ProvideInput하면 false", !executor.ProvideInput(task, RuntimeValue.FromBug(bug)));
				break;

			case Step.Sync:
				if (task != current) return;
				Check("Sync: 동기 호출 중 스택 2 (바깥 1 + A 1)", maxStack == 2, $"실제 최대 {maxStack}");
				Check("Sync: 끝나면 스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
				break;

			case Step.Async:
				// 바깥 작업이 먼저 끝난다. 비동기 함수의 작업이 끝날 때 확인한다
				if (task.Name != "TestAsyncFunction") return;
				Check("Async: 작업 2개 시작 (바깥 + 비동기 함수)", startedTasks == 2, $"실제 {startedTasks}");
				break;

			default:
				return;
		}

		NextStep();
	}

	void NextStep()
	{
		step++;
		current = null;
		casting = false;

		if (step != Step.Done) return;
		if (failedCount == 0) Debug.Log("[InputWaitTest] 전체 통과");
		else Debug.LogError($"[InputWaitTest] 실패 {failedCount}개");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[InputWaitTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
