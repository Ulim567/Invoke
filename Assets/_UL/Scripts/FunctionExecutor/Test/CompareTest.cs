using System.Linq;
using UnityEngine;

/// <summary>
/// 비교 블록과 비교 규칙(NumericRules.TryCompare) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙(ImplicitAll)이고, 실패 처리 표에 TypeMismatch 줄(처리 EndTask, 분류 CompileError)이 있어야 모두 통과한다.<br/>
/// 메모리를 7칸 쓰므로 메모리 크기가 7칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CompareTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);

	FunctionExecutor executor;
	// 테스트가 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		startTick = executor.CurrentTick;
		executor.TaskEnded += OnTaskEnded;

		TestCRules(GetComponent<LanguageRule>().Rule);
		TestOtherRules();
		CastFunctions();
	}

	// 테스트 1: C 규칙(연결한 언어 규칙 파일)의 비교
	void TestCRules(LanguageRuleData c)
	{
		Check("C 파일의 규칙이 ImplicitAll", c.NumericConversion == NumericConversion.ImplicitAll);

		CheckCompare(c, "3 < 3.5", RuntimeValue.FromInt(3), CompareOperator.Less, RuntimeValue.FromFloat(3.5f), true);
		CheckCompare(c, "3 == 3.0", RuntimeValue.FromInt(3), CompareOperator.Equal, RuntimeValue.FromFloat(3f), true);
		CheckCompare(c, "'a' == 97", RuntimeValue.FromChar('a'), CompareOperator.Equal, RuntimeValue.FromInt(97), true);
		CheckCompare(c, "true == 1", RuntimeValue.FromBool(true), CompareOperator.Equal, RuntimeValue.FromInt(1), true);
		CheckCompare(c, "true > false", RuntimeValue.FromBool(true), CompareOperator.Greater, RuntimeValue.FromBool(false), true);
		CheckCompare(c, "7 >= 7", RuntimeValue.FromInt(7), CompareOperator.GreaterOrEqual, RuntimeValue.FromInt(7), true);
		CheckCompare(c, "2 <= 1", RuntimeValue.FromInt(2), CompareOperator.LessOrEqual, RuntimeValue.FromInt(1), false);
		CheckCompare(c, "5 != 5", RuntimeValue.FromInt(5), CompareOperator.NotEqual, RuntimeValue.FromInt(5), false);
		// float으로 바꿔 비교하면 두 값이 같아져 false가 된다
		CheckCompare(c, "16777217 > 16777216", RuntimeValue.FromInt(16777217), CompareOperator.Greater, RuntimeValue.FromInt(16777216), true);

		CheckCompareFails(c, "\"a\" == \"a\"", RuntimeValue.FromString("a"), CompareOperator.Equal, RuntimeValue.FromString("a"));
		CheckCompareFails(c, "\"a\" == 1", RuntimeValue.FromString("a"), CompareOperator.Equal, RuntimeValue.FromInt(1));
	}

	// 테스트 2: C가 아닌 언어의 규칙(이후 다른 언어를 더할 때 쓸 규칙)
	void TestOtherRules()
	{
		LanguageRuleData widening = LanguageRuleData.CreateForTest(NumericConversion.WideningOnly, true);
		CheckCompare(widening, "[WideningOnly] 3 < 3.5", RuntimeValue.FromInt(3), CompareOperator.Less, RuntimeValue.FromFloat(3.5f), true);
		CheckCompare(widening, "[WideningOnly] 'a' == 97", RuntimeValue.FromChar('a'), CompareOperator.Equal, RuntimeValue.FromInt(97), true);
		CheckCompare(widening, "[WideningOnly] true == false", RuntimeValue.FromBool(true), CompareOperator.Equal, RuntimeValue.FromBool(false), false);
		CheckCompareFails(widening, "[WideningOnly] true > false", RuntimeValue.FromBool(true), CompareOperator.Greater, RuntimeValue.FromBool(false));
		CheckCompareFails(widening, "[WideningOnly] true == 1", RuntimeValue.FromBool(true), CompareOperator.Equal, RuntimeValue.FromInt(1));

		LanguageRuleData strict = LanguageRuleData.CreateForTest(NumericConversion.Strict, false);
		CheckCompare(strict, "[Strict] 'a' == 'b'", RuntimeValue.FromChar('a'), CompareOperator.Equal, RuntimeValue.FromChar('b'), false);
		CheckCompareFails(strict, "[Strict] 3 < 3.5", RuntimeValue.FromInt(3), CompareOperator.Less, RuntimeValue.FromFloat(3.5f));
		CheckCompareFails(strict, "[Strict] 'a' == 97", RuntimeValue.FromChar('a'), CompareOperator.Equal, RuntimeValue.FromInt(97));

		Destroy(widening);
		Destroy(strict);
	}

	// 테스트 3: 함수 데이터로 만든 함수를 실행. 블록 하나 = 1틱. 모두 틱 1부터 함께 진행한다
	void CastFunctions()
	{
		// bool b = 3 < 3.5;
		// 틱 1 비교, 틱 2 선언, 틱 3 읽기(테스트용) → b true
		FunctionData less = FunctionData.Create("Less");
		less.Blocks.Add(BlockData.Declare(Bool, "b", Compare(SlotData.FromLiteral(Int, "3"), CompareOperator.Less, SlotData.FromLiteral(Float, "3.5"))));
		CastWithRead(less, "b", "true");

		// bool b = (1 + 2) == 3;
		// 틱 1 더하기, 틱 2 비교, 틱 3 선언, 틱 4 읽기 → b true
		FunctionData nested = FunctionData.Create("Nested");
		SlotData onePlusTwo = SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromLiteral(Int, "1"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "2")));
		nested.Blocks.Add(BlockData.Declare(Bool, "b", Compare(onePlusTwo, CompareOperator.Equal, SlotData.FromLiteral(Int, "3"))));
		CastWithRead(nested, "b", "true");

		// string s = "a"; bool b = s == "a"; → 틱 2에 비교 블록이 TypeMismatch (C는 string을 ==로 비교할 수 없음)
		FunctionData stringEqual = FunctionData.Create("StringEqual");
		stringEqual.Blocks.Add(BlockData.Declare(String, "s", SlotData.FromLiteral(String, "a")));
		stringEqual.Blocks.Add(BlockData.Declare(Bool, "b", Compare(SlotData.FromVariable("s"), CompareOperator.Equal, SlotData.FromLiteral(String, "a"))));
		CastConverted(stringEqual);
	}

	static SlotData Compare(SlotData left, CompareOperator op, SlotData right) =>
		SlotData.FromBlock(BlockData.Compare(left, op, right));

	// 바꾼 함수 끝에 테스트용 읽기 블록을 붙여 시전한다. 함수가 끝났을 때의 변수 값을 확인하기 위함
	void CastWithRead(FunctionData data, string variableName, string expected)
	{
		if (!Convert(data, out UserFunction converted)) return;

		Block read = new TestReadBlock(new VariableValue(variableName), v => Check($"{data.Name}: 끝났을 때 {variableName} {expected}", v.ToString() == expected, $"실제 {v}"));
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(read).ToArray()));
	}

	void CastConverted(FunctionData data)
	{
		if (Convert(data, out UserFunction converted)) executor.Cast(converted);
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
			case "Less": CheckCompleted(task, tick, 3); break;
			case "Nested": CheckCompleted(task, tick, 4); break;
			case "StringEqual":
				FailureCategory? category = task.Failure.HasValue ? GetComponent<LanguageRule>().GetFailureCategory(task.Failure.Value) : null;
				Check($"{task.Name}: 틱 2에 TypeMismatch(CompileError)로 실패",
					task.Failure == FunctionFailure.TypeMismatch && category == FailureCategory.CompileError && tick == 2,
					$"실제 틱 {tick}, {task.Failure}, {category}");
				break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick, $"실제 틱 {tick}, {task.EndReason}");
	}

	static void CheckCompare(LanguageRuleData rule, string name, RuntimeValue left, CompareOperator op, RuntimeValue right, bool expected)
	{
		bool ok = NumericRules.TryCompare(op, left, right, rule, out bool result);
		Check($"{name} = {expected}", ok && result == expected, ok ? $"실제 {result}" : "비교하지 못함");
	}

	static void CheckCompareFails(LanguageRuleData rule, string name, RuntimeValue left, CompareOperator op, RuntimeValue right)
	{
		bool ok = NumericRules.TryCompare(op, left, right, rule, out bool result);
		Check($"{name} → TypeMismatch", !ok, $"실제 결과 {result}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[CompareTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
