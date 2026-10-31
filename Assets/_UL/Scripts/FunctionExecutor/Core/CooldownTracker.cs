using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 하나의 쿨타임 기록. 슬롯에서 시전한 함수(원본)마다 실행 중인 작업과 쿨타임이 끝나는 틱을 가진다.
/// <para>
/// 사용법<br/>
/// - FunctionExecutor가 만들어 가지고 있다. 다른 스크립트는 FunctionExecutor.GetRemainingCooldownTicks, IsRunning으로 읽는다.<br/>
/// - 시전 전 검사(CooldownCondition)는 이 클래스를 직접 본다.
/// </para>
/// </summary>
// 실행기와 따로 두는 이유: 작업 진행(틱, 작업 목록)과 쿨타임 규칙을 나누어, 쿨타임 규칙을 바꿀 때 이 클래스만 고치기 위함
// 컴포넌트가 아닌 일반 클래스로 둔다. 프리팹에 따로 붙이지 않아도 되게 하기 위함
public class CooldownTracker
{
	readonly PlayerStats stats;
	// 실행기를 참조하지 않고 지금 틱만 읽는다. 쿨타임이 실행기의 다른 기능에 기대지 않게 하기 위함
	readonly Func<int> getCurrentTick;

	// 원본마다 쿨타임이 끝나는 틱. 쿨타임 값은 이 클래스만 바꾼다
	// 원본 객체로 구분하므로, 슬롯은 같은 함수 객체를 계속 넘겨야 쿨타임이 이어진다
	readonly Dictionary<FunctionBlock, int> cooldownEndTicks = new Dictionary<FunctionBlock, int>();
	// 원본마다 지금 실행 중인 작업. 쿨타임 검사가 같은 원본을 겹쳐 시전하지 못하게 막으므로 원본 하나에 작업 하나다
	readonly Dictionary<FunctionBlock, FunctionTask> runningTasks = new Dictionary<FunctionBlock, FunctionTask>();

	public CooldownTracker(PlayerStats stats, Func<int> getCurrentTick)
	{
		this.stats = stats;
		this.getCurrentTick = getCurrentTick;
	}

	/// <summary>
	/// 원본 function의 남은 쿨타임(틱 수). 쿨타임 중이 아니면 0이다.
	/// </summary>
	public int GetRemainingTicks(FunctionBlock function)
	{
		if (!cooldownEndTicks.TryGetValue(function, out int endTick)) return 0;
		return Mathf.Max(0, endTick - getCurrentTick());
	}

	/// <summary>
	/// 원본 function의 작업이 아직 실행 중이거나 다음 틱을 기다리고 있으면 true.
	/// </summary>
	// 작업이 끝났는지는 등록한 작업의 IsEnded로 본다. 끝난 순간부터 false가 되어, 실행기가 작업 목록을 뒤져 확인하던 것과 같다
	public bool IsRunning(FunctionBlock function)
	{
		return runningTasks.TryGetValue(function, out FunctionTask task) && !task.IsEnded;
	}

	// 슬롯에서 시전한 작업을 실행 중으로 등록한다. 실행기가 Cast에서 작업을 만든 뒤 부른다
	internal void Register(FunctionTask task)
	{
		runningTasks[task.CooldownSource] = task;
	}

	// 쿨타임은 함수가 끝난 순간부터 흐른다. 정상 종료, 실패, 실행 중단 모두 같다
	// 쿨타임 감소는 끝나는 순간의 능력치로 한 번 적용한다
	// 실행기가 끝난 작업을 목록에서 뺄 때 부른다
	internal void StartCooldown(FunctionTask task)
	{
		FunctionBlock source = task.CooldownSource;
		// 사용자 정의 함수 안의 비동기 시전으로 만든 작업은 원본이 없다. 그 쿨타임은 바깥 함수의 쿨타임에 이미 더해져 있다
		if (source == null) return;

		// 쿨타임 길이와 상관없이 실행 중 등록은 지운다. 끝난 작업을 계속 들고 있지 않기 위함
		if (runningTasks.TryGetValue(source, out FunctionTask running) && running == task) runningTasks.Remove(source);

		int cooldown = source.CooldownTicks;
		if (cooldown <= 0) return;

		// 공격 속도(ApplyAttackSpeed)와 같게 반올림해서 틱 수로 맞춘다
		cooldown = Mathf.RoundToInt(stats.ApplyCooldownReduction(cooldown));
		cooldownEndTicks[source] = getCurrentTick() + cooldown;
	}
}
