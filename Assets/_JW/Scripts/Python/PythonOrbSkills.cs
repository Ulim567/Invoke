using System.Collections.Generic;
using UnityEngine;

// 파이썬 마법사의 구슬 계열 스킬. 마나 구슬(ManaOrb)을 heap에 만들고, 구슬 스킬이 구슬을 써서(Free) 더 센 마법을 쓴다.
// Python은 가비지 컬렉션이라 해제 스킬이 따로 없다. 구슬은 쓰면 사라지고, 끊긴 구슬 생성의 구슬도 누수되지 않는다.

/// <summary>
/// 구슬 생성. 마나 구슬 하나를 heap에 만든다 (대상 없음, 누르면 바로 발동).
/// <para>
/// - 크기는 표의 heap 크기(임시 2칸). 스킬이 정상으로 끝나면 구슬은 남아 구슬 스킬이 쓸 수 있다.<br/>
/// - 이미 ManaOrbs.MaxCount(5)개거나 heap이 모자라면 만들지 않는다 (에러 아님).<br/>
/// - 도중에 끊기면 만들던 구슬은 사라진다 (가비지 컬렉션, 누수 아님).
/// </para>
/// </summary>
public class PyOrbCreate : MagicSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbCreate;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		ManaOrbs orbs = caster.GetComponent<ManaOrbs>();
		if (orbs == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 ManaOrbs가 없습니다.");
			return;
		}
		// 이미 5개면 만들지 않는다
		int count = CountHeapObjects<ManaOrb>(task);
		if (count >= ManaOrbs.MaxCount) return;

		int pointer = Malloc(task, new ManaOrb(), Data != null ? Data.HeapSize : 2);
		if (pointer == HeapStorage.NullPointer)
		{
			Debug.Log($"{Name}: heap이 모자라 구슬을 만들지 못함");
			return;
		}
		orbs.Refresh();
		Effects(task)?.RaiseFired(Id, orbs.OrbPosition(count, count + 1), Vector2.zero);
	}
}

/// <summary>
/// 구슬을 쓰는 스킬의 기반. 구슬이 하나도 없으면 NoManaOrbError로 끝난다.
/// <para>
/// - 쓸 수 있는 구슬: 구슬 생성이 끝나 숨은 시스템 전역에 있는 것 (GetGlobalPointers). 아직 만드는 중인 구슬은 못 쓴다.<br/>
/// - 먼저 만든 구슬부터 쓴다. 쓰면 heap에서 해제(Free)되고, ManaOrbs가 개수를 다시 세어 그림도 사라진다.
/// </para>
/// </summary>
public abstract class OrbSkill : MagicBoltSkill
{
	protected ManaOrbs orbs;

	/// <summary>쓸 수 있는 구슬 수</summary>
	protected int OrbCount(FunctionContext task) => GetGlobalPointers<ManaOrb>(task).Count;

	/// <summary>시작할 때 부른다. 구슬이 없으면 NoManaOrbError로 실패시키고 false.</summary>
	protected bool RequireOrb(FunctionContext task)
	{
		caster = task.Caster;
		orbs = caster.GetComponent<ManaOrbs>();
		if (OrbCount(task) > 0) return true;
		Fail(new NoManaOrbError());
		return false;
	}

	/// <summary>가장 먼저 만든 구슬 하나를 쓴다 (heap에서 해제). position은 그 구슬이 떠 있던 자리. 구슬이 없으면 false.</summary>
	protected bool TryConsumeOrb(FunctionContext task, out Vector2 position)
	{
		position = default;
		List<int> pointers = GetGlobalPointers<ManaOrb>(task);
		if (pointers.Count == 0) return false;
		position = orbs != null ? orbs.OrbPosition(0, orbs.Count) : StaffTip(caster);
		return Free(task, pointers[0]);
	}
}

/// <summary>
/// 구슬 발사. 구슬 하나를 써서 고른 대상 쪽으로 쏜다 (단일, 구슬 1개 소비). 매직 미사일보다 세다 (표 배율).
/// 구슬이 떠 있던 자리에서 나간다. 대상 값이 없으면 바라보는 방향.
/// </summary>
public class PyOrbShot : OrbSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbShot;

	protected override void OnSkillStart(FunctionContext task)
	{
		if (!RequireOrb(task)) return;
		Monster target = TargetArgument();
		FaceTarget(target);
		if (!TryConsumeOrb(task, out Vector2 from)) return;
		Vector2 dir = target != null ? BodyCenter(target.gameObject) - from : new Vector2(FacingOf(caster), 0f);
		LaunchBolt(task, from, dir);
	}
}

