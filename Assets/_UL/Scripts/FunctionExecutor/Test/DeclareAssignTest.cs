using System.Linq;
using UnityEngine;

/// <summary>
/// 선언 블록, 대입 블록, 함수 데이터 변환(FunctionDataConverter) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 언어 규칙 파일의 실패 처리 표에 DuplicateVariable, TypeMismatch 줄(처리 EndTask, 분류 CompileError)이 있어야 모두 통과한다.<br/>
/// 메모리를 10칸 쓰므로 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class DeclareAssignTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);

	FunctionExecutor executor;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestRejected();
		CastFunctions();
	}

	// 테스트 1: 아직 실행할 수 없는 함수는 변환에서 거부한다. 이 테스트는 콘솔 오류 2개(「실행할 수 없습니다」)를 내는 것이 정상이다
	void TestRejected()
	{
		FunctionData withIf = FunctionData.Create("WithIf");
		withIf.Blocks.Add(BlockData.Conditional(ConditionalBranch.If, SlotData.FromLiteral(new VariableType(VariableTypeKind.Bool), "true")));
		Check("조건문이 든 함수 거부", !FunctionDataConverter.TryConvert(withIf, out _));

		FunctionData withPosition = FunctionData.Create("WithPosition");
		withPosition.Blocks.Add(BlockData.Declare(new VariableType(VariableTypeKind.Position), "p"));
		Check("위치 타입 변수를 선언한 함수 거부", !FunctionDataConverter.TryConvert(withPosition, out _));
	}

	// 테스트 2: 바꾼 함수를 실행. 모두 틱 1부터 함께 진행하고, 끝나는 틱은 OnTaskEnded가 확인한다
	void CastFunctions()
	{
		// int x = 3; x = 5; → 틱 1 선언, 틱 2 대입, 틱 3 읽기(테스트용)
		FunctionData assign = FunctionData.Create("Assign");
		assign.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));
		assign.Blocks.Add(BlockData.Assign("x", SlotData.FromLiteral(Int, "5")));
		CastWithRead(assign, 2, "x", "5");

		// int x; → 기본값 0. 틱 1 선언, 틱 2 읽기
		FunctionData defaultValue = FunctionData.Create("Default");
		defaultValue.Blocks.Add(BlockData.Declare(Int, "x"));
		CastWithRead(defaultValue, 2, "x", "0");

		// int x = 1; int x = 2; → 틱 2에 DuplicateVariable. 지역 변수 2개라 프레임 3
		FunctionData duplicate = FunctionData.Create("Duplicate");
		duplicate.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "1")));
		duplicate.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "2")));
		CastConverted(duplicate, 3);

		// int x = 0.5; → 틱 1에 TypeMismatch
		FunctionData mismatch = FunctionData.Create("Mismatch");
		mismatch.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Float, "0.5")));
		CastConverted(mismatch, 2);

		// y = 1; (선언 없음) → 틱 1에 UndeclaredVariable. 지역 변수가 없어 프레임 1
		FunctionData undeclared = FunctionData.Create("Undeclared");
		undeclared.Blocks.Add(BlockData.Assign("y", SlotData.FromLiteral(Int, "1")));
		CastConverted(undeclared, 1);
	}

	// 바꾼 함수 끝에 테스트용 읽기 블록을 붙여 시전한다. 함수가 끝났을 때의 변수 값을 확인하기 위함
	void CastWithRead(FunctionData data, int expectedFrameSize, string variableName, string expected)
	{
		if (!Convert(data, expectedFrameSize, out UserFunction converted)) return;

		Block read = new TestReadBlock(new VariableValue(variableName), v => Check($"{data.Name}: 끝났을 때 {variableName} {expected}", v.ToString() == expected, $"실제 {v}"));
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(read).ToArray()));
	}

	void CastConverted(FunctionData data, int expectedFrameSize)
	{
		if (Convert(data, expectedFrameSize, out UserFunction converted)) executor.Cast(converted);
	}

	static bool Convert(FunctionData data, int expectedFrameSize, out UserFunction converted)
	{
		bool ok = FunctionDataConverter.TryConvert(data, out converted);
		Check($"{data.Name}: 변환 성공, 프레임 {expectedFrameSize}", ok && converted.FrameSize == expectedFrameSize,
			ok ? $"실제 프레임 {converted.FrameSize}" : "변환 실패");
		return ok;
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick;
		switch (task.Name)
		{
			case "Assign": CheckCompleted(task, tick, 3); break;
			case "Default": CheckCompleted(task, tick, 2); break;
			case "Duplicate": CheckFailed(task, tick, 2, FunctionFailure.DuplicateVariable); break;
			case "Mismatch": CheckFailed(task, tick, 1, FunctionFailure.TypeMismatch); break;
			case "Undeclared": CheckFailed(task, tick, 1, FunctionFailure.UndeclaredVariable); break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료",
			task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}");
	}

	void CheckFailed(FunctionTask task, int tick, int expectedTick, FunctionFailure expected)
	{
		FailureCategory category = task.Failure.HasValue ? executor.GetFailureCategory(task.Failure.Value) : FailureCategory.NotCodeError;
		Check($"{task.Name}: 틱 {expectedTick}에 {expected}(CompileError)로 실패",
			task.Failure == expected && category == FailureCategory.CompileError && tick == expectedTick,
			$"실제 틱 {tick}, {task.Failure}, {category}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[DeclareAssignTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
