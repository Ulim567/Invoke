using System.Collections.Generic;
using UnityEngine;

// 휘두르기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 휘두르기·검 크게베기는 #163, 소환검 휘두르기·상처 가르기·검 다중베기는 #172, 전장 휩쓸기는 #220(대상 array, #191)에서 채웠다. 나머지 효과는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 검기를 날리는 스킬의 기반 (#163). 검 휘두르기, 검 크게베기가 상속한다.
/// <para>
/// - 시작할 때 몸 가운데에서 검기를 만들고(SwordWaveLauncher.SpawnWave) heap에 올린다(Malloc, 표의 heap 크기). heap이 모자라면 검기를 없애고 아무것도 날리지 않는다.<br/>
/// - 검기를 여러 개 날리는 스킬(소환검 휘두르기)은 LaunchWave를 여러 번 부른다. 검기마다 heap을 따로 잡고 따로 돌려준다 (#172).<br/>
/// - 검기가 끝나면(사거리 끝, 첫 대상에서 멈춤) 그 틱에 검기를 없애고 heap을 돌려준다(Free). 스킬이 끝날 때 아직 날고 있어도 같다.<br/>
/// - 끊기면(실행 중단, 취소) 검기는 없애지만 heap은 누수된다 (heap 누수 규칙, OnCancel에서는 Free할 수 없음).<br/>
/// - 데미지: 검기가 알린 몬스터마다 DamageCalculator(공격력 × 표 배율) → IDamageable.TakeDamage → OnHitMonster → SkillEffectEvents.Hit.
/// </para>
/// </summary>
public abstract class SwordWaveSkill : DataDrivenSkill
{
	// 날린 검기와 그 heap 포인터. 소환검 휘두르기는 여러 개 (#172)
	class Flying
	{
		public SwordWave Wave;
		public int Pointer;
		public System.Action<Monster> OnHit;
	}
	// 실행기는 스킬을 MemberwiseClone으로 복사하므로, 리스트는 복사본이 처음 쓸 때 새로 만든다 (원본과 공유하지 않게)
	List<Flying> flying;
	protected GameObject caster;

	/// <summary>큰 검기(SwordWave_Wide)를 쓰는지</summary>
	protected abstract bool Wide { get; }
	/// <summary>초당 유닛 (임시)</summary>
	protected abstract float Speed { get; }
	/// <summary>날아가는 거리 (유닛, 임시)</summary>
	protected abstract float Range { get; }
	/// <summary>판정 상자 (가로 = 날아가는 방향, 세로, 유닛, 임시)</summary>
	protected abstract Vector2 HitSize { get; }
	/// <summary>true면 관통, false면 첫 대상에서 멈춤</summary>
	protected abstract bool Pierce { get; }

	/// <summary>날아갈 방향. 기본은 바라보는 방향</summary>
	protected virtual Vector2 Direction(FunctionContext task, Vector2 origin)
	{
		PlayerMovement movement = task.Caster.GetComponent<PlayerMovement>();
		return new Vector2(movement != null ? movement.Facing : 1, 0f);
	}

	/// <summary>대상마다 데미지를 준 뒤 불린다. 기본은 아무것도 안 함 (크게베기는 출혈, 상처 가르기는 출혈 도트 배율).</summary>
	protected virtual void OnHitMonster(Monster monster, DamageResult result) { }

