using System;
using System.Collections.Generic;

/// <summary>
/// 자세 숫자 하나가 주는 효과 값 (#538). 여러 규칙에 해당하면 규칙마다의 값을 더한 것이다.
/// <para>
/// - 데미지: 모든 스킬에 붙는 추가분(DamageBonus)과 스킬 계열에만 붙는 추가분(C 찌르기·휘두르기·소환, Python 원소·범위·구슬, #546)이 있다. DamageBonusFor로 둘을 더해 읽는다.<br/>
/// - ReceivedDamageRate: 받는 피해의 증감 (+0.1이면 10% 더 아프다).<br/>
/// - CooldownRate: 모든 스킬의 쿨타임 증감 (-0.15면 15% 짧게, +0.1이면 10% 길게). 보조 계열에만 붙는 증감(CurseCooldownRate, #546)은 CooldownRateFor로 더해 읽는다.<br/>
/// - CurseDurationRate: 보조 계열 스킬이 거는 상태 이상의 지속 시간 증감 (+0.3이면 30% 길게, #546). StatusDurationRateFor로 읽는다.<br/>
/// - SelfDamageRate: 데미지를 준 스킬마다 깎는 체력. 최대 체력에 대한 비율이다.<br/>
/// - LifestealRate: 스킬이 준 데미지 가운데 체력으로 회복하는 비율.
/// </para>
/// </summary>
public readonly struct StanceEffect
{
	public readonly float DamageBonus;
	public readonly float ThrustDamageBonus;
	public readonly float SlashDamageBonus;
	public readonly float SummonDamageBonus;
	public readonly float ElementDamageBonus;
	public readonly float ZoneDamageBonus;
	public readonly float OrbDamageBonus;
	public readonly float ReceivedDamageRate;
	public readonly float CooldownRate;
	public readonly float CurseCooldownRate;
	public readonly float CurseDurationRate;
	public readonly float SelfDamageRate;
	public readonly float LifestealRate;

	/// <summary>효과 없음. 자세가 없거나(0) 그 캐릭터 언어에 자세 규칙이 없을 때.</summary>
	public static readonly StanceEffect None = default;

	// 규칙마다 쓰는 칸이 몇 개뿐이므로, 쓰는 칸만 이름을 붙여 넘기도록 모두 기본값 0을 둔다
	public StanceEffect(float damage = 0f, float thrust = 0f, float slash = 0f, float summon = 0f,
		float received = 0f, float cooldown = 0f, float selfDamage = 0f, float lifesteal = 0f,
		float element = 0f, float zone = 0f, float orb = 0f, float curseCooldown = 0f, float curseDuration = 0f)
	{
		DamageBonus = damage;
		ThrustDamageBonus = thrust;
		SlashDamageBonus = slash;
		SummonDamageBonus = summon;
		ElementDamageBonus = element;
		ZoneDamageBonus = zone;
		OrbDamageBonus = orb;
		ReceivedDamageRate = received;
		CooldownRate = cooldown;
		CurseCooldownRate = curseCooldown;
		CurseDurationRate = curseDuration;
		SelfDamageRate = selfDamage;
		LifestealRate = lifesteal;
	}

	/// <summary>
	/// 그 계열의 스킬이 받는 데미지 추가분 = 모든 스킬 추가분 + 그 계열 추가분. family가 null이면(출혈·중독 도트처럼 스킬 정보가 없음) 모든 스킬 추가분만.
	/// </summary>
	public float DamageBonusFor(SkillFamily? family)
	{
		switch (family)
		{
			case SkillFamily.Thrust: return DamageBonus + ThrustDamageBonus;
			case SkillFamily.Slash: return DamageBonus + SlashDamageBonus;
			case SkillFamily.Summon: return DamageBonus + SummonDamageBonus;
			case SkillFamily.Element: return DamageBonus + ElementDamageBonus;
			case SkillFamily.Zone: return DamageBonus + ZoneDamageBonus;
			case SkillFamily.Orb: return DamageBonus + OrbDamageBonus;
			default: return DamageBonus;
		}
	}

	/// <summary>
	/// 그 계열의 스킬에 거는 쿨타임 증감 = 모든 스킬 증감 + 보조 계열 증감(보조 계열일 때만, #546). family가 null이면(스킬이 아닌 함수) 모든 스킬 증감만.
	/// </summary>
	public float CooldownRateFor(SkillFamily? family) =>
		family == SkillFamily.Curse ? CooldownRate + CurseCooldownRate : CooldownRate;

	/// <summary>
	/// 그 계열의 스킬이 거는 상태 이상의 지속 시간 증감 (#546). 지금은 보조 계열(저주·뱀 속박·섬광)만 있고, 그 밖의 계열은 0.
	/// </summary>
	// 보조 계열만 보는 이유: 7의 배수의 이득(지속 시간)과 손해(쿨타임)가 같은 스킬 묶음에 걸려야 선택이 되기 때문.
	// 독 안개의 중독은 범위 계열이라 늘리지 않는다 (이우림 결정 11/18)
	public float StatusDurationRateFor(SkillFamily? family) =>
		family == SkillFamily.Curse ? CurseDurationRate : 0f;

	/// <summary>
	/// 효과 값을 화면 글로 나눠 gains(이득)와 losses(손해)에 더한다 (#545). 예: 홀수 → 이득 「찌르기 계열 데미지 +15%」, 손해 「휘두르기 계열 데미지 -15%」.
	/// </summary>
	// 글을 효과 값에서 만드는 이유: 스킬 트리의 규칙 카드가 이 글을 보여 주므로, 수치를 고쳐도 화면 글이 따로 어긋나지 않게 하기 위함
	public void Describe(List<string> gains, List<string> losses)
	{
		AddRate(gains, losses, DamageBonus, "데미지", true);
		AddRate(gains, losses, ThrustDamageBonus, "찌르기 계열 데미지", true);
		AddRate(gains, losses, SlashDamageBonus, "휘두르기 계열 데미지", true);
		AddRate(gains, losses, SummonDamageBonus, "소환 계열 데미지", true);
		AddRate(gains, losses, ElementDamageBonus, "원소 계열 데미지", true);
		AddRate(gains, losses, ZoneDamageBonus, "범위 계열 데미지", true);
		AddRate(gains, losses, OrbDamageBonus, "구슬 계열 데미지", true);
		AddRate(gains, losses, ReceivedDamageRate, "받는 피해", false);
		AddRate(gains, losses, CooldownRate, "쿨타임", false);
		AddRate(gains, losses, CurseDurationRate, "보조 계열 상태 이상 지속 시간", true);
		AddRate(gains, losses, CurseCooldownRate, "보조 계열 쿨타임", false);
		if (LifestealRate > 0f) gains.Add($"스킬이 준 데미지의 {Percent(LifestealRate)}% 회복");
		if (SelfDamageRate > 0f) losses.Add($"데미지를 준 스킬마다 자해 (최대 체력의 {Percent(SelfDamageRate)}%)");
	}

	// 증감 하나를 이득·손해 가운데 맞는 쪽에 넣는다. higherIsBetter: 값이 커지면 플레이어에게 좋은지 (받는 피해·쿨타임은 줄어야 좋다)
	static void AddRate(List<string> gains, List<string> losses, float value, string name, bool higherIsBetter)
	{
		if (Percent(Math.Abs(value)) == 0) return;
		string text = $"{name} {(value > 0f ? "+" : "-")}{Percent(Math.Abs(value))}%";
		((value > 0f) == higherIsBetter ? gains : losses).Add(text);
	}

	static int Percent(float rate) => (int)Math.Round(rate * 100f);

	// 규칙 하나의 효과를 더해 겹친다. 기획: 숫자가 여러 규칙에 해당하면 효과가 모두 겹쳐서 적용된다
	public static StanceEffect operator +(StanceEffect a, StanceEffect b) => new StanceEffect(
		a.DamageBonus + b.DamageBonus,
		a.ThrustDamageBonus + b.ThrustDamageBonus,
		a.SlashDamageBonus + b.SlashDamageBonus,
		a.SummonDamageBonus + b.SummonDamageBonus,
		a.ReceivedDamageRate + b.ReceivedDamageRate,
		a.CooldownRate + b.CooldownRate,
		a.SelfDamageRate + b.SelfDamageRate,
		a.LifestealRate + b.LifestealRate,
		a.ElementDamageBonus + b.ElementDamageBonus,
		a.ZoneDamageBonus + b.ZoneDamageBonus,
		a.OrbDamageBonus + b.OrbDamageBonus,
		a.CurseCooldownRate + b.CurseCooldownRate,
		a.CurseDurationRate + b.CurseDurationRate);
}

