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
/// 캐릭터 언어 하나의 자세 규칙 (#538). 자세 숫자를 받아 그 숫자의 효과 값을 돌려준다.
/// <para>
/// - C 규칙(CStanceRule)과 Python 규칙(PythonStanceRule, #546)이 있다. 새 언어는 이 클래스를 상속한 규칙을 하나 더 만들고 For에 연결한다.<br/>
/// - 자세가 없으면(0) 어떤 규칙도 적용하지 않는다. 수학에서는 0이 짝수이고 3의 배수이지만, 「자세 없음」에 효과가 붙으면 안 되기 때문이다.<br/>
/// - 규칙은 스킬 트리 해금과 관계없이 처음부터 모두 적용된다. 해금은 규칙 내용을 보여 주는 일만 한다.
/// </para>
/// </summary>
// 효과를 언어별 클래스로 나눈 이유: 자세 상태(PlayerStance)와 함수(init_stance, get_stance)는 언어와 관계없이 같고, 효과만 언어마다 다르기 때문 (이우림 결정 11/18)
public abstract class StanceRule
{
	static readonly StanceRule C = new CStanceRule();
	static readonly StanceRule Python = new PythonStanceRule();

	/// <summary>그 캐릭터 언어의 자세 규칙. 모르는 언어는 C 규칙이다.</summary>
	public static StanceRule For(string language) =>
		LanguageFamilies.Of(language) == LanguageFamily.Python ? Python : C;

	/// <summary>자세 숫자 stance의 효과. 0 이하이면 효과 없음.</summary>
	public StanceEffect EffectOf(int stance) => stance <= 0 ? StanceEffect.None : EffectOfStance(stance);

	// 0보다 큰 자세 숫자의 효과. 언어마다 덮어쓴다
	protected abstract StanceEffect EffectOfStance(int stance);

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
	static readonly StanceEffect Odd = new StanceEffect(thrust: 0.15f, slash: -0.15f);
	static readonly StanceEffect Even = new StanceEffect(slash: 0.15f, thrust: -0.15f);
	static readonly StanceEffect MultipleOfThree = new StanceEffect(cooldown: -0.15f, received: 0.1f);
	static readonly StanceEffect MultipleOfFive = new StanceEffect(damage: 0.1f, cooldown: 0.1f);
	static readonly StanceEffect Middle = new StanceEffect(lifesteal: 0.05f, damage: -0.05f);
	static readonly StanceEffect Prime = new StanceEffect(damage: 0.2f, selfDamage: 0.01f);
	static readonly StanceEffect Fibonacci = new StanceEffect(summon: 0.2f, thrust: -0.1f);
	// 평균 50번을 뽑아야 나오므로 손해를 두지 않는다 (기획)
	static readonly StanceEffect Perfect = new StanceEffect(damage: 0.3f);

	/// <summary>40~60 규칙의 범위 (양 끝 포함).</summary>
	public const int MiddleMin = 40;
	public const int MiddleMax = 60;

	protected override StanceEffect EffectOfStance(int stance)
	{
		StanceEffect effect = stance % 2 == 1 ? Odd : Even;
		if (stance % 3 == 0) effect += MultipleOfThree;
		if (stance % 5 == 0) effect += MultipleOfFive;
		if (stance >= MiddleMin && stance <= MiddleMax) effect += Middle;
		if (IsPrime(stance)) effect += Prime;
		if (IsFibonacci(stance)) effect += Fibonacci;
		if (IsPerfect(stance)) effect += Perfect;
		return effect;
	}
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
	static readonly StanceEffect Big = new StanceEffect(element: 0.15f, zone: -0.15f);
	static readonly StanceEffect Small = new StanceEffect(zone: 0.15f, element: -0.15f);
	static readonly StanceEffect MultipleOfFour = new StanceEffect(orb: 0.2f, element: -0.1f);
	static readonly StanceEffect Palindrome = new StanceEffect(cooldown: -0.15f, received: 0.1f);
	static readonly StanceEffect MultipleOfSeven = new StanceEffect(curseDuration: 0.3f, curseCooldown: 0.2f);
	static readonly StanceEffect Square = new StanceEffect(received: -0.15f, damage: -0.05f);
	// 평균 25번을 뽑아야 나오므로 손해를 두지 않는다 (기획)
	static readonly StanceEffect Cube = new StanceEffect(damage: 0.25f);

	/// <summary>큰 수 규칙의 가장 작은 숫자. 이보다 작으면(1~50) 작은 수 규칙이다.</summary>
	public const int BigMin = 51;

	protected override StanceEffect EffectOfStance(int stance)
	{
		StanceEffect effect = stance >= BigMin ? Big : Small;
		if (stance % 4 == 0) effect += MultipleOfFour;
		if (IsPalindrome(stance)) effect += Palindrome;
		if (stance % 7 == 0) effect += MultipleOfSeven;
		if (IsSquare(stance)) effect += Square;
		if (IsCube(stance)) effect += Cube;
		return effect;
	}
}
