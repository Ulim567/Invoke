using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 수치를 SkillTable 표에서 읽는 사전 정의 함수(C 스킬, 공통 이동). 스킬 클래스는 이 클래스를 상속하고 Id만 정한다.
/// <para>
/// - 이름, 틱 수, 쿨타임, 지역 변수 수: 표에서 읽는다. 표에 없으면 이름은 ID, 틱 수는 1.<br/>
/// - 파라미터: 표의 대상 지정 방식(SkillTargeting)으로 정한다.<br/>
/// - 시작할 때 필요 조건을 검사한다. 맨손인데 맨손으로 못 쓰는 스킬이면 UnarmedError, 소환검이 필요한데 없으면 NoSummonedSwordError로 끝낸다.<br/>
/// - 효과(판정, 데미지)는 각 스킬 이슈에서 OnSkillStart, OnTick, OnEnd, OnCancel을 덮어써서 만든다.
/// </para>
/// <para>
/// 맨손: 시전자의 PhysicalSword를 읽는다. 없으면 TempSwordState(이우림, 테스트용)를 읽는다.<br/>
/// 소환검 개수: 시전자의 SummonedSwords에서 사용 가능한 개수를 읽는다. 없으면 TempSwordState를 읽는다.<br/>
/// 둘 다 없으면 검을 들고 있고 소환검도 있는 것으로 본다.
/// </para>
/// </summary>
public abstract class DataDrivenSkill : PredefinedFunction
{
	/// <summary>이 스킬의 수치. 표에 없으면 null.</summary>
	protected SkillData Data => SkillTable.TryGet(Id, out SkillData data) ? data : null;

	public override string Name => Data != null ? Data.DisplayName : Id;
	public override int TickCount => Data != null ? Data.TickCount : 1;
	public override int CooldownTicks => Data != null ? Data.CooldownTicks : 0;
	public override int LocalVariableCount => Data != null ? Data.LocalVariableCount : 0;
	public override IReadOnlyList<ParameterData> Parameters
	{
		get
		{
			IReadOnlyList<ParameterData> byTargeting = ParametersFor(Data != null ? Data.Targeting : SkillTargeting.None);
			if (!TakesSwordPointer) return byTargeting;
			// 소환검 하나를 쓰는 스킬: 대상 다음에 쓸 소환검 포인터 sword (#220). 비워 두면 스킬이 알아서 고른다
			List<ParameterData> list = new List<ParameterData>(byTargeting) { new ParameterData("sword", SwordPointerType()) };
			return list;
		}
	}

	/// <summary>
	/// 소환검 포인터(Sword*) 파라미터 sword를 받는 스킬이면 true. 소환검 찌르기·소환검 던지기·해제가 덮어쓴다.<br/>
	/// 블록코딩에서 Sword* s = SummonSword(); SummonedThrow(target, s); 처럼 넘기면 그 검을 쓴다. 안 넘기면 지금처럼 알아서 고른다.
	/// </summary>
	protected virtual bool TakesSwordPointer => false;

	/// <summary>소환검 포인터 타입 Sword* (PointerTo(StructOf("Sword"))). 부를 때마다 새로 만든다</summary>
	public static VariableType SwordPointerType() => VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	/// <summary>
	/// sword 파라미터로 넘긴 소환검을 꺼낸다. 넘기지 않았으면 given = false, 결과 false (스킬이 알아서 고른다).<br/>
	/// 넘겼는데 쓸 수 없는 포인터(NULL, 해제됨, 누수, 다른 객체)면 InvalidPointerError로 작업이 실패하고 given = true, 결과 false.
	/// </summary>
	protected bool TryGetSwordArgument(FunctionContext task, out SummonedSword sword, out bool given)
	{
		sword = null;
		given = TryGetArgument("sword", out RuntimeValue value);
		if (!given) return false;
		return TryGetHeapObject(task, value.AsPointer(), out sword);
	}

	/// <summary>
	/// 소환검 하나를 쓰는 스킬이 쓸 검을 고른다 (#220). sword로 넘겼으면 그 검(곁에 있는 Ready 상태여야 함), 안 넘겼으면 가장 먼저 소환한 사용 가능한 검.<br/>
	/// 넘긴 검이 Ready가 아니면 false (에러 아님, 아무것도 안 하고 끝남). 잘못된 포인터면 InvalidPointerError로 작업이 끝나고 false.
	/// </summary>
	protected bool TryPickSword(FunctionContext task, SummonedSwords swords, out SummonedSword sword)
	{
		if (TryGetSwordArgument(task, out sword, out bool given))
			return sword != null && sword.State == SummonedSwordState.Ready;
		sword = null;
		if (given) return false;
		return swords.TryGetFirstReady(out sword);
	}

