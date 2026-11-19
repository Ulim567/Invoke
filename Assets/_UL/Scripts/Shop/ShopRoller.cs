using System;
using System.Collections.Generic;

/// <summary>
/// 상점 칸에 올릴 상품을 뽑는다 (#534). 서버(PlayerProgress)가 부르고, 네트워크와 DB를 모르는 일반 C# 함수라 테스트에서 바로 부를 수 있다.
/// <para>
/// - 문법 블록 칸: 해금한 문법 블록 가운데 그 캐릭터 언어에서 쓸 수 있는 것을 상점 무게(ShopRules.SyntaxWeights)로 뽑는다.<br/>
/// - 함수 칸: 해금한 함수(스킬, get_bug, is_valid_target, wait)를 희귀도 무게로 뽑는다. 대시·더블점프(common.)와 테스트 함수는 뺀다.<br/>
/// - 숨기는 블록·함수(HiddenBlocks: malloc, free, 대시, 더블점프)는 뽑지 않는다 (#598).<br/>
/// - 이미 ItemCountRules.MaxCount(99)개 가진 항목은 뽑지 않는다. 같은 항목이 여러 칸에 올라와도 된다. 후보가 없으면 빈 칸.<br/>
/// - unlocked가 null이면 모든 노드를 해금한 것으로 본다 (「로그인 건너뛰기」 임시 계정은 편집 화면도 해금을 보지 않는다).<br/>
/// - 몬스터 드롭(RollDrop)도 같은 후보와 무게로 뽑는다.
/// </para>
/// </summary>
public static class ShopRoller
{
	// 상점에 올리는 사전 정의 함수의 분류 (고정 ID 앞부분). 함수 탭(BlockEditor)과 같고, 공통 이동(common.)은 뺀다.
	// 대시·더블점프는 나중에 함수가 아니라 키보드 고정 이동기로 바뀌기 때문 (기획 11/18)
	static readonly string[] FunctionPrefixes = { "c.", "py.", "input.", "game." };

	// 함수 칸에 올라오는 블록 종류 (함수 탭의 wait, malloc, free). 나머지 블록 종류는 문법 블록 칸
	static readonly BlockKind[] BuiltinFunctionKinds = { BlockKind.Wait, BlockKind.Malloc, BlockKind.Free };

	/// <summary>기본 무작위: 최소~최대(포함) 정수.</summary>
	static int DefaultRange(int min, int maxInclusive) => UnityEngine.Random.Range(min, maxInclusive + 1);

	/// <summary>
	/// 칸 8개를 모두 새로 뽑는다. 새로고침과 처음 상점에 쓴다.
	/// </summary>
	/// <param name="unlocked">해금한 노드 ID. null이면 모두 해금한 것으로 본다</param>
	/// <param name="owned">가진 개수 (항목 ID → 개수). 99개인 항목을 빼는 데 쓴다</param>
	/// <param name="language">캐릭터 언어 (CharacterLanguages)</param>
	/// <param name="randomRange">최소~최대(포함) 정수를 고르는 함수. null이면 UnityEngine.Random</param>
	public static ShopOffer[] RollAll(ICollection<string> unlocked, IReadOnlyDictionary<string, int> owned, string language,
		Func<int, int, int> randomRange = null)
	{
		var offers = new ShopOffer[ShopRules.SlotCount];
		for (int slot = 0; slot < offers.Length; slot++) offers[slot] = RollSlot(slot, unlocked, owned, language, randomRange);
		return offers;
	}

	/// <summary>
	/// 칸 하나에 올릴 상품을 뽑고 가격을 정한다. 후보가 없으면 빈 칸(ShopOffer.Empty).
	/// </summary>
	public static ShopOffer RollSlot(int slot, ICollection<string> unlocked, IReadOnlyDictionary<string, int> owned, string language,
		Func<int, int, int> randomRange = null)
	{
		randomRange ??= DefaultRange;
		List<(string Id, int Weight)> candidates = ShopRules.IsSyntaxSlot(slot)
			? SyntaxCandidates(unlocked, owned, language)
			: FunctionCandidates(unlocked, owned, language);
		string itemId = Pick(candidates, randomRange);
		return itemId == null ? ShopOffer.Empty : new ShopOffer(itemId, ShopRules.RollPrice(slot, itemId, randomRange));
	}

	/// <summary>
	/// 몬스터 드롭으로 줄 블록·함수 하나를 뽑는다. 후보와 무게는 상점과 같다(SyntaxCandidates, FunctionCandidates). 후보가 없으면 null.<br/>
	/// 문법 블록과 함수 가운데 하나를 반반으로 고르고(한쪽 후보가 없으면 다른 쪽), 그 안에서 무게로 뽑는다.
	/// MonsterReward(김도훈)가 서버에서 받는 플레이어마다 부른다.
	/// </summary>
	/// <param name="unlocked">해금한 노드 ID (PlayerProgress.RollUnlocked). null이면 모두 해금한 것으로 본다</param>
	/// <param name="owned">가진 개수. 99개인 항목은 뽑지 않는다</param>
	/// <param name="language">받는 캐릭터의 언어. 그 언어에서 쓸 수 있는 블록·함수만 뽑는다</param>
	/// <param name="randomRange">최소~최대(포함) 정수를 고르는 함수. null이면 UnityEngine.Random</param>
	public static string RollDrop(ICollection<string> unlocked, IReadOnlyDictionary<string, int> owned, string language,
		Func<int, int, int> randomRange = null)
	{
		randomRange ??= DefaultRange;
		List<(string Id, int Weight)> syntax = SyntaxCandidates(unlocked, owned, language);
		List<(string Id, int Weight)> functions = FunctionCandidates(unlocked, owned, language);
		// 문법 블록 무게(1~10)와 함수 무게(희귀도 10~60)는 크기가 달라 한 목록에 섞으면 거의 함수만 나온다.
		// 그래서 상점이 문법 4칸·함수 4칸으로 나뉜 것처럼 먼저 반반으로 나눈다
		bool pickSyntax = syntax.Count > 0 && (functions.Count == 0 || randomRange(0, 1) == 0);
		return Pick(pickSyntax ? syntax : functions, randomRange);
	}

