using UnityEngine;

/// <summary>
/// 작업의 상태. 입력 대기는 파라미터 입력 함수를 만들 때 쓴다.
/// </summary>
public enum FunctionTaskState
{
	Running,
	WaitingInput,
	Ended,
}

/// <summary>
/// 작업이 끝난 이유. Failed이면 어떤 실패였는지 FunctionTask.Failure로 확인한다.
/// </summary>
// 실패 종류마다 값을 두지 않는다. 실패 종류 목록(FunctionFailure)과 두 군데에서 함께 늘려야 하는 일을 막기 위함
public enum FunctionTaskEndReason
{
	None,
	Completed,
	// 실행 중단 공격으로 끊겼다. 작업 자신의 실패가 아니므로 Failed와 나눈다
	Interrupted,
	Failed,
}

/// <summary>
/// 시전한 함수 하나 = 작업 하나.<br/>
/// 작업은 시전한 함수 블록의 복사본을 가지고, 실행기가 틱마다 그 블록을 1틱씩 실행시킨다.
/// <para>
/// 사용법<br/>
/// - 작업은 실행기가 만든다. 다른 스크립트는 작업을 직접 만들거나 진행시키지 않는다.<br/>
/// - 사전 정의 함수는 OnStart, OnTick, OnEnd, OnCancel에서 받은 작업의 Caster로 시전한 캐릭터를 찾는다.
/// </para>
/// </summary>
public class FunctionTask
{
	// 실행 중인 함수 블록의 복사본. 안에 든 블록들도 이 블록이 실행한다
	readonly FunctionBlock function;
	// 실패한 블록이 기록한 실패 종류. 바깥에는 Failure로, 실패로 끝났을 때만 보여 준다
	// null을 쓰는 이유: enum 기본값은 첫 값인 Overflow라서, 기록하지 않았는데도 Overflow로 보이기 때문
	FunctionFailure? recordedFailure;
	// 스킬이 Fail로 낸 에러. 실패 종류가 SkillFailed일 때만 있다
	SkillError recordedError;

	/// <summary>
	/// 이 작업을 시전한 캐릭터.
	/// </summary>
	public GameObject Caster { get; }

	/// <summary>
	/// 처음 시전한 함수의 이름. 로그와 화면 표시에 쓴다.
	/// </summary>
	public string Name { get; }

	// 시전한 캐릭터의 메모리. 함수 블록이 실행을 시작하고 끝날 때 스택을 요청하고 반환한다
	internal ProcessMemory Memory { get; }

	// 이 작업을 만든 실행기. 사용자 정의 함수가 비동기 함수를 새 작업으로 시전할 때 쓴다
	internal FunctionExecutor Executor { get; }

	public FunctionTaskState State { get; private set; } = FunctionTaskState.Running;
	public FunctionTaskEndReason EndReason { get; private set; } = FunctionTaskEndReason.None;

	public bool IsEnded => State == FunctionTaskState.Ended;

	/// <summary>
	/// 실패로 끝났으면 true. 실패했는지는 이 값으로 확인한다.
	/// </summary>
	public bool IsFailed => EndReason == FunctionTaskEndReason.Failed;

	/// <summary>
	/// 실패로 끝났을 때 어떤 실패였는지. 실패로 끝나지 않았으면 null이다.
	/// </summary>
	// 기록한 값을 그대로 보여 주지 않고 IsFailed일 때만 보여 준다
	// 이후 Java의 try처럼 실패를 잡고 계속 진행해 Completed로 끝나는 경우에도, 이 값만 보고 실패로 오해하지 않게 하기 위함
	public FunctionFailure? Failure => IsFailed ? recordedFailure : null;

	/// <summary>
	/// 스킬이 낸 에러로 끝났을 때 그 에러(Failure가 SkillFailed일 때). 그 밖에는 null이다.
	/// </summary>
	// Failure와 같은 이유로 실패로 끝났을 때만 보여 준다
	public SkillError Error => IsFailed ? recordedError : null;

	// 작업은 실행기만 만든다. function은 원본이 아니라 실행기가 만든 복사본이어야 한다
	internal FunctionTask(FunctionExecutor executor, ProcessMemory memory, FunctionBlock function)
	{
		Executor = executor;
		Caster = executor.gameObject;
		Memory = memory;
		Name = function.Name;
		this.function = function;
	}

	/// <summary>
	/// 작업을 한 틱 진행한다. 실행기만 부른다.
	/// </summary>
	public void Step()
	{
		// 입력 대기 중이거나 이미 끝난 작업은 틱이 와도 진행하지 않는다
		if (State != FunctionTaskState.Running) return;

		BlockResult result = function.Execute(this);
		if (result == BlockResult.Running) return;

		// 실패했으면 쌓여 있던 함수들은 Execute 안에서 이미 끊기며 메모리를 돌려줬다
		// 무엇을 할지(이 작업만 끝낼지, C처럼 모든 작업을 끝낼지)는 실행기가 언어 규칙 표를 보고 정한다
		// Failed를 돌려준 블록은 반드시 RecordFailure를 먼저 부른다. 빠뜨렸으면 Value가 예외를 던져 바로 드러난다
		if (result == BlockResult.Failed) Executor.HandleFailure(this, recordedFailure.Value);
		else End(FunctionTaskEndReason.Completed);
	}

	/// <summary>
	/// 실패한 블록이 그 자리에서 실패 종류를 기록한다. 기록한 뒤 그 블록은 BlockResult.Failed를 돌려준다.<br/>
	/// 스킬이 Fail로 낸 에러이면 error도 함께 넘긴다.
	/// </summary>
	internal void RecordFailure(FunctionFailure failure, SkillError error = null)
	{
		recordedFailure = failure;
		recordedError = error;
	}

	// 실행 도중에 작업을 끊는다. 실행 중단 공격을 받으면 실행기의 InterruptAll이 부른다
	internal void Interrupt()
	{
		if (IsEnded) return;

		// 실행 중인 블록들이 안쪽까지 차례로 OnCancel을 받아 정리하게 한다
		function.Cancel(this);
		End(FunctionTaskEndReason.Interrupted);
	}

	// 실행기가 실패를 처리하며 이 작업을 끝낼 때도 부른다
	internal void End(FunctionTaskEndReason reason)
	{
		State = FunctionTaskState.Ended;
		EndReason = reason;
	}
}