/// <summary>
/// 구슬 난사. 가진 구슬을 모두 써서, 고른 대상들에게 차례로(StaggerTicks 간격) 하나씩 쏜다 (여러 명 선택, 구슬 전부 소비).
/// <para>
/// - 대상은 targets(Target 배열). 고른 순서대로 구슬 하나씩. 구슬이 대상보다 많으면 처음 대상부터 다시 돈다.<br/>
/// - 쏘기 전에 죽은 대상은 건너뛴다. 살아 있는 대상이 하나도 없으면 남은 구슬은 쓰지 않는다.<br/>
/// - 구슬마다 처음 닿은 몬스터에게 공격력 × 표 배율.
/// </para>
/// </summary>
public class PyOrbBarrage : OrbSkill
{
	/// <summary>구슬을 하나씩 쏘는 간격 (틱, 임시)</summary>
	public const int StaggerTicks = 3;

	public override string Id => PredefinedFunctionIds.PyOrbBarrage;

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	List<Monster> targets;
	int remaining;
	int launched;

	protected override void OnSkillStart(FunctionContext task)
	{
		targets = new List<Monster>();
		remaining = 0;
		launched = 0;
		if (!RequireOrb(task)) return;
		if (!TryGetArgument("targets", out RuntimeValue value)) return;
		foreach (Monster bug in value.AsBugs())
		{
			if (bug != null && !bug.IsDead && !targets.Contains(bug)) targets.Add(bug);
		}
		if (targets.Count > 0) remaining = OrbCount(task);
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (remaining > 0 && (tick - 1) % StaggerTicks == 0) LaunchNext(task);
		base.OnTick(task, tick);
	}

	void LaunchNext(FunctionContext task)
	{
		// 고른 순서대로 돌며 살아 있는 다음 대상을 찾는다
		Monster target = null;
		for (int i = 0; i < targets.Count && target == null; i++)
		{
			Monster candidate = targets[(launched + i) % targets.Count];
			if (candidate != null && !candidate.IsDead) target = candidate;
		}
		if (target == null || !TryConsumeOrb(task, out Vector2 from))
		{
			remaining = 0;
			return;
		}
		remaining--;
		launched++;
		LaunchBolt(task, from, BodyCenter(target.gameObject) - from);
	}
}

/// <summary>
/// 구슬 폭발. 가진 구슬을 모두 터뜨려 캐릭터 둘레의 몬스터를 모두 맞힌다 (범위, 구슬 전부 소비).
/// 표의 Area Radius 안의 몬스터에게 공격력 × 표 배율 × 터뜨린 구슬 수.
/// </summary>
public class PyOrbBurst : OrbSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbBurst;

	protected override void OnSkillStart(FunctionContext task)
	{
		if (!RequireOrb(task)) return;
		int burst = 0;
		while (burst < ManaOrbs.MaxCount && TryConsumeOrb(task, out _)) burst++;
		if (burst == 0) return;

		Vector2 center = BodyCenter(caster);
		Effects(task)?.RaiseFired(Id, center, Vector2.zero);
		foreach (Monster monster in MonstersInCircle(caster, center, Radius)) Deal(monster, BodyCenter(monster.gameObject), burst);
	}
}

/// <summary>
/// 구슬 보호막. 구슬 하나를 써서 ShieldSeconds(임시 10초) 동안 다음 피해 한 번을 막는 보호막을 두른다 (대상 없음, 구슬 1개 소비).
/// 보호막은 OrbShield가 맡는다. 이미 보호막이 있으면 시간만 새로 시작한다.
/// </summary>
public class PyOrbShield : OrbSkill
{
	/// <summary>보호막이 유지되는 시간 (초, 임시)</summary>
	public const float ShieldSeconds = 10f;

	public override string Id => PredefinedFunctionIds.PyOrbShield;

	protected override void OnSkillStart(FunctionContext task)
	{
		if (!RequireOrb(task)) return;
		OrbShield shield = caster.GetComponent<OrbShield>();
		if (shield == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 OrbShield가 없습니다.");
			return;
		}
		if (!TryConsumeOrb(task, out _)) return;
		shield.Activate(ShieldSeconds);
		Effects(task)?.RaiseFired(Id, BodyCenter(caster), Vector2.zero);
	}
}
