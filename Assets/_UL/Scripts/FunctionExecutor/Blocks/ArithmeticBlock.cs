/// <summary>
/// 연산 블록. 예: x + 2. value slot 두 개를 계산해 값 하나를 내놓는다. 1틱을 쓴다.
/// <para>
/// - expression으로 쓴 블록이 있으면 그 블록들이 왼쪽부터 자기 틱을 쓴 뒤, 다음 틱에 계산한다.<br/>
/// - 계산 규칙(타입이 섞일 때, 0으로 나누기 등)은 NumericRules와 캐릭터 언어의 LanguageRule을 따른다.<br/>
/// - 계산할 수 없는 타입이면 TypeMismatch, 0으로 나누면 DivideByZero로 실패한다.
/// </para>
/// </summary>
public class ArithmeticBlock : ValueBlock
{
	readonly ArithmeticOperator op;
	readonly ValueBlock leftOriginal;
	readonly ValueBlock rightOriginal;
	// 이 실행에서 쓰는 value slot들. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public ArithmeticBlock(ValueBlock left, ArithmeticOperator op, ValueBlock right)
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

		if (!NumericRules.TryCalculate(op, slots[0], slots[1], context.Rule, out RuntimeValue value, out FunctionFailure failure))
		{
			context.RecordFailure(failure);
			return BlockResult.Failed;
		}

		Result = value;
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
