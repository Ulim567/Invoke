/// <summary>
/// array 원소 쓰기 블록. 예: arr[i] = 5. 1틱을 쓴다.
/// <para>
/// - index, 쓸 값을 왼쪽부터 실행한다. 거기 쓴 블록(연산 등)이 있으면 그 블록들이 자기 틱을 쓴 뒤, 다음 틱에 원소를 바꾼다.<br/>
/// - 쓸 값은 캐릭터 언어의 숫자 타입 변환 규칙으로 원소 타입에 맞춘다(대입 블록과 같음).<br/>
/// - array 변수가 없으면 UndeclaredVariable, array가 아니거나 index가 정수 타입이 아니거나 값을 원소 타입으로 바꿀 수 없으면 TypeMismatch, 범위 밖이면 IndexOutOfRange로 실패한다.
/// </para>
/// </summary>
// 대입 블록에 index 칸을 더하지 않고 따로 둔 이유: 대입 블록의 변수 칸은 변수 이름만 받으므로, 섞으면 한 블록이 두 가지 모양이 되어 편집 화면과 변환기가 경우를 나눠야 하기 때문
public class ArraySetBlock : Block
{
	readonly string arrayName;
	// value slot(원본). index, 쓸 값 순서
	readonly ValueBlock indexOriginal;
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public ArraySetBlock(string arrayName, ValueBlock index, ValueBlock value)
	{
		this.arrayName = arrayName;
		indexOriginal = index;
		valueOriginal = value;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(indexOriginal, valueOriginal);
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
		if (!NumericRules.TryConvertForStore(valueOriginal, slots[1], array.ElementType, context.Rule, out RuntimeValue value))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		array.Set(index, value);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
