using UnityEngine;

/// <summary>
/// 값 블록(LiteralValue, VariableValue) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 언어 규칙 파일의 실패 처리 표에 UndeclaredVariable, InvalidLiteral 줄(처리 EndTask, 분류 CompileError)이 있어야 모두 통과한다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트가 메모리를 먼저 쓰면 결과가 달라지기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ValueBlockTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType Char = new VariableType(VariableTypeKind.Char);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);

	FunctionExecutor executor;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestParse();
		CastFunctions();
	}

	// 테스트 1: 직접 입력한 글자열을 값으로 바꾸기
	void TestParse()
	{
		CheckParse(Int, "3", "3");
		CheckParse(Int, "-7", "-7");
		CheckParse(Float, "0.5", "0.5");
		CheckParse(Bool, "true", "true");
		CheckParse(Char, "a", "'a'");
		CheckParse(String, "abc", "\"abc\"");

		CheckParseRejected(Int, "abc");
		CheckParseRejected(Int, "3.5");
		CheckParseRejected(Float, "NaN");
		CheckParseRejected(Bool, "True");
		CheckParseRejected(Char, "ab");
	}

	// 테스트 2: 실행기로 값 블록 실행. 모두 틱 1부터 함께 진행하고, 끝나는 틱은 OnTaskEnded가 확인한다
	void CastFunctions()
	{
		// 이름이 같은 변수 x를 가진 두 함수를 동시에 실행해도 각자 자기 x를 읽는다. 틱 1 선언, 틱 2 읽기
		executor.Cast(new UserFunction("Fire",
			new TestDeclareBlock("x", RuntimeValue.FromInt(10)),
			new TestReadBlock(new VariableValue("x"), v => CheckValue("Fire의 x", v, "10"))));
		executor.Cast(new UserFunction("Guard",
			new TestDeclareBlock("x", RuntimeValue.FromInt(100)),
			new TestReadBlock(new VariableValue("x"), v => CheckValue("Guard의 x", v, "100"))));

		// 직접 입력한 값은 틱을 따로 쓰지 않으므로, 읽기 블록 하나만 든 함수는 틱 1에 끝난다
		executor.Cast(new UserFunction("Literal",
			new TestReadBlock(new LiteralValue(Float, "0.5"), v => CheckValue("직접 입력한 0.5", v, "0.5"))));

		// 안쪽 함수가 끝나면 바깥 함수는 다시 자기 x를 읽는다
		// 틱 1 바깥 선언, 틱 2 안쪽 선언, 틱 3 안쪽 읽기, 틱 4 바깥 읽기
		executor.Cast(new UserFunction("Outer",
			new TestDeclareBlock("x", RuntimeValue.FromInt(1)),
			new UserFunction("Inner",
				new TestDeclareBlock("x", RuntimeValue.FromInt(2)),
				new TestReadBlock(new VariableValue("x"), v => CheckValue("Inner의 x", v, "2"))),
			new TestReadBlock(new VariableValue("x"), v => CheckValue("Inner가 끝난 뒤 Outer의 x", v, "1"))));

		// 실패: 틱 1에 실패로 끝난다
		executor.Cast(new UserFunction("Undeclared",
			new TestReadBlock(new VariableValue("y"), v => Check("없는 변수 y를 읽음", false))));
		executor.Cast(new UserFunction("BadLiteral",
			new TestReadBlock(new LiteralValue(Int, "abc"), v => Check("int 칸의 \"abc\"를 읽음", false))));
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick;
		switch (task.Name)
		{
			case "Fire":
			case "Guard":
				CheckCompleted(task, tick, 2);
				break;
			case "Literal":
				CheckCompleted(task, tick, 1);
				break;
			case "Outer":
				CheckCompleted(task, tick, 4);
				break;
			case "Undeclared":
				CheckFailed(task, tick, FunctionFailure.UndeclaredVariable);
				break;
			case "BadLiteral":
				CheckFailed(task, tick, FunctionFailure.InvalidLiteral);
				break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name} 틱 {expectedTick}에 정상 종료",
			task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}");
	}

	void CheckFailed(FunctionTask task, int tick, FunctionFailure expected)
	{
		FailureCategory category = task.Failure.HasValue ? executor.GetFailureCategory(task.Failure.Value) : FailureCategory.NotCodeError;
		Check($"{task.Name} 틱 1에 {expected}(CompileError)로 실패",
			task.Failure == expected && category == FailureCategory.CompileError && tick == 1,
			$"실제 틱 {tick}, {task.Failure}, {category}");
	}

	static void CheckParse(VariableType type, string literal, string expected)
	{
		bool parsed = LiteralValue.TryParse(type, literal, out RuntimeValue value);
		Check($"{type.Kind} \"{literal}\" → {expected}", parsed && value.ToString() == expected, parsed ? $"실제 {value}" : "바꾸지 못함");
	}

	static void CheckParseRejected(VariableType type, string literal)
	{
		Check($"{type.Kind} \"{literal}\" 거부", !LiteralValue.TryParse(type, literal, out _));
	}

	static void CheckValue(string name, RuntimeValue value, string expected)
	{
		Check($"{name} {expected}", value.ToString() == expected, $"실제 {value}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ValueBlockTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
