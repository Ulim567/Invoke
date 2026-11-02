/// <summary>
/// 블록을 1틱 실행한 결과.
/// </summary>
public enum BlockResult
{
	// 다음 틱에도 이어서 실행해야 한다
	Running,
	// 끝까지 실행했다
	Done,
	// return을 만나 끝났다. 이 블록을 담은 함수도 남은 블록을 실행하지 않고 끝나야 한다
	Returned,
	// 실패해서 실행하지 못했다. 바깥 함수들도 끝까지 실행하지 못하고 끝난다
	// 실패 종류마다 값을 따로 두지 않는다. 종류는 실패한 블록이 FunctionContext.RecordFailure로 기록하므로, 종류가 늘어도 블록 결과를 받는 쪽은 고치지 않는다
	Failed,
	// break를 만나 끝났다. 가장 안쪽 반복문이 받아 반복을 끝낸다. 반복문 밖이면 사용자 정의 함수가 NotInLoop로 실패시킨다
	Break,
	// continue를 만나 끝났다. 가장 안쪽 반복문이 받아 다음 검사로 간다. 반복문 밖이면 Break와 같다
	Continue,
}

/// <summary>
/// 블록 복사본 하나의 진행 상태.
/// </summary>
// 시작·끝·끊김을 bool 세 개로 두면 "끝나지 않았는데 끊김" 같은 있을 수 없는 조합이 생기므로 값 하나로 둔다
// BlockResult와 따로 두는 이유: 두 값은 읽는 쪽과 남는 기간이 다르다
// - BlockResult: Execute 한 번(1틱)의 결과. 바깥 블록이나 실행기가 받아 다음에 할 일(같은 블록 계속, 다음 블록, 함수 끝내기, 실패 처리)을 정하고 버린다.
//   Done과 Returned처럼 블록 자신에게는 같은 "끝남"이어도 바깥 함수가 다르게 처리해야 하는 차이를 담는다
// - BlockState: 블록 복사본이 틱을 넘어 기억하는 자기 상태. 블록 자신이 읽어 OnStart를 부를지, Cancel에서 정리할 것이 있는지,
//   지금 OnCancel 안인지(사전 정의 함수의 Malloc·Return 막기)를 정한다. 아직 실행하지 않은 NotStarted처럼 틱 결과로는 나타낼 수 없는 값도 담는다
public enum BlockState
{
	// 아직 실행하지 않았다. 프레임을 쓰지 못해 시작하지 못한 함수 블록도 이 값으로 남는다
	NotStarted,
	// OnStart가 불렸고 아직 끝나지 않았다
	Running,
	// 끝까지 실행했다(return 포함). OnEnd가 불릴 때는 이미 이 값이다
	Ended,
	// 도중에 끊겼다(실패 포함). OnCancel이 불릴 때는 이미 이 값이다
	Cancelled,
}

/// <summary>
/// 모든 블록(사전 정의 함수, 사용자 정의 함수, return 같은 문법)의 기반 클래스.
/// <para>
/// 실행 방법<br/>
/// - 실행기가 틱마다 Execute를 한 번 부른다. Execute 한 번이 1틱이다.<br/>
/// - 처음 실행될 때 OnStart, 끝까지 실행했을 때 OnEnd, 도중에 끊겼을 때(실패 포함) OnCancel이 한 번씩 불린다.
/// </para>
/// <para>
/// 복사<br/>
/// - 블록은 진행 상황을 자기 필드에 가진다. 그래서 함수에 들어 있는 블록은 원본으로만 두고, 실행할 때마다 Clone으로 만든 복사본을 실행한다.<br/>
/// - Clone은 필드를 그대로 베끼므로, List 같은 참조 형식 필드는 원본과 복사본이 함께 쓴다. 실행 중에 쓰는 참조 형식 값은 OnStart에서 새로 만든다.
/// </para>
/// </summary>
public abstract class Block
{
	BlockState state;

	// 함수 블록이 시작 전에 프레임을 쓸지 정할 때 읽는다
	internal BlockState State => state;

	// 이 블록이 끝났는지. OnEnd, OnCancel이 불릴 때는 이미 true다
	internal bool HasEnded => state == BlockState.Ended || state == BlockState.Cancelled;

	// 이 블록이 끊겼는지. OnCancel이 불릴 때는 이미 true다
	// 사전 정의 함수가 OnCancel 안에서 힙을 바꾸지 못하게 막을 때 쓴다. HasEnded만으로는 OnEnd와 OnCancel을 구분할 수 없기 때문
	internal bool WasCancelled => state == BlockState.Cancelled;

