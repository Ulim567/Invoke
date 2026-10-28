using UnityEngine;

/// <summary>
/// 실행기의 작업이 시작하고 끝날 때 콘솔에 로그를 찍는 디버그용 컴포넌트.<br/>
/// 실행기와 같은 오브젝트에 붙인다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class FunctionExecutorLogger : MonoBehaviour
{
	FunctionExecutor executor;
	ProcessMemory memory;

	void Awake()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
	}

	void OnEnable()
	{
		executor.TaskStarted += LogStarted;
		executor.TaskEnded += LogEnded;
		executor.CastRejected += LogRejected;
	}

	void OnDisable()
	{
		executor.TaskStarted -= LogStarted;
		executor.TaskEnded -= LogEnded;
		executor.CastRejected -= LogRejected;
	}

	void LogStarted(FunctionTask task)
	{
		Debug.Log($"[틱 {executor.CurrentTick}] {task.Caster.name}: {task.Name} 시작");
	}

	void LogEnded(FunctionTask task)
	{
		// 실패했으면 EndReason은 Failed뿐이므로, 어떤 실패였는지 함께 찍는다 (예: Failed: Overflow, Failed: SkillFailed - TestAttackFunction: 맨손)
		string reason = task.EndReason.ToString();
		if (task.IsFailed) reason += $": {task.Failure}";
		if (task.Error != null) reason += $" - {task.Error.SourceName}: {task.Error.Message}";
		// 끝난 작업을 모두 진행한 뒤에 불리므로, 이 틱이 끝난 시점의 스택 사용량이 찍힌다
		// 슬롯에서 시전한 작업이면, 방금 시작된 쿨타임도 찍는다
		string cooldown = task.CooldownSource != null ? $" / 쿨타임 {executor.GetRemainingCooldownTicks(task.CooldownSource)}틱" : "";
		Debug.Log($"[틱 {executor.CurrentTick}] {task.Caster.name}: {task.Name} 끝 ({reason}) / 스택 {memory.StackUsed}/{memory.Capacity}{cooldown}");
	}

	void LogRejected(FunctionBlock function, FunctionFailure failure)
	{
		// 거부는 게임 속 실패이므로 오류가 아니라 일반 로그로 찍는다
		Debug.Log($"[틱 {executor.CurrentTick}] {name}: {function.Name} 시전 거부 ({failure}) / 남은 칸 {memory.Free}, 프레임 {function.FrameSize} / 남은 쿨타임 {executor.GetRemainingCooldownTicks(function)}틱");
	}
}