/// <summary>
/// 자세 규칙 표의 한 줄 (#545): 스킬 트리 규칙 노드 ID, 규칙 이름, 숫자 조건 글, 숫자 조건, 효과 값.
/// <para>
/// 효과 계산(StanceRule.EffectOf)과 스킬 트리의 규칙 카드(이름, 조건, 이득, 손해)가 같은 줄을 읽는다.
/// </para>
/// </summary>
public sealed class StanceRuleEntry
{
	/// <summary>스킬 트리 규칙 노드 ID. C는 c.stance.이름, Python은 py.stance.이름 (예: c.stance.odd)</summary>
	public readonly string NodeId;
	/// <summary>규칙 이름 (예: 적중)</summary>
	public readonly string Name;
	/// <summary>숫자 조건 글. 카드 문장 「자세 숫자가 ○○일 때」의 ○○ 자리 (예: 홀수, 40~60 사이)</summary>
	public readonly string Condition;
	/// <summary>이 규칙 하나의 효과</summary>
	public readonly StanceEffect Effect;
	/// <summary>이득 글들 (StanceEffect.Describe). 예: 찌르기 계열 데미지 +15%</summary>
	public readonly IReadOnlyList<string> Gains;
	/// <summary>손해 글들. 손해가 없는 규칙(완전수 등)은 비어 있다</summary>
	public readonly IReadOnlyList<string> Losses;