	/// <summary>문법 블록 칸의 후보와 무게.</summary>
	public static List<(string Id, int Weight)> SyntaxCandidates(ICollection<string> unlocked, IReadOnlyDictionary<string, int> owned, string language)
	{
		var candidates = new List<(string, int)>();
		LanguageFamily family = LanguageFamilies.Of(language);
		foreach (BlockKind kind in (BlockKind[])Enum.GetValues(typeof(BlockKind)))
		{
			// 함수 블록은 개수가 없고(사전 정의 함수는 함수 칸), wait·malloc·free는 함수 칸에 올라온다
			if (kind == BlockKind.Function || Array.IndexOf(BuiltinFunctionKinds, kind) >= 0) continue;
			if (!BlockData.IsUsableIn(kind, family) || !IsBlockUnlocked(kind, unlocked)) continue;
			foreach (string key in KeysOf(kind))
			{
				if (IsFull(key, owned)) continue;
				int weight = ShopRules.SyntaxWeightOf(key);
				if (weight > 0) candidates.Add((key, weight));
			}
		}
		return candidates;
	}

	/// <summary>함수 칸의 후보와 무게.</summary>
	public static List<(string Id, int Weight)> FunctionCandidates(ICollection<string> unlocked, IReadOnlyDictionary<string, int> owned, string language)
	{
		var candidates = new List<(string, int)>();
		LanguageFamily family = LanguageFamilies.Of(language);

		// 사전 정의 함수: 편집 화면 함수 탭과 같은 규칙. 함수 블록을 해금해야 하고, 스킬 트리에 노드가 있는 스킬은 그 노드도
		if (IsBlockUnlocked(BlockKind.Function, unlocked) && PredefinedFunctionRegistry.All != null)
		{
			foreach (PredefinedFunction function in PredefinedFunctionRegistry.All)
			{
				string id = function?.Id;
				if (id == null || !HasFunctionPrefix(id) || HiddenBlocks.IsHidden(id)) continue;
				if (!PlayerCharacter.CanUse(language, id)) continue;
				if (unlocked != null && SkillTree.TryGet(id, out _) && !unlocked.Contains(id)) continue;
				Add(id);
			}
		}

		// wait, malloc, free: 각자의 블록 종류를 해금해야 한다. malloc·free는 C만 (BlockData.IsUsableIn)
		// malloc·free는 화면에서 숨기는 블록이라 상점·드롭에도 올리지 않는다 (#598). 보이지 않는 블록을 받으면 쓸 곳이 없기 때문
		foreach (BlockKind kind in BuiltinFunctionKinds)
		{
			if (HiddenBlocks.IsHidden(kind)) continue;
			if (BlockData.IsUsableIn(kind, family) && IsBlockUnlocked(kind, unlocked)) Add(kind.ToString());
		}
		return candidates;

		void Add(string id)
		{
			if (IsFull(id, owned)) return;
			int weight = ShopRules.FunctionWeightOf(id);
			if (weight > 0) candidates.Add((id, weight));
		}
	}

	// 블록 종류 하나의 항목 ID들 (BlockUsageCounter.KeyOf와 같음). 조건문은 If, ElseIf, Else로 나눈다
	static IEnumerable<string> KeysOf(BlockKind kind)
	{
		if (kind == BlockKind.Conditional)
		{
			yield return ConditionalBranch.If.ToString();
			yield return ConditionalBranch.ElseIf.ToString();
			yield return ConditionalBranch.Else.ToString();
		}
		else yield return kind.ToString();
	}

	static bool IsBlockUnlocked(BlockKind kind, ICollection<string> unlocked) => unlocked == null || SkillTree.IsBlockUnlocked(kind, unlocked);

	static bool IsFull(string id, IReadOnlyDictionary<string, int> owned) =>
		owned != null && owned.TryGetValue(id, out int count) && count >= ItemCountRules.MaxCount;

	static bool HasFunctionPrefix(string id)
	{
		foreach (string prefix in FunctionPrefixes)
			if (id.StartsWith(prefix, StringComparison.Ordinal)) return true;
		return false;
	}

	// 무게 비율로 하나를 고른다. 후보가 없으면 null
	static string Pick(List<(string Id, int Weight)> candidates, Func<int, int, int> randomRange)
	{
		int total = 0;
		foreach ((string _, int weight) in candidates) total += weight;
		if (total <= 0) return null;

		int roll = randomRange(0, total - 1);
		foreach ((string id, int weight) in candidates)
		{
			if (roll < weight) return id;
			roll -= weight;
		}
		return candidates[candidates.Count - 1].Id;
	}
}
