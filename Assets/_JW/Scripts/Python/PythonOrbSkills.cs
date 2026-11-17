using System.Collections.Generic;
using UnityEngine;

// 파이썬 마법사의 구슬 계열 스킬. 구슬 생성이 마나 구슬(ManaOrb)을 heap에 만들어 Orb 값으로 돌려주고,
// 플레이어가 그 값을 변수·리스트에 담아 구슬 스킬에 넘긴다 (#532, 이우림이 Python 문법 맞추기에서 고침).
// 구슬 스킬은 구슬을 해제하지 않고 「다 쓴 구슬」로만 바꾼다. heap에서 지우는 일은 가비지 컬렉션이 한다(가리키는 이름이 없을 때).

/// <summary>
/// 구슬 생성. 마나 구슬 하나를 heap에 만들고 Orb 값으로 돌려준다 (대상 없음). 예: orb = orb_create()
/// <para>
/// - 크기는 표의 heap 크기(임시 2칸).<br/>
/// - 구슬이 이미 ManaOrbs.MaxCount(5)개면 만들지 않고 None을 돌려준다(C의 소환검 5개일 때 NULL과 같음). 다 쓴 구슬도 센다(C의 누수 소환검과 같음).<br/>
/// - heap이 모자라면 MemoryError(OutOfMemory)로 끝난다(리스트 append와 같음).<br/>
/// - 돌려준 구슬을 변수·리스트에 담지 않으면 그 statement가 끝날 때 가비지 컬렉션이 해제한다. 숨은 시스템 전역으로 옮기지 않는다.
/// </para>
/// </summary>
public class PyOrbCreate : MagicSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbCreate;

	public override VariableType ReturnType => PythonObjects.OrbType;

	// 만든 구슬은 플레이어가 담는다. 숨은 시스템 전역에 두면 이름이 없어도 사라지지 않기 때문 (이우림 결정 11/18 (나))
	protected override bool MovesMallocedToSystemGlobals => false;

	protected override void OnSkillStart(FunctionContext task)
	{
		caster = task.Caster;
		ManaOrbs orbs = caster.GetComponent<ManaOrbs>();
		if (orbs == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 ManaOrbs가 없습니다.");
			Return(RuntimeValue.None);
			return;
		}
		// 구슬이 이미 5개면 만들지 않는다. 다 쓴 구슬도 센다: 변수·리스트가 다 쓴 구슬을 놓아야 가비지 컬렉션이 해제해 자리가 돌아온다
		if (!orbs.CanCreate)
		{
			Return(RuntimeValue.None);
			return;
		}

		int pointer = Malloc(task, new ManaOrb(), Data != null ? Data.HeapSize : 2);
		if (pointer == HeapStorage.NullPointer)
		{
			FailWith(FunctionFailure.OutOfMemory);
			return;
		}
		Return(RuntimeValue.FromOrb(pointer));
		orbs.Refresh();
		Effects(task)?.RaiseFired(Id, orbs.OrbPosition(orbs.IndexOf(pointer), orbs.TotalCount), Vector2.zero);
	}
}

/// <summary>
/// 구슬을 쓰는 스킬의 기반 (#532).
/// <para>
/// - 쓸 구슬: 파라미터로 넘긴 Orb 값(orb) 또는 리스트(orbs). 파라미터 값이 없으면(슬롯에서 바로 시전) 쓸 수 있는 구슬 중 먼저 만든 것부터 고른다.<br/>
/// - 구슬을 쓰면 「다 쓴 구슬」(ManaOrb.Spend)로 바꾼다. 해제하지 않는다. 쓸 수 있는 개수에서 빠지고, 머리 위와 HUD에 빨갛게 남는다.<br/>
/// - 다 쓴 구슬 하나를 넘기면 UsedManaOrbError, 리스트에 쓸 구슬이 없으면 NoManaOrbError, 리스트에 구슬이 아닌 값이 있으면 TypeError(TypeMismatch).
/// </para>
/// </summary>
public abstract class OrbSkill : MagicBoltSkill
{
	protected ManaOrbs orbs;

	/// <summary>Orb 하나를 받는 파라미터 이름</summary>
	protected const string OrbParameter = "orb";
	/// <summary>Orb 리스트를 받는 파라미터 이름</summary>
	protected const string OrbsParameter = "orbs";

