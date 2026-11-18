using System.Collections.Generic;
using UnityEngine;

// 파이썬 마법사 스킬의 기반과 원소·범위 계열. 수치는 SkillTable 표, 필요 조건 검사와 연출 event는 DataDrivenSkill이 맡는다.
// 구슬 계열은 PythonOrbSkills.cs, 보조·이동 계열은 PythonUtilitySkills.cs. 기획: docs/systems/python-skills.md

/// <summary>
/// 마법탄 하나의 판정 수치. 서버의 판정(MagicBoltSkill)과 각 화면의 그림(MagicEffectView)이 같은 값을 읽어 같은 길로 난다.
/// </summary>
public readonly struct MagicBoltSpec
{
	/// <summary>초당 유닛</summary>
	public readonly float Speed;
	/// <summary>날아가는 거리 (유닛)</summary>
	public readonly float Range;
	/// <summary>판정 상자 (가로 = 날아가는 방향, 세로, 유닛)</summary>
	public readonly Vector2 HitSize;
	/// <summary>true면 관통, false면 첫 대상에서 멈춤</summary>
	public readonly bool Pierce;

	public MagicBoltSpec(float speed, float range, Vector2 hitSize, bool pierce)
	{
		Speed = speed;
		Range = range;
		HitSize = hitSize;
		Pierce = pierce;
	}
}

/// <summary>
/// 마법탄을 쏘는 스킬의 판정 수치 표 (전부 임시). 스킬 ID로 찾는다.
/// 날아가는 시간(Range ÷ Speed)은 그 스킬의 동작 시간(표의 틱 수 × 0.02초)보다 짧아야 한다. 스킬이 끝나면 마법탄도 사라지기 때문이다.
/// </summary>
public static class MagicBolts
{
	static readonly Dictionary<string, MagicBoltSpec> specs = new Dictionary<string, MagicBoltSpec>
	{
		[PredefinedFunctionIds.PyMagicMissile] = new MagicBoltSpec(22f, 10f, new Vector2(0.6f, 0.6f), false),
		[PredefinedFunctionIds.PyFireball] = new MagicBoltSpec(18f, 10f, new Vector2(0.8f, 0.8f), false),
		[PredefinedFunctionIds.PyIceSpear] = new MagicBoltSpec(26f, 10f, new Vector2(1.2f, 0.5f), true),
		[PredefinedFunctionIds.PyOrbShot] = new MagicBoltSpec(26f, 12f, new Vector2(0.7f, 0.7f), false),
		[PredefinedFunctionIds.PyOrbBarrage] = new MagicBoltSpec(26f, 12f, new Vector2(0.7f, 0.7f), false),
		[PredefinedFunctionIds.PyHex] = new MagicBoltSpec(16f, 9f, new Vector2(0.7f, 0.7f), false),
		[PredefinedFunctionIds.PySnakeBind] = new MagicBoltSpec(16f, 9f, new Vector2(0.8f, 0.6f), false),
	};

	/// <summary>스킬 ID의 마법탄 수치. 마법탄을 쏘지 않는 스킬이면 false.</summary>
	public static bool TryGet(string skillId, out MagicBoltSpec spec)
	{
		if (skillId != null && specs.TryGetValue(skillId, out spec)) return true;
		spec = default;
		return false;
	}
}

/// <summary>
/// 파이썬 마법사 스킬의 공통: 지팡이 끝 자리, 대상 파라미터, 데미지 주기, 둘레의 몬스터 찾기.
/// <para>
/// - 데미지: 공격력 × 표 배율 × scale → DamageCalculator → IDamageable.TakeDamage(시전자) → SkillEffectEvents.Hit.<br/>
/// - 판정은 시전자가 속한 장소 씬의 물리 세계에 묻는다 (place-scenes.md).
/// </para>
/// </summary>
public abstract class MagicSkill : DataDrivenSkill
{
	protected GameObject caster;

	/// <summary>범위 반경 (표의 Area Radius). 표에 없으면 3</summary>
	protected float Radius => Data != null ? Data.AreaRadius : 3f;

	protected static Vector2 BodyCenter(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}

