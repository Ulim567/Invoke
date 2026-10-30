using UnityEngine;

/// <summary>
/// 플레이어와 몬스터가 함께 쓰는 데미지 계산. 계산만 하고, 체력을 줄이거나 연출을 하지 않는다 (코드 작성 규칙 1).
/// <para>
/// 계산식<br/>
/// - 데미지 = 공격력 × 스킬 배율 × (1 + 추가분 합)<br/>
/// - 추가분은 조건 배율(출혈, 표식 등)의 "+몇 %"를 더한 값이다. 예: 표식(+100%) + 집중찌르기 출혈(+50%) → 1 + 1.0 + 0.5 = 2.5배<br/>
/// - 치명타가 나면 치명타 추가분도 더한다.<br/>
/// - 대상에게 표식(StatusEffects)이 있으면 표식 추가분(DamageBonus.Mark, +100%)을 자동으로 더한다. 스킬은 표식을 BonusRate에 넣지 않는다.<br/>
/// - 결과는 반올림(0.5는 올림)한 정수다. 0보다 크면 최소 1이다.
/// </para>
/// <para>
/// 처형<br/>
/// - CanExecute인 공격(검 던지기)이 체력 2% 이하인 대상을 맞히면 처형한다. 보스는 처형하지 않는다.<br/>
/// - 처형이면 데미지는 대상의 남은 체력과 같다. TakeDamage로 넘기면 대상이 죽는다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 스킬: DamageRequest를 만들어 Calculate를 부르고, 결과의 Damage를 맞은 대상의 IDamageable.TakeDamage로 넘긴다.<br/>
/// - 몬스터 공격: DamageRequest.Flat(데미지)로 만들어 Calculate를 부른다.<br/>
/// - 조건 배율 값은 DamageBonus에 모아 두었다. 어떤 조건이 붙는지는 스킬이 정해서 BonusRate에 더해 넘긴다.
/// </para>
/// </summary>
public static class DamageCalculator
{
	/// <summary>처형할 수 있는 체력 비율(%). 최대 체력의 2% 이하면 처형한다.</summary>
	public const int ExecuteHpPercent = 2;

	/// <summary>
	/// request대로 데미지를 계산한다. 처형 판정이 필요 없는 경우(대상 정보가 없을 때) 쓴다.
	/// 치명타는 UnityEngine.Random으로 굴린다.
	/// </summary>
	public static DamageResult Calculate(in DamageRequest request)
		=> Calculate(request, DamageTarget.None, Random.value);

	/// <summary>
	/// request대로 target에게 줄 데미지를 계산한다. 처형 판정까지 한다.
	/// 치명타는 UnityEngine.Random으로 굴린다.
	/// </summary>
	public static DamageResult Calculate(in DamageRequest request, in DamageTarget target)
		=> Calculate(request, target, Random.value);

	/// <summary>
	/// critRoll(0 이상 1 미만)을 직접 넣어 계산한다. critRoll이 CritChance보다 작으면 치명타다.
	/// 결과가 항상 같아야 하는 테스트에서 쓴다.
	/// </summary>
	public static DamageResult Calculate(in DamageRequest request, in DamageTarget target, float critRoll)
	{
		// 처형은 배율 계산보다 먼저 본다. 처형이면 남은 체력을 그대로 데미지로 준다
		if (CanExecute(request, target))
			return new DamageResult(target.Hp, isCritical: false, isExecution: true);

		bool isCritical = critRoll < request.CritChance;
		float bonus = request.BonusRate + (isCritical ? request.CritBonusRate : 0f) + (target.IsMarked ? DamageBonus.Mark : 0f);
		float raw = request.AttackPower * request.SkillMultiplier * (1f + bonus);

		return new DamageResult(ToDamage(raw), isCritical, isExecution: false);
	}

	/// <summary>
	/// 이 공격으로 target을 처형할 수 있으면 true. (처형 가능한 공격, 보스 아님, 살아 있고 체력 2% 이하)
	/// </summary>
	public static bool CanExecute(in DamageRequest request, in DamageTarget target)
	{
		if (!request.CanExecute || !target.HasValue || target.IsBoss) return false;
		if (target.Hp <= 0 || target.MaxHp <= 0) return false;
		// 0.02f는 정확히 0.02가 아니어서(0.0199…) 2/100이 2% 이하로 안 잡힐 수 있다. 정수끼리 비교한다
		return (long)target.Hp * 100 <= (long)target.MaxHp * ExecuteHpPercent;
	}

