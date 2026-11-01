using System;
using System.Globalization;

/// <summary>
/// 블록을 실행하는 동안 만들어지는 값 하나. 예: 3, 0.5, true, 포인터 1
/// <para>
/// 사용법<br/>
/// - FromInt, FromFloat, FromBool, FromString, FromChar, FromPointer, FromBug로 만들고, AsInt 등으로 꺼낸다.<br/>
/// - 담은 타입과 다른 As 함수를 부르면 예외가 난다. 타입은 Type으로 먼저 확인한다.<br/>
/// - 한 번 만든 값은 바꿀 수 없다. 변수의 값을 바꿀 때는 새 값을 만들어 VariableStore에 쓴다.
/// </para>
/// <para>
/// 지금 다루는 타입: int, float, bool, string, char, 포인터, 대상(Target. 종류는 Bug만). 위치, 배열 같은 타입은 해당 이슈에서 추가한다.
/// </para>
/// </summary>
// 바꿀 수 없는 값으로 둔다. C에서 y = x 뒤에 x를 바꿔도 y는 그대로이듯, 두 변수가 같은 값을 받아도 서로 영향을 주지 않게 하기 위함
public readonly struct RuntimeValue
{
	// 실제 값. 타입마다 필드를 따로 두지 않고 object 하나에 담는다. 타입이 늘어도 필드를 더하지 않기 위함
	// 포인터는 HeapStorage가 돌려준 int 포인터 값을 담는다
	readonly object value;

	/// <summary>
	/// 이 값의 타입. 포인터는 가리키는 타입까지 담는다(예: Sword*).
	/// </summary>
	public VariableType Type { get; }

	RuntimeValue(VariableType type, object value)
	{
		Type = type;
		this.value = value;
	}

	public static RuntimeValue FromInt(int value) => new RuntimeValue(new VariableType(VariableTypeKind.Int), value);

	public static RuntimeValue FromFloat(float value) => new RuntimeValue(new VariableType(VariableTypeKind.Float), value);

	public static RuntimeValue FromBool(bool value) => new RuntimeValue(new VariableType(VariableTypeKind.Bool), value);

	public static RuntimeValue FromString(string value)
	{
		// null 글자열은 받지 않는다. 지금 다루는 언어(C)에는 비어 있는 글자열만 있고 null 글자열은 없기 때문
		if (value == null) throw new ArgumentNullException(nameof(value));
		return new RuntimeValue(new VariableType(VariableTypeKind.String), value);
	}

	public static RuntimeValue FromChar(char value) => new RuntimeValue(new VariableType(VariableTypeKind.Char), value);

	/// <summary>
	/// 포인터 값. pointerType은 포인터 타입 자체다(예: PointerTo(StructOf("Sword"))). 실패한 malloc은 HeapStorage.NullPointer를 넣는다.
	/// </summary>
	public static RuntimeValue FromPointer(VariableType pointerType, int pointer)
	{
		if (pointerType == null) throw new ArgumentNullException(nameof(pointerType));
		if (pointerType.Kind != VariableTypeKind.Pointer)
			throw new ArgumentException($"포인터 타입이 아닙니다: {pointerType.Kind}", nameof(pointerType));
		return new RuntimeValue(pointerType, pointer);
	}

	/// <summary>
	/// 직접 입력한 NULL의 값. 타입은 가리키는 타입이 없는 포인터(PointerTo(null))이고, 값은 HeapStorage.NullPointer다.<br/>
	/// 비교 블록은 어떤 포인터와도 비교하고, 선언·대입 블록은 어떤 포인터 변수에도 넣는다.
	/// </summary>
	public static RuntimeValue Null => FromPointer(VariableType.PointerTo(null), HeapStorage.NullPointer);

	/// <summary>
	/// Bug(몬스터 하나) 대상 값. 타입은 TargetOf(TargetKind.Bug)다. null이면 「대상 없음」이다.
	/// </summary>
	public static RuntimeValue FromBug(Monster bug) => new RuntimeValue(VariableType.TargetOf(TargetKind.Bug), bug);

	/// <summary>
	/// targetType(Target 타입) 변수의 「대상 없음」 값. 초기값을 비운 Target 변수에 쓴다.
	/// </summary>
	public static RuntimeValue NoTarget(VariableType targetType)
	{
		if (targetType == null) throw new ArgumentNullException(nameof(targetType));
		if (targetType.Kind != VariableTypeKind.Target)
			throw new ArgumentException($"Target 타입이 아닙니다: {targetType.Kind}", nameof(targetType));
		return new RuntimeValue(targetType, null);
	}

	/// <summary>
	/// 「대상 없음」이면 true. 죽었거나 오브젝트가 없어진 대상은 「대상 없음」이 아니다.
	/// </summary>
	// C의 해제한 포인터가 NULL이 아닌 것과 같다. 그래서 if (b != NULL)을 지나도, 스킬에 넘기는 순간 InvalidTarget이 난다
	// Unity의 == 대신 object로 비교한다. Unity의 ==는 없어진 오브젝트도 null로 보기 때문
	public bool IsNoTarget => Type?.Kind == VariableTypeKind.Target && (object)value == null;

	/// <summary>
	/// 스킬에 대상으로 넘길 수 있는 대상이면 true. 「대상 없음」, 죽은 대상, 오브젝트가 없어진 대상은 false다.
	/// </summary>
	// 죽은 순간부터 쓸 수 없다고 본다. 사망 모션 동안(Despawn Delay) 오브젝트가 남아 있어도, 결과가 그 Inspector 값에 따라 달라지지 않게 하기 위함
	// 대상 종류가 늘면(Player 등) 그 종류의 검사를 여기에 더한다
	public bool IsUsableTarget
	{
		get
		{
			if (Type?.Kind != VariableTypeKind.Target) return false;

			switch (Type.TargetKind)
			{
				case TargetKind.Bug:
					Monster bug = AsBug();
					return bug != null && !bug.IsDead;
			}
			return false;
		}
	}

	/// <summary>
	/// 직접 입력한 NULL의 타입(가리키는 타입이 없는 포인터)이면 true.
	/// </summary>
	internal static bool IsNullType(VariableType type) => type?.Kind == VariableTypeKind.Pointer && type.ElementType == null;

	/// <summary>
	/// 지금 실행기가 값으로 다룰 수 있는 타입이면 true. int, float, bool, string, char, 미리 정한 struct의 포인터(예: Sword*), 대상(Target).
	/// </summary>
	// 함수 데이터를 바꿀 때 아직 다루지 않는 타입(위치, 배열, 정의되지 않은 struct 등)의 변수를 미리 거르기 위함
	internal static bool IsSupportedType(VariableType type)
	{
		if (type == null) return false;

		return type.Kind switch
		{
			VariableTypeKind.Int or VariableTypeKind.Float or VariableTypeKind.Bool
				or VariableTypeKind.String or VariableTypeKind.Char or VariableTypeKind.Target => true,
			// 지금 포인터를 만드는 곳은 malloc뿐이고, malloc은 미리 정한 struct만 만든다
			VariableTypeKind.Pointer => type.ElementType?.Kind == VariableTypeKind.Struct
				&& PredefinedStructs.TryGet(type.ElementType.StructName, out _),
			_ => false,
		};
	}

	// 담은 타입과 다른 함수를 부르면 InvalidCastException이 난다. 실행기 코드의 실수이므로 바로 드러나게 한다
	public int AsInt() => (int)value;
	public float AsFloat() => (float)value;
	public bool AsBool() => (bool)value;
	public string AsString() => (string)value;
	public char AsChar() => (char)value;
	public int AsPointer() => (int)value;
	// 종류가 Bug인 대상의 몬스터. 「대상 없음」이면 null이다
	public Monster AsBug() => (Monster)value;

	// 종류와 상관없이 대상 오브젝트를 꺼낸다. 대상끼리 비교하거나 로그에 이름을 적을 때 쓴다. 「대상 없음」이면 null이다
	internal UnityEngine.Object TargetObject => (UnityEngine.Object)value;

	/// <summary>
	/// 로그에 보여 줄 글자. 소수는 컴퓨터 설정과 상관없이 점으로 적는다.
	/// </summary>
	public override string ToString()
	{
		if (Type == null) return "(값 없음)";

		return Type.Kind switch
		{
			VariableTypeKind.Float => AsFloat().ToString(CultureInfo.InvariantCulture),
			VariableTypeKind.Bool => AsBool() ? "true" : "false",
			// 글자 번호 0은 화면에 보이지 않으므로 C 표기로 적는다
			VariableTypeKind.Char => AsChar() == '\0' ? "'\\0'" : $"'{AsChar()}'",
			VariableTypeKind.String => $"\"{AsString()}\"",
			VariableTypeKind.Pointer => $"포인터 {AsPointer()}",
			// 없어진 오브젝트의 name을 읽으면 예외가 나므로 먼저 거른다
			VariableTypeKind.Target => IsNoTarget ? "(대상 없음)" : TargetObject == null ? "(없어진 대상)" : TargetObject.name,
			_ => value.ToString(),
		};
	}
}
