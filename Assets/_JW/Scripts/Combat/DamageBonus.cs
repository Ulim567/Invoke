/// <summary>
/// 조건 배율의 추가분 값 모음 (노션 「C 캐릭터 - 1차」 6. 배율).<br/>
/// 값은 "+몇 %"를 소수로 적은 것이다. 스킬이 조건을 확인한 뒤 DamageRequest.BonusRate에 더해 넣는다.
/// <para>
/// 중첩: 추가분끼리 더한다. 예: 표식 + 집중찌르기 = 1 + Mark + BleedFocusedThrust = 2.5배
/// </para>
/// </summary>
public static class DamageBonus
{
	/// <summary>표식 상태: 모든 스킬 +100%</summary>
	public const float Mark = 1.0f;

	/// <summary>출혈 상태: 검 집중찌르기 +50%</summary>
	public const float BleedFocusedThrust = 0.5f;

	/// <summary>출혈 상태: 상처 공략 +100%</summary>
	public const float BleedWoundExploit = 1.0f;

	/// <summary>출혈 상태: 상처 가르기 +100% (출혈 도트에 적용)</summary>
	public const float BleedWoundSlash = 1.0f;

	/// <summary>미니게임 성공: 급소공략 +200%</summary>
	public const float VitalStrikeMinigame = 2.0f;

	/// <summary>치명타: 검 깊이찌르기 +150%. DamageRequest.CritBonusRate에 넣는다.</summary>
	public const float CritDeepThrust = 1.5f;
}