	// 반올림해서 정수로 바꾼다. Mathf.RoundToInt는 2.5를 2로 만드는(짝수 쪽 반올림) 방식이라 쓰지 않는다
	static int ToDamage(float raw)
	{
		if (raw <= 0f) return 0;
		return Mathf.Max(1, Mathf.FloorToInt(raw + 0.5f));
	}
}

/// <summary>
/// 데미지 계산에 넘기는 공격 정보. 스킬(또는 몬스터 공격)이 만든다.
/// </summary>
public struct DamageRequest
{
	/// <summary>공격력. 플레이어는 PlayerStats.AttackPower를 넣는다.</summary>
	public int AttackPower;

	/// <summary>스킬 배율. 1이면 공격력 그대로(100%)다.</summary>
	public float SkillMultiplier;

	/// <summary>조건 배율 추가분의 합. +50%면 0.5, +100%면 1.0. DamageBonus의 값을 더해 넣는다.</summary>
	public float BonusRate;

	/// <summary>치명타 확률 (0~1). 0이면 치명타가 나지 않는다.</summary>
	public float CritChance;

	/// <summary>치명타가 났을 때 더하는 추가분. 검 깊이찌르기는 DamageBonus.CritDeepThrust(+150%).</summary>
	public float CritBonusRate;

	/// <summary>처형할 수 있는 공격이면 true. 검 던지기만 true로 둔다.</summary>
	public bool CanExecute;

	/// <summary>공격력과 스킬 배율로 만든다. 나머지는 필요할 때 필드에 넣는다.</summary>
	public DamageRequest(int attackPower, float skillMultiplier)
	{
		AttackPower = attackPower;
		SkillMultiplier = skillMultiplier;
		BonusRate = 0f;
		CritChance = 0f;
		CritBonusRate = 0f;
		CanExecute = false;
	}

	/// <summary>
	/// 배율 없이 정해진 데미지를 그대로 주는 공격. 몬스터 공격(AttackEffect.Amount)에 쓴다.
	/// </summary>
	public static DamageRequest Flat(int damage) => new DamageRequest(damage, 1f);
}

/// <summary>
/// 맞은 대상의 정보. 처형 판정과 표식 배율에 쓴다. Monster라면 FromMonster로 만든다.
/// </summary>
public readonly struct DamageTarget
{
	/// <summary>대상 정보가 없음. 처형 판정을 하지 않는다.</summary>
	public static readonly DamageTarget None = default;

	public readonly bool HasValue;
	public readonly int Hp;
	public readonly int MaxHp;
	public readonly bool IsBoss;
	/// <summary>표식 상태면 true. 데미지 +100%</summary>
	public readonly bool IsMarked;

	public DamageTarget(int hp, int maxHp, bool isBoss, bool isMarked = false)
	{
		HasValue = true;
		Hp = hp;
		MaxHp = maxHp;
		IsBoss = isBoss;
		IsMarked = isMarked;
	}

	/// <summary>
	/// Monster에서 만든다. Hp, MaxHp, IsBoss를 읽고 (docs/systems/monster.md), 같은 오브젝트의 StatusEffects에서 표식을 읽는다.
	/// </summary>
	public static DamageTarget FromMonster(Monster monster)
	{
		if (monster == null) return None;
		StatusEffects status = monster.GetComponent<StatusEffects>();
		return new DamageTarget(monster.Hp, monster.MaxHp, monster.IsBoss, status != null && status.IsMarked);
	}
}

/// <summary>
/// 데미지 계산 결과.
/// </summary>
public readonly struct DamageResult
{
	/// <summary>대상에게 줄 데미지 (반올림한 정수). IDamageable.TakeDamage로 넘긴다.</summary>
	public readonly int Damage;

	/// <summary>치명타가 났으면 true. 데미지 숫자 색 등 연출에서 쓴다.</summary>
	public readonly bool IsCritical;

	/// <summary>처형이면 true. 처형 연출에서 쓴다.</summary>
	public readonly bool IsExecution;

	public DamageResult(int damage, bool isCritical, bool isExecution)
	{
		Damage = damage;
		IsCritical = isCritical;
		IsExecution = isExecution;
	}
}
