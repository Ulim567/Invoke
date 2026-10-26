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
}

/// <summary>
/// 모든 블록(사전 정의 함수, 사용자 정의 함수, return 같은 문법)의 기반 클래스.
/// <para>
/// 실행 방법<br/>
/// - 실행기가 틱마다 Execute를 한 번 부른다. Execute 한 번이 1틱이다.<br/>
/// - 처음 실행될 때 OnStart, 끝까지 실행했을 때 OnEnd, 도중에 끊겼을 때 OnCancel이 한 번씩 불린다.
/// </para>
/// <para>
/// 복사<br/>
/// - 블록은 진행 상황을 자기 필드에 가진다. 그래서 함수에 들어 있는 블록은 원본으로만 두고, 실행할 때마다 Clone으로 만든 복사본을 실행한다.<br/>
/// - Clone은 필드를 그대로 베끼므로, List 같은 참조 형식 필드는 원본과 복사본이 함께 쓴다. 실행 중에 쓰는 참조 형식 값은 OnStart에서 새로 만든다.
/// </para>
/// </summary>
public abstract class Block
{
	bool started;
	bool ended;

	/// <summary>
	/// 이 블록을 1틱 실행한다. 실행기와 바깥 블록만 부른다.
	/// </summary>
	internal BlockResult Execute(FunctionTask task)
	{
		if (!started)
		{
			started = true;
			OnStart(task);
		}

		BlockResult result = Step(task);
		if (result != BlockResult.Running)
		{
			ended = true;
			OnEnd(task);
		}
		return result;
	}

	/// <summary>
	/// 실행 도중에 이 블록을 끊는다. 외부 중단 등으로 작업이 끊길 때 부른다.
	/// </summary>
	internal void Cancel(FunctionTask task)
	{
		// 시작했고 아직 끝나지 않은 블록만 정리한다. 이미 끝난 블록을 두 번 정리하지 않기 위함
		if (!started || ended) return;

		ended = true;
		OnCancel(task);
	}

	// 원본은 여러 작업이 함께 쓰므로 직접 실행하지 않고, 실행할 때마다 이 복사본을 만든다
	internal Block Clone() => (Block)MemberwiseClone();

	/// <summary>
	/// 1틱 분량을 실행하고 결과를 돌려준다.
	/// </summary>
	protected abstract BlockResult Step(FunctionTask task);

	/// <summary>
	/// 처음 실행될 때 Step보다 먼저 한 번 불린다.
	/// </summary>
	protected virtual void OnStart(FunctionTask task) { }

	/// <summary>
	/// 끝까지 실행했을 때(return 포함) Step 다음으로 한 번 불린다.
	/// </summary>
	protected virtual void OnEnd(FunctionTask task) { }

	/// <summary>
	/// 실행 도중에 끊겼을 때 한 번 불린다. 이때는 OnEnd가 불리지 않는다.
	/// </summary>
	protected virtual void OnCancel(FunctionTask task) { }
}

/// <summary>
/// 남은 블록을 실행하지 않고 지금 함수를 끝내는 블록. 1틱을 쓴다.
/// </summary>
public class ReturnBlock : Block
{
	protected override BlockResult Step(FunctionTask task) => BlockResult.Returned;
}
