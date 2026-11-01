/// <summary>
/// free 블록. 예: free(s). function body에 놓는다. 1틱을 쓴다.
/// <para>
/// - value slot의 포인터가 가리키는 객체를 heap에서 해제하고 칸을 돌려준다. 화면의 오브젝트는 없애지 않는다.<br/>
/// - NULL(NullPointer, 직접 입력한 NULL, 언어 규칙이 ZeroIsNull일 때 직접 입력한 0)이면 C처럼 아무 일도 하지 않고 끝난다.<br/>
/// - 쓸 수 없는 포인터(해제한 포인터, 누수된 포인터)이면 InvalidPointer로 실패한다(플레이어 코드 에러).<br/>
/// - value slot이 포인터가 아니면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class FreeBlock : Block
{
	readonly ValueBlock pointerOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
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

		// free(0)은 C처럼 free(NULL)이다
		RuntimeValue value = NumericRules.NullIfZeroLiteral(pointerOriginal, slots[0], context.Rule);
		if (value.Type?.Kind != VariableTypeKind.Pointer)
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		// HeapStorage.Free는 NullPointer에도 false를 돌려주므로 먼저 거른다. 스킬이 쓰는 PredefinedFunction.Free는 바꾸지 않는다
		int pointer = value.AsPointer();
		if (pointer == HeapStorage.NullPointer) return BlockResult.Done;
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
