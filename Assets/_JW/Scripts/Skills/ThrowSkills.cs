using UnityEngine;

// 던지기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 효과는 #167에서 채웠다. 검 움직임은 PhysicalSword, SummonedSwords가 맡고, 스킬은 명중 판정과 데미지만 한다.

/// <summary>던지기 스킬의 공통: 맞은 몬스터에게 데미지를 준다 (#167)</summary>
public abstract class ThrowSkillBase : DataDrivenSkill
{
	protected GameObject caster;

	/// <summary>처형할 수 있는 공격인지 (검 던지기만 true)</summary>
	protected virtual bool CanExecute => false;

	/// <summary>맞은 몬스터에게 공격력 × 표 배율 데미지를 주고 Hit 연출 event를 낸다.</summary>
	protected void DealDamage(GameObject target, Vector2 point)
	{
		Monster monster = target != null ? target.GetComponentInParent<Monster>() : null;
		if (caster == null || monster == null || monster.IsDead) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;
		DamageRequest request = new DamageRequest(attackPower, multiplier) { CanExecute = CanExecute };
		DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(Id, monster.gameObject, point, result);
	}

	protected static Vector2 Center(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}

	/// <summary>대상 파라미터. 없으면 null</summary>
	protected Monster Target() => TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
}

/// <summary>
/// 검 던지기 (#167). 손에 든 검을 포물선으로 던진다 (단일).
/// <para>
/// - 대상이 HitRange(임시 5칸, 몸 가운데끼리 거리) 안이면 명중, 밖이거나 대상이 없으면 빗나가 그 자리에 떨어진다.<br/>
/// - 맞으면 공격력 × 표 배율. 처형할 수 있는 공격이라 체력 2% 이하인 일반 몬스터는 처형된다 (damage-calculation.md).<br/>
/// - 검은 맞은 자리에 떨어지고, 줍거나 검 돌아오기로 회수할 때까지 맨손이다.
/// </para>
/// </summary>
public class CSwordThrow : ThrowSkillBase
{
	/// <summary>명중 사거리 (유닛, 임시)</summary>
	public const float HitRange = 5f;

	public override string Id => PredefinedFunctionIds.SwordThrow;

	protected override bool CanExecute => true;

	PhysicalSword sword;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		sword = caster.GetComponent<PhysicalSword>();
		if (sword == null) return;

		Monster target = Target();
		bool hit = target != null && Vector2.Distance(Center(caster), Center(target.gameObject)) <= HitRange;
		PlayerMovement movement = caster.GetComponent<PlayerMovement>();
		if (target != null && movement != null) movement.FaceTowards(target.transform.position.x);

		if (hit) sword.Hit += OnHit;
		if (!sword.Throw(hit ? target.gameObject : null)) { Unsubscribe(); return; }
		Vector2 dir = target != null ? Center(target.gameObject) - sword.HandPosition : Vector2.right * (movement != null ? movement.Facing : 1);
		Effects(task)?.RaiseFired(Id, sword.HandPosition, dir);
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (sword != null) sword.Hit -= OnHit;
	}

	void OnHit(GameObject target)
	{
		Unsubscribe();
		DealDamage(target, Center(target));
	}
}

/// <summary>
/// 소환검 던지기 (#167). 가장 먼저 소환한 사용 가능한 소환검을 대상 쪽으로 일직선으로 던진다 (단일, 소환검 필요).
/// <para>
/// - 대상이 HitRange(임시 8칸) 안이면 명중, 밖이면 빗나가 계속 날아가 벽·땅에 꽂힌다.<br/>
/// - 맞으면 공격력 × 표 배율 (처형 없음). 소환검은 몬스터 몸에 꽂혀 따라다닌다.<br/>
/// - sword(Sword*)를 넘기면 그 소환검을 던진다 (#220). 그 검이 곁에 없으면 아무것도 안 하고 끝난다. 잘못된 포인터면 InvalidPointerError.
/// </para>
/// </summary>
public class CSummonedThrow : ThrowSkillBase
{
	/// <summary>명중 사거리 (유닛, 임시). 검 던지기보다 길다</summary>
	public const float HitRange = 8f;

	public override string Id => PredefinedFunctionIds.SummonedThrow;

	protected override bool TakesSwordPointer => true;

	SummonedSwords swords;
	SummonedSword thrown;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		Monster target = Target();
		// sword로 넘긴 검이 있으면 그 검, 없으면 가장 먼저 소환한 검 (#220). 소환검이 없으면 DataDrivenSkill이 시작 전에 NoSummonedSwordError로 막는다
		thrown = null;
		if (swords == null || target == null || !TryPickSword(task, swords, out thrown)) return;

		bool hit = Vector2.Distance(Center(caster), Center(target.gameObject)) <= HitRange;
		Vector2 from = thrown.transform.position;
		Vector2 dir = Center(target.gameObject) - from;
		if (hit) swords.Hit += OnHit;
		if (!swords.Throw(thrown, hit ? target.gameObject : null, dir)) { Unsubscribe(); return; }
		Effects(task)?.RaiseFired(Id, from, dir);
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (swords != null) swords.Hit -= OnHit;
	}

	void OnHit(SummonedSword sword, GameObject target)
	{
		if (sword != thrown) return;
		Unsubscribe();
		DealDamage(target, sword.transform.position);
	}
}

/// <summary>
/// 검 돌아오기 (#167, #232). 던진 소환검 중 가장 먼저 던진 것 하나를 날려서 돌려받는다 (경로, 맨손 가능).
/// <para>
/// - 물리 검은 돌려받지 않는다. 물리 검은 Z로만 줍는다 (#232).<br/>
/// - 순서: SummonedSword.ThrowOrder (SwordThrowOrder 번호표)가 가장 작은 것.<br/>
/// - 돌아오는 길에 지나가는 몬스터를 한 번씩 공격력 × 표 배율로 벤다 (ReturnHit).<br/>
/// - 돌려받을 소환검이 없으면 아무 일도 없이 끝난다. 돌아오는 중인 검, 누수된 소환검은 고르지 않는다.<br/>
/// - 스킬(20틱)이 끝난 뒤에도 검은 계속 날아오지만, 그 뒤에 지나간 몬스터에게는 데미지를 주지 않는다.
/// </para>
/// </summary>
public class CSwordReturn : ThrowSkillBase
{
	public override string Id => PredefinedFunctionIds.SwordReturn;

	SummonedSwords swords;
	SummonedSword returning;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		returning = null;
		if (swords == null) return;

		// 가장 먼저 던진 소환검 찾기
		int best = int.MaxValue;
		foreach (SummonedSword s in swords.Swords)
		{
			if (s == null || (s.State != SummonedSwordState.Flying && s.State != SummonedSwordState.Dropped) || s.ThrowOrder >= best) continue;
			best = s.ThrowOrder;
			returning = s;
		}
		if (returning == null) return;

		Vector2 from = returning.transform.position;
		swords.ReturnHit += OnSummonedReturnHit;
		if (!swords.StartReturn(returning)) { Unsubscribe(); return; }
		Effects(task)?.RaiseFired(Id, from, (Vector2)caster.transform.position - from);
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (swords != null) swords.ReturnHit -= OnSummonedReturnHit;
	}

	void OnSummonedReturnHit(SummonedSword sword, GameObject monster)
	{
		if (sword == returning) DealDamage(monster, sword.transform.position);
	}
}
