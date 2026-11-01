/// <summary>
/// 시전할 함수가 실행 중이거나 쿨타임 중이 아닌지 검사한다.
/// <para>
/// - 사용자 정의 함수: 자기 쿨타임이 없으므로, 같은 함수가 실행 중일 때만 막는다.<br/>
/// - 스킬: 같은 스킬(고정 ID)이 어디서든 실행 중이거나 쿨타임 중이면 막는다.<br/>
/// 쿨타임은 스킬이 끝난 뒤부터 흐르므로, 실행 중인 동안에는 아직 쿨타임이 아니다.
/// 그래서 실행 중인 것도 함께 막는다. 막지 않으면 실행 중에 같은 슬롯을 계속 눌러 여러 번 겹쳐 쓸 수 있기 때문이다.
/// </para>
/// </summary>
public class CooldownCondition : CastCondition
{
	readonly CooldownTracker cooldowns;

	public CooldownCondition(CooldownTracker cooldowns)
	{
		this.cooldowns = cooldowns;
	}

	public override bool Check(FunctionBlock function, out CastRejection rejection)
	{
		rejection = CastRejection.OnCooldown;
		return !cooldowns.IsRunning(function) && cooldowns.GetRemainingTicks(function) <= 0;
	}
}
