using System.Linq;
using UnityEngine;

/// <summary>
/// 논리 블록, Not 블록과 조건 값 변환(NumericRules.TryConvertToCondition) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙(ImplicitAll)이고, 실패 처리 표에 TypeMismatch 줄(처리 EndTask, 분류 CompileError)이 있어야 모두 통과한다.<br/>
/// 메모리를 10칸 쓰므로 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class LogicTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	FunctionExecutor executor;
	// 테스트가 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		startTick = executor.CurrentTick;
		executor.TaskEnded += OnTaskEnded;

		TestConditions(GetComponent<LanguageRule>().Rule);
		CastFunctions();
	}

	// 테스트 1: 언어 규칙마다 조건 값 변환
	void TestConditions(LanguageRuleData c)
	{
		Check("C 파일의 규칙이 ImplicitAll", c.NumericConversion == NumericConversion.ImplicitAll);

		CheckCondition(c, "true", RuntimeValue.FromBool(true), true);
		CheckCondition(c, "5", RuntimeValue.FromInt(5), true);
		CheckCondition(c, "0", RuntimeValue.FromInt(0), false);
		CheckCondition(c, "0.0", RuntimeValue.FromFloat(0f), false);
		CheckCondition(c, "'a'", RuntimeValue.FromChar('a'), true);
		CheckCondition(c, "NULL", RuntimeValue.Null, false);
		// 포인터 값 5는 테스트용이다. 조건 값 변환은 heap을 찾지 않고 NULL인지만 본다
		CheckCondition(c, "NULL이 아닌 포인터", RuntimeValue.FromPointer(SwordPointer, 5), true);
		CheckConditionFails(c, "\"a\"", RuntimeValue.FromString("a"));

		LanguageRuleData widening = LanguageRuleData.CreateForTest(NumericConversion.WideningOnly, true);
		CheckCondition(widening, "[WideningOnly] true", RuntimeValue.FromBool(true), true);
		CheckConditionFails(widening, "[WideningOnly] 5", RuntimeValue.FromInt(5));
		CheckConditionFails(widening, "[WideningOnly] 포인터", RuntimeValue.FromPointer(SwordPointer, 5));

		LanguageRuleData strict = LanguageRuleData.CreateForTest(NumericConversion.Strict, false);
		CheckCondition(strict, "[Strict] false", RuntimeValue.FromBool(false), false);
		CheckConditionFails(strict, "[Strict] 'a'", RuntimeValue.FromChar('a'));

		Destroy(widening);
		Destroy(strict);
	}

	// 테스트 2: 함수 데이터로 만든 함수를 실행. 블록 하나 = 1틱. 모두 틱 1부터 함께 진행한다
	void CastFunctions()
	{
		// bool a = false && (1 / 0 == 0); bool b = true || (1 / 0 == 0);
		// 오른쪽을 실행하지 않으므로 DivideByZero가 나지 않는다
		// 틱 1 &&, 틱 2 a 선언, 틱 3 ||, 틱 4 b 선언, 틱 5~6 읽기 → a false, b true
		FunctionData shortCircuit = FunctionData.Create("ShortCircuit");
		shortCircuit.Blocks.Add(BlockData.Declare(Bool, "a", Logic(SlotData.FromLiteral(Bool, "false"), LogicOperator.And, DivideByZeroEqualsZero())));
		shortCircuit.Blocks.Add(BlockData.Declare(Bool, "b", Logic(SlotData.FromLiteral(Bool, "true"), LogicOperator.Or, DivideByZeroEqualsZero())));
		CastWithReads(shortCircuit, ("a", "false"), ("b", "true"));

		// bool b = true && (1 < 2);
		// 틱 1 비교, 틱 2 &&, 틱 3 선언, 틱 4 읽기 → b true
		FunctionData bothSides = FunctionData.Create("BothSides");
		SlotData oneLessTwo = SlotData.FromBlock(BlockData.Compare(SlotData.FromLiteral(Int, "1"), CompareOperator.Less, SlotData.FromLiteral(Int, "2")));
		bothSides.Blocks.Add(BlockData.Declare(Bool, "b", Logic(SlotData.FromLiteral(Bool, "true"), LogicOperator.And, oneLessTwo)));
		CastWithReads(bothSides, ("b", "true"));

		// bool b = !5;
		// 틱 1 Not, 틱 2 선언, 틱 3 읽기 → b false (C는 0이 아니면 참)
		FunctionData notFive = FunctionData.Create("NotFive");
		notFive.Blocks.Add(BlockData.Declare(Bool, "b", SlotData.FromBlock(BlockData.Not(SlotData.FromLiteral(Int, "5")))));
		CastWithReads(notFive, ("b", "false"));

		// string s = "a"; bool b = !s; → 틱 2에 Not 블록이 TypeMismatch (string은 조건으로 쓸 수 없음)
		FunctionData notString = FunctionData.Create("NotString");
		notString.Blocks.Add(BlockData.Declare(String, "s", SlotData.FromLiteral(String, "a")));
		notString.Blocks.Add(BlockData.Declare(Bool, "b", SlotData.FromBlock(BlockData.Not(SlotData.FromVariable("s")))));
		if (Convert(notString, out UserFunction converted)) executor.Cast(converted);
	}

	static SlotData Logic(SlotData left, LogicOperator op, SlotData right) =>
		SlotData.FromBlock(BlockData.Logic(left, op, right));

	// 실행하면 DivideByZero로 실패하는 expression. short-circuit으로 실행되지 않는지 확인하기 위함
	static SlotData DivideByZeroEqualsZero()
	{
		SlotData divide = SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromLiteral(Int, "1"), ArithmeticOperator.Divide, SlotData.FromLiteral(Int, "0")));
		return SlotData.FromBlock(BlockData.Compare(divide, CompareOperator.Equal, SlotData.FromLiteral(Int, "0")));
	}

	// 바꾼 함수 끝에 테스트용 읽기 블록들을 붙여 시전한다. 함수가 끝났을 때의 변수 값을 확인하기 위함
	void CastWithReads(FunctionData data, params (string name, string expected)[] reads)
	{
		if (!Convert(data, out UserFunction converted)) return;

		Block[] readBlocks = reads.Select(r => (Block)new TestReadBlock(new VariableValue(r.name),
			v => Check($"{data.Name}: 끝났을 때 {r.name} {r.expected}", v.ToString() == r.expected, $"실제 {v}"))).ToArray();
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Concat(readBlocks).ToArray()));
	}

	static bool Convert(FunctionData data, out UserFunction converted)
	{
		bool ok = FunctionDataConverter.TryConvert(data, out converted);
		if (!ok) Check($"{data.Name}: 변환 성공", false);
		return ok;
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "ShortCircuit": CheckCompleted(task, tick, 6); break;
			case "BothSides": CheckCompleted(task, tick, 4); break;
			case "NotFive": CheckCompleted(task, tick, 3); break;
			case "NotString":
				FailureCategory? category = task.Failure.HasValue ? GetComponent<LanguageRule>().GetFailureCategory(task.Failure.Value) : null;
				Check($"{task.Name}: 틱 2에 TypeMismatch(CompileError)로 실패",
					task.Failure == FunctionFailure.TypeMismatch && category == FailureCategory.CompileError && tick == 2,
					$"실제 틱 {tick}, {task.Failure}, {category}");
				break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	static void CheckCondition(LanguageRuleData rule, string name, RuntimeValue value, bool expected)
	{
		bool ok = NumericRules.TryConvertToCondition(value, rule, out bool condition);
		Check($"조건 {name} = {expected}", ok && condition == expected, ok ? $"실제 {condition}" : "바꾸지 못함");
	}

	static void CheckConditionFails(LanguageRuleData rule, string name, RuntimeValue value)
	{
		bool ok = NumericRules.TryConvertToCondition(value, rule, out bool condition);
		Check($"조건 {name} → TypeMismatch", !ok, $"실제 결과 {condition}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[LogicTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
