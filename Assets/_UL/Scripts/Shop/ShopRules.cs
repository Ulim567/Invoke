using System;
using System.Collections.Generic;

/// <summary>
/// 로비 상점의 숫자 표 (#534). 서버의 뽑기·판정(ShopRoller, PlayerProgress)과 화면(ShopPanel)이 함께 읽는다.
/// <para>
/// - 칸: 문법 블록 4칸(칸 번호 0~3) + 함수 4칸(4~7). 칸 수는 후보가 적어도 그대로다.<br/>
/// - 문법 블록: 상점 무게(SyntaxWeights)로 뽑고, 가격은 칸에 올라올 때 버그 조각 7~13 가운데 무작위.<br/>
/// - 함수(스킬 포함): 희귀도(FunctionRarities)의 무게로 뽑고, 가격도 희귀도로 정한다.<br/>
/// - 가격은 재료 목록(ShopPrice)이다. 보스 코어 등 다른 재료를 받으려면 가격 규칙의 BossCore를 1 이상으로 둔다.
/// </para>
/// </summary>
public static class ShopRules
{
	/// <summary>문법 블록 칸 수 (칸 번호 0 ~ SyntaxSlotCount-1).</summary>
	public const int SyntaxSlotCount = 4;

	/// <summary>함수 칸 수 (문법 블록 칸 뒤).</summary>
	public const int FunctionSlotCount = 4;

	/// <summary>상점 칸 전체 수.</summary>
	public const int SlotCount = SyntaxSlotCount + FunctionSlotCount;

	/// <summary>그 칸이 문법 블록 칸이면 true, 함수 칸이면 false.</summary>
	public static bool IsSyntaxSlot(int slot) => slot < SyntaxSlotCount;

	/// <summary>재료 ID. SkillTreeDefaults(임혜원)와 같은 값.</summary>
	public const string BugFragment = "bug_fragment";
	public const string BossCore = "boss_core";

	/// <summary>새로고침 비용 (기획 11/18: 버그 조각 10개).</summary>
	public static readonly ShopPrice[] RefreshCost = { new ShopPrice(BugFragment, 10) };

	// 문법 블록 가격: 칸에 올라올 때 이 범위(양 끝 포함)에서 무작위 (Notion 「블록 목록」 상점)
	const int SyntaxPriceMin = 7;
	const int SyntaxPriceMax = 13;
	// 문법 블록 가격에 더할 보스 코어 개수. 0이면 받지 않는다 (기획 11/18: 지금은 받지 않음)
	const int SyntaxBossCore = 0;

	/// <summary>
	/// 문법 블록의 상점 무게 (항목 ID → 무게). 클수록 자주 올라온다. 값은 Notion 「블록 표」의 「상점 무게」.
	/// 표에 없는 블록은 DefaultSyntaxWeight로 뽑는다. 몬스터 드롭(ShopRoller.RollDrop)도 이 무게를 쓴다.
	/// </summary>
	public static readonly IReadOnlyDictionary<string, int> SyntaxWeights = new Dictionary<string, int>
	{
		["If"] = 10,
		["ElseIf"] = 3,
		["Else"] = 6,
		["For"] = 6,
		["While"] = 2,
		["Break"] = 1,
		["Continue"] = 1,
		["Declare"] = 10,
		["Assign"] = 10,
		["ArrayDeclare"] = 3,
		["ArrayGet"] = 3,
		["ArraySet"] = 3,
		["Arithmetic"] = 6,
		["Compare"] = 6,
		["Logic"] = 3,
		["Not"] = 3,
		["Return"] = 2,
		// ---- Python 블록 (이우림 11/19). 비슷한 C 블록의 무게에 맞추고, 구슬 list에 꼭 필요한 append만 높였다 ----
		["ListAppend"] = 6, // 구슬 난사·구슬 폭발이 구슬 list를 받는다
		["ListNew"] = 3,    // C ArrayDeclare와 같은 자리
		["Global"] = 3,     // 구슬을 function 밖에 남길 때
		["ListLen"] = 2,
		["ListPop"] = 2,
		["Del"] = 1,        // C Break·Continue처럼 드물게 쓴다
	};

