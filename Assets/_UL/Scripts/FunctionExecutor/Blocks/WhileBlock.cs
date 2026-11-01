/// <summary>
/// while 블록. 예: while (x &lt; 3) { ... }. 조건이 참인 동안 { }를 반복한다.
/// <para>
/// - 틱, break, continue, 범위 규칙은 LoopBlock을 따른다. 조건 검사 한 번 = 1틱.<br/>
/// - 조건으로 바꾸는 규칙은 논리 블록과 같다(C: 0이 아니면 참, NULL이 아니면 참). 조건으로 쓸 수 없는 값이면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class WhileBlock : LoopBlock
{
	readonly ValueBlock conditionOriginal;
	// 이번 검사의 조건 value slot. 검사마다 새로 만든다
	ValueSlots conditionSlot;

	public WhileBlock(ValueBlock condition, params Block[] body) : base(body)
	{
		conditionOriginal = condition;
	}

	protected override BlockResult Check(FunctionContext context, out bool loop)
	{
		loop = false;
		conditionSlot ??= new ValueSlots(conditionOriginal);
		BlockResult result = conditionSlot.Step(context);
		if (result != BlockResult.Done) return result;

		RuntimeValue value = conditionSlot[0];
		conditionSlot = null;
		return LogicBlock.TryGetCondition(context, value, out loop) ? BlockResult.Done : BlockResult.Failed;
	}

	protected override void OnCancel(FunctionContext context)
	{
		base.OnCancel(context);
		conditionSlot?.Cancel(context);
	}
}
