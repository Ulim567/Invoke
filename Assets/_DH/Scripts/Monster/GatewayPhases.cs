using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 레이드 보스 Gateway의 페이즈 전환과 기믹. 기본 공격은 같은 오브젝트의 GatewayAI가 하고, 이 컴포넌트는 페이즈와 기믹만 맡는다.
/// <para>
/// 페이즈 (체력 비율)<br/>
/// - 1페이즈 100~70%: 플레이어를 처음 발견하고 First Gimmick Delay초 뒤 기믹 1(기둥 파괴)을 한 번<br/>
/// - 2페이즈 70% 이하: 들어오는 순간 Transition Pause초 멈췄다가 기믹 2(전체 공격)를 한 번<br/>
/// - 3페이즈 40% 이하: 같은 방식으로 기믹 3(진짜 찾기)을 한 번 (기믹 3 이슈에서 만든다)<br/>
/// - 기믹은 페이즈마다 한 번만 나온다. 한 번에 여러 문턱을 넘으면 가장 낮은 페이즈로 바로 간다.
/// </para>
/// <para>
/// 기믹 1: 기둥 파괴<br/>
/// - 보스 기준 Pillar Offsets(X) 위치의 땅에 기둥(Monster)을 세운다. 기둥은 스킬로 때려 부술 수 있다. 보스는 그동안 기본 공격을 계속한다.<br/>
/// - Pillar Time Limit초 안에 모두 부수면 성공: 보스에게 기절 Groggy Duration초(그로기, StatusEffects). GroggyEffectPlayed(연출).<br/>
/// - 시간이 다 되면 실패: 남은 기둥이 터지고 기믹 2(전체 공격)로 이어진다.
/// </para>
/// <para>
/// 기믹 2: 전체 공격<br/>
/// - 보스가 Charge Time초 동안 기를 모은다(ChargeEffectPlayed, 그동안 기본 공격 멈춤). 보스 왼쪽·오른쪽에 안전지대(SafeZone)를 하나씩 땅 위에 만든다.<br/>
/// - 시간이 되면 안전지대 밖의 같은 씬 살아 있는 플레이어 모두에게 Overload Attack을 적용하고(OverloadEffectPlayed), 안전지대를 없앤다.
/// </para>
/// <para>
/// 사용법: Gateway 프리팹 최상위에 Monster, GatewayAI, StatusEffects와 함께 붙인다. 기둥 프리팹은 NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다.<br/>
/// 네트워크를 붙이면 서버에서만 돈다. 땅 찾기는 보스가 속한 씬의 물리 세계에 한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster), typeof(GatewayAI), typeof(StatusEffects))]
public class GatewayPhases : MonoBehaviour
{
	[Header("페이즈")]
	[SerializeField, Tooltip("다음 페이즈로 넘어가는 체력 비율. 큰 값부터 (2페이즈 0.7, 3페이즈 0.4)")]
	float[] phaseThresholds = { 0.7f, 0.4f };

	[SerializeField, Min(0f), Tooltip("페이즈가 바뀔 때 보스가 멈추는 시간(초)")]
	float transitionPause = 1.5f;

	[SerializeField, Min(0f), Tooltip("플레이어를 처음 발견하고 기믹 1을 시작하기까지 시간(초)")]
	float firstGimmickDelay = 15f;

