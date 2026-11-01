using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 레이드 보스 Gateway의 패턴. 제자리에 서서 가장 가까운 플레이어를 바라보고, 일정 간격마다 공격 하나를 랜덤으로 고른다.
/// <para>
/// 공격 (같은 공격은 연속 두 번 고르지 않는다)<br/>
/// - 패킷 던지기(Throw): 같은 오브젝트의 ProjectileAttack으로 가장 가까운 플레이어 쪽으로 수평으로 던진다. 연출은 ProjectileAttack.FireEffectPlayed.<br/>
/// - 패킷 낙하(Drop): 살아 있는 플레이어마다 그 발밑 땅에, 그리고 Drop Area 안 랜덤한 땅 Extra Drops곳에 FallingPacket을 만든다.
///   SlamEffectPlayed(연출)가 먼저 나가고, Slam Delay초 뒤에 그림자가 나타난다.<br/>
/// - 공격 범위(Detect Range) 안에 살아 있는 플레이어가 없으면 공격하지 않는다.
/// </para>
/// <para>
/// 사용법: Gateway 프리팹 최상위에 Monster(Is Boss 체크), ProjectileAttack과 함께 붙인다. 움직이지 않으므로 Rigidbody2D는 필요 없다.<br/>
/// 상태 이상(같은 오브젝트의 StatusEffects, 없으면 무시): 기절 중에는 공격을 고르지 않는다.<br/>
/// 네트워크를 붙이면 패턴은 서버에서만 돌린다. 판정은 보스가 속한 씬의 물리 세계에 한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster), typeof(ProjectileAttack))]
public class GatewayAI : MonoBehaviour
{
	/// <summary>
	/// Gateway의 공격 종류. 공격 2(웜·때리기)와 기믹은 이후 이슈에서 더한다.
	/// </summary>
	public enum Pattern
	{
		Throw, // 패킷 던지기
		Drop,  // 패킷 낙하
	}

	[Header("패턴")]
	[SerializeField, Tooltip("쓸 공격 목록. 이 안에서 랜덤으로 고른다")]
	Pattern[] patterns = { Pattern.Throw, Pattern.Drop };

	[SerializeField, Min(0.1f), Tooltip("공격 하나를 시작하고 다음 공격을 고르기까지 시간(초)")]
	float attackInterval = 3f;

	[SerializeField, Min(0f), Tooltip("플레이어를 처음 발견하고 첫 공격까지 기다리는 시간(초)")]
	float firstAttackDelay = 2f;

	[SerializeField, Tooltip("공격할 대상 레이어. Player로 둔다")]
	LayerMask targetLayers;

	[SerializeField, Min(0f), Tooltip("이 거리 안에 살아 있는 플레이어가 있어야 공격한다 (유닛). 보스방을 덮게 둔다")]
	float detectRange = 20f;

	[SerializeField, Min(0.02f), Tooltip("플레이어를 찾는 간격(초). 매 프레임 찾지 않아 부담을 줄인다")]
	float detectInterval = 0.2f;

