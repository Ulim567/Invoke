using System.Linq;
using UnityEngine;

/// <summary>
/// 값을 돌려주는 함수 블록(PredefinedValueBlock)과 스킬의 Return 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 언어 규칙 파일의 실패 처리 표에 MissingReturnValue 줄(처리 EndTask, 분류 NotCodeError)이 있어야 모두 통과한다.<br/>
/// 콘솔 오류 2개가 나는 것이 정상이다: 반환 값이 없는 함수를 값 칸에 끼운 함수의 변환 거부 1개, TestMissingReturnFunction의 「Return을 부르지 않고 끝났습니다」 1개.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ReturnValueTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	// 테스트가 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		startTick = executor.CurrentTick;
		executor.TaskEnded += OnTaskEnded;

		TestConvert();
		CastFunctions();
	}

	// 테스트 1: 변환
	void TestConvert()
	{
		// int a = TestFunctionA(); → TestFunctionA는 값을 돌려주지 않으므로 거부
		FunctionData voidValue = FunctionData.Create("VoidValue");
		voidValue.Blocks.Add(BlockData.Declare(Int, "a", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestFunctionA))));
		Check("반환 값이 없는 함수를 값 칸에 끼우면 거부 (콘솔 오류 1개가 정상)", !FunctionDataConverter.TryConvert(voidValue, out _));
	}

	// 테스트 2: 실행. 모두 시전한 다음 틱(1)부터 함께 진행한다
	void CastFunctions()
	{
		// int m = TestMissingReturnFunction(); → 틱 1에 MissingReturnValue (콘솔 오류 1개가 정상). 메모리를 먼저 돌려주도록 맨 먼저 시전한다
		FunctionData missing = FunctionData.Create("MissingReturn");
		missing.Blocks.Add(BlockData.Declare(Int, "m", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestMissingReturnFunction))));
		CastConverted(missing);

		// 이슈 완료 조건 3. int n = TestReturnFunction(4);
		// 틱 1~3 스킬, 틱 4 선언, 틱 5 읽기(테스트용) → n 8. 값 칸 안의 스킬 쿨타임(4틱)도 함수의 쿨타임에 들어간다
		FunctionData returned = FunctionData.Create("Returned");
		returned.Blocks.Add(BlockData.Declare(Int, "n", SlotData.FromBlock(
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestReturnFunction, SlotData.FromLiteral(Int, "4")))));
		if (FunctionDataConverter.TryConvert(returned, out UserFunction converted))
		{
			Check("Returned: 값 칸 안의 스킬 쿨타임 4", converted.CooldownTicks == 4, $"실제 {converted.CooldownTicks}");
			Block read = new TestReadBlock(new VariableValue("n"), v => Check("Returned: 끝났을 때 n 8", v.ToString() == "8", $"실제 {v}"));
			executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(read).ToArray()));
		}
		else Check("Returned: 변환 성공", false);

		// TestReturnFunction(1); → 함수 몸통에 놓으면 반환 값을 버리고 틱 3에 끝난다
		FunctionData discarded = FunctionData.Create("Discarded");
		discarded.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestReturnFunction, SlotData.FromLiteral(Int, "1")));
		CastConverted(discarded);
	}

	void CastConverted(FunctionData data)
	{
		if (FunctionDataConverter.TryConvert(data, out UserFunction converted)) executor.Cast(converted);
		else Check($"{data.Name}: 변환 성공", false);
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "Returned": CheckCompleted(task, tick, 5); break;
			case "Discarded": CheckCompleted(task, tick, 3); break;
			case "MissingReturn":
				FailureCategory category = task.Failure.HasValue ? executor.GetFailureCategory(task.Failure.Value) : FailureCategory.CompileError;
				Check("MissingReturn: 틱 1에 MissingReturnValue(NotCodeError)로 실패",
					task.Failure == FunctionFailure.MissingReturnValue && category == FailureCategory.NotCodeError && tick == 1,
					$"실제 틱 {tick}, {task.Failure}, {category}");
				break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료",
			task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ReturnValueTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
