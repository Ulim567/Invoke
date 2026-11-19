using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 캐릭터의 레벨, 경험치, 돈, 재료, 스킬 트리 해금 목록을 가진 컴포넌트. 플레이어 프리팹 루트에 붙인다.
/// <para>
/// - 이 값들은 이 컴포넌트만 바꾼다(코드 작성 규칙 2). 다른 시스템은 AddExp, AddMoney, AddMaterial 등을 부른다.<br/>
/// - 값을 바꾸는 함수는 서버(호스트 포함)와 네트워크를 켜지 않은 테스트에서만 동작한다. 서버는 바꾼 직후 DB(ProgressStore)에 쓴다.<br/>
/// - 바뀐 값은 Mirror가 이 플레이어의 소유 클라이언트에만 보낸다(Sync Mode: Owner, 프리팹 Inspector).<br/>
/// - 화면은 값을 읽고 Changed를 받아 다시 그린다.
/// </para>
/// <para>
/// 「로그인 건너뛰기」 임시 계정과 네트워크를 켜지 않은 테스트는 DB에 쓰지 않고 값을 메모리에만 둔다.<br/>
/// 레벨업: AddExp가 LevelRules(김도훈)의 경험치 표로 레벨을 올린다.<br/>
/// 노드 해금: 소유 클라이언트가 RequestUnlock을 부르면 서버의 TryUnlock이 검사·재료 빼기·저장을 하고, 결과를 UnlockAnswered로 알린다.<br/>
/// 상점 (#534): 상점 칸(ShopOffers)도 여기에 둔다. 재료와 개수를 바꾸는 곳을 이 컴포넌트 하나로 두기 위함이다.
/// 소유 클라이언트가 RequestBuyShopItem·RequestRefreshShop을 부르면 서버가 판정·저장하고 ShopAnswered로 알린다.
/// </para>
/// </summary>
public class PlayerProgress : NetworkBehaviour
{
	[SyncVar(hook = nameof(OnSyncVarChanged))] int level = 1;
	// 현재 레벨 안의 경험치. 레벨업하면 0부터 다시 센다
	[SyncVar(hook = nameof(OnSyncVarChanged))] int exp;
	[SyncVar(hook = nameof(OnSyncVarChanged))] int money;
	// 재료 ID → 개수. 0개인 재료는 넣지 않는다
	readonly SyncDictionary<string, int> materials = new SyncDictionary<string, int>();
	// 블록·함수의 항목 ID → 가진 개수 (#500, ItemCountRules). 0개인 항목은 넣지 않는다
	readonly SyncDictionary<string, int> itemCounts = new SyncDictionary<string, int>();
	readonly SyncList<string> unlocked = new SyncList<string>();
	// 상점 칸 (#534). 칸 번호 순서로 ShopRules.SlotCount개. 처음 상점을 열기 전에는 비어 있을 수 있다
	readonly SyncList<ShopOffer> shopOffers = new SyncList<ShopOffer>();

#if UNITY_SERVER || UNITY_EDITOR
	// [서버] 값을 쓸 DB와 캐릭터 key. ApplyLoaded가 정한다. null이면 DB에 쓰지 않는다(임시 계정, 네트워크를 켜지 않은 테스트)
	ProgressStore store;
	string characterKey;
#endif

	/// <summary>레벨. 처음 값 1.</summary>
	public int Level => level;

	/// <summary>현재 레벨 안의 경험치.</summary>
	public int Exp => exp;

	public int Money => money;

	/// <summary>해금한 스킬 트리 노드 ID. SkillTree.CanUnlock, SkillTree.IsBlockUnlocked에 넘기는 해금 목록이다.</summary>
	public IReadOnlyList<string> Unlocked => unlocked;

