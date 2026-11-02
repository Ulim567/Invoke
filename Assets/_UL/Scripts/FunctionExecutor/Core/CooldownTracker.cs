using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 하나의 쿨타임 기록. 스킬(사전 정의 함수의 고정 ID)마다 실행 중인 수와 쿨타임이 끝나는 틱을 가지고,
/// 슬롯에서 시전한 함수(원본)마다 실행 중인 작업을 가진다.
/// <para>
/// 사용법<br/>
/// - FunctionExecutor가 만들어 가지고 있다. 다른 스크립트는 FunctionExecutor.GetRemainingCooldownTicks, IsRunning으로 읽는다.<br/>
/// - 시전 전 검사(CooldownCondition)는 이 클래스를 직접 본다.<br/>
/// - 스킬의 시작과 끝은 PredefinedFunction이 실행 문맥을 거쳐 알린다. 슬롯 직접 시전, 함수 안의 호출, 비동기 시전이 모두 같다.
/// </para>
/// </summary>
// 실행기와 따로 두는 이유: 작업 진행(틱, 작업 목록)과 쿨타임 규칙을 나누어, 쿨타임 규칙을 바꿀 때 이 클래스만 고치기 위함
// 컴포넌트가 아닌 일반 클래스로 둔다. 프리팹에 따로 붙이지 않아도 되게 하기 위함
public class CooldownTracker
{
	readonly PlayerStats stats;
	// 실행기를 참조하지 않고 지금 틱만 읽는다. 쿨타임이 실행기의 다른 기능에 기대지 않게 하기 위함
	readonly Func<int> getCurrentTick;

	// 스킬 고정 ID마다 쿨타임이 끝나는 틱. 쿨타임 값은 이 클래스만 바꾼다
	// 함수가 아니라 스킬에 쿨타임을 두는 이유: 반복문으로 같은 스킬을 여러 번 실행해도 스킬마다 쿨타임이 걸리게 하기 위함
	readonly Dictionary<string, int> cooldownEndTicks = new Dictionary<string, int>();
	// 스킬 고정 ID마다 지금 실행 중인 수. 비동기 시전이 같은 틱에 겹치면 같은 스킬이 동시에 둘 이상 실행될 수 있어 개수로 센다
	readonly Dictionary<string, int> runningSkillCounts = new Dictionary<string, int>();
	// 슬롯에서 시전한 원본마다 지금 실행 중인 작업. 같은 원본을 겹쳐 시전하지 못하게 막으므로 원본 하나에 작업 하나다
	// 사용자 정의 함수는 자기 쿨타임이 없으므로, 「실행 중이면 다시 시전할 수 없음」 규칙은 이 기록만으로 지킨다
	readonly Dictionary<FunctionBlock, FunctionTask> runningTasks = new Dictionary<FunctionBlock, FunctionTask>();

	/// <summary>
	/// 스킬의 쿨타임이 시작되거나 늘어났을 때 불린다 (스킬 고정 ID, 쿨타임이 끝나는 틱).
	/// </summary>
	public event Action<string, int> CooldownStarted;

	public CooldownTracker(PlayerStats stats, Func<int> getCurrentTick)
	{
		this.stats = stats;
		this.getCurrentTick = getCurrentTick;
	}

	/// <summary>
	/// 고정 ID가 skillId인 스킬의 남은 쿨타임(틱 수). 쿨타임 중이 아니면 0이다.
	/// </summary>
	public int GetRemainingTicks(string skillId)
	{
		if (!cooldownEndTicks.TryGetValue(skillId, out int endTick)) return 0;
		return Mathf.Max(0, endTick - getCurrentTick());
	}

	/// <summary>
	/// 슬롯에서 시전하는 function의 남은 쿨타임(틱 수). 스킬이면 그 고정 ID의 쿨타임이고, 사용자 정의 함수는 자기 쿨타임이 없으므로 0이다.
	/// </summary>
	public int GetRemainingTicks(FunctionBlock function)
	{
		return function is PredefinedFunction skill ? GetRemainingTicks(skill.Id) : 0;
	}

