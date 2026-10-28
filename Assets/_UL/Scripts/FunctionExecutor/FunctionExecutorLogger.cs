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
	}

	void OnDisable()
	{
		executor.TaskStarted -= LogStarted;
		executor.TaskEnded -= LogEnded;
	}

	void LogStarted(FunctionTask task)
	{
		Debug.Log($"[틱 {executor.CurrentTick}] {task.Caster.name}: {task.Name} 시작");
	}

	void LogEnded(FunctionTask task)
	{
		// 실패했으면 EndReason은 Failed뿐이므로, 어떤 실패였는지 함께 찍는다 (예: Failed: Overflow)
		string reason = task.IsFailed ? $"{task.EndReason}: {task.Failure}" : task.EndReason.ToString();
		// 끝난 작업을 모두 진행한 뒤에 불리므로, 이 틱이 끝난 시점의 스택 사용량이 찍힌다
		Debug.Log($"[틱 {executor.CurrentTick}] {task.Caster.name}: {task.Name} 끝 ({reason}) / 스택 {memory.StackUsed}/{memory.Capacity}");
	}
}
