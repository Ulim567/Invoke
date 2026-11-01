using UnityEngine;

/// <summary>
/// 블록 범위(VariableStore의 범위, Statements의 { }) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙(바깥과 같은 이름 허용)이고, 실패 처리 표에 UndeclaredVariable, DuplicateVariable 줄이 있어야 모두 통과한다.<br/>
/// 메모리를 8칸 쓰므로 메모리 크기가 8칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ScopeTest : MonoBehaviour
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

		Check("C 파일의 규칙이 바깥과 같은 이름 허용", GetComponent<LanguageRule>().Rule.AllowShadowing);
		TestStore();
		CastFunctions();
	}

	// 테스트 1: VariableStore의 범위 열기·닫기와 선언 가능 판단
	void TestStore()
	{
		VariableStore store = new VariableStore();
		store.TryDeclare("x", Int, RuntimeValue.FromInt(1));
		Check("같은 범위에는 같은 이름을 선언할 수 없음", !store.CanDeclare("x", true));

		store.OpenScope();
		Check("{ } 안: 바깥과 같은 이름 허용(C)이면 선언 가능", store.CanDeclare("x", true));
		Check("{ } 안: 바깥과 같은 이름 거부(Java, C#)이면 선언 불가", !store.CanDeclare("x", false));

		store.TryDeclare("x", Int, RuntimeValue.FromInt(2));
		store.TryDeclare("y", Int, RuntimeValue.FromInt(3));
		CheckValue(store, "{ } 안의 x는 안쪽 변수", "x", "2");
		Check("{ } 안: 방금 선언한 이름은 다시 선언할 수 없음", !store.CanDeclare("x", true));
		store.TrySet("x", RuntimeValue.FromInt(5));

		store.CloseScope();
		CheckValue(store, "} 뒤의 x는 바깥 변수(안쪽 대입의 영향 없음)", "x", "1");
		Check("} 뒤에는 안쪽에서 선언한 y가 없음", !store.Contains("y"));

		bool threw = false;
		try { store.CloseScope(); }
		catch (System.InvalidOperationException) { threw = true; }
		Check("function body의 범위는 닫을 수 없음(예외)", threw);
	}

	// 테스트 2: 함수를 실행. 블록 하나 = 1틱. { }는 따로 틱을 쓰지 않는다. 모두 틱 1부터 함께 진행한다
	void CastFunctions()
	{
		// int x = 1; { int x = 2; 읽기 x; int y = 3; } { int y = 4; 읽기 y; } 읽기 x; 읽기 y;
		// 틱 1~4 첫 { }까지, 틱 5~6 둘째 { }, 틱 7 x 읽기, 틱 8 y 읽기 → UndeclaredVariable
		// 둘째 { }의 int y: 첫 { }가 닫히며 y가 사라졌으므로 다시 선언할 수 있다. 반복문의 바퀴마다 새 변수를 만드는 것과 같다
		executor.Cast(new UserFunction("Scope", 5,
			new DeclareBlock("x", Int, new LiteralValue(Int, "1")),
			new TestScopeBlock(
				new DeclareBlock("x", Int, new LiteralValue(Int, "2")),
				Read("x", "2", "{ } 안의 x"),
				new DeclareBlock("y", Int, new LiteralValue(Int, "3"))),
			new TestScopeBlock(
				new DeclareBlock("y", Int, new LiteralValue(Int, "4")),
				Read("y", "4", "둘째 { } 안에서 다시 선언한 y")),
			Read("x", "1", "} 뒤의 x"),
			// 이 읽기는 y가 없어 실패해야 한다. 값을 읽으면 테스트 실패
			Read("y", "(없음)", "} 뒤의 y")));

		// { int a = 1; int a = 2; } → 틱 2에 DuplicateVariable (같은 범위)
		executor.Cast(new UserFunction("SameScopeDuplicate", 3,
			new TestScopeBlock(
				new DeclareBlock("a", Int, new LiteralValue(Int, "1")),
				new DeclareBlock("a", Int, new LiteralValue(Int, "2")))));
	}

	static Block Read(string variableName, string expected, string what) =>
		new TestReadBlock(new VariableValue(variableName), v => Check($"Scope: {what} {expected}", v.ToString() == expected, $"실제 {v}"));

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "Scope": CheckFailed(task, tick, 8, FunctionFailure.UndeclaredVariable); break;
			case "SameScopeDuplicate": CheckFailed(task, tick, 2, FunctionFailure.DuplicateVariable); break;
		}
	}

	static void CheckFailed(FunctionTask task, int tick, int expectedTick, FunctionFailure expected)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 {expected}로 실패", task.Failure == expected && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	static void CheckValue(VariableStore store, string name, string variableName, string expected)
	{
		bool found = store.TryGet(variableName, out RuntimeValue value);
		Check($"{name} = {expected}", found && value.ToString() == expected, found ? $"실제 {value}" : "변수 없음");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ScopeTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
