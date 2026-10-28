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
	// 실패 종류마다 값을 따로 두지 않는다. 종류는 실패한 블록이 FunctionTask.RecordFailure로 기록하므로, 종류가 늘어도 블록 결과를 받는 쪽은 고치지 않는다
	Failed,
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
	bool started;
	bool ended;

	// 이 블록이 끝났는지. OnEnd, OnCancel이 불릴 때는 이미 true다
	internal bool HasEnded => ended;

	/// <summary>
	/// 이 블록을 1틱 실행한다. 실행기와 바깥 블록만 부른다.
	/// </summary>
	internal BlockResult Execute(FunctionTask task)
	{
		if (!started)
		{
			// 프레임을 쌓을 공간이 없으면 시작하지 않는다. 시작하지 않았으므로 정리할 것도 없다
			if (!AllocateMemory(task))
			{
				ended = true;
				task.RecordFailure(FunctionFailure.Overflow);
				return BlockResult.Failed;
			}

			started = true;
			OnStart(task);
		}

		BlockResult result = Step(task);
		if (result == BlockResult.Running) return result;

		ended = true;
		// 안쪽에서 실패하면 이 블록도 끝까지 실행하지 못한 것이므로 끊긴 것으로 정리한다
		if (result == BlockResult.Failed) OnCancel(task);
		else OnEnd(task);

		// 함수가 끝나는 즉시 프레임을 반환한다. 실제 프로그램에서 return하는 순간 프레임이 빠지는 것과 같다
		ReleaseMemory(task);
		return result;
	}

	/// <summary>
	/// 실행 도중에 이 블록을 끊는다. 외부 중단 등으로 작업이 끊길 때 부른다.
	/// </summary>
	internal void Cancel(FunctionTask task)
	{
		// 시작했고 아직 끝나지 않은 블록만 정리한다. 이미 끝난 블록을 두 번 정리하거나 메모리를 두 번 반환하지 않기 위함
		if (!started || ended) return;

		ended = true;
		OnCancel(task);
		ReleaseMemory(task);
	}

	// 실행을 시작할 때 메모리를 요청하고, 끝나거나 끊길 때 반환하는 자리. 함수 블록만 덮어쓴다
	// OnStart 등과 따로 두어, C 스킬이 OnStart를 덮어쓰면서 base를 부르지 않아도 메모리 계산이 틀어지지 않게 한다
	internal virtual bool AllocateMemory(FunctionTask task) => true;
	internal virtual void ReleaseMemory(FunctionTask task) { }

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