	[Header("기믹 1: 기둥 파괴")]
	[SerializeField, Tooltip("세울 기둥 프리팹 (Monster). NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	Monster pillarPrefab;

	[SerializeField, Tooltip("보스 위치에서 기둥을 세울 가로 거리들 (유닛). 개수만큼 세운다")]
	float[] pillarOffsets = { -15f, -8f, 15f };

	[SerializeField, Min(1f), Tooltip("기둥을 모두 부숴야 하는 제한 시간(초)")]
	float pillarTimeLimit = 15f;

	[SerializeField, Min(0f), Tooltip("성공했을 때 보스 그로기(기절) 시간(초)")]
	float groggyDuration = 6f;

	[Header("기믹 2: 전체 공격")]
	[SerializeField, FormerlySerializedAs("failAttack"), Tooltip("전체 공격. 안전지대 밖의 같은 씬 플레이어 모두에게 적용한다")]
	MonsterAttackData overloadAttack;

	[SerializeField, Tooltip("안전지대 프리팹 (SafeZone). NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	SafeZone safeZonePrefab;

	[SerializeField, Min(0.5f), Tooltip("기를 모으는 시간(초). 이 동안 안전지대로 들어가야 한다")]
	float chargeTime = 3f;

	[SerializeField, Tooltip("보스에서 안전지대 중심까지 가로 거리 범위 (유닛). x 최소, y 최대. 왼쪽·오른쪽에 하나씩 이 범위에서 랜덤")]
	Vector2 safeZoneDistance = new Vector2(6f, 15f);

	[Header("공통")]
	[SerializeField, Tooltip("기둥·안전지대를 놓을 땅 레이어. Ground, Platform으로 둔다")]
	LayerMask groundLayers;

	Monster monster;
	GatewayAI ai;
	StatusEffects status;

	int phase = 1;
	// 페이즈마다 기믹을 이미 했으면 true (인덱스 = 페이즈 - 1)
	readonly bool[] gimmickDone = new bool[3];
	bool engaged;
	float firstGimmickTime;
	// 페이즈 전환으로 멈춘 뒤 기믹을 시작할 시각. 0이면 기다리는 기믹 없음
	float pendingGimmickTime;

	// 기믹 2 진행 상태
	readonly List<SafeZone> safeZones = new List<SafeZone>(2);
	bool overloadActive;
	float overloadTime;

	// 기믹 1 진행 상태
	readonly List<Monster> pillars = new List<Monster>(3);
	bool pillarsActive;
	float pillarEndTime;

	/// <summary>
	/// 지금 페이즈 (1, 2, 3).
	/// </summary>
	public int Phase => phase;

	/// <summary>
	/// [로직] 페이즈가 바뀐 순간 불린다. 새 페이즈 번호를 넘긴다.
	/// </summary>
	public event Action<int> PhaseChanged;

	/// <summary>
	/// [연출] 전체 공격을 위해 기를 모으기 시작할 때 불린다. Charge 모션에 쓴다. 모으는 시간(초)을 넘긴다.
	/// </summary>
	public event Action<float> ChargeEffectPlayed;

	/// <summary>
	/// [연출] 전체 공격이 터질 때 불린다. 화면 번쩍임, 흔들림, 소리에 쓴다.
	/// </summary>
	public event Action OverloadEffectPlayed;

	/// <summary>
	/// [연출] 기둥을 모두 부숴 보스가 그로기(기절)에 들어갈 때 불린다. Groggy 모션에 쓴다. 그로기 시간(초)을 넘긴다.
	/// </summary>
	public event Action<float> GroggyEffectPlayed;

	void Awake()
	{
		monster = GetComponent<Monster>();
		ai = GetComponent<GatewayAI>();
		status = GetComponent<StatusEffects>();
	}

	void OnEnable()
	{
		monster.Damaged += OnDamaged;
		monster.Died += OnDied;
	}

	void OnDisable()
	{
		monster.Damaged -= OnDamaged;
		monster.Died -= OnDied;
	}

	void Update()
	{
		if (!IsAuthority() || monster.IsDead) return;
		float now = Time.time;

		// 1페이즈 기믹: 처음 발견하고 일정 시간 뒤 한 번
		if (!engaged && ai.Target != null)
		{
			engaged = true;
			firstGimmickTime = now + firstGimmickDelay;
		}
		if (engaged && phase == 1 && !gimmickDone[0] && now >= firstGimmickTime) StartGimmick(1);

		// 페이즈 전환으로 멈춘 뒤 그 페이즈 기믹
		if (pendingGimmickTime > 0f && now >= pendingGimmickTime)
		{
			pendingGimmickTime = 0f;
			if (!gimmickDone[phase - 1]) StartGimmick(phase);
		}

		if (pillarsActive) UpdatePillars(now);
		if (overloadActive && now >= overloadTime) ReleaseOverload();
	}

	// 체력이 문턱을 넘으면 다음 페이즈로 간다 (Damaged는 로직 event라 서버에서만 불린다)
	void OnDamaged(int damage)
	{
		if (monster.IsDead || monster.MaxHp <= 0) return;
		float ratio = (float)monster.Hp / monster.MaxHp;

		int next = phase;
		for (int i = 0; i < phaseThresholds.Length; i++)
			if (ratio <= phaseThresholds[i]) next = Mathf.Max(next, i + 2);

		if (next > phase) EnterPhase(next);
	}

	void EnterPhase(int next)
	{
		phase = Mathf.Min(next, gimmickDone.Length);
		ai.Pause(transitionPause);
		pendingGimmickTime = Time.time + transitionPause;
		PhaseChanged?.Invoke(phase);
	}

	void StartGimmick(int forPhase)
	{
		gimmickDone[forPhase - 1] = true;
		switch (forPhase)
		{
			case 1: StartPillars(); break;
			// 기믹 3은 다음 이슈에서 만든다
			case 2: StartOverload(); break;
			case 3: Debug.Log("[GatewayPhases] 3페이즈 기믹(진짜 찾기)은 아직 없음"); break;
		}
	}

	// ── 기믹 1: 기둥 파괴 ──

	void StartPillars()
	{
		if (pillarPrefab == null) return;
		pillars.Clear();

		Vector2 boss = transform.position;
		PhysicsScene2D physics = gameObject.scene.GetPhysicsScene2D();
		for (int i = 0; i < pillarOffsets.Length; i++)
		{
			// 보스보다 높은 곳에서 아래로 땅을 찾아, 그 위에 세운다 (기둥 Pivot은 바닥)
			Vector2 from = new Vector2(boss.x + pillarOffsets[i], boss.y + 10f);
			RaycastHit2D hit = physics.Raycast(from, Vector2.down, 40f, groundLayers);
			if (!hit) continue;

			GameObject obj = NetworkSpawner.Spawn(pillarPrefab.gameObject, hit.point, Quaternion.identity, gameObject.scene);
			if (obj != null) pillars.Add(obj.GetComponent<Monster>());
		}

		pillarsActive = pillars.Count > 0;
		pillarEndTime = Time.time + pillarTimeLimit;
	}

	void UpdatePillars(float now)
	{
		int alive = 0;
		for (int i = 0; i < pillars.Count; i++)
			if (pillars[i] != null && !pillars[i].IsDead) alive++;

		if (alive == 0)
		{
			pillarsActive = false;
			Groggy();
		}
		else if (now >= pillarEndTime)
		{
			pillarsActive = false;
			BreakRemainingPillars();
			// 실패하면 전체 공격으로 이어진다
			StartOverload();
		}
	}

	// 성공: 보스 기절(그로기). GatewayAI는 기절 중에 공격하지 않는다
	void Groggy()
	{
		status.Apply(StatusType.Stun, groggyDuration);
		PlayGroggyEffect();
	}

	// 실패하면 남은 기둥은 터진다. 기둥을 없애는 일도 Monster를 거친다 (체력을 0으로)
	void BreakRemainingPillars()
	{
		for (int i = 0; i < pillars.Count; i++)
		{
			Monster p = pillars[i];
			if (p != null && !p.IsDead) p.TakeDamage(p.Hp);
		}
		pillars.Clear();
	}

	// ── 기믹 2: 전체 공격 ──

	void StartOverload()
	{
		if (overloadActive) return;
		overloadActive = true;
		overloadTime = Time.time + chargeTime;
		// 모으는 동안 기본 공격을 멈춘다. 터진 뒤 조금 더 쉰다
		ai.Pause(chargeTime + 0.5f);
		SpawnSafeZones();
		PlayChargeEffect();
	}

	// 보스 왼쪽·오른쪽에 하나씩, 땅 위에 안전지대를 만든다
	void SpawnSafeZones()
	{
		safeZones.Clear();
		if (safeZonePrefab == null) return;

		Vector2 boss = transform.position;
		PhysicsScene2D physics = gameObject.scene.GetPhysicsScene2D();
		for (int side = -1; side <= 1; side += 2)
		{
			float dx = UnityEngine.Random.Range(safeZoneDistance.x, Mathf.Max(safeZoneDistance.x, safeZoneDistance.y));
			Vector2 from = new Vector2(boss.x + side * dx, boss.y + 10f);
			RaycastHit2D hit = physics.Raycast(from, Vector2.down, 40f, groundLayers);
			if (!hit) continue;

			GameObject obj = NetworkSpawner.Spawn(safeZonePrefab.gameObject, hit.point, Quaternion.identity, gameObject.scene);
			if (obj != null) safeZones.Add(obj.GetComponent<SafeZone>());
		}
	}

	// 안전지대 밖의 같은 씬 살아 있는 플레이어 모두에게 전체 공격을 적용하고 안전지대를 없앤다
	void ReleaseOverload()
	{
		overloadActive = false;
		PlayOverloadEffect();

		if (overloadAttack != null)
		{
			PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
			for (int i = 0; i < players.Length; i++)
			{
				PlayerHealth p = players[i];
				if (p.IsDead || p.gameObject.scene != gameObject.scene || InSafeZone(p.transform.position)) continue;
				Collider2D body = p.GetComponentInChildren<Collider2D>();
				if (body != null) MonsterAttackResolver.Apply(overloadAttack, body);
			}
		}

		ClearSafeZones();
	}

	bool InSafeZone(Vector2 feet)
	{
		for (int i = 0; i < safeZones.Count; i++)
			if (safeZones[i] != null && safeZones[i].Contains(feet)) return true;
		return false;
	}

	// 안전지대를 없애는 곳은 여기 하나다 (코드 작성 규칙 3)
	void ClearSafeZones()
	{
		for (int i = 0; i < safeZones.Count; i++)
			if (safeZones[i] != null) NetworkSpawner.Despawn(safeZones[i].gameObject);
		safeZones.Clear();
	}

	// 보스가 죽으면 남은 기둥도 터뜨리고 기믹을 멈춘다
	void OnDied()
	{
		pillarsActive = false;
		pendingGimmickTime = 0f;
		BreakRemainingPillars();
		// 모으던 전체 공격은 취소한다
		overloadActive = false;
		ClearSafeZones();
	}

	// 연출만 한다. 네트워크를 붙이면 [ClientRpc]를 단다 (코드 작성 규칙 1)
	void PlayGroggyEffect() => GroggyEffectPlayed?.Invoke(groggyDuration);

	void PlayChargeEffect() => ChargeEffectPlayed?.Invoke(chargeTime);

	void PlayOverloadEffect() => OverloadEffectPlayed?.Invoke();

	// 서버이거나 네트워크를 켜지 않은 테스트면 true
	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;
}
