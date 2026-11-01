/// <summary>
/// Not 블록. 예: !a. value slot 하나를 조건(참·거짓)으로 바꾼 뒤 뒤집은 bool 값을 내놓는다. 1틱을 쓴다.
/// <para>
/// - 조건으로 바꾸는 규칙은 논리 블록과 같다(C: !5 → false, !NULL → true).<br/>
/// - 조건으로 바꿀 수 없는 값이면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class NotBlock : ValueBlock
{
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public NotBlock(ValueBlock value)
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
		if (!LogicBlock.TryGetCondition(context, slots[0], out bool condition)) return BlockResult.Failed;

		Result = RuntimeValue.FromBool(!condition);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