	/// <summary>
	/// 고정 ID가 skillId인 스킬이 어느 작업에서든 실행 중이면 true.
	/// </summary>
	public bool IsSkillRunning(string skillId)
	{
		return runningSkillCounts.TryGetValue(skillId, out int count) && count > 0;
	}

	/// <summary>
	/// 고정 ID가 skillId인 스킬을 지금 시작할 수 있으면 true. 어느 작업에서도 실행 중이 아니고 쿨타임 중도 아니어야 한다.<br/>
	/// 함수 안의 스킬은 false인 동안 실패하지 않고 기다린다.
	/// </summary>
	public bool CanUseSkill(string skillId)
	{
		return !IsSkillRunning(skillId) && GetRemainingTicks(skillId) <= 0;
	}

	/// <summary>
	/// 슬롯에서 시전한 원본 function의 작업이 아직 실행 중이거나 다음 틱을 기다리고 있으면 true.<br/>
	/// 스킬이면 같은 고정 ID의 스킬이 다른 함수 안에서 실행 중일 때도 true다.
	/// </summary>
	// 작업이 끝났는지는 등록한 작업의 IsEnded로 본다. 끝난 순간부터 false가 된다
	public bool IsRunning(FunctionBlock function)
	{
		if (runningTasks.TryGetValue(function, out FunctionTask task) && !task.IsEnded) return true;
		return function is PredefinedFunction skill && IsSkillRunning(skill.Id);
	}

	// 슬롯에서 시전한 작업을 실행 중으로 등록한다. 실행기가 Cast에서 작업을 만든 뒤 부른다
	internal void Register(FunctionTask task)
	{
		runningTasks[task.CastSource] = task;
	}

	// 끝난 작업의 실행 중 등록을 지운다. 끝난 작업을 계속 들고 있지 않기 위함
	// 실행기가 끝난 작업을 목록에서 뺄 때 부른다
	internal void Unregister(FunctionTask task)
	{
		FunctionBlock source = task.CastSource;
		// 함수 안의 비동기 시전으로 만든 작업은 슬롯에서 시전한 것이 아니므로 등록되어 있지 않다
		if (source == null) return;

		if (runningTasks.TryGetValue(source, out FunctionTask running) && running == task) runningTasks.Remove(source);
	}

	// 스킬이 프레임을 받아 실행을 시작했다. PredefinedFunction이 부른다
	internal void SkillStarted(PredefinedFunction skill)
	{
		runningSkillCounts.TryGetValue(skill.Id, out int count);
		runningSkillCounts[skill.Id] = count + 1;
	}

	// 스킬이 끝났다. 쿨타임은 이 스킬이 끝난 순간부터 흐른다. 정상 종료, 실패, 실행 중단 모두 같다
	// 실패·실행 중단에도 쿨타임 전체를 거는 이유: 스킬을 일부러 끊어 쿨타임을 줄이지 못하게 하기 위함
	// 쿨타임 감소는 끝나는 순간의 능력치로 한 번 적용한다. PredefinedFunction이 부른다
	internal void SkillEnded(PredefinedFunction skill)
	{
		string id = skill.Id;
		if (runningSkillCounts.TryGetValue(id, out int count))
		{
			if (count <= 1) runningSkillCounts.Remove(id);
			else runningSkillCounts[id] = count - 1;
		}

		int cooldown = skill.CooldownTicks;
		if (cooldown <= 0) return;

		// 공격 속도(ApplyAttackSpeed)와 같게 반올림해서 틱 수로 맞춘다
		cooldown = Mathf.RoundToInt(stats.ApplyCooldownReduction(cooldown));
		int endTick = getCurrentTick() + cooldown;
		// 같은 스킬이 겹쳐 실행되었으면 더 늦게 끝나는 쿨타임을 남긴다. 먼저 끝난 쪽의 기록이 쿨타임을 줄이지 않게 하기 위함
		if (cooldownEndTicks.TryGetValue(id, out int existing) && existing > endTick) return;
		cooldownEndTicks[id] = endTick;
		CooldownStarted?.Invoke(id, endTick);
	}
}
