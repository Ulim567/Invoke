using System.Linq;
using UnityEngine;

/// <summary>
/// 비교 블록과 비교 규칙(NumericRules.TryCompare), NULL, free(NULL) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙(ImplicitAll, 0을 NULL로 봄, string 비교 금지)이고, 실패 처리 표에 TypeMismatch 줄(처리 EndTask, 분류 CompileError)이 있어야 모두 통과한다.<br/>
/// 두 번에 나누어 시전한다. 처음 함수 3개가 모두 끝나면 포인터 함수를 시전한다. 한 번에 메모리를 8칸까지 쓰므로 메모리 크기가 8칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class CompareTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	FunctionExecutor executor;
	ProcessMemory memory;
	// 지금 묶음을 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;
	// 처음 묶음에서 끝난 함수 수. 3개가 모두 끝나면 포인터 함수를 시전한다
	int firstEndedCount;
	// 테스트를 시작할 때의 HeapUsed. 포인터 함수가 malloc한 객체를 free했는지 마지막에 비교한다
	int heapAtStart;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		startTick = executor.CurrentTick;
		heapAtStart = memory.HeapUsed;
		executor.TaskEnded += OnTaskEnded;

		LanguageRuleData c = GetComponent<LanguageRule>().Rule;
		TestCRules(c);
		TestPointerRules(c);
		TestOtherRules();
		CastFunctions();
	}

	// 테스트 1: C 규칙(연결한 언어 규칙 파일)의 비교
	void TestCRules(LanguageRuleData c)
	{
		Check("C 파일의 규칙이 ImplicitAll, 0을 NULL로 봄, string 비교 금지",
			c.NumericConversion == NumericConversion.ImplicitAll && c.ZeroIsNull && c.StringEquality == StringEquality.Disallowed);

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

	// 테스트 2: 포인터와 NULL 비교, NULL 대입 변환 (C 규칙)
	void TestPointerRules(LanguageRuleData c)
	{
		Check("NULL 직접 입력을 읽음", LiteralValue.TryParse(VariableType.PointerTo(null), SlotData.NullLiteral, out RuntimeValue parsed)
			&& RuntimeValue.IsNullType(parsed.Type) && parsed.AsPointer() == HeapStorage.NullPointer);
		Check("가리키는 타입이 있는 포인터는 직접 입력할 수 없음", !LiteralValue.TryParse(SwordPointer, SlotData.NullLiteral, out _));

		// 포인터 값 5는 테스트용이다. 비교는 heap을 찾지 않고 값만 본다
		RuntimeValue p = RuntimeValue.FromPointer(SwordPointer, 5);
		RuntimeValue samePlace = RuntimeValue.FromPointer(SwordPointer, 5);
		RuntimeValue nullSword = RuntimeValue.FromPointer(SwordPointer, HeapStorage.NullPointer);
		RuntimeValue shield = RuntimeValue.FromPointer(VariableType.PointerTo(VariableType.StructOf("Shield")), 5);

		CheckCompare(c, "p == NULL", p, CompareOperator.Equal, RuntimeValue.Null, false);
		CheckCompare(c, "malloc 실패한 포인터 == NULL", nullSword, CompareOperator.Equal, RuntimeValue.Null, true);
		CheckCompare(c, "NULL == NULL", RuntimeValue.Null, CompareOperator.Equal, RuntimeValue.Null, true);
		CheckCompare(c, "p != malloc 실패한 포인터", p, CompareOperator.NotEqual, nullSword, true);
		CheckCompare(c, "같은 주소의 포인터끼리 ==", p, CompareOperator.Equal, samePlace, true);
		CheckCompareFails(c, "p < NULL", p, CompareOperator.Less, RuntimeValue.Null);
		CheckCompareFails(c, "p == 1", p, CompareOperator.Equal, RuntimeValue.FromInt(1));
		CheckCompareFails(c, "Sword* == Shield*", p, CompareOperator.Equal, shield);

		// 직접 입력한 0은 포인터 자리에서만 NULL이다. 0을 변수에서 읽으면 NULL이 아니다
		LiteralValue zero = new LiteralValue(Int, "0");
		RuntimeValue zeroValue = RuntimeValue.FromInt(0);
		Check("p != 0 (직접 입력한 0) = True", NumericRules.TryCompare(CompareOperator.NotEqual, null, p, zero, zeroValue, c, out bool notNull) && notNull);
		Check("x == 0 (숫자끼리는 0 그대로) = True", NumericRules.TryCompare(CompareOperator.Equal, null, zeroValue, zero, zeroValue, c, out bool isZero) && isZero);
		Check("p != 0 (변수에서 읽은 0) → TypeMismatch", !NumericRules.TryCompare(CompareOperator.NotEqual, null, p, new VariableValue("z"), zeroValue, c, out _));
		Check("p == 1 (직접 입력한 1) → TypeMismatch", !NumericRules.TryCompare(CompareOperator.Equal, null, p, new LiteralValue(Int, "1"), RuntimeValue.FromInt(1), c, out _));

		bool storedZero = NumericRules.TryConvertForStore(zero, zeroValue, SwordPointer, c, out RuntimeValue zeroPointer);
		Check("Sword*에 직접 입력한 0을 넣으면 NullPointer", storedZero && zeroPointer.AsPointer() == HeapStorage.NullPointer, storedZero ? $"실제 {zeroPointer}" : "바꾸지 못함");
		Check("int에 직접 입력한 0을 넣으면 0", NumericRules.TryConvertForStore(zero, zeroValue, Int, c, out RuntimeValue zeroInt) && zeroInt.ToString() == "0");

		bool stored = NumericRules.TryConvertForStore(RuntimeValue.Null, SwordPointer, c, out RuntimeValue storedNull);
		Check("Sword*에 NULL을 넣으면 Sword* 타입의 NullPointer",
			stored && VariableStore.IsSameType(storedNull.Type, SwordPointer) && storedNull.AsPointer() == HeapStorage.NullPointer,
			stored ? $"실제 {storedNull}" : "바꾸지 못함");
		Check("int에 NULL을 넣을 수 없음", !NumericRules.TryConvertForStore(RuntimeValue.Null, Int, c, out _));
	}

	// 테스트 3: C가 아닌 언어의 규칙(이후 다른 언어를 더할 때 쓸 규칙)
	void TestOtherRules()
	{
		LanguageRuleData byContent = LanguageRuleData.CreateForTest(NumericConversion.ImplicitAll, false, true, StringEquality.ByContent);
		CheckCompare(byContent, "[ByContent] \"a\" == \"a\"", RuntimeValue.FromString("a"), CompareOperator.Equal, RuntimeValue.FromString("a"), true);
		CheckCompare(byContent, "[ByContent] \"a\" != \"b\"", RuntimeValue.FromString("a"), CompareOperator.NotEqual, RuntimeValue.FromString("b"), true);
		CheckCompareFails(byContent, "[ByContent] \"a\" < \"b\"", RuntimeValue.FromString("a"), CompareOperator.Less, RuntimeValue.FromString("b"));

		LanguageRuleData zeroIsNotNull = LanguageRuleData.CreateForTest(NumericConversion.ImplicitAll, false, false);
		Check("[0을 NULL로 보지 않음] p == 0 → TypeMismatch", !NumericRules.TryCompare(CompareOperator.Equal,
			null, RuntimeValue.FromPointer(SwordPointer, 5), new LiteralValue(Int, "0"), RuntimeValue.FromInt(0), zeroIsNotNull, out _));

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

		Destroy(byContent);
		Destroy(zeroIsNotNull);
		Destroy(widening);
		Destroy(strict);
	}

	// 테스트 4: 함수 데이터로 만든 함수를 실행. 블록 하나 = 1틱. 모두 틱 1부터 함께 진행한다
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

	// 처음 묶음이 끝난 뒤 시전한다. 처음 묶음과 함께 시전하면 메모리를 15칸 쓰기 때문
	// 틱 1 malloc, 틱 2 p 선언, 틱 3 q 선언, 틱 4~9 비교와 선언 세 번, 틱 10 free(NULL), 틱 11 free(0), 틱 12 free(p), 틱 13~15 읽기 → 틱 15에 정상 종료
	void CastPointerFunction()
	{
		FunctionData pointers = FunctionData.Create("Pointers");
		pointers.Blocks.Add(BlockData.Declare(SwordPointer, "p", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
		pointers.Blocks.Add(BlockData.Declare(SwordPointer, "q", SlotData.FromLiteral(Int, "0")));
		pointers.Blocks.Add(BlockData.Declare(Bool, "a", Compare(SlotData.FromVariable("p"), CompareOperator.Equal, SlotData.FromNull())));
		pointers.Blocks.Add(BlockData.Declare(Bool, "b", Compare(SlotData.FromVariable("q"), CompareOperator.Equal, SlotData.FromNull())));
		pointers.Blocks.Add(BlockData.Declare(Bool, "c", Compare(SlotData.FromVariable("p"), CompareOperator.NotEqual, SlotData.FromLiteral(Int, "0"))));
		pointers.Blocks.Add(BlockData.Free(SlotData.FromNull()));
		pointers.Blocks.Add(BlockData.Free(SlotData.FromLiteral(Int, "0")));
		pointers.Blocks.Add(BlockData.Free(SlotData.FromVariable("p")));
		if (!Convert(pointers, out UserFunction converted)) return;

		Block[] reads =
		{
			new TestReadBlock(new VariableValue("a"), v => Check("Pointers: p == NULL은 false", v.ToString() == "false", $"실제 {v}")),
			new TestReadBlock(new VariableValue("b"), v => Check("Pointers: Sword* q = 0 뒤 q == NULL은 true", v.ToString() == "true", $"실제 {v}")),
			new TestReadBlock(new VariableValue("c"), v => Check("Pointers: p != 0은 true", v.ToString() == "true", $"실제 {v}")),
		};
		startTick = executor.CurrentTick;
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Concat(reads).ToArray()));
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
		bool ok = FunctionDataConverter.TryConvert(data, System.Array.Empty<FunctionData>(), out converted);
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
			case "Pointers":
				CheckCompleted(task, tick, 15);
				Check("Pointers: 끝난 뒤 HeapUsed가 시작할 때와 같음 (free(p)만 해제)", memory.HeapUsed == heapAtStart, $"시작 {heapAtStart}, 지금 {memory.HeapUsed}");
				return;
		}

		if (++firstEndedCount == 3) CastPointerFunction();
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
