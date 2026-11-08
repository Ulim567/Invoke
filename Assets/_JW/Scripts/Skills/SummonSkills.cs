using System.Collections.Generic;
using UnityEngine;

// 소환 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 소환·해제는 #102에서 채웠고, Sword* 반환·파라미터는 #220, 표식 남기기·검무는 #243, 패링은 #245에서 더했다. 나머지 효과(판정, 데미지, 상태 이상)는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 검 소환 (malloc). 대상 없음, 누르면 바로 발동 (#102, #220에서 Sword* 반환)
/// <para>
/// - 시작할 때 소환검을 만들고 heap에 올린다(Malloc). 크기는 PredefinedStructs의 Sword 크기(임시 2칸, malloc(Sword) 블록과 같음).<br/>
/// - 반환: 만든 소환검의 포인터 Sword*. 블록코딩에서 Sword* s = SummonSword(); 로 받아 다른 스킬에 넘기거나 free(s)로 해제한다. 만들지 못하면 NULL.<br/>
/// - 포인터는 이 작업의 것이고, 정상으로 끝나면 숨은 시스템 전역으로 옮겨진다. 받은 변수를 잃어도 누수가 아니다 (predefined-function.md).<br/>
/// - 도중에 실행 중단·실패로 끝나면 그 소환검은 누수된다 (heap 누수 규칙, SummonedSwords가 ObjectLeaked를 받아 멈춤).<br/>
/// - 소환검이 이미 5개(누수 포함)거나 heap이 모자라면 만들지 않는다. C의 malloc처럼 에러로 끝내지 않는다.
/// </para>
/// </summary>
public class CSummonSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonSword;

	public override VariableType ReturnType => SwordPointerType();

	/// <summary>소환검 하나의 heap 크기. malloc(Sword) 블록과 같은 값 (PredefinedStructs)</summary>
	public static int SwordHeapSize => PredefinedStructs.TryGet(PredefinedStructs.Sword, out StructDefinition def) ? def.HeapSize : 1;

	protected override void OnSkillStart(FunctionContext task)
	{
		// 일찍 끝나도 반환 값이 있게 NULL을 먼저 정하고, 만들면 실제 포인터로 덮어쓴다 (실행기 규칙: 반환 타입이 있으면 Return 필수)
		Return(RuntimeValue.FromPointer(SwordPointerType(), HeapStorage.NullPointer));

		SummonedSwords swords = task.Caster.GetComponent<SummonedSwords>();
		if (swords == null)
		{
			Debug.LogError($"{Name}: {task.Caster.name}에 SummonedSwords가 없습니다.");
			return;
		}
		// 이미 5개(누수 포함)면 만들지 않는다
		if (CountHeapObjects<SummonedSword>(task) >= SummonedSwords.MaxCount) return;

		SummonedSword sword = swords.SpawnSword();
		// 만들지 못했으면(프리팹 설정 오류 등) 소환하지 않는다. 원인은 NetworkSpawner가 로그로 남긴다
		if (sword == null) return;
		int pointer = Malloc(task, sword, SwordHeapSize);
		// heap 부족: 만든 오브젝트를 없애고 끝낸다 (에러 아님)
		if (pointer == HeapStorage.NullPointer)
		{
			swords.DespawnSword(sword);
			return;
		}
		swords.Register(sword, pointer);
		Return(RuntimeValue.FromPointer(SwordPointerType(), pointer));
	}
}

