using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 몬스터가 죽으면 플레이어에게 경험치·돈·재료(드롭)를 준다. 드롭은 땅에 떨어지지 않고 바로 가방(PlayerProgress)에 들어간다.
/// <para>
/// - 받는 플레이어 (#484): 누적 피해량이 가장 큰 플레이어 한 명 (Monster.GetAttackerRanking). 같으면 먼저 때린 플레이어.<br/>
///   그 플레이어가 죽었거나 몬스터의 씬을 떠났으면 다음 순위. 받을 수 있는 플레이어가 없으면 아무도 받지 않는다.<br/>
///   Whole Scene을 켜면(레이드 보스) 같은 씬의 플레이어 모두에게 준다. 죽어서 관전 중인 파티원도 받는다.<br/>
/// - 재료는 몬스터가 죽을 때 한 번 굴린다. 확률(Chance) 1이면 반드시 나온다.<br/>
/// - 블록·함수 드롭: 받는 플레이어마다 Item Chance로 굴려서, 나오면 그 플레이어가 쓸 수 있는 항목(해금한 스킬 트리 노드가 여는 항목 + 그 캐릭터 언어의 처음 항목) 중
///   무작위로 Item Count개(하나씩 따로 고름)의 가진 개수를 1씩 늘린다 (ItemCountRules, 이우림 #500). 획득 표시(시안 8-5)에는 받은 항목의 이름까지 알린다(LocalRewardReceived의 항목 ID 목록).<br/>
///   받은 블록·함수는 재료처럼 블록 아이콘(ItemDropIconId)이 날아와 보인다. 아이콘은 무엇을 받았든 같다.<br/>
/// - 값은 PlayerProgress(이우림)의 AddExp, AddMoney, AddMaterial, AddItem으로만 바꾼다. PlayerProgress가 없는 플레이어(테스트용 프리팹)는 건너뛴다.<br/>
/// - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 준다. 판정은 몬스터가 속한 씬의 물리 세계에 한다.<br/>
/// - 연출 이벤트(RewardEffectPlayed)는 [TargetRpc]로 받은 플레이어의 화면(호스트 포함)에서만 불린다 (#404). 몬스터는 Despawn Delay(0.5초) 뒤에 없어지므로,
///   연출 쪽은 띄운 그림을 몬스터의 자식으로 두지 않는다.
/// </para>
/// <para>
/// 사용법: 보상을 줄 몬스터 프리팹(Leak, Loop, Gateway) 최상위에 Monster와 함께 붙인다.
/// NetworkBehaviour라 프리팹에 NetworkIdentity가 있어야 한다.
/// 기둥·분신·웜처럼 보상이 없는 몬스터에는 붙이지 않는다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterReward : NetworkBehaviour
{
	/// <summary>
	/// 재료 드롭 한 줄. 재료 ID(bug_fragment 버그 조각, boss_core 보스 코어), 개수(Count~Max Count 사이 무작위), 나올 확률.
	/// </summary>
	[Serializable]
	public struct MaterialDrop
	{
		[Tooltip("재료 ID. bug_fragment(버그 조각), boss_core(보스 코어)")]
		public string materialId;

		[Min(1), Tooltip("나오는 개수 (최소)")]
		public int count;

		[Min(0), Tooltip("나오는 개수 최대. Count보다 크면 Count~Max Count 사이에서 무작위, 아니면 늘 Count개")]
		public int maxCount;

		[Range(0f, 1f), Tooltip("나올 확률 (1이면 반드시)")]
		public float chance;
	}

	[Header("보상")]
	[SerializeField, Min(0), Tooltip("받는 플레이어마다 주는 경험치")]
	int exp = 10;

	[SerializeField, Min(0), Tooltip("받는 플레이어마다 주는 돈")]
	int money = 5;

	[SerializeField, Tooltip("재료 드롭 목록. 줄마다 따로 확률을 굴린다")]
	MaterialDrop[] drops = { new MaterialDrop { materialId = "bug_fragment", count = 2, maxCount = 3, chance = 1f } };

	[Header("블록·함수 드롭")]
	[SerializeField, Range(0f, 1f), Tooltip("받는 플레이어마다 블록·함수가 나올 확률 (1이면 반드시)")]
	float itemChance = 0.1f;

	[SerializeField, Min(0), Tooltip("나올 때 주는 블록·함수 수. 하나씩 따로 무작위로 고른다")]
	int itemCount = 1;

	[Header("받는 플레이어")]
	[SerializeField, Tooltip("켜면 피해량·생사에 상관없이 같은 씬의 플레이어 모두가 받는다 (레이드 보스). 끄면 가장 많이 때린 플레이어 한 명")]
	bool wholeScene;

	// 판정에 쓰는 목록은 모든 몬스터가 함께 쓴다 (죽을 때 한 번씩만 쓰고, 메인 스레드라 겹치지 않음)
	static readonly List<GameObject> ranking = new List<GameObject>(8);
	static readonly List<GameObject> roots = new List<GameObject>(64);
	static readonly HashSet<PlayerProgress> rewarded = new HashSet<PlayerProgress>();
	// 블록·함수 드롭 후보 (받는 플레이어마다 다시 채움)
	static readonly List<string> itemPool = new List<string>(32);
	static readonly HashSet<string> itemPoolSet = new HashSet<string>();
	// 새 캐릭터 처음 항목 (ItemCountRules.Initial의 키). 캐릭터 언어마다 다르므로(C: thrust…, Python: py.*) 언어별로 한 번씩만 센다
	static readonly Dictionary<string, string[]> initialItemsByLanguage = new Dictionary<string, string[]>(2);

	Monster monster;
	// 이번에 나온 재료. 받는 플레이어 모두에게 같은 것을 준다
	readonly List<MaterialDrop> rolled = new List<MaterialDrop>(4);
	// 클라이언트가 받은 재료 (TargetRewardEffect). 이벤트 안에서만 읽는다
	readonly List<MaterialDrop> received = new List<MaterialDrop>(4);
	// RewardEffectPlayed에 넘기는 목록: 재료 + 블록·함수 아이콘 한 줄 (EffectDrops). 이벤트 안에서만 읽는다
	readonly List<MaterialDrop> effectDrops = new List<MaterialDrop>(5);
	// 받는 플레이어 한 명이 이번에 받은 블록·함수의 항목 ID (받은 수만큼). 받는 플레이어마다 다시 채운다
	readonly List<string> givenItems = new List<string>(4);

	/// <summary>
	/// 블록·함수 드롭을 재료처럼 날아오게 보여 줄 때 쓰는 아이콘 ID. 진짜 재료가 아니어서 가방(PlayerProgress)에는 들어가지 않는다.<br/>
	/// 블록·함수가 나오면 RewardEffectPlayed의 재료 목록 끝에 (이 ID, 받은 수) 한 줄이 붙는다. MaterialDropView가 Resources/MaterialDrops/item_block 아이콘으로 띄운다.
	/// LocalRewardReceived의 재료 목록에는 붙지 않는다 (블록 수는 따로 넘긴다).
	/// </summary>
	public const string ItemDropIconId = "item_block";

	/// <summary>
	/// [연출] 내 캐릭터가 보상을 받았을 때. (받은 플레이어, 경험치, 돈, 받은 재료 목록)을 넘긴다. "+경험치" 글자, 재료가 날아오는 연출에 쓴다.<br/>
	/// 받은 플레이어의 화면에서만 불린다 (서버가 [TargetRpc]로 보냄, 호스트 포함). 받은 플레이어는 그 화면의 내 캐릭터다.
	/// 네트워크를 켜지 않은 테스트에서는 받은 플레이어마다 그 자리에서 불린다.
	/// 재료 목록은 이 이벤트 안에서만 읽는다(다음 처치 때 바뀐다).
	/// </summary>
	public event Action<PlayerProgress, int, int, IReadOnlyList<MaterialDrop>> RewardEffectPlayed;

	/// <summary>
	/// [연출] 내 캐릭터가 어떤 몬스터에게서든 보상을 받았을 때 (경험치, 재료 목록, 받은 블록·함수의 항목 ID 목록). 획득 표시(시안 8-5)처럼 몬스터와 상관없이 받는 화면이 쓴다.<br/>
	/// 항목 ID는 받은 수만큼 들어 있다 (같은 항목을 두 번 받으면 두 번). 이름은 ShopItemInfo.DisplayNameOf로 바꾼다.<br/>
	/// 받은 플레이어의 화면에서만 불린다. 두 목록은 이 이벤트 안에서만 읽는다.
	/// </summary>
	public static event Action<int, IReadOnlyList<MaterialDrop>, IReadOnlyList<string>> LocalRewardReceived;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		LocalRewardReceived = null;
		initialItemsByLanguage.Clear();
	}

	void Awake()
	{
		monster = GetComponent<Monster>();
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
		else RewardTopAttacker();
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
			if (d.chance < 1f && UnityEngine.Random.value >= d.chance) continue;
			// 개수도 여기서 정한다. 받는 플레이어 모두 같은 개수를 받는다
			if (d.maxCount > d.count) d.count = UnityEngine.Random.Range(d.count, d.maxCount + 1);
			rolled.Add(d);
		}
	}

	// 가장 많이 때린 플레이어 한 명 (#484). 죽었거나 다른 씬으로 떠난 플레이어는 건너뛰고 다음 순위에게 준다
	void RewardTopAttacker()
	{
		monster.GetAttackerRanking(ranking);
		for (int i = 0; i < ranking.Count; i++)
		{
			PlayerProgress player = ranking[i].GetComponentInParent<PlayerProgress>();
			if (player == null || player.gameObject.scene != gameObject.scene) continue;

			PlayerHealth health = player.GetComponentInChildren<PlayerHealth>();
			if (health != null && health.IsDead) continue;
			Give(player);
			break;
		}
		ranking.Clear();
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
		GiveItems(player);
		PlayRewardEffect(player);
	}

	// 블록·함수 드롭. 받은 항목 ID를 givenItems에 남긴다. 받는 사람마다 쓸 수 있는 항목이 달라서 사람마다 굴린다
	void GiveItems(PlayerProgress player)
	{
		givenItems.Clear();
		if (itemCount <= 0 || itemChance <= 0f) return;
		if (itemChance < 1f && UnityEngine.Random.value >= itemChance) return;

		FillItemPool(player);
		if (itemPool.Count == 0) return;
		for (int i = 0; i < itemCount; i++)
		{
			string itemId = itemPool[UnityEngine.Random.Range(0, itemPool.Count)];
			player.AddItem(itemId, 1);
			givenItems.Add(itemId);
		}
	}

	// 그 플레이어가 쓸 수 있는 항목: 처음 항목 + 해금한 노드가 여는 항목 (해금 안 한 블록은 받아도 저장할 때 막혀서 빼둔다)
	static void FillItemPool(PlayerProgress player)
	{
		itemPool.Clear();
		itemPoolSet.Clear();
		// 처음 항목은 그 플레이어의 캐릭터 언어 것만 (Python 마법사가 C 항목을 받던 문제)
		string language = PlayerCharacter.LanguageOf(player.gameObject);
		if (!initialItemsByLanguage.TryGetValue(language, out string[] initialItems))
		{
			var initial = ItemCountRules.Initial(language);
			initialItems = new string[initial.Count];
			initial.Keys.CopyTo(initialItems, 0);
			initialItemsByLanguage[language] = initialItems;
		}
		for (int i = 0; i < initialItems.Length; i++) AddToPool(initialItems[i]);

		IReadOnlyList<string> unlocked = player.Unlocked;
		for (int i = 0; i < unlocked.Count; i++)
		{
			if (!SkillTree.TryGet(unlocked[i], out SkillTreeNode node)) continue;
			List<string> keys = ItemCountRules.KeysUnlockedBy(node);
			for (int k = 0; k < keys.Count; k++) AddToPool(keys[k]);
		}
	}

	static void AddToPool(string itemId)
	{
		if (!string.IsNullOrEmpty(itemId) && itemPoolSet.Add(itemId)) itemPool.Add(itemId);
	}

	// 연출만 한다 (코드 작성 규칙 1). 받은 플레이어의 화면에만 보낸다. 네트워크를 켜지 않은 테스트는 [TargetRpc]를 보낼 수 없으므로 그 자리에서 부른다
	void PlayRewardEffect(PlayerProgress player)
	{
		if (!NetworkServer.active)
		{
			RewardEffectPlayed?.Invoke(player, exp, money, EffectDrops(rolled, givenItems.Count));
			LocalRewardReceived?.Invoke(exp, rolled, givenItems);
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
		TargetRewardEffect(conn, exp, money, ids, counts, givenItems.ToArray());
	}

	// 받은 플레이어의 클라이언트(호스트 포함)에서 불린다. 몬스터를 없애기 전에 보내므로 몬스터가 아직 이 화면에 있다
	// itemIds: 받은 블록·함수의 항목 ID (받은 수만큼). 획득 표시가 이름을 보여 주려고 수가 아니라 ID를 보낸다
	[TargetRpc]
	void TargetRewardEffect(NetworkConnectionToClient target, int expGained, int moneyGained, string[] ids, int[] counts, string[] itemIds)
	{
		int items = itemIds != null ? itemIds.Length : 0;
		NetworkIdentity me = NetworkClient.localPlayer;
		PlayerProgress receiver = me != null ? me.GetComponent<PlayerProgress>() : null;

		received.Clear();
		int n = ids != null && counts != null ? Mathf.Min(ids.Length, counts.Length) : 0;
		for (int i = 0; i < n; i++) received.Add(new MaterialDrop { materialId = ids[i], count = counts[i], chance = 1f });
		RewardEffectPlayed?.Invoke(receiver, expGained, moneyGained, EffectDrops(received, items));
		LocalRewardReceived?.Invoke(expGained, received, itemIds ?? System.Array.Empty<string>());
	}

	// 연출용 재료 목록. 블록·함수를 받았으면 재료 뒤에 아이콘(ItemDropIconId) 한 줄을 붙여, 버그 조각처럼 날아오게 한다
	IReadOnlyList<MaterialDrop> EffectDrops(List<MaterialDrop> materials, int items)
	{
		if (items <= 0) return materials;
		effectDrops.Clear();
		effectDrops.AddRange(materials);
		effectDrops.Add(new MaterialDrop { materialId = ItemDropIconId, count = items, chance = 1f });
		return effectDrops;
	}

	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;
}
