using System.Collections.Generic;
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
/// 작업이 끝난 이유. 실패는 책임에 따라 나눈다: Failed는 플레이어 책임, InternalError는 개발자 책임.<br/>
/// Failed이면 어떤 실패였는지 FunctionTask.Failure로 확인한다.
/// </summary>
// 실패 종류마다 값을 두지 않는다. 실패 종류 목록(FunctionFailure)과 두 군데에서 함께 늘려야 하는 일을 막기 위함
public enum FunctionTaskEndReason
{
	None,
	Completed,
	// 실행 중단 공격으로 끊겼다. 작업 자신의 실패가 아니므로 Failed와 나눈다
	Interrupted,
	// 플레이어가 짠 함수에 잘못이 있었다(compile error, runtime error). 화면이 플레이어의 에러로 보여 준다
	Failed,
	// 개발자가 만든 것(스킬 코드, 언어 규칙 파일)에 잘못이 있었다. 실행기가 콘솔 오류를 내고, 화면은 플레이어의 에러로 보여 주지 않는다
	// 예: 스킬이 Return을 빠뜨림, 언어 규칙 표에 그 실패 종류의 줄이 없음
	InternalError,
}

/// <summary>
/// 시전한 함수 하나 = 작업 하나.<br/>
/// 작업은 시전한 함수 블록의 복사본을 가지고, 실행기가 틱마다 그 블록을 1틱씩 실행시킨다.
/// <para>
/// 사용법<br/>
/// - 작업은 실행기가 만든다. 다른 스크립트는 작업을 직접 만들거나 진행시키지 않는다.<br/>
/// - 다른 스크립트(화면 등)는 작업 상태(State, EndReason, Failure, Error)를 읽기만 한다.<br/>
/// - 블록이 실행 중에 쓰는 것(지역 변수, 실패 기록, 메모리)은 작업이 아니라 실행 문맥(FunctionContext)에 있다.
/// </para>
/// </summary>
public class FunctionTask
{
	// 실행 중인 함수 블록의 복사본. 안에 든 블록들도 이 블록이 실행한다
	readonly FunctionBlock function;
	// 블록에 넘기는 실행 문맥. 작업마다 하나다
	readonly FunctionContext context;

	/// <summary>
	/// 이 작업을 시전한 캐릭터.
	/// </summary>
	public GameObject Caster { get; }

	/// <summary>
	/// 처음 시전한 함수의 이름. 로그와 화면 표시에 쓴다.
	/// </summary>
	public string Name { get; }

	// 슬롯에서 시전할 때 넘긴 함수(원본). 이 작업이 끝나면 실행기가 이 함수의 쿨타임을 시작한다
	// 함수 안의 비동기 시전으로 만든 작업은 null이다. 그 스킬의 쿨타임은 바깥 함수의 쿨타임에 이미 더해져 있기 때문
	internal FunctionBlock CooldownSource { get; set; }

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
	public FunctionFailure? Failure => IsFailed ? context.RecordedFailure : null;

	/// <summary>
	/// 스킬이 낸 에러로 끝났을 때 그 에러(Failure가 SkillFailed일 때). 그 밖에는 null이다.
	/// </summary>
	// Failure와 같은 이유로 실패로 끝났을 때만 보여 준다
	public SkillError Error => IsFailed ? context.RecordedError : null;

	// 작업은 실행기만 만든다. function은 원본이 아니라 실행기가 만든 복사본이어야 한다
	// 실행기를 받지 않는다. 작업은 결과만 남기고, 실행기가 읽어서 처리하기 때문
	internal FunctionTask(GameObject caster, ProcessMemory memory, LanguageRule language, FunctionBlock function)
	{
		Caster = caster;
		Name = function.Name;
		this.function = function;
		context = new FunctionContext(this, caster, memory, language);
	}

	/// <summary>
	/// 작업을 한 틱 진행한다. 실행기만 부른다.<br/>
	/// 플레이어 책임으로 실패했으면 그 실패 종류를 돌려준다. 실행기가 언어 규칙 표로 처리한다. 그 밖에는 null이다.
	/// </summary>
	internal FunctionFailure? Step()
	{
		// 입력 대기 중이거나 이미 끝난 작업은 틱이 와도 진행하지 않는다
		if (State != FunctionTaskState.Running) return null;

		BlockResult result = function.Execute(context);
		if (result == BlockResult.Running) return null;

		// 정상 종료와 개발자 책임은 언어 규칙 표가 필요 없으므로 작업이 스스로 끝낸다
		if (result != BlockResult.Failed)
		{
			End(FunctionTaskEndReason.Completed);
			return null;
		}
		if (context.InternalErrorRecorded)
		{
			End(FunctionTaskEndReason.InternalError);
			return null;
		}

		// 실패했으면 쌓여 있던 함수들은 Execute 안에서 이미 끊기며 메모리를 돌려줬다
		// 무엇을 할지(이 작업만 끝낼지, C처럼 모든 작업을 끝낼지)는 실행기가 언어 규칙 표를 보고 정한다
		// Failed를 돌려준 블록은 반드시 RecordFailure나 RecordInternalError를 먼저 부른다. 빠뜨렸으면 Value가 예외를 던져 바로 드러난다
		return context.RecordedFailure.Value;
	}

	// 이번 틱에 블록이 요청한 비동기 시전을 꺼낸다. 실행기가 Step 다음에 부른다
	internal IReadOnlyList<FunctionBlock> TakeAsyncRequests() => context.TakeAsyncRequests();

	// 실행 도중에 작업을 끊는다. 실행 중단 공격을 받으면 실행기의 InterruptAll이 부른다
	internal void Interrupt()
	{
		if (IsEnded) return;

		// 실행 중인 블록들이 안쪽까지 차례로 OnCancel을 받아 정리하게 한다
		function.Cancel(context);
		End(FunctionTaskEndReason.Interrupted);
	}

	// 실행기가 실패를 처리하며 이 작업을 끝낼 때도 부른다
	internal void End(FunctionTaskEndReason reason)
	{
		State = FunctionTaskState.Ended;
		EndReason = reason;
	}
}