	// 부를 때마다 새로 만든다. VariableType은 값을 바꿀 수 있어서 함께 쓰면 한 곳에서 바꾼 값이 퍼지기 때문 (PredefinedFunction.ReturnType과 같은 이유)
	static IReadOnlyList<ParameterData> ParametersFor(SkillTargeting targeting)
	{
		switch (targeting)
		{
			case SkillTargeting.Single:
			case SkillTargeting.SingleMultiHit:
			case SkillTargeting.Pierce:
				return new[] { new ParameterData("target", new VariableType(VariableTypeKind.Target)) };
			case SkillTargeting.MultiSelect:
				// 실행기는 list 대신 array로 정했다 (#191). 값은 RuntimeValue.FromBugs, 읽기는 AsBugs()
				return new[] { new ParameterData("targets", VariableType.ArrayOf(new VariableType(VariableTypeKind.Target))) };
			default:
				return Array.Empty<ParameterData>();
		}
	}

	// 필요 조건을 통과해 시작했는지. 시작하지 못한 스킬은 Ended를 내지 않는다
	bool started;

	// 필요 조건 검사와 연출 event(SkillEffectEvents)를 빼먹지 않도록 OnStart·OnEnd·OnCancel은 여기서 닫고,
	// 스킬은 OnSkillStart·OnSkillEnd·OnSkillCancel을 덮어쓴다
	protected sealed override void OnStart(FunctionContext task)
	{
		if (!CheckRequirements(task)) return;
		started = true;
		Debug.Log($"[Skill] {task.Caster.name}: {Name} 시전 ({TickCount}틱)");
		Effects(task)?.RaiseStarted(Id);
		OnSkillStart(task);
	}

	protected sealed override void OnEnd(FunctionContext task)
	{
		OnSkillEnd(task);
		if (started) Effects(task)?.RaiseEnded(Id, true);
	}

	protected sealed override void OnCancel(FunctionContext task)
	{
		OnSkillCancel(task);
		if (started) Effects(task)?.RaiseEnded(Id, false);
	}

	/// <summary>
	/// 필요 조건을 통과한 뒤 첫 틱에 한 번 불린다. 효과를 만들 때 OnStart 대신 덮어쓴다.
	/// </summary>
	protected virtual void OnSkillStart(FunctionContext task) { }

	/// <summary>끝까지 실행했을 때 한 번 불린다. OnEnd 대신 덮어쓴다.</summary>
	protected virtual void OnSkillEnd(FunctionContext task) { }

	/// <summary>도중에 끊겼거나 실패했을 때 한 번 불린다 (실행 중단 공격, 슬롯 취소, Overflow 재시작, Fail). OnCancel 대신 덮어쓴다. heap은 건드리지 않는다.</summary>
	protected virtual void OnSkillCancel(FunctionContext task) { }

	/// <summary>시전자의 연출 event. 없으면 null (연출 없이 동작).</summary>
	protected static SkillEffectEvents Effects(FunctionContext task) => task.Caster != null ? task.Caster.GetComponent<SkillEffectEvents>() : null;

	/// <summary>
	/// 소환검이 필요한 스킬(needsSummonedSword)이 쓸 수 있는 소환검 수. 0이면 NoSummonedSwordError로 끝난다.<br/>
	/// 기본: 시전자의 SummonedSwords.ReadyCount(플레이어 곁에 있는 소환검). 없으면 TempSwordState(이우림, 테스트용), 그것도 없으면 있는 것으로 본다.<br/>
	/// 해제처럼 던진 소환검도 쓰는 스킬은 덮어쓴다.
	/// </summary>
	protected virtual int CountUsableSummonedSwords(FunctionContext task)
	{
		SummonedSwords summoned = task.Caster.GetComponent<SummonedSwords>();
		if (summoned != null) return summoned.ReadyCount;
		TempSwordState temp = task.Caster.GetComponent<TempSwordState>();
		return temp != null ? temp.SummonedSwordCount : int.MaxValue;
	}

	bool CheckRequirements(FunctionContext task)
	{
		SkillData data = Data;
		if (data == null) return true;

		PhysicalSword physical = task.Caster.GetComponent<PhysicalSword>();
		TempSwordState temp = task.Caster.GetComponent<TempSwordState>();

		bool unarmed = physical != null ? physical.IsUnarmed : temp != null && temp.IsUnarmed;
		if (!data.UsableUnarmed && unarmed)
		{
			Fail(new UnarmedError());
			return false;
		}
		if (data.NeedsSummonedSword && CountUsableSummonedSwords(task) <= 0)
		{
			Fail(new NoSummonedSwordError());
			return false;
		}
		return true;
	}
}
