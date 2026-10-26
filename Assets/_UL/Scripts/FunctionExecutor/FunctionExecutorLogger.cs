using UnityEngine;

/// <summary>
/// 실행기의 작업이 시작하고 끝날 때 콘솔에 로그를 찍는 디버그용 컴포넌트.<br/>
/// 실행기와 같은 오브젝트에 붙인다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class FunctionExecutorLogger : MonoBehaviour
{
	FunctionExecutor executor;

	void Awake()
	{
		executor = GetComponent<FunctionExecutor>();
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
		Debug.Log($"[틱 {executor.CurrentTick}] {task.Caster.name}: {task.Name} 끝 ({task.EndReason})");
	}
}
