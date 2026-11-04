using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공격 형태 "투사체". ProjectileAttack이 만들어 날리고, 맞으면 공격 효과를 적용한 뒤 사라진다.
/// <para>
/// 사용법 (프리팹 설정)<br/>
/// - Layer는 MonsterProjectile로 둔다. Player, Ground, Platform과만 부딪힌다.<br/>
/// - Rigidbody2D: Body Type Dynamic, Gravity Scale 0, Freeze Rotation Z<br/>
/// - Collider2D: Is Trigger를 켠다 (몸으로 밀지 않고 겹침만 알린다)<br/>
/// - Target Layers는 Player, Block Layers는 Ground와 Platform으로 둔다.<br/>
/// - Pierce를 켜면 맞은 플레이어마다 한 번씩 효과를 주고 사라지지 않고 계속 날아간다(보스의 큰 패킷 덩어리처럼 한 줄을 모두 맞히는 공격).
/// </para>
/// <para>
/// 패링(박진우): 플레이어에 닿으면 효과를 주기 전에 그 플레이어의 ParryGuard.TryParry(this)를 묻는다.
/// true면 패링 성공이라 효과 없이 사라진다(관통 투사체도 사라진다). Parryable·Misses·판정 시간은 ParryGuard가 본다.
/// </para>
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MonsterProjectile : MonoBehaviour
{
	[SerializeField, Min(0.1f), Tooltip("아무것도 맞히지 못했을 때 사라지기까지 시간(초)")]
	float lifetime = 5f;

	[SerializeField, Tooltip("맞히면 공격 효과를 줄 레이어. Player로 둔다")]
	LayerMask targetLayers;

	[SerializeField, Tooltip("닿으면 효과 없이 사라질 레이어. Ground, Platform으로 둔다")]
	LayerMask blockLayers;

	[SerializeField, Tooltip("켜면 맞은 대상마다 한 번씩 효과를 주고 계속 날아간다(관통). 끄면 처음 맞은 대상에서 사라진다")]
	bool pierce;

	Rigidbody2D body;
	float despawnTime;
	// 같은 물리 단계에 여러 Collider와 겹쳐도 한 번만 맞도록 막는다
	bool spent;
	// 관통할 때 이미 맞힌 대상. Collider가 여러 개인 대상도 한 번만 맞는다
	readonly HashSet<GameObject> pierced = new HashSet<GameObject>();

	/// <summary>
	/// 이 투사체가 맞았을 때 적용할 공격 데이터. Launch에서 정해진다.
	/// </summary>
	public MonsterAttackData Attack { get; private set; }

	/// <summary>
	/// true면 플레이어에게 맞지 않고 통과한다(쏜 몬스터가 실명 중이었음). Launch에서 정해진다.
	/// </summary>
	public bool Misses { get; private set; }

	void Awake()
	{
		body = GetComponent<Rigidbody2D>();
	}

	/// <summary>
	/// attack 데이터를 담아 velocity 속도로 날린다. ProjectileAttack이 생성 직후 부른다.<br/>
	/// misses가 true면 플레이어를 통과한다(실명). 땅·발판에는 그대로 막힌다.
	/// </summary>
	public void Launch(MonsterAttackData attack, Vector2 velocity, bool misses = false)
	{
		Attack = attack;
		Misses = misses;
		pierced.Clear();
		body.linearVelocity = velocity;
		despawnTime = Time.time + lifetime;
		spent = false;
	}

	void FixedUpdate()
	{
		if (Time.time >= despawnTime) Despawn();
	}

	// 네트워크를 붙이면 [ServerCallback]을 단다. 맞았는지는 서버에서만 판단한다
	void OnTriggerEnter2D(Collider2D other)
	{
		if (spent) return;

		int layerBit = 1 << other.gameObject.layer;
		if ((targetLayers.value & layerBit) != 0)
		{
			// 빗나가는 투사체는 플레이어를 그냥 지나간다
			if (Misses) return;

			// 관통: 대상(Rigidbody가 있는 최상위)마다 한 번만 판정한다. 이미 맞힌 대상의 다른 Collider는 무시
			GameObject owner = null;
			if (pierce)
			{
				owner = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
				if (pierced.Contains(owner)) return;
			}

			// 효과를 주기 전에 패링부터 묻는다. 잡혔으면 효과 없이 사라진다
			if (TryParried(other)) return;

			if (pierce)
			{
				pierced.Add(owner);
				MonsterAttackResolver.Apply(Attack, other);
				return;
			}
			spent = true;
			MonsterAttackResolver.Apply(Attack, other);
			Despawn();
		}
		else if ((blockLayers.value & layerBit) != 0)
		{
			spent = true;
			Despawn();
		}
	}

	// 맞은 플레이어가 패링 중이면(ParryGuard, 박진우) 잡혀서 사라진다. 잡혔으면 true
	bool TryParried(Collider2D other)
	{
		ParryGuard guard = other.GetComponentInParent<ParryGuard>();
		if (guard == null || !guard.TryParry(this)) return false;

		spent = true;
		Despawn();
		return true;
	}

	// 투사체를 없애는 곳은 여기 하나다. 네트워크를 붙이면 NetworkServer.Destroy로 바꾼다 (코드 작성 규칙 3)
	void Despawn() => Destroy(gameObject);
}