	/// <summary>기본: 몸 가운데에서 검기 하나를 날린다.</summary>
	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		Vector2 origin = BodyCenter(caster);
		LaunchWave(task, origin, Direction(task, origin));
	}

	/// <summary>
	/// origin에서 dir 방향으로 검기 하나를 만들어 heap에 올리고 날린다. heap이 모자라거나 만들지 못하면 false (에러 아님).
	/// </summary>
	protected bool LaunchWave(FunctionContext task, Vector2 origin, Vector2 dir)
	{
		caster = task.Caster;
		SwordWaveLauncher launcher = caster.GetComponent<SwordWaveLauncher>();
		if (launcher == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 SwordWaveLauncher가 없습니다.");
			return false;
		}
		dir = dir.sqrMagnitude < 0.0001f ? Vector2.right : dir.normalized;

		SwordWave wave = launcher.SpawnWave(Wide, origin);
		if (wave == null) return false;
		int pointer = Malloc(task, wave, Data != null ? Data.HeapSize : 1);
		if (pointer == HeapStorage.NullPointer)
		{
			// heap 부족: 검기를 날리지 않는다 (에러 아님)
			Debug.Log($"{Name}: heap이 모자라 검기를 날리지 못함");
			launcher.DespawnWave(wave);
			return false;
		}

		flying ??= new List<Flying>();
		Flying f = new Flying { Wave = wave, Pointer = pointer };
		f.OnHit = monster => OnWaveHit(f.Wave, monster);
		flying.Add(f);
		Effects(task)?.RaiseFired(Id, origin, dir);
		wave.MonsterHit += f.OnHit;
		wave.Launch(dir, Speed, Range, HitSize, Pierce);
		return true;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (flying == null) return;
		for (int i = flying.Count - 1; i >= 0; i--)
		{
			if (flying[i].Wave == null || flying[i].Wave.IsFinished) Release(task, i);
		}
	}

	protected override void OnSkillEnd(FunctionContext task)
	{
		if (flying == null) return;
		for (int i = flying.Count - 1; i >= 0; i--) Release(task, i);
	}

	// 끊기면 검기는 없애고 heap은 누수로 남긴다
	protected override void OnSkillCancel(FunctionContext task)
	{
		if (flying == null) return;
		SwordWaveLauncher launcher = task.Caster.GetComponent<SwordWaveLauncher>();
		foreach (Flying f in flying)
		{
			if (f.Wave == null) continue;
			f.Wave.MonsterHit -= f.OnHit;
			launcher?.DespawnWave(f.Wave);
		}
		flying.Clear();
	}

	void Release(FunctionContext task, int index)
	{
		Flying f = flying[index];
		flying.RemoveAt(index);
		if (f.Wave != null)
		{
			f.Wave.MonsterHit -= f.OnHit;
			task.Caster.GetComponent<SwordWaveLauncher>()?.DespawnWave(f.Wave);
		}
		if (f.Pointer != HeapStorage.NullPointer) Free(task, f.Pointer);
	}

	void OnWaveHit(SwordWave wave, Monster monster)
	{
		if (caster == null || monster == null) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;
		DamageResult result = DamageCalculator.Calculate(new DamageRequest(attackPower, multiplier), DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage, caster);
		OnHitMonster(monster, result);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(Id, monster.gameObject, wave != null ? (Vector2)wave.transform.position : (Vector2)monster.transform.position, result);
	}

	protected static Vector2 BodyCenter(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}
}

/// <summary>
/// 검 휘두르기 (#163). 고른 대상 쪽으로 검기를 날려 처음 닿은 몬스터 한 명을 벤다 (단일).
/// <para>대상 값이 없으면 바라보는 방향으로 날린다. 검기 1×2, 사거리 8, 속도 25 (임시)</para>
/// </summary>
public class CSwordSlash : SwordWaveSkill
{
	public override string Id => PredefinedFunctionIds.SwordSlash;

	protected override bool Wide => false;
	protected override float Speed => 25f;
	protected override float Range => 8f;
	protected override Vector2 HitSize => new Vector2(1f, 2f);
	protected override bool Pierce => false;

	protected override Vector2 Direction(FunctionContext task, Vector2 origin)
	{
		if (!TryGetArgument("target", out RuntimeValue value) || value.AsBug() == null) return base.Direction(task, origin);
		Monster target = value.AsBug();
		PlayerMovement movement = task.Caster.GetComponent<PlayerMovement>();
		if (movement != null) movement.FaceTowards(target.transform.position.x);
		return BodyCenter(target.gameObject) - origin;
	}
}

/// <summary>
/// 검 크게베기 (#163). 바라보는 방향으로 큰 검기를 날려, 판정 상자에 걸린 몬스터를 모두 베고 출혈을 건다 (범위, 관통).
/// <para>검기 1×4, 사거리 6, 속도 25 (임시). 배율은 표(0.5, 임시)</para>
/// </summary>
public class CWideSlash : SwordWaveSkill
{
	/// <summary>검기가 날아가는 거리 (유닛, 임시). 테스트도 읽는다</summary>
	public const float WaveRange = 6f;

	public override string Id => PredefinedFunctionIds.WideSlash;

	protected override bool Wide => true;
	protected override float Speed => 25f;
	protected override float Range => WaveRange;
	protected override Vector2 HitSize => new Vector2(1f, 4f);
	protected override bool Pierce => true;

	protected override void OnHitMonster(Monster monster, DamageResult result) =>
		StatusEffects.TryApply(monster.gameObject, StatusType.Bleed, caster);
}

