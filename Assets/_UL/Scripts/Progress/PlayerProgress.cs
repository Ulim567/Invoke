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
/// 노드 해금: 소유 클라이언트가 RequestUnlock을 부르면 서버의 TryUnlock이 검사·재료 빼기·저장을 하고, 결과를 UnlockAnswered로 알린다.
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
	readonly SyncList<string> unlocked = new SyncList<string>();

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
	/// [표시] 레벨, 경험치, 돈, 재료, 해금 목록 중 하나라도 바뀌었을 때. 값을 가진 곳(서버, 호스트, 소유 클라이언트)에서 불린다.
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

	/// <summary>그 재료의 개수. 없으면 0.</summary>
	public int GetMaterialCount(string materialId)
	{
		return materialId != null && materials.TryGetValue(materialId, out int count) ? count : 0;
	}

	void Awake()
	{
		materials.OnChange += (op, key, value) => OnSyncedCollectionChanged();
		unlocked.OnChange += (op, index, value) => OnSyncedCollectionChanged();
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
		unlocked.Clear();
		unlocked.AddRange(data.Unlocked);
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
	/// [서버] 스킬 트리 노드를 해금한다. 노드 ID, 이미 해금함, 선행 조건(SkillTree.CanUnlock), 재료(노드의 Cost) 순서로 검사하고
	/// 먼저 걸린 이유를 돌려준다. 통과하면 재료를 빼고 노드와 같이 열리는 노드(SkillTree.AutoUnlockedBy)를 해금 목록에 넣은 뒤
	/// DB에 한 트랜잭션으로 쓴다. 다른 시스템은 이 함수가 아니라 소유 클라이언트의 RequestUnlock을 쓴다.
	/// </summary>
	public UnlockResult TryUnlock(string nodeId)
	{
		if (!CanChange(nameof(TryUnlock))) return UnlockResult.NotAllowed;
		if (!SkillTree.TryGet(nodeId, out SkillTreeNode node)) return UnlockResult.UnknownNode;
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
		List<string> autoNodes = SkillTree.AutoUnlockedBy(afterUnlock);
		var newNodes = new List<string> { nodeId };
		newNodes.AddRange(autoNodes);

		// DB에 먼저 쓰고, 성공한 뒤에 메모리를 바꾼다. 저장이 예외로 실패하면 메모리는 그대로여서
		// 재료만 빠지거나 해금만 사라지는 불일치가 생기지 않는다
#if UNITY_SERVER || UNITY_EDITOR
		store?.SaveUnlock(characterKey, newNodes, newCounts);
#endif
		foreach (KeyValuePair<string, int> pair in newCounts)
		{
			if (pair.Value > 0) materials[pair.Key] = pair.Value;
			else materials.Remove(pair.Key);
		}
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
