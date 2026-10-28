using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 하나의 실행기(스레드 1개). 시전한 함수마다 작업을 하나 만들고, 틱마다 모든 작업을 한 단계씩 진행한다.
/// <para>
/// 사용법<br/>
/// - 캐릭터에 하나만 붙인다.<br/>
/// - 함수를 시전할 때 Cast(함수)를 부른다. 같은 함수든 다른 함수든 여러 번 부를 수 있다.<br/>
/// - 작업이 진행을 시작하거나 끝날 때 TaskStarted, TaskEnded로 알린다. 로그·이펙트 같은 연출은 이 이벤트를 받아서 한다.
/// </para>
/// <para>
/// 틱 규칙<br/>
/// - 1틱은 FixedUpdate를 fixedUpdatesPerTick번 지난 것이다.<br/>
/// - 같은 틱에서는 먼저 시전한 작업부터 진행한다.<br/>
/// - 시전한 작업은 다음 틱부터 진행한다. 틱 도중에 시전되어도 같다.<br/>
/// - 끝난 작업은 그 틱의 모든 작업을 진행한 뒤에 한꺼번에 뺀다.
/// </para>
/// </summary>
[RequireComponent(typeof(ProcessMemory))]
public class FunctionExecutor : MonoBehaviour
{
	[SerializeField, Min(1), Tooltip("1틱이 FixedUpdate 몇 번인지")]
	int fixedUpdatesPerTick = 1;

	[SerializeField, Tooltip("이 캐릭터 언어의 실패 처리 표")]
	LanguageRule languageRule;

	// 진행 중인 작업. 틱마다 이 목록의 작업만 진행하며, 시전한 순서대로 쌓인다
	readonly List<FunctionTask> tasks = new List<FunctionTask>();
	// 다음 틱이 시작되기를 기다리는 작업. Cast는 항상 여기에 넣고, 다음 틱이 시작될 때 tasks로 옮긴다
	// 틱 도중에 tasks가 바뀌면 작업을 건너뛰거나 두 번 진행할 수 있고, 새 작업이 끝나는 틱도 생긴 위치에 따라 달라지기 때문
	readonly List<FunctionTask> pendingTasks = new List<FunctionTask>();

	int fixedUpdateCount;
	ProcessMemory memory;

	/// <summary>
	/// 지금까지 지난 틱 수. 첫 틱이 1이다.
	/// </summary>
	public int CurrentTick { get; private set; }

	/// <summary>
	/// 작업이 첫 단계를 진행하기 직전에 불린다.
	/// </summary>
	public event Action<FunctionTask> TaskStarted;

	/// <summary>
	/// 작업이 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 task.EndReason으로, 실패했으면 실패 종류는 task.Failure로 확인한다.
	/// </summary>
	public event Action<FunctionTask> TaskEnded;

	/// <summary>
	/// 함수(사전 정의 함수, 사용자 정의 함수)를 시전해서 작업을 하나 만든다. 작업은 다음 틱부터 진행한다.<br/>
	/// 넘긴 함수는 원본으로 남고 복사본이 실행되므로, 같은 함수 객체를 여러 번 넘겨도 된다.
	/// </summary>
	public FunctionTask Cast(FunctionBlock function)
	{
		FunctionTask task = new FunctionTask(this, memory, (FunctionBlock)function.Clone());
		pendingTasks.Add(task);
		return task;
	}

	/// <summary>
	/// 진행 중인 작업과 다음 틱을 기다리는 작업을 모두 끊는다. 실행 중단 공격에 맞았을 때 부른다.<br/>
	/// 끊긴 작업은 EndReason이 Interrupted가 되고, 실행 중이던 함수들은 OnEnd 대신 OnCancel을 받으며 스택을 돌려준다.<br/>
	/// 끊긴 작업도 다른 작업처럼 틱이 끝날 때 목록에서 빠지며 TaskEnded가 불린다.
	/// </summary>
	public void InterruptAll()
	{
		// foreach 대신 번호로 돈다. OnCancel에서 Cast를 부르면 목록이 늘어나는데, foreach는 그때 오류를 내기 때문
		// 늘어난 작업도 끊어야 하므로 Count를 매번 다시 읽는다
		for (int i = 0; i < tasks.Count; i++)
			tasks[i].Interrupt();
		for (int i = 0; i < pendingTasks.Count; i++)
			pendingTasks[i].Interrupt();
	}