	/// <summary>
	/// [서버] 상점과 몬스터 드롭이 후보를 뽑을 때 넘기는 해금 목록 (ShopRoller). 「로그인 건너뛰기」 임시 계정(DB를 쓰지 않음)은 null = 모두 해금으로 본다.
	/// </summary>
	// 임시 계정을 null로 두는 이유: 편집 화면도 임시 계정은 해금을 보지 않아서, 해금 안 한 블록도 쓸 수 있다
	public ICollection<string> RollUnlocked => UsesStore ? unlocked : null;

	/// <summary>
	/// <summary>
	/// 블록·함수의 항목 ID → 가진 개수 (#500). 0개인 항목은 없다. 항목 ID는 BlockUsageCounter.KeyOf와 같다.<br/>
	/// 남은 개수 = 가진 개수 − BlockUsageCounter.Count로 센 개수. 「로그인 건너뛰기」 임시 계정은 개수를 쓰지 않는다.
	/// </summary>
	public IReadOnlyDictionary<string, int> ItemCounts => itemCounts;

	/// <summary>
	/// [표시] 레벨, 경험치, 돈, 재료, 블록·함수 개수, 해금 목록 중 하나라도 바뀌었을 때. 값을 가진 곳(서버, 호스트, 소유 클라이언트)에서 불린다.
	/// </summary>
	public event Action Changed;

	/// <summary>
	/// [로직] 레벨이 오른 순간 (새 레벨). 서버에서 AddExp가 부른다. 한 번에 여러 레벨이 오르면 오른 레벨마다 불린다.
	/// </summary>
	public event Action<int> LeveledUp;

	/// <summary>
	/// [표시] 내가 보낸 해금 요청(RequestUnlock)의 결과 (노드 ID, 결과). 성공(Unlocked)도 알린다. 소유 클라이언트에서 불린다.<br/>
	/// 네트워크를 켜지 않은 테스트에서는 RequestUnlock이 바로 부른다.
	/// </summary>
	public event Action<string, UnlockResult> UnlockAnswered;

	/// <summary>
	/// 상점 칸 (#534). 칸 번호 순서(0~3 문법 블록, 4~7 함수)로 ShopRules.SlotCount개. 상점을 처음 열기 전에는 비어 있을 수 있다.<br/>
	/// 구매·새로고침 때만 바뀐다. 바뀌면 Changed가 불린다.
	/// </summary>
	public IReadOnlyList<ShopOffer> ShopOffers => shopOffers;

	/// <summary>
	/// [표시] 내가 보낸 상점 요청(RequestOpenShop, RequestBuyShopItem, RequestRefreshShop)의 결과 (요청 종류, 칸 번호, 결과). 성공(Done)도 알린다.
	/// 소유 클라이언트에서 불린다. 칸 번호는 구매에서만 뜻이 있다.
	/// </summary>
	public event Action<ShopRequest, int, ShopResult> ShopAnswered;

	/// <summary>그 재료의 개수. 없으면 0.</summary>
	public int GetMaterialCount(string materialId)
	{
		return materialId != null && materials.TryGetValue(materialId, out int count) ? count : 0;
	}

	/// <summary>그 블록·함수 항목의 가진 개수 (#500). 없으면 0.</summary>
	public int GetItemCount(string itemId)
	{
		return itemId != null && itemCounts.TryGetValue(itemId, out int count) ? count : 0;
	}

	void Awake()
	{
		materials.OnChange += (op, key, value) => OnSyncedCollectionChanged();
		itemCounts.OnChange += (op, key, value) => OnSyncedCollectionChanged();
		unlocked.OnChange += (op, index, value) => OnSyncedCollectionChanged();
		shopOffers.OnChange += (op, index, value) => OnSyncedCollectionChanged();
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		// 처음 받은 재료·해금 목록에는 동기화 콜백이 불리지 않으므로 한 번 알린다
		if (isClientOnly) Changed?.Invoke();
	}

