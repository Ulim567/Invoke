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
/// - 웜 소환(Summon): SummonEffectPlayed(연출, Throw 모션)가 먼저 나가고 Summon Delay초 뒤 보스 양옆 땅 위에 웜을 만든다.
///   웜은 땅을 기어 플레이어를 쫓는 몬스터(GroundChaseAI + ContactDamage)다. 살아 있는 웜이 Max Worms 이상이면 고르지 않는다.<br/>
/// - 때리기(Smash): 바라보는 쪽 Smash Range 안에 플레이어가 있을 때만 고른다. SlamEffectPlayed(연출)가 먼저 나가고 Smash Delay초 뒤 몸 앞 구역(Smash Size, 아래 변은 보스 아래 땅)의 플레이어에게 Smash Attack을 적용한다.<br/>
/// - 공격 범위(Detect Range) 안에 살아 있는 플레이어가 없으면 공격하지 않는다.
/// </para>
/// <para>
/// 사용법: Gateway 프리팹 최상위에 Monster(Is Boss 체크), ProjectileAttack과 함께 붙인다. 움직이지 않으므로 Rigidbody2D는 필요 없다.<br/>
/// 상태 이상(같은 오브젝트의 StatusEffects, 없으면 무시): 기절 중에는 공격을 고르지 않는다. Pause(초)를 부르면 그동안 공격을 멈추고, Resume()으로 바로 풀 수 있다(페이즈 전환, 분신 기믹).<br/>
/// 네트워크를 붙이면 패턴은 서버에서만 돌린다. 판정은 보스가 속한 씬의 물리 세계에 한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster), typeof(ProjectileAttack))]
public class GatewayAI : MonoBehaviour
{
	/// <summary>
	/// Gateway의 공격 종류. 기믹은 GatewayPhases가 맡는다.
	/// </summary>
	public enum Pattern
	{
		Throw,  // 패킷 던지기
		Drop,   // 패킷 낙하
		Summon, // 웜 소환
		Smash,  // 때리기 (근접)
	}

	[Header("패턴")]
	[SerializeField, Tooltip("쓸 공격 목록. 이 안에서 랜덤으로 고른다")]
	Pattern[] patterns = { Pattern.Throw, Pattern.Drop, Pattern.Summon, Pattern.Smash };

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

