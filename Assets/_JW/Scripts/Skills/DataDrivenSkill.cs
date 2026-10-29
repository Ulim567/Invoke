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
/// 맨손·소환검 상태는 물리 검·소환검 관리가 생기기 전까지 TempSwordState(이우림, 테스트용)를 읽는다.
/// 시전자에게 TempSwordState가 없으면 검을 들고 있고 소환검도 있는 것으로 본다.
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
	public override IReadOnlyList<ParameterData> Parameters => ParametersFor(Data != null ? Data.Targeting : SkillTargeting.None);

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
				return new[] { new ParameterData("targets", VariableType.ListOf(new VariableType(VariableTypeKind.Target))) };
			default:
				return Array.Empty<ParameterData>();
		}
	}

	// 필요 조건 검사를 빼먹지 않도록 OnStart는 여기서 닫고, 스킬은 OnSkillStart를 덮어쓴다
	protected sealed override void OnStart(FunctionTask task)
	{
		if (!CheckRequirements(task)) return;
		Debug.Log($"[Skill] {task.Caster.name}: {Name} 시전 ({TickCount}틱)");
		OnSkillStart(task);
	}

	/// <summary>
	/// 필요 조건을 통과한 뒤 첫 틱에 한 번 불린다. 효과를 만들 때 OnStart 대신 덮어쓴다.
	/// </summary>
	protected virtual void OnSkillStart(FunctionTask task) { }

	bool CheckRequirements(FunctionTask task)
	{
		SkillData data = Data;
		if (data == null) return true;

		TempSwordState sword = task.Caster.GetComponent<TempSwordState>();
		if (sword == null) return true;

		if (!data.UsableUnarmed && sword.IsUnarmed)
		{
			Fail(new UnarmedError());
			return false;
		}
		if (data.NeedsSummonedSword && sword.SummonedSwordCount <= 0)
		{
			Fail(new NoSummonedSwordError());
			return false;
		}
		return true;
	}
}
