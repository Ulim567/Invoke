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
/// 변수, 파라미터, 반환 타입의 타입 하나.
/// <para>
/// 사용법<br/>
/// - 기본 타입은 new VariableType(VariableTypeKind.Int)처럼 만든다.<br/>
/// - 배열, list, 포인터는 ArrayOf, ListOf, PointerTo로 원소 타입을 함께 넣어 만든다. 예: Sword* = PointerTo(StructOf("Sword"))<br/>
/// - struct는 StructOf로 이름을 넣어 만든다.
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
}
