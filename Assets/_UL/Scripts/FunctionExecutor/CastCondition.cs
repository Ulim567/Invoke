/// <summary>
/// 함수를 시전할 수 있는지 검사하는 조건 하나. 조건 하나가 클래스 하나다.
/// <para>
/// 사용법<br/>
/// - 이 클래스를 상속하고 Check를 덮어쓴다.<br/>
/// - 검사에 필요한 컴포넌트(메모리, 쿨타임 등)는 생성자로 받는다. 조건을 만드는 쪽(FunctionExecutor)이 넘겨준다.<br/>
/// - 실패하면 실패 종류를 failure에 넣고 false를 돌려준다. 실패했을 때 무엇을 할지는 조건이 아니라 언어 규칙 표가 정한다.
/// </para>
/// </summary>
public abstract class CastCondition
{
	/// <summary>
	/// function을 시전할 수 있으면 true. 시전할 수 없으면 false와 실패 종류를 돌려준다.
	/// </summary>
	public abstract bool Check(FunctionBlock function, out FunctionFailure failure);
}