	/// <summary>발밑 자리 (콜라이더 아랫변 가운데)</summary>
	protected static Vector2 Feet(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? new Vector2(col.bounds.center.x, col.bounds.min.y) : (Vector2)obj.transform.position;
	}

	protected static int FacingOf(GameObject caster)
	{
		PlayerMovement movement = caster.GetComponent<PlayerMovement>();
		return movement != null ? movement.Facing : 1;
	}

	/// <summary>지팡이 끝: 몸 가운데에서 바라보는 쪽 앞 위 (임시). 마법탄이 나가는 자리</summary>
	protected static Vector2 StaffTip(GameObject caster) => BodyCenter(caster) + new Vector2(0.7f * FacingOf(caster), 0.4f);

	/// <summary>대상 파라미터 target. 값이 없으면 null. 대상 하나를 받는 스킬(Single, Pierce)만 부른다</summary>
	protected Monster TargetArgument() => TryGetArgument("target", out RuntimeValue value) ? value.AsBug() : null;

	/// <summary>대상 쪽을 바라보게 한다</summary>
	protected void FaceTarget(Monster target)
	{
		PlayerMovement movement = caster.GetComponent<PlayerMovement>();
		if (target != null && movement != null) movement.FaceTowards(target.transform.position.x);
	}

	/// <summary>몬스터에게 공격력 × 표 배율 × scale 데미지를 주고 Hit 연출 event를 낸다. 죽었거나 없는 대상이면 아무 일도 없다.</summary>
	protected DamageResult Deal(Monster monster, Vector2 point, float scale = 1f)
	{
		if (caster == null || monster == null || monster.IsDead) return default;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = (Data != null ? Data.DamageMultiplier : 1f) * scale;
		// 자세(#538)의 데미지 추가분을 조건 배율에 더한다. 지금 Python은 자세 규칙이 없어 0이다. 규칙이 생기면 이 자리가 그대로 쓴다
		DamageRequest request = new DamageRequest(attackPower, multiplier) { BonusRate = StanceDamageBonus(caster) };
		DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage, caster);
		NotifyDamageDealt(caster, result.Damage);
		caster.GetComponent<SkillEffectEvents>()?.RaiseHit(Id, monster.gameObject, point, result);
		return result;
	}

	/// <summary>center 둘레 radius 안의 살아 있는 몬스터 (같은 몬스터는 한 번만)</summary>
	protected static List<Monster> MonstersInCircle(GameObject caster, Vector2 center, float radius)
	{
		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(LayerMask.GetMask("Monster"));
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		caster.scene.GetPhysicsScene2D().OverlapCircle(center, radius, filter, hits);

		List<Monster> found = new List<Monster>();
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead || found.Contains(monster)) continue;
			found.Add(monster);
		}
		return found;
	}
}

/// <summary>
/// 마법탄을 쏘는 스킬의 기반. 매직 미사일, 파이어볼, 얼음 창, 구슬 발사 등이 상속한다.
/// <para>
/// - 마법탄은 오브젝트가 아니라 서버의 판정 값이다. LaunchBolt로 쏘면 매 틱 MagicBolts 표의 속도만큼 나아가며 판정 상자에 닿은 몬스터를 맞힌다 (닿아야 맞는다, #437과 같은 기준).<br/>
/// - 그림은 SkillEffectEvents.Fired(스킬 ID, 출발 자리, 방향)를 받은 각 화면의 MagicEffectView가 같은 표로 같은 길을 그린다.<br/>
/// - heap: 마법탄 하나가 표의 heap 크기만큼 쓴다 (0이면 쓰지 않음). 사거리 끝·명중으로 끝나면 그 틱에 돌려주고, 스킬이 끝날 때 남은 것도 돌려준다.<br/>
/// - 끊기면(실행 중단, 취소) 마법탄은 사라진다. heap은 Python의 가비지 컬렉션이 돌려준다 (OnCancel에서는 Free할 수 없음).<br/>
/// - 관통이 아니면 처음 닿은 몬스터에서 멈추고, 관통이면 같은 몬스터를 두 번 맞히지 않고 끝까지 간다.
/// </para>
/// </summary>
public abstract class MagicBoltSkill : MagicSkill
{
	// 날아가는 마법탄 하나 (서버 판정용 값)
	protected class Bolt
	{
		public Vector2 Position;
		public Vector2 Direction;
		public float Travelled;
		public float DamageScale = 1f;
		public int Pointer = HeapStorage.NullPointer;
		public bool Finished;
		public readonly HashSet<Monster> AlreadyHit = new HashSet<Monster>();
	}

