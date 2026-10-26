using System;
using UnityEngine;

/// <summary>
/// 모든 몬스터(잡몹, 보스)의 체력·피격·사망을 맡는 컴포넌트.<br/>
/// 체력은 이 컴포넌트만 바꾼다. 다른 스크립트는 TakeDamage를 부르고, Hp·MaxHp는 읽기만 한다.
/// <para>
/// 사용법<br/>
/// - 몬스터 프리팹 최상위 오브젝트에 붙인다. Layer는 Monster로 둔다.<br/>
/// - 피격·사망 연출(이펙트, 소리, 애니메이션)은 Damaged, Died 이벤트를 받아서 한다.<br/>
/// - 체력이 0이 되면 Died를 알리고, despawnDelay초 뒤에 오브젝트를 없앤다.
/// </para>
/// </summary>
public class Monster : MonoBehaviour, IDamageable
{
	[SerializeField, Min(1), Tooltip("최대 체력")]
	int maxHp = 100;

	[SerializeField, Min(0f), Tooltip("사망 후 오브젝트를 없애기까지 기다리는 시간(초). 사망 연출이 보일 시간")]
	float despawnDelay = 0.5f;

	/// <summary>
	/// 최대 체력.
	/// </summary>
	public int MaxHp => maxHp;

	/// <summary>
	/// 지금 체력. 0이면 죽은 것이다.
	/// </summary>
	public int Hp { get; private set; }

	/// <summary>
	/// 죽었으면 true. 죽은 뒤에는 데미지를 받지 않는다.
	/// </summary>
	public bool IsDead => Hp <= 0;

	/// <summary>
	/// 데미지를 받아 체력이 줄었을 때 불린다. 받은 데미지를 넘긴다. 피격 연출에 쓴다.
	/// </summary>
	public event Action<int> Damaged;

	/// <summary>
	/// 체력이 0이 된 순간 한 번 불린다. 사망 연출, 드롭, 스포너 집계에 쓴다.
	/// </summary>
	public event Action Died;

	void Awake()
	{
		Hp = maxHp;
	}

	/// <summary>
	/// damage만큼 체력을 줄인다. 0 이하이거나 이미 죽었으면 무시한다. 체력이 0이 되면 사망한다.
	/// </summary>
	public void TakeDamage(int damage)
	{
		if (damage <= 0 || IsDead) return;

		Hp = Mathf.Max(Hp - damage, 0);
		PlayHitEffect(damage);

		if (Hp == 0) Die();
	}

	void Die()
	{
		PlayDeathEffect();
		Despawn();
	}

	// 아래 두 함수는 연출만 한다. 네트워크를 붙이면 [ClientRpc]를 단다 (코드 작성 규칙 1)
	void PlayHitEffect(int damage) => Damaged?.Invoke(damage);

	void PlayDeathEffect() => Died?.Invoke();

	// 몬스터를 없애는 곳은 여기 하나다. 네트워크를 붙이면 NetworkServer.Destroy로 바꾼다 (코드 작성 규칙 3)
	void Despawn() => Destroy(gameObject, despawnDelay);
}
