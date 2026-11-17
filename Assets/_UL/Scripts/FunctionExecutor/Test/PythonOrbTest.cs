using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Python 마나 구슬 시험 (#532). orb_create가 Orb를 돌려주고, 담지 않은 구슬은 가비지 컬렉션이 해제하며, 구슬 스킬은 「다 쓴 구슬」로만 바꾸는지 본다.
/// <para>
/// - 사용법: Play 중 Spawn(baseStatsPath)을 부른다. 콘솔에 「[PythonOrbTest] … 통과/실패」, 마지막에 「전체 통과」.<br/>
/// - 시험 오브젝트에 ManaOrbs, NetworkIdentity, BoxCollider2D를 붙인다. 네트워크를 켜지 않은 상태에서 돈다.
/// </para>
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class PythonOrbTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	ProcessMemory memory;
	ManaOrbs orbs;
	LanguageRuleData rule;
	int failures;
	readonly Dictionary<string, Action<FunctionTask>> expectations = new Dictionary<string, Action<FunctionTask>>();
	readonly Queue<FunctionData> queue = new Queue<FunctionData>();
	readonly Dictionary<string, Action<FunctionTask>> pendingExpectations = new Dictionary<string, Action<FunctionTask>>();

#if UNITY_EDITOR
	/// <summary>시험 오브젝트를 만든다. baseStatsPath는 PlayerStats의 기본 능력치 에셋 경로.</summary>
	public static GameObject Spawn(string baseStatsPath)
	{
		GameObject go = new GameObject("PythonOrbTest");
		go.SetActive(false);
		go.AddComponent<BoxCollider2D>();
		go.AddComponent<Mirror.NetworkIdentity>();
		PlayerStats stats = go.AddComponent<PlayerStats>();
		FieldInfo baseStats = typeof(PlayerStats).GetField("baseStats", BindingFlags.NonPublic | BindingFlags.Instance);
		baseStats?.SetValue(stats, UnityEditor.AssetDatabase.LoadAssetAtPath<ScriptableObject>(baseStatsPath));
		// 기본 능력치의 메모리(10칸)로는 전역 변수·리스트·구슬 시험을 모두 담을 수 없어 늘린다
		stats.AddBonus(StatType.MemorySize, 90);
		LanguageRule language = go.AddComponent<LanguageRule>();
		language.SetRuleDataForTest(LanguageRuleData.CreatePythonForTest());
		// 시험 오브젝트에는 PlayerCharacter가 없어 캐릭터 언어로 정할 수 없으므로 언어 방식을 직접 정한다
		language.SetFamilyForTest(LanguageFamily.Python);
		go.AddComponent<FunctionExecutor>();
		go.AddComponent<FunctionExecutorLogger>();
		go.AddComponent<ManaOrbs>();
		go.AddComponent<PythonOrbTest>();
		go.SetActive(true);
		return go;
	}
#endif

	System.Collections.IEnumerator Start()
	{
		// 실행기의 Start(전역 변수 만들기, 가비지 컬렉션 켜기)가 끝난 뒤에 시작한다
		yield return null;
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		orbs = GetComponent<ManaOrbs>();
		rule = GetComponent<LanguageRule>().Rule;
		executor.TaskEnded += OnTaskEnded;
		Debug.Log($"[PythonOrbTest] 메모리 {memory.Capacity}칸");

		// 전역 변수 파일: o = None, keep = [], r1 … r5 = 0
		GlobalVariablesData globals = new GlobalVariablesData();
		globals.Declarations.Add(BlockData.Assign("o", SlotData.FromNone()));
		globals.Declarations.Add(BlockData.ListNew("keep"));
		for (int i = 1; i <= 5; i++) globals.Declarations.Add(BlockData.Assign("r" + i, L(0)));
		executor.SetGlobalVariablesFile(globals);
		executor.RestartProcess();

		// o = orb_create(); keep.append(orb_create()); orb_create()  → 담은 구슬 2개만 남음
		Run("Create", Done(() =>
			{
				Check("쓸 수 있는 구슬 2개", orbs.Count == 2, $"실제 {orbs.Count}");
				Check("담지 않은 세 번째 구슬은 해제됨 (heap의 구슬 2개)", memory.Heap.GetPointers<ManaOrb>().Count == 2, $"실제 {memory.Heap.GetPointers<ManaOrb>().Count}");
			}),
			BlockData.Global("o"),
			BlockData.Assign("o", Call(PredefinedFunctionIds.PyOrbCreate)),
			BlockData.ListAppend("keep", Call(PredefinedFunctionIds.PyOrbCreate)),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbCreate));

		// r1 = is_valid_orb(o); r2 = is_valid_orb(None)
		Run("Valid", Done(() => { ExpectGlobal("r1", "True", "is_valid_orb(o)"); ExpectGlobal("r2", "False", "is_valid_orb(None)"); }),
			BlockData.Global("r1"), BlockData.Global("r2"),
			BlockData.Assign("r1", Call(PredefinedFunctionIds.PyIsValidOrb, V("o"))),
			BlockData.Assign("r2", Call(PredefinedFunctionIds.PyIsValidOrb, SlotData.FromNone())));

		// orb_burst(keep); r3 = is_valid_orb(keep[0]) → 다 쓴 구슬, 리스트에서 빠지지 않음
		Run("BurstKeep", Done(() =>
			{
				ExpectGlobal("r3", "False", "다 쓴 구슬 is_valid_orb(keep[0])");
				Check("쓸 수 있는 구슬 1개 (다 쓴 구슬은 빠짐)", orbs.Count == 1, $"실제 {orbs.Count}");
				Check("다 쓴 구슬은 해제되지 않음 (heap의 구슬 2개)", memory.Heap.GetPointers<ManaOrb>().Count == 2, $"실제 {memory.Heap.GetPointers<ManaOrb>().Count}");
			}),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbBurst, V("keep")),
			BlockData.Global("r3"),
			BlockData.Assign("r3", Call(PredefinedFunctionIds.PyIsValidOrb, SlotData.FromBlock(BlockData.ArrayGet("keep", L(0))))));

		// 쓸 구슬이 없는 리스트 → NoManaOrbError
		Run("BurstAgain", task => Check("BurstAgain: NoManaOrbError", task.Failure == FunctionFailure.SkillFailed && task.Error is NoManaOrbError,
				$"실제 {task.EndReason}, {task.Failure}, {task.Error?.GetType().Name}"),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbBurst, V("keep")));

		// 다 쓴 구슬을 orb_shot에 넘김 → UsedManaOrbError
		Run("UsedOrb", task => Check("UsedOrb: UsedManaOrbError", task.Failure == FunctionFailure.SkillFailed && task.Error is UsedManaOrbError,
				$"실제 {task.EndReason}, {task.Failure}, {task.Error?.GetType().Name}"),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbShot, SlotData.EmptyValue(), SlotData.FromBlock(BlockData.ArrayGet("keep", L(0)))));

		// 구슬이 아닌 원소가 든 리스트 → TypeError
		Run("BurstBad", task => Check("BurstBad: TypeError(TypeMismatch)", task.Failure == FunctionFailure.TypeMismatch, $"실제 {task.EndReason}, {task.Failure}"),
			BlockData.ListNew("bad"),
			BlockData.ListAppend("bad", L(1)),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbBurst, V("bad")));

		// 구슬이 아닌 값을 orb에 넘김 → TypeError (파라미터 타입 검사)
		Run("ShotNotOrb", task => Check("ShotNotOrb: TypeError(TypeMismatch)", task.Failure == FunctionFailure.TypeMismatch, $"실제 {task.EndReason}, {task.Failure}"),
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.PyOrbShot, SlotData.EmptyValue(), L(3)));

		// global o; del o → o의 구슬은 가리키는 이름이 없어 해제됨. keep의 다 쓴 구슬만 남음
		Run("Del", Done(() => Check("del 뒤 heap의 구슬 1개", memory.Heap.GetPointers<ManaOrb>().Count == 1, $"실제 {memory.Heap.GetPointers<ManaOrb>().Count}")),
			BlockData.Global("o"), BlockData.Del("o"));

		// 쓸 수 있는 구슬이 5개면 6번째 orb_create()는 None. 함수가 끝나면 지역 리스트와 구슬 5개가 해제됨
		BlockData loop = BlockData.For("i", L(0), L(6));
		loop.InnerBlocks.Add(BlockData.ListAppend("lst", Call(PredefinedFunctionIds.PyOrbCreate)));
		Run("Five", Done(() =>
			{
				ExpectGlobal("r4", "True", "6번째 orb_create() == None");
				ExpectGlobal("r5", "5", "5개일 때 쓸 수 있는 구슬 수 len 확인용");
				Check("함수가 끝나면 지역 리스트의 구슬 해제 (heap의 구슬 1개)", memory.Heap.GetPointers<ManaOrb>().Count == 1, $"실제 {memory.Heap.GetPointers<ManaOrb>().Count}");
				Check("쓸 수 있는 구슬 0개", orbs.Count == 0, $"실제 {orbs.Count}");
			}),
			BlockData.Global("r4"), BlockData.Global("r5"),
			BlockData.ListNew("lst"),
			loop,
			BlockData.Assign("r4", SlotData.FromBlock(BlockData.Compare(SlotData.FromBlock(BlockData.ArrayGet("lst", L(5))), CompareOperator.Equal, SlotData.FromNone()))),
			BlockData.Assign("r5", L(5)));

		StartNext();
	}

	static SlotData L(int value) => SlotData.FromLiteral(Int, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
	static SlotData V(string name) => SlotData.FromVariable(name);
	static SlotData Call(string id, params SlotData[] arguments) => SlotData.FromBlock(BlockData.CallPredefinedFunction(id, arguments));

	void Run(string name, Action<FunctionTask> expect, params BlockData[] blocks)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.AddRange(blocks);
		pendingExpectations[name] = expect;
		queue.Enqueue(data);
	}

	void StartNext()
	{
		while (queue.Count > 0)
		{
			FunctionData data = queue.Dequeue();
			if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), LanguageFamily.Python, out UserFunction function))
			{
				Check($"{data.Name}: 변환", false);
				continue;
			}
			expectations[data.Name] = pendingExpectations[data.Name];
			if (executor.Cast(function) != null) return;
			expectations.Remove(data.Name);
			Check($"{data.Name}: 시전", false);
		}
		if (failures == 0) Debug.Log("[PythonOrbTest] 전체 통과");
		else Debug.LogError($"[PythonOrbTest] 실패 {failures}개");
	}

	Action<FunctionTask> Done(Action check) => task =>
	{
		Check($"{task.Name}: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}, {task.Error?.GetType().Name}");
		check();
	};

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
		string message = $"[PythonOrbTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";
		if (passed) Debug.Log(message);
		else
		{
			failures++;
			Debug.LogError(message);
		}
	}
}
