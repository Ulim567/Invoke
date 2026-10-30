using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공중을 떠다니며 멀리서 쏘는 몬스터의 AI. 잡몹 2 Loop가 쓴다.
/// <para>
/// - 떠다니기: 중력 없이, 생긴 자리보다 hoverHeight만큼 위 높이에서 위아래로 살짝 둥실거린다.<br/>
/// - 순찰: "움직이기"와 "멈추기"를 랜덤 시간씩 번갈아 하고, 다시 움직일 때 turnChance 확률로 돌아선다.
///   앞이 벽(obstacleLayers)이거나 처음 자리에서 patrolRadius보다 멀어지면 돌아선다.<br/>
/// - 공격: attackRange 안에 살아 있는 플레이어가 들어오면 그 자리에 멈춰 그 플레이어를 바라보고, fireInterval초마다 몸 중심을 겨냥해 쏜다(ProjectileAttack.Fire).
///   loseRange 밖으로 나가거나 죽으면 순찰로 돌아간다. 쫓아가지는 않는다.<br/>
/// - 밀려나기: 맞으면(Monster.Damaged) 쏘던 플레이어 반대쪽(없으면 바라보는 반대쪽)으로 knockbackTime초 동안 밀린다.<br/>
/// - 상태 이상(같은 오브젝트의 StatusEffects, 없으면 무시): 기절 중에는 제자리에 멈추고 탐지·발사하지 않는다. 이속 감소 중에는 순찰 속도에 MoveSpeedMultiplier를 곱한다.<br/>
/// - 죽으면 멈춘다. 판정은 몬스터가 속한 씬의 물리 세계에 한다.
/// </para>
/// <para>
/// 사용법: 몬스터 프리팹에 Monster, Rigidbody2D(Dynamic, Gravity Scale 0, Freeze Rotation Z), Collider2D, ProjectileAttack과 함께 붙인다.<br/>
/// 네트워크를 붙이면 AI는 서버에서만 돌린다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster), typeof(Rigidbody2D), typeof(ProjectileAttack))]
public class FlyingShooterAI : MonoBehaviour
{
	[Header("떠다니기")]
	[SerializeField, Min(0f), Tooltip("생긴 자리에서 이만큼 위에 떠 있는다 (유닛)")]
	float hoverHeight = 2.5f;

	[SerializeField, Min(0f), Tooltip("위아래로 둥실거리는 폭 (유닛)")]
	float bobAmplitude = 0.15f;

	[SerializeField, Min(0f), Tooltip("1초에 둥실거리는 횟수")]
	float bobFrequency = 1f;

	[SerializeField, Min(0.1f), Tooltip("떠 있을 높이로 돌아가는 세기. 클수록 빨리 돌아간다")]
	float heightFollow = 6f;

	[SerializeField, Min(0.1f), Tooltip("높이를 맞출 때 최대 위아래 속도 (초당 유닛)")]
	float maxVerticalSpeed = 3f;

	[Header("순찰 (랜덤)")]
	[SerializeField, Min(0f), Tooltip("순찰 속도 (초당 유닛)")]
	float patrolSpeed = 1.2f;

	[SerializeField, Min(0f), Tooltip("처음 자리에서 좌우로 이만큼까지만 움직인다 (유닛)")]
	float patrolRadius = 4f;

	[SerializeField, Tooltip("한 번 움직이는 시간(초). x 최소, y 최대")]
	Vector2 moveTimeRange = new Vector2(1.5f, 3.5f);

	[SerializeField, Tooltip("한 번 멈추는 시간(초). x 최소, y 최대")]
	Vector2 idleTimeRange = new Vector2(0.5f, 1.5f);

	[SerializeField, Range(0f, 1f), Tooltip("움직이기가 끝났을 때 멈출 확률")]
	float idleChance = 0.4f;

	[SerializeField, Range(0f, 1f), Tooltip("다시 움직이기 시작할 때 반대 방향으로 돌아설 확률")]
	float turnChance = 0.4f;

	[SerializeField, Tooltip("앞을 막는 장애물 레이어. Ground로 둔다")]
	LayerMask obstacleLayers;

	[SerializeField, Min(0.01f), Tooltip("몸 앞쪽으로 장애물을 찾는 거리")]
	float wallCheckDistance = 0.2f;

	[Header("공격")]
	[SerializeField, Tooltip("쏠 대상 레이어. Player로 둔다")]
	LayerMask targetLayers;

	[SerializeField, Min(0f), Tooltip("이 거리 안에 들어온 플레이어를 쏜다 (유닛)")]
	float attackRange = 6f;

	[SerializeField, Min(0f), Tooltip("쏘던 플레이어가 이 거리 밖으로 나가면 그만 쏜다. attackRange보다 크게 둔다")]
	float loseRange = 8f;

	[SerializeField, Min(0.02f), Tooltip("플레이어를 찾는 간격(초)")]
	float detectInterval = 0.2f;

