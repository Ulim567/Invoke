using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 땅을 걸어 다니는 몬스터의 순찰·추적 AI. 잡몹 Leak이 쓴다.
/// <para>
/// - 순찰: "걷기"와 "멈추기"를 랜덤 시간씩 번갈아 한다. 걷기가 끝나면 idleChance 확률로 멈추고, 다시 걸을 때 turnChance 확률로 반대로 돌아선다.<br/>
///   걷는 중 앞이 낭떠러지거나 장애물이면 바로 돌아선다. 처음 방향과 상태는 랜덤이라 여러 마리가 똑같이 움직이지 않는다.<br/>
/// - 발견: chasePlayers가 켜져 있을 때만 한다. 끄면 플레이어를 쫓지 않고 순찰만 한다(Leak). detectRange 안에 살아 있는 플레이어가 있으면 가장 가까운 플레이어를 쫓는다. loseRange 밖으로 벗어나거나 죽으면 순찰로 돌아간다.<br/>
/// - 추적: 좌우로만 chaseSpeed로 움직인다. 앞이 낭떠러지거나 장애물이면 그 자리에 멈춘다(떨어지지 않고, 다른 몬스터를 밀지 않는다).<br/>
/// - 몬스터가 죽으면 멈춘다. 공격(접촉 피해)은 같은 오브젝트의 ContactDamage가 한다.<br/>
/// - 탐지·낭떠러지·장애물 검사는 몬스터가 속한 씬의 물리 세계에 한다. 서버에서는 장소 씬마다 물리 세계가 따로 있기 때문이다.
/// </para>
/// <para>
/// 네트워크를 붙이면 AI는 서버에서만 돌린다(FixedUpdate에 [ServerCallback]). 위치는 Rigidbody 동기화로 각 화면에 보인다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster), typeof(Rigidbody2D), typeof(Collider2D))]
public class GroundChaseAI : MonoBehaviour
{
	[Header("이동")]
	[SerializeField, Min(0f), Tooltip("순찰 속도 (초당 유닛)")]
	float patrolSpeed = 1.5f;

	[SerializeField, Min(0f), Tooltip("추적 속도 (초당 유닛)")]
	float chaseSpeed = 2.5f;

	[SerializeField, Tooltip("밟고 서는 레이어. Ground, Platform으로 둔다")]
	LayerMask groundLayers;

	[SerializeField, Min(0.05f), Tooltip("발 앞쪽 아래로 땅을 찾는 거리. 이보다 깊으면 낭떠러지로 본다")]
	float edgeCheckDepth = 0.5f;

	[SerializeField, Tooltip("앞을 막는 장애물 레이어. Ground로 둔다. Monster를 넣으면 몬스터끼리 마주칠 때 통과하지 않고 돌아선다")]
	LayerMask obstacleLayers;

	[SerializeField, Min(0.01f), Tooltip("몸 앞쪽으로 장애물을 찾는 거리")]
	float wallCheckDistance = 0.1f;

	[Header("순찰 (랜덤)")]
	[SerializeField, Tooltip("한 번 걷는 시간(초). x 최소, y 최대")]
	Vector2 walkTimeRange = new Vector2(1.5f, 4f);

	[SerializeField, Tooltip("한 번 멈추는 시간(초). x 최소, y 최대")]
	Vector2 idleTimeRange = new Vector2(0.5f, 2f);

	[SerializeField, Range(0f, 1f), Tooltip("걷기가 끝났을 때 멈출 확률. 멈추지 않으면 계속 걷는다")]
	float idleChance = 0.5f;

	[SerializeField, Range(0f, 1f), Tooltip("다시 걷기 시작할 때 반대 방향으로 돌아설 확률")]
	float turnChance = 0.4f;

	[Header("탐지")]
	[SerializeField, Tooltip("켜면 플레이어를 발견해 쫓는다. 끄면 플레이어를 무시하고 순찰만 한다")]
	bool chasePlayers = true;

	[SerializeField, Tooltip("쫓을 대상 레이어. Player로 둔다")]
	LayerMask targetLayers;

	[SerializeField, Min(0f), Tooltip("이 거리 안에 들어온 플레이어를 발견한다 (유닛)")]
	float detectRange = 8f;

	[SerializeField, Min(0f), Tooltip("쫓던 플레이어가 이 거리 밖으로 나가면 놓친다. detectRange보다 크게 둔다")]
	float loseRange = 12f;

	[SerializeField, Min(0.02f), Tooltip("플레이어를 찾는 간격(초). 매 물리 프레임마다 찾지 않아 부담을 줄인다")]
	float detectInterval = 0.2f;

	[SerializeField, Min(0f), Tooltip("쫓는 대상과 가로 거리가 이보다 가까우면 멈춘다. 대상 위치에서 떨지 않게 하기 위함")]
	float stopDistance = 0.1f;

	Monster monster;
	Rigidbody2D body;
	Collider2D bodyCollider;
	SpriteRenderer spriteRenderer;
	ContactFilter2D targetFilter;
	// 탐지 결과를 담는 목록. 재사용해서 메모리 할당을 없앤다
	readonly List<Collider2D> found = new List<Collider2D>(4);

	PlayerHealth target;
	float nextDetectTime;
	// 바라보는 방향. 오른쪽 1, 왼쪽 -1
	int facing = 1;
	// 순찰 상태. true면 멈추기, false면 걷기. patrolStateEndTime이 지나면 다음 상태를 뽑는다
	bool isIdle;
	float patrolStateEndTime;

