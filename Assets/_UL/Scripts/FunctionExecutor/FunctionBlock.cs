/// <summary>
/// 함수 블록(사전 정의 함수, 사용자 정의 함수)의 기반 클래스.<br/>
/// 슬롯에 넣어 시전할 수 있는 것은 함수 블록뿐이다. return 같은 문법 블록은 함수 안에서만 쓴다.
/// <para>
/// 함수 블록은 실행을 시작할 때 FrameSize만큼 스택을 쓰고, 끝나거나 끊기는 즉시 돌려준다.
/// </para>
/// </summary>
public abstract class FunctionBlock : Block
{
	// 실제로 받은 칸 수. 실행 도중에 FrameSize가 바뀌어도 받은 만큼만 돌려주기 위해 따로 기록한다
	int allocatedStack;

	/// <summary>
	/// 함수 이름. 로그와 화면 표시에 쓴다.
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// 이 함수를 실행할 때 스택에 쌓이는 프레임 크기(칸).<br/>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수.<br/>
	/// 사전 정의 함수는 Parameters, LocalVariableCount로 계산한다. 사용자 정의 함수는 만들 때 받은 값이다(함수 데이터에서 바꾸면 FunctionDataConverter가 계산).
	/// </summary>
	public virtual int FrameSize => 1;

	/// <summary>
	/// 쿨타임(tick 수). 슬롯에서 시전한 함수가 끝난 뒤 이만큼 지나야 다시 시전할 수 있다.<br/>
	/// 사전 정의 함수는 스킬 구현자가 덮어써서 정하고(기본 0), 사용자 정의 함수는 안에 든 함수들의 쿨타임을 더한 값이다.
	/// </summary>
	// 동작 시간(TickCount)과 따로 둔다. 짧게 실행되고 오래 기다리는 스킬, 오래 실행되고 바로 다시 쓰는 스킬을 모두 만들기 위함
	public virtual int CooldownTicks => 0;

	// 순서: 프레임 쓰기 → OnStart → … → OnEnd/OnCancel → 프레임 돌려주기
	// 이 순서여야 UserFunction이 OnStart/OnEnd에서 쌓고 내리는 변수 저장소와 프레임이 어긋나지 않는다
	// sealed: C 스킬이나 사용자 정의 함수가 덮어써서 이 순서를 깨지 못하게 한다. OnStart에 두지 않는 이유도 같다(C 스킬이 base를 부르지 않아도 됨)
	internal sealed override BlockResult Execute(FunctionTask task)
	{
		if (State == BlockState.NotStarted)
		{
			// 프레임을 쌓을 공간이 없으면 시작하지 않는다. 시작하지 않았으므로 정리할 것도 없다
			if (!TryAllocateFrame(task))
			{
				task.RecordFailure(FunctionFailure.Overflow);
				return BlockResult.Failed;
			}
		}

		BlockResult result = base.Execute(task);
		// 함수가 끝나는 즉시 프레임을 반환한다. 실제 프로그램에서 return하는 순간 프레임이 빠지는 것과 같다
		if (result != BlockResult.Running) ReleaseFrame(task);
		return result;
	}

	internal sealed override void Cancel(FunctionTask task)
	{
		base.Cancel(task);
		ReleaseFrame(task);
	}

	bool TryAllocateFrame(FunctionTask task)
	{
		if (!task.Memory.TryAllocateStack(FrameSize)) return false;

		allocatedStack = FrameSize;
		return true;
	}

	// 받은 칸이 없으면(시작하지 않음, 이미 돌려줌) 아무것도 하지 않는다. 메모리를 두 번 돌려주지 않기 위함
	void ReleaseFrame(FunctionTask task)
	{
		if (allocatedStack == 0) return;

		task.Memory.ReleaseStack(allocatedStack);
		allocatedStack = 0;
	}
}
