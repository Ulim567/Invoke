using UnityEngine;

/// <summary>
/// 플레이어 한 명의 자세 숫자를 가지는 컴포넌트 (#538). 자세 숫자는 이 컴포넌트만 바꾼다.
/// <para>
/// - 자세는 1~100의 int이고, 자세가 없으면 0이다. init_stance()가 Roll로 다시 뽑고, 장소를 옮기면 InvokeNetworkManager가 Clear로 없앤다.<br/>
/// - 효과는 캐릭터 언어의 자세 규칙(StanceRule)이 정한다. 규칙이 없는 언어(지금은 Python)는 자세가 있어도 효과가 없다.<br/>
/// - 효과를 쓰는 곳: 데미지(DamageBonusOf), 받는 피해(ApplyReceivedDamage), 쿨타임(CooldownRateOf, CooldownTracker가 읽음),
///   자해·회복(NotifySkillDealtDamage, 스킬이 데미지를 준 뒤 부름).
/// </para>
/// <para>
/// 네트워크: 서버(또는 네트워크 없는 테스트)만 값을 가진다. 화면에 보이지 않는 값이고, 실행기와 데미지 판정이 서버에서 돌기 때문에 보내지 않는다.
/// 플레이어는 printf로 출력해서 숫자를 확인한다.
/// </para>
/// </summary>
// 공용 Player.prefab 최상위(PlayerStats, PlayerHealth와 같은 곳)에 붙인다
public class PlayerStance : MonoBehaviour
{
	/// <summary>자세가 없을 때의 숫자.</summary>
	public const int NoStance = 0;
	/// <summary>init_stance()가 뽑는 가장 작은 숫자.</summary>
	public const int MinStance = 1;
	/// <summary>init_stance()가 뽑는 가장 큰 숫자.</summary>
	public const int MaxStance = 100;

	PlayerHealth health;
	// 다음 Update에 깎을 자해량과 채울 회복량. 바로 바꾸지 않는 이유는 NotifySkillDealtDamage의 주석
	int pendingSelfDamage;
	int pendingHeal;
	// 회복량의 1 미만 부분. 모아 두었다가 1이 될 때마다 회복한다 (이우림 결정 11/18: 공격력 10의 5%인 0.5를 버리거나 1로 올리지 않고 정확히 5%를 주기 위함)
	float healRemainder;

	/// <summary>지금 자세 숫자. 자세가 없으면 0.</summary>
	public int Stance { get; private set; }

	/// <summary>지금 자세의 효과. 자세가 없거나 캐릭터 언어에 자세 규칙이 없으면 효과 없음.</summary>
	public StanceEffect Effect
	{
		get
		{
			StanceRule rule = StanceRule.For(PlayerCharacter.LanguageOf(gameObject));
			return rule != null ? rule.EffectOf(Stance) : StanceEffect.None;
		}
	}

	void Awake() => health = GetComponent<PlayerHealth>();

	/// <summary>
	/// [서버] 자세를 1~100 무작위 숫자로 다시 정하고 그 숫자를 돌려준다. init_stance()가 부른다.
	/// </summary>
	public int Roll()
	{
		SetStance(Random.Range(MinStance, MaxStance + 1));
		return Stance;
	}

	/// <summary>
	/// [서버] 자세를 없앤다(0). 장소를 옮길 때 InvokeNetworkManager가 부른다.
	/// </summary>
	public void Clear() => SetStance(NoStance);

	// 시험에서 원하는 숫자로 정할 때도 쓴다 (StanceTest)
	// 모아 둔 회복량의 1 미만 부분은 버린다. 다른 자세에서 모은 것이 새 자세로 넘어가지 않게 하기 위함
	internal void SetStance(int stance)
	{
		Stance = Mathf.Clamp(stance, NoStance, MaxStance);
		healRemainder = 0f;
	}

