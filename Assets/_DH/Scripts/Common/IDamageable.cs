/// <summary>
/// 데미지를 받을 수 있는 모든 것(몬스터, 플레이어, 보스, 기둥, 분신 등)이 구현하는 인터페이스.<br/>
/// 공격하는 쪽은 맞은 대상이 무엇인지 몰라도 TakeDamage만 부르면 된다.
/// <para>
/// 사용법<br/>
/// - 맞은 Collider2D에서 GetComponentInParent&lt;IDamageable&gt;()로 가져와 TakeDamage를 부른다.<br/>
/// - 데미지는 정수다. 배율을 곱한 뒤 반올림하는 일은 데미지 계산 쪽에서 끝내고 넘긴다.
/// </para>
/// </summary>
public interface IDamageable
{
	/// <summary>
	/// damage만큼 피해를 준다. 0 이하이거나 이미 죽은 대상이면 아무 일도 일어나지 않는다.
	/// </summary>
	void TakeDamage(int damage);

	/// <summary>
	/// 때린 플레이어(attacker)와 함께 피해를 준다 (#484). 플레이어 스킬은 이 함수에 시전자를 넘긴다.<br/>
	/// 몬스터(Monster)는 때린 플레이어별 누적 피해량을 기록해 처치 보상을 받을 사람을 정한다. 나머지 대상은 TakeDamage(damage)와 같다.
	/// </summary>
	// 기본 구현을 두어, 때린 사람을 쓰지 않는 대상(PlayerHealth)은 고치지 않아도 되게 한다
	void TakeDamage(int damage, UnityEngine.GameObject attacker) => TakeDamage(damage);
}
