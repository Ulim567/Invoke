/// <summary>
/// 대입 블록. 예: x = 5. 값 칸을 읽어 지금 함수의 지역 변수 값을 바꾼다. 1틱을 쓴다.
/// <para>
/// - 값 칸에 끼운 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱에 값을 바꾼다.<br/>
/// - 값은 캐릭터 언어의 숫자 타입 변환 규칙으로 변수 타입에 맞춘다(C: float 변수에 3 → 3.0, int 변수에 0.5 → 0).<br/>
/// - 변수가 없으면 UndeclaredVariable, 값을 변수 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class AssignBlock : Block
{
	readonly string variableName;
	// 값 칸(원본)
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 값 칸. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public AssignBlock(string variableName, ValueBlock value)
	{
		this.variableName = variableName;
		valueOriginal = value;
	}

	protected override void OnStart(FunctionTask task)
	{
		slots = new ValueSlots(valueOriginal);
	}

	protected override BlockResult Step(FunctionTask task)
	{
		BlockResult result = slots.Step(task);
		if (result != BlockResult.Done) return result;

		// 대입 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = task.CurrentVariables;
		if (!variables.TryGetType(variableName, out VariableType type))
		{
			task.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}
		if (!NumericRules.TryConvertForStore(slots[0], type, task.Executor.Rule, out RuntimeValue value)
			|| !variables.TrySet(variableName, value))
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		slots?.Cancel(task);
	}
}