	// Changed를 부르는 곳을 나눈다. 서버(호스트 포함)는 값을 바꾼 함수가 부르고, 클라이언트 전용은 동기화 콜백이 부른다
	// 동기화 콜백은 서버에서도 불리므로(SyncDictionary, 호스트의 SyncVar hook), 나누지 않으면 두 번 불리기 때문
	void OnSyncVarChanged(int oldValue, int newValue)
	{
		if (isClientOnly) Changed?.Invoke();
	}

	void OnSyncedCollectionChanged()
	{
		if (isClientOnly) Changed?.Invoke();
	}

#if UNITY_SERVER || UNITY_EDITOR
	/// <summary>
	/// [서버] DB에서 불러온 값을 넣는다. 플레이어를 만들 때 AccountServer가 부른다. 이 함수는 DB에 쓰지 않는다.<br/>
	/// 이후 값이 바뀌면 progressStore의 characterKey 캐릭터에 쓴다. progressStore가 null이면 DB에 쓰지 않는다.
	/// </summary>
	public void ApplyLoaded(CharacterProgressData data, ProgressStore progressStore, string characterKey)
	{
		store = progressStore;
		this.characterKey = characterKey;

		level = data.Level;
		exp = data.Exp;
		money = data.Money;
		materials.Clear();
		foreach (KeyValuePair<string, int> pair in data.Materials)
		{
			if (pair.Value > 0) materials[pair.Key] = pair.Value;
		}
		itemCounts.Clear();
		foreach (KeyValuePair<string, int> pair in data.ItemCounts)
		{
			if (pair.Value > 0) itemCounts[pair.Key] = Math.Min(pair.Value, ItemCountRules.MaxCount);
		}
		unlocked.Clear();
		unlocked.AddRange(data.Unlocked);
		// 상점 칸이 다 있을 때만 넣는다. 빠졌으면 비워 두고, 상점을 열 때 PrepareShop이 새로 뽑아 저장한다 (#534)
		shopOffers.Clear();
		if (data.ShopOffers != null && data.ShopOffers.Count == ShopRules.SlotCount) shopOffers.AddRange(data.ShopOffers);
		Changed?.Invoke();
	}
#endif

	/// <summary>[서버] 경험치를 더한다. 0 이하는 무시한다. 다음 레벨 경험치(LevelRules.ExpToNext)를 채우면 레벨을 올리고 남은 경험치를 넘긴다. 최대 레벨에서는 쌓이지 않는다.</summary>
	public void AddExp(int amount)
	{
		if (amount <= 0 || level >= LevelRules.MaxLevel || !CanChange(nameof(AddExp))) return;
		exp += amount;

		// 레벨업 (규칙: LevelRules, 김도훈). 한 번에 여러 레벨이 오를 수 있다
		int startLevel = level;
		while (level < LevelRules.MaxLevel && exp >= LevelRules.ExpToNext(level))
		{
			exp -= LevelRules.ExpToNext(level);
			level++;
		}
		if (level >= LevelRules.MaxLevel) exp = 0;

		SaveProgress();
		for (int l = startLevel + 1; l <= level; l++) LeveledUp?.Invoke(l);
		Changed?.Invoke();
	}

	/// <summary>[서버] 돈을 더한다. 0 이하는 무시한다.</summary>
	public void AddMoney(int amount)
	{
		if (amount <= 0 || !CanChange(nameof(AddMoney))) return;
		money += amount;
		SaveProgress();
		Changed?.Invoke();
	}

	/// <summary>[서버] 돈을 뺀다. 모자라면 빼지 않고 false.</summary>
	public bool TrySpendMoney(int amount)
	{
		if (amount < 0 || !CanChange(nameof(TrySpendMoney)) || money < amount) return false;
		money -= amount;
		SaveProgress();
		Changed?.Invoke();
		return true;
	}

