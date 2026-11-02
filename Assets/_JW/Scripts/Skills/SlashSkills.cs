using UnityEngine;

// 휘두르기 계열 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 검 휘두르기·검 크게베기는 #163에서 채웠다 (SwordWaveSkill). 나머지 효과는 각 스킬 이슈에서 채운다. 그 전까지는 정해진 틱 동안 실행되고 로그만 남긴다.

/// <summary>
/// 검기를 날리는 스킬의 기반 (#163). 검 휘두르기, 검 크게베기가 상속한다.
/// <para>
/// - 시작할 때 몸 가운데에서 검기를 만들고(SwordWaveLauncher.SpawnWave) heap에 올린다(Malloc, 표의 heap 크기). heap이 모자라면 검기를 없애고 아무것도 날리지 않는다.<br/>
/// - 검기가 끝나면(사거리 끝, 첫 대상에서 멈춤) 그 틱에 검기를 없애고 heap을 돌려준다(Free). 스킬이 끝날 때 아직 날고 있어도 같다.<br/>
/// - 끊기면(실행 중단, 취소) 검기는 없애지만 heap은 누수된다 (heap 누수 규칙, OnCancel에서는 Free할 수 없음).<br/>
/// - 데미지: 검기가 알린 몬스터마다 DamageCalculator(공격력 × 표 배율) → IDamageable.TakeDamage → OnHitMonster → SkillEffectEvents.Hit.
/// </para>
/// </summary>
public abstract class SwordWaveSkill : DataDrivenSkill
{
	SwordWave wave;
	int pointer = HeapStorage.NullPointer;
	GameObject caster;

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

	/// <summary>대상마다 데미지를 준 뒤 불린다. 기본은 아무것도 안 함 (크게베기는 출혈).</summary>
	protected virtual void OnHitMonster(Monster monster, DamageResult result) { }

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		SwordWaveLauncher launcher = caster.GetComponent<SwordWaveLauncher>();
		if (launcher == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 SwordWaveLauncher가 없습니다.");
			return;
		}

		Vector2 origin = BodyCenter(caster);
		Vector2 dir = Direction(task, origin).normalized;
		if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

		wave = launcher.SpawnWave(Wide, origin);
		if (wave == null) return;
		pointer = Malloc(task, wave, Data != null ? Data.HeapSize : 1);
		if (pointer == HeapStorage.NullPointer)
		{
			// heap 부족: 검기를 날리지 않는다 (에러 아님)
			Debug.Log($"{Name}: heap이 모자라 검기를 날리지 못함");
			launcher.DespawnWave(wave);
			wave = null;
			return;
		}

		Effects(task)?.RaiseFired(Id, origin, dir);
		wave.MonsterHit += OnWaveHit;
		wave.Launch(dir, Speed, Range, HitSize, Pierce);
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (wave != null && wave.IsFinished) Release(task);
	}

	protected override void OnSkillEnd(FunctionContext task) => Release(task);

	// 끊기면 검기는 없애고 heap은 누수로 남긴다
	protected override void OnSkillCancel(FunctionContext task)
	{
		if (wave == null) return;
		wave.MonsterHit -= OnWaveHit;
		task.Caster.GetComponent<SwordWaveLauncher>()?.DespawnWave(wave);
		wave = null;
	}

	void Release(FunctionContext task)
	{
		if (wave == null) return;
		wave.MonsterHit -= OnWaveHit;
		task.Caster.GetComponent<SwordWaveLauncher>()?.DespawnWave(wave);
		wave = null;
		if (pointer != HeapStorage.NullPointer) Free(task, pointer);
		pointer = HeapStorage.NullPointer;
	}

	void OnWaveHit(Monster monster)
	{
		if (caster == null || monster == null) return;
		PlayerStats stats = caster.GetComponent<PlayerStats>();
		int attackPower = stats != null ? stats.AttackPower : 0;
		float multiplier = Data != null ? Data.DamageMultiplier : 1f;
		DamageResult result = DamageCalculator.Calculate(new DamageRequest(attackPower, multiplier), DamageTarget.FromMonster(monster));
		monster.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage);
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
		StatusEffects.TryApply(monster.gameObject, StatusType.Bleed);
}

/// <summary>소환검 휘두르기. 소환된 검 개수만큼 검기 발사 (단일)</summary>
public class CSummonedSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.SummonedSlash;
}

/// <summary>상처 가르기. 출혈 상태의 적에게 출혈 도트 데미지 2.0배 (단일)</summary>
public class CWoundSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.WoundSlash;
}

/// <summary>검 다중베기. 소환된 검들이 각각 한 번씩 대상을 벰 (단일 + 다단 히트)</summary>
public class CMultiSlash : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.MultiSlash;
}

/// <summary>전장 휩쓸기. 소환된 검들이 여러 적에게 각각 가서 벰 (여러 명 선택)</summary>
public class CBattlefieldSweep : DataDrivenSkill
{
	public override string Id => PredefinedFunctionIds.BattlefieldSweep;
}
