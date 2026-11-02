using System.Collections.Generic;
using UnityEngine;

// 찌르기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 찌르기·집중찌르기·꼬챙이는 #152에서 채웠다 (ThrustSkill). 나머지 효과는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 손에 든 검으로 대상을 찌르는 스킬의 기반 (#152). 검 찌르기, 검 집중찌르기, 꼬챙이가 상속한다.
/// <para>
/// - 대상: 파라미터 target (슬롯에서 고른 몬스터, 블록에서 넘긴 대상). 값이 없으면 바라보는 방향 사거리 안의 가장 가까운 몬스터. 그것도 없으면 허공을 찌른다(빗나감).<br/>
/// - 대상이 사거리(Reach, 임시 2.5칸)보다 멀면 대상 앞까지 돌진한 뒤 찌른다 (PlayerMovement.StartLunge).<br/>
/// - 사거리 안에 들어온 틱에 한 번 찌른다. 스킬이 끝날 때까지 못 닿으면(벽에 막힘 등) 빗나간다.<br/>
/// - 데미지: DamageCalculator(공격력 × 스킬 표 배율 + BonusRate) → IDamageable.TakeDamage. 맞은 대상마다 SkillEffectEvents.Hit.<br/>
/// - 판정은 시전자가 속한 장소 씬의 물리 세계에 묻는다 (place-scenes.md).
/// </para>
/// </summary>
public abstract class ThrustSkill : DataDrivenSkill
{
	/// <summary>찌르기 사거리 (유닛, 임시). 몸 중심에서 대상 콜라이더의 가장 가까운 점까지 좌우 거리</summary>
	public const float Reach = 2.5f;

	// 돌진할 때 대상 앞 어디에 멈출지 (사거리 안쪽으로 조금 들어가게)
	const float LungeStopRatio = 0.6f;

	Monster target;
	bool resolved;

	protected override void OnSkillStart(FunctionContext task)
	{
		GameObject caster = task.Caster;
		PlayerMovement movement = caster.GetComponent<PlayerMovement>();
		int facing = movement != null ? movement.Facing : 1;

		target = TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
		if (target == null) target = FindNearestInFront(caster, facing);

		if (target == null)
		{
			// 고를 대상이 없으면 허공을 찌른다
			resolved = true;
			Effects(task)?.RaiseFired(Id, BodyCenter(caster), new Vector2(facing, 0f));
			return;
		}

		if (movement != null) movement.FaceTowards(target.transform.position.x);
		float gap = HorizontalGap(caster, target);
		if (gap > Reach && movement != null)
		{
			int dir = target.transform.position.x >= caster.transform.position.x ? 1 : -1;
			float stopX = ClosestPoint(target, caster.transform.position).x - dir * Reach * LungeStopRatio;
			movement.StartLunge(stopX);
		}
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (resolved) return;
		if (target == null || target.IsDead) { resolved = true; StopLunge(task); return; }

		if (HorizontalGap(task.Caster, target) <= Reach)
		{
			resolved = true;
			StopLunge(task);
			Strike(task);
		}
	}

	// 끝날 때까지 닿지 못했으면 빗나간 것으로 끝낸다
	protected override void OnSkillEnd(FunctionContext task) => StopLunge(task);

	protected override void OnSkillCancel(FunctionContext task) => StopLunge(task);

	/// <summary>
	/// 찌른 순간 맞을 대상들. 기본은 고른 대상 한 명. 꼬챙이는 뒤쪽 일직선 몬스터를 더한다.
	/// </summary>
	protected virtual List<Monster> CollectHits(GameObject caster, Monster primary, int dir) => new List<Monster> { primary };

	/// <summary>
	/// 대상마다 더할 조건 배율 (DamageBonus). 기본 0. 집중찌르기는 출혈이면 +50%.
	/// </summary>
	protected virtual float BonusRate(Monster monster) => 0f;

