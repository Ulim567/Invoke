using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임이 통째로 제공하는 함수(C 스킬, 공통 함수, 특수 이동 함수, 입력 함수)의 기반 클래스.<br/>
/// 플레이어는 내부를 볼 수 없고, 실행기가 정해진 틱 수 동안 이 함수를 진행시킨다.
/// <para>
/// 사용법 (docs/systems/predefined-function.md)<br/>
/// - 이 클래스를 상속하고 Id, Name, TickCount를 정한다. Id는 PredefinedFunctionIds의 상수를 쓴다.<br/>
/// - CodeSize, Parameters, ReturnType, LocalVariableCount, IsAsync는 기본값과 다를 때만 덮어쓴다.<br/>
/// - OnStart, OnTick, OnEnd, OnCancel 중 필요한 것만 덮어써서, 몇 번째 틱에 무엇을 할지 적는다. (예: 자세잡기 → 차징 → 발사)<br/>
/// - 실행되는 것은 언제나 복사본이므로, 실행 중에 쓰는 값(예: 차징한 양)을 필드에 그대로 저장해도 된다.<br/>
/// - OnStart나 OnTick에서 실행할 수 없는 상태를 발견하면 Fail(에러)로 에러를 내고, 에러 없이 일찍 끝내려면 Stop()을 부른다.<br/>
/// - 에러로 끝날 때 이 스킬만의 결과(예: 시전자가 피해를 입음)가 있으면 OnFail에 적는다.
/// </para>
/// <para>
/// 한 틱에서 부르는 순서: OnStart(첫 틱만) → OnTick → OnEnd(마지막 틱만)<br/>
/// 도중에 끊기면 OnEnd 대신 OnCancel이 불린다. 만들어 둔 오브젝트를 없애는 등의 정리는 OnCancel에서 한다.<br/>
/// TickCount가 1 이하이면 1틱짜리로 진행한다. 모든 블록이 최소 1틱을 써야 무한 반복이 한 틱 안에 갇히지 않는다.
/// </para>
/// </summary>
public abstract class PredefinedFunction : FunctionBlock
{
	int elapsedTicks;
	// Fail, Stop은 요청만 기록하고, Step이 이 요청을 보고 이 함수를 끝낸다
	// OnStart, OnTick 도중에 바로 끝내면 그 뒤의 스킬 코드가 끝난 함수 위에서 계속 실행되기 때문
	SkillError requestedError;
	bool stopRequested;

	/// <summary>
	/// 고정 ID. 함수 데이터의 함수 블록이 이 값으로 이 함수를 가리킨다. 화면 이름(Name)과 따로 둔다.
	/// </summary>
	public abstract string Id { get; }

	/// <summary>
	/// 이 함수의 동작 시간(틱 수).
	/// </summary>
	public abstract int TickCount { get; }

	/// <summary>
	/// 코드 영역 크기. 함수 안의 함수 블록으로 들어가든 슬롯에 바로 넣든 이 값으로 센다.
	/// </summary>
	public virtual int CodeSize => 1;

	/// <summary>
	/// 파라미터 이름과 타입. 편집 화면은 이 수와 순서대로 함수 블록의 값 칸을 만든다.
	/// </summary>
	public virtual IReadOnlyList<ParameterData> Parameters => Array.Empty<ParameterData>();

	/// <summary>
	/// 돌려주는 값의 타입. Void가 아니면 함수 블록을 값 칸에 끼울 수 있다.
	/// </summary>
	// 부를 때마다 새로 만든다. VariableType은 값을 바꿀 수 있어서, 하나를 함께 쓰면 한 곳에서 바꾼 값이 모든 함수에 퍼지기 때문
	public virtual VariableType ReturnType => new VariableType(VariableTypeKind.Void);

	/// <summary>
	/// 실행 중에 쓰는 지역 변수 수. 스택 프레임 크기 계산에 쓴다.<br/>
	/// 스킬마다 스택 부담을 조정하는 값이다. 무거운 스킬일수록 크게 정한다.
	/// </summary>
	// C 스킬의 내부는 블록이 아니라 C# 코드라서 실제 지역 변수를 셀 수 없다. 그래서 스킬 작성자가 직접 정한다
	public virtual int LocalVariableCount => 0;

