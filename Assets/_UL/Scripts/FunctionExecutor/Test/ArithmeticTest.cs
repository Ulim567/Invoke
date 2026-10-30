using System.Linq;
using UnityEngine;

/// <summary>
/// 연산 블록과 숫자 타입 변환 규칙(NumericRules) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙(ImplicitAll, float % 금지)이고, 실패 처리 표에 DivideByZero 줄(처리 EndTask, 분류 RuntimeError)이 있어야 모두 통과한다.<br/>
/// 메모리를 10칸 쓰므로 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ArithmeticTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Char = new VariableType(VariableTypeKind.Char);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);

	FunctionExecutor executor;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestCRules(executor.Rule);
		TestOtherRules();
		CastFunctions();
	}

	// 테스트 1: C 규칙(연결한 언어 규칙 파일)의 계산과 변환
	void TestCRules(LanguageRule c)
	{
		Check("C 파일의 규칙이 ImplicitAll, float % 금지", c.NumericConversion == NumericConversion.ImplicitAll && !c.AllowFloatModulo);

		CheckCalc(c, "3 + 0.5", RuntimeValue.FromInt(3), ArithmeticOperator.Add, RuntimeValue.FromFloat(0.5f), "3.5");
		CheckCalc(c, "7 / 2", RuntimeValue.FromInt(7), ArithmeticOperator.Divide, RuntimeValue.FromInt(2), "3");
		CheckCalc(c, "7.0 / 2", RuntimeValue.FromFloat(7f), ArithmeticOperator.Divide, RuntimeValue.FromInt(2), "3.5");
		CheckCalc(c, "-7 % 2", RuntimeValue.FromInt(-7), ArithmeticOperator.Modulo, RuntimeValue.FromInt(2), "-1");
		CheckCalc(c, "'a' + 1", RuntimeValue.FromChar('a'), ArithmeticOperator.Add, RuntimeValue.FromInt(1), "98");
		CheckCalc(c, "true + 1", RuntimeValue.FromBool(true), ArithmeticOperator.Add, RuntimeValue.FromInt(1), "2");
		CheckCalc(c, "int 최댓값 + 1", RuntimeValue.FromInt(int.MaxValue), ArithmeticOperator.Add, RuntimeValue.FromInt(1), int.MinValue.ToString());
		CheckCalc(c, "int 최솟값 / -1", RuntimeValue.FromInt(int.MinValue), ArithmeticOperator.Divide, RuntimeValue.FromInt(-1), int.MinValue.ToString());

		CheckCalcFails(c, "1 / 0", RuntimeValue.FromInt(1), ArithmeticOperator.Divide, RuntimeValue.FromInt(0), FunctionFailure.DivideByZero);
		CheckCalcFails(c, "5 % 0", RuntimeValue.FromInt(5), ArithmeticOperator.Modulo, RuntimeValue.FromInt(0), FunctionFailure.DivideByZero);
		CheckCalcFails(c, "1.0 / 0", RuntimeValue.FromFloat(1f), ArithmeticOperator.Divide, RuntimeValue.FromInt(0), FunctionFailure.DivideByZero);
		CheckCalcFails(c, "5.5 % 2", RuntimeValue.FromFloat(5.5f), ArithmeticOperator.Modulo, RuntimeValue.FromInt(2), FunctionFailure.TypeMismatch);
		CheckCalcFails(c, "\"a\" + 1", RuntimeValue.FromString("a"), ArithmeticOperator.Add, RuntimeValue.FromInt(1), FunctionFailure.TypeMismatch);

		CheckStore(c, "int에 0.9", RuntimeValue.FromFloat(0.9f), Int, "0");
		CheckStore(c, "int에 -1.7", RuntimeValue.FromFloat(-1.7f), Int, "-1");
		CheckStore(c, "float에 3", RuntimeValue.FromInt(3), Float, "3");
		CheckStore(c, "char에 98", RuntimeValue.FromInt(98), Char, "'b'");
		CheckStore(c, "bool에 2", RuntimeValue.FromInt(2), Bool, "true");
		CheckStoreFails(c, "int에 \"abc\"", RuntimeValue.FromString("abc"), Int);
	}

	// 테스트 2: C가 아닌 언어의 규칙(이후 다른 언어를 더할 때 쓸 규칙)
	void TestOtherRules()
	{
		LanguageRule widening = LanguageRule.CreateForTest(NumericConversion.WideningOnly, true);
		CheckCalc(widening, "[WideningOnly] 3 + 0.5", RuntimeValue.FromInt(3), ArithmeticOperator.Add, RuntimeValue.FromFloat(0.5f), "3.5");
		CheckCalc(widening, "[WideningOnly] 'a' + 1", RuntimeValue.FromChar('a'), ArithmeticOperator.Add, RuntimeValue.FromInt(1), "98");
		CheckCalc(widening, "[WideningOnly] 5.5 % 2", RuntimeValue.FromFloat(5.5f), ArithmeticOperator.Modulo, RuntimeValue.FromInt(2), "1.5");
		CheckCalcFails(widening, "[WideningOnly] true + 1", RuntimeValue.FromBool(true), ArithmeticOperator.Add, RuntimeValue.FromInt(1), FunctionFailure.TypeMismatch);
		CheckStore(widening, "[WideningOnly] float에 3", RuntimeValue.FromInt(3), Float, "3");
		CheckStoreFails(widening, "[WideningOnly] int에 0.5", RuntimeValue.FromFloat(0.5f), Int);

		LanguageRule strict = LanguageRule.CreateForTest(NumericConversion.Strict, false);
		CheckCalcFails(strict, "[Strict] 3 + 0.5", RuntimeValue.FromInt(3), ArithmeticOperator.Add, RuntimeValue.FromFloat(0.5f), FunctionFailure.TypeMismatch);
		CheckCalcFails(strict, "[Strict] 'a' + 1", RuntimeValue.FromChar('a'), ArithmeticOperator.Add, RuntimeValue.FromInt(1), FunctionFailure.TypeMismatch);
		CheckStoreFails(strict, "[Strict] float에 3", RuntimeValue.FromInt(3), Float);

		Destroy(widening);
		Destroy(strict);
	}

	// 테스트 3: 함수 데이터로 만든 함수를 실행. 블록 하나 = 1틱. 모두 틱 1부터 함께 진행한다
	void CastFunctions()
	{
		// 이슈 완료 조건 1. int x = 3; x = x + 2;
		// 틱 1 선언, 틱 2 더하기, 틱 3 대입, 틱 4 읽기(테스트용) → x 5
		FunctionData addTwo = FunctionData.Create("AddTwo");
		addTwo.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));
		addTwo.Blocks.Add(BlockData.Assign("x", Calc(SlotData.FromVariable("x"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "2"))));
		CastWithRead(addTwo, "x", "5");

		// int x = (1 + 2) * 3;
		// 틱 1 더하기, 틱 2 곱하기, 틱 3 선언, 틱 4 읽기 → x 9
		FunctionData nested = FunctionData.Create("Nested");
		SlotData onePlusTwo = Calc(SlotData.FromLiteral(Int, "1"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "2"));
		nested.Blocks.Add(BlockData.Declare(Int, "x", Calc(onePlusTwo, ArithmeticOperator.Multiply, SlotData.FromLiteral(Int, "3"))));
		CastWithRead(nested, "x", "9");

		// int z = 0; int q = 1 / z; → 틱 2에 나누기 블록이 DivideByZero
		FunctionData divide = FunctionData.Create("DivideByZero");
		divide.Blocks.Add(BlockData.Declare(Int, "z", SlotData.FromLiteral(Int, "0")));
		divide.Blocks.Add(BlockData.Declare(Int, "q", Calc(SlotData.FromLiteral(Int, "1"), ArithmeticOperator.Divide, SlotData.FromVariable("z"))));
		CastConverted(divide);

		// float f = 5.5; float g = f % 2; → 틱 2에 나머지 블록이 TypeMismatch (C는 float에 %를 쓸 수 없음)
		FunctionData floatModulo = FunctionData.Create("FloatModulo");
		floatModulo.Blocks.Add(BlockData.Declare(Float, "f", SlotData.FromLiteral(Float, "5.5")));
		floatModulo.Blocks.Add(BlockData.Declare(Float, "g", Calc(SlotData.FromVariable("f"), ArithmeticOperator.Modulo, SlotData.FromLiteral(Int, "2"))));
		CastConverted(floatModulo);
	}

	static SlotData Calc(SlotData left, ArithmeticOperator op, SlotData right) =>
		SlotData.FromBlock(BlockData.Arithmetic(left, op, right));

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
		int tick = executor.CurrentTick;
		switch (task.Name)
		{
			case "AddTwo":
			case "Nested":
				Check($"{task.Name}: 틱 4에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == 4, $"실제 틱 {tick}, {task.EndReason}");
				break;
			case "DivideByZero": CheckFailed(task, tick, FunctionFailure.DivideByZero, FailureCategory.RuntimeError); break;
			case "FloatModulo": CheckFailed(task, tick, FunctionFailure.TypeMismatch, FailureCategory.CompileError); break;
		}
	}

	void CheckFailed(FunctionTask task, int tick, FunctionFailure expected, FailureCategory expectedCategory)
	{
		FailureCategory category = task.Failure.HasValue ? executor.GetFailureCategory(task.Failure.Value) : FailureCategory.NotCodeError;
		Check($"{task.Name}: 틱 2에 {expected}({expectedCategory})로 실패",
			task.Failure == expected && category == expectedCategory && tick == 2,
			$"실제 틱 {tick}, {task.Failure}, {category}");
	}

	static void CheckCalc(LanguageRule rule, string name, RuntimeValue left, ArithmeticOperator op, RuntimeValue right, string expected)
	{
		bool ok = NumericRules.TryCalculate(op, left, right, rule, out RuntimeValue result, out FunctionFailure failure);
		Check($"{name} = {expected}", ok && result.ToString() == expected, ok ? $"실제 {result}" : $"실패 {failure}");
	}

	static void CheckCalcFails(LanguageRule rule, string name, RuntimeValue left, ArithmeticOperator op, RuntimeValue right, FunctionFailure expected)
	{
		bool ok = NumericRules.TryCalculate(op, left, right, rule, out RuntimeValue result, out FunctionFailure failure);
		Check($"{name} → {expected}", !ok && failure == expected, ok ? $"실제 결과 {result}" : $"실제 {failure}");
	}

	static void CheckStore(LanguageRule rule, string name, RuntimeValue value, VariableType target, string expected)
	{
		bool ok = NumericRules.TryConvertForStore(value, target, rule, out RuntimeValue converted);
		Check($"{name} → {expected}", ok && converted.ToString() == expected, ok ? $"실제 {converted}" : "바꾸지 못함");
	}

	static void CheckStoreFails(LanguageRule rule, string name, RuntimeValue value, VariableType target)
	{
		Check($"{name} → TypeMismatch", !NumericRules.TryConvertForStore(value, target, rule, out _));
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ArithmeticTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
