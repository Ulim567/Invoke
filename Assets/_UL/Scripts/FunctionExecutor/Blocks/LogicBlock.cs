/// <summary>
/// 논리 블록. 예: a &amp;&amp; b, a || b. value slot 두 개를 조건(참·거짓)으로 바꿔 bool 값 하나를 내놓는다. 1틱을 쓴다.
/// <para>
/// - 왼쪽 value slot을 먼저 실행한다. &amp;&amp;의 왼쪽이 거짓이거나 ||의 왼쪽이 참이면 오른쪽 value slot을 실행하지 않는다(C의 short-circuit). 오른쪽에 쓴 블록은 틱도 쓰지 않는다.<br/>
/// - 조건으로 바꾸는 규칙은 NumericRules.TryConvertToCondition과 캐릭터 언어의 LanguageRule을 따른다(C: 0이 아니면 참, NULL이 아니면 참).<br/>
/// - 조건으로 바꿀 수 없는 값이면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class LogicBlock : ValueBlock
{
	readonly LogicOperator op;
	readonly ValueBlock leftOriginal;
	readonly ValueBlock rightOriginal;
	// 이 실행에서 쓰는 value slot들. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	// 오른쪽은 왼쪽 값으로 결과가 정해지지 않을 때만 만든다. null이면 아직 왼쪽을 실행하는 중이다
	ValueSlots leftSlot;
	ValueSlots rightSlot;

	public LogicBlock(ValueBlock left, LogicOperator op, ValueBlock right)
	{
		leftOriginal = left;
		this.op = op;
		rightOriginal = right;
	}

	protected override void OnStart(FunctionContext context)
	{
		leftSlot = new ValueSlots(leftOriginal);
		rightSlot = null;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		if (rightSlot == null)
		{
			BlockResult leftResult = leftSlot.Step(context);
			if (leftResult != BlockResult.Done) return leftResult;
			if (!TryGetCondition(context, leftSlot[0], out bool left)) return BlockResult.Failed;

			// &&는 왼쪽이 거짓이면, ||는 왼쪽이 참이면 결과가 왼쪽 값으로 정해진다
			if (left == (op == LogicOperator.Or))
			{
				Result = RuntimeValue.FromBool(left);
				return BlockResult.Done;
			}
			rightSlot = new ValueSlots(rightOriginal);
		}

		BlockResult rightResult = rightSlot.Step(context);
		if (rightResult != BlockResult.Done) return rightResult;
		if (!TryGetCondition(context, rightSlot[0], out bool right)) return BlockResult.Failed;

		// 여기까지 오면 결과는 오른쪽 값과 같다 (true && x = x, false || x = x)
		Result = RuntimeValue.FromBool(right);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		leftSlot?.Cancel(context);
		rightSlot?.Cancel(context);
	}

	// 조건으로 바꾸지 못하면 TypeMismatch를 기록한다
	internal static bool TryGetCondition(FunctionContext context, RuntimeValue value, out bool condition)
	{
		if (NumericRules.TryConvertToCondition(value, context.Rule, out condition)) return true;

		context.RecordFailure(FunctionFailure.TypeMismatch);
		return false;
	}
}
