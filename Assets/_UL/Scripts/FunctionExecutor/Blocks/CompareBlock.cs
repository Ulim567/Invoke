/// <summary>
/// 비교 블록. 예: x &lt; 3. value slot 두 개를 비교해 bool 값 하나를 내놓는다. 1틱을 쓴다.
/// <para>
/// - expression으로 쓴 블록이 있으면 그 블록들이 왼쪽부터 자기 틱을 쓴 뒤, 다음 틱에 비교한다.<br/>
/// - 비교 규칙(타입이 섞일 때, 포인터와 NULL, string 등)은 NumericRules와 캐릭터 언어의 LanguageRule을 따른다.<br/>
/// - 비교할 수 없는 타입이면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class CompareBlock : ValueBlock
{
	readonly CompareOperator op;
	readonly ValueBlock leftOriginal;
	readonly ValueBlock rightOriginal;
	// 이 실행에서 쓰는 value slot들. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public CompareBlock(ValueBlock left, CompareOperator op, ValueBlock right)
	{
		leftOriginal = left;
		this.op = op;
		rightOriginal = right;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(leftOriginal, rightOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		if (!NumericRules.TryCompare(op, leftOriginal, slots[0], rightOriginal, slots[1], context.Rule, out bool compared))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		Result = RuntimeValue.FromBool(compared);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