/// <summary>
/// 해제 (free). 대상 없음, 누르면 바로 발동 (#102, #220에서 Sword* 파라미터)
/// <para>
/// - sword 파라미터로 포인터를 넘기면 그 소환검을 해제한다 (free(s)와 같음). 플레이어가 malloc(Sword)로 만든 검도 된다.<br/>
/// - 안 넘기면 마지막에 소환한 소환검부터 해제한다 (숨은 시스템 전역 목록의 마지막). 플레이어가 malloc으로 만든 검은 이 목록에 없다.<br/>
/// - 날아가는 중이거나 떨어진 소환검도 거리와 상관없이 해제한다. 누수된 소환검, 아직 진행 중인 검 소환의 소환검은 해제하지 않는다.<br/>
/// - 해제할 소환검이 없으면 NoSummonedSwordError. 해제하면 heap 칸이 돌아오고, 소환검 오브젝트는 SummonedSwords가 ObjectFreed를 받아 없앤다.
/// </para>
/// </summary>
public class CFreeSword : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.FreeSword;

	protected override bool TakesSwordPointer => true;

	// 포인터를 넘겼으면 그 포인터가 맞는지는 Free가 본다. 안 넘겼으면 해제할 수 있는 소환검 수(숨은 전역 목록)를 센다
	protected override int CountUsableSummonedSwords(FunctionContext task) =>
		TryGetArgument("sword", out _) ? int.MaxValue : GetGlobalPointers<SummonedSword>(task).Count;

	protected override void OnSkillStart(FunctionContext task)
	{
		if (TryGetArgument("sword", out RuntimeValue value))
		{
			int given = value.AsPointer();
			// 소환검이 아닌 포인터면 InvalidPointerError로 끝난다
			if (!TryGetHeapObject(task, given, out SummonedSword _)) return;
			Free(task, given);
			return;
		}

		List<int> pointers = GetGlobalPointers<SummonedSword>(task);
		if (pointers.Count == 0)
		{
			Fail(new NoSummonedSwordError());
			return;
		}
		int pointer = pointers[pointers.Count - 1];
		if (!TryGetHeapObject(task, pointer, out SummonedSword _)) return;
		// 오브젝트 삭제는 SummonedSwords가 ObjectFreed를 받아서 한다 (여기서 지우면 두 번 지움, #191)
		Free(task, pointer);
	}
}

/// <summary>
/// 패링 (#245). 누른 순간부터 WindowSeconds(임시 0.3초) 동안 패링 가능한 몬스터 투사체를 잡고, 잡으면 날아온 쪽으로 검기를 한 번 날려 반격한다 (대상 없음, 맨손 불가).
/// <para>
/// - 판정은 플레이어의 ParryGuard가 한다. 몬스터 투사체(MonsterProjectile, 김도훈)가 닿을 때 ParryGuard.TryParry로 물어보고, 잡히면 데미지 없이 사라진다.<br/>
/// - 잡을 수 있는 것: MonsterAttackData.Parryable이 켜진 투사체만. 몸통 박치기·낙하 공격은 못 잡는다.<br/>
/// - 반격: 잡은 다음 틱에 몸 가운데에서 투사체가 날아온 쪽으로 검기(SwordWave)를 날린다. 처음 닿은 몬스터에게 공격력 × 표 배율. 검기는 heap(표의 heapSize, 1칸)을 쓰고 끝나면 돌려준다.<br/>
/// - 한 번 잡으면 판정이 닫힌다 (1회). 판정 시간 안에 못 잡으면 아무 일도 없이 끝난다.
/// </para>
/// </summary>
public class CParry : SwordWaveSkill
{
	/// <summary>패링 판정 시간 (초, 임시)</summary>
	public const float WindowSeconds = 0.3f;

	public override string Id => PredefinedFunctionIds.Parry;

	// 반격 검기: 검 휘두르기와 같은 크기, 쏜 몬스터까지 닿게 사거리를 길게 (임시)
	protected override bool Wide => false;
	protected override float Speed => 30f;
	protected override float Range => 14f;
	protected override Vector2 HitSize => new Vector2(1f, 2f);
	protected override bool Pierce => false;

	ParryGuard guard;
	bool counterPending;
	Vector2 counterDirection;

