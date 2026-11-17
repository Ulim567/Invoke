using System.Numerics;

/// <summary>
/// Python 캐릭터의 연산·비교·조건·값 넘기기 규칙 (#532). 언어 규칙 파일의 언어 방식이 Python일 때 NumericRules가 이 규칙으로 넘긴다.
/// <para>
/// - int는 넘치지 않는다(큰 정수). bool은 int처럼 계산한다(True + 1 = 2).<br/>
/// - /는 언제나 float, //와 %는 아래로 내림한다(-7 // 2 = -4, -7 % 2 = 1).<br/>
/// - 0으로 나누기(/, //, %): ZeroDivisionError(DivideByZero).<br/>
/// - 문자열은 문자열끼리 +만 된다.<br/>
/// - ==, !=는 어떤 두 값이든 비교한다(타입이 다르면 다름). 크기 비교는 숫자끼리, 문자열끼리만 된다.<br/>
/// - 조건: 0, 0.0, "", 빈 리스트, None은 거짓이고 나머지는 참이다.
/// </para>
/// </summary>
// NumericRules에 섞지 않고 따로 두는 이유: C 규칙(C99)을 한 줄도 바꾸지 않기 위함. C 캐릭터의 동작은 그대로여야 한다
internal static class PythonRules
{
	public static bool TryCalculate(ArithmeticOperator op, RuntimeValue left, RuntimeValue right, out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.TypeMismatch;

		if (left.Type?.Kind == VariableTypeKind.String && right.Type?.Kind == VariableTypeKind.String)
		{
			if (op != ArithmeticOperator.Add) return false;
			result = RuntimeValue.FromString(left.AsString() + right.AsString());
			return true;
		}

		if (!IsNumber(left) || !IsNumber(right)) return false;

		bool isFloat = left.Type.Kind == VariableTypeKind.Float || right.Type.Kind == VariableTypeKind.Float;
		// /는 int끼리도 float이다
		if (isFloat || op == ArithmeticOperator.Divide) return TryCalculateFloat(op, ToDouble(left), ToDouble(right), out result, out failure);
		return TryCalculateInteger(op, ToInteger(left), ToInteger(right), out result, out failure);
	}

	static bool TryCalculateInteger(ArithmeticOperator op, BigInteger left, BigInteger right, out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.DivideByZero;
		bool divides = op == ArithmeticOperator.FloorDivide || op == ArithmeticOperator.Modulo;
		if (divides && right.IsZero) return false;

		BigInteger value;
		switch (op)
		{
			case ArithmeticOperator.Add: value = left + right; break;
			case ArithmeticOperator.Subtract: value = left - right; break;
			case ArithmeticOperator.Multiply: value = left * right; break;
			case ArithmeticOperator.FloorDivide: value = FloorDivide(left, right); break;
			// 나머지의 부호는 오른쪽 값을 따른다(Python). left - (left // right) * right와 같다
			case ArithmeticOperator.Modulo: value = left - FloorDivide(left, right) * right; break;
			default:
				failure = FunctionFailure.TypeMismatch;
				return false;
		}

		result = RuntimeValue.FromInteger(value);
		return true;
	}

	// 아래로 내림하는 정수 나눗셈. BigInteger.Divide는 0 쪽으로 버리므로, 부호가 다르고 나누어떨어지지 않으면 1을 뺀다
	static BigInteger FloorDivide(BigInteger left, BigInteger right)
	{
		BigInteger quotient = BigInteger.DivRem(left, right, out BigInteger remainder);
		if (!remainder.IsZero && (remainder.Sign < 0) != (right.Sign < 0)) quotient -= 1;
		return quotient;
	}