	readonly Func<int, bool> matches;

	public StanceRuleEntry(string nodeId, string name, string condition, Func<int, bool> matches, StanceEffect effect)
	{
		NodeId = nodeId;
		Name = name;
		Condition = condition;
		this.matches = matches;
		Effect = effect;
		var gains = new List<string>();
		var losses = new List<string>();
		effect.Describe(gains, losses);
		Gains = gains;
		Losses = losses;
	}

	/// <summary>자세 숫자 stance(1 이상)가 이 규칙의 조건에 맞으면 true.</summary>
	public bool Matches(int stance) => matches(stance);
}

/// <summary>
/// 캐릭터 언어 하나의 자세 규칙 (#538). 자세 숫자를 받아 그 숫자의 효과 값을 돌려준다.
/// <para>
/// - C 규칙(CStanceRule)과 Python 규칙(PythonStanceRule, #546)이 있다. 새 언어는 이 클래스를 상속한 규칙을 하나 더 만들고 For와 All에 연결한다.<br/>
/// - 규칙은 언어마다 표(Rules) 하나다. 효과는 조건에 맞는 줄의 효과를 모두 더한 것이다 (#545).<br/>
/// - 자세가 없으면(0) 어떤 규칙도 적용하지 않는다. 수학에서는 0이 짝수이고 3의 배수이지만, 「자세 없음」에 효과가 붙으면 안 되기 때문이다.<br/>
/// - 규칙은 스킬 트리 해금과 관계없이 처음부터 모두 적용된다. 해금은 규칙 내용을 보여 주는 일만 한다 (규칙 노드, SkillTreeNodeKind.Rule).
/// </para>
/// </summary>
// 효과를 언어별 클래스로 나눈 이유: 자세 상태(PlayerStance)와 함수(init_stance, get_stance)는 언어와 관계없이 같고, 효과만 언어마다 다르기 때문 (이우림 결정 11/18)
public abstract class StanceRule
{
	static readonly StanceRule C = new CStanceRule();
	static readonly StanceRule Python = new PythonStanceRule();
	static readonly StanceRule[] all = { C, Python };