	/// <summary>[서버] 재료를 더한다. 0 이하는 무시한다.</summary>
	public void AddMaterial(string materialId, int count)
	{
		if (string.IsNullOrEmpty(materialId) || count <= 0 || !CanChange(nameof(AddMaterial))) return;
		int newCount = GetMaterialCount(materialId) + count;
		materials[materialId] = newCount;
#if UNITY_SERVER || UNITY_EDITOR
		store?.SetMaterialCount(characterKey, materialId, newCount);
#endif
		Changed?.Invoke();
	}

	/// <summary>
	/// [서버] 블록·함수 항목의 가진 개수를 더한다 (#500). 0 이하는 무시하고, ItemCountRules.MaxCount에서 멈춘다.<br/>
	/// 지금은 노드 해금(TryUnlock)이 지급할 때 쓴다. 상점과 잡몹 드랍도 이 함수로 준다.
	/// </summary>
	public void AddItem(string itemId, int count)
	{
		if (string.IsNullOrEmpty(itemId) || count <= 0 || !CanChange(nameof(AddItem))) return;
		int newCount = Math.Min(GetItemCount(itemId) + count, ItemCountRules.MaxCount);
		itemCounts[itemId] = newCount;
#if UNITY_SERVER || UNITY_EDITOR
		store?.SetItemCount(characterKey, itemId, newCount);
#endif
		Changed?.Invoke();
	}

	/// <summary>
	/// [서버] 스킬 트리 노드를 해금한다. 노드 ID(이 캐릭터 언어의 노드인지 포함, #523), 이미 해금함, 선행 조건(SkillTree.CanUnlock), 재료(노드의 Cost) 순서로 검사하고
	/// 먼저 걸린 이유를 돌려준다. 통과하면 재료를 빼고 노드와 같이 열리는 노드(SkillTree.AutoUnlockedBy)를 해금 목록에 넣고,
	/// 연 노드마다 그 노드가 여는 블록·함수를 1개씩 준 뒤(ItemCountRules.KeysUnlockedBy, #500) DB에 한 트랜잭션으로 쓴다. 다른 시스템은 이 함수가 아니라 소유 클라이언트의 RequestUnlock을 쓴다.
	/// </summary>
	public UnlockResult TryUnlock(string nodeId)
	{
		if (!CanChange(nameof(TryUnlock))) return UnlockResult.NotAllowed;
		if (!SkillTree.TryGet(nodeId, out SkillTreeNode node)) return UnlockResult.UnknownNode;
		// 다른 언어 캐릭터의 노드는 이 캐릭터의 트리에 없는 노드로 본다 (C 캐릭터의 py.*, 파이썬 캐릭터의 c.*, #523)
		string language = PlayerCharacter.LanguageOf(gameObject);
		if (!SkillTree.IsFor(nodeId, language)) return UnlockResult.UnknownNode;
		if (unlocked.Contains(nodeId)) return UnlockResult.AlreadyUnlocked;
		if (!SkillTree.CanUnlock(nodeId, unlocked)) return UnlockResult.PrerequisiteMissing;

		// 재료를 뺀 뒤의 개수를 먼저 계산한다. 하나라도 모자라면 아무것도 바꾸지 않는다
		// 같은 재료가 Cost에 두 번 적혀 있어도 합쳐서 빼도록 계산 결과를 이어 쓴다
		var newCounts = new Dictionary<string, int>();
		foreach (MaterialCost cost in node.Cost)
		{
			int current = newCounts.TryGetValue(cost.MaterialId, out int pending) ? pending : GetMaterialCount(cost.MaterialId);
			newCounts[cost.MaterialId] = current - cost.Count;
		}
		foreach (int count in newCounts.Values)
		{
			if (count < 0) return UnlockResult.MaterialShort;
		}

		// 같이 열리는 노드는 방금 연 노드가 목록에 들어간 뒤에 구해야 한다 (예: 검 소환 → 해제)
		var afterUnlock = new HashSet<string>(unlocked) { nodeId };
		List<string> autoNodes = SkillTree.AutoUnlockedBy(afterUnlock, language);
		var newNodes = new List<string> { nodeId };
		newNodes.AddRange(autoNodes);

		// 연 노드(같이 열린 노드 포함)마다 그 노드가 여는 블록·함수를 1개씩 준다. 두 노드가 같은 항목을 열면 2개
		var newItemCounts = new Dictionary<string, int>();
		foreach (string id in newNodes)
		{
			if (!SkillTree.TryGet(id, out SkillTreeNode opened)) continue;
			foreach (string key in ItemCountRules.KeysUnlockedBy(opened))
			{
				int current = newItemCounts.TryGetValue(key, out int pending) ? pending : GetItemCount(key);
				newItemCounts[key] = Math.Min(current + 1, ItemCountRules.MaxCount);
			}
		}

		// DB에 먼저 쓰고, 성공한 뒤에 메모리를 바꾼다. 저장이 예외로 실패하면 메모리는 그대로여서
		// 재료만 빠지거나 해금만 사라지는 불일치가 생기지 않는다
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveUnlock(characterKey, newNodes, newCounts, newItemCounts);
#endif
		foreach (KeyValuePair<string, int> pair in newCounts)
		{
			if (pair.Value > 0) materials[pair.Key] = pair.Value;
			else materials.Remove(pair.Key);
		}
		foreach (KeyValuePair<string, int> pair in newItemCounts) itemCounts[pair.Key] = pair.Value;
		unlocked.AddRange(newNodes);
		Changed?.Invoke();
		return UnlockResult.Unlocked;
	}