	void Strike(FunctionContext task)
	{
		GameObject caster = task.Caster;
		int dir = target.transform.position.x >= caster.transform.position.x ? 1 : -1;
		SkillEffectEvents effects = Effects(task);
		effects?.RaiseFired(Id, BodyCenter(caster), new Vector2(dir, 0f));

		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;

		foreach (Monster monster in CollectHits(caster, target, dir))
		{
			if (monster == null || monster.IsDead) continue;
			DamageRequest request = new DamageRequest(attackPower, multiplier) { BonusRate = BonusRate(monster) };
			DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster));
			Vector2 point = ClosestPoint(monster, caster.transform.position);
			monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage);
			effects?.RaiseHit(Id, monster.gameObject, point, result);
		}
	}

	// 찌르기가 나가는 높이: 시전자 몸 콜라이더의 가운데
	static Vector2 BodyCenter(GameObject caster)
	{
		Collider2D col = caster.GetComponent<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)caster.transform.position;
	}

	static void StopLunge(FunctionContext task)
	{
		PlayerMovement movement = task.Caster != null ? task.Caster.GetComponent<PlayerMovement>() : null;
		if (movement != null) movement.StopLunge();
	}

	// 몸 중심에서 대상 콜라이더의 가장 가까운 점까지 좌우 거리
	static float HorizontalGap(GameObject caster, Monster monster) =>
		Mathf.Abs(ClosestPoint(monster, caster.transform.position).x - caster.transform.position.x);

	protected static Vector2 ClosestPoint(Monster monster, Vector2 from)
	{
		Collider2D col = monster.GetComponentInChildren<Collider2D>();
		return col != null ? col.ClosestPoint(from) : (Vector2)monster.transform.position;
	}

	/// <summary>
	/// 시전자가 속한 장소 씬의 물리 세계에서 box 안의 살아 있는 몬스터 (place-scenes.md).
	/// </summary>
	protected static List<Monster> FindMonstersInBox(GameObject caster, Vector2 center, Vector2 size)
	{
		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(LayerMask.GetMask("Monster"));
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		caster.scene.GetPhysicsScene2D().OverlapBox(center, size, 0f, filter, hits);

		List<Monster> found = new List<Monster>();
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster != null && !monster.IsDead && !found.Contains(monster)) found.Add(monster);
		}
		return found;
	}

	// 대상 값이 없을 때: 바라보는 방향 사거리 안의 가장 가까운 몬스터
	static Monster FindNearestInFront(GameObject caster, int facing)
	{
		Vector2 origin = caster.transform.position;
		Vector2 center = origin + new Vector2(facing * Reach * 0.5f, 1f);
		Monster nearest = null;
		float best = float.MaxValue;
		foreach (Monster monster in FindMonstersInBox(caster, center, new Vector2(Reach, 2.5f)))
		{
			float d = HorizontalGap(caster, monster);
			if (d < best) { best = d; nearest = monster; }
		}
		return nearest;
	}
}

/// <summary>검 찌르기. 기본 찌르기 (단일)</summary>
public class CThrust : ThrustSkill
{
	public override string Id => PredefinedFunctionIds.Thrust;
}

/// <summary>검 집중찌르기. 출혈 상태의 적에게 데미지 1.5배 (단일, DamageBonus.BleedFocusedThrust)</summary>
public class CFocusedThrust : ThrustSkill
{
	public override string Id => PredefinedFunctionIds.FocusedThrust;

	protected override float BonusRate(Monster monster)
	{
		StatusEffects status = monster.GetComponent<StatusEffects>();
		return status != null && status.Has(StatusType.Bleed) ? DamageBonus.BleedFocusedThrust : 0f;
	}
}

/// <summary>급소공략. 미니게임 성공 시 3.0배, 눈은 실명·발목은 이속 감소 (단일)</summary>
public class CVitalStrike : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.VitalStrike;
}

/// <summary>
/// 꼬챙이. 여러 명 동시에 찌름, 데미지 약함 (관통)
/// <para>고른 대상부터 찌르는 방향 일직선 뒤 Pierce Length(임시 3칸) 안의 몬스터가 모두 맞는다. 배율은 스킬 표(약함)</para>
/// </summary>
public class CSkewer : ThrustSkill
{
	/// <summary>첫 대상 뒤로 관통하는 길이 (유닛, 임시)</summary>
	public const float PierceLength = 3f;

	// 관통 판정 높이 (유닛). 몸통 높이쯤
	const float PierceHeight = 1.5f;

	public override string Id => PredefinedFunctionIds.Skewer;

	protected override List<Monster> CollectHits(GameObject caster, Monster primary, int dir)
	{
		List<Monster> hits = new List<Monster> { primary };
		Collider2D col = primary.GetComponentInChildren<Collider2D>();
		Vector2 start = col != null ? (Vector2)col.bounds.center : (Vector2)primary.transform.position;
		Vector2 center = start + new Vector2(dir * PierceLength * 0.5f, 0f);
		foreach (Monster monster in FindMonstersInBox(caster, center, new Vector2(PierceLength, PierceHeight)))
			if (!hits.Contains(monster)) hits.Add(monster);
		return hits;
	}
}

/// <summary>소환검 찌르기. 소환검에게 명령해 검이 찌름 (단일)</summary>
public class CSummonedThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonedThrust;
}

/// <summary>상처 공략. 출혈 상태의 적에게 데미지 2.0배 (단일)</summary>
public class CWoundExploit : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.WoundExploit;
}

/// <summary>검 깊이찌르기. 치명타 2.5배 (단일)</summary>
public class CDeepThrust : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.DeepThrust;
}
