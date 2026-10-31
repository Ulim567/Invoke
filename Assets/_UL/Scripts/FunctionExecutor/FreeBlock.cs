/// <summary>
/// free 블록. 예: free(s). 함수 몸통에 놓는다. 1틱을 쓴다.
/// <para>
/// - 값 칸의 포인터가 가리키는 객체를 heap에서 해제하고 칸을 돌려준다. 화면의 오브젝트는 없애지 않는다.<br/>
/// - 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터)이면 InvalidPointer로 실패한다(플레이어 코드 에러).<br/>
/// - 값 칸이 포인터가 아니면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class FreeBlock : Block
{
	readonly ValueBlock pointerOriginal;
	// 이 실행에서 쓰는 값 칸. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public FreeBlock(ValueBlock pointer)
	{
		pointerOriginal = pointer;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(pointerOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		RuntimeValue value = slots[0];
		if (value.Type?.Kind != VariableTypeKind.Pointer)
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		int pointer = value.AsPointer();
		if (!context.Memory.Heap.Free(pointer))
		{
			// 스킬의 Free와 달리 SkillFailed로 내지 않는다. 플레이어가 짠 free이므로 언어 규칙 표가 분류하는 플레이어 코드 에러다
			context.RecordFailure(FunctionFailure.InvalidPointer);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
