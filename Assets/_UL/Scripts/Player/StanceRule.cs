/// <summary>
/// 자세 숫자 하나가 주는 효과 값 (#538). 여러 규칙에 해당하면 규칙마다의 값을 더한 것이다.
/// <para>
/// - 데미지: 모든 스킬에 붙는 추가분(DamageBonus)과 스킬 계열에만 붙는 추가분(찌르기·휘두르기·소환)이 있다. DamageBonusFor로 둘을 더해 읽는다.<br/>
/// - ReceivedDamageRate: 받는 피해의 증감 (+0.1이면 10% 더 아프다).<br/>
/// - CooldownRate: 쿨타임의 증감 (-0.15면 15% 짧게, +0.1이면 10% 길게).<br/>
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
	public readonly float ReceivedDamageRate;
	public readonly float CooldownRate;
	public readonly float SelfDamageRate;
	public readonly float LifestealRate;

	/// <summary>효과 없음. 자세가 없거나(0) 그 캐릭터 언어에 자세 규칙이 없을 때.</summary>
	public static readonly StanceEffect None = default;

	// 규칙마다 쓰는 칸이 몇 개뿐이므로, 쓰는 칸만 이름을 붙여 넘기도록 모두 기본값 0을 둔다
	public StanceEffect(float damage = 0f, float thrust = 0f, float slash = 0f, float summon = 0f,
		float received = 0f, float cooldown = 0f, float selfDamage = 0f, float lifesteal = 0f)
	{
		DamageBonus = damage;
		ThrustDamageBonus = thrust;
		SlashDamageBonus = slash;
		SummonDamageBonus = summon;
		ReceivedDamageRate = received;
		CooldownRate = cooldown;
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
			default: return DamageBonus;
		}
	}

	// 규칙 하나의 효과를 더해 겹친다. 기획: 숫자가 여러 규칙에 해당하면 효과가 모두 겹쳐서 적용된다
	public static StanceEffect operator +(StanceEffect a, StanceEffect b) => new StanceEffect(
		a.DamageBonus + b.DamageBonus,
		a.ThrustDamageBonus + b.ThrustDamageBonus,
		a.SlashDamageBonus + b.SlashDamageBonus,
		a.SummonDamageBonus + b.SummonDamageBonus,
		a.ReceivedDamageRate + b.ReceivedDamageRate,
		a.CooldownRate + b.CooldownRate,
		a.SelfDamageRate + b.SelfDamageRate,
		a.LifestealRate + b.LifestealRate);
}

/// <summary>
/// 캐릭터 언어 하나의 자세 규칙 (#538). 자세 숫자를 받아 그 숫자의 효과 값을 돌려준다.
/// <para>
/// - 지금은 C 규칙(CStanceRule)만 있다. Python 캐릭터의 자세 기획이 정해지면 이 클래스를 상속한 규칙을 하나 더 만들고 For에 연결한다.<br/>
/// - 자세가 없으면(0) 어떤 규칙도 적용하지 않는다. 수학에서는 0이 짝수이고 3의 배수이지만, 「자세 없음」에 효과가 붙으면 안 되기 때문이다.<br/>
/// - 규칙은 스킬 트리 해금과 관계없이 처음부터 모두 적용된다. 해금은 규칙 내용을 보여 주는 일만 한다.
/// </para>
/// </summary>
// 효과를 언어별 클래스로 나눈 이유: 자세 상태(PlayerStance)와 함수(init_stance, get_stance)는 언어와 관계없이 같고, 효과만 언어마다 다르기 때문 (이우림 결정 11/18)
public abstract class StanceRule
{
	static readonly StanceRule C = new CStanceRule();

	/// <summary>그 캐릭터 언어의 자세 규칙. 규칙이 없는 언어(지금은 Python)는 null이다.</summary>
	public static StanceRule For(string language) =>
		LanguageFamilies.Of(language) == LanguageFamily.Python ? null : C;

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
}

/// <summary>
/// C 캐릭터의 자세 규칙 (#538, Notion 「자세와 소환검 기획」의 효과 수치(임시)).
/// <para>
/// - 홀수: 찌르기 계열 데미지 +15%, 휘두르기 계열 데미지 -15%<br/>
/// - 짝수: 휘두르기 계열 데미지 +15%, 찌르기 계열 데미지 -15%<br/>
/// - 3의 배수: 쿨타임 -15%, 받는 피해 +10%<br/>
/// - 5의 배수: 데미지 +10%, 쿨타임 +10%<br/>
/// - 40~60: 스킬이 준 데미지의 5% 회복, 데미지 -5%<br/>
/// - 소수: 데미지 +20%, 데미지를 준 스킬마다 자해 2%(최대 체력 비율)<br/>
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
	static readonly StanceEffect Prime = new StanceEffect(damage: 0.2f, selfDamage: 0.02f);
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
