using UnityEngine;

// 파이썬 마법사의 보조·이동 계열 스킬. 보조는 약한 마법탄이나 번쩍임으로 상태 이상을 건다. 이동은 MovementSkill(스킬 동안 heap 1칸)을 따른다.
// 보조 스킬의 상태 이상 지속 시간은 시전자의 자세가 늘릴 수 있다 (#546, Python 7의 배수 +30%). 그래서 시전자와 StanceStatusDurationRate를 넘긴다.

/// <summary>
/// 저주. 고른 대상 쪽으로 저주탄을 쏴, 처음 닿은 몬스터에게 약한 데미지와 표식을 건다 (단일). 표식이 걸린 대상은 그 다음 공격부터 데미지 2배.
/// </summary>
public class PyHex : AimedBoltSkill
{
	public override string Id => PredefinedFunctionIds.PyHex;

	protected override void OnBoltHit(FunctionContext task, Monster monster, Vector2 point, DamageResult result) =>
		StatusEffects.TryApply(monster.gameObject, StatusType.Mark, task.Caster, StanceStatusDurationRate(task.Caster));
}

/// <summary>
/// 뱀 속박. 고른 대상 쪽으로 뱀을 날려, 처음 닿은 몬스터에게 약한 데미지와 기절을 건다 (단일).
/// </summary>
public class PySnakeBind : AimedBoltSkill
{
	public override string Id => PredefinedFunctionIds.PySnakeBind;

	protected override void OnBoltHit(FunctionContext task, Monster monster, Vector2 point, DamageResult result) =>
		StatusEffects.TryApply(monster.gameObject, StatusType.Stun, task.Caster, StanceStatusDurationRate(task.Caster));
}

/// <summary>
/// 섬광. 지팡이에서 빛을 터뜨려 캐릭터 둘레(표의 Area Radius)의 몬스터 모두에게 실명을 건다 (범위, 데미지 없음).
/// </summary>
public class PyFlash : MagicSkill
{
	public override string Id => PredefinedFunctionIds.PyFlash;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		Vector2 center = BodyCenter(caster);
		Effects(task)?.RaiseFired(Id, center, Vector2.zero);
		float durationRate = StanceStatusDurationRate(caster);
		foreach (Monster monster in MonstersInCircle(caster, center, Radius)) StatusEffects.TryApply(monster.gameObject, StatusType.Blind, caster, durationRate);
	}
}

/// <summary>
/// 블링크. 바라보는 쪽으로 BlinkDistance(임시 5칸)만큼 순간이동한다 (대상 없음, 공중 가능). 벽 앞에서는 멈춘다 (PlayerMovement.Blink).
/// Fired의 방향에는 이동 벡터(바라보는 쪽 × 거리)를 담는다. 그림이 출발 자리와 도착 자리에 연기를 낸다.
/// </summary>
public class PyBlink : MovementSkill
{
	/// <summary>순간이동 거리 (유닛, 임시)</summary>
	public const float BlinkDistance = 5f;

	public override string Id => PredefinedFunctionIds.PyBlink;

	protected override void StartMove(FunctionContext task, PlayerMovement movement)
	{
		Vector2 from = Center(task.Caster);
		movement.Blink(movement.Facing, BlinkDistance);
		Effects(task)?.RaiseFired(Id, from, new Vector2(movement.Facing * BlinkDistance, 0f));
	}

	static Vector2 Center(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
	}
}

/// <summary>
/// 공중 부양. LevitateSeconds(임시 6초) 동안 중력 없이 ←→↑↓로 떠다닌다 (대상 없음). 검 타기와 같은 움직임이고 소환검은 쓰지 않는다 (PlayerMovement.StartLevitate).
/// 스킬은 부양을 시작만 하고 먼저 끝난다. 시작 직후 끊기면 바로 내려온다.
/// </summary>
public class PyLevitate : MovementSkill
{
	/// <summary>떠 있는 시간 (초, 임시)</summary>
	public const float LevitateSeconds = 6f;

	public override string Id => PredefinedFunctionIds.PyLevitate;

	bool floating;

	protected override void StartMove(FunctionContext task, PlayerMovement movement)
	{
		floating = true;
		movement.StartLevitate(LevitateSeconds);
		Effects(task)?.RaiseFired(Id, task.Caster.transform.position, Vector2.zero);
	}

	// 정상으로 끝나면 계속 떠 있는다 (시간이 다 되면 PlayerMovement가 내린다). 끊겼을 때만 바로 내려온다
	protected override void EndMove(FunctionContext task, PlayerMovement movement, bool cancelled)
	{
		if (!floating || !cancelled) return;
		movement?.StopRide();
		floating = false;
	}
}