	/// <summary>
	/// [소유 클라이언트] 서버에 노드 해금을 요청한다. 결과는 UnlockAnswered로 온다.<br/>
	/// 내 플레이어(isLocalPlayer)가 아니면 아무것도 하지 않는다.
	/// </summary>
	public void RequestUnlock(string nodeId)
	{
		if (!isLocalPlayer) return;
		CmdUnlock(nodeId);
	}

	// 클라이언트가 보낸 노드 ID는 믿지 않는다. TryUnlock이 SkillTree에서 찾아 보고, 없으면 UnknownNode로 돌려준다
	[Command]
	void CmdUnlock(string nodeId)
	{
		TargetUnlockAnswered(nodeId, TryUnlock(nodeId));
	}

	[TargetRpc]
	void TargetUnlockAnswered(string nodeId, UnlockResult result)
	{
		if (result != UnlockResult.Unlocked)
			Debug.LogWarning($"[PlayerProgress] {name}: 서버가 {nodeId} 해금을 거부했습니다: {result}", this);
		UnlockAnswered?.Invoke(nodeId, result);
	}

	// ---- 상점 (#534) ----

	/// <summary>
	/// [서버] 상점 칸이 없으면(처음 여는 캐릭터, 상점 전에 만든 캐릭터, 임시 계정) 8칸을 뽑아 저장한다. 이미 있으면 아무것도 하지 않는다.<br/>
	/// 상점을 열 때 부른다. 열 때마다 새로 뽑지 않는다 (칸은 구매·새로고침 때만 바뀜).
	/// </summary>
	public void PrepareShop()
	{
		if (!CanChange(nameof(PrepareShop)) || shopOffers.Count == ShopRules.SlotCount) return;
		ShopOffer[] offers = ShopRoller.RollAll(RollUnlocked, itemCounts, PlayerCharacter.LanguageOf(gameObject));
		var bySlot = new Dictionary<int, ShopOffer>();
		for (int i = 0; i < offers.Length; i++) bySlot[i] = offers[i];
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveShop(characterKey, null, null, bySlot);
#endif
		shopOffers.Clear();
		shopOffers.AddRange(offers);
		Changed?.Invoke();
	}

