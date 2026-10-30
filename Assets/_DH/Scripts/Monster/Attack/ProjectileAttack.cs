using UnityEngine;

/// <summary>
/// 몬스터가 투사체 공격을 쏘게 하는 컴포넌트. 공격 = 투사체(형태) + Attack 데이터(효과).
/// <para>
/// 사용법<br/>
/// - Monster와 같은 오브젝트에 붙이고, Attack에 MonsterAttackData 파일, Projectile Prefab에 MonsterProjectile 프리팹을 넣는다.<br/>
/// - 언제, 어느 방향으로 쏠지는 몬스터 AI(잡몹·보스 이슈)가 정하고 Fire(방향)를 부른다.<br/>
/// - 죽은 몬스터는 쏘지 않는다.<br/>
/// - 같은 오브젝트의 StatusEffects(없으면 무시)가 기절이면 쏘지 않고, 실명이면 쏘지만 그 투사체는 플레이어를 통과한다(빗나감).
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class ProjectileAttack : MonoBehaviour
{
	[SerializeField, Tooltip("맞았을 때 적용할 공격 데이터")]
	MonsterAttackData attack;

	[SerializeField, Tooltip("날릴 투사체 프리팹")]
	MonsterProjectile projectilePrefab;

	[SerializeField, Min(0f), Tooltip("투사체 속도 (초당 유닛)")]
	float speed = 6f;

	[SerializeField, Tooltip("몬스터 위치에서 투사체가 나오는 곳까지의 거리. 왼쪽으로 쏘면 x가 반대로 뒤집힌다")]
	Vector2 firePointOffset = new Vector2(0.5f, 0.5f);

	Monster monster;
	// 상태 이상. 몬스터에 붙어 있지 않으면 null이고, 상태 이상에 반응하지 않는다
	StatusEffects status;

	void Awake()
	{
		monster = GetComponent<Monster>();
		status = GetComponent<StatusEffects>();
	}

	/// <summary>
	/// direction 방향으로 투사체를 쏜다. 방향 길이는 상관없다(자동으로 1로 맞춘다).<br/>
	/// 몬스터가 죽었거나 기절했거나 방향이 (0, 0)이면 쏘지 않는다. 실명 중에 쏜 투사체는 빗나간다.
	/// </summary>
	public void Fire(Vector2 direction)
	{
		if (monster.IsDead || (status != null && status.IsStunned) || direction.sqrMagnitude < 0.0001f) return;

		Vector2 dir = direction.normalized;
		Vector2 offset = new Vector2(dir.x < 0f ? -firePointOffset.x : firePointOffset.x, firePointOffset.y);

		MonsterProjectile projectile = SpawnProjectile((Vector2)transform.position + offset);
		// 실명 중에 쏜 투사체는 날아가는 동안 실명이 풀려도 빗나간다
		projectile.Launch(attack, dir * speed, status != null && status.IsBlinded);
	}

	// 투사체를 만드는 곳은 여기 하나다. 네트워크를 붙이면 NetworkServer.Spawn을 더한다 (코드 작성 규칙 3·4)
	MonsterProjectile SpawnProjectile(Vector2 position) => Instantiate(projectilePrefab, position, Quaternion.identity);

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Vector2 pos = transform.position;
		Gizmos.DrawWireSphere(pos + firePointOffset, 0.1f);
		Gizmos.DrawWireSphere(pos + new Vector2(-firePointOffset.x, firePointOffset.y), 0.1f);
	}
}
