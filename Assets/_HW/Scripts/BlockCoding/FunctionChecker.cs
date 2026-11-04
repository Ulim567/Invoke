using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 블록 함수 하나를 저장·장착 전에 검사한다 (편집 화면의 「컴파일」). 데이터는 바꾸지 않고 오류 목록만 돌려준다.
/// <para>
/// - 편집 화면(BlockEditor)의 컴파일 버튼·오류 창·빨간 테두리, 함수 목록의 빨간 밑줄, 장착 화면(SlotEquipPanel)에서 오류 있는 함수를 막을 때 쓴다.<br/>
/// - 검사: for·while 밖의 break·continue, 값을 돌려줘야 하는데 return이 없는 길(BlockData.AlwaysReturnsValue), 비어 있는 값 칸,
///   선언하지 않은 변수(함수 안의 선언·배열·for의 변수, 파라미터, 전역 변수에 없는 이름) (function-data.md, function-executor.md).
/// </para>
/// </summary>
public static class FunctionChecker
{
	/// <summary>오류 하나. Block은 오류가 난 문장 블록 (함수 전체의 오류면 null), Code는 그 블록의 C 글자</summary>
	public class Error
	{
		public BlockData Block;
		public string Code;
		public string Message;
	}

	/// <summary>
	/// 오류 목록. globals는 전역 변수 선언 (null이면 전역 변수가 없는 것으로 본다).
	/// </summary>
	public static List<Error> Find(FunctionData function, GlobalVariablesData globals = null)
	{
		var errors = new List<Error>();
		if (function == null || function.Blocks == null) return errors;

		// 쓸 수 있는 이름: 파라미터, 전역 변수, 함수 안에서 선언한 이름 (순서는 보지 않음)
		var declared = new HashSet<string>();
		foreach (ParameterData parameter in function.Parameters ?? new List<ParameterData>())
			if (!string.IsNullOrEmpty(parameter?.Name)) declared.Add(parameter.Name);
		foreach (BlockData declaration in globals?.Declarations ?? new List<BlockData>())
			CollectDeclared(new[] { declaration }, declared);
		CollectDeclared(function.Blocks, declared);

		Walk(function.Blocks, 0, declared, errors);

		bool returnsValue = function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void;
		if (returnsValue && !BlockData.AlwaysReturnsValue(function.Blocks))
			errors.Add(new Error { Code = function.Name, Message = "값을 돌려주지 않고 끝나는 길이 있어요 (return 필요)" });
		return errors;
	}

	/// <summary>오류 문장만 (예전 쓰임새)</summary>
	public static List<string> Check(FunctionData function, GlobalVariablesData globals = null) =>
		Find(function, globals).Select(e => e.Message).ToList();

	/// <summary>오류가 하나라도 있으면 true</summary>
	public static bool HasErrors(FunctionData function, GlobalVariablesData globals = null) => Find(function, globals).Count > 0;

	static void CollectDeclared(IEnumerable<BlockData> blocks, HashSet<string> names)
	{
		foreach (BlockData block in blocks)
		{
			if (block == null) continue;
			if ((block.Kind == BlockKind.Declare || block.Kind == BlockKind.ArrayDeclare) && block.Slots.Count > 1 && !string.IsNullOrEmpty(block.Slots[1]?.Name))
				names.Add(block.Slots[1].Name);
			if (block.Kind == BlockKind.For && block.Slots.Count > 0 && !string.IsNullOrEmpty(block.Slots[0]?.Name))
				names.Add(block.Slots[0].Name);
			if (block.InnerBlocks != null) CollectDeclared(block.InnerBlocks, names);
		}
	}

	// 문장 블록을 차례로 보며 오류를 모은다. loopDepth는 지금 for·while 안에 몇 겹 들어와 있는지
	static void Walk(IEnumerable<BlockData> blocks, int loopDepth, HashSet<string> declared, List<Error> errors)
	{
		foreach (BlockData block in blocks)
		{
			if (block == null) continue;
			if ((block.Kind == BlockKind.Break || block.Kind == BlockKind.Continue) && loopDepth == 0)
				Add(errors, block, "break, continue는 for, while 안에서만 쓸 수 있어요");

			int empty = 0;
			var unknown = new List<string>();
			CheckSlots(block, declared, ref empty, unknown);
			if (empty > 0) Add(errors, block, $"비어 있는 값 칸 {empty}개");
			foreach (string name in unknown.Distinct()) Add(errors, block, $"선언하지 않은 변수 {name}");

			if (block.InnerBlocks != null)
			{
				bool loop = block.Kind == BlockKind.For || block.Kind == BlockKind.While;
				Walk(block.InnerBlocks, loopDepth + (loop ? 1 : 0), declared, errors);
			}
		}
	}

	// 블록의 칸(끼운 값 블록 안까지)에서 빈 값 칸과 모르는 변수 이름을 찾는다. 선언의 초기값과 return의 값은 비워도 된다
	static void CheckSlots(BlockData block, HashSet<string> declared, ref int empty, List<string> unknown)
	{
		for (int i = 0; i < block.Slots.Count; i++)
		{
			SlotData slot = block.Slots[i];
			if (slot == null) continue;
			if (slot.Block != null)
			{
				CheckSlots(slot.Block, declared, ref empty, unknown);
				continue;
			}
			if (!string.IsNullOrEmpty(slot.VariableName))
			{
				if (!declared.Contains(slot.VariableName)) unknown.Add(slot.VariableName);
				continue;
			}
			bool optional = block.Kind == BlockKind.Return || (block.Kind == BlockKind.Declare && i == 2);
			if (slot.Kind == SlotKind.Value && !optional && string.IsNullOrEmpty(slot.Literal)) empty++;
		}
	}

	static void Add(List<Error> errors, BlockData block, string message) =>
		errors.Add(new Error { Block = block, Code = BlockLabels.TextOf(block), Message = message });
}
