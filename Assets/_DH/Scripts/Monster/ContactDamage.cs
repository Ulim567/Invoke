using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 몸에 닿은 플레이어에게 피해를 주는 컴포넌트. 몸은 서로 통과한다.
/// <para>
/// Player와 Monster 레이어는 충돌이 꺼져 있어서 OnTriggerEnter2D가 오지 않는다.
/// 그래서 FixedUpdate마다 몸 크기의 상자 안에 targetLayers 오브젝트가 있는지 직접 검사한다.<br/>
/// 검사는 몬스터가 속한 씬의 물리 세계에 한다. 서버에서는 장소 씬마다 물리 세계가 따로 있기 때문이다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Monster와 같은 오브젝트에 붙인다. targetLayers는 Player로 둔다.<br/>
/// - 상자 크기(boxSize, boxOffset)는 Scene 창에서 오브젝트를 선택하면 빨간 상자로 보인다.<br/>
/// - 같은 대상에게는 hitInterval초에 한 번만 피해를 준다.<br/>
/// - 같은 오브젝트의 StatusEffects(없으면 무시)가 기절(행동 불가)이나 실명(공격이 빗나감)이면 피해를 주지 않는다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class ContactDamage : MonoBehaviour
{
	[SerializeField, Min(0), Tooltip("닿았을 때 주는 데미지")]
	int damage = 10;

	[SerializeField, Min(0f), Tooltip("같은 대상에게 다시 피해를 주기까지 기다리는 시간(초)")]
	float hitInterval = 1f;

	[SerializeField, Tooltip("검사 상자 크기")]
	Vector2 boxSize = Vector2.one;

	[SerializeField, Tooltip("오브젝트 위치에서 검사 상자 중심까지의 거리")]
	Vector2 boxOffset;

	[SerializeField, Tooltip("피해를 줄 레이어. Player로 둔다")]
	LayerMask targetLayers;

	// 검사 결과를 담는 목록. 매번 새로 만들지 않고 재사용해서 메모리 할당을 없앤다
	readonly List<Collider2D> hits = new List<Collider2D>(4);
	// 대상마다 다음에 피해를 줄 수 있는 시각
	readonly Dictionary<IDamageable, float> nextHitTime = new Dictionary<IDamageable, float>();

	ContactFilter2D filter;
	Monster monster;
	// 상태 이상. 몬스터에 붙어 있지 않으면 null이고, 상태 이상에 반응하지 않는다
	StatusEffects status;

	void Awake()
	{
		monster = GetComponent<Monster>();
		status = GetComponent<StatusEffects>();

		filter.SetLayerMask(targetLayers);
		// 몸(Trigger가 아닌 Collider)에 닿았을 때만 피해를 준다
		filter.useTriggers = false;
	}

	void FixedUpdate()
	{
		// 기절이면 행동하지 않고, 실명이면 공격이 빗나간다
		if (monster.IsDead || (status != null && (status.IsStunned || status.IsBlinded))) return;

		// 내가 속한 장소 씬의 물리 세계에 묻는다. 장소를 옮기면 씬이 바뀌므로 저장하지 않고 매번 가져온다
		int count = gameObject.scene.GetPhysicsScene2D().OverlapBox((Vector2)transform.position + boxOffset, boxSize, 0f, filter, hits);
		if (count == 0) return;

		float now = Time.fixedTime;
		for (int i = 0; i < count; i++)
		{
			// Collider가 자식에 있어도 찾도록 부모까지 올라가며 찾는다
			IDamageable target = hits[i].GetComponentInParent<IDamageable>();
			if (target == null) continue;
			// Collider가 여러 개인 대상도 hitInterval 동안 한 번만 맞는다
			if (nextHitTime.TryGetValue(target, out float next) && now < next) continue;

			nextHitTime[target] = now + hitInterval;
			target.TakeDamage(damage);
		}
	}

	void OnDisable()
	{
		// 사라진 대상이 목록에 남지 않도록 비운다
		nextHitTime.Clear();
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube((Vector2)transform.position + boxOffset, boxSize);
	}
}