/// <summary>
/// 소환검 휘두르기 (#172). 사용 가능한 소환검이 모두 제자리에서 각자 검기를 대상 쪽으로 날린다 (단일, 소환검 필요).
/// <para>검기는 검 휘두르기와 같다 (1×2, 사거리 8, 첫 대상에서 멈춤). 검기 하나당 heap 1칸, 사라지면 자동 해제. heap이 모자라면 거기까지만 날린다. 대상 값이 없으면 바라보는 방향</para>
/// </summary>
public class CSummonedSlash : SwordWaveSkill
{
	public override string Id => PredefinedFunctionIds.SummonedSlash;

	protected override bool Wide => false;
	protected override float Speed => 25f;
	protected override float Range => 8f;
	protected override Vector2 HitSize => new Vector2(1f, 2f);
	protected override bool Pierce => false;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		SummonedSwords swords = caster.GetComponent<SummonedSwords>();
		if (swords == null) return;
		Monster target = TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
		PlayerMovement movement = caster.GetComponent<PlayerMovement>();
		if (target != null && movement != null) movement.FaceTowards(target.transform.position.x);

		foreach (SummonedSword sword in new List<SummonedSword>(swords.Swords))
		{
			if (sword == null || sword.State != SummonedSwordState.Ready) continue;
			Vector2 from = sword.transform.position;
			Vector2 dir = target != null ? BodyCenter(target.gameObject) - from : new Vector2(movement != null ? movement.Facing : 1, 0f);
			if (!LaunchWave(task, from, dir)) break; // heap 부족이면 더 날리지 않는다
		}
	}
}

/// <summary>
/// 상처 가르기 (#172). 검 휘두르기처럼 검기를 날린다 (단일). 맞은 적이 출혈 중이면 그 출혈이 끝날 때까지 출혈 틱 데미지가 2.0배 (DamageBonus.BleedWoundSlash).
/// <para>검기 데미지 자체는 기본. 출혈이 아닌 적에게는 아무 효과 없음</para>
/// </summary>
public class CWoundSlash : CSwordSlash
{
	public override string Id => PredefinedFunctionIds.WoundSlash;

	protected override void OnHitMonster(Monster monster, DamageResult result)
	{
		StatusEffects status = monster.GetComponent<StatusEffects>();
		if (status != null && status.Has(StatusType.Bleed)) status.AmplifyBleed(DamageBonus.BleedWoundSlash);
	}
}

/// <summary>
/// 검 다중베기 (#172). 사용 가능한 소환검이 차례로(StaggerTicks 간격) 대상에게 날아가 한 번씩 베고 돌아온다 (단일 + 다단 히트, 소환검 필요).
/// <para>
/// - 움직임은 소환검 찌르기와 같다 (SummonedSwords.Stab). 검마다 닿은 순간 공격력 × 표 배율.<br/>
/// - 스킬(20틱)이 끝날 때까지 닿지 못한 검은 데미지 없이 돌아온다. 대상 값이 없으면 아무것도 안 한다.
/// </para>
/// </summary>
public class CMultiSlash : DataDrivenSkill
{
	/// <summary>검을 하나씩 보내는 간격 (틱, 임시)</summary>
	public const int StaggerTicks = 2;

	public override string Id => PredefinedFunctionIds.MultiSlash;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	List<SummonedSword> queue;
	HashSet<SummonedSword> sent;
	SummonedSwords swords;
	Monster target;
	GameObject caster;
	int startTick = -1;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		target = TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;
		queue = new List<SummonedSword>();
		sent = new HashSet<SummonedSword>();
		startTick = -1;
		if (swords == null || target == null) return;
		foreach (SummonedSword s in swords.Swords)
		{
			if (s != null && s.State == SummonedSwordState.Ready) queue.Add(s);
		}
		swords.StabHit += OnStabHit;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (swords == null || target == null || queue == null || queue.Count == 0) return;
		if (startTick < 0) startTick = tick;
		if ((tick - startTick) % StaggerTicks != 0) return;

		SummonedSword next = queue[0];
		queue.RemoveAt(0);
		if (next == null || target.IsDead) return;
		Vector2 from = next.transform.position;
		if (swords.Stab(next, target.gameObject))
		{
			sent.Add(next);
			Effects(task)?.RaiseFired(Id, from, (Vector2)target.transform.position - from);
		}
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (swords != null) swords.StabHit -= OnStabHit;
		queue?.Clear();
	}

	void OnStabHit(SummonedSword sword, GameObject hit)
	{
		if (sent == null || !sent.Remove(sword) || hit == null) return;
		Monster monster = hit.GetComponentInParent<Monster>();
		if (monster == null || monster.IsDead) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;
		DamageResult result = DamageCalculator.Calculate(new DamageRequest(attackPower, multiplier), DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage, caster);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(Id, monster.gameObject, sword.transform.position, result);
	}
}

