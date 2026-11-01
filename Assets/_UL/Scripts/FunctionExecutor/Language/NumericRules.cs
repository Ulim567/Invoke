/// <summary>
/// 연산 블록의 계산, 비교 블록의 비교, 선언·대입 때 값을 변수 타입으로 바꾸는 규칙, 값을 조건(참·거짓)으로 바꾸는 규칙.<br/>
/// 언어마다 다른 부분(타입을 섞을 때, float의 %)은 LanguageRuleData의 칸을 따른다.
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
	public static bool TryCalculate(ArithmeticOperator op, RuntimeValue left, RuntimeValue right, LanguageRuleData rule,
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
	/// value slot의 값을 target 타입의 변수·파라미터에 넣을 값으로 바꾼다. 바꿀 수 없으면 false를 돌려준다(TypeMismatch).<br/>
	/// source는 그 값을 내놓은 value slot의 블록(원본)이다. target이 포인터이면 직접 입력한 0을 언어 규칙에 따라 NULL로 본다(C의 Sword* p = 0).
	/// </summary>
	public static bool TryConvertForStore(ValueBlock source, RuntimeValue value, VariableType target, LanguageRuleData rule, out RuntimeValue converted)
	{
		if (target?.Kind == VariableTypeKind.Pointer) value = NullIfZeroLiteral(source, value, rule);
		return TryConvertForStore(value, target, rule, out converted);
	}

	/// <summary>
	/// value를 target 타입의 변수에 넣을 값으로 바꾼다. 바꿀 수 없으면 false를 돌려준다(TypeMismatch).<br/>
	/// value slot에서 나오지 않은 값(스킬의 반환 값 등)에 쓴다. value slot의 값은 source를 받는 함수를 쓴다.
	/// </summary>
	public static bool TryConvertForStore(RuntimeValue value, VariableType target, LanguageRuleData rule, out RuntimeValue converted)
	{
		converted = value;
		if (VariableStore.IsSameType(target, value.Type)) return true;
		if (target == null || value.Type == null) return false;

		// NULL은 어떤 포인터 변수에도 넣는다. 넣은 뒤에는 그 변수의 포인터 타입을 가진 null이 된다
		if (RuntimeValue.IsNullType(value.Type))
		{
			if (target.Kind != VariableTypeKind.Pointer) return false;
			converted = RuntimeValue.FromPointer(target, HeapStorage.NullPointer);
			return true;
		}

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

	/// <summary>
	/// value slot 두 개의 값을 비교해 결과를 result에 담는다. 비교할 수 없는 타입이면 false를 돌려준다(TypeMismatch).<br/>
	/// leftSource, rightSource는 그 값을 내놓은 value slot의 블록(원본)이다. 한쪽이 포인터이면 다른 쪽의 직접 입력한 0을 언어 규칙에 따라 NULL로 본다(C의 p == 0).
	/// </summary>
	// 포인터와 비교할 때만 바꾼다. 숫자끼리 비교하는 0(x == 0)은 그대로 0이어야 하기 때문
	public static bool TryCompare(CompareOperator op, ValueBlock leftSource, RuntimeValue left, ValueBlock rightSource, RuntimeValue right,
		LanguageRuleData rule, out bool result)
	{
		if (right.Type?.Kind == VariableTypeKind.Pointer) left = NullIfZeroLiteral(leftSource, left, rule);
		if (left.Type?.Kind == VariableTypeKind.Pointer) right = NullIfZeroLiteral(rightSource, right, rule);
		return TryCompare(op, left, right, rule, out result);
	}

	/// <summary>
	/// left op right를 비교해 결과를 result에 담는다. 비교할 수 없는 타입이면 false를 돌려준다(TypeMismatch).
	/// </summary>
	// 타입이 섞이면 TryCalculate와 같은 규칙으로 맞춘 뒤 비교한다. C의 3 == 3.0처럼, 계산할 때와 같은 값으로 비교해야 하기 때문
	public static bool TryCompare(CompareOperator op, RuntimeValue left, RuntimeValue right, LanguageRuleData rule, out bool result)
	{
		result = false;
		VariableTypeKind leftKind = left.Type?.Kind ?? VariableTypeKind.Void;
		VariableTypeKind rightKind = right.Type?.Kind ?? VariableTypeKind.Void;
		bool isEquality = op == CompareOperator.Equal || op == CompareOperator.NotEqual;

		// 대상(Target)은 ==, !=만 된다. 대상끼리는 같은 대상인지, NULL과는 「대상 없음」인지 비교한다
		// 포인터보다 먼저 본다. NULL은 포인터 타입이라, 아래 포인터 규칙이 먼저 받으면 대상과 NULL의 비교를 거부하기 때문
		// 죽었거나 없어진 대상은 NULL과 같지 않다(RuntimeValue.IsNoTarget). 직접 입력한 0은 NULL로 보지 않는다. zeroIsNull은 포인터 자리의 규칙이기 때문
		// 종류가 다른 대상끼리(예: Bug와 Player)는 다른 타입의 포인터끼리처럼 비교하지 않는다
		if (leftKind == VariableTypeKind.Target || rightKind == VariableTypeKind.Target)
		{
			if (!isEquality) return false;

			bool same;
			if (leftKind == rightKind)
			{
				if (!VariableStore.IsSameType(left.Type, right.Type)) return false;
				same = ReferenceEquals(left.TargetObject, right.TargetObject);
			}
			else if (RuntimeValue.IsNullType(left.Type)) same = right.IsNoTarget;
			else if (RuntimeValue.IsNullType(right.Type)) same = left.IsNoTarget;
			else return false;

			result = same == (op == CompareOperator.Equal);
			return true;
		}

		// 포인터는 ==, !=만 된다. 같은 타입의 포인터끼리, 또는 한쪽이 NULL이면 주소를 비교한다
		// 크기 비교를 막는 이유: C에서도 포인터의 크기 비교는 같은 배열 안의 포인터끼리만 뜻이 있는데, 지금 heap에는 배열이 없기 때문
		if (leftKind == VariableTypeKind.Pointer || rightKind == VariableTypeKind.Pointer)
		{
			if (leftKind != rightKind || !isEquality) return false;
			bool comparable = RuntimeValue.IsNullType(left.Type) || RuntimeValue.IsNullType(right.Type) || VariableStore.IsSameType(left.Type, right.Type);
			if (!comparable) return false;

			result = (left.AsPointer() == right.AsPointer()) == (op == CompareOperator.Equal);
			return true;
		}

		// string은 언어 규칙이 ByContent일 때만 ==, !=로 글자 내용을 비교한다. 다른 타입과 섞이면 아래 숫자 규칙에서 TypeMismatch가 된다
		if (leftKind == VariableTypeKind.String && rightKind == VariableTypeKind.String)
		{
			if (rule.StringEquality != StringEquality.ByContent || !isEquality) return false;

			result = (left.AsString() == right.AsString()) == (op == CompareOperator.Equal);
			return true;
		}

		// 같은 타입끼리는 언어와 상관없이 비교한다. char, bool 계산을 막는 언어도 'a' == 'b', true == false는 쓸 수 있기 때문
		// bool의 크기 비교(true > false)만은 bool을 int로 올리는 언어(C)에서만 된다
		if (leftKind == rightKind && IsNumeric(leftKind))
		{
			if (leftKind == VariableTypeKind.Bool && !isEquality && rule.NumericConversion != NumericConversion.ImplicitAll) return false;

			result = leftKind == VariableTypeKind.Float ? Compare(op, left.AsFloat(), right.AsFloat()) : Compare(op, ToInt(left), ToInt(right));
			return true;
		}

		if (!TryGetOperandKind(left, rule, out VariableTypeKind leftOperand)) return false;
		if (!TryGetOperandKind(right, rule, out VariableTypeKind rightOperand)) return false;
		// 여기까지 와서 타입이 다르면 한쪽은 int, 한쪽은 float이다
		if (leftOperand != rightOperand && rule.NumericConversion == NumericConversion.Strict) return false;

		// int끼리는 float으로 바꾸지 않고 비교한다. 큰 int는 float으로 바꾸면 값이 달라지기 때문(16777217 → 16777216)
		bool isFloat = leftOperand == VariableTypeKind.Float || rightOperand == VariableTypeKind.Float;
		result = isFloat ? Compare(op, ToFloat(left), ToFloat(right)) : Compare(op, ToInt(left), ToInt(right));
		return true;
	}

	/// <summary>
	/// source가 직접 입력한 0이고 언어 규칙이 ZeroIsNull이면 NULL(RuntimeValue.Null)을, 아니면 value를 그대로 돌려준다.<br/>
	/// 포인터 자리에서만 부른다. 숫자 자리의 0은 그대로 0이어야 하기 때문<br/>
	/// 비교와 변수에 넣기는 source를 받는 TryCompare, TryConvertForStore가 이 함수를 부른다. 블록은 free처럼 언제나 포인터 자리인 곳에서만 직접 부른다.
	/// </summary>
	public static RuntimeValue NullIfZeroLiteral(ValueBlock source, RuntimeValue value, LanguageRuleData rule) =>
		rule.ZeroIsNull && source != null && source.IsZeroLiteral ? RuntimeValue.Null : value;

	/// <summary>
	/// value를 조건(참·거짓)으로 바꾼다. 논리 블록, 조건문, while이 쓴다. 바꿀 수 없으면 false를 돌려준다(TypeMismatch).<br/>
	/// - bool: 모든 언어에서 그대로<br/>
	/// - ImplicitAll(C): 숫자는 0이 아니면 참, 포인터는 NULL이 아니면 참<br/>
	/// - 다른 규칙: bool만
	/// </summary>
	// bool이 아닌 값을 조건으로 쓰는 것은 bool을 숫자로 다루는 언어(ImplicitAll)만 허용한다. Java, C#은 if (5), if (p)를 허용하지 않기 때문
	public static bool TryConvertToCondition(RuntimeValue value, LanguageRuleData rule, out bool condition)
	{
		condition = false;
		VariableTypeKind kind = value.Type?.Kind ?? VariableTypeKind.Void;
		if (kind == VariableTypeKind.Bool)
		{
			condition = value.AsBool();
			return true;
		}
		if (rule.NumericConversion != NumericConversion.ImplicitAll) return false;

		switch (kind)
		{
			case VariableTypeKind.Int:
			case VariableTypeKind.Char:
				condition = ToInt(value) != 0;
				return true;
			case VariableTypeKind.Float:
				condition = value.AsFloat() != 0f;
				return true;
			case VariableTypeKind.Pointer:
				condition = value.AsPointer() != HeapStorage.NullPointer;
				return true;
		}
		return false;
	}

	static bool IsNumeric(VariableTypeKind kind) =>
		kind == VariableTypeKind.Int || kind == VariableTypeKind.Float || kind == VariableTypeKind.Char || kind == VariableTypeKind.Bool;

	// 계산에 쓸 때의 타입. char, bool은 언어에 따라 int로 올리거나 거부한다
	static bool TryGetOperandKind(RuntimeValue value, LanguageRuleData rule, out VariableTypeKind kind)
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

	static bool TryCalculateFloat(ArithmeticOperator op, float left, float right, LanguageRuleData rule, out RuntimeValue result, out FunctionFailure failure)
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

	// 연산자는 FunctionDataConverter가 정의된 값인지 미리 확인하므로 default에 오지 않는다
	static bool Compare(CompareOperator op, int left, int right) => op switch
	{
		CompareOperator.Greater => left > right,
		CompareOperator.Less => left < right,
		CompareOperator.Equal => left == right,
		CompareOperator.NotEqual => left != right,
		CompareOperator.GreaterOrEqual => left >= right,
		CompareOperator.LessOrEqual => left <= right,
		_ => throw new System.ArgumentOutOfRangeException(nameof(op)),
	};

	static bool Compare(CompareOperator op, float left, float right) => op switch
	{
		CompareOperator.Greater => left > right,
		CompareOperator.Less => left < right,
		CompareOperator.Equal => left == right,
		CompareOperator.NotEqual => left != right,
		CompareOperator.GreaterOrEqual => left >= right,
		CompareOperator.LessOrEqual => left <= right,
		_ => throw new System.ArgumentOutOfRangeException(nameof(op)),
	};
}