	/// <summary>Orb 리스트 타입. 원소 타입은 적지 않는다(Python 리스트는 아무 값이나 담기 때문). 원소는 스킬이 확인한다</summary>
	protected static VariableType OrbListType => VariableType.ListOf(VariableType.AnyType);

	/// <summary>시작할 때 부른다. 시전자의 ManaOrbs를 찾아 둔다.</summary>
	protected void FindOrbs(FunctionContext task)
	{
		caster = task.Caster;
		orbs = caster.GetComponent<ManaOrbs>();
	}

	/// <summary>
	/// orb 파라미터의 구슬 하나를 고른다. 넘긴 구슬이 다 쓴 구슬이면 UsedManaOrbError로 실패시키고 false.<br/>
	/// 값이 없으면(슬롯에서 바로 시전) 쓸 수 있는 구슬 중 먼저 만든 것. 하나도 없으면 NoManaOrbError로 실패시키고 false.
	/// </summary>
	protected bool TryTakeOrbArgument(FunctionContext task, out int pointer)
	{
		pointer = HeapStorage.NullPointer;
		if (!TryGetArgument(OrbParameter, out RuntimeValue value))
		{
			List<int> usable = orbs != null ? orbs.UsablePointers() : new List<int>();
			if (usable.Count == 0)
			{
				Fail(new NoManaOrbError());
				return false;
			}
			pointer = usable[0];
			return true;
		}

		// 파라미터 타입(Orb) 검사를 지났으므로 구슬 값이다. 가리키는 구슬이 없으면(초기화로 heap이 비워짐) 다 쓴 구슬로 본다
		if (!value.IsOrb || !TryPeekHeapObject(task, value.AsObjectPointer(), out ManaOrb orb) || orb.IsSpent)
		{
			Fail(new UsedManaOrbError());
			return false;
		}
		pointer = value.AsObjectPointer();
		return true;
	}

	/// <summary>
	/// orbs 파라미터(리스트)에서 쓸 수 있는 구슬들을 리스트 순서대로 고른다. 다 쓴 구슬은 건너뛰고, 리스트에서 빼지 않는다.<br/>
	/// 구슬이 아닌 원소가 있으면 TypeError, 쓸 구슬이 하나도 없으면 NoManaOrbError로 실패시키고 false.
	/// 값이 없으면(슬롯에서 바로 시전) 쓸 수 있는 모든 구슬.
	/// </summary>
	protected bool TryTakeOrbList(FunctionContext task, out List<int> pointers)
	{
		pointers = new List<int>();
		if (!TryGetArgument(OrbsParameter, out RuntimeValue value))
		{
			if (orbs != null) pointers.AddRange(orbs.UsablePointers());
		}
		else
		{
			if (!TryGetListElements(task, value, out IReadOnlyList<RuntimeValue> elements))
			{
				FailWith(FunctionFailure.TypeMismatch);
				return false;
			}
			foreach (RuntimeValue element in elements)
			{
				if (!element.IsOrb)
				{
					FailWith(FunctionFailure.TypeMismatch);
					return false;
				}
				int pointer = element.AsObjectPointer();
				// 같은 구슬이 리스트에 두 번 들어 있어도 한 번만 쓴다
				if (TryPeekHeapObject(task, pointer, out ManaOrb orb) && !orb.IsSpent && !pointers.Contains(pointer)) pointers.Add(pointer);
			}
		}

		if (pointers.Count > 0) return true;
		Fail(new NoManaOrbError());
		return false;
	}

	/// <summary>
	/// 구슬 pointer를 다 쓴 구슬로 바꾼다. position은 그 구슬이 떠 있던 자리. 이미 다 쓴 구슬이면 false.
	/// </summary>
	protected bool TrySpendOrb(FunctionContext task, int pointer, out Vector2 position)
	{
		position = default;
		if (!TryPeekHeapObject(task, pointer, out ManaOrb orb) || orb.IsSpent) return false;

		int index = orbs != null ? orbs.IndexOf(pointer) : -1;
		position = orbs != null && index >= 0 ? orbs.OrbPosition(index, orbs.TotalCount) : StaffTip(caster);
		orb.Spend();
		orbs?.Refresh();
		return true;
	}
}