/// <summary>
/// 전장 휩쓸기 (#220). 여러 명을 골라 사용 가능한 소환검이 대상마다 하나씩 차례로(StaggerTicks 간격) 날아가 베고 돌아온다 (여러 명 선택, 소환검 필요).
/// <para>
/// - 대상은 targets(Target 배열, RuntimeValue.FromBugs). 고른 순서대로 가장 먼저 소환한 검부터 하나씩 맡는다.<br/>
/// - 대상이 소환검보다 많으면 남는 대상은 베지 않는다. 소환검이 더 많으면 남는 검은 곁에 그대로 있다.<br/>
/// - 움직임은 검 다중베기와 같다 (SummonedSwords.Stab). 검마다 닿은 순간 공격력 × 표 배율.<br/>
/// - 보내기 전이나 닿았을 때 죽은 대상은 건너뛴다. 배열에 같은 몬스터가 두 번 있으면 한 번만 벤다.<br/>
/// - 배열 값 검사(InvalidTarget)는 실행기가 시작할 때 한다. 스킬(20틱)이 끝날 때까지 닿지 못한 검은 데미지 없이 돌아온다.
/// </para>
/// </summary>
public class CBattlefieldSweep : DataDrivenSkill
{
	/// <summary>검을 하나씩 보내는 간격 (틱, 임시). 검 다중베기와 같다</summary>
	public const int StaggerTicks = CMultiSlash.StaggerTicks;

	public override string Id => PredefinedFunctionIds.BattlefieldSweep;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	List<(SummonedSword sword, Monster target)> queue;
	Dictionary<SummonedSword, Monster> sent;
	SummonedSwords swords;
	GameObject caster;
	int startTick = -1;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		swords = caster.GetComponent<SummonedSwords>();
		queue = new List<(SummonedSword, Monster)>();
		sent = new Dictionary<SummonedSword, Monster>();
		startTick = -1;
		if (swords == null || !TryGetArgument("targets", out RuntimeValue value)) return;

		// 가장 먼저 소환한 사용 가능한 검부터 (SummonedSwords 목록 순서)
		List<SummonedSword> ready = new List<SummonedSword>();
		foreach (SummonedSword s in swords.Swords)
		{
			if (s != null && s.State == SummonedSwordState.Ready) ready.Add(s);
		}
		HashSet<Monster> picked = new HashSet<Monster>();
		int next = 0;
		foreach (Monster bug in value.AsBugs())
		{
			if (next >= ready.Count) break;
			if (bug == null || bug.IsDead || !picked.Add(bug)) continue;
			queue.Add((ready[next++], bug));
		}
		if (queue.Count > 0) swords.StabHit += OnStabHit;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (swords == null || queue == null || queue.Count == 0) return;
		if (startTick < 0) startTick = tick;
		if ((tick - startTick) % StaggerTicks != 0) return;

		(SummonedSword sword, Monster target) = queue[0];
		queue.RemoveAt(0);
		// 기다리는 사이 대상이 죽었거나 검이 다른 데 쓰였으면 건너뛴다
		if (sword == null || target == null || target.IsDead || sword.State != SummonedSwordState.Ready) return;
		Vector2 from = sword.transform.position;
		if (swords.Stab(sword, target.gameObject))
		{
			sent[sword] = target;
			Effects(task)?.RaiseFired(Id, from, (Vector2)target.transform.position - from);
		}
	}

	protected override void OnSkillEnd(FunctionContext task) => Unsubscribe();
	protected override void OnSkillCancel(FunctionContext task) => Unsubscribe();

	void Unsubscribe()
	{
		if (swords != null) swords.StabHit -= OnStabHit;
		queue?.Clear();
	}

	void OnStabHit(SummonedSword sword, GameObject hit)
	{
		if (sent == null || !sent.Remove(sword) || hit == null) return;
		Monster monster = hit.GetComponentInParent<Monster>();
		if (monster == null || monster.IsDead) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;
		DamageResult result = DamageCalculator.Calculate(new DamageRequest(attackPower, multiplier), DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage, caster);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(Id, monster.gameObject, sword.transform.position, result);
	}
}
