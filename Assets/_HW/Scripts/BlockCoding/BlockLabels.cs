using UnityEngine;

/// <summary>
/// 블록 데이터(BlockData)를 화면에 보여 줄 글자와 색으로 바꾼다. 보여 주기만 하고 데이터는 바꾸지 않는다.
/// <para>
/// 지금은 C 언어 모양으로만 보여 준다. 다른 언어가 생기면 언어별로 글자를 나눈다 (function-data.md 「언어별 표시」).
/// </para>
/// </summary>
public static class BlockLabels
{
	// 블록 분류별 색. 글자는 밝은색이므로 배경은 어두운 쪽으로 둔다
	static readonly Color ControlColor = new Color32(0xB8, 0x74, 0x2F, 0xFF);  // 조건문, 반복문
	static readonly Color VariableColor = new Color32(0x2F, 0x8F, 0x86, 0xFF); // 선언, 대입
	static readonly Color FunctionColor = new Color32(0x6A, 0x5A, 0xC9, 0xFF); // 함수, C 스킬
	static readonly Color MemoryColor = new Color32(0xC2, 0x4D, 0x86, 0xFF);   // malloc, free
	static readonly Color OtherColor = new Color32(0x4A, 0x55, 0x78, 0xFF);    // return, 기다리기

	/// <summary>
	/// 블록 분류에 맞는 배경색
	/// </summary>
	public static Color ColorOf(BlockData block) => block.Kind switch
	{
		BlockKind.Conditional or BlockKind.For or BlockKind.While
			or BlockKind.Break or BlockKind.Continue => ControlColor,
		BlockKind.Declare or BlockKind.Assign or BlockKind.ArrayDeclare => VariableColor,
		BlockKind.Function => FunctionColor,
		BlockKind.Malloc or BlockKind.Free => MemoryColor,
		_ => OtherColor,
	};

	/// <summary>
	/// 블록 한 줄에 보여 줄 글자. 예: "if ( )", "for (int i = 0; i &lt; 10; i++)"
	/// </summary>
	public static string TextOf(BlockData block)
	{
		switch (block.Kind)
		{
			case BlockKind.Declare:
				return $"{TypeName(Slot(block, 0)?.Type)} {Slot(block, 1)?.Name} = {Value(Slot(block, 2))}";
			case BlockKind.Assign:
				return $"{Slot(block, 0)?.VariableName} = {Value(Slot(block, 1))}";
			case BlockKind.Conditional:
				string branch = Slot(block, 0)?.Option;
				if (branch == nameof(ConditionalBranch.Else)) return "else";
				string keyword = branch == nameof(ConditionalBranch.ElseIf) ? "else if" : "if";
				return $"{keyword} ({Value(Slot(block, 1))})";
			case BlockKind.For:
				string i = Slot(block, 0)?.Name;
				return $"for (int {i} = {Value(Slot(block, 1))}; {i} < {Value(Slot(block, 2))}; {i}++)";
			case BlockKind.While:
				return $"while ({Value(Slot(block, 0))})";
			case BlockKind.Break:
				return "break;";
			case BlockKind.Continue:
				return "continue;";
			case BlockKind.Free:
				return $"free({Value(Slot(block, 0))});";
			case BlockKind.Return:
				return $"return {Value(Slot(block, 0))};";
			case BlockKind.Wait:
				return $"wait({Value(Slot(block, 0))}초);";
			case BlockKind.Function:
				return $"{FunctionName(block)}();";
			default:
				return block.Kind.ToString();
		}
	}

	static SlotData Slot(BlockData block, int index) =>
		index < block.Slots.Count ? block.Slots[index] : null;

	// 값 칸 하나를 글자로. 비어 있으면 "  "로 빈칸을 보여 준다. 값 넣기는 「블럭코딩 화면 2」에서 만든다
	static string Value(SlotData slot)
	{
		if (slot == null) return "  ";
		if (slot.Block != null) return TextOf(slot.Block).TrimEnd(';');
		if (!string.IsNullOrEmpty(slot.VariableName)) return slot.VariableName;
		if (!string.IsNullOrEmpty(slot.Literal)) return slot.Literal;
		return "  ";
	}

	static string TypeName(VariableType type) => type == null ? "?" : type.Kind switch
	{
		VariableTypeKind.Int => "int",
		VariableTypeKind.Float => "float",
		VariableTypeKind.Bool => "bool",
		VariableTypeKind.String => "string",
		VariableTypeKind.Char => "char",
		_ => type.Kind.ToString(),
	};

	// 고정 ID "c.thrust"에서 점 뒤 이름만 보여 준다. 사용자 정의 함수는 GUID라서 이름을 따로 찾아야 한다 (화면 2)
	static string FunctionName(BlockData block)
	{
		if (block.IsUserFunction || string.IsNullOrEmpty(block.FunctionId)) return "함수";
		int dot = block.FunctionId.IndexOf('.');
		return dot >= 0 ? block.FunctionId.Substring(dot + 1) : block.FunctionId;
	}
}