	[SerializeField, Min(0.1f), Tooltip("쏘는 간격(초). 발사 모으는 시간(Fire Delay)보다 길게 둔다")]
	float fireInterval = 2f;

	[SerializeField, Min(0f), Tooltip("플레이어를 처음 발견하고 첫 발을 쏘기까지 기다리는 시간(초)")]
	float firstShotDelay = 0.5f;

	[Header("밀려나기")]
	[SerializeField, Min(0f), Tooltip("맞았을 때 밀려나는 속도 (초당 유닛). 0이면 밀리지 않는다")]
	float knockbackSpeed = 4f;

	[SerializeField, Min(0f), Tooltip("맞았을 때 밀려나는 시간(초)")]
	float knockbackTime = 0.15f;

	Monster monster;
	// 상태 이상. 몬스터에 붙어 있지 않으면 null이고, 상태 이상에 반응하지 않는다
	StatusEffects status;
	ProjectileAttack shooter;
	Rigidbody2D body;
	Collider2D bodyCollider;
	SpriteRenderer spriteRenderer;
	ContactFilter2D targetFilter;
	// 탐지 결과를 담는 목록. 재사용해서 메모리 할당을 없앤다
	readonly List<Collider2D> found = new List<Collider2D>(4);

	PlayerHealth target;
	// 겨냥할 때 몸 중심을 쓰려고 대상의 Collider를 같이 기억한다
	Collider2D targetCollider;
	float nextDetectTime;
	float nextFireTime;

	// 떠 있을 기준 자리(생긴 자리 + hoverHeight). 순찰 범위도 이 x를 기준으로 한다
	Vector2 home;
	// 둥실거림 시작 위상. 여러 마리가 똑같이 움직이지 않게 랜덤으로 둔다
	float bobPhase;

	// 바라보는 방향. 오른쪽 1, 왼쪽 -1
	int facing = 1;
	bool isIdle;
	float patrolStateEndTime;

	// 밀려나는 방향(오른쪽 1, 왼쪽 -1)과 끝나는 시각
	int knockbackDir;
	float knockbackEndTime;

	/// <summary>
	/// 지금 쏘고 있는 플레이어. 순찰 중이면 null.
	/// </summary>
	public PlayerHealth Target => target;

	/// <summary>
	/// 바라보는 방향. 오른쪽 1, 왼쪽 -1.
	/// </summary>
	public int Facing => facing;

	/// <summary>
	/// 지금 좌우로 움직이고 있으면 true(밀려나는 중 포함).
	/// </summary>
	public bool IsMoving => Mathf.Abs(body.linearVelocity.x) > 0.01f;

	void Awake()
	{
		monster = GetComponent<Monster>();
		status = GetComponent<StatusEffects>();
		shooter = GetComponent<ProjectileAttack>();
		body = GetComponent<Rigidbody2D>();
		bodyCollider = GetComponent<Collider2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();

		targetFilter.SetLayerMask(targetLayers);
		targetFilter.useTriggers = false;
	}

	void OnEnable()
	{
		monster.Damaged += OnDamaged;
	}

	void OnDisable()
	{
		monster.Damaged -= OnDamaged;
	}

	void Start()
	{
		home = body.position + new Vector2(0f, hoverHeight);
		bobPhase = Random.value * Mathf.PI * 2f;

		// 처음 방향과 상태를 랜덤으로 정해, 여러 마리가 똑같이 움직이지 않게 한다
		Face(Random.value < 0.5f ? 1 : -1);
		ShowFacing();
		isIdle = Random.value < idleChance;
		patrolStateEndTime = Time.time + RandomIn(isIdle ? idleTimeRange : moveTimeRange);
	}

	void FixedUpdate()
	{
		// 죽었거나 기절했으면 그 자리에 멈춘다 (기절: 행동 불가)
		if (monster.IsDead || (status != null && status.IsStunned))
		{
			body.linearVelocity = Vector2.zero;
			return;
		}

		float now = Time.time;
		if (now >= nextDetectTime)
		{
			nextDetectTime = now + detectInterval;
			UpdateTarget(now);
		}

		float vx;
		if (now < knockbackEndTime) vx = knockbackDir * knockbackSpeed;
		else if (target != null) vx = Attack(now);
		else vx = Patrol(now) * SpeedMultiplier();

		body.linearVelocity = new Vector2(vx, VerticalSpeed(now));
	}

	// 떠 있을 높이(기준 높이 + 둥실거림)로 돌아가는 위아래 속도
	float VerticalSpeed(float now)
	{
		float desiredY = home.y + Mathf.Sin(now * bobFrequency * Mathf.PI * 2f + bobPhase) * bobAmplitude;
		return Mathf.Clamp((desiredY - body.position.y) * heightFollow, -maxVerticalSpeed, maxVerticalSpeed);
	}

