using System.Linq;
using UnityEngine;

/// <summary>
/// 함수 데이터 테스트용 컴포넌트. 플레이를 시작하면 테스트를 돌리고 결과를 콘솔 로그로 남긴다.<br/>
/// 실행기가 필요 없으므로 씬의 아무 오브젝트에나 붙이면 된다.
/// <para>
/// 테스트에 쓰는 사전 정의 함수 ID("c.thrust", "input.get_position")는 임시 값이다. 실제 ID 규칙은 C 스킬 작성 규칙에서 정한다.
/// </para>
/// </summary>
public class FunctionDataTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);

	void Start()
	{
		TestSaveAndLoad();
		TestCodeSize();
		TestBrokenJson();
	}

	// 테스트 1: 저장했다 불러오면 같은 내용이 나오는지 (이슈 #27 완료 조건)
	void TestSaveAndLoad()
	{
		FunctionData helper = FunctionData.Create("Helper");
		FunctionData function = MakeFunctionWithAllBlockKinds(helper.Guid);

		string json = FunctionDataJson.ToJson(function);
		Debug.Log($"[FunctionDataTest] 저장한 JSON: {json}");

		bool loaded = FunctionDataJson.TryFromJson(json, out FunctionData loadedFunction);
		Check("불러오기 성공", loaded);
		if (!loaded) return;

		// 되돌린 함수 데이터를 다시 JSON으로 바꿔서 처음 JSON과 글자 하나까지 같으면 내용이 같은 것이다
		Check("저장했다 불러오면 같은 내용", FunctionDataJson.ToJson(loadedFunction) == json);

		// 블록 24개: 아래 MakeFunctionWithAllBlockKinds의 주석에 적은 개수를 더한 값
		Check("블록 수 24", loadedFunction.CountBlocks() == 24, $"실제 {loadedFunction.CountBlocks()}");

		// 지역 변수 5개: x, i(for), arr, s, p
		string localNames = string.Join(", ", loadedFunction.LocalVariables().Select(v => v.Name));
		Check("지역 변수 x, i, arr, s, p", localNames == "x, i, arr, s, p", $"실제 {localNames}");
	}

	// 정한 블록 종류 17개를 모두 한 번 이상 쓰는 함수. 줄 끝 숫자는 그 줄의 블록 수
	static FunctionData MakeFunctionWithAllBlockKinds(string helperGuid)
	{
		FunctionData function = FunctionData.Create("Attack");
		function.ReturnType = Int;
		function.Parameters.Add(new ParameterData("count", Int));

		// int x = 3                                                                       1
		function.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));

		// x = x + 1                                                                       2
		function.Blocks.Add(BlockData.Assign("x", SlotData.FromBlock(
			BlockData.Arithmetic(SlotData.FromVariable("x"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "1")))));

		// if (x > 3) { 찌르기 }                                                            3
		BlockData ifBlock = BlockData.Conditional(ConditionalBranch.If, SlotData.FromBlock(
			BlockData.Compare(SlotData.FromVariable("x"), CompareOperator.Greater, SlotData.FromLiteral(Int, "3"))));
		ifBlock.InnerBlocks.Add(BlockData.CallPredefinedFunction("c.thrust"));
		function.Blocks.Add(ifBlock);

		// else { 0.5초 기다리기 }                                                           2
		BlockData elseBlock = BlockData.Conditional(ConditionalBranch.Else);
		elseBlock.InnerBlocks.Add(BlockData.Wait(SlotData.FromLiteral(Float, "0.5")));
		function.Blocks.Add(elseBlock);

		// for (int i = 0; i < 10; i++) { if (true And Not flag) { break } continue }      6
		BlockData forBlock = BlockData.For("i", SlotData.FromLiteral(Int, "0"), SlotData.FromLiteral(Int, "10"));
		BlockData innerIf = BlockData.Conditional(ConditionalBranch.If, SlotData.FromBlock(
			BlockData.Logic(SlotData.FromLiteral(Bool, "true"), LogicOperator.And,
				SlotData.FromBlock(BlockData.Not(SlotData.FromVariable("flag"))))));
		innerIf.InnerBlocks.Add(BlockData.Break());
		forBlock.InnerBlocks.Add(innerIf);
		forBlock.InnerBlocks.Add(BlockData.Continue());
		function.Blocks.Add(forBlock);

		// while (x < 5) { }                                                               2
		function.Blocks.Add(BlockData.While(SlotData.FromBlock(
			BlockData.Compare(SlotData.FromVariable("x"), CompareOperator.Less, SlotData.FromLiteral(Int, "5")))));

		// int arr[5]                                                                      1
		function.Blocks.Add(BlockData.ArrayDeclare(Int, "arr", SlotData.FromLiteral(Int, "5")));

		// Sword* s = malloc(Sword)                                                        2
		VariableType sword = VariableType.StructOf("Sword");
		function.Blocks.Add(BlockData.Declare(VariableType.PointerTo(sword), "s", SlotData.FromBlock(BlockData.Malloc(sword))));

		// free(s)                                                                         1
		function.Blocks.Add(BlockData.Free(SlotData.FromVariable("s")));

		// Helper(x)                                                                       1
		function.Blocks.Add(BlockData.CallUserFunction(helperGuid, SlotData.FromVariable("x")));

		// 위치 p = GetPosition()                                                           2
		function.Blocks.Add(BlockData.Declare(new VariableType(VariableTypeKind.Position), "p",
			SlotData.FromBlock(BlockData.CallPredefinedFunction("input.get_position"))));

		// return x                                                                        1
		function.Blocks.Add(BlockData.Return(SlotData.FromVariable("x")));

		return function;
	}

	// 테스트 2: 코드 영역 크기
	void TestCodeSize()
	{
		// C (블록 5개): int x = 3, for (i: 0~3) { 찌르기 }, x = x + 1
		FunctionData c = FunctionData.Create("C");
		c.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));
		BlockData forBlock = BlockData.For("i", SlotData.FromLiteral(Int, "0"), SlotData.FromLiteral(Int, "3"));
		forBlock.InnerBlocks.Add(BlockData.CallPredefinedFunction("c.thrust"));
		c.Blocks.Add(forBlock);
		c.Blocks.Add(BlockData.Assign("x", SlotData.FromBlock(
			BlockData.Arithmetic(SlotData.FromVariable("x"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "1")))));

		// A (블록 4개): C를 두 번 부름
		FunctionData a = FunctionData.Create("A");
		a.Blocks.Add(BlockData.CallUserFunction(c.Guid));
		a.Blocks.Add(BlockData.CallUserFunction(c.Guid));
		a.Blocks.Add(BlockData.Declare(Int, "y"));
		a.Blocks.Add(BlockData.Return());

		// B (블록 2개): C를 부름
		FunctionData b = FunctionData.Create("B");
		b.Blocks.Add(BlockData.CallUserFunction(c.Guid));
		b.Blocks.Add(BlockData.Return());

		FunctionData[] owned = { a, b, c };

		// A 4 + B 2 + C 5 = 11. C는 세 번 불리지만 한 번만 센다
		int size = CodeSizeCalculator.Calculate(new[] { a.Guid, b.Guid }, new string[0], owned);
		Check("슬롯 A, B → 11", size == 11, $"실제 {size}");

		// 슬롯에 C 스킬을 바로 하나 더 넣으면 1이 늘어 12
		size = CodeSizeCalculator.Calculate(new[] { a.Guid, b.Guid }, new[] { "c.thrust" }, owned);
		Check("슬롯 A, B, C 스킬 → 12", size == 12, $"실제 {size}");

		// 재귀: D(블록 2개)가 E를, E(블록 1개)가 D를 부른다. 끝없이 돌지 않고 2 + 1 = 3
		FunctionData d = FunctionData.Create("D");
		FunctionData e = FunctionData.Create("E");
		d.Blocks.Add(BlockData.CallUserFunction(e.Guid));
		d.Blocks.Add(BlockData.Return());
		e.Blocks.Add(BlockData.CallUserFunction(d.Guid));
		size = CodeSizeCalculator.Calculate(new[] { d.Guid }, new string[0], new[] { d, e });
		Check("재귀 D ↔ E → 3", size == 3, $"실제 {size}");

		// 지운 함수 호출: F(블록 2개)가 목록에 없는 GUID를 부른다. 그 함수는 건너뛰고 2
		FunctionData f = FunctionData.Create("F");
		f.Blocks.Add(BlockData.CallUserFunction("deleted-function-guid"));
		f.Blocks.Add(BlockData.Return());
		size = CodeSizeCalculator.Calculate(new[] { f.Guid }, new string[0], new[] { f });
		Check("없는 함수 호출 → 2", size == 2, $"실제 {size}");
	}

	// 테스트 3: 잘못된 JSON은 오류를 던지지 않고 실패로 돌아오는지
	void TestBrokenJson()
	{
		Check("빈 글자열 → 실패", !FunctionDataJson.TryFromJson("", out _));
		Check("깨진 JSON → 실패", !FunctionDataJson.TryFromJson("{ \"Name\": ", out _));
		Check("null → 실패", !FunctionDataJson.TryFromJson("null", out _));
		Check("번호로 적힌 블록 종류 → 실패", !FunctionDataJson.TryFromJson("{ \"Blocks\": [ { \"Kind\": 999 } ] }", out _));
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[FunctionDataTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
