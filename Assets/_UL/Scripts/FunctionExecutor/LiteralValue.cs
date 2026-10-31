using System.Globalization;

/// <summary>
/// 값 칸에 직접 입력한 값(예: 3, 0.5, true). 실행되면 그 틱 안에서 바로 값을 내놓는다.
/// <para>
/// 글자열 규칙 (docs/systems/function-data.md)<br/>
/// - int: "3", float: "0.5" (컴퓨터 설정과 상관없이 점), bool: "true" / "false", char: 글자 하나 "a", string: 그대로<br/>
/// - 바꿀 수 없는 글자열이면 실행될 때 InvalidLiteral로 실패한다.
/// </para>
/// </summary>
public class LiteralValue : ValueBlock
{
	// 바꾼 값과 바꾸기에 성공했는지. 만들 때 한 번만 바꿔, 같은 함수를 여러 번 시전해도 다시 바꾸지 않는다
	readonly RuntimeValue parsedValue;
	readonly bool isValid;

	/// <summary>
	/// type 타입의 글자열 literal로 만든다. 바꿀 수 없는 글자열이어도 만들어지고, 실행될 때 실패한다.
	/// </summary>
	// 만들 때 실패시키지 않는 이유: 함수 데이터를 바꾸는 때에는 아직 작업이 없어 실패를 기록할 곳이 없기 때문
	public LiteralValue(VariableType type, string literal)
	{
		isValid = TryParse(type, literal, out parsedValue);
	}

	// 키보드로 적은 값은 블록이 아니므로 틱을 쓰지 않는다
	internal override bool UsesTick => false;

	protected override BlockResult Step(FunctionTask task)
	{
		if (!isValid)
		{
			task.RecordFailure(FunctionFailure.InvalidLiteral);
			return BlockResult.Failed;
		}

		Result = parsedValue;
		return BlockResult.Done;
	}

	/// <summary>
	/// 글자열을 type 타입의 값으로 바꾼다. 바꿀 수 없으면 false를 돌려준다.
	/// </summary>
	internal static bool TryParse(VariableType type, string literal, out RuntimeValue value)
	{
		value = default;
		if (type == null || literal == null) return false;

		switch (type.Kind)
		{
			case VariableTypeKind.Int:
				if (!int.TryParse(literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i)) return false;
				value = RuntimeValue.FromInt(i);
				return true;

			case VariableTypeKind.Float:
				// "NaN", "Infinity"도 float로 바뀌지만, 플레이어가 입력할 수 있는 숫자가 아니므로 거부한다
				if (!float.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)
					|| float.IsNaN(f) || float.IsInfinity(f)) return false;
				value = RuntimeValue.FromFloat(f);
				return true;

			case VariableTypeKind.Bool:
				// 대소문자를 구분한다. 편집 화면이 "true" / "false"만 넣기 때문
				if (literal == "true") value = RuntimeValue.FromBool(true);
				else if (literal == "false") value = RuntimeValue.FromBool(false);
				else return false;
				return true;

			case VariableTypeKind.Char:
				if (literal.Length != 1) return false;
				value = RuntimeValue.FromChar(literal[0]);
				return true;

			case VariableTypeKind.String:
				value = RuntimeValue.FromString(literal);
				return true;
		}

		// 포인터 등은 직접 입력하는 값이 아니다
		return false;
	}
}
