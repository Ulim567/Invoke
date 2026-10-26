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
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수. 지금은 파라미터와 지역 변수가 없으므로 1이다.<br/>
	/// C 스킬은 덮어써서 스킬마다 다르게 정할 수 있다.
	/// </summary>
	public virtual int FrameSize => 1;

	internal sealed override bool AllocateMemory(FunctionTask task)
	{
		if (!task.Memory.TryAllocateStack(FrameSize)) return false;

		allocatedStack = FrameSize;
		return true;
	}

	internal sealed override void ReleaseMemory(FunctionTask task)
	{
		task.Memory.ReleaseStack(allocatedStack);
		allocatedStack = 0;
	}
}
