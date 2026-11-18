using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

/// <summary>
/// 출력 함수 시험 (#536). C의 printf와 Python의 print가 값을 그 언어 모양으로 바꾸고, 변수·expression이면 「n = 3」처럼 이름을 붙이는지 본다.
/// <para>
/// - 사용법: Play 중 Spawn(baseStatsPath, python)을 부른다. 콘솔에 「[PrintTest C] … 통과/실패」, 마지막에 「전체 통과」.<br/>
/// - 실행기의 Output event만 본다. 화면(HUD)으로 보내는 일은 FunctionExecutorNetwork·HudView가 하므로 사람이 화면에서 확인한다.
/// </para>
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class PrintTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);

	bool python;
	string tag;
	FunctionExecutor executor;
	int failures;
	// 시전한 함수 이름 → 기대하는 출력 줄들
	readonly Queue<(FunctionData Data, string[] Expected)> queue = new Queue<(FunctionData, string[])>();
	string runningName;
	string[] runningExpected;
	readonly List<string> outputs = new List<string>();

#if UNITY_EDITOR
	/// <summary>시험 오브젝트를 만든다. baseStatsPath는 PlayerStats의 기본 능력치 에셋 경로, python이면 Python 규칙으로 돈다.</summary>
	public static GameObject Spawn(string baseStatsPath, bool python)
	{
		GameObject go = new GameObject(python ? "PrintTest_Python" : "PrintTest_C");
		go.SetActive(false);
		PlayerStats stats = go.AddComponent<PlayerStats>();
		FieldInfo baseStats = typeof(PlayerStats).GetField("baseStats", BindingFlags.NonPublic | BindingFlags.Instance);
		baseStats?.SetValue(stats, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(baseStatsPath));
		// array·리스트를 담을 메모리
		stats.AddBonus(StatType.MemorySize, 40);
		LanguageRule language = go.AddComponent<LanguageRule>();
		if (python)
		{
			language.SetRuleDataForTest(LanguageRuleData.CreatePythonForTest());
			language.SetFamilyForTest(LanguageFamily.Python);
		}
		else
		{
			language.SetRuleDataForTest(UnityEditor.AssetDatabase.LoadAssetAtPath<LanguageRuleData>("Assets/_UL/Data/LanguageRule_C.asset"));
			language.SetFamilyForTest(LanguageFamily.C);
		}
		go.AddComponent<FunctionExecutor>();
		PrintTest test = go.AddComponent<PrintTest>();
		test.python = python;
		go.SetActive(true);
		return go;
	}
#endif

	System.Collections.IEnumerator Start()
	{
		yield return null;
		tag = python ? "[PrintTest Python]" : "[PrintTest C]";
		executor = GetComponent<FunctionExecutor>();
		executor.Output += OnOutput;
		executor.TaskEnded += OnTaskEnded;

		if (python) QueuePython();
		else QueueC();
		StartNext();
	}

	void QueueC()
	{
		string print = PredefinedFunctionIds.Printf;
		Queue("변수", new[] { "n = 3" },
			BlockData.Declare(Int, "n", L(3)),
			Call(print, V("n")));
		Queue("값을 직접 적음", new[] { "5", "true", "abc" },
			Call(print, L(5)),
			Call(print, SlotData.FromLiteral(Bool, "true")),
			Call(print, SlotData.FromLiteral(String, "abc")));
		Queue("expression", new[] { "n + 1 = 4" },
			BlockData.Declare(Int, "n", L(3)),
			Call(print, SlotData.FromBlock(BlockData.Arithmetic(V("n"), ArithmeticOperator.Add, L(1)))));
		Queue("float, bool 변수", new[] { "f = 2.5", "b = false" },
			BlockData.Declare(Float, "f", SlotData.FromLiteral(Float, "2.5")),
			BlockData.Declare(Bool, "b", SlotData.FromLiteral(Bool, "false")),
			Call(print, V("f")),
			Call(print, V("b")));
		Queue("NULL, array", new[] { "NULL", "a = {0, 0, 0}" },
			BlockData.ArrayDeclare(Int, "a", L(3)),
			Call(print, SlotData.FromNull()),
			Call(print, V("a")));

		// for (int i = 0; i < 5; i++) printf(i);  → 다섯 줄
		BlockData loop = BlockData.For("i", L(0), L(5));
		loop.InnerBlocks.Add(Call(print, V("i")));
		Queue("반복문 다섯 줄", new[] { "i = 0", "i = 1", "i = 2", "i = 3", "i = 4" }, loop);
	}

	void QueuePython()
	{
		string print = PredefinedFunctionIds.PyPrint;
		Queue("값", new[] { "True", "None", "abc" },
			Call(print, SlotData.FromLiteral(Bool, "true")),
			Call(print, SlotData.FromNone()),
			Call(print, SlotData.FromLiteral(String, "abc")));
		Queue("소수 변수", new[] { "x = 3.0" },
			BlockData.Assign("x", SlotData.FromLiteral(Float, "3")),
			Call(print, V("x")));
		Queue("리스트", new[] { "lst = [1, 'a']" },
			BlockData.ListNew("lst"),
			BlockData.ListAppend("lst", L(1)),
			BlockData.ListAppend("lst", SlotData.FromLiteral(String, "a")),
			Call(print, V("lst")));
		Queue("논리 expression", new[] { "x > 1 and True = True" },
			BlockData.Assign("x", L(2)),
			Call(print, SlotData.FromBlock(BlockData.Logic(
				SlotData.FromBlock(BlockData.Compare(V("x"), CompareOperator.Greater, L(1))),
				LogicOperator.And,
				SlotData.FromLiteral(Bool, "true")))));
	}

	static SlotData L(int value) => SlotData.FromLiteral(Int, value.ToString(CultureInfo.InvariantCulture));
	static SlotData V(string name) => SlotData.FromVariable(name);
	static BlockData Call(string id, params SlotData[] arguments) => BlockData.CallPredefinedFunction(id, arguments);

	void Queue(string name, string[] expected, params BlockData[] blocks)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.AddRange(blocks);
		queue.Enqueue((data, expected));
	}

	void StartNext()
	{
		while (queue.Count > 0)
		{
			(FunctionData data, string[] expected) = queue.Dequeue();
			LanguageFamily family = python ? LanguageFamily.Python : LanguageFamily.C;
			if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), family, out UserFunction function))
			{
				Check($"{data.Name}: 변환", false, "");
				continue;
			}
			runningName = data.Name;
			runningExpected = expected;
			outputs.Clear();
			if (executor.Cast(function) != null) return;
			Check($"{data.Name}: 시전", false, "");
		}
		if (failures == 0) Debug.Log($"{tag} 전체 통과");
		else Debug.LogError($"{tag} 실패 {failures}개");
	}

	void OnOutput(FunctionTask task, string text) => outputs.Add(text);

	void OnTaskEnded(FunctionTask task)
	{
		if (task.Name != runningName) return;
		string actual = string.Join(" | ", outputs);
		string expected = string.Join(" | ", runningExpected);
		Check($"{runningName}: 출력 {expected}", task.EndReason == FunctionTaskEndReason.Completed && actual == expected,
			$"끝난 이유 {task.EndReason}, 실제 출력 {actual}");
		StartNext();
	}

	void Check(string name, bool ok, string detail)
	{
		if (ok) Debug.Log($"{tag} {name} 통과");
		else
		{
			failures++;
			Debug.LogError($"{tag} {name} 실패: {detail}");
		}
	}
}
