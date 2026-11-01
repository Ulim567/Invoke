/// <summary>
/// 대입 블록. 예: x = 5. value slot을 읽어 지금 함수의 지역 변수 값을 바꾼다. 1틱을 쓴다.
/// <para>
/// - expression으로 쓴 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱에 값을 바꾼다.<br/>
/// - 값은 캐릭터 언어의 숫자 타입 변환 규칙으로 변수 타입에 맞춘다(C: float 변수에 3 → 3.0, int 변수에 0.5 → 0).<br/>
/// - 변수가 없으면 UndeclaredVariable, 값을 변수 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - array 변수에는 대입할 수 없다(TypeMismatch). C에서도 arr = other는 compile error다. 원소는 ArraySetBlock으로 바꾼다.
/// </para>
/// </summary>
public class AssignBlock : Block
{
	readonly string variableName;
	// value slot(원본)
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public AssignBlock(string variableName, ValueBlock value)
	{
		this.variableName = variableName;
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

		// 대입 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = context.CurrentVariables;
		if (!variables.TryGetType(variableName, out VariableType type))
		{
			context.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}
		// array는 같은 타입끼리 TryConvertForStore를 통과하므로 따로 막는다. 막지 않으면 두 변수가 같은 원소를 보게 되기 때문
		if (type.Kind == VariableTypeKind.Array
			|| !NumericRules.TryConvertForStore(valueOriginal, slots[0], type, context.Rule, out RuntimeValue value)
			|| !variables.TrySet(variableName, value))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