/// <summary>
/// 구슬 발사. orb_shot(target, orb). 구슬 하나를 써서 고른 대상 쪽으로 쏜다. 매직 미사일보다 세다 (표 배율).
/// 구슬이 떠 있던 자리에서 나간다. 대상 값이 없으면 바라보는 방향.
/// </summary>
public class PyOrbShot : OrbSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbShot;

	public override IReadOnlyList<ParameterData> Parameters
	{
		get
		{
			List<ParameterData> list = new List<ParameterData>(base.Parameters) { new ParameterData(OrbParameter, PythonObjects.OrbType) };
			return list;
		}
	}

	protected override void OnSkillStart(FunctionContext task)
	{
		FindOrbs(task);
		if (!TryTakeOrbArgument(task, out int pointer)) return;
		Monster target = TargetArgument();
		FaceTarget(target);
		if (!TrySpendOrb(task, pointer, out Vector2 from)) return;
		Vector2 dir = target != null ? BodyCenter(target.gameObject) - from : new Vector2(FacingOf(caster), 0f);
		LaunchBolt(task, from, dir);
	}
}

/// <summary>
/// 구슬 난사. orb_barrage(targets, orbs). 리스트의 쓸 수 있는 구슬을 모두 써서, 고른 대상들에게 차례로(StaggerTicks 간격) 하나씩 쏜다.
/// <para>
/// - targets: 대상 리스트(슬롯에서 바로 시전하면 고른 대상 array). 고른 순서대로 구슬 하나씩. 구슬이 대상보다 많으면 처음 대상부터 다시 돈다.<br/>
/// - 쏘기 전에 죽은 대상은 건너뛴다. 살아 있는 대상이 하나도 없으면 남은 구슬은 쓰지 않는다.<br/>
/// - 구슬마다 처음 닿은 몬스터에게 공격력 × 표 배율.
/// </para>
/// </summary>
public class PyOrbBarrage : OrbSkill
{
	/// <summary>구슬을 하나씩 쏘는 간격 (틱, 임시)</summary>
	public const int StaggerTicks = 3;

	public override string Id => PredefinedFunctionIds.PyOrbBarrage;

	public override IReadOnlyList<ParameterData> Parameters => new[]
	{
		// 원소 타입을 Target으로 적는 이유: 슬롯 입력(SlotCaster)이 이 파라미터를 보고 여러 명 선택으로 시전하기 때문
		new ParameterData("targets", VariableType.ListOf(VariableType.TargetOf(null))),
		new ParameterData(OrbsParameter, OrbListType),
	};

	// MemberwiseClone 복사본끼리 공유하지 않게 시작할 때 새로 만든다
	List<Monster> targets;
	List<int> orbPointers;
	int launched;

	protected override void OnSkillStart(FunctionContext task)
	{
		targets = new List<Monster>();
		orbPointers = new List<int>();
		launched = 0;
		FindOrbs(task);
		if (!TryTakeOrbList(task, out List<int> pointers)) return;
		if (!TryGetArgument("targets", out RuntimeValue value)) return;
		foreach (Monster bug in BugsOf(task, value))
		{
			if (bug != null && !bug.IsDead && !targets.Contains(bug)) targets.Add(bug);
		}
		if (targets.Count > 0) orbPointers = pointers;
	}

	// 대상 리스트(Python)와 대상 array(슬롯 여러 명 선택)를 함께 읽는다. 대상이 아닌 원소는 건너뛴다
	List<Monster> BugsOf(FunctionContext task, RuntimeValue value)
	{
		if (value.Type?.Kind == VariableTypeKind.Array) return value.AsBugs();

		List<Monster> bugs = new List<Monster>();
		if (!TryGetListElements(task, value, out IReadOnlyList<RuntimeValue> elements)) return bugs;
		foreach (RuntimeValue element in elements)
			if (element.Type?.Kind == VariableTypeKind.Target && element.Type.TargetKind == TargetKind.Bug) bugs.Add(element.AsBug());
		return bugs;
	}

	protected override void OnTick(FunctionContext task, int tick)
	{
		if (launched < orbPointers.Count && (tick - 1) % StaggerTicks == 0) LaunchNext(task);
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
		// 쏘는 사이에 다른 작업이 그 구슬을 써 버렸으면 그 구슬은 건너뛴다
		if (target == null)
		{
			launched = orbPointers.Count;
			return;
		}
		int pointer = orbPointers[launched];
		launched++;
		if (!TrySpendOrb(task, pointer, out Vector2 from)) return;
		LaunchBolt(task, from, BodyCenter(target.gameObject) - from);
	}
}