	/// <summary>
	/// [서버] 상점 칸 하나를 산다. 칸 번호, 빈 칸, 99개, 재료 순서로 검사하고 먼저 걸린 이유를 돌려준다.<br/>
	/// 통과하면 가격만큼 재료를 빼고, 그 항목의 가진 개수를 1 늘리고, 그 칸에 새 상품을 뽑아 DB에 한 트랜잭션으로 쓴다.<br/>
	/// 「로그인 건너뛰기」 임시 계정은 개수를 쓰지 않으므로 재료만 빼고 칸을 바꾼다 (기획 11/18). 다른 시스템은 소유 클라이언트의 RequestBuyShopItem을 쓴다.
	/// </summary>
	public ShopResult TryBuyShopItem(int slot)
	{
		if (!CanChange(nameof(TryBuyShopItem))) return ShopResult.NotAllowed;
		if (slot < 0 || slot >= ShopRules.SlotCount) return ShopResult.InvalidSlot;
		PrepareShop();
		ShopOffer offer = shopOffers[slot];
		if (offer.IsEmpty) return ShopResult.EmptySlot;

		bool countsItems = UsesStore;
		if (countsItems && GetItemCount(offer.ItemId) >= ItemCountRules.MaxCount) return ShopResult.MaxCount;
		if (!TrySubtract(offer.Price, out Dictionary<string, int> newMaterials)) return ShopResult.MaterialShort;

		Dictionary<string, int> newItemCounts = null;
		var ownedAfter = new Dictionary<string, int>(itemCounts);
		if (countsItems)
		{
			int newCount = Math.Min(GetItemCount(offer.ItemId) + 1, ItemCountRules.MaxCount);
			newItemCounts = new Dictionary<string, int> { [offer.ItemId] = newCount };
			ownedAfter[offer.ItemId] = newCount;
		}
		// 산 뒤의 개수로 뽑는다. 방금 99개가 된 항목이 그 칸에 다시 올라오지 않게
		ShopOffer next = ShopRoller.RollSlot(slot, RollUnlocked, ownedAfter, PlayerCharacter.LanguageOf(gameObject));

		// DB에 먼저 쓰고, 성공한 뒤에 메모리를 바꾼다 (TryUnlock과 같은 이유)
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveShop(characterKey, newMaterials, newItemCounts, new Dictionary<int, ShopOffer> { [slot] = next });
#endif
		ApplyMaterialCounts(newMaterials);
		if (newItemCounts != null)
		{
			foreach (KeyValuePair<string, int> pair in newItemCounts) itemCounts[pair.Key] = pair.Value;
		}
		shopOffers[slot] = next;
		Changed?.Invoke();
		return ShopResult.Done;
	}

	/// <summary>
	/// [서버] 상점을 새로고침한다. 새로고침 비용(ShopRules.RefreshCost)만큼 재료가 있으면 빼고 8칸을 모두 새로 뽑아 DB에 한 트랜잭션으로 쓴다.
	/// 다른 시스템은 소유 클라이언트의 RequestRefreshShop을 쓴다.
	/// </summary>
	public ShopResult TryRefreshShop()
	{
		if (!CanChange(nameof(TryRefreshShop))) return ShopResult.NotAllowed;
		if (!TrySubtract(ShopRules.RefreshCost, out Dictionary<string, int> newMaterials)) return ShopResult.MaterialShort;

		ShopOffer[] offers = ShopRoller.RollAll(RollUnlocked, itemCounts, PlayerCharacter.LanguageOf(gameObject));
		var bySlot = new Dictionary<int, ShopOffer>();
		for (int i = 0; i < offers.Length; i++) bySlot[i] = offers[i];
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveShop(characterKey, newMaterials, null, bySlot);
#endif
		ApplyMaterialCounts(newMaterials);
		shopOffers.Clear();
		shopOffers.AddRange(offers);
		Changed?.Invoke();
		return ShopResult.Done;
	}