	[Header("패킷 낙하")]
	[SerializeField, Tooltip("떨어지는 패킷 프리팹 (FallingPacket). NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	FallingPacket fallingPacketPrefab;

	[SerializeField, Min(0f), Tooltip("Slam 연출이 시작되고 그림자가 나타나기까지 시간(초). Slam 모션에서 내려치는 프레임에 맞춘다")]
	float slamDelay = 0.5f;

	[SerializeField, Min(0), Tooltip("플레이어 발밑 말고 랜덤으로 더 떨어뜨릴 개수")]
	int extraDrops = 2;

	[SerializeField, Tooltip("랜덤 낙하 구역 크기. 보스 위치가 가운데. 보스방 바닥을 덮게 둔다")]
	Vector2 dropArea = new Vector2(24f, 12f);

	[SerializeField, Tooltip("패킷이 떨어질 땅 레이어. Ground, Platform으로 둔다")]
	LayerMask groundLayers;

	Monster monster;
	// 상태 이상. 몬스터에 붙어 있지 않으면 null이고, 상태 이상에 반응하지 않는다
	StatusEffects status;
	ProjectileAttack thrower;
	SpriteRenderer spriteRenderer;
	ContactFilter2D targetFilter;
	// 탐지 결과와 살아 있는 플레이어 목록. 재사용해서 메모리 할당을 없앤다
	readonly List<Collider2D> found = new List<Collider2D>(8);
	readonly List<PlayerHealth> players = new List<PlayerHealth>(4);

	PlayerHealth target;
	bool engaged;
	float nextDetectTime;
	float nextAttackTime;
	int lastPattern = -1;
	// 모으는 중인 낙하. Slam 연출 뒤 시간이 되면 그림자를 만든다
	bool dropPending;
	float dropTime;
	int facing = 1;

	/// <summary>
	/// [연출] 패킷 낙하를 시작할 때 불린다(Slam 모션). 그림자는 Slam Delay초 뒤에 나타난다.
	/// </summary>
	public event Action SlamEffectPlayed;

	/// <summary>
	/// 지금 바라보는(던질) 플레이어. 공격 범위 안에 아무도 없으면 null.
	/// </summary>
	public PlayerHealth Target => target;

	/// <summary>
	/// 바라보는 방향. 오른쪽 1, 왼쪽 -1.
	/// </summary>
	public int Facing => facing;

	void Awake()
	{
		monster = GetComponent<Monster>();
		status = GetComponent<StatusEffects>();
		thrower = GetComponent<ProjectileAttack>();
		spriteRenderer = GetComponent<SpriteRenderer>();

		targetFilter.SetLayerMask(targetLayers);
		targetFilter.useTriggers = false;
	}

	void Update()
	{
		// 패턴은 서버(또는 네트워크 없는 테스트)에서만 돈다. 죽거나 기절하면 공격하지 않는다
		if (!IsAuthority() || monster.IsDead || (status != null && status.IsStunned))
		{
			dropPending = false;
			return;
		}

		float now = Time.time;
		if (dropPending && now >= dropTime)
		{
			dropPending = false;
			SpawnDrops();
		}

		if (now >= nextDetectTime)
		{
			nextDetectTime = now + detectInterval;
			FindPlayers();
		}
		// 탐지 사이에 대상이 죽었으면 바로 놓는다
		if (target != null && target.IsDead) target = null;
		if (target == null)
		{
			engaged = false;
			return;
		}

		Face(target.transform.position.x > transform.position.x ? 1 : -1);

		// 처음 발견하면 잠깐 기다렸다가 공격을 시작한다
		if (!engaged)
		{
			engaged = true;
			nextAttackTime = now + firstAttackDelay;
		}
		if (now < nextAttackTime || dropPending || thrower.IsCharging) return;

		nextAttackTime = now + attackInterval;
		UsePattern(PickPattern());
	}

	// 공격 범위 안의 살아 있는 플레이어를 모으고, 가장 가까운 플레이어를 target으로 둔다
	void FindPlayers()
	{
		players.Clear();
		target = null;
		Vector2 pos = transform.position;
		int count = ScenePhysics().OverlapCircle(pos, detectRange, targetFilter, found);
		float bestSqr = float.MaxValue;
		for (int i = 0; i < count; i++)
		{
			PlayerHealth player = found[i].GetComponentInParent<PlayerHealth>();
			if (player == null || player.IsDead || players.Contains(player)) continue;
			players.Add(player);

			float sqr = ((Vector2)player.transform.position - pos).sqrMagnitude;
			if (sqr < bestSqr)
			{
				bestSqr = sqr;
				target = player;
			}
		}
	}

	// 목록에서 랜덤으로 고르되, 공격이 2개 이상이면 직전 공격은 빼고 고른다
	Pattern PickPattern()
	{
		int n = patterns.Length;
		int index = UnityEngine.Random.Range(0, n);
		if (n > 1 && index == lastPattern) index = (index + UnityEngine.Random.Range(1, n)) % n;
		lastPattern = index;
		return patterns[index];
	}

	void UsePattern(Pattern pattern)
	{
		switch (pattern)
		{
			case Pattern.Throw:
				// 수평으로 던진다. 높이는 ProjectileAttack의 Fire Point Offset으로 맞춘다
				thrower.Fire(new Vector2(facing, 0f));
				break;

			case Pattern.Drop:
				PlaySlamEffect();
				dropPending = true;
				dropTime = Time.time + slamDelay;
				break;
		}
	}

	// 플레이어마다 발밑 땅에, 그리고 구역 안 랜덤한 땅에 떨어뜨린다
	void SpawnDrops()
	{
		if (fallingPacketPrefab == null) return;

		for (int i = 0; i < players.Count; i++)
		{
			// 플레이어 발(Pivot)보다 조금 위에서 아래로 땅을 찾는다
			if (TryFindGround(players[i].transform.position + Vector3.up * 0.1f, 50f, out Vector2 ground)) SpawnPacket(ground);
		}

		Vector2 center = transform.position;
		Vector2 half = dropArea * 0.5f;
		for (int i = 0; i < extraDrops; i++)
		{
			// 구역 맨 위에서 아래로 첫 땅(윗면)을 찾는다
			Vector2 start = new Vector2(center.x + UnityEngine.Random.Range(-half.x, half.x), center.y + half.y);
			if (TryFindGround(start, dropArea.y, out Vector2 ground)) SpawnPacket(ground);
		}
	}

	bool TryFindGround(Vector2 from, float distance, out Vector2 ground)
	{
		RaycastHit2D hit = ScenePhysics().Raycast(from, Vector2.down, distance, groundLayers);
		ground = hit.point;
		return hit;
	}

	// 떨어지는 패킷을 만드는 곳은 여기 하나다 (코드 작성 규칙 3)
	void SpawnPacket(Vector2 ground) =>
		NetworkSpawner.Spawn(fallingPacketPrefab.gameObject, ground, Quaternion.identity, gameObject.scene);

	// 서버이거나 네트워크를 켜지 않은 테스트면 true
	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;

	// 내가 속한 장소 씬의 물리 세계. 저장하지 않고 쓸 때마다 가져온다
	PhysicsScene2D ScenePhysics() => gameObject.scene.GetPhysicsScene2D();

	void Face(int dir)
	{
		if (dir == facing) return;
		facing = dir;
		ShowFacing();
	}

	// 아래 함수들은 연출만 한다. 네트워크를 붙이면 [ClientRpc]를 단다 (코드 작성 규칙 1)
	// 그림은 오른쪽을 보고 그린다고 정하고, 왼쪽이면 뒤집는다
	void ShowFacing()
	{
		if (spriteRenderer != null) spriteRenderer.flipX = facing < 0;
	}

	void PlaySlamEffect() => SlamEffectPlayed?.Invoke();

	void OnDrawGizmosSelected()
	{
		Vector3 pos = transform.position;
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(pos, detectRange);
		Gizmos.color = Color.magenta;
		Gizmos.DrawWireCube(pos, dropArea);
	}
}