	/// <summary>그 캐릭터 언어의 자세 규칙. 모르는 언어는 C 규칙이다.</summary>
	public static StanceRule For(string language) =>
		LanguageFamilies.Of(language) == LanguageFamily.Python ? Python : C;

	/// <summary>모든 언어의 자세 규칙 (C, Python). 스킬 트리 검사가 규칙 노드를 찾을 때 쓴다.</summary>
	public static IReadOnlyList<StanceRule> All => all;

	/// <summary>자세 함수(init_stance, get_stance)의 고정 ID들. C와 Python 모두. 스킬 노드이지만 SkillTable(스킬 표)에는 없다 (#545).</summary>
	public static readonly IReadOnlyList<string> FunctionIds = new[]
	{
		PredefinedFunctionIds.InitStance, PredefinedFunctionIds.GetStance,
		PredefinedFunctionIds.PyInitStance, PredefinedFunctionIds.PyGetStance,
	};

	/// <summary>이 언어의 규칙 표. 화면에 보이는 순서와 같다.</summary>
	public abstract IReadOnlyList<StanceRuleEntry> Rules { get; }

	/// <summary>자세 숫자 stance의 효과. 0 이하이면 효과 없음.</summary>
	public StanceEffect EffectOf(int stance)
	{
		StanceEffect effect = StanceEffect.None;
		if (stance <= 0) return effect;
		foreach (StanceRuleEntry rule in Rules)
		{
			if (rule.Matches(stance)) effect += rule.Effect;
		}
		return effect;
	}

	/// <summary>
	/// 규칙 노드 ID(c.stance.odd 등)로 규칙 표의 줄을 찾는다. 모든 언어의 표에서 찾고, 없으면 false (#545).
	/// </summary>
	public static bool TryGetRule(string nodeId, out StanceRuleEntry rule)
	{
		foreach (StanceRule language in all)
		{
			foreach (StanceRuleEntry entry in language.Rules)
			{
				if (entry.NodeId != nodeId) continue;
				rule = entry;
				return true;
			}
		}
		rule = null;
		return false;
	}

	/// <summary>소수이면 true. 1은 소수가 아니다.</summary>
	public static bool IsPrime(int n)
	{
		if (n < 2) return false;
		for (int i = 2; i * i <= n; i++)
			if (n % i == 0) return false;
		return true;
	}

	/// <summary>피보나치 수(1, 2, 3, 5, 8, 13, 21, 34, 55, 89, …)이면 true.</summary>
	public static bool IsFibonacci(int n)
	{
		if (n < 1) return false;
		int a = 1, b = 2;
		while (a < n)
		{
			int next = a + b;
			a = b;
			b = next;
		}
		return a == n;
	}

	/// <summary>완전수(자기 자신을 뺀 약수의 합이 자기 자신, 6, 28, …)이면 true.</summary>
	public static bool IsPerfect(int n)
	{
		if (n < 2) return false;
		int sum = 1;
		for (int i = 2; i * i <= n; i++)
		{
			if (n % i != 0) continue;
			sum += i;
			if (i != n / i) sum += n / i;
		}
		return sum == n;
	}

	/// <summary>회문수(앞에서 읽어도 뒤에서 읽어도 같은 수, 1~9, 11, 22, …, 99, 101, …)이면 true.</summary>
	public static bool IsPalindrome(int n)
	{
		if (n < 1) return false;
		int reversed = 0;
		for (int x = n; x > 0; x /= 10) reversed = reversed * 10 + x % 10;
		return reversed == n;
	}

	/// <summary>완전제곱수(1, 4, 9, …, 100, …)이면 true.</summary>
	public static bool IsSquare(int n)
	{
		if (n < 1) return false;
		int root = 1;
		while (root * root < n) root++;
		return root * root == n;
	}

	/// <summary>세제곱수(1, 8, 27, 64, …)이면 true.</summary>
	public static bool IsCube(int n)
	{
		if (n < 1) return false;
		int root = 1;
		while (root * root * root < n) root++;
		return root * root * root == n;
	}
}

