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

	// 시전할 때 차례로 검사하는 공통 조건. 앞의 조건에서 실패하면 뒤의 조건은 검사하지 않는다
	// 새 조건(쿨타임 등)은 Awake에서 이 목록에 추가한다
	readonly List<CastCondition> castConditions = new List<CastCondition>();

	// 슬롯에서 시전한 함수(원본)마다 쿨타임이 끝나는 틱. 쿨타임 값은 실행기만 바꾼다
	// 원본 객체로 구분하므로, 슬롯은 같은 함수 객체를 계속 넘겨야 쿨타임이 이어진다
	readonly Dictionary<FunctionBlock, int> cooldownEndTicks = new Dictionary<FunctionBlock, int>();

	int fixedUpdateCount;
	ProcessMemory memory;
	PlayerStats stats;

	/// <summary>
	/// 지금까지 지난 틱 수. 첫 틱이 1이다.
	/// </summary>
	public int CurrentTick { get; private set; }

	/// <summary>
	/// 1틱의 길이(초). 스킬 애니메이션 길이를 TickCount에 맞추거나, 쿨타임을 초로 보여 줄 때 쓴다.
	/// </summary>
	public float SecondsPerTick => Time.fixedDeltaTime * fixedUpdatesPerTick;

	/// <summary>
	/// 작업이 첫 단계를 진행하기 직전에 불린다.
	/// </summary>
	public event Action<FunctionTask> TaskStarted;

	/// <summary>
	/// 작업이 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 task.EndReason으로, 실패했으면 실패 종류는 task.Failure로 확인한다.
	/// </summary>
	public event Action<FunctionTask> TaskEnded;

	/// <summary>
	/// 시전 전 검사에 걸려 시전하지 않았을 때 불린다. 거부한 함수와 실패 종류를 넘긴다.
	/// </summary>
	public event Action<FunctionBlock, FunctionFailure> CastRejected;

	/// <summary>
	/// 함수(사전 정의 함수, 사용자 정의 함수)를 시전해서 작업을 하나 만든다. 작업은 다음 틱부터 진행한다.<br/>
	/// 넘긴 함수는 원본으로 남고 복사본이 실행되므로, 같은 함수 객체를 여러 번 넘겨도 된다.<br/>
	/// 시전 전 검사(메모리 등)에 걸리면 작업을 만들지 않고 CastRejected를 알린 뒤 null을 돌려준다.
	/// </summary>
	public FunctionTask Cast(FunctionBlock function)
	{
		// 빈 슬롯은 시전하는 쪽(키 입력 처리)이 걸러 Cast를 부르지 않는다. 그래서 null은 코드 실수로 보고 바로 드러나게 한다
		if (function == null) throw new ArgumentNullException(nameof(function));

		foreach (CastCondition condition in castConditions)
		{
			if (condition.Check(function, out FunctionFailure failure)) continue;

			HandleCastFailure(function, failure);
			return null;
		}

		FunctionTask task = CastUnchecked(function);
		task.CooldownSource = function;
		return task;
	}

	/// <summary>
	/// 슬롯에서 시전한 function의 남은 쿨타임(틱 수). 쿨타임 중이 아니면 0이다. 쿨타임 UI에서 쓴다.
	/// </summary>
	public int GetRemainingCooldownTicks(FunctionBlock function)
	{
		if (!cooldownEndTicks.TryGetValue(function, out int endTick)) return 0;
		return Mathf.Max(0, endTick - CurrentTick);
	}

	/// <summary>
	/// 슬롯에서 시전한 function이 아직 실행 중이거나 다음 틱을 기다리고 있으면 true.
	/// </summary>
	public bool IsRunning(FunctionBlock function)
	{
		foreach (FunctionTask task in tasks)
			if (task.CooldownSource == function && !task.IsEnded) return true;
		foreach (FunctionTask task in pendingTasks)
			if (task.CooldownSource == function && !task.IsEnded) return true;
		return false;
	}

	/// <summary>
	/// 시전 전 검사 없이 작업을 만든다. 사용자 정의 함수가 안쪽의 비동기 함수를 새 작업으로 시전할 때 쓴다.
	/// </summary>
	// 안쪽 비동기 시전은 슬롯에서 시전한 것이 아니라 이미 실행 중인 코드의 일부이므로, 시전 전 검사를 하지 않는다
	internal FunctionTask CastUnchecked(FunctionBlock function)
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
		switch (FindHandling(task.Name, failure))
		{
			// 이미 실행 중인 작업은 시전을 되돌릴 수 없으므로, 거부도 그 작업만 끝내는 것으로 처리한다
			case FailureHandling.Reject:
			case FailureHandling.EndTask:
				task.End(FunctionTaskEndReason.Failed);
				break;
		}
	}

	// 시전 전 검사에 걸렸을 때. 아직 작업이 없다
	void HandleCastFailure(FunctionBlock function, FunctionFailure failure)
	{
		switch (FindHandling(function.Name, failure))
		{
			// 끝낼 작업이 아직 없으므로, 그 작업만 끝내는 처리도 시전하지 않는 것과 같다
			// 재부팅처럼 시전 전에도 다른 일을 하는 처리가 생기면 여기에 case를 따로 둔다
			case FailureHandling.Reject:
			case FailureHandling.EndTask:
				CastRejected?.Invoke(function, failure);
				break;
		}
	}

	FailureHandling FindHandling(string functionName, FunctionFailure failure)
	{
		if (languageRule != null && languageRule.TryGetHandling(failure, out FailureHandling handling)) return handling;

		// 파일이 없는 경우는 Awake에서 이미 알렸다. 표에 빠진 줄은 어느 함수에서 났는지 알 수 있도록 실제로 났을 때 한 번 더 알린다
		if (languageRule != null)
			Debug.LogError($"{languageRule.name}: 실패 종류 {failure}의 처리가 표에 없습니다. {functionName}을 EndTask로 처리합니다.", languageRule);

		// 예외를 던지지 않고 EndTask로 처리한다. 예외를 던지면 이번 틱의 나머지 작업이 진행되지 않고, 끝난 작업도 목록에서 빠지지 않기 때문
		return FailureHandling.EndTask;
	}

	void Awake()
	{
		memory = GetComponent<ProcessMemory>();
		stats = GetComponent<PlayerStats>();
		// 쿨타임을 먼저 검사한다. 둘 다 걸리면 「아직 쓸 수 없음」이 플레이어에게 더 정확한 이유이기 때문
		castConditions.Add(new CooldownCondition(this));
		castConditions.Add(new MemoryCondition(memory));
		CheckLanguageRule();
	}

	// 쿨타임은 함수가 끝난 순간부터 흐른다. 정상 종료, 실패, 실행 중단 모두 같다
	// 쿨타임 감소는 끝나는 순간의 능력치로 한 번 적용한다
	void StartCooldown(FunctionTask task)
	{
		if (task.CooldownSource == null) return;

		int cooldown = task.CooldownSource.CooldownTicks;
		if (cooldown <= 0) return;

		// 공격 속도(ApplyAttackSpeed)와 같게 반올림해서 틱 수로 맞춘다
		cooldown = Mathf.RoundToInt(stats.ApplyCooldownReduction(cooldown));
		cooldownEndTicks[task.CooldownSource] = CurrentTick + cooldown;
	}

	// 그 실패가 실제로 나기 전에, Play를 시작하자마자 표의 문제를 알리기 위함
	void CheckLanguageRule()
	{
		if (languageRule == null)
		{
			Debug.LogError($"{name}: FunctionExecutor에 언어 규칙 파일이 연결되지 않았습니다. 모든 실패를 EndTask로 처리합니다.", this);
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
		{
			if (!task.IsEnded) continue;

			StartCooldown(task);
			// TaskEnded보다 먼저 한다. 이벤트를 받은 쪽이 누수 여부까지 정해진 상태를 보게 하기 위함
			memory.Heap.HandleTaskEnded(task);
			TaskEnded?.Invoke(task);
		}
		tasks.RemoveAll(task => task.IsEnded);
	}

	// 같은 틱 안에서 작업을 진행하는 순서. tasks는 시전한 순서대로 쌓이므로 그대로 돌려준다
	// 순서 규칙을 바꿀 때는 이 함수만 고친다
	IReadOnlyList<FunctionTask> GetTickOrder() => tasks;
}