	// 실행기는 스킬을 MemberwiseClone으로 복사하므로, 리스트는 복사본이 처음 쓸 때 새로 만든다 (원본과 공유하지 않게)
	List<Bolt> bolts;
	MagicBoltSpec spec;
	float secondsPerTick;

	/// <summary>지금 날고 있는 마법탄 수. 테스트가 읽는다.</summary>
	public int FlyingCount => bolts != null ? bolts.Count : 0;

	/// <summary>마법탄이 몬스터를 맞혀 데미지를 준 뒤 불린다. 기본은 아무것도 안 함 (파이어볼 폭발, 얼음 창 이속 감소 등).</summary>
	protected virtual void OnBoltHit(FunctionContext task, Monster monster, Vector2 point, DamageResult result) { }

	/// <summary>
	/// origin에서 dir 방향으로 마법탄 하나를 쏜다. heap이 모자라면 쏘지 않고 false (에러 아님).
	/// </summary>
	protected bool LaunchBolt(FunctionContext task, Vector2 origin, Vector2 dir, float damageScale = 1f)
	{
		caster = task.Caster;
		if (!MagicBolts.TryGet(Id, out spec))
		{
			Debug.LogError($"{Name}: MagicBolts 표에 {Id}의 마법탄 수치가 없습니다.");
			return false;
		}
		dir = dir.sqrMagnitude < 0.0001f ? new Vector2(FacingOf(caster), 0f) : dir.normalized;

		Bolt bolt = new Bolt { Position = origin, Direction = dir, DamageScale = damageScale };
		int size = Data != null ? Data.HeapSize : 0;
		if (size > 0)
		{
			bolt.Pointer = Malloc(task, bolt, size);
			if (bolt.Pointer == HeapStorage.NullPointer)
			{
				// heap 부족: 쏘지 않는다 (에러 아님)
				Debug.Log($"{Name}: heap이 모자라 마법탄을 쏘지 못함");
				return false;
			}
		}

		FunctionExecutor executor = caster.GetComponent<FunctionExecutor>();
		secondsPerTick = executor != null ? executor.SecondsPerTick : Time.fixedDeltaTime;
		(bolts ??= new List<Bolt>()).Add(bolt);
		Effects(task)?.RaiseFired(Id, origin, dir);
		// 쏜 자리에 이미 몬스터가 있으면 바로 맞힌다
		CheckHits(task, bolt);
		return true;
	}

	protected override void OnTick(FunctionContext task, int tick) => StepBolts(task);

	/// <summary>모든 마법탄을 한 틱만큼 움직이고 판정한다. OnTick을 덮어쓴 스킬은 base.OnTick을 부른다.</summary>
	void StepBolts(FunctionContext task)
	{
		if (bolts == null) return;
		for (int i = bolts.Count - 1; i >= 0; i--)
		{
			Bolt bolt = bolts[i];
			if (!bolt.Finished)
			{
				float step = Mathf.Min(spec.Speed * secondsPerTick, spec.Range - bolt.Travelled);
				bolt.Position += bolt.Direction * step;
				bolt.Travelled += step;
				CheckHits(task, bolt);
				if (bolt.Travelled >= spec.Range) bolt.Finished = true;
			}
			if (bolt.Finished) Release(task, i);
		}
	}

	protected override void OnSkillEnd(FunctionContext task)
	{
		if (bolts == null) return;
		for (int i = bolts.Count - 1; i >= 0; i--) Release(task, i);
	}

	// 끊기면 마법탄만 지운다. heap은 가비지 컬렉션이 돌려준다
	protected override void OnSkillCancel(FunctionContext task) => bolts?.Clear();