/// <summary>
/// C 캐릭터의 자세 규칙 (#538, Notion 「자세와 소환검 기획」의 효과 수치(임시)).
/// <para>
/// - 홀수: 찌르기 계열 데미지 +15%, 휘두르기 계열 데미지 -15%<br/>
/// - 짝수: 휘두르기 계열 데미지 +15%, 찌르기 계열 데미지 -15%<br/>
/// - 3의 배수: 쿨타임 -15%, 받는 피해 +10%<br/>
/// - 5의 배수: 데미지 +10%, 쿨타임 +10%<br/>
/// - 40~60: 스킬이 준 데미지의 5% 회복, 데미지 -5%<br/>
/// - 소수: 데미지 +20%, 데미지를 준 스킬마다 자해 1%(최대 체력 비율, 11/18 2%에서 낮춤)<br/>
/// - 피보나치 수: 소환 계열 데미지 +20%, 찌르기 계열 데미지 -10%<br/>
/// - 완전수: 데미지 +30%<br/>
/// 예: 5는 홀수+5의 배수+소수+피보나치 수(데미지 +30%, 찌르기 +5%, 휘두르기 -15%, 소환 +20%, 쿨타임 +10%, 자해).
/// </para>
/// </summary>
// 수치는 모두 임시다. 해 보고 조정한다. 「데미지」는 모든 스킬과 출혈·중독 도트, 계열 데미지는 그 계열 스킬만이다 (이우림 결정 11/18)
public class CStanceRule : StanceRule
{
	/// <summary>40~60 규칙의 범위 (양 끝 포함).</summary>
	public const int MiddleMin = 40;
	public const int MiddleMax = 60;

	// 스킬 트리 규칙 노드 ID (#545). 한 번 정하면 바꾸지 않는다 (해금 목록에 저장됨)
	public const string OddNode = "c.stance.odd";
	public const string EvenNode = "c.stance.even";
	public const string ThreeNode = "c.stance.three";
	public const string FiveNode = "c.stance.five";
	public const string MiddleNode = "c.stance.middle";
	public const string PrimeNode = "c.stance.prime";
	public const string FibonacciNode = "c.stance.fibonacci";
	public const string PerfectNode = "c.stance.perfect";

	// 규칙 이름은 Notion 「캐릭터 자세와 소환검 기획」 1. C 자세의 「자세 규칙」 표
	static readonly StanceRuleEntry[] Table =
	{
		new StanceRuleEntry(OddNode, "적중", "홀수", n => n % 2 == 1, new StanceEffect(thrust: 0.15f, slash: -0.15f)),
		new StanceRuleEntry(EvenNode, "일망타진", "짝수", n => n % 2 == 0, new StanceEffect(slash: 0.15f, thrust: -0.15f)),
		new StanceRuleEntry(ThreeNode, "조급함", "3의 배수", n => n % 3 == 0, new StanceEffect(cooldown: -0.15f, received: 0.1f)),
		new StanceRuleEntry(FiveNode, "신중함", "5의 배수", n => n % 5 == 0, new StanceEffect(damage: 0.1f, cooldown: 0.1f)),
		new StanceRuleEntry(MiddleNode, "흡혈", $"{MiddleMin}~{MiddleMax} 사이", n => n >= MiddleMin && n <= MiddleMax,
			new StanceEffect(lifesteal: 0.05f, damage: -0.05f)),
		new StanceRuleEntry(PrimeNode, "혹사", "소수", IsPrime, new StanceEffect(damage: 0.2f, selfDamage: 0.01f)),
		new StanceRuleEntry(FibonacciNode, "지휘", "피보나치 수(1, 2, 3, 5, 8, 13, 21, 34, 55, 89)", IsFibonacci,
			new StanceEffect(summon: 0.2f, thrust: -0.1f)),
		// 평균 50번을 뽑아야 나오므로 손해를 두지 않는다 (기획)
		new StanceRuleEntry(PerfectNode, "완벽", "완전수(6, 28)", IsPerfect, new StanceEffect(damage: 0.3f)),
	};