	/// <summary>표에 없는 문법 블록의 임시 무게. 새 블록을 더하고 무게를 아직 정하지 않았을 때 쓴다.</summary>
	public const int DefaultSyntaxWeight = 1;

	/// <summary>희귀도마다 뽑는 무게와 가격. 값은 임시다 (박진우가 정함).</summary>
	public static readonly IReadOnlyDictionary<ShopRarity, RarityRule> RarityRules = new Dictionary<ShopRarity, RarityRule>
	{
		// ---- 박진우가 정함 (11/18, 수치는 임시) ----
		[ShopRarity.Common] = new RarityRule(weight: 60, bugFragmentMin: 8, bugFragmentMax: 12, bossCore: 0),
		[ShopRarity.Rare] = new RarityRule(weight: 30, bugFragmentMin: 15, bugFragmentMax: 20, bossCore: 0),
		// Epic 스킬(전장 휩쓸기, 검무, 메테오, 구슬 폭발)은 스킬 트리에서도 보스 코어가 든다. 상점에서 버그 조각만으로 계속 사지 못하게 코어 1개를 붙인다
		[ShopRarity.Epic] = new RarityRule(weight: 10, bugFragmentMin: 25, bugFragmentMax: 30, bossCore: 1),
		// ---------------------------------------
	};

	/// <summary>
	/// 함수의 희귀도 (항목 ID → 희귀도). 표에 없는 함수는 Common. 값은 임시다 (박진우가 정함).<br/>
	/// 항목 ID는 가진 개수와 같다: 사전 정의 함수는 고정 ID(c.thrust), malloc·free·wait는 블록 종류 이름(Malloc, Free, Wait).
	/// </summary>
	// 임시 기준: 처음 스킬·기본 함수는 Common, 스킬 트리에서 재료를 많이 쓰는 스킬은 Rare, 보스 코어가 드는 마지막 스킬은 Epic
	public static readonly IReadOnlyDictionary<string, ShopRarity> FunctionRarities = new Dictionary<string, ShopRarity>
	{
		// ---- 박진우가 정함 (11/18, 우림이 임시 기준 그대로) ----
		// C 스킬
		[PredefinedFunctionIds.VitalStrike] = ShopRarity.Rare,
		[PredefinedFunctionIds.WoundExploit] = ShopRarity.Rare,
		[PredefinedFunctionIds.DeepThrust] = ShopRarity.Rare,
		[PredefinedFunctionIds.SwordReturn] = ShopRarity.Rare,
		[PredefinedFunctionIds.SummonedSlash] = ShopRarity.Rare,
		[PredefinedFunctionIds.WoundSlash] = ShopRarity.Rare,
		[PredefinedFunctionIds.MultiSlash] = ShopRarity.Rare,
		[PredefinedFunctionIds.SummonSword] = ShopRarity.Rare,
		[PredefinedFunctionIds.SwordRide] = ShopRarity.Rare,
		[PredefinedFunctionIds.BattlefieldSweep] = ShopRarity.Epic,
		[PredefinedFunctionIds.SwordDance] = ShopRarity.Epic,
		// Python 스킬
		[PredefinedFunctionIds.PyChainLightning] = ShopRarity.Rare,
		[PredefinedFunctionIds.PyBlizzard] = ShopRarity.Rare,
		[PredefinedFunctionIds.PyOrbBarrage] = ShopRarity.Rare,
		[PredefinedFunctionIds.PyLevitate] = ShopRarity.Rare,
		[PredefinedFunctionIds.PyMeteor] = ShopRarity.Epic,
		[PredefinedFunctionIds.PyOrbBurst] = ShopRarity.Epic,
		// 메모리 함수
		["Malloc"] = ShopRarity.Rare,
		// ---------------------------------------
	};

	/// <summary>함수의 희귀도. 표에 없으면 Common.</summary>
	public static ShopRarity RarityOf(string itemId) =>
		itemId != null && FunctionRarities.TryGetValue(itemId, out ShopRarity rarity) ? rarity : ShopRarity.Common;