	// 시작할 때 검기를 날리지 않고 판정 시간만 연다
	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		counterPending = false;
		guard = caster.GetComponent<ParryGuard>();
		if (guard == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 ParryGuard가 없습니다.");
			return;
		}
		guard.Parried += OnParried;
		guard.Open(WindowSeconds);
	}

	// 투사체 충돌(물리 단계)에서 불리므로 바로 날리지 않고 다음 틱에 날린다 (heap은 틱 안에서 다룬다)
	void OnParried(Vector2 point, Vector2 back)
	{
		Unsubscribe();
		counterPending = true;
		counterDirection = back;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (counterPending)
		{
			counterPending = false;
			LaunchWave(task, BodyCenter(task.Caster), counterDirection);
		}
		base.OnTick(task, tick);
	}

	protected override void OnSkillEnd(FunctionContext task)
	{
		Unsubscribe();
		guard?.Close();
		base.OnSkillEnd(task);
	}

	protected override void OnSkillCancel(FunctionContext task)
	{
		Unsubscribe();
		guard?.Close();
		base.OnSkillCancel(task);
	}

	void Unsubscribe()
	{
		if (guard != null) guard.Parried -= OnParried;
	}
}

/// <summary>
/// 표식 남기기 (#243). 소환검 2개가 대상 왼쪽·오른쪽에서 찔러 약한 데미지를 주고, 둘 다 찌르면 표식 + 기절을 건다 (단일, 소환검 2개 필요).
/// <para>
/// - 사용 가능한 소환검이 2개 미만이면 시작 전에 NoSummonedSwordError로 끝난다.<br/>
/// - 가장 먼저 소환한 사용 가능한 검 2개를 쓴다. 하나는 왼쪽, 하나는 오른쪽 옆으로 간 뒤 안쪽으로 찌르고 돌아온다 (SummonedSwords.Stab의 side).<br/>
/// - 데미지: 검마다 닿은 순간 공격력 × 표 배율 (약하게, 임시 0.5).<br/>
/// - 두 검이 모두 닿으면 표식(StatusType.Mark, 기본 5초)과 기절(Stun, 기본 1.0초)을 건다. 표식은 그 다음 공격부터 데미지 2배 (이번 찌르기에는 안 붙음).<br/>
/// - 스킬(20틱)이 끝날 때까지 둘 다 닿지 못하면(대상이 죽는 등) 표식·기절은 걸지 않는다. 대상 값이 없으면 아무것도 안 한다.
/// </para>
/// </summary>
public class CMark : DataDrivenSkill
{
	/// <summary>필요한 사용 가능한 소환검 수</summary>
	public const int RequiredSwords = 2;

	public override string Id => PredefinedFunctionIds.Mark;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	HashSet<SummonedSword> sent;
	SummonedSwords swords;
	Monster target;
	GameObject caster;
	int hits;

	// 2개 미만이면 0으로 알려 NoSummonedSwordError로 끝나게 한다
	protected override int CountUsableSummonedSwords(FunctionContext task)
	{
		int usable = base.CountUsableSummonedSwords(task);
		return usable >= RequiredSwords ? usable : 0;
	}

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		target = TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
		sent = new HashSet<SummonedSword>();
		hits = 0;
		if (swords == null || target == null) return;

		List<SummonedSword> picked = new List<SummonedSword>();
		foreach (SummonedSword s in swords.Swords)
		{
			if (s != null && s.State == SummonedSwordState.Ready) picked.Add(s);
			if (picked.Count == RequiredSwords) break;
		}
		if (picked.Count < RequiredSwords) return;

		swords.StabHit += OnStabHit;
		int side = -1;
		foreach (SummonedSword s in picked)
		{
			Vector2 from = s.transform.position;
			if (swords.Stab(s, target.gameObject, side))
			{
				sent.Add(s);
				Effects(task)?.RaiseFired(Id, from, (Vector2)target.transform.position - from);
			}
			side = -side;
		}
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (swords != null) swords.StabHit -= OnStabHit;
	}

	void OnStabHit(SummonedSword sword, GameObject hit)
	{
		if (sent == null || !sent.Contains(sword) || hit == null) return;
		Monster monster = hit.GetComponentInParent<Monster>();
		if (monster == null || monster.IsDead) return;
		hits++;
		SummonDamage.Deal(Id, Data, caster, monster, sword.transform.position, 0f);
		// 두 검이 모두 찔렀을 때 표식과 기절
		if (hits < sent.Count) return;
		Unsubscribe();
		StatusEffects.TryApply(monster.gameObject, StatusType.Mark);
		StatusEffects.TryApply(monster.gameObject, StatusType.Stun);
	}
}

