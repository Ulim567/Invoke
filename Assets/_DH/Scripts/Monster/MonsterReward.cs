using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 몬스터가 죽으면 근처 플레이어에게 경험치·돈·재료(드롭)를 준다. 드롭은 땅에 떨어지지 않고 바로 가방(PlayerProgress)에 들어간다.
/// <para>
/// - 받는 플레이어: 몬스터가 죽은 자리에서 Reward Range 안에 있는 살아 있는 플레이어 모두. 때린 사람을 따로 세지 않는다.<br/>
///   Whole Scene을 켜면(레이드 보스) 같은 씬의 플레이어 모두에게 준다. 죽어서 관전 중인 파티원도 받는다.<br/>
/// - 재료는 몬스터가 죽을 때 한 번 굴려서, 받는 플레이어 모두에게 같은 것을 준다. 확률(Chance) 1이면 반드시 나온다.<br/>
/// - 값은 PlayerProgress(이우림)의 AddExp, AddMoney, AddMaterial로만 바꾼다. PlayerProgress가 없는 플레이어(테스트용 프리팹)는 건너뛴다.<br/>
/// - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 준다. 판정은 몬스터가 속한 씬의 물리 세계에 한다.<br/>
/// - 연출 이벤트(RewardEffectPlayed)는 [TargetRpc]로 받은 플레이어의 화면(호스트 포함)에서만 불린다 (#404). 몬스터는 Despawn Delay(0.5초) 뒤에 없어지므로,
///   연출 쪽은 띄운 그림을 몬스터의 자식으로 두지 않는다.
/// </para>
/// <para>
/// 사용법: 보상을 줄 몬스터 프리팹(Leak, Loop, Gateway) 최상위에 Monster와 함께 붙인다. Player Layers는 Player로 둔다.
/// NetworkBehaviour라 프리팹에 NetworkIdentity가 있어야 한다.
/// 기둥·분신·웜처럼 보상이 없는 몬스터에는 붙이지 않는다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterReward : NetworkBehaviour
{
	/// <summary>
	/// 재료 드롭 한 줄. 재료 ID(bug_fragment 버그 조각, boss_core 보스 코어), 개수, 나올 확률.
	/// </summary>
	[Serializable]
	public struct MaterialDrop
	{
		[Tooltip("재료 ID. bug_fragment(버그 조각), boss_core(보스 코어)")]
		public string materialId;

		[Min(1), Tooltip("나오는 개수")]
		public int count;

		[Range(0f, 1f), Tooltip("나올 확률 (1이면 반드시)")]
		public float chance;
	}

	[Header("보상")]
	[SerializeField, Min(0), Tooltip("받는 플레이어마다 주는 경험치")]
	int exp = 10;

	[SerializeField, Min(0), Tooltip("받는 플레이어마다 주는 돈")]
	int money = 5;

	[SerializeField, Tooltip("재료 드롭 목록. 줄마다 따로 확률을 굴린다")]
	MaterialDrop[] drops = { new MaterialDrop { materialId = "bug_fragment", count = 1, chance = 0.5f } };

	[Header("받는 플레이어")]
	[SerializeField, Min(0f), Tooltip("몬스터가 죽은 자리에서 이 거리 안의 살아 있는 플레이어가 받는다 (유닛)")]
	float rewardRange = 20f;

	[SerializeField, Tooltip("플레이어 레이어. Player로 둔다")]
	LayerMask playerLayers;

	[SerializeField, Tooltip("켜면 거리와 생사에 상관없이 같은 씬의 플레이어 모두가 받는다 (레이드 보스)")]
	bool wholeScene;

	// 판정에 쓰는 목록은 모든 몬스터가 함께 쓴다 (죽을 때 한 번씩만 쓰고, 메인 스레드라 겹치지 않음)
	static readonly List<Collider2D> found = new List<Collider2D>(16);
	static readonly List<GameObject> roots = new List<GameObject>(64);
	static readonly HashSet<PlayerProgress> rewarded = new HashSet<PlayerProgress>();

	Monster monster;
	ContactFilter2D playerFilter;
	// 이번에 나온 재료. 받는 플레이어 모두에게 같은 것을 준다
	readonly List<MaterialDrop> rolled = new List<MaterialDrop>(4);
	// 클라이언트가 받은 재료 (TargetRewardEffect). 이벤트 안에서만 읽는다
	readonly List<MaterialDrop> received = new List<MaterialDrop>(4);

	/// <summary>
	/// [연출] 내 캐릭터가 보상을 받았을 때. (받은 플레이어, 경험치, 돈, 받은 재료 목록)을 넘긴다. "+경험치" 글자, 재료가 날아오는 연출에 쓴다.<br/>
	/// 받은 플레이어의 화면에서만 불린다 (서버가 [TargetRpc]로 보냄, 호스트 포함). 받은 플레이어는 그 화면의 내 캐릭터다.
	/// 네트워크를 켜지 않은 테스트에서는 받은 플레이어마다 그 자리에서 불린다.
	/// 재료 목록은 이 이벤트 안에서만 읽는다(다음 처치 때 바뀐다).
	/// </summary>
	public event Action<PlayerProgress, int, int, IReadOnlyList<MaterialDrop>> RewardEffectPlayed;

	void Awake()
	{
		monster = GetComponent<Monster>();
		playerFilter.SetLayerMask(playerLayers);
		playerFilter.useTriggers = true;
		if (!wholeScene && playerLayers.value == 0)
			Debug.LogWarning($"[MonsterReward] {name}: Player Layers가 비어 있어 보상을 받을 플레이어를 찾지 못합니다. Player로 두세요.", this);
	}

	void OnEnable() => monster.Died += OnDied;

	void OnDisable() => monster.Died -= OnDied;

	// Died는 로직 이벤트라 서버에서만 불린다
	void OnDied()
	{
		if (!IsAuthority()) return;

		RollDrops();
		rewarded.Clear();
		if (wholeScene) RewardWholeScene();
		else RewardNearby();
		rewarded.Clear();
	}

	void RollDrops()
	{
		rolled.Clear();
		for (int i = 0; i < drops.Length; i++)
		{
			MaterialDrop d = drops[i];
			if (string.IsNullOrEmpty(d.materialId) || d.count <= 0) continue;
			// Random.value는 0 이상 1 이하라 확률 1은 늘 나오고 0은 안 나온다
			if (d.chance >= 1f || UnityEngine.Random.value < d.chance) rolled.Add(d);
		}
	}

	// 죽은 자리 근처의 살아 있는 플레이어
	void RewardNearby()
	{
		int count = gameObject.scene.GetPhysicsScene2D().OverlapCircle(transform.position, rewardRange, playerFilter, found);
		for (int i = 0; i < count; i++)
		{
			PlayerProgress player = found[i].GetComponentInParent<PlayerProgress>();
			if (player == null || rewarded.Contains(player)) continue;

			PlayerHealth health = player.GetComponentInChildren<PlayerHealth>();
			if (health != null && health.IsDead) continue;
			Give(player);
		}
	}

	// 같은 씬의 플레이어 모두 (플레이어는 씬 최상위 오브젝트). 죽은 파티원도 받는다
	void RewardWholeScene()
	{
		gameObject.scene.GetRootGameObjects(roots);
		for (int i = 0; i < roots.Count; i++)
			if (roots[i].TryGetComponent(out PlayerProgress player) && !rewarded.Contains(player)) Give(player);
		roots.Clear();
	}

	// 값은 PlayerProgress만 바꾼다 (코드 작성 규칙 2)
	void Give(PlayerProgress player)
	{
		rewarded.Add(player);
		player.AddExp(exp);
		player.AddMoney(money);
		for (int i = 0; i < rolled.Count; i++) player.AddMaterial(rolled[i].materialId, rolled[i].count);
		PlayRewardEffect(player);
	}

	// 연출만 한다 (코드 작성 규칙 1). 받은 플레이어의 화면에만 보낸다. 네트워크를 켜지 않은 테스트는 [TargetRpc]를 보낼 수 없으므로 그 자리에서 부른다
	void PlayRewardEffect(PlayerProgress player)
	{
		if (!NetworkServer.active)
		{
			RewardEffectPlayed?.Invoke(player, exp, money, rolled);
			return;
		}

		NetworkConnectionToClient conn = player.TryGetComponent(out NetworkIdentity identity) ? identity.connectionToClient : null;
		if (conn == null) return;

		string[] ids = new string[rolled.Count];
		int[] counts = new int[rolled.Count];
		for (int i = 0; i < rolled.Count; i++)
		{
			ids[i] = rolled[i].materialId;
			counts[i] = rolled[i].count;
		}
		TargetRewardEffect(conn, exp, money, ids, counts);
	}

	// 받은 플레이어의 클라이언트(호스트 포함)에서 불린다. 몬스터를 없애기 전에 보내므로 몬스터가 아직 이 화면에 있다
	[TargetRpc]
	void TargetRewardEffect(NetworkConnectionToClient target, int expGained, int moneyGained, string[] ids, int[] counts)
	{
		NetworkIdentity me = NetworkClient.localPlayer;
		PlayerProgress receiver = me != null ? me.GetComponent<PlayerProgress>() : null;

		received.Clear();
		int n = ids != null && counts != null ? Mathf.Min(ids.Length, counts.Length) : 0;
		for (int i = 0; i < n; i++) received.Add(new MaterialDrop { materialId = ids[i], count = counts[i], chance = 1f });
		RewardEffectPlayed?.Invoke(receiver, expGained, moneyGained, received);
	}

	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;

	void OnDrawGizmosSelected()
	{
		if (wholeScene) return;
		Gizmos.color = new Color(1f, 0.85f, 0.2f);
		Gizmos.DrawWireSphere(transform.position, rewardRange);
	}
}