	// 멈춰서 대상을 바라보고, 간격마다 몸 중심을 겨냥해 쏜다
	float Attack(float now)
	{
		Vector2 aim = targetCollider != null ? (Vector2)targetCollider.bounds.center : (Vector2)target.transform.position;
		Vector2 toTarget = aim - body.position;
		if (Mathf.Abs(toTarget.x) > 0.01f) Face(toTarget.x > 0f ? 1 : -1);

		if (now >= nextFireTime && !shooter.IsCharging)
		{
			nextFireTime = now + fireInterval;
			shooter.Fire(toTarget);
		}
		return 0f;
	}

	float Patrol(float now)
	{
		if (now >= patrolStateEndTime) NextPatrolState(now);
		if (isIdle) return 0f;

		// 벽이 있거나 순찰 범위 끝이면 돌아서고, 이번 프레임은 멈춘다
		if (IsBlocked(facing) || (body.position.x - home.x) * facing >= patrolRadius)
		{
			Face(-facing);
			return 0f;
		}
		return facing * patrolSpeed;
	}

	void NextPatrolState(float now)
	{
		if (!isIdle && Random.value < idleChance)
		{
			StartIdle(now);
			return;
		}

		isIdle = false;
		if (Random.value < turnChance) Face(-facing);
		patrolStateEndTime = now + RandomIn(moveTimeRange);
	}

	void StartIdle(float now)
	{
		isIdle = true;
		patrolStateEndTime = now + RandomIn(idleTimeRange);
	}

	static float RandomIn(Vector2 range) => Random.Range(range.x, Mathf.Max(range.x, range.y));

	void UpdateTarget(float now)
	{
		Vector2 pos = body.position;

		// 쏘던 대상이 죽었거나 너무 멀어지면 놓치고, 잠깐 멈췄다가 순찰로 돌아간다
		if (target != null && (target.IsDead || ((Vector2)target.transform.position - pos).sqrMagnitude > loseRange * loseRange))
		{
			target = null;
			targetCollider = null;
			StartIdle(now);
		}
		if (target != null) return;

		int count = ScenePhysics().OverlapCircle(pos, attackRange, targetFilter, found);
		float bestSqr = float.MaxValue;
		for (int i = 0; i < count; i++)
		{
			PlayerHealth player = found[i].GetComponentInParent<PlayerHealth>();
			if (player == null || player.IsDead) continue;

			float sqr = ((Vector2)player.transform.position - pos).sqrMagnitude;
			if (sqr < bestSqr)
			{
				bestSqr = sqr;
				target = player;
				targetCollider = found[i];
			}
		}

		// 새로 발견했으면 바로 쏘지 않고 잠깐 뒤에 첫 발을 쏜다
		if (target != null) nextFireTime = now + firstShotDelay;
	}

	// 맞으면 쏘던 플레이어 반대쪽으로 밀린다. 대상이 없으면 바라보는 반대쪽으로 밀린다
	void OnDamaged(int damage)
	{
		if (monster.IsDead || knockbackSpeed <= 0f || knockbackTime <= 0f) return;

		int dir = -facing;
		if (target != null)
		{
			float dx = body.position.x - target.transform.position.x;
			if (Mathf.Abs(dx) > 0.01f) dir = dx > 0f ? 1 : -1;
		}
		knockbackDir = dir;
		knockbackEndTime = Time.time + knockbackTime;
	}

	// dir 쪽 앞에 장애물(벽)이 있으면 true
	bool IsBlocked(int dir)
	{
		Bounds b = bodyCollider.bounds;
		// 내 Collider 바로 바깥에서 시작해서 나 자신은 잡히지 않게 한다
		Vector2 front = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), b.center.y);
		return ScenePhysics().Raycast(front, new Vector2(dir, 0f), wallCheckDistance, obstacleLayers);
	}

	// 이속 감소 중이면 0.5처럼 1보다 작은 값. 상태 이상이 없으면 1
	float SpeedMultiplier() => status != null ? status.MoveSpeedMultiplier : 1f;

	// 내가 속한 장소 씬의 물리 세계. 장소를 옮기면 씬이 바뀌므로 저장하지 않고 검사할 때마다 가져온다
	PhysicsScene2D ScenePhysics() => gameObject.scene.GetPhysicsScene2D();

	void Face(int dir)
	{
		if (dir == facing) return;
		facing = dir;
		ShowFacing();
	}

	// 연출만 한다. 그림은 오른쪽을 보고 그린다고 정하고(플레이어와 같음), 왼쪽이면 뒤집는다
	void ShowFacing()
	{
		if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
	}

	void OnDrawGizmosSelected()
	{
		Vector3 pos = transform.position;
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(pos, attackRange);
		Gizmos.color = new Color(1f, 0.5f, 0f);
		Gizmos.DrawWireSphere(pos, loseRange);
		// 순찰 범위 (Play 중에는 기준 자리, 아니면 지금 위치 기준)
		Vector3 center = Application.isPlaying ? (Vector3)home : pos + Vector3.up * hoverHeight;
		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(center + Vector3.left * patrolRadius, center + Vector3.right * patrolRadius);
	}
}
