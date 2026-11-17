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
	// Python의 게임 객체(Orb)도 이 종류에 이름을 담는다 (#532, PythonObjects)
	Struct,

	// 아래는 Python 타입 (#532). 함수 데이터 JSON은 이름으로 저장하지만, 순서를 지키려고 맨 끝에 더한다
	// None 값 하나만 가진 타입
	None,
	// 어떤 값이든 받는 타입. 타입을 적지 않는 Python 함수의 파라미터·반환 값, 스킬의 「아무 값」 파라미터에 쓴다
	Any,
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

	/// <summary>
	/// 어떤 값이든 받는 타입 (Python, #532).
	/// </summary>
	public static VariableType AnyType => new VariableType(VariableTypeKind.Any);
}

/// <summary>
/// Python 캐릭터가 값으로 다루는 게임 객체의 타입 이름 (#532). heap에 올라가고, 변수·리스트에 담는다. malloc 블록으로는 만들지 않는다.
/// </summary>
// PredefinedStructs(C의 malloc 목록)와 나누는 이유: 그 목록은 C 편집 화면의 malloc 드롭다운에 그대로 나오기 때문
public static class PythonObjects
{
	// 마나 구슬. orb_create가 돌려준다
	public const string Orb = "Orb";

	/// <summary>
	/// 마나 구슬 타입. 예: orb_create의 반환 타입, orb_shot의 orb 파라미터
	/// </summary>
	public static VariableType OrbType => VariableType.StructOf(Orb);

	/// <summary>
	/// name이 Python 게임 객체의 타입 이름이면 true.
	/// </summary>
	public static bool IsKnown(string name) => name == Orb;
}
