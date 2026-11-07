using UnityEngine;

// 이동 계열 (특수 이동 함수) 스킬. 수치는 SkillTable 표, 필요 조건 검사는 DataDrivenSkill이 맡는다.
// 대시·더블점프·검 타기는 #256에서 채웠다. 이동은 소유 클라이언트가 하므로 PlayerMovement에 요청만 한다 (서버에서는 [TargetRpc]로 소유 클라이언트에 감, #223).

/// <summary>
/// 이동 스킬의 공통 (#256): 스킬 동안 표의 heap(이동 계열 1칸)을 잡고, 끝나면 돌려준다.
/// <para>
/// - heap이 모자라면 이동하지 않는다 (에러 아님, 검기와 같은 규칙).<br/>
/// - 도중에 끊기면(실행 중단, 취소) heap은 누수로 남는다 (heap 누수 규칙).
/// </para>
/// </summary>
public abstract class MovementSkill : DataDrivenSkill
{
	int pointer = HeapStorage.NullPointer;

	/// <summary>heap을 잡은 뒤 이동을 시작한다. 시전자의 PlayerMovement를 넘긴다</summary>
	protected abstract void StartMove(FunctionContext task, PlayerMovement movement);

	/// <summary>스킬이 끝나거나(cancelled false) 끊길 때(true). 기본은 아무것도 안 함 (검 타기는 끊길 때만 내려온다)</summary>
	protected virtual void EndMove(FunctionContext task, PlayerMovement movement, bool cancelled) { }

	protected override void OnSkillStart(FunctionContext task)
	{
		pointer = HeapStorage.NullPointer;
		PlayerMovement movement = task.Caster.GetComponent<PlayerMovement>();
		if (movement == null) return;
		int size = Data != null ? Data.HeapSize : 1;
		if (size > 0)
		{
			// 이동 동작 자체를 heap에 올린다 (이 스킬 복사본을 객체로 씀)
			pointer = Malloc(task, this, size);
			if (pointer == HeapStorage.NullPointer)
			{
				Debug.Log($"{Name}: heap이 모자라 이동하지 못함");
				return;
			}
		}
		StartMove(task, movement);
	}

	protected override void OnSkillEnd(FunctionContext task)
	{
		EndMove(task, task.Caster.GetComponent<PlayerMovement>(), false);
		if (pointer != HeapStorage.NullPointer) Free(task, pointer);
		pointer = HeapStorage.NullPointer;
	}

	protected override void OnSkillCancel(FunctionContext task) => EndMove(task, task.Caster.GetComponent<PlayerMovement>(), true);
}

/// <summary>
/// 대시 (#256). 바라보는 쪽으로 짧게 빠르게 이동한다 (PlayerMovement.StartDash, 임시 4칸 / 0.15초). 세 캐릭터 공통, 대상 없음, 공중 가능.
/// </summary>
public class CommonDash : MovementSkill
{
	public override string Id => PredefinedFunctionIds.Dash;

	protected override void StartMove(FunctionContext task, PlayerMovement movement)
	{
		movement.StartDash(movement.Facing);
		Effects(task)?.RaiseFired(Id, task.Caster.transform.position, new Vector2(movement.Facing, 0f));
	}
}

/// <summary>
/// 더블점프 (#256). 플레이어는 스킬 트리에서 해금하면 공중에서 Space로 한 번 더 뛴다 (PlayerMovement가 스스로 함, 슬롯에는 못 넣음).<br/>
/// 블록코딩에서 이 함수를 부르면 같은 공중 점프를 한다: 공중이고 이번 공중에서 아직 안 썼을 때만. 세 캐릭터 공통, 대상 없음.
/// </summary>
public class CommonDoubleJump : MovementSkill
{
	public override string Id => PredefinedFunctionIds.DoubleJump;

	protected override void StartMove(FunctionContext task, PlayerMovement movement)
	{
		movement.RequestAirJump();
		Effects(task)?.RaiseFired(Id, task.Caster.transform.position, Vector2.up);
	}
}

/// <summary>
/// 검 타기 (#256). 사용 가능한 소환검 하나가 발밑에 붙고, PlayerMovement.RideSeconds(임시 30초) 동안 중력 없이 ←→↑↓로 날아다닌다 (대상 없음, 소환검 필요).
/// <para>
/// - 가장 먼저 소환한 사용 가능한 검을 쓴다 (SummonedSwords.Ride, Riding 상태). 소환검이 없으면 시작 전에 NoSummonedSwordError.<br/>
/// - 스킬(20틱)은 타기를 시작만 하고 먼저 끝난다. 나는 시간은 스킬 시간과 따로다. 시간이 다 되면 내려오고(중력이 돌아옴) 검은 곁으로 돌아온다 (PlayerMovement, SummonedSwords가 각자 시간을 잼).<br/>
/// - 스킬이 시작 직후 끊기면(실행 중단 등) 바로 내려오고 검도 돌아온다.<br/>
/// - 위아래(↑↓, MoveY)는 PlayerMovement가 검 타기 중에 직접 읽는다 (#317). 이 스킬은 타기를 시작·끝내기만 한다.
/// </para>
/// </summary>
public class CSwordRide : MovementSkill
{
	public override string Id => PredefinedFunctionIds.SwordRide;

	SummonedSwords swords;
	SummonedSword riding;

	protected override void StartMove(FunctionContext task, PlayerMovement movement)
	{
		swords = task.Caster.GetComponent<SummonedSwords>();
		riding = null;
		if (swords == null || !swords.TryGetFirstReady(out SummonedSword sword) || !swords.Ride(sword, movement.RideSeconds)) return;
		riding = sword;
		movement.StartRide();
		Effects(task)?.RaiseFired(Id, task.Caster.transform.position, Vector2.zero);
	}

	// 정상으로 끝나면 계속 난다 (시간이 다 되면 각자 끝냄). 끊겼을 때만 바로 내려온다
	protected override void EndMove(FunctionContext task, PlayerMovement movement, bool cancelled)
	{
		if (riding == null || !cancelled) return;
		movement?.StopRide();
		swords?.EndRide(riding);
		riding = null;
	}
}