	static bool TryCalculateFloat(ArithmeticOperator op, double left, double right, out RuntimeValue result, out FunctionFailure failure)
	{
		result = default;
		failure = FunctionFailure.DivideByZero;
		bool divides = op == ArithmeticOperator.Divide || op == ArithmeticOperator.FloorDivide || op == ArithmeticOperator.Modulo;
		if (divides && right == 0d) return false;

		double value;
		switch (op)
		{
			case ArithmeticOperator.Add: value = left + right; break;
			case ArithmeticOperator.Subtract: value = left - right; break;
			case ArithmeticOperator.Multiply: value = left * right; break;
			case ArithmeticOperator.Divide: value = left / right; break;
			case ArithmeticOperator.FloorDivide: value = System.Math.Floor(left / right); break;
			case ArithmeticOperator.Modulo: value = left - System.Math.Floor(left / right) * right; break;
			default:
				failure = FunctionFailure.TypeMismatch;
				return false;
		}

		result = RuntimeValue.FromFloat((float)value);
		return true;
	}

	/// <summary>
	/// left op right를 비교한다. 비교할 수 없으면(크기 비교에 숫자·문자열이 아닌 값) false(TypeError).
	/// </summary>
	public static bool TryCompare(CompareOperator op, RuntimeValue left, RuntimeValue right, out bool result)
	{
		result = false;
		bool isEquality = op == CompareOperator.Equal || op == CompareOperator.NotEqual;

		if (IsNumber(left) && IsNumber(right))
		{
			int order = CompareNumbers(left, right);
			result = op switch
			{
				CompareOperator.Greater => order > 0,
				CompareOperator.Less => order < 0,
				CompareOperator.Equal => order == 0,
				CompareOperator.NotEqual => order != 0,
				CompareOperator.GreaterOrEqual => order >= 0,
				CompareOperator.LessOrEqual => order <= 0,
				_ => false,
			};
			return true;
		}

		VariableTypeKind leftKind = left.Type?.Kind ?? VariableTypeKind.None;
		VariableTypeKind rightKind = right.Type?.Kind ?? VariableTypeKind.None;
		if (leftKind == VariableTypeKind.String && rightKind == VariableTypeKind.String)
		{
			int order = string.CompareOrdinal(left.AsString(), right.AsString());
			result = op switch
			{
				CompareOperator.Greater => order > 0,
				CompareOperator.Less => order < 0,
				CompareOperator.Equal => order == 0,
				CompareOperator.NotEqual => order != 0,
				CompareOperator.GreaterOrEqual => order >= 0,
				CompareOperator.LessOrEqual => order <= 0,
				_ => false,
			};
			return true;
		}

		// 크기 비교는 숫자끼리, 문자열끼리만 된다(Python의 TypeError)
		if (!isEquality) return false;

		bool same = AreSame(left, right);
		result = same == (op == CompareOperator.Equal);
		return true;
	}

	// ==의 뜻. 타입이 다르면 다르다. None은 None끼리만 같다. 리스트와 구슬은 같은 객체인지, 대상은 같은 몬스터인지 본다
	static bool AreSame(RuntimeValue left, RuntimeValue right)
	{
		VariableTypeKind kind = left.Type?.Kind ?? VariableTypeKind.None;
		if (kind != (right.Type?.Kind ?? VariableTypeKind.None)) return false;

		switch (kind)
		{
			case VariableTypeKind.None: return true;
			case VariableTypeKind.Target: return ReferenceEquals(left.TargetObject, right.TargetObject);
			case VariableTypeKind.List:
			case VariableTypeKind.Struct:
				return left.Type.StructName == right.Type.StructName && left.AsObjectPointer() == right.AsObjectPointer();
		}
		return false;
	}

	/// <summary>
	/// 조건으로 쓴 값의 참·거짓(Python의 truthiness). heap은 리스트의 길이를 읽을 때 쓴다.
	/// </summary>
	// 언제나 true를 돌려준다. Python은 어떤 값이든 조건으로 쓸 수 있기 때문
	public static bool TryGetTruth(RuntimeValue value, HeapStorage heap, out bool condition)
	{
		switch (value.Type?.Kind)
		{
			case VariableTypeKind.Bool: condition = value.AsBool(); break;
			case VariableTypeKind.Int: condition = !value.AsInteger().IsZero; break;
			case VariableTypeKind.Float: condition = value.AsFloat() != 0f; break;
			case VariableTypeKind.String: condition = value.AsString().Length > 0; break;
			case VariableTypeKind.None: condition = false; break;
			case VariableTypeKind.List:
				condition = heap != null && heap.TryGet(value.AsObjectPointer(), out ListStorage list) && list.Count > 0;
				break;
			// 구슬, 대상 등 객체는 언제나 참이다
			default: condition = value.Type != null; break;
		}
		return true;
	}

