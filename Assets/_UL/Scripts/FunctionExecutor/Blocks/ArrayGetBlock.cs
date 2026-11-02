/// <summary>
/// array 원소 읽기 블록. 예: arr[i]. value slot에 끼워 쓰고, 그 원소의 값을 내놓는다. 1틱을 쓴다.
/// <para>
/// - index에 쓴 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱에 원소를 읽는다.<br/>
/// - array 변수가 없으면 UndeclaredVariable, array가 아니거나 index가 정수 타입이 아니면 TypeMismatch, 범위 밖이면 IndexOutOfRange로 실패한다.
/// </para>
/// </summary>
public class ArrayGetBlock : ValueBlock
{
	readonly string arrayName;
	// index value slot(원본)
	readonly ValueBlock indexOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public ArrayGetBlock(string arrayName, ValueBlock index)
	{
		this.arrayName = arrayName;
		indexOriginal = index;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(indexOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		if (!ArrayStorage.TryFind(context, arrayName, out ArrayStorage array)) return BlockResult.Failed;
		if (!ArrayStorage.TryGetIndex(slots[0], context.Rule, out int index))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		if (!array.IsInRange(index))
		{
			context.RecordFailure(FunctionFailure.IndexOutOfRange);
			return BlockResult.Failed;
		}

		Result = array.Get(index);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