	void Release(FunctionContext task, int index)
	{
		Bolt bolt = bolts[index];
		bolts.RemoveAt(index);
		if (bolt.Pointer != HeapStorage.NullPointer) Free(task, bolt.Pointer);
	}

	void CheckHits(FunctionContext task, Bolt bolt)
	{
		if (bolt.Finished) return;

		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(LayerMask.GetMask("Monster"));
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		float angle = Mathf.Atan2(bolt.Direction.y, bolt.Direction.x) * Mathf.Rad2Deg;
		caster.scene.GetPhysicsScene2D().OverlapBox(bolt.Position, spec.HitSize, angle, filter, hits);

		// 가까운 몬스터부터 맞힌다 (관통이 아니면 가장 먼저 닿은 한 명)
		Vector2 at = bolt.Position;
		hits.Sort((a, b) => ((Vector2)a.bounds.center - at).sqrMagnitude.CompareTo(((Vector2)b.bounds.center - at).sqrMagnitude));
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead || !bolt.AlreadyHit.Add(monster)) continue;
			DamageResult result = Deal(monster, at, bolt.DamageScale);
			OnBoltHit(task, monster, at, result);
			if (!spec.Pierce)
			{
				bolt.Finished = true;
				return;
			}
		}
	}
}

/// <summary>
/// 고른 대상 쪽으로 지팡이 끝에서 마법탄 하나를 쏘는 스킬의 기반. 대상 값이 없으면 바라보는 방향으로 쏜다.
/// 쏜 뒤에는 곧게 날아가므로 대상이 움직이면 빗나갈 수 있다.
/// </summary>
public abstract class AimedBoltSkill : MagicBoltSkill
{
	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		Monster target = TargetArgument();
		FaceTarget(target);
		Vector2 origin = StaffTip(caster);
		Vector2 dir = target != null ? BodyCenter(target.gameObject) - origin : new Vector2(FacingOf(caster), 0f);
		LaunchBolt(task, origin, dir);
	}
}

// 원소 계열 ------------------------------------------------------------------

/// <summary>
/// 매직 미사일. 고른 대상 쪽으로 마법탄을 쏴 처음 닿은 몬스터 한 명에게 공격력 × 표 배율 (단일). 마법사의 기본 공격.
/// </summary>
public class PyMagicMissile : AimedBoltSkill
{
	public override string Id => PredefinedFunctionIds.PyMagicMissile;
}

/// <summary>
/// 파이어볼. 고른 대상 쪽으로 불덩이를 쏜다 (단일 + 폭발).
/// <para>
/// - 처음 닿은 몬스터에게 공격력 × 표 배율.<br/>
/// - 닿은 자리에서 터져, 표의 Area Radius 안의 다른 몬스터에게 그 데미지의 SplashRate(임시 0.5)배.
/// </para>
/// </summary>
public class PyFireball : AimedBoltSkill
{
	/// <summary>폭발이 둘레 몬스터에게 주는 데미지 비율 (임시)</summary>
	public const float SplashRate = 0.5f;

	public override string Id => PredefinedFunctionIds.PyFireball;

	protected override void OnBoltHit(FunctionContext task, Monster monster, Vector2 point, DamageResult result)
	{
		foreach (Monster other in MonstersInCircle(caster, point, Radius))
		{
			if (other == monster) continue;
			Deal(other, BodyCenter(other.gameObject), SplashRate);
		}
	}
}

/// <summary>
/// 얼음 창. 고른 대상 쪽으로 얼음 창을 쏴, 지나가는 길의 몬스터를 모두 맞히고 이속 감소를 건다 (관통).
/// </summary>
public class PyIceSpear : AimedBoltSkill
{
	public override string Id => PredefinedFunctionIds.PyIceSpear;

	protected override void OnBoltHit(FunctionContext task, Monster monster, Vector2 point, DamageResult result) =>
		StatusEffects.TryApply(monster.gameObject, StatusType.Slow);
}

