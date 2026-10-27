/// <summary>
/// 칸의 종류.
/// </summary>
public enum SlotKind
{
	// 직접 입력한 값, 변수, 끼운 블록 중 하나가 들어간다
	Value,
	// 변수만 들어간다. 예: 대입 블록의 왼쪽
	Variable,
	// 목록에서 고른 것이 들어간다. 연산자, if·else if·else, 타입 등
	Option,
	// 새 이름이 들어간다. 예: 선언 블록의 변수 이름
	Name,
}

/// <summary>
/// 블록의 칸 하나에 무엇이 들어 있는지 나타낸다.
/// <para>
/// 칸 종류마다 클래스를 나누지 않고 이 클래스 하나로 담는다. 칸에 든 것에 맞는 값만 채우고 나머지는 null로 둔다.<br/>
/// (클래스를 나누면 JSON에서 되돌릴 때 어느 클래스인지 고르는 변환 코드가 따로 필요하기 때문)
/// </para>
/// <para>
/// 칸에 든 것과 채우는 값<br/>
/// - 직접 입력한 값: Type, Literal (예: int, "3")<br/>
/// - 변수: VariableName (예: "x")<br/>
/// - 끼운 블록: Block<br/>
/// - 목록에서 고른 연산자 등: Option (예: "Add", "ElseIf". BlockOptions.cs의 enum 이름)<br/>
/// - 목록에서 고른 타입: Type (예: 선언 블록의 int)<br/>
/// - 새 이름: Name (예: "x")<br/>
/// - 비워 둔 값 칸: 모두 null
/// </para>
/// <para>
/// 사용법<br/>
/// - FromLiteral, FromVariable 등으로 만든다. 편집 화면은 플레이어가 칸을 바꾸면 값을 바로 고친다.
/// </para>
/// </summary>
public class SlotData
{
	public SlotKind Kind { get; set; }

	/// <summary>
	/// 직접 입력한 값의 타입, 또는 목록에서 고른 타입.
	/// </summary>
	public VariableType Type { get; set; }

	/// <summary>
	/// 직접 입력한 값을 글자열로 담는다. 예: "3", "0.5", "true"<br/>
	/// 실행할 때 Type에 맞는 값으로 바꾼다. 소수는 컴퓨터 설정과 상관없이 "0.5"처럼 점으로 적는다.
	/// </summary>
	public string Literal { get; set; }

	/// <summary>
	/// 넣은 변수의 이름.
	/// </summary>
	public string VariableName { get; set; }

	/// <summary>
	/// 값 칸에 끼운 블록. 연산, 비교, 논리, malloc, 값을 돌려주는 함수 블록.
	/// </summary>
	public BlockData Block { get; set; }

	/// <summary>
	/// 목록에서 고른 것. BlockOptions.cs의 enum 이름을 담는다. 예: "Add", "Equal", "And", "ElseIf"<br/>
	/// 화면에 보이는 글자("+", "else if")를 담지 않는 이유: 언어마다 보이는 모양이 다르기 때문
	/// </summary>
	public string Option { get; set; }

	/// <summary>
	/// 새로 지은 이름.
	/// </summary>
	public string Name { get; set; }

	public static SlotData FromLiteral(VariableType type, string literal) =>
		new SlotData { Kind = SlotKind.Value, Type = type, Literal = literal };

	public static SlotData FromVariable(string variableName) =>
		new SlotData { Kind = SlotKind.Value, VariableName = variableName };

	public static SlotData FromBlock(BlockData block) =>
		new SlotData { Kind = SlotKind.Value, Block = block };

	/// <summary>
	/// 비워 둔 값 칸. 예: 초기값을 넣지 않은 선언 블록의 초기값 칸
	/// </summary>
	public static SlotData EmptyValue() =>
		new SlotData { Kind = SlotKind.Value };

	/// <summary>
	/// 변수만 들어가는 칸. 예: 대입 블록의 왼쪽
	/// </summary>
	public static SlotData VariableOnly(string variableName) =>
		new SlotData { Kind = SlotKind.Variable, VariableName = variableName };

	public static SlotData FromOption(string option) =>
		new SlotData { Kind = SlotKind.Option, Option = option };

	public static SlotData FromTypeOption(VariableType type) =>
		new SlotData { Kind = SlotKind.Option, Type = type };

	public static SlotData FromName(string name) =>
		new SlotData { Kind = SlotKind.Name, Name = name };
}
