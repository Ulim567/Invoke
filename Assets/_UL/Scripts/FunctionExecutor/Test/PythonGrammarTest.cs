using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Python 문법 시험 (#532). 변수 규칙(대입이 곧 변수, global, del, 범위 없음, 프레임 크기), 연산(/, //, %, 큰 정수, 문자열 +, None),
/// 조건(truthiness), 타입 없는 def, 리스트(append, pop, len, index, MemoryError, 전역 리스트)를 함수 데이터로 만들어 Python 규칙으로 실행한다.
/// <para>
/// - 결과는 전역 변수(Python 전역 변수 파일의 r1 … r30)에 담아 작업이 끝난 뒤 읽는다.<br/>
/// - 사용법: Play 중 Spawn(baseStatsPath)을 부르면 시험 오브젝트를 만들고 시험이 돈다. 콘솔에 「[PythonGrammarTest] … 통과/실패」, 마지막에 「전체 통과」.<br/>
/// - 언어 규칙은 코드로 만든 Python 규칙(LanguageRuleData.CreatePythonForTest)이다. LanguageRule_Python.asset을 Inspector에서 고치기 전에도 돈다.
/// </para>
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class PythonGrammarTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Str = new VariableType(VariableTypeKind.String);
	static readonly VariableType BoolType = new VariableType(VariableTypeKind.Bool);

	FunctionExecutor executor;
	ProcessMemory memory;
	LanguageRuleData rule;
	int failures;
	readonly Dictionary<string, Action<FunctionTask>> expectations = new Dictionary<string, Action<FunctionTask>>();

#if UNITY_EDITOR
	/// <summary>
	/// 시험 오브젝트를 만든다. baseStatsPath는 PlayerStats의 기본 능력치 에셋 경로(예: Assets/_DH/Data/C_BaseStats.asset).
	/// </summary>
	public static GameObject Spawn(string baseStatsPath)
	{
		GameObject go = new GameObject("PythonGrammarTest");
		go.SetActive(false);
		PlayerStats stats = go.AddComponent<PlayerStats>();
		FieldInfo baseStats = typeof(PlayerStats).GetField("baseStats", BindingFlags.NonPublic | BindingFlags.Instance);
		baseStats?.SetValue(stats, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(baseStatsPath));
		// 기본 능력치의 메모리(10칸)로는 전역 변수·리스트·구슬 시험을 모두 담을 수 없어 늘린다
		stats.AddBonus(StatType.MemorySize, 190);
		LanguageRule language = go.AddComponent<LanguageRule>();
		language.SetRuleDataForTest(LanguageRuleData.CreatePythonForTest());
		// 시험 오브젝트에는 PlayerCharacter가 없어 캐릭터 언어로 정할 수 없으므로 언어 방식을 직접 정한다
		language.SetFamilyForTest(LanguageFamily.Python);
		go.AddComponent<FunctionExecutor>();
		go.AddComponent<FunctionExecutorLogger>();
		go.AddComponent<PythonGrammarTest>();
		go.SetActive(true);
		return go;
	}
#endif

	// 실행기의 Start(전역 변수 만들기, 가비지 컬렉션 켜기)가 끝난 뒤에 시작하려고 한 프레임 기다린다
	System.Collections.IEnumerator Start()
	{
		yield return null;
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		rule = GetComponent<LanguageRule>().Rule;
		executor.TaskEnded += OnTaskEnded;

		Check("언어 방식 Python", GetComponent<LanguageRule>().Family == LanguageFamily.Python);
		Check("가비지 컬렉션 켜짐", memory.Heap.CollectsGarbage);

		// 전역 변수 파일: r1 … r30 = 0, g = 7, items = [], full = [] (33개)
		GlobalVariablesData globals = new GlobalVariablesData();
		for (int i = 1; i <= 30; i++) globals.Declarations.Add(BlockData.Assign("r" + i, L(0)));
		globals.Declarations.Add(BlockData.Assign("g", L(7)));
		globals.Declarations.Add(BlockData.ListNew("items"));
		globals.Declarations.Add(BlockData.ListNew("full"));
		Check("전역 변수 파일 검사 통과", executor.CanUseGlobalVariables(globals, out string globalsError), globalsError);
		executor.SetGlobalVariablesFile(globals);
		executor.RestartProcess();
		Check("전역 변수 33개 = 데이터 영역 33칸 (리스트의 원소는 heap)", memory.DataUsed == 33, $"실제 {memory.DataUsed}");

		CastVariables();
		CastArithmetic();
		CastFunctions();
		CastLists();
		StartNext();
	}

	// 1단계: 변수 규칙
	void CastVariables()
	{
		// x = 3; x = "a"; global r1; r1 = x  → r1 == "a" (다른 타입 재대입)
		Run("Retype", Done(() => ExpectGlobal("r1", "\"a\"", "다른 타입 재대입")),
			BlockData.Assign("x", L(3)), BlockData.Assign("x", S("a")), BlockData.Global("r1"), BlockData.Assign("r1", V("x")));

		// if True: y = 5; global r2; r2 = y → 5 (if 안 변수도 함수 끝까지)
		BlockData ifTrue = BlockData.Conditional(ConditionalBranch.If, SlotData.FromLiteral(BoolType, "true"));
		ifTrue.InnerBlocks.Add(BlockData.Assign("y", L(5)));
		Run("NoBlockScope", Done(() => ExpectGlobal("r2", "5", "if 안에서 만든 변수")),
			ifTrue, BlockData.Global("r2"), BlockData.Assign("r2", V("y")));

		// r3 = r3 + 1 (global 없음) → NameError. 대입하는 이름은 지역이고 아직 값이 없기 때문
		Run("UnboundLocal", Fails(FunctionFailure.UndeclaredVariable), BlockData.Assign("r3", Arith(V("r3"), ArithmeticOperator.Add, L(1))));

		// z = g; global r4; r4 = z → 7 (읽기만 한 이름은 전역)
		Run("ReadGlobal", Done(() => ExpectGlobal("r4", "7", "읽기만 한 전역 g")),
			BlockData.Assign("z", V("g")), BlockData.Global("r4"), BlockData.Assign("r4", V("z")));

		// w = 1; del w; global r5; r5 = w → NameError
		Run("Del", Fails(FunctionFailure.UndeclaredVariable),
			BlockData.Assign("w", L(1)), BlockData.Del("w"), BlockData.Global("r5"), BlockData.Assign("r5", V("w")));

		// global nothere; nothere = 1 → NameError (전역 변수 파일에 없는 이름, 이우림 결정 (가))
		Run("GlobalNotInFile", Fails(FunctionFailure.UndeclaredVariable), BlockData.Global("nothere"), BlockData.Assign("nothere", L(1)));

		// 프레임 크기: a = 1; b = 2; global r6; r6 = 3 → 1 + 지역 이름 2(a, b) = 3
		FunctionData frame = Data("Frame", BlockData.Assign("a", L(1)), BlockData.Assign("b", L(2)), BlockData.Global("r6"), BlockData.Assign("r6", L(3)));
		Check("프레임 크기 = 1 + 대입하는 지역 이름 수(global 제외)", FunctionDataConverter.TryConvert(frame, Array.Empty<FunctionData>(), LanguageFamily.Python, out UserFunction converted)
			&& converted.FrameSize == 3, $"실제 {converted?.FrameSize}");

		// for i in range(0, 4): r7 = r7 + i (global r7) → 6, for 뒤에도 i 남음 → r8 = 3
		BlockData loop = BlockData.For("i", L(0), L(4));
		loop.InnerBlocks.Add(BlockData.Assign("r7", Arith(V("r7"), ArithmeticOperator.Add, V("i"))));
		Run("ForRange", Done(() => { ExpectGlobal("r7", "6", "range(0, 4)의 합"); ExpectGlobal("r8", "3", "for 뒤의 i"); }),
			BlockData.Global("r7"), BlockData.Global("r8"), loop, BlockData.Assign("r8", V("i")));
	}

	// 2단계: 연산, None, 조건
	void CastArithmetic()
	{
		Run("Arithmetic", Done(() =>
			{
				ExpectGlobal("r9", "3.5", "7 / 2");
				ExpectGlobal("r10", "-4", "-7 // 2");
				ExpectGlobal("r11", "1", "-7 % 2");
				ExpectGlobal("r12", "2147483648", "int 최댓값 + 1 (넘침 없음)");
				ExpectGlobal("r13", "\"ab\"", "\"a\" + \"b\"");
				ExpectGlobal("r14", "2", "True + 1");
				ExpectGlobal("r15", "True", "None == None");
				ExpectGlobal("r16", "False", "3 == \"3\"");
				ExpectGlobal("r17", "1", "if \"a\" (참), if 0 (거짓)");
			}),
			BlockData.Global("r9"), BlockData.Global("r10"), BlockData.Global("r11"), BlockData.Global("r12"), BlockData.Global("r13"),
			BlockData.Global("r14"), BlockData.Global("r15"), BlockData.Global("r16"), BlockData.Global("r17"),
			BlockData.Assign("r9", Arith(L(7), ArithmeticOperator.Divide, L(2))),
			BlockData.Assign("r10", Arith(L(-7), ArithmeticOperator.FloorDivide, L(2))),
			BlockData.Assign("r11", Arith(L(-7), ArithmeticOperator.Modulo, L(2))),
			BlockData.Assign("r12", Arith(L(int.MaxValue), ArithmeticOperator.Add, L(1))),
			BlockData.Assign("r13", Arith(S("a"), ArithmeticOperator.Add, S("b"))),
			BlockData.Assign("r14", Arith(SlotData.FromLiteral(BoolType, "true"), ArithmeticOperator.Add, L(1))),
			BlockData.Assign("r15", Cmp(SlotData.FromNone(), CompareOperator.Equal, SlotData.FromNone())),
			BlockData.Assign("r16", Cmp(L(3), CompareOperator.Equal, S("3"))),
			If(S("a"), BlockData.Assign("r17", L(1))),
			If(L(0), BlockData.Assign("r17", L(9))));

		Run("ZeroDivision", Fails(FunctionFailure.DivideByZero), BlockData.Assign("x", Arith(L(1), ArithmeticOperator.FloorDivide, L(0))));
		Run("OrderNone", Fails(FunctionFailure.TypeMismatch), BlockData.Assign("x", Cmp(L(5), CompareOperator.Less, S("a"))));
	}

	// 2단계: 타입 없는 def. f(a): return a * 2, h(): (return 없음) → None
	void CastFunctions()
	{
		FunctionData f = FunctionData.Create("f");
		f.Parameters.Add(new ParameterData("a", null));
		f.Blocks.Add(BlockData.Return(Arith(V("a"), ArithmeticOperator.Multiply, L(2))));
		FunctionData h = FunctionData.Create("h");
		h.Blocks.Add(BlockData.Assign("q", L(1)));

		FunctionData caller = Data("Def",
			BlockData.Global("r18"), BlockData.Global("r19"),
			BlockData.Assign("r18", SlotData.FromBlock(BlockData.CallUserFunction(f.Guid, L(4)))),
			BlockData.Assign("r19", SlotData.FromBlock(BlockData.CallUserFunction(h.Guid))));
		Cast(caller, new[] { f, h }, Done(() => { ExpectGlobal("r18", "8", "f(4) = 4 * 2"); ExpectGlobal("r19", "None", "return 없는 h()"); }));
	}

	// 3단계: 리스트
	void CastLists()
	{
		// lst = []; lst.append(1); lst.append("x"); r20 = len(lst); r21 = lst[-1]; lst[0] = 5; r22 = lst.pop(0)
		Run("List", Done(() =>
			{
				ExpectGlobal("r20", "2", "len(lst)");
				ExpectGlobal("r21", "\"x\"", "lst[-1]");
				ExpectGlobal("r22", "5", "lst.pop(0) (lst[0] = 5 뒤)");
				Check("함수가 끝나면 지역 리스트가 해제됨 (heap 0칸)", memory.HeapUsed == 0, $"실제 {memory.HeapUsed}");
			}),
			BlockData.Global("r20"), BlockData.Global("r21"), BlockData.Global("r22"),
			BlockData.ListNew("lst"),
			BlockData.ListAppend("lst", L(1)),
			BlockData.ListAppend("lst", S("x")),
			BlockData.Assign("r20", SlotData.FromBlock(BlockData.ListLen(V("lst")))),
			BlockData.Assign("r21", SlotData.FromBlock(BlockData.ArrayGet("lst", L(-1)))),
			BlockData.ArraySet("lst", L(0), L(5)),
			BlockData.Assign("r22", SlotData.FromBlock(BlockData.ListPop("lst", L(0)))));

		Run("PopEmpty", Fails(FunctionFailure.IndexOutOfRange),
			BlockData.ListNew("e"), BlockData.Assign("v", SlotData.FromBlock(BlockData.ListPop("e"))));
		Run("NotAList", Fails(FunctionFailure.TypeMismatch), BlockData.Assign("n", L(3)), BlockData.ListAppend("n", L(1)));

		// 전역 리스트 items에 append 두 번 → 끝나도 남음(heap 2칸)
		Run("GlobalList", Done(() => Check("전역 리스트 items는 함수가 끝나도 남음 (heap 2칸)", memory.HeapUsed == 2, $"실제 {memory.HeapUsed}")),
			BlockData.ListAppend("items", L(1)), BlockData.ListAppend("items", L(2)));

		// heap이 찰 때까지 append → MemoryError
		BlockData forever = BlockData.While(SlotData.FromLiteral(BoolType, "true"));
		forever.InnerBlocks.Add(BlockData.ListAppend("full", L(1)));
		Run("HeapFull", task =>
		{
			Check("heap이 차면 MemoryError(OutOfMemory)", task.Failure == FunctionFailure.OutOfMemory, $"실제 {task.EndReason}, {task.Failure}");
			// 실패한 순간 남은 칸은 0이고, 작업이 끝나며 프레임 1칸을 돌려받는다
			Check("MemoryError 뒤 남은 칸은 돌려받은 프레임 1칸", memory.Free == 1, $"실제 Free {memory.Free}");
		}, forever);
	}

	// ---- 함수 데이터 도우미 ----

	static SlotData L(int value) => SlotData.FromLiteral(Int, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
	static SlotData S(string value) => SlotData.FromLiteral(Str, value);
	static SlotData V(string name) => SlotData.FromVariable(name);
	static SlotData Arith(SlotData left, ArithmeticOperator op, SlotData right) => SlotData.FromBlock(BlockData.Arithmetic(left, op, right));
	static SlotData Cmp(SlotData left, CompareOperator op, SlotData right) => SlotData.FromBlock(BlockData.Compare(left, op, right));

	static BlockData If(SlotData condition, params BlockData[] body)
	{
		BlockData block = BlockData.Conditional(ConditionalBranch.If, condition);
		block.InnerBlocks.AddRange(body);
		return block;
	}

	static FunctionData Data(string name, params BlockData[] blocks)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.AddRange(blocks);
		return data;
	}

	void Run(string name, Action<FunctionTask> expect, params BlockData[] blocks) => Cast(Data(name, blocks), Array.Empty<FunctionData>(), expect);

	// 시험을 하나씩 차례로 돌린다. heap 칸 수를 다른 시험의 리스트와 섞지 않고 정확히 보기 위함
	readonly Queue<(FunctionData Data, FunctionData[] Owned, Action<FunctionTask> Expect)> queue = new Queue<(FunctionData, FunctionData[], Action<FunctionTask>)>();

	void Cast(FunctionData data, FunctionData[] owned, Action<FunctionTask> expect) => queue.Enqueue((data, owned, expect));

	void StartNext()
	{
		while (queue.Count > 0)
		{
			(FunctionData data, FunctionData[] owned, Action<FunctionTask> expect) = queue.Dequeue();
			if (!FunctionDataConverter.TryConvert(data, owned, LanguageFamily.Python, out UserFunction function))
			{
				Check($"{data.Name}: 변환", false);
				continue;
			}
			expectations[data.Name] = expect;
			if (executor.Cast(function) != null) return;
			expectations.Remove(data.Name);
			Check($"{data.Name}: 시전", false);
		}
		if (failures == 0) Debug.Log("[PythonGrammarTest] 전체 통과");
		else Debug.LogError($"[PythonGrammarTest] 실패 {failures}개");
	}

	Action<FunctionTask> Done(Action check) => task =>
	{
		Check($"{task.Name}: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
		check();
	};

	Action<FunctionTask> Fails(FunctionFailure failure) => task =>
		Check($"{task.Name}: {failure}로 실패", task.Failure == failure, $"실제 {task.EndReason}, {task.Failure}");

	void ExpectGlobal(string name, string expected, string what)
	{
		bool found = memory.Globals.TryGet(name, out RuntimeValue value);
		string text = found ? (value.Type?.Kind == VariableTypeKind.Bool ? (value.AsBool() ? "True" : "False") : value.ToString()) : "(없음)";
		Check($"{what}: {name} == {expected}", text == expected, $"실제 {text}");
	}

	void OnTaskEnded(FunctionTask task)
	{
		if (!expectations.TryGetValue(task.Name, out Action<FunctionTask> expect)) return;
		expectations.Remove(task.Name);
		expect(task);
		StartNext();
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[PythonGrammarTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";
		if (passed) Debug.Log(message);
		else
		{
			failures++;
			Debug.LogError(message);
		}
	}
}