	/// <summary>[소유 클라이언트] 상점 창을 열 때 부른다. 칸이 없으면 서버가 뽑는다. 결과는 ShopAnswered로 온다.</summary>
	public void RequestOpenShop()
	{
		if (!isLocalPlayer) return;
		CmdShop(ShopRequest.Open, 0);
	}

	/// <summary>[소유 클라이언트] 서버에 상점 칸 하나의 구매를 요청한다. 결과는 ShopAnswered로 온다. 내 플레이어가 아니면 아무것도 하지 않는다.</summary>
	public void RequestBuyShopItem(int slot)
	{
		if (!isLocalPlayer) return;
		CmdShop(ShopRequest.Buy, slot);
	}

	/// <summary>[소유 클라이언트] 서버에 상점 새로고침을 요청한다. 결과는 ShopAnswered로 온다. 내 플레이어가 아니면 아무것도 하지 않는다.</summary>
	public void RequestRefreshShop()
	{
		if (!isLocalPlayer) return;
		CmdShop(ShopRequest.Refresh, 0);
	}

	// 클라이언트는 칸 번호만 보낸다. 상품과 가격은 서버가 가진 칸에서 읽으므로 클라이언트가 가격을 바꿀 수 없다
	[Command]
	void CmdShop(ShopRequest request, int slot)
	{
		ShopResult result;
		switch (request)
		{
			case ShopRequest.Open:
				PrepareShop();
				result = ShopResult.Done;
				break;
			case ShopRequest.Buy:
				result = TryBuyShopItem(slot);
				break;
			case ShopRequest.Refresh:
				result = TryRefreshShop();
				break;
			default:
				result = ShopResult.NotAllowed;
				break;
		}
		TargetShopAnswered(request, slot, result);
	}

	[TargetRpc]
	void TargetShopAnswered(ShopRequest request, int slot, ShopResult result)
	{
		if (result != ShopResult.Done)
			Debug.LogWarning($"[PlayerProgress] {name}: 서버가 상점 {request}(칸 {slot})을 거부했습니다: {result}", this);
		ShopAnswered?.Invoke(request, slot, result);
	}

	// 서버 DB에 쓰는 실제 계정이면 true. 임시 계정과 네트워크를 켜지 않은 테스트는 false
	bool UsesStore
	{
		get
		{
#if UNITY_SERVER || UNITY_EDITOR
			return store != null;
#else
			return false;
#endif
		}
	}

	// 가격만큼 뺀 뒤의 재료 개수를 계산한다. 하나라도 모자라면 false (아무것도 바꾸지 않음)
	bool TrySubtract(IReadOnlyList<ShopPrice> price, out Dictionary<string, int> newCounts)
	{
		newCounts = new Dictionary<string, int>();
		foreach (KeyValuePair<string, int> pair in ShopPrices.Sum(price))
		{
			int after = GetMaterialCount(pair.Key) - pair.Value;
			if (after < 0) return false;
			newCounts[pair.Key] = after;
		}
		return true;
	}

	// 재료의 새 개수를 메모리에 넣는다. 0개면 지운다
	void ApplyMaterialCounts(IReadOnlyDictionary<string, int> counts)
	{
		foreach (KeyValuePair<string, int> pair in counts)
		{
			if (pair.Value > 0) materials[pair.Key] = pair.Value;
			else materials.Remove(pair.Key);
		}
	}

	// 값은 서버에서만 바꾼다. 클라이언트 전용에서 바꾸면 서버·DB와 달라지기 때문
	bool CanChange(string caller)
	{
		if (NetworkClient.active && !NetworkServer.active)
		{
			Debug.LogWarning($"[PlayerProgress] {name}: {caller}는 서버에서만 부릅니다. 클라이언트에서는 값을 바꾸지 않습니다.", this);
			return false;
		}
		return true;
	}

	void SaveProgress()
	{
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveProgress(characterKey, level, exp, money);
#endif
	}
}
