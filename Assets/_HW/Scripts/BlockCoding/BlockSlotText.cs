using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// 값 칸에 키보드로 적은 글자를 칸 데이터(SlotData)로 바꾸고, 칸 데이터를 다시 글자로 보여 준다.
/// <para>
/// 적은 글자를 보고 무엇인지 정한다.<br/>
/// - 3, -1 → int / 0.5 → float / true, false → bool / 'a' → char / "abc" → string<br/>
/// - x, hp 같은 이름 → 변수<br/>
/// - 비우면 빈 칸, 그 밖의 글자는 잘못된 값 (칸은 비워 둔다)
/// </para>
/// </summary>
public static class BlockSlotText
{
	static readonly Regex Identifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$");
	static readonly Regex FloatNumber = new Regex(@"^-?\d*\.\d+$");

	/// <summary>
	/// 변수 이름으로 쓸 수 있는 글자면 true. 예: x, hp, enemy_count
	/// </summary>
	public static bool IsIdentifier(string text) =>
		!string.IsNullOrEmpty(text) && Identifier.IsMatch(text) && text != "true" && text != "false";

	/// <summary>
	/// 값 칸에 적은 글자를 칸 데이터에 넣는다. 알 수 없는 글자면 칸을 비우고 false를 돌려준다.
	/// </summary>
	public static bool TryApply(SlotData slot, string text)
	{
		text = text?.Trim() ?? "";
		slot.Type = null;
		slot.Literal = null;
		slot.VariableName = null;

		if (text.Length == 0) return true;

		if (text == "true" || text == "false")
			return SetLiteral(slot, VariableTypeKind.Bool, text);

		if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int intValue))
			return SetLiteral(slot, VariableTypeKind.Int, intValue.ToString(CultureInfo.InvariantCulture));

		// 소수는 컴퓨터 설정과 상관없이 점으로 적는다 (function-data.md 「칸 고치기」)
		if (FloatNumber.IsMatch(text)
			&& float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float floatValue))
			return SetLiteral(slot, VariableTypeKind.Float, floatValue.ToString(CultureInfo.InvariantCulture));

		if (text.Length == 3 && text[0] == '\'' && text[2] == '\'')
			return SetLiteral(slot, VariableTypeKind.Char, text[1].ToString());

		if (text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"')
			return SetLiteral(slot, VariableTypeKind.String, text.Substring(1, text.Length - 2));

		if (IsIdentifier(text))
		{
			slot.VariableName = text;
			return true;
		}

		return false;
	}

	/// <summary>
	/// 칸 데이터를 값 칸에 보여 줄 글자로. 블록을 끼운 칸이나 빈 칸은 빈 글자.
	/// </summary>
	public static string ToText(SlotData slot)
	{
		if (slot == null || slot.Block != null) return "";
		if (!string.IsNullOrEmpty(slot.VariableName)) return slot.VariableName;
		if (slot.Literal == null) return "";

		return slot.Type?.Kind switch
		{
			VariableTypeKind.String => $"\"{slot.Literal}\"",
			VariableTypeKind.Char => $"'{slot.Literal}'",
			_ => slot.Literal,
		};
	}

	static bool SetLiteral(SlotData slot, VariableTypeKind kind, string literal)
	{
		slot.Type = new VariableType(kind);
		slot.Literal = literal;
		return true;
	}
}
