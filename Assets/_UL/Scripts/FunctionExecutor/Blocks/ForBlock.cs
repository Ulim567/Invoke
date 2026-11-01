/// <summary>
/// for 블록. for (int i = start; i &lt; end; i++) { ... }
/// <para>
/// - 틱, break, continue, 범위 규칙은 LoopBlock을 따른다. 검사 한 번 = 1틱.<br/>
/// - 첫 검사: start를 int로 바꿔 i를 선언한 뒤 i &lt; end를 검사한다. 다음 검사부터: i++ 뒤에 검사한다. 모두 검사와 같은 틱에 한다.<br/>
/// - end는 검사마다 다시 읽는다. { }가 i를 바꾸면 바뀐 값으로 계속한다. i가 int 범위를 넘으면 반대쪽 끝으로 돌아간다.<br/>
/// - i는 for 블록만의 범위에 만든다. for가 끝나면 사라진다. 바깥과 같은 이름은 캐릭터 언어가 허용할 때만 만든다(LanguageRuleData.AllowShadowing).<br/>
/// - start를 int로 바꿀 수 없거나 i &lt; end를 비교할 수 없으면 TypeMismatch, i를 선언할 수 없으면 DuplicateVariable로 실패한다.
/// </para>
/// </summary>
public class ForBlock : LoopBlock
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	readonly string indexName;
	readonly ValueBlock startOriginal;
	readonly ValueBlock endOriginal;
	// i를 선언한 지역 변수 저장소. i의 범위를 닫을 때 쓴다. 아직 선언하지 않았으면 null이다
	VariableStore indexScope;
	// 첫 검사의 start, 이번 검사의 end value slot. 블록 복사본마다 따로 가져야 하므로 실행 중에 만든다
	ValueSlots startSlot;
	ValueSlots endSlot;
	// 다음 검사 전에 i++를 해야 하면 true. 한 바퀴가 끝날 때 켠다
	bool incrementPending;

	public ForBlock(string indexName, ValueBlock start, ValueBlock end, params Block[] body) : base(body)
	{
		this.indexName = indexName;
		startOriginal = start;
		endOriginal = end;
	}

	protected override void OnStart(FunctionContext context)
	{
		startSlot = new ValueSlots(startOriginal);
	}

	protected override BlockResult Check(FunctionContext context, out bool loop)
	{
		loop = false;
		if (indexScope == null)
		{
			BlockResult startResult = startSlot.Step(context);
			if (startResult != BlockResult.Done) return startResult;
			if (!TryDeclareIndex(context)) return BlockResult.Failed;
		}
		else if (incrementPending)
		{
			// end에 쓴 expression이 틱을 써도 i++는 한 번만 하도록 바로 끈다
			incrementPending = false;
			indexScope.TryGet(indexName, out RuntimeValue current);
			indexScope.TrySet(indexName, RuntimeValue.FromInt(unchecked(current.AsInt() + 1)));
		}

		endSlot ??= new ValueSlots(endOriginal);
		BlockResult endResult = endSlot.Step(context);
		if (endResult != BlockResult.Done) return endResult;

		RuntimeValue end = endSlot[0];
		endSlot = null;
		indexScope.TryGet(indexName, out RuntimeValue index);
		if (!NumericRules.TryCompare(CompareOperator.Less, null, index, endOriginal, end, context.Rule, out loop))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnLapEnd()
	{
		incrementPending = true;
	}

	protected override void OnEnd(FunctionContext context)
	{
		indexScope?.CloseScope();
	}

	protected override void OnCancel(FunctionContext context)
	{
		base.OnCancel(context);
		startSlot?.Cancel(context);
		endSlot?.Cancel(context);
		indexScope?.CloseScope();
	}

	// i의 범위를 열고 start 값으로 i를 선언한다. 실패하면 실패 종류를 기록하고 false
	// 범위를 먼저 연다. 실패해도 OnCancel이 닫으므로 짝이 어긋나지 않는다
	bool TryDeclareIndex(FunctionContext context)
	{
		LanguageRuleData rule = context.Rule;
		indexScope = context.CurrentVariables;
		indexScope.OpenScope();

		if (!NumericRules.TryConvertForStore(startOriginal, startSlot[0], Int, rule, out RuntimeValue start))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}
		if (!indexScope.CanDeclare(indexName, rule.AllowShadowing))
		{
			context.RecordFailure(FunctionFailure.DuplicateVariable);
			return false;
		}

		indexScope.TryDeclare(indexName, Int, start);
		return true;
	}
}