/// <summary>
/// 체인 라이트닝. 고른 대상에게 번개를 내리꽂고, 가까운 다른 몬스터로 MaxJumps(임시 2)번까지 튄다 (단일 → 연쇄).
/// <para>
/// - 번개는 바로 닿는다. 대상이 HitRange(임시 9칸, 몸 가운데끼리 거리) 밖이면 빗나간다.<br/>
/// - 첫 대상: 공격력 × 표 배율. 튄 대상: 그 JumpDamageRate(임시 0.6)배.<br/>
/// - JumpIntervalTicks(임시 3틱)마다 마지막으로 맞은 자리에서 ChainRange(임시 4칸) 안의, 아직 안 맞은 가장 가까운 몬스터로 튄다. 없으면 거기서 끝난다.<br/>
/// - 번개 줄기마다 Fired(시작 자리, 끝 자리까지의 벡터)를 낸다. 그림(MagicEffectView)이 그 사이에 번개를 그린다.
/// </para>
/// </summary>
public class PyChainLightning : MagicSkill
{
	/// <summary>첫 대상까지의 사거리 (유닛, 임시)</summary>
	public const float HitRange = 9f;
	/// <summary>다음 대상으로 튀는 거리 (유닛, 임시)</summary>
	public const float ChainRange = 4f;
	/// <summary>튀는 횟수 (임시)</summary>
	public const int MaxJumps = 2;
	/// <summary>튄 대상이 받는 데미지 비율 (임시)</summary>
	public const float JumpDamageRate = 0.6f;
	/// <summary>튀는 간격 (틱, 임시)</summary>
	public const int JumpIntervalTicks = 3;

	public override string Id => PredefinedFunctionIds.PyChainLightning;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	HashSet<Monster> struck;
	Vector2 lastPoint;
	int jumpsLeft;
	int currentTick;
	int nextJumpTick;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		struck = new HashSet<Monster>();
		jumpsLeft = 0;
		currentTick = 0;
		Monster target = TargetArgument();
		FaceTarget(target);
		Vector2 origin = StaffTip(caster);
		if (target == null)
		{
			Effects(task)?.RaiseFired(Id, origin, new Vector2(FacingOf(caster) * 2f, 0f));
			return;
		}

		Vector2 at = BodyCenter(target.gameObject);
		if (Vector2.Distance(BodyCenter(caster), at) > HitRange)
		{
			// 사거리 밖: 번개가 닿지 못하고 흩어진다
			Effects(task)?.RaiseFired(Id, origin, (at - origin).normalized * 2f);
			return;
		}
		Strike(task, origin, target, 1f);
		jumpsLeft = MaxJumps;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		currentTick = tick;
		if (jumpsLeft <= 0 || tick < nextJumpTick) return;

		Monster next = null;
		float best = float.MaxValue;
		foreach (Monster monster in MonstersInCircle(caster, lastPoint, ChainRange))
		{
			if (struck.Contains(monster)) continue;
			float dist = (BodyCenter(monster.gameObject) - lastPoint).sqrMagnitude;
			if (dist < best) { best = dist; next = monster; }
		}
		if (next == null)
		{
			jumpsLeft = 0;
			return;
		}
		jumpsLeft--;
		Strike(task, lastPoint, next, JumpDamageRate);
	}

	void Strike(FunctionContext task, Vector2 from, Monster monster, float scale)
	{
		struck.Add(monster);
		Vector2 to = BodyCenter(monster.gameObject);
		Effects(task)?.RaiseFired(Id, from, to - from);
		Deal(monster, to, scale);
		lastPoint = to;
		nextJumpTick = currentTick + JumpIntervalTicks;
	}
}

// 범위 계열 ------------------------------------------------------------------

/// <summary>
/// 고른 대상이 있던 자리에 범위 마법을 까는 스킬의 기반. 메테오, 블리자드, 독 안개가 상속한다.
/// <para>
/// - 시작할 때 대상의 자리를 한 번 읽어 그 자리에 깐다. 그 뒤 대상이 움직여도 자리는 그대로다.<br/>
/// - 깔면 Fired(스킬 ID, 가운데 자리, 0)를 낸다. 그림(MagicEffectView)은 스킬이 끝날 때(Ended)까지 보여 준다.<br/>
/// - 매 틱 OnZoneTick이 불린다. 대상 값이 없으면 아무것도 깔지 않는다.
/// </para>
/// </summary>
public abstract class MagicZoneSkill : MagicSkill
{
	protected Vector2 center;
	bool placed;

