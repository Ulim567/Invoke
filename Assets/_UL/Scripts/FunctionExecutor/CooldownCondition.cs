/// <summary>
/// 시전할 함수가 실행 중이거나 쿨타임 중이 아닌지 검사한다.
/// <para>
/// 쿨타임은 함수가 끝난 뒤부터 흐르므로, 실행 중인 동안에는 아직 쿨타임이 아니다.<br/>
/// 그래서 실행 중인 함수도 함께 막는다. 막지 않으면 실행 중에 같은 슬롯을 계속 눌러 여러 번 겹쳐 쓸 수 있기 때문이다.
/// </para>
/// </summary>
public class CooldownCondition : CastCondition
{
	readonly FunctionExecutor executor;

	public CooldownCondition(FunctionExecutor executor)
	{
		this.executor = executor;
	}

	public override bool Check(FunctionBlock function, out FunctionFailure failure)
	{
		failure = FunctionFailure.OnCooldown;
		return !executor.IsRunning(function) && executor.GetRemainingCooldownTicks(function) <= 0;
	}
}
