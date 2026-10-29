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
	static readonly Color ValueColor = new Color32(0x3E, 0x8E, 0x4F, 0xFF);    // 연산, 비교, 논리
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
		BlockKind.Arithmetic or BlockKind.Compare or BlockKind.Logic or BlockKind.Not => ValueColor,
		BlockKind.Function => FunctionColor,
		BlockKind.Malloc or BlockKind.Free => MemoryColor,
		_ => OtherColor,
	};

	/// <summary>
	/// 블록 전체를 한 줄 글자로. 블록 목록(팔레트)의 견본에 쓴다. 예: "if ( )", "x + 1"
	/// </summary>
	public static string TextOf(BlockData block)
	{
		switch (block.Kind)
		{
			case BlockKind.Declare:
				return $"{TypeName(Slot(block, 0)?.Type)} {Slot(block, 1)?.Name} = {Value(Slot(block, 2))};";
			case BlockKind.Assign:
				return $"{Slot(block, 0)?.VariableName} = {Value(Slot(block, 1))};";
			case BlockKind.Arithmetic:
			case BlockKind.Compare:
			case BlockKind.Logic:
				return $"{Value(Slot(block, 0))} {OperatorText(Slot(block, 1)?.Option)} {Value(Slot(block, 2))}";
			case BlockKind.Not:
				return $"!{Value(Slot(block, 0))}";
			case BlockKind.Conditional:
				string branch = Slot(block, 0)?.Option;
				if (branch == nameof(ConditionalBranch.Else)) return "else";
				return $"{BranchKeyword(branch)} ({Value(Slot(block, 1))})";
			case BlockKind.For:
				string i = Slot(block, 0)?.Name;
				return $"for (int {i} = {Value(Slot(block, 1))}; {i} < {Value(Slot(block, 2))}; {i}++)";
			case BlockKind.While:
				return $"while ({Value(Slot(block, 0))})";
			case BlockKind.Break:
				return "break;";
			case BlockKind.Continue:
				return "continue;";
			case BlockKind.ArrayDeclare:
				return $"{TypeName(Slot(block, 0)?.Type)} {Slot(block, 1)?.Name}[{Value(Slot(block, 2))}];";
			case BlockKind.Malloc:
				return $"malloc({TypeName(Slot(block, 0)?.Type)})";
			case BlockKind.Free:
				return $"free({Value(Slot(block, 0))});";
			case BlockKind.Return:
				return $"return {Value(Slot(block, 0))};";
			case BlockKind.Wait:
				return $"wait({Value(Slot(block, 0))}초);";
			case BlockKind.Function:
				string args = string.Join(", ", block.Slots.ConvertAll(Value));
				return $"{FunctionName(block)}({args});";
			default:
				return block.Kind.ToString();
		}
	}

	/// <summary>
	/// 조건문 갈래 이름("If", "ElseIf", "Else")을 C 글자로
	/// </summary>
	public static string BranchKeyword(string branch) => branch switch
	{
		nameof(ConditionalBranch.ElseIf) => "else if",
		nameof(ConditionalBranch.Else) => "else",
		_ => "if",
	};

	/// <summary>
	/// 연산자 enum 이름(BlockOptions.cs)을 C 글자로. 예: "Add" → "+", "And" → "&amp;&amp;"
	/// </summary>
	public static string OperatorText(string option) => option switch
	{
		nameof(ArithmeticOperator.Add) => "+",
		nameof(ArithmeticOperator.Subtract) => "-",
		nameof(ArithmeticOperator.Multiply) => "*",
		nameof(ArithmeticOperator.Divide) => "/",
		nameof(ArithmeticOperator.Modulo) => "%",
		nameof(CompareOperator.Greater) => ">",
		nameof(CompareOperator.Less) => "<",
		nameof(CompareOperator.Equal) => "==",
		nameof(CompareOperator.NotEqual) => "!=",
		nameof(CompareOperator.GreaterOrEqual) => ">=",
		nameof(CompareOperator.LessOrEqual) => "<=",
		nameof(LogicOperator.And) => "&&",
		nameof(LogicOperator.Or) => "||",
		_ => "?",
	};

	/// <summary>
	/// 타입을 C 글자로. 예: Int → "int"
	/// </summary>
	public static string TypeName(VariableType type) => type == null ? "?" : type.Kind switch
	{
		VariableTypeKind.Int => "int",
		VariableTypeKind.Float => "float",
		VariableTypeKind.Bool => "bool",
		VariableTypeKind.String => "string",
		VariableTypeKind.Char => "char",
		VariableTypeKind.Struct => type.StructName ?? "struct",
		VariableTypeKind.Pointer => TypeName(type.ElementType) + "*",
		_ => type.Kind.ToString(),
	};

	/// <summary>
	/// 함수 블록에 보여 줄 함수 이름. 사전 정의 함수는 고정 ID "c.thrust"에서 점 뒤 이름만.
	/// 사용자 정의 함수는 GUID라서 BlockEditor가 이름을 찾아 준다.
	/// </summary>
	public static string FunctionName(BlockData block)
	{
		if (string.IsNullOrEmpty(block.FunctionId)) return "함수";
		if (block.IsUserFunction) return "함수";
		int dot = block.FunctionId.IndexOf('.');
		return dot >= 0 ? block.FunctionId.Substring(dot + 1) : block.FunctionId;
	}

	static SlotData Slot(BlockData block, int index) =>
		index < block.Slots.Count ? block.Slots[index] : null;

	// 값 칸 하나를 글자로. 비어 있으면 "  "로 빈칸을 보여 준다
	static string Value(SlotData slot)
	{
		if (slot == null) return "  ";
		if (slot.Block != null) return TextOf(slot.Block).TrimEnd(';');
		string text = BlockSlotText.ToText(slot);
		return text.Length == 0 ? "  " : text;
	}
}