	public override IReadOnlyList<StanceRuleEntry> Rules => Table;
}

/// <summary>
/// Python 캐릭터의 자세 규칙 (#546, Notion 「캐릭터 자세와 소환검 기획 (C, Python)」 3. Python 자세의 효과 수치(임시)).
/// <para>
/// - 큰 수(51~100, 직격): 원소 계열 데미지 +15%, 범위 계열 데미지 -15%<br/>
/// - 작은 수(1~50, 포화): 범위 계열 데미지 +15%, 원소 계열 데미지 -15%<br/>
/// - 4의 배수(응축): 구슬 계열 데미지 +20%, 원소 계열 데미지 -10%<br/>
/// - 회문수(경솔): 쿨타임 -15%, 받는 피해 +10%<br/>
/// - 7의 배수(집요함): 보조 계열 상태 이상 지속 시간 +30%, 보조 계열 쿨타임 +20%<br/>
/// - 완전제곱수(비늘): 받는 피해 -15%, 데미지 -5%<br/>
/// - 세제곱수(각성): 데미지 +25%<br/>
/// 예: 64는 큰 수+4의 배수+완전제곱수+세제곱수(원소 +5%, 범위 -15%, 구슬 +20%, 받는 피해 -15%, 데미지 +20%).
/// </para>
/// </summary>
// 수치는 모두 임시다. 계열은 Python 스킬 트리의 묶음(SkillTable의 Family)을 따른다. 「데미지」는 C와 같이 모든 스킬과 출혈·중독 도트다
public class PythonStanceRule : StanceRule
{
	/// <summary>큰 수 규칙의 가장 작은 숫자. 이보다 작으면(1~50) 작은 수 규칙이다.</summary>
	public const int BigMin = 51;

	// 스킬 트리 규칙 노드 ID (#545). 한 번 정하면 바꾸지 않는다 (해금 목록에 저장됨)
	public const string BigNode = "py.stance.big";
	public const string SmallNode = "py.stance.small";
	public const string FourNode = "py.stance.four";
	public const string PalindromeNode = "py.stance.palindrome";
	public const string SevenNode = "py.stance.seven";
	public const string SquareNode = "py.stance.square";
	public const string CubeNode = "py.stance.cube";

	// 규칙 이름은 Notion 「캐릭터 자세와 소환검 기획」 3. Python 자세의 「자세 규칙」 표
	static readonly StanceRuleEntry[] Table =
	{
		new StanceRuleEntry(BigNode, "직격", $"큰 수({BigMin}~100)", n => n >= BigMin, new StanceEffect(element: 0.15f, zone: -0.15f)),
		new StanceRuleEntry(SmallNode, "포화", $"작은 수(1~{BigMin - 1})", n => n < BigMin, new StanceEffect(zone: 0.15f, element: -0.15f)),
		new StanceRuleEntry(FourNode, "응축", "4의 배수", n => n % 4 == 0, new StanceEffect(orb: 0.2f, element: -0.1f)),
		new StanceRuleEntry(PalindromeNode, "경솔", "회문수(1~9, 11, 22, …, 99)", IsPalindrome, new StanceEffect(cooldown: -0.15f, received: 0.1f)),
		new StanceRuleEntry(SevenNode, "집요함", "7의 배수", n => n % 7 == 0, new StanceEffect(curseDuration: 0.3f, curseCooldown: 0.2f)),
		new StanceRuleEntry(SquareNode, "비늘", "완전제곱수(1, 4, 9, …, 100)", IsSquare, new StanceEffect(received: -0.15f, damage: -0.05f)),
		// 평균 25번을 뽑아야 나오므로 손해를 두지 않는다 (기획)
		new StanceRuleEntry(CubeNode, "각성", "세제곱수(1, 8, 27, 64)", IsCube, new StanceEffect(damage: 0.25f)),
	};

	public override IReadOnlyList<StanceRuleEntry> Rules => Table;
}
