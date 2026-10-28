/// <summary>
/// 남은 메모리가 시전할 함수의 프레임 크기 이상인지 검사한다.
/// <para>
/// 맨 바깥 함수의 프레임만 본다. 실행기가 맨 처음 쓰는 칸이 이것이기 때문이다.<br/>
/// 안쪽 함수가 실행 중에 쓰는 stack은 검사하지 않는다. 모자라면 실행 중에 Overflow로 처리한다.
/// </para>
/// </summary>
public class MemoryCondition : CastCondition
{
	readonly ProcessMemory memory;

	public MemoryCondition(ProcessMemory memory)
	{
		this.memory = memory;
	}

	public override bool Check(FunctionBlock function, out FunctionFailure failure)
	{
		failure = FunctionFailure.NotEnoughMemory;
		// 시전한 작업은 다음 틱에 stack을 쓰므로, 같은 틱에 연달아 시전하면 둘 다 같은 Free를 보고 통과할 수 있다
		// 그때 나중에 시작한 쪽은 실행 중에 Overflow로 끝난다
		return memory.Free >= function.FrameSize;
	}
}