	/// <summary>
	/// 다른 함수 안에서 불렸을 때 기다리지 않으면 true. 이때는 새 작업으로 실행되고, 부른 쪽은 바로 다음 블록으로 넘어간다.
	/// </summary>
	public virtual bool IsAsync => false;

	/// <summary>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수.
	/// </summary>
	// 스킬마다 직접 정하지 않고 계산한다. 파라미터를 고치고 프레임 크기를 고치지 않아 둘이 어긋나는 일을 막기 위함
	public sealed override int FrameSize => 1 + Parameters.Count + LocalVariableCount;

	protected sealed override BlockResult Step(FunctionTask task)
	{
		// OnStart에서 이미 Fail이나 Stop을 불렀으면 OnTick을 부르지 않는다
		if (requestedError == null && !stopRequested)
		{
			elapsedTicks++;
			OnTick(task, elapsedTicks);
		}

		// Fail과 Stop을 둘 다 불렀으면 에러를 먼저 따른다. 에러를 놓치면 안 되기 때문
		if (requestedError != null)
		{
			task.RecordFailure(FunctionFailure.SkillFailed, requestedError);
			OnFail(task, requestedError);
			return BlockResult.Failed;
		}
		if (stopRequested) return BlockResult.Done;

		return elapsedTicks >= TickCount ? BlockResult.Done : BlockResult.Running;
	}

	/// <summary>
	/// 매 틱 불린다. tick은 이번이 몇 번째 틱인지이며 1부터 TickCount까지 센다.
	/// </summary>
	protected virtual void OnTick(FunctionTask task, int tick) { }

	/// <summary>
	/// 이 스킬이 Fail로 에러를 냈을 때 한 번 불린다. 이 스킬만의 결과(예: 시전자가 피해를 입음)를 적는다.<br/>
	/// 이 다음에 OnCancel이 불리므로, 만들어 둔 오브젝트의 정리는 지금처럼 OnCancel에서 한다.<br/>
	/// 작업을 끝낼지, 페널티를 줄지 같은 처리는 여기서 하지 않는다. 캐릭터 언어의 언어 규칙 표가 정한다.
	/// </summary>
	// OnCancel과 나눈 이유: OnCancel은 실행 중단 공격, Overflow 때도 불리므로 스킬이 스스로 낸 에러만 구분할 수 없기 때문
	protected virtual void OnFail(FunctionTask task, SkillError error) { }

	/// <summary>
	/// 이 함수를 에러로 끝낸다. OnStart나 OnTick에서 부른다.<br/>
	/// 그 틱이 끝날 때 OnEnd 대신 OnCancel이 불리고, 무엇을 할지(그 작업만 끝냄 등)는 캐릭터 언어의 언어 규칙 표가 정한다.<br/>
	/// 여러 번 부르면 처음 에러만 쓴다.
	/// </summary>
	protected void Fail(SkillError error)
	{
		if (error == null) throw new ArgumentNullException(nameof(error));
		// 이미 끝난 함수(OnEnd, OnCancel 안)에서 부르면 되돌릴 수 없으므로 스킬 코드의 실수로 알린다
		if (HasEnded)
		{
			Debug.LogError($"{Name}: 끝난 뒤에 Fail({error.Message})을 불렀습니다. Fail은 OnStart, OnTick에서만 부릅니다.");
			return;
		}

		if (requestedError != null) return;

		error.SourceName = Name;
		requestedError = error;
	}

	/// <summary>
	/// 이 함수를 에러 없이 일찍 끝낸다. OnStart나 OnTick에서 부른다.<br/>
	/// 그 틱이 끝날 때 OnEnd가 불리고, 사용자 정의 함수 안이었다면 다음 블록으로 넘어간다.
	/// </summary>
	protected void Stop()
	{
		if (HasEnded)
		{
			Debug.LogError($"{Name}: 끝난 뒤에 Stop()을 불렀습니다. Stop은 OnStart, OnTick에서만 부릅니다.");
			return;
		}

		stopRequested = true;
	}
}
