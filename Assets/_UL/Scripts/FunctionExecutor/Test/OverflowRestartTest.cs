using UnityEngine;

/// <summary>
/// Overflow가 나면 프로세스를 새로 시작하는지(FailureHandling.RestartProcess) 테스트하는 컴포넌트(#143). 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// - 시작: 전역 변수 int count = 3;을 저장하고, 첫 틱에 count를 9로 바꾼다. 사용자 정의 함수 안의 TestHoldMallocFunction(힙 2칸), while (true) { }, TestLongMallocFunction(20틱, 힙 2칸)을 시전한다.<br/>
/// - 틱 25: 프레임 20칸짜리 함수를 부르는 함수를 시전해 Overflow를 낸다.<br/>
/// - 확인: Overflow를 낸 작업은 Failed(Overflow), 다른 작업은 Interrupted, 스택 0, 힙 0(소환검 같은 객체도 지움), count가 초기값 3, 재시작 뒤 새 작업 없음.<br/>
/// 언어 규칙 파일의 Overflow 줄이 RestartProcess여야 한다. 메모리 크기가 10칸이어야 한다(20칸 프레임이 Overflow를 내도록).<br/>
/// 끝나면 시작할 때의 전역 변수 파일로 되돌린다. 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[OverflowRestartTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class OverflowRestartTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	ProcessMemory memory;
	// 테스트를 시작할 때의 전역 변수 파일. 이 컴퓨터에 파일이 하나뿐이라, 끝나면 되돌려야 다른 테스트의 Free가 바뀌지 않는다
	GlobalVariablesData originalFile;
	FunctionTask holdNested, loop, overflowCaller;
	// Clear로 지운 객체 수. ObjectCleared가 불릴 때마다 센다
	int clearedCount;
	// Overflow를 낸 작업이 끝난 뒤에 진행을 시작한 작업 수. 프로세스를 새로 시작한 뒤에는 작업이 생기지 않아야 한다
	int startedAfterRestart;
	// Overflow를 낸 작업이 끝났는지. 재시작이 일어났는지는 이 값이 아니라 마지막 단계의 확인 항목(힙 0, count 3)으로 본다
	bool overflowEnded;
	// 전역 변수 파일을 시작할 때의 파일로 되돌렸는지. 테스트가 끝나지 못하고 멈춰도 한 번은 되돌리기 위함
	bool fileRestored;
	int failedCount;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		originalFile = GlobalVariablesStorage.Load();
		memory.Heap.ObjectCleared += _ => clearedCount++;
		executor.TaskStarted += _ => { if (overflowEnded) startedAfterRestart++; };
		executor.TaskEnded += task => { if (task == overflowCaller) overflowEnded = true; };

		bool restartRule = GetComponent<LanguageRule>().TryFindHandling(name, FunctionFailure.Overflow, out FailureHandling handling) && handling == FailureHandling.RestartProcess;
		Check("언어 규칙 파일의 Overflow 줄이 RestartProcess", restartRule, $"실제 {handling}");

		// 저장하면 RestartProcess가 불려 count가 초기값 3으로 만들어진다. 첫 틱에 값을 바꿔, 재시작 때 초기값으로 돌아가는지 본다
		GlobalVariablesData file = new GlobalVariablesData();
		file.Declarations.Add(BlockData.Declare(Int, "count", SlotData.FromLiteral(Int, "3")));
		Check("전역 변수 파일 저장", executor.TrySaveGlobalVariables(file));

		// 서로 다른 스킬만 쓴다. 같은 스킬은 동시에 실행되지 않고 기다리기 때문
		holdNested = executor.Cast(new UserFunction("HoldNested", new TestHoldMallocFunction()));
		loop = executor.Cast(new UserFunction("Loop",
			new WhileBlock(new LiteralValue(new VariableType(VariableTypeKind.Bool), "true"))));
		executor.Cast(new TestLongMallocFunction());
	}

	void FixedUpdate()
	{
		int tick = executor.CurrentTick;
		switch (step)
		{
			// Start에서 바꾸지 않는다. FunctionExecutor.Start가 이 컴포넌트의 Start보다 늦게 불리면 전역 변수를 다시 만들어 값이 3으로 돌아가기 때문
			case 0 when tick >= 1:
				memory.Globals.TrySet("count", RuntimeValue.FromInt(9));
				break;

			case 1 when tick >= 25:
				// HoldNested 2칸(사용자 정의 함수 + 안의 스킬) + Loop 1칸
				Check("Overflow 전: 스택 3", memory.StackUsed == 3, $"실제 {memory.StackUsed}");
				// TestHoldMallocFunction은 진행 중이라 지역 2칸, 끝난 TestLongMallocFunction은 시스템 전역 2칸
				Check("Overflow 전: 힙 4", memory.HeapUsed == 4, $"실제 {memory.HeapUsed}, {memory.Heap.GetUsageByOwner()}");
				CheckCount("Overflow 전: count 9", "9");

				// 바깥 프레임 1칸은 시전 전 검사를 통과하고, 안쪽 20칸 프레임이 메모리 크기를 넘어 실행 중에 Overflow가 난다
				overflowCaller = executor.Cast(new UserFunction("OverflowCaller", new UserFunction("TooBig", 20)));
				break;

			// Overflow가 난 틱의 끝에 끝난 작업이 목록에서 빠진다. 그다음 틱에도 새 작업이 시작하지 않는지 보기 위해 한 틱 더 기다린다
			case 2 when overflowEnded && tick >= 28:
				Check("OverflowCaller: Failed(Overflow)로 끝남", overflowCaller.EndReason == FunctionTaskEndReason.Failed && overflowCaller.Failure == FunctionFailure.Overflow, $"실제 {overflowCaller.EndReason}, {overflowCaller.Failure}");
				CheckInterrupted(holdNested);
				CheckInterrupted(loop);
				Check("스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
				Check("힙 0 (객체 2개를 지움)", memory.HeapUsed == 0 && clearedCount == 2, $"실제 {memory.HeapUsed}, 지운 객체 {clearedCount}개");
				CheckCount("count가 초기값 3", "3");
				Check("재시작 뒤 새 작업 없음", startedAfterRestart == 0, $"실제 {startedAfterRestart}개");

				if (failedCount == 0) Debug.Log("[OverflowRestartTest] 전체 통과");
				else Debug.LogError($"[OverflowRestartTest] 실패 {failedCount}개");

				RestoreFile();
				executor.RestartProcess();
				Debug.Log($"[OverflowRestartTest] 끝. 시작할 때의 전역 변수 파일로 되돌림 (DataUsed {memory.DataUsed})");
				break;

			default:
				return;
		}
		step++;
	}

	// 마지막 단계 전에 Play를 멈추거나 오브젝트를 지워도 파일을 되돌린다. 남겨 두면 다음 Play부터 모든 캐릭터의 DataUsed가 1이 되기 때문
	void OnDestroy()
	{
		if (originalFile != null) RestoreFile();
	}

	// 저장 함수를 거치지 않고 되돌린다. 원래 파일이 지금 메모리 크기에서 저장 검사에 걸려도 그대로 되돌리기 위함
	void RestoreFile()
	{
		if (fileRestored) return;

		GlobalVariablesStorage.Save(originalFile);
		fileRestored = true;
	}

	void CheckCount(string name, string expected)
	{
		bool found = memory.Globals.TryGet("count", out RuntimeValue value);
		Check(name, found && value.ToString() == expected, found ? $"실제 {value}" : "전역 변수 없음");
	}

	void CheckInterrupted(FunctionTask task)
	{
		Check($"{task.Name}: Interrupted로 끝남", task.EndReason == FunctionTaskEndReason.Interrupted, $"실제 {task.EndReason}");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[OverflowRestartTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
