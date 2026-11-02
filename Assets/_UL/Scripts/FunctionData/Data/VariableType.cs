/// <summary>
/// 타입의 종류.
/// </summary>
public enum VariableTypeKind
{
	// 돌려주는 값이 없음. 반환 타입에만 쓴다
	Void,

	Int,
	Float,
	Bool,
	String,
	Char,

	// 게임 전용
	Position,
	Direction,
	// 대상 하나. 어떤 종류의 대상인지는 TargetKind에 담는다
	Target,
	Function,

	// 원소 타입(ElementType)이 필요한 타입
	Array,
	List,
	Pointer,

	// struct 이름(StructName)이 필요한 타입. 게임이 미리 정한 것과 플레이어가 만드는 것(Type 정의)을 함께 담는다
	Struct,
}

/// <summary>
/// Target 타입이 가리키는 대상의 종류.
/// </summary>
// 대상마다 VariableTypeKind 값을 따로 두지 않는다. 슬롯 입력 방식처럼 "대상인지"만 보는 코드가 종류가 늘어도 그대로 쓰이게 하기 위함
// 함수 데이터 JSON은 이름으로 저장하므로 중간에 값을 더해도 된다
public enum TargetKind
{
	// 몬스터 하나. 게임 컨셉에서 적은 버그이므로 플레이어에게 보이는 이름을 Bug로 한다
	Bug,
}

/// <summary>
/// 변수, 파라미터, 반환 타입의 타입 하나.
/// <para>
/// 사용법<br/>
/// - 기본 타입은 new VariableType(VariableTypeKind.Int)처럼 만든다.<br/>
/// - 배열, list, 포인터는 ArrayOf, ListOf, PointerTo로 원소 타입을 함께 넣어 만든다. 예: Sword* = PointerTo(StructOf("Sword"))<br/>
/// - struct는 StructOf로 이름을 넣어 만든다.<br/>
/// - 대상은 TargetOf로 종류를 넣어 만든다. 예: Bug = TargetOf(TargetKind.Bug). 종류 없이 만든 Target은 어떤 종류의 대상이든 받는다.
/// </para>
/// <para>
/// 언어마다 화면에 보이는 이름(C의 char[], Java의 boolean 등)은 이 클래스가 정하지 않는다. 편집 화면이 정한다.
/// </para>
/// </summary>
public class VariableType
{
	public VariableTypeKind Kind { get; set; }

	/// <summary>
	/// 배열, list, 포인터가 담거나 가리키는 타입. 다른 타입에서는 null.
	/// </summary>
	public VariableType ElementType { get; set; }

	/// <summary>
	/// struct의 이름. 예: Sword. struct가 아니면 null.
	/// </summary>
	public string StructName { get; set; }

	/// <summary>
	/// Target이 가리키는 대상의 종류. Target이 아니거나 종류를 정하지 않았으면 null.<br/>
	/// 종류를 정하지 않은 Target(파라미터, 변수)은 어떤 종류의 대상이든 받는다. 값은 언제나 자기 종류를 가진다.
	/// </summary>
	// null을 쓰는 이유: Target이 아닌 타입의 JSON에 이 칸이 적히지 않게 하고(NullValueHandling.Ignore),
	// 종류 없이 만든 박진우 코드의 Target 파라미터(DataDrivenSkill)가 Bug 값을 그대로 받게 하기 위함
	public TargetKind? TargetKind { get; set; }

	// JSON에서 되돌릴 때 Newtonsoft Json이 빈 객체를 먼저 만들고 칸을 채우므로 필요하다
	public VariableType() { }

	public VariableType(VariableTypeKind kind)
	{
		Kind = kind;
	}

	public static VariableType ArrayOf(VariableType elementType) =>
		new VariableType(VariableTypeKind.Array) { ElementType = elementType };

	public static VariableType ListOf(VariableType elementType) =>
		new VariableType(VariableTypeKind.List) { ElementType = elementType };

	public static VariableType PointerTo(VariableType elementType) =>
		new VariableType(VariableTypeKind.Pointer) { ElementType = elementType };

	public static VariableType StructOf(string structName) =>
		new VariableType(VariableTypeKind.Struct) { StructName = structName };

	public static VariableType TargetOf(TargetKind? kind) =>
		new VariableType(VariableTypeKind.Target) { TargetKind = kind };
}
