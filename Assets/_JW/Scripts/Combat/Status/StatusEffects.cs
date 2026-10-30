using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>상태 이상 종류 (노션 「C 캐릭터 - 1차」 5. 상태이상)</summary>
public enum StatusType
{
	Bleed, // 출혈: 도트 데미지
	Mark,  // 표식: 어떤 스킬에 맞아도 데미지 2.0배
	Stun,  // 기절: 행동 불가
	Blind, // 실명: 이 대상의 공격이 빗나감
	Slow,  // 이속 감소: 이동 속도 감소
}

/// <summary>
/// 상태 이상의 기본 수치. 전부 임시 값이다 (기획 「C 캐릭터 - 1차」 10. 아직 정하지 않은 것). 기절 1초만 기획 값이다.
/// </summary>
public static class StatusDefaults
{
	public const float BleedDuration = 5f;
	public const float BleedInterval = 1f;
	public const int BleedDamage = 5;
	public const float MarkDuration = 5f;
	public const float StunDuration = 1f;
	public const float BlindDuration = 3f;
	public const float SlowDuration = 3f;
	/// <summary>이속 감소 중 이동 속도 배율 (0.5 = 50% 감소)</summary>
	public const float SlowMultiplier = 0.5f;

	public static float DurationOf(StatusType type) => type switch
	{
		StatusType.Bleed => BleedDuration,
		StatusType.Mark => MarkDuration,
		StatusType.Stun => StunDuration,
		StatusType.Blind => BlindDuration,
		StatusType.Slow => SlowDuration,
		_ => 0f,
	};
}

/// <summary>
/// 대상(몬스터)에 걸린 상태 이상을 관리하는 컴포넌트. 몬스터 프리팹 최상위 오브젝트에 붙인다 (#97).
/// <para>
/// - 상태는 이 컴포넌트만 바꾼다 (코드 작성 규칙 2). 다른 스크립트는 Apply, Clear를 부르고 Has, IsStunned 등을 읽는다.<br/>
/// - 같은 상태를 다시 걸면 지속 시간만 새로 시작한다 (겹치지 않음).<br/>
/// - 출혈: 걸려 있는 동안 BleedInterval마다 BleedDamage를 DamageCalculator를 거쳐 같은 오브젝트의 IDamageable.TakeDamage로 준다.<br/>
/// - 표식: DamageTarget.FromMonster가 읽어서 DamageCalculator가 데미지 +100%를 자동으로 붙인다.<br/>
/// - 기절·이속 감소·실명: 값만 공개한다. 몬스터 AI·공격이 읽어서 반응한다 (김도훈 「몬스터가 상태 이상에 반응하고 죽으면 드롭하기」).<br/>
/// - 대상이 죽으면 모든 상태를 지운다.
/// </para>
/// </summary>
public class StatusEffects : MonoBehaviour
{
	// 걸려 있는 상태와 끝나는 시각
	readonly Dictionary<StatusType, float> endTimes = new Dictionary<StatusType, float>();
	float nextBleedTime;

	IDamageable damageable;
	Monster monster;

	/// <summary>[표시] 상태가 걸렸을 때 (다시 걸어 시간이 새로 시작될 때도). 상태 이상 표시 연출에 쓴다.</summary>
	public event Action<StatusType> StatusApplied;

	/// <summary>[표시] 상태가 풀렸을 때 (시간이 다 됨, Clear, 대상 사망).</summary>
	public event Action<StatusType> StatusRemoved;

	/// <summary>[로직] 출혈 데미지를 줬을 때 (준 데미지).</summary>
	public event Action<int> BleedTicked;

	/// <summary>기절 중이면 true. 몬스터 AI가 이 동안 멈춘다.</summary>
	public bool IsStunned => Has(StatusType.Stun);

	/// <summary>실명 중이면 true. 이 대상의 공격은 빗나간다.</summary>
	public bool IsBlinded => Has(StatusType.Blind);

	/// <summary>표식이 있으면 true. DamageCalculator가 데미지 +100%를 붙인다.</summary>
	public bool IsMarked => Has(StatusType.Mark);

