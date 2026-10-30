/// <summary>
/// free 블록. 예: free(s). 함수 몸통에 놓는다. 1틱을 쓴다.
/// <para>
/// - 값 칸의 포인터가 가리키는 객체를 heap에서 해제하고 칸을 돌려준다. 화면의 오브젝트는 없애지 않는다.<br/>
/// - 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터)이면 InvalidPointerError로 실패한다(실패 종류 SkillFailed, 스킬의 Free와 같음).<br/>
/// - 값 칸이 포인터가 아니면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class FreeBlock : Block
{
	// 에러를 낸 곳으로 보여 줄 이름. 스킬이 낸 에러는 스킬 이름이 들어가는 자리다
	const string SourceName = "free";

	readonly ValueBlock pointerOriginal;
	// 이 실행에서 쓰는 값 칸. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public FreeBlock(ValueBlock pointer)
	{
		pointerOriginal = pointer;
	}

	protected override void OnStart(FunctionTask task)
	{
		slots = new ValueSlots(pointerOriginal);
	}

	protected override BlockResult Step(FunctionTask task)
	{
		BlockResult result = slots.Step(task);
		if (result != BlockResult.Done) return result;

		RuntimeValue value = slots[0];
		if (value.Type?.Kind != VariableTypeKind.Pointer)
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		int pointer = value.AsPointer();
		if (!task.Memory.Heap.Free(pointer))
		{
			task.RecordFailure(FunctionFailure.SkillFailed, new InvalidPointerError(pointer) { SourceName = SourceName });
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		slots?.Cancel(task);
	}
}