	/// <summary>문법 블록의 상점 무게. 표에 없으면 DefaultSyntaxWeight.</summary>
	public static int SyntaxWeightOf(string itemId) =>
		itemId != null && SyntaxWeights.TryGetValue(itemId, out int weight) ? weight : DefaultSyntaxWeight;

	/// <summary>함수를 뽑는 무게 (희귀도의 무게).</summary>
	public static int FunctionWeightOf(string itemId) => RarityRules.TryGetValue(RarityOf(itemId), out RarityRule rule) ? rule.Weight : 0;

	/// <summary>
	/// 칸에 새로 올라온 항목의 가격을 정한다. 문법 블록 칸은 버그 조각 7~13, 함수 칸은 희귀도의 범위.
	/// randomRange(최소, 최대 포함)로 개수를 고른다.
	/// </summary>
	public static ShopPrice[] RollPrice(int slot, string itemId, Func<int, int, int> randomRange)
	{
		int bugFragments, bossCores;
		if (IsSyntaxSlot(slot))
		{
			bugFragments = randomRange(SyntaxPriceMin, SyntaxPriceMax);
			bossCores = SyntaxBossCore;
		}
		else
		{
			RarityRule rule = RarityRules.TryGetValue(RarityOf(itemId), out RarityRule found) ? found : RarityRules[ShopRarity.Common];
			bugFragments = randomRange(rule.BugFragmentMin, rule.BugFragmentMax);
			bossCores = rule.BossCore;
		}

		var price = new List<ShopPrice> { new ShopPrice(BugFragment, Math.Max(1, bugFragments)) };
		if (bossCores > 0) price.Add(new ShopPrice(BossCore, bossCores));
		return price.ToArray();
	}

	/// <summary>희귀도 하나의 뽑는 무게와 가격 범위.</summary>
	public readonly struct RarityRule
	{
		public readonly int Weight;
		public readonly int BugFragmentMin;
		public readonly int BugFragmentMax;
		/// <summary>가격에 더할 보스 코어 개수. 0이면 받지 않는다.</summary>
		public readonly int BossCore;

		public RarityRule(int weight, int bugFragmentMin, int bugFragmentMax, int bossCore)
		{
			Weight = weight;
			BugFragmentMin = bugFragmentMin;
			BugFragmentMax = bugFragmentMax;
			BossCore = bossCore;
		}
	}
}

/// <summary>함수의 희귀도 (#534). 희귀할수록 덜 올라오고 비싸다 (ShopRules.RarityRules).</summary>
public enum ShopRarity
{
	Common,
	Rare,
	Epic,
}

/// <summary>상점 요청의 종류 (#534). PlayerProgress.ShopAnswered가 함께 알린다.</summary>
public enum ShopRequest
{
	Open,    // 상점 창을 열 때. 칸이 없으면 서버가 뽑는다
	Buy,     // 칸 하나 사기
	Refresh, // 새로고침
	// 새 값은 맨 끝에만 더한다. 이 값은 네트워크로 보내므로 중간에 끼우면 뒤 값들의 번호가 바뀐다
}

/// <summary>
/// 상점 요청의 결과 (#534). PlayerProgress.TryBuyShopItem·TryRefreshShop이 돌려주고, 소유 클라이언트에는 PlayerProgress.ShopAnswered로 알린다.<br/>
/// 구매는 InvalidSlot → EmptySlot → MaxCount → MaterialShort 순서로 검사하고, 먼저 걸린 이유 하나만 돌려준다.
/// </summary>
public enum ShopResult
{
	Done,          // 사거나 새로고침함 (Open은 칸을 준비함)
	NotAllowed,    // 서버가 아닌 곳에서 부름. 값을 바꾸지 않음
	InvalidSlot,   // 칸 번호가 0~7 밖
	EmptySlot,     // 그 칸에 올라온 항목이 없음 (후보가 없었음)
	MaxCount,      // 이미 ItemCountRules.MaxCount(99)개를 가짐
	MaterialShort, // 가격(새로고침 비용)보다 가진 재료가 모자람
	// 새 값은 맨 끝에만 더한다. 이 값은 네트워크로 보내므로 중간에 끼우면 뒤 값들의 번호가 바뀐다
}
