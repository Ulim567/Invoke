using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BlockUsageCounter(블록·함수 개수 세기, #500)를 확인하는 테스트 컴포넌트. 플레이를 시작하면 테스트를 돌리고 결과를 콘솔 로그로 남긴다.<br/>
/// 서버·실행기가 필요 없으므로 씬의 아무 오브젝트에나 붙이면 된다. 마지막 줄에 통과 개수가 나온다.
/// </summary>
public class BlockUsageCounterTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	int passed;
	int total;

	void Start()
	{
		TestSlotFunction();
		TestUserFunctionCalls();
		TestRecursion();
		TestLoopAndConditional();
		TestGlobalsAndMissing();

		string summary = $"[BlockUsageCounterTest] {passed}/{total} 통과";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	// Q 슬롯의 thrust(get_bug()); → thrust 1개, get_bug 1개
	void TestSlotFunction()
	{
		FunctionData slot = SlotWith(Thrust());
		var used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot }, null);
		Check("thrust(get_bug()); → thrust 1, get_bug 1, 다른 항목 없음",
			Get(used, PredefinedFunctionIds.Thrust) == 1 && Get(used, PredefinedFunctionIds.GetBug) == 1 && used.Count == 2, Describe(used));
	}

	// 내가 만든 함수는 부른 횟수만큼 안을 다시 센다. 부르지 않으면 세지 않는다
	void TestUserFunctionCalls()
	{
		FunctionData aim = FunctionData.Create("aim");
		aim.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust, SlotData.EmptyValue()));

		FunctionData slot = SlotWith(BlockData.CallUserFunction(aim.Guid), BlockData.CallUserFunction(aim.Guid));
		var used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot, aim }, null);
		Check("aim() 안 thrust 1개, aim()을 2번 부름 → thrust 2, 호출 블록은 세지 않음",
			Get(used, PredefinedFunctionIds.Thrust) == 2 && used.Count == 1, Describe(used));

		FunctionData emptySlot = SlotWith();
		used = BlockUsageCounter.Count(new[] { emptySlot.Guid }, new[] { emptySlot, aim }, null);
		Check("아무도 부르지 않는 aim() → 0개", used.Count == 0, Describe(used));

		// 함수가 함수를 부르는 2단계: outer() { aim(); aim(); aim(); }를 슬롯에서 2번 → thrust 6
		FunctionData outer = FunctionData.Create("outer");
		for (int i = 0; i < 3; i++) outer.Blocks.Add(BlockData.CallUserFunction(aim.Guid));
		FunctionData twice = SlotWith(BlockData.CallUserFunction(outer.Guid), BlockData.CallUserFunction(outer.Guid));
		used = BlockUsageCounter.Count(new[] { twice.Guid }, new[] { twice, outer, aim }, null);
		Check("outer()가 aim()을 3번, 슬롯이 outer()를 2번 → thrust 6", Get(used, PredefinedFunctionIds.Thrust) == 6, Describe(used));
	}

	// 지금 세고 있는 함수를 다시 만나면 그 안은 다시 세지 않는다
	void TestRecursion()
	{
		FunctionData self = FunctionData.Create("self");
		self.Blocks.Add(Thrust());
		self.Blocks.Add(BlockData.CallUserFunction(self.Guid));
		FunctionData slot = SlotWith(BlockData.CallUserFunction(self.Guid));
		var used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot, self }, null);
		Check("자기를 부르는 self() 안 thrust(get_bug()) → thrust 1, get_bug 1",
			Get(used, PredefinedFunctionIds.Thrust) == 1 && Get(used, PredefinedFunctionIds.GetBug) == 1, Describe(used));

		// 서로 부르는 두 함수: f() { thrust; g(); }, g() { get_bug 문장; f(); }
		FunctionData f = FunctionData.Create("f");
		FunctionData g = FunctionData.Create("g");
		f.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust, SlotData.EmptyValue()));
		f.Blocks.Add(BlockData.CallUserFunction(g.Guid));
		g.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug));
		g.Blocks.Add(BlockData.CallUserFunction(f.Guid));
		slot = SlotWith(BlockData.CallUserFunction(f.Guid));
		used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot, f, g }, null);
		Check("f()와 g()가 서로 부름 → thrust 1, get_bug 1",
			Get(used, PredefinedFunctionIds.Thrust) == 1 && Get(used, PredefinedFunctionIds.GetBug) == 1, Describe(used));
	}

	// 반복문은 곱하지 않는다. if, else if, else는 따로 센다
	void TestLoopAndConditional()
	{
		BlockData forBlock = BlockData.For("i", SlotData.FromLiteral(Int, "0"), SlotData.FromLiteral(Int, "10"));
		forBlock.InnerBlocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust, SlotData.EmptyValue()));
		FunctionData slot = SlotWith(forBlock);
		var used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot }, null);
		Check("10번 도는 for 안 thrust → For 1, thrust 1",
			Get(used, "For") == 1 && Get(used, PredefinedFunctionIds.Thrust) == 1, Describe(used));

		slot = SlotWith(
			BlockData.Conditional(ConditionalBranch.If, SlotData.EmptyValue()),
			BlockData.Conditional(ConditionalBranch.ElseIf, SlotData.EmptyValue()),
			BlockData.Conditional(ConditionalBranch.Else));
		used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot }, null);
		Check("if, else if, else → If 1, ElseIf 1, Else 1",
			Get(used, "If") == 1 && Get(used, "ElseIf") == 1 && Get(used, "Else") == 1 && used.Count == 3, Describe(used));
	}

	// 전역 변수 파일의 블록도 세고, 없는 함수 호출은 건너뛴다
	void TestGlobalsAndMissing()
	{
		var globals = new GlobalVariablesData();
		globals.Declarations.Add(BlockData.Declare(Int, "hp", SlotData.FromLiteral(Int, "3")));
		FunctionData slot = SlotWith(BlockData.CallUserFunction("없는-GUID"));
		var used = BlockUsageCounter.Count(new[] { slot.Guid }, new[] { slot }, globals);
		Check("전역 변수 int hp = 3; → Declare 1, 없는 함수 호출은 0", Get(used, "Declare") == 1 && used.Count == 1, Describe(used));
	}

	// thrust(get_bug()); 한 줄
	static BlockData Thrust() =>
		BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust,
			SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug)));

	static FunctionData SlotWith(params BlockData[] blocks)
	{
		FunctionData slot = FunctionData.Create(ProgressRules.SlotFunctionName(0));
		slot.Blocks.AddRange(blocks);
		return slot;
	}

	static int Get(Dictionary<string, int> used, string key) => used.TryGetValue(key, out int count) ? count : 0;

	static string Describe(Dictionary<string, int> used)
	{
		var parts = new List<string>();
		foreach (KeyValuePair<string, int> pair in used) parts.Add($"{pair.Key} {pair.Value}");
		return parts.Count == 0 ? "없음" : string.Join(", ", parts);
	}

	void Check(string name, bool ok, string detail)
	{
		total++;
		if (ok)
		{
			passed++;
			Debug.Log($"[BlockUsageCounterTest] [통과] {name}");
		}
		else
		{
			Debug.LogError($"[BlockUsageCounterTest] [실패] {name} (실제: {detail})");
		}
	}
}