	[Header("웜 소환")]
	[SerializeField, Tooltip("웜 프리팹 (Monster + GroundChaseAI + ContactDamage). NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	Monster wormPrefab;

	[SerializeField, Min(0f), Tooltip("소환 연출이 시작되고 웜이 나오기까지 시간(초). Throw 모션에서 내보내는 프레임에 맞춘다")]
	float summonDelay = 0.58f;

	[SerializeField, Min(1), Tooltip("한 번에 내보내는 웜 수. 보스 양옆에 번갈아 나온다")]
	int wormsPerSummon = 2;

	[SerializeField, Min(1), Tooltip("동시에 살아 있을 수 있는 웜 수. 이보다 많으면 소환을 고르지 않는다")]
	int maxWorms = 4;

	[SerializeField, Min(0f), Tooltip("보스 위치에서 웜이 나오는 가로 거리 (유닛). 양옆으로 이만큼 떨어진 땅 위에 나온다")]
	float wormSpawnDistance = 4.5f;

	[Header("때리기")]
	[SerializeField, Tooltip("때리기 공격 데이터 (체력 피해 + 실행 중단)")]
	MonsterAttackData smashAttack;

	[SerializeField, Min(0f), Tooltip("보스 몸 앞쪽 끝에서 이 거리 안에 플레이어가 있어야 때리기를 고른다 (유닛)")]
	float smashRange = 6f;

	[SerializeField, Min(0f), Tooltip("Slam 연출이 시작되고 내려치기까지 시간(초). Slam 6번째 장면")]
	float smashDelay = 0.42f;

	[SerializeField, Tooltip("때리는 구역 크기. 보스 몸 앞쪽에 붙고 아래 변이 보스 발 높이")]
	Vector2 smashSize = new Vector2(6f, 4f);

	Monster monster;
	Collider2D bodyCollider;
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
	// 이 시각까지 공격을 멈춘다 (페이즈 전환 등)
	float pausedUntil;
	// 모으는 중인 웜 소환·때리기
	bool summonPending;
	float summonTime;
	bool smashPending;
	float smashTime;
	int smashDir;
	// 살아 있는 웜. 소환 수 제한에 쓴다
	readonly List<Monster> worms = new List<Monster>(4);
	readonly HashSet<GameObject> smashed = new HashSet<GameObject>();

	/// <summary>
	/// [연출] 웜 소환을 시작할 때 불린다(Throw 모션). 웜은 Summon Delay초 뒤에 나온다.
	/// </summary>
	public event Action SummonEffectPlayed;

	/// <summary>
	/// [연출] 패킷 낙하를 시작할 때 불린다(Slam 모션). 그림자는 Slam Delay초 뒤에 나타난다.
	/// </summary>
	public event Action SlamEffectPlayed;

	/// <summary>
	/// seconds초 동안 공격을 멈춘다. 모으는 중인 낙하는 취소된다. 페이즈 전환(GatewayPhases)이 부른다. 서버에서만 부른다.
	/// </summary>
	public void Pause(float seconds)
	{
		pausedUntil = Mathf.Max(pausedUntil, Time.time + seconds);
		// 멈춤이 끝나면 바로 공격하지 않고 한 간격을 기다린다
		nextAttackTime = Mathf.Max(nextAttackTime, pausedUntil + attackInterval);
	}

	/// <summary>
	/// Pause로 멈춘 것을 지금 풀고, 한 간격 뒤부터 다시 공격한다. 서버에서만 부른다.
	/// </summary>
	public void Resume()
	{
		pausedUntil = Time.time;
		nextAttackTime = Time.time + attackInterval;
	}

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
		bodyCollider = GetComponent<Collider2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();

		targetFilter.SetLayerMask(targetLayers);
		targetFilter.useTriggers = false;
	}

	void Update()
	{
		// 패턴은 서버(또는 네트워크 없는 테스트)에서만 돈다. 죽거나 기절하면 공격하지 않는다
		float now = Time.time;
		if (!IsAuthority() || monster.IsDead || (status != null && status.IsStunned) || now < pausedUntil)
		{
			// 모으던 공격은 취소한다
			dropPending = summonPending = smashPending = false;
			return;
		}

		if (dropPending && now >= dropTime)
		{
			dropPending = false;
			SpawnDrops();
		}
		if (summonPending && now >= summonTime)
		{
			summonPending = false;
			SpawnWorms();
		}
		if (smashPending && now >= smashTime)
		{
			smashPending = false;
			Smash();
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
		if (now < nextAttackTime || dropPending || summonPending || smashPending || thrower.IsCharging) return;

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

	// 목록에서 랜덤으로 고르되, 직전 공격과 지금 쓸 수 없는 공격(가까운 플레이어 없는 때리기, 웜이 가득 찬 소환)은 뺀다
	Pattern PickPattern()
	{
		int n = patterns.Length;
		int start = UnityEngine.Random.Range(0, n);
		int fallback = -1;
		for (int k = 0; k < n; k++)
		{
			int index = (start + k) % n;
			if (!CanUse(patterns[index])) continue;
			if (fallback < 0) fallback = index;
			if (n > 1 && index == lastPattern) continue;
			lastPattern = index;
			return patterns[index];
		}
		// 쓸 수 있는 공격이 직전 공격뿐이면 그것을, 그것도 없으면 던지기를 쓴다
		if (fallback >= 0)
		{
			lastPattern = fallback;
			return patterns[fallback];
		}
		return Pattern.Throw;
	}

	bool CanUse(Pattern pattern)
	{
		switch (pattern)
		{
			case Pattern.Summon: return wormPrefab != null && AliveWorms() < maxWorms;
			case Pattern.Smash: return smashAttack != null && TargetInSmashRange();
			default: return true;
		}
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

			case Pattern.Summon:
				PlaySummonEffect();
				summonPending = true;
				summonTime = Time.time + summonDelay;
				break;

			case Pattern.Smash:
				PlaySlamEffect();
				smashPending = true;
				smashTime = Time.time + smashDelay;
				// 내려치기 시작한 쪽을 기억한다 (그사이 돌아봐도 처음 쪽을 친다)
				smashDir = facing;
				break;
		}
	}

	// ── 웜 소환 ──

	// 보스 양옆에 번갈아, 그 아래 땅 위에 웜을 만든다 (웜 그림 Pivot은 발밑)
	void SpawnWorms()
	{
		Vector2 boss = transform.position;
		for (int i = 0; i < wormsPerSummon && AliveWorms() < maxWorms; i++)
		{
			int side = i % 2 == 0 ? 1 : -1;
			Vector2 from = new Vector2(boss.x + side * wormSpawnDistance, boss.y + 5f);
			if (!TryFindGround(from, 40f, out Vector2 ground)) continue;

			GameObject obj = NetworkSpawner.Spawn(wormPrefab.gameObject, ground, Quaternion.identity, gameObject.scene);
			if (obj != null) worms.Add(obj.GetComponent<Monster>());
		}
	}

	// 죽었거나 사라진 웜을 목록에서 빼고 살아 있는 수를 센다
	int AliveWorms()
	{
		worms.RemoveAll(w => w == null || w.IsDead);
		return worms.Count;
	}

	// ── 때리기 ──

	// 바라보는 쪽, 몸 앞쪽 끝에서 Smash Range 안에 목표 플레이어가 있으면 true
	bool TargetInSmashRange()
	{
		if (target == null) return false;
		float front = transform.position.x + facing * BodyHalfWidth();
		float dx = (target.transform.position.x - front) * facing;
		return dx >= -BodyHalfWidth() && dx <= smashRange;
	}

	// 몸 앞 구역 안의 플레이어에게 한 번씩 때리기 공격을 적용한다
	void Smash()
	{
		Bounds b = bodyCollider != null ? bodyCollider.bounds : new Bounds(transform.position, Vector3.zero);
		Vector2 center = new Vector2(b.center.x + smashDir * (b.extents.x + smashSize.x * 0.5f), SmashFloor(b) + smashSize.y * 0.5f);
		int count = ScenePhysics().OverlapBox(center, smashSize, 0f, targetFilter, found);
		smashed.Clear();
		for (int i = 0; i < count; i++)
		{
			Collider2D hit = found[i];
			GameObject owner = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
			if (smashed.Add(owner)) MonsterAttackResolver.Apply(smashAttack, hit);
		}
	}

	// 때리기 구역의 아래 변 = 보스 아래 땅. 땅을 못 찾으면 몸 판정 바닥 (보스가 땅에서 떠 있어도 바닥의 플레이어를 친다)
	float SmashFloor(Bounds b) =>
		TryFindGround(new Vector2(b.center.x, b.center.y), 30f, out Vector2 ground) ? ground.y : b.min.y;

	float BodyHalfWidth() => bodyCollider != null ? bodyCollider.bounds.extents.x : 0f;

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

	void PlaySummonEffect() => SummonEffectPlayed?.Invoke();

	void OnDrawGizmosSelected()
	{
		Vector3 pos = transform.position;
		Gizmos.color = Color.red;
		Gizmos.DrawWireSphere(pos, detectRange);
		Gizmos.color = Color.magenta;
		Gizmos.DrawWireCube(pos, dropArea);

		// 때리기 구역 (바라보는 쪽)
		Collider2D col = GetComponent<Collider2D>();
		if (col != null)
		{
			Bounds b = col.bounds;
			Vector3 c = new Vector3(b.center.x + facing * (b.extents.x + smashSize.x * 0.5f), SmashFloor(b) + smashSize.y * 0.5f, 0f);
			Gizmos.color = new Color(1f, 0.3f, 0f);
			Gizmos.DrawWireCube(c, smashSize);
		}
	}
}
