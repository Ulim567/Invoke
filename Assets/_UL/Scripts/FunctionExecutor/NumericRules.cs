/// <summary>
/// 연산 블록의 계산과, 선언·대입 때 값을 변수 타입으로 바꾸는 규칙.<br/>
/// 언어마다 다른 부분(타입을 섞을 때, float의 %)은 LanguageRule의 칸을 따른다.
/// <para>
/// 모든 언어에 같은 규칙<br/>
/// - 계산할 수 있는 타입: int, float, char, bool. string, 포인터 등은 TypeMismatch<br/>
/// - int / int는 정수 나눗셈(7 / 2 = 3), %의 부호는 왼쪽 값을 따름(-7 % 2 = -1). C99와 같다<br/>
/// - 0으로 나누기, 0으로 나머지: int, float 모두 DivideByZero<br/>
/// - int 범위를 넘으면 반대쪽 끝으로 돌아간다(int 최댓값 + 1 = int 최솟값)
/// </para>
/// </summary>
// 언어마다 다른 규칙을 이 파일 한 곳에 모은다. 다른 언어를 더할 때 고칠 곳을 줄이기 위함
internal static class NumericRules
{
	/// <summary>
	/// left op right를 계산한다. 계산할 수 없으면 실패 종류를 failure에 담고 false를 돌려준다.
	/// </summary>
	public static bool TryCalculate(ArithmeticOperator op, RuntimeValue left, RuntimeValue right, LanguageRule rule,
		out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.TypeMismatch;

		if (!TryGetOperandKind(left, rule, out VariableTypeKind leftKind)) return false;
		if (!TryGetOperandKind(right, rule, out VariableTypeKind rightKind)) return false;

		bool isFloat;
		if (leftKind == rightKind) isFloat = leftKind == VariableTypeKind.Float;
		// 여기까지 오면 한쪽은 int, 한쪽은 float이다
		else if (rule.NumericConversion == NumericConversion.Strict) return false;
		else isFloat = true;

		if (isFloat) return TryCalculateFloat(op, ToFloat(left), ToFloat(right), rule, out result, out failure);
		return TryCalculateInt(op, ToInt(left), ToInt(right), out result, out failure);
	}

	/// <summary>
	/// value를 target 타입의 변수에 넣을 값으로 바꾼다. 바꿀 수 없으면 false를 돌려준다(TypeMismatch).
	/// </summary>
	public static bool TryConvertForStore(RuntimeValue value, VariableType target, LanguageRule rule, out RuntimeValue converted)
	{
		converted = value;
		if (VariableStore.IsSameType(target, value.Type)) return true;
		if (target == null || value.Type == null) return false;

		VariableTypeKind from = value.Type.Kind;
		VariableTypeKind to = target.Kind;
		if (!IsNumeric(from) || !IsNumeric(to)) return false;

		switch (rule.NumericConversion)
		{
			case NumericConversion.Strict:
				return false;
			case NumericConversion.WideningOnly:
				bool widening = (from == VariableTypeKind.Int && to == VariableTypeKind.Float)
					|| (from == VariableTypeKind.Char && (to == VariableTypeKind.Int || to == VariableTypeKind.Float));
				if (!widening) return false;
				break;
		}

		switch (to)
		{
			case VariableTypeKind.Int:
				// C처럼 소수점 아래를 버린다(0.9 → 0, -1.7 → -1)
				converted = RuntimeValue.FromInt(from == VariableTypeKind.Float ? unchecked((int)value.AsFloat()) : ToInt(value));
				return true;
			case VariableTypeKind.Float:
				converted = RuntimeValue.FromFloat(ToFloat(value));
				return true;
			case VariableTypeKind.Char:
				int code = from == VariableTypeKind.Float ? unchecked((int)value.AsFloat()) : ToInt(value);
				converted = RuntimeValue.FromChar(unchecked((char)code));
				return true;
			case VariableTypeKind.Bool:
				// C처럼 0이 아니면 true
				converted = RuntimeValue.FromBool(from == VariableTypeKind.Float ? value.AsFloat() != 0f : ToInt(value) != 0);
				return true;
		}
		return false;
	}

