using UnityEngine;

/// <summary>
/// 기다리기 블록(WaitBlock) 테스트용 컴포넌트(#502). 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// - 1틱이 0.02초(Fixed Timestep 0.02, Fixed Updates Per Tick 1)라고 보고 틱 수를 확인한다.<br/>
/// - 1단계: 틱 수(0.5초, 0.03초, 0, 음수, expression), 타입 불일치, 빈 초 칸의 변환 거부를 확인한다. 함께 시전하므로 메모리를 7칸 쓴다.<br/>
/// - 2단계: 기다리는 중에 InterruptAll. 3단계: 기다리는 중에 슬롯 취소(Cancel).<br/>
/// 연결한 언어 규칙 파일이 C 규칙이고 실패 처리 표에 TypeMismatch 줄이 있어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[WaitTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class WaitTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);

	// 1단계에서 시전하는 함수 수. 모두 끝나면 2단계로 넘어간다
	const int FirstCount = 6;
	// 2·3단계에서 시전한 뒤 끊을 때까지의 틱. 1초(50틱)를 기다리는 중간에 끊기 위함
	const int TicksBeforeStop = 10;

	FunctionExecutor executor;
	ProcessMemory memory;
	// 지금 단계를 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다
	int startTick;
	int firstEndedCount;
	int failedCount;
	// 지금 단계. 1: 틱 수, 2: 실행 중단, 3: 슬롯 취소, 4: 끝
	int phase;
	// 2·3단계에서 끊을 작업. 끊고 나면 null로 비운다
	FunctionTask waiting;
	// 3단계에서 슬롯에서 넘긴 것처럼 시전한 원본. Cancel이 이 원본으로 작업을 찾는다
	UserFunction cancelSource;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		executor.TaskEnded += OnTaskEnded;

		Check("1틱 0.02초", Mathf.Approximately(executor.SecondsPerTick, 0.02f), $"실제 {executor.SecondsPerTick}");
		CastFirst();
	}

	// 1단계: 블록이 쓰는 틱 전체가 적은 시간(올림), 0·음수는 1틱
	void CastFirst()
	{
		phase = 1;
		startTick = executor.CurrentTick;

		// 0.5 / 0.02 = 25. float 오차로 26틱이 되지 않아야 한다
		CastWait("Wait05", Literal(Float, "0.5"));
		// 0.03 / 0.02 = 1.5 → 올려서 2틱
		CastWait("Wait003", Literal(Float, "0.03"));
		CastWait("WaitZero", Literal(Int, "0"));
		CastWait("WaitNegative", Literal(Int, "-1"));

		// float t = 0.1; wait(t * 2); → 선언 1틱, 곱하기 1틱, 기다리기 10틱 = 12틱
		FunctionData expression = FunctionData.Create("WaitExpression");
		expression.Blocks.Add(BlockData.Declare(Float, "t", Literal(Float, "0.1")));
		expression.Blocks.Add(BlockData.Wait(SlotData.FromBlock(
			BlockData.Arithmetic(SlotData.FromVariable("t"), ArithmeticOperator.Multiply, Literal(Int, "2")))));
		if (Convert(expression, out UserFunction converted)) executor.Cast(converted);

		// 초 칸에 string → 틱 1에 TypeMismatch
		CastWait("WaitString", Literal(String, "a"));

		// 빈 초 칸(함수 탭에서 놓은 그대로) → 변환 거부. 변환기가 콘솔 오류를 남기는 것이 정상이다
		FunctionData empty = FunctionData.Create("WaitEmpty");
		empty.Blocks.Add(BlockData.Wait(SlotData.EmptyValue()));
		Check("빈 초 칸: 변환 거부", !FunctionDataConverter.TryConvert(empty, System.Array.Empty<FunctionData>(), out _));
	}

	void FixedUpdate()
	{
		if (waiting == null || executor.CurrentTick - startTick < TicksBeforeStop) return;

		// 기다리는 중: 입력 대기처럼 멈추지 않고 Running, 함수의 프레임 1칸이 남아 있음
		Check($"{waiting.Name}: 기다리는 중 Running", waiting.State == FunctionTaskState.Running, $"실제 {waiting.State}");
		Check($"{waiting.Name}: 기다리는 중 스택 1", memory.StackUsed == 1, $"실제 {memory.StackUsed}");

		if (phase == 2) executor.InterruptAll();
		else Check("Cancel이 작업을 끊음", executor.Cancel(cancelSource));
		// 프레임은 끊는 즉시 돌아온다
		Check($"{waiting.Name}: 끊은 직후 스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
		waiting = null;
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "Wait05": CheckCompleted(task, tick, 25); break;
			case "Wait003": CheckCompleted(task, tick, 2); break;
			case "WaitZero": CheckCompleted(task, tick, 1); break;
			case "WaitNegative": CheckCompleted(task, tick, 1); break;
			case "WaitExpression": CheckCompleted(task, tick, 12); break;
			case "WaitString":
				Check($"{task.Name}: 틱 1에 TypeMismatch로 실패", task.Failure == FunctionFailure.TypeMismatch && tick == 1,
					$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
				break;

			case "WaitInterrupt":
				Check($"{task.Name}: Interrupted로 끝남", task.EndReason == FunctionTaskEndReason.Interrupted, $"실제 {task.EndReason}");
				CastCancel();
				return;
			case "WaitCancel":
				Check($"{task.Name}: Cancelled로 끝남", task.EndReason == FunctionTaskEndReason.Cancelled, $"실제 {task.EndReason}");
				Finish();
				return;
			default:
				return;
		}

		if (++firstEndedCount == FirstCount) CastInterrupt();
	}

	// 2단계: 1초를 기다리는 중에 실행 중단 공격
	void CastInterrupt()
	{
		phase = 2;
		startTick = executor.CurrentTick;
		waiting = CastWait("WaitInterrupt", Literal(Int, "1"));
	}

	// 3단계: 1초를 기다리는 중에 슬롯 취소
	void CastCancel()
	{
		phase = 3;
		startTick = executor.CurrentTick;
		FunctionData data = FunctionData.Create("WaitCancel");
		data.Blocks.Add(BlockData.Wait(Literal(Int, "1")));
		if (!Convert(data, out cancelSource)) return;
		waiting = executor.Cast(cancelSource);
	}

	void Finish()
	{
		phase = 4;
		if (failedCount == 0) Debug.Log("[WaitTest] 전체 통과");
		else Debug.LogError($"[WaitTest] 실패 {failedCount}개");
	}

	// wait(seconds); 하나만 든 함수를 바꿔 시전한다
	FunctionTask CastWait(string name, SlotData seconds)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.Add(BlockData.Wait(seconds));
		return Convert(data, out UserFunction converted) ? executor.Cast(converted) : null;
	}

	static SlotData Literal(VariableType type, string literal) => SlotData.FromLiteral(type, literal);

	bool Convert(FunctionData data, out UserFunction converted)
	{
		bool ok = FunctionDataConverter.TryConvert(data, System.Array.Empty<FunctionData>(), out converted);
		if (!ok) Check($"{data.Name}: 변환 성공", false);
		return ok;
	}

	void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[WaitTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
