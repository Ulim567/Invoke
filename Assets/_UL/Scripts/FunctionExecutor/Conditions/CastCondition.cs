/// <summary>
/// 시전 전 검사에 걸린 이유. 실행기가 작업을 만들지 않고 CastRejected로 알린다.
/// </summary>
// FunctionFailure와 따로 두는 이유: 플레이어 코드의 잘못이 아니라 게임 상황(기다리면 쓸 수 있음)이므로, 언어 규칙 표를 거치지 않는다
public enum CastRejection
{
	// 같은 함수가 아직 실행 중이거나 쿨타임 중이다
	OnCooldown,
	// 남은 메모리가 그 함수의 프레임 크기보다 적다
	NotEnoughMemory,
}

/// <summary>
/// 함수를 시전할 수 있는지 검사하는 조건 하나. 조건 하나가 클래스 하나다.
/// <para>
/// 사용법<br/>
/// - 이 클래스를 상속하고 Check를 덮어쓴다.<br/>
/// - 검사에 필요한 컴포넌트(메모리, 쿨타임 등)는 생성자로 받는다. 조건을 만드는 쪽(FunctionExecutor)이 넘겨준다.<br/>
/// - 걸리면 이유를 rejection에 넣고 false를 돌려준다. 실행기가 작업을 만들지 않고 CastRejected로 알린다.
/// </para>
/// </summary>
public abstract class CastCondition
{
	/// <summary>
	/// function을 시전할 수 있으면 true. 시전할 수 없으면 false와 거부 이유를 돌려준다.
	/// </summary>
	public abstract bool Check(FunctionBlock function, out CastRejection rejection);
}