	static bool IsNumeric(VariableTypeKind kind) =>
		kind == VariableTypeKind.Int || kind == VariableTypeKind.Float || kind == VariableTypeKind.Char || kind == VariableTypeKind.Bool;

	// 계산에 쓸 때의 타입. char, bool은 언어에 따라 int로 올리거나 거부한다
	static bool TryGetOperandKind(RuntimeValue value, LanguageRule rule, out VariableTypeKind kind)
	{
		kind = value.Type?.Kind ?? VariableTypeKind.Void;
		switch (kind)
		{
			case VariableTypeKind.Int:
			case VariableTypeKind.Float:
				return true;
			// C, Java, C# 모두 char 계산은 int로 한다('a' + 1 = 98). 같은 타입끼리만 계산하는 언어는 거부한다
			case VariableTypeKind.Char:
				if (rule.NumericConversion == NumericConversion.Strict) return false;
				kind = VariableTypeKind.Int;
				return true;
			// C는 bool도 int로 계산한다(true + 1 = 2). Java, C#은 bool 계산을 허용하지 않는다
			case VariableTypeKind.Bool:
				if (rule.NumericConversion != NumericConversion.ImplicitAll) return false;
				kind = VariableTypeKind.Int;
				return true;
		}
		return false;
	}

	static int ToInt(RuntimeValue value) => value.Type.Kind switch
	{
		VariableTypeKind.Char => value.AsChar(),
		VariableTypeKind.Bool => value.AsBool() ? 1 : 0,
		_ => value.AsInt(),
	};

	static float ToFloat(RuntimeValue value) => value.Type.Kind == VariableTypeKind.Float ? value.AsFloat() : ToInt(value);

	static bool TryCalculateInt(ArithmeticOperator op, int left, int right, out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.DivideByZero;
		if ((op == ArithmeticOperator.Divide || op == ArithmeticOperator.Modulo) && right == 0) return false;

		int value;
		switch (op)
		{
			case ArithmeticOperator.Add: value = unchecked(left + right); break;
			case ArithmeticOperator.Subtract: value = unchecked(left - right); break;
			case ArithmeticOperator.Multiply: value = unchecked(left * right); break;
			// int 최솟값 / -1은 C#에서 예외가 나므로 따로 계산한다. 결과가 int 범위를 넘어 반대쪽 끝으로 돌아간 값과 같다
			case ArithmeticOperator.Divide: value = right == -1 ? unchecked(-left) : left / right; break;
			case ArithmeticOperator.Modulo: value = right == -1 ? 0 : left % right; break;
			default:
				failure = FunctionFailure.TypeMismatch;
				return false;
		}

		result = RuntimeValue.FromInt(value);
		return true;
	}

	static bool TryCalculateFloat(ArithmeticOperator op, float left, float right, LanguageRule rule, out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.TypeMismatch;
		if (op == ArithmeticOperator.Modulo && !rule.AllowFloatModulo) return false;

		failure = FunctionFailure.DivideByZero;
		if ((op == ArithmeticOperator.Divide || op == ArithmeticOperator.Modulo) && right == 0f) return false;

		float value;
		switch (op)
		{
			case ArithmeticOperator.Add: value = left + right; break;
			case ArithmeticOperator.Subtract: value = left - right; break;
			case ArithmeticOperator.Multiply: value = left * right; break;
			case ArithmeticOperator.Divide: value = left / right; break;
			case ArithmeticOperator.Modulo: value = left % right; break;
			default:
				failure = FunctionFailure.TypeMismatch;
				return false;
		}

		result = RuntimeValue.FromFloat(value);
		return true;
	}
}