	/// <summary>
	/// [서버] 받는 피해에 자세의 증감을 적용한 값. PlayerHealth.TakeDamage가 피해를 줄이기 전에 부른다.<br/>
	/// 반올림은 데미지 계산(DamageCalculator)과 같다. 0보다 큰 피해는 최소 1이다.
	/// </summary>
	public int ApplyReceivedDamage(int damage)
	{
		float rate = Effect.ReceivedDamageRate;
		if (damage <= 0 || rate == 0f) return damage;
		DamageRequest request = DamageRequest.Flat(damage);
		request.BonusRate = rate;
		return DamageCalculator.Calculate(request).Damage;
	}

	/// <summary>
	/// [서버] 스킬이 몬스터에게 damage만큼 데미지를 준 뒤 부른다(DataDrivenSkill.NotifyDamageDealt). 출혈·중독 도트는 부르지 않는다.<br/>
	/// - 회복: 준 데미지 × 회복 비율. 1 미만 부분은 모아 두었다가 1이 될 때마다 회복한다.<br/>
	/// - 자해: firstOfCast(스킬 시전 하나가 처음 데미지를 줌)일 때만 최대 체력 × 자해 비율. 경직·무적 없이, 무적 중에도 깎는다(PlayerHealth.TakeSelfDamage).
	/// </summary>
	// 바로 바꾸지 않고 다음 Update에 바꾸는 이유: 이 함수는 실행기의 틱 안(스킬 OnTick)에서도 불린다.
	// 자해로 죽으면 실행기가 모든 작업을 끊는데(FunctionExecutorNetwork), 틱 도중에 그 스킬이 끊기면 남은 스킬 코드가 끊긴 스킬 위에서 돌기 때문
	public void NotifySkillDealtDamage(int damage, bool firstOfCast)
	{
		if (damage <= 0 || health == null) return;
		StanceEffect effect = Effect;

		if (effect.LifestealRate > 0f)
		{
			healRemainder += damage * effect.LifestealRate;
			int whole = Mathf.FloorToInt(healRemainder);
			healRemainder -= whole;
			pendingHeal += whole;
		}

		// 최대 체력 × 비율을 데미지 계산과 같게 반올림한다 (100 × 2% = 2, 0보다 크면 최소 1)
		if (firstOfCast && effect.SelfDamageRate > 0f)
			pendingSelfDamage += DamageCalculator.Calculate(new DamageRequest(health.MaxHp, effect.SelfDamageRate)).Damage;
	}

	// 회복을 자해보다 먼저 한다. 같은 프레임에 둘 다 있으면(41, 43 등) 회복으로 버틸 수 있게 하기 위함
	void Update()
	{
		if (health == null || (pendingHeal <= 0 && pendingSelfDamage <= 0)) return;
		int heal = pendingHeal;
		int selfDamage = pendingSelfDamage;
		pendingHeal = 0;
		pendingSelfDamage = 0;
		if (heal > 0) health.Heal(heal);
		if (selfDamage > 0) health.TakeSelfDamage(selfDamage);
	}

	/// <summary>
	/// attacker(플레이어)의 지금 자세가 주는 데미지 추가분. DamageRequest.BonusRate에 더한다. 자세가 없으면 0.<br/>
	/// 스킬은 자기 계열(SkillData.Family)을 넘긴다(DataDrivenSkill.StanceDamageBonus). 출혈·중독 도트는 계열 없이(null) 불러 모든 스킬 추가분만 받는다.
	/// </summary>
	public static float DamageBonusOf(GameObject attacker, SkillFamily? family = null) =>
		attacker != null && attacker.TryGetComponent(out PlayerStance stance) ? stance.Effect.DamageBonusFor(family) : 0f;

	/// <summary>caster의 지금 자세의 쿨타임 증감(-0.15면 15% 짧게). CooldownTracker가 쿨타임을 걸 때 곱한다. 자세가 없으면 0.</summary>
	public static float CooldownRateOf(GameObject caster) =>
		caster != null && caster.TryGetComponent(out PlayerStance stance) ? stance.Effect.CooldownRate : 0f;
}
