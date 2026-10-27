// 드롭다운 칸에서 고르는 선택지들.
// 함수 데이터에는 화면에 보이는 글자("+", "else if", "&&")가 아니라 enum 이름("Add", "ElseIf", "And")을 저장한다.
// 같은 선택지라도 언어마다 화면에 보이는 모양이 다르기 때문이다. 예: else if는 Python에서 elif
// 언어마다 어떤 글자로 보여 줄지는 편집 화면이 정한다.

/// <summary>
/// 조건문 블록의 갈래.
/// </summary>
public enum ConditionalBranch
{
	If,
	ElseIf,
	Else,
}

/// <summary>
/// 연산 블록의 연산자. 차례대로 +, -, *, /, %
/// </summary>
public enum ArithmeticOperator
{
	Add,
	Subtract,
	Multiply,
	Divide,
	Modulo,
}

/// <summary>
/// 비교 블록의 연산자. 차례대로 &gt;, &lt;, ==, !=, &gt;=, &lt;=
/// </summary>
public enum CompareOperator
{
	Greater,
	Less,
	Equal,
	NotEqual,
	GreaterOrEqual,
	LessOrEqual,
}

/// <summary>
/// 논리 블록(Not 제외)의 연산자.
/// </summary>
public enum LogicOperator
{
	And,
	Or,
}