	/// <summary>
	/// 이 블록을 1틱 실행한다. 실행기와 바깥 블록만 부른다.
	/// </summary>
	// 함수 블록만 덮어써서 앞뒤로 프레임을 쓰고 돌려준다
	internal virtual BlockResult Execute(FunctionContext context)
	{
		if (state == BlockState.NotStarted)
		{
			state = BlockState.Running;
			OnStart(context);
		}

		BlockResult result = Step(context);
		if (result == BlockResult.Running) return result;

		// 안쪽에서 실패하면 이 블록도 끝까지 실행하지 못한 것이므로 끊긴 것으로 정리한다
		if (result == BlockResult.Failed)
		{
			state = BlockState.Cancelled;
			OnCancel(context);
		}
		else
		{
			state = BlockState.Ended;
			OnEnd(context);
		}

		return result;
	}

	/// <summary>
	/// 실행 도중에 이 블록을 끊는다. 외부 중단 등으로 작업이 끊길 때 부른다.
	/// </summary>
	// 함수 블록만 덮어써서 OnCancel 뒤에 프레임을 돌려준다
	internal virtual void Cancel(FunctionContext context)
	{
		// 시작했고 아직 끝나지 않은 블록만 정리한다. 이미 끝난 블록을 두 번 정리하지 않기 위함
		if (state != BlockState.Running) return;

		state = BlockState.Cancelled;
		OnCancel(context);
	}

	// 원본은 여러 작업이 함께 쓰므로 직접 실행하지 않고, 실행할 때마다 이 복사본을 만든다
	internal Block Clone() => (Block)MemberwiseClone();

	/// <summary>
	/// 1틱 분량을 실행하고 결과를 돌려준다.
	/// </summary>
	protected abstract BlockResult Step(FunctionContext context);

	/// <summary>
	/// 처음 실행될 때 Step보다 먼저 한 번 불린다.
	/// </summary>
	protected virtual void OnStart(FunctionContext context) { }

	/// <summary>
	/// 끝까지 실행했을 때(return 포함) Step 다음으로 한 번 불린다.
	/// </summary>
	protected virtual void OnEnd(FunctionContext context) { }

	/// <summary>
	/// 실행 도중에 끊겼을 때 한 번 불린다. 이때는 OnEnd가 불리지 않는다.
	/// </summary>
	protected virtual void OnCancel(FunctionContext context) { }
}

/// <summary>
/// 남은 블록을 실행하지 않고 지금 함수를 끝내는 블록. 1틱을 쓴다. 예: return;, return x + 1;
/// <para>
/// - value slot에 쓴 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱에 함수를 끝낸다.<br/>
/// - 값을 반환 타입으로 맞추는 일과 return 누락 검사는 이 블록을 담은 사용자 정의 함수가 끝날 때 한다(UserFunction).
/// </para>
/// </summary>
public class ReturnBlock : Block
{
	// value slot(원본). 값을 비운 return;이면 null
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	/// <summary>
	/// 값을 돌려주지 않는 return;
	/// </summary>
	public ReturnBlock() : this(null) { }

	/// <summary>
	/// value를 돌려주는 return. value가 null이면 return;과 같다.
	/// </summary>
	public ReturnBlock(ValueBlock value)
	{
		valueOriginal = value;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(valueOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		// 값은 실행 문맥에 맡긴다. 이 블록은 자기를 담은 함수를 모르고, Returned를 받는 가장 안쪽 사용자 정의 함수가 꺼내 가기 때문
		context.RecordReturnValue(valueOriginal == null ? null : slots.Get(0));
		return BlockResult.Returned;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}

/// <summary>
/// break 블록. 가장 안쪽 반복문을 끝낸다. 1틱을 쓴다. 반복문 밖에 놓이면 NotInLoop로 실패한다.
/// </summary>
public class BreakBlock : Block
{
	protected override BlockResult Step(FunctionContext context) => BlockResult.Break;
}

/// <summary>
/// continue 블록. 가장 안쪽 반복문의 이번 바퀴를 끝내고 다음 검사로 간다. 1틱을 쓴다. 반복문 밖에 놓이면 NotInLoop로 실패한다.
/// </summary>
public class ContinueBlock : Block
{
	protected override BlockResult Step(FunctionContext context) => BlockResult.Continue;
}
