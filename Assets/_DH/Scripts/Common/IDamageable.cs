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
}