	/// <summary>
	/// 작업이 실패했을 때 그 작업이 부른다. 언어 규칙 표에서 처리를 찾아 실행한다.
	/// </summary>
	// 작업이 스스로 처리하지 않고 실행기가 맡는다. 재부팅처럼 다른 작업까지 끝내는 처리는 모든 작업을 가진 실행기만 할 수 있기 때문
	internal void HandleFailure(FunctionTask task, FunctionFailure failure)
	{
		switch (FindHandling(task, failure))
		{
			case FailureHandling.EndTask:
				task.End(FunctionTaskEndReason.Failed);
				break;
		}
	}

	FailureHandling FindHandling(FunctionTask task, FunctionFailure failure)
	{
		if (languageRule != null && languageRule.TryGetHandling(failure, out FailureHandling handling)) return handling;

		// 파일이 없는 경우는 Awake에서 이미 알렸다. 표에 빠진 줄은 어느 작업에서 났는지 알 수 있도록 실제로 났을 때 한 번 더 알린다
		if (languageRule != null)
			Debug.LogError($"{languageRule.name}: 실패 종류 {failure}의 처리가 표에 없습니다. 작업 {task.Name}만 끝냅니다.", languageRule);

		// 예외를 던지지 않고 그 작업만 끝낸다. 예외를 던지면 이번 틱의 나머지 작업이 진행되지 않고, 끝난 작업도 목록에서 빠지지 않기 때문
		return FailureHandling.EndTask;
	}

	void Awake()
	{
		memory = GetComponent<ProcessMemory>();
		CheckLanguageRule();
	}

	// 그 실패가 실제로 나기 전에, Play를 시작하자마자 표의 문제를 알리기 위함
	void CheckLanguageRule()
	{
		if (languageRule == null)
		{
			Debug.LogError($"{name}: FunctionExecutor에 언어 규칙 파일이 연결되지 않았습니다. 모든 실패를 그 작업만 끝내는 것으로 처리합니다.", this);
			return;
		}

		foreach (FunctionFailure failure in languageRule.FindMissingFailures())
			Debug.LogError($"{languageRule.name}: 실패 종류 {failure}의 처리가 표에 없습니다.", languageRule);
	}

	void FixedUpdate()
	{
		fixedUpdateCount++;
		if (fixedUpdateCount < fixedUpdatesPerTick) return;

		fixedUpdateCount = 0;
		RunTick();
	}

	void RunTick()
	{
		CurrentTick++;

		// 이벤트를 받은 쪽이 Cast를 불러도 pendingTasks가 도는 중에 바뀌지 않도록, 먼저 옮기고 나서 알린다
		int firstNew = tasks.Count;
		tasks.AddRange(pendingTasks);
		pendingTasks.Clear();
		// 진행을 시작하기 전에 끊긴 작업은 시작을 알리지 않는다. 이 틱이 끝날 때 TaskEnded만 불린다
		for (int i = firstNew; i < tasks.Count; i++)
			if (!tasks[i].IsEnded) TaskStarted?.Invoke(tasks[i]);

		foreach (FunctionTask task in GetTickOrder())
			task.Step();

		// 끝난 작업은 틱을 마친 뒤에 빼야, 이번 틱에 끝난 작업의 메모리가 진행 순서와 상관없이 항상 다음 틱부터 돌아온다
		foreach (FunctionTask task in tasks)
			if (task.IsEnded) TaskEnded?.Invoke(task);
		tasks.RemoveAll(task => task.IsEnded);
	}

	// 같은 틱 안에서 작업을 진행하는 순서. tasks는 시전한 순서대로 쌓이므로 그대로 돌려준다
	// 순서 규칙을 바꿀 때는 이 함수만 고친다
	IReadOnlyList<FunctionTask> GetTickOrder() => tasks;
}