/// <summary>
/// 검무 (#243). 사용 가능한 소환검이 모두 대상 둘레를 돌며 일정 간격으로 벤다. 데미지는 낮지만 맞을 때마다 출혈 (단일, 소환검 필요).
/// <para>
/// - 검은 대상 둘레에 고르게 나뉘어 돈다 (SummonedSwords.Dance). 도는 동안은 Dancing 상태라 다른 스킬에 쓰이지 않는다.<br/>
/// - HitIntervalTicks(임시 6틱)마다 대상 가까이 있는 검마다 한 번씩 공격력 × 표 배율(0.5) 데미지, 맞으면 출혈(StatusEffects.TryApply).<br/>
/// - 스킬(20틱)이 끝나거나 끊기면 검은 곁으로 돌아온다. 대상이 죽으면 더 베지 않는다. 대상 값이 없으면 아무것도 안 한다.
/// </para>
/// </summary>
public class CSwordDance : DataDrivenSkill
{
	/// <summary>베는 간격 (틱, 임시)</summary>
	public const int HitIntervalTicks = 6;

	// 대상 가운데에서 이 거리 안의 검만 벤다 (아직 날아가는 중인 검은 안 벰)
	const float HitReach = 2.5f;

	public override string Id => PredefinedFunctionIds.SwordDance;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	List<SummonedSword> dancing;
	SummonedSwords swords;
	Monster target;
	GameObject caster;
	int startTick = -1;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		target = TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
		dancing = new List<SummonedSword>();
		startTick = -1;
		if (swords == null || target == null) return;

		List<SummonedSword> ready = new List<SummonedSword>();
		foreach (SummonedSword s in swords.Swords)
		{
			if (s != null && s.State == SummonedSwordState.Ready) ready.Add(s);
		}
		for (int i = 0; i < ready.Count; i++)
		{
			if (swords.Dance(ready[i], target.gameObject, 360f * i / ready.Count)) dancing.Add(ready[i]);
		}
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (target == null || dancing == null || dancing.Count == 0) return;
		if (startTick < 0) startTick = tick;
		if ((tick - startTick) % HitIntervalTicks != HitIntervalTicks - 1 || target.IsDead) return;

		Vector2 center = BodyCenter(target.gameObject);
		bool fired = false;
		foreach (SummonedSword s in dancing)
		{
			if (s == null || s.State != SummonedSwordState.Dancing) continue;
			if (Vector2.Distance(s.transform.position, center) > HitReach) continue;
			if (!fired)
			{
				fired = true;
				Effects(task)?.RaiseFired(Id, center, Vector2.zero);
			}
			SummonDamage.Deal(Id, Data, caster, target, s.transform.position, 0f);
			StatusEffects.TryApply(target.gameObject, StatusType.Bleed, caster);
			if (target.IsDead) break;
		}
	}

	protected override void OnSkillEnd(FunctionContext task) => EndDance();
	protected override void OnSkillCancel(FunctionContext task) => EndDance();

	void EndDance()
	{
		if (swords == null || dancing == null) return;
		foreach (SummonedSword s in dancing) swords.EndDance(s);
		dancing.Clear();
	}

	static Vector2 BodyCenter(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}
}

/// <summary>소환 계열 스킬이 함께 쓰는 데미지 주기 (#243). 공격력 × 표 배율 → TakeDamage → SkillEffectEvents.Hit</summary>
static class SummonDamage
{
	public static void Deal(string skillId, SkillData data, GameObject caster, Monster monster, Vector2 point, float bonusRate)
	{
		if (caster == null || monster == null || monster.IsDead) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = data != null ? data.DamageMultiplier : 1f;
		DamageRequest request = new DamageRequest(attackPower, multiplier) { BonusRate = bonusRate };
		DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage, caster);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(skillId, monster.gameObject, point, result);
	}
}
