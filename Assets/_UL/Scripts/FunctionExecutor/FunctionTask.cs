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
/// 작업이 끝난 이유. 외부 중단과 오버플로는 해당 기능을 만들 때 쓴다.
/// </summary>
public enum FunctionTaskEndReason
{
	None,
	Completed,
	Interrupted,
	Overflow,
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

	/// <summary>
	/// 이 작업을 시전한 캐릭터.
	/// </summary>
	public GameObject Caster { get; }

	/// <summary>
	/// 처음 시전한 함수의 이름. 로그와 화면 표시에 쓴다.
	/// </summary>
	public string Name { get; }

	public FunctionTaskState State { get; private set; } = FunctionTaskState.Running;
	public FunctionTaskEndReason EndReason { get; private set; } = FunctionTaskEndReason.None;

	public bool IsEnded => State == FunctionTaskState.Ended;

	// 작업은 실행기만 만든다. function은 원본이 아니라 실행기가 만든 복사본이어야 한다
	internal FunctionTask(GameObject caster, FunctionBlock function)
	{
		Caster = caster;
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

		// 함수 블록이 끝까지 실행됐으면 정상 종료
		if (function.Execute(this) != BlockResult.Running) End(FunctionTaskEndReason.Completed);
	}

	// 실행 도중에 작업을 끊는다. 외부 중단 기능을 만들 때 실행기가 부른다
	internal void Interrupt()
	{
		if (IsEnded) return;

		// 실행 중인 블록들이 안쪽까지 차례로 OnCancel을 받아 정리하게 한다
		function.Cancel(this);
		End(FunctionTaskEndReason.Interrupted);
	}

	void End(FunctionTaskEndReason reason)
	{
		State = FunctionTaskState.Ended;
		EndReason = reason;
	}
}