	/// <summary>
	/// value를 target 타입의 파라미터(스킬, 사용자 정의 함수)·변수에 넘길 값으로 바꾼다. 바꿀 수 없으면 false(TypeError).<br/>
	/// Python 값은 바꾸지 않고 그대로 넘기는 것이 원칙이고, 스킬이 float을 받는 자리의 int·bool, int를 받는 자리의 bool만 바꾼다.
	/// </summary>
	public static bool TryConvertForStore(RuntimeValue value, VariableType target, out RuntimeValue converted)
	{
		converted = value;
		if (target == null || value.Type == null) return false;
		if (target.Kind == VariableTypeKind.Any) return true;

		VariableTypeKind from = value.Type.Kind;
		switch (target.Kind)
		{
			// int 범위를 넘는 큰 정수는 스킬의 int 파라미터에 넣을 수 없다(구현 기본값, 11/18)
			case VariableTypeKind.Int when from == VariableTypeKind.Int: return !value.IsBigInteger;
			case VariableTypeKind.Int when from == VariableTypeKind.Bool:
				converted = RuntimeValue.FromInt(value.AsBool() ? 1 : 0);
				return true;
			case VariableTypeKind.Float when from == VariableTypeKind.Int || from == VariableTypeKind.Bool:
				converted = RuntimeValue.FromFloat((float)ToDouble(value));
				return true;
			// 리스트 파라미터(구슬 리스트, 대상 리스트)는 어떤 리스트든 받는다. 원소는 스킬이 확인한다
			// 대상 array도 받는다. 슬롯에서 여러 명을 골라 바로 시전하면 대상 array(RuntimeValue.FromBugs)가 오기 때문
			case VariableTypeKind.List: return from == VariableTypeKind.List || from == VariableTypeKind.Array;
		}
		return VariableStore.IsSameType(target, value.Type);
	}

	/// <summary>
	/// value가 int(bool 포함)이면 큰 정수로 꺼낸다. range의 start·end, 리스트 index가 쓴다.
	/// </summary>
	public static bool TryGetInteger(RuntimeValue value, out BigInteger number)
	{
		number = BigInteger.Zero;
		switch (value.Type?.Kind)
		{
			case VariableTypeKind.Int: number = value.AsInteger(); return true;
			case VariableTypeKind.Bool: number = value.AsBool() ? BigInteger.One : BigInteger.Zero; return true;
		}
		return false;
	}

	static bool IsNumber(RuntimeValue value) =>
		value.Type?.Kind == VariableTypeKind.Int || value.Type?.Kind == VariableTypeKind.Float || value.Type?.Kind == VariableTypeKind.Bool;

	static BigInteger ToInteger(RuntimeValue value) =>
		value.Type.Kind == VariableTypeKind.Bool ? (value.AsBool() ? BigInteger.One : BigInteger.Zero) : value.AsInteger();

	static double ToDouble(RuntimeValue value) => value.Type.Kind == VariableTypeKind.Float ? value.AsFloat() : (double)ToInteger(value);

	// 숫자 두 개의 크기 순서. 둘 다 정수이면 큰 정수로, 하나라도 float이면 double로 비교한다
	static int CompareNumbers(RuntimeValue left, RuntimeValue right)
	{
		bool isFloat = left.Type.Kind == VariableTypeKind.Float || right.Type.Kind == VariableTypeKind.Float;
		return isFloat ? ToDouble(left).CompareTo(ToDouble(right)) : ToInteger(left).CompareTo(ToInteger(right));
	}
}