/// <summary>
/// 구슬 폭발. orb_burst(orbs). 리스트의 쓸 수 있는 구슬을 모두 터뜨려 캐릭터 둘레의 몬스터를 모두 맞힌다 (범위).
/// 표의 Area Radius 안의 몬스터에게 공격력 × 표 배율 × 터뜨린 구슬 수.
/// </summary>
public class PyOrbBurst : OrbSkill
{
	public override string Id => PredefinedFunctionIds.PyOrbBurst;

	public override IReadOnlyList<ParameterData> Parameters => new[] { new ParameterData(OrbsParameter, OrbListType) };

	protected override void OnSkillStart(FunctionContext task)
	{
		FindOrbs(task);
		if (!TryTakeOrbList(task, out List<int> pointers)) return;
		int burst = 0;
		foreach (int pointer in pointers)
			if (TrySpendOrb(task, pointer, out _)) burst++;
		if (burst == 0) return;

		Vector2 center = BodyCenter(caster);
		Effects(task)?.RaiseFired(Id, center, Vector2.zero);
		foreach (Monster monster in MonstersInCircle(caster, center, Radius)) Deal(monster, BodyCenter(monster.gameObject), burst);
	}
}

/// <summary>
/// 구슬 보호막. orb_shield(orb). 구슬 하나를 써서 ShieldSeconds(임시 10초) 동안 다음 피해 한 번을 막는 보호막을 두른다 (대상 없음).
/// 보호막은 OrbShield가 맡는다. 이미 보호막이 있으면 시간만 새로 시작한다.
/// </summary>
public class PyOrbShield : OrbSkill
{
	/// <summary>보호막이 유지되는 시간 (초, 임시)</summary>
	public const float ShieldSeconds = 10f;

	public override string Id => PredefinedFunctionIds.PyOrbShield;

	public override IReadOnlyList<ParameterData> Parameters => new[] { new ParameterData(OrbParameter, PythonObjects.OrbType) };

	protected override void OnSkillStart(FunctionContext task)
	{
		FindOrbs(task);
		OrbShield shield = caster.GetComponent<OrbShield>();
		if (shield == null)
		{
			Debug.LogError($"{Name}: {caster.name}에 OrbShield가 없습니다.");
			return;
		}
		if (!TryTakeOrbArgument(task, out int pointer)) return;
		if (!TrySpendOrb(task, pointer, out _)) return;
		shield.Activate(ShieldSeconds);
		Effects(task)?.RaiseFired(Id, BodyCenter(caster), Vector2.zero);
	}
}

/// <summary>
/// 구슬 확인 함수 (#532). is_valid_orb(orb). 넘긴 값이 아직 쓰지 않은 마나 구슬이면 True, 아니면 False.
/// <para>
/// - False: 다 쓴 구슬, None, 구슬이 아닌 값, 값을 넘기지 않음(슬롯에서 바로 시전).<br/>
/// - 다 쓴 구슬은 None과 비교해서는 알 수 없다(구슬 값은 그대로 남기 때문). 그래서 이 함수로 확인한다. is_valid_target과 같은 모양이다.<br/>
/// - 스킬 트리 노드가 없다. Python 캐릭터에게 처음부터 보인다 (이우림 결정 11/18).
/// </para>
/// </summary>
public class PyIsValidOrb : PredefinedFunction
{
	static readonly IReadOnlyList<ParameterData> OrbParameter =
		new[] { new ParameterData("orb", VariableType.AnyType) };

	public override string Id => PredefinedFunctionIds.PyIsValidOrb;
	public override string Name => "IsValidOrb";
	public override int TickCount => 1;
	public override IReadOnlyList<ParameterData> Parameters => OrbParameter;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Bool);

	protected override void OnTick(FunctionContext context, int tick)
	{
		bool valid = TryGetArgument("orb", out RuntimeValue value) && value.IsOrb
			&& TryPeekHeapObject(context, value.AsObjectPointer(), out ManaOrb orb) && !orb.IsSpent;
		Return(RuntimeValue.FromBool(valid));
	}
}