	/// <summary>
	/// 지금 쫓고 있는 플레이어. 순찰 중이면 null.
	/// </summary>
	public PlayerHealth Target => target;

	/// <summary>
	/// 지금 좌우로 움직이고 있으면 true. 이동·대기 애니메이션을 바꿀 때 읽는다.
	/// </summary>
	public bool IsMoving => Mathf.Abs(body.linearVelocity.x) > 0.01f;

	/// <summary>
	/// 바라보는 방향. 오른쪽 1, 왼쪽 -1. 공격 방향을 정할 때 읽는다.
	/// </summary>
	public int Facing => facing;

	void Awake()
	{
		monster = GetComponent<Monster>();
		body = GetComponent<Rigidbody2D>();
		bodyCollider = GetComponent<Collider2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();

		targetFilter.SetLayerMask(targetLayers);
		targetFilter.useTriggers = false;
	}

	void Start()
	{
		// 처음 방향과 상태를 랜덤으로 정해, 같은 프리팹 여러 마리가 똑같이 움직이지 않게 한다
		Face(Random.value < 0.5f ? 1 : -1);
		isIdle = Random.value < idleChance;
		patrolStateEndTime = Time.time + RandomIn(isIdle ? idleTimeRange : walkTimeRange);
	}

	void FixedUpdate()
	{
		if (monster.IsDead)
		{
			SetVelocityX(0f);
			return;
		}

		// 쫓지 않는 몬스터는 탐지 자체를 하지 않아 검사 비용이 없다
		if (chasePlayers && Time.time >= nextDetectTime)
		{
			nextDetectTime = Time.time + detectInterval;
			UpdateTarget();
		}

		if (target != null) Chase();
		else Patrol();
	}

	void Patrol()
	{
		if (Time.time >= patrolStateEndTime) NextPatrolState();

		if (isIdle)
		{
			SetVelocityX(0f);
			return;
		}

		// 앞이 막혔으면 돌아서고, 이번 프레임은 멈춘다 (돌아선 방향은 다음 프레임에 검사)
		if (IsBlocked(facing))
		{
			Face(-facing);
			SetVelocityX(0f);
			return;
		}
		SetVelocityX(facing * patrolSpeed);
	}

	// 걷기가 끝나면 멈추거나 계속 걷고, 멈추기가 끝나면 다시 걷는다. 다시 걸을 때 가끔 돌아선다
	void NextPatrolState()
	{
		if (!isIdle && Random.value < idleChance)
		{
			StartIdle();
			return;
		}

		isIdle = false;
		if (Random.value < turnChance) Face(-facing);
		patrolStateEndTime = Time.time + RandomIn(walkTimeRange);
	}

	void StartIdle()
	{
		isIdle = true;
		patrolStateEndTime = Time.time + RandomIn(idleTimeRange);
	}

	static float RandomIn(Vector2 range) => Random.Range(range.x, Mathf.Max(range.x, range.y));

	void Chase()
	{
		float dx = target.transform.position.x - body.position.x;
		if (Mathf.Abs(dx) <= stopDistance)
		{
			SetVelocityX(0f);
			return;
		}

		int dir = dx > 0f ? 1 : -1;
		Face(dir);
		// 쫓다가 낭떠러지나 벽을 만나면 그 자리에서 기다린다
		SetVelocityX(IsBlocked(dir) ? 0f : dir * chaseSpeed);
	}

	void UpdateTarget()
	{
		Vector2 pos = body.position;

		// 쫓던 대상이 죽었거나 너무 멀어지면 놓치고, 잠깐 멈췄다가 순찰로 돌아간다
		if (target != null && (target.IsDead || ((Vector2)target.transform.position - pos).sqrMagnitude > loseRange * loseRange))
		{
			target = null;
			StartIdle();
		}
		if (target != null) return;

		int count = ScenePhysics().OverlapCircle(pos, detectRange, targetFilter, found);
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
			}
		}
	}

	// dir 쪽이 낭떠러지이거나 장애물(벽, 다른 몬스터)이면 true
	bool IsBlocked(int dir)
	{
		Bounds b = bodyCollider.bounds;

		// 발 바로 앞 아래에 땅이 없으면 낭떠러지
		Vector2 footFront = new Vector2(b.center.x + dir * (b.extents.x + 0.05f), b.min.y + 0.05f);
		PhysicsScene2D physics = ScenePhysics();
		if (!physics.Raycast(footFront, Vector2.down, edgeCheckDepth, groundLayers)) return true;

		// 몸 가운데 높이에서 앞으로 장애물을 찾는다
		// 내 Collider 바로 바깥에서 시작해서, Monster 레이어를 검사해도 나 자신은 잡히지 않게 한다
		Vector2 bodyFront = new Vector2(b.center.x + dir * (b.extents.x + 0.02f), b.center.y);
		return physics.Raycast(bodyFront, new Vector2(dir, 0f), wallCheckDistance, obstacleLayers);
	}

	// 내가 속한 장소 씬의 물리 세계. 장소를 옮기면 씬이 바뀌므로 저장하지 않고 검사할 때마다 가져온다
	PhysicsScene2D ScenePhysics() => gameObject.scene.GetPhysicsScene2D();

	void SetVelocityX(float x) => body.linearVelocity = new Vector2(x, body.linearVelocity.y);

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
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(pos, detectRange);
		Gizmos.color = new Color(1f, 0.5f, 0f);
		Gizmos.DrawWireSphere(pos, loseRange);
	}
}