	/// <summary>깔 자리. 기본은 대상의 몸 가운데</summary>
	protected virtual Vector2 PlaceAt(Monster target) => BodyCenter(target.gameObject);

	/// <summary>깔린 동안 매 틱 불린다 (tick은 1부터)</summary>
	protected abstract void OnZoneTick(FunctionContext task, int tick);

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		placed = false;
		Monster target = TargetArgument();
		if (target == null) return;
		FaceTarget(target);
		center = PlaceAt(target);
		placed = true;
		Effects(task)?.RaiseFired(Id, center, Vector2.zero);
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (placed) OnZoneTick(task, tick);
	}

	/// <summary>가운데 둘레 표의 Area Radius 안의 몬스터 모두에게 공격력 × 표 배율 데미지를 주고, 맞은 몬스터를 돌려준다.</summary>
	protected List<Monster> HitAllInZone()
	{
		List<Monster> monsters = MonstersInCircle(caster, center, Radius);
		foreach (Monster monster in monsters) Deal(monster, BodyCenter(monster.gameObject));
		return monsters;
	}
}

/// <summary>
/// 메테오. 고른 대상의 발밑 자리에 운석을 떨어뜨린다 (범위).
/// <para>
/// - 시전하고 ImpactTick(임시 20틱 = 0.4초) 뒤에 떨어진다. 그때 표의 Area Radius 안의 몬스터 모두에게 공격력 × 표 배율.<br/>
/// - 떨어지기 전에 끊기면 데미지가 없다. 쿨타임은 표.
/// </para>
/// </summary>
public class PyMeteor : MagicZoneSkill
{
	/// <summary>운석이 떨어지는 틱 (임시). 그림(meteor_cast)의 떨어지는 장면 길이와 맞춘다</summary>
	public const int ImpactTick = 20;

	public override string Id => PredefinedFunctionIds.PyMeteor;

	protected override Vector2 PlaceAt(Monster target) => Feet(target.gameObject);

	protected override void OnZoneTick(FunctionContext task, int tick)
	{
		if (tick == ImpactTick) HitAllInZone();
	}
}

/// <summary>
/// 블리자드. 고른 대상의 자리에 눈보라를 깐다 (범위, 다단 히트).
/// <para>
/// - 스킬 동안 PulseIntervalTicks(임시 10틱)마다 표의 Area Radius 안의 몬스터 모두에게 공격력 × 표 배율, 맞으면 이속 감소.<br/>
/// - 끊기면 눈보라도 사라진다.
/// </para>
/// </summary>
public class PyBlizzard : MagicZoneSkill
{
	/// <summary>데미지 간격 (틱, 임시)</summary>
	public const int PulseIntervalTicks = 10;

	public override string Id => PredefinedFunctionIds.PyBlizzard;

	protected override void OnZoneTick(FunctionContext task, int tick)
	{
		if (tick % PulseIntervalTicks != 0) return;
		foreach (Monster monster in HitAllInZone()) StatusEffects.TryApply(monster.gameObject, StatusType.Slow);
	}
}

/// <summary>
/// 독 안개. 고른 대상의 자리에 독 안개를 깐다 (범위, 약한 데미지 + 중독).
/// <para>
/// - 스킬 동안 PulseIntervalTicks(임시 10틱)마다 표의 Area Radius 안의 몬스터 모두에게 공격력 × 표 배율(약함), 맞으면 중독 한 겹.<br/>
/// - 안개 안에 오래 있을수록 중독이 겹쳐(최대 StatusDefaults.PoisonMaxStacks) 더 아프다.
/// </para>
/// </summary>
public class PyVenomMist : MagicZoneSkill
{
	/// <summary>데미지 간격 (틱, 임시)</summary>
	public const int PulseIntervalTicks = 10;

	public override string Id => PredefinedFunctionIds.PyVenomMist;

	protected override void OnZoneTick(FunctionContext task, int tick)
	{
		if (tick % PulseIntervalTicks != 0) return;
		foreach (Monster monster in HitAllInZone()) StatusEffects.TryApply(monster.gameObject, StatusType.Poison, caster);
	}
}