	/// <summary>이동 속도 배율. 이속 감소 중이면 StatusDefaults.SlowMultiplier, 아니면 1. 몬스터 AI가 이동 속도에 곱한다.</summary>
	public float MoveSpeedMultiplier => Has(StatusType.Slow) ? StatusDefaults.SlowMultiplier : 1f;

	void Awake()
	{
		damageable = GetComponent<IDamageable>();
		monster = GetComponent<Monster>();
	}

	void OnEnable()
	{
		if (monster != null) monster.Died += ClearAll;
	}

	void OnDisable()
	{
		if (monster != null) monster.Died -= ClearAll;
	}

	/// <summary>
	/// 상태를 기본 지속 시간(StatusDefaults)으로 건다. 이미 걸려 있으면 지속 시간만 새로 시작한다.
	/// 죽은 대상에게는 걸지 않는다.
	/// </summary>
	public void Apply(StatusType type) => Apply(type, StatusDefaults.DurationOf(type));

	/// <summary>
	/// 상태를 duration초 동안 건다. 이미 걸려 있으면 지속 시간만 새로 시작한다 (남은 시간에 더하지 않음).
	/// </summary>
	public void Apply(StatusType type, float duration)
	{
		if (duration <= 0f || (monster != null && monster.IsDead)) return;

		bool wasBleeding = Has(StatusType.Bleed);
		endTimes[type] = Time.time + duration;
		// 출혈을 새로 걸면 한 간격 뒤에 첫 데미지. 이미 출혈 중이면 데미지 간격은 그대로 둔다
		if (type == StatusType.Bleed && !wasBleeding) nextBleedTime = Time.time + StatusDefaults.BleedInterval;
		StatusApplied?.Invoke(type);
	}

	/// <summary>이 상태가 걸려 있으면 true.</summary>
	public bool Has(StatusType type) => endTimes.TryGetValue(type, out float end) && Time.time < end;

	/// <summary>남은 시간(초). 걸려 있지 않으면 0.</summary>
	public float RemainingTime(StatusType type) => endTimes.TryGetValue(type, out float end) ? Mathf.Max(0f, end - Time.time) : 0f;

	/// <summary>이 상태를 바로 푼다.</summary>
	public void Clear(StatusType type)
	{
		if (endTimes.Remove(type)) StatusRemoved?.Invoke(type);
	}

	/// <summary>모든 상태를 바로 푼다.</summary>
	public void ClearAll()
	{
		foreach (StatusType type in new List<StatusType>(endTimes.Keys)) Clear(type);
	}

	/// <summary>
	/// target(또는 부모)에 붙은 StatusEffects로 상태를 건다. 스킬이 맞은 대상에 쓴다.
	/// StatusEffects가 없는 대상이면 false.
	/// </summary>
	public static bool TryApply(GameObject target, StatusType type)
	{
		if (target == null) return false;
		StatusEffects effects = target.GetComponentInParent<StatusEffects>();
		if (effects == null) return false;
		effects.Apply(type);
		return true;
	}

	void Update()
	{
		// 출혈 데미지는 끝나는 시각 전까지만 준다 (지속 5초, 간격 1초 → 5번)
		if (endTimes.TryGetValue(StatusType.Bleed, out float bleedEnd))
		{
			while (nextBleedTime <= bleedEnd && Time.time >= nextBleedTime)
			{
				nextBleedTime += StatusDefaults.BleedInterval;
				BleedTick();
			}
		}

		// 시간이 다 된 상태를 푼다
		List<StatusType> expired = null;
		foreach (KeyValuePair<StatusType, float> pair in endTimes)
		{
			if (Time.time >= pair.Value) (expired ??= new List<StatusType>()).Add(pair.Key);
		}
		if (expired != null) foreach (StatusType type in expired) Clear(type);
	}

	// 출혈은 스킬 공격이 아니라서 표식 2배와 처형을 받지 않는다 (대상 정보 없이 계산)
	void BleedTick()
	{
		if (damageable == null || (monster != null && monster.IsDead)) return;
		int damage = DamageCalculator.Calculate(DamageRequest.Flat(StatusDefaults.BleedDamage)).Damage;
		damageable.TakeDamage(damage);
		BleedTicked?.Invoke(damage);
	}
}
