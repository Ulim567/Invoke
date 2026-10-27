using UnityEngine;

/// <summary>
/// 공격 형태 "투사체". ProjectileAttack이 만들어 날리고, 맞으면 공격 효과를 적용한 뒤 사라진다.
/// <para>
/// 사용법 (프리팹 설정)<br/>
/// - Layer는 MonsterProjectile로 둔다. Player, Ground, Platform과만 부딪힌다.<br/>
/// - Rigidbody2D: Body Type Dynamic, Gravity Scale 0, Freeze Rotation Z<br/>
/// - Collider2D: Is Trigger를 켠다 (몸으로 밀지 않고 겹침만 알린다)<br/>
/// - Target Layers는 Player, Block Layers는 Ground와 Platform으로 둔다.
/// </para>
/// <para>
/// 패링(박진우)은 Attack.Parryable로 잡을 수 있는지 확인한다. 잡은 투사체를 없애는 방법은 패링 이슈에서 정한다.
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

	Rigidbody2D body;
	float despawnTime;
	// 같은 물리 단계에 여러 Collider와 겹쳐도 한 번만 맞도록 막는다
	bool spent;

	/// <summary>
	/// 이 투사체가 맞았을 때 적용할 공격 데이터. Launch에서 정해진다.
	/// </summary>
	public MonsterAttackData Attack { get; private set; }

	void Awake()
	{
		body = GetComponent<Rigidbody2D>();
	}

	/// <summary>
	/// attack 데이터를 담아 velocity 속도로 날린다. ProjectileAttack이 생성 직후 부른다.
	/// </summary>
	public void Launch(MonsterAttackData attack, Vector2 velocity)
	{
		Attack = attack;
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

	// 투사체를 없애는 곳은 여기 하나다. 네트워크를 붙이면 NetworkServer.Destroy로 바꾼다 (코드 작성 규칙 3)
	void Despawn() => Destroy(gameObject);
}
