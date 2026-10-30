/// <summary>
/// 대입 블록. 예: x = 5. 값 칸을 읽어 지금 함수의 지역 변수 값을 바꾼다. 1틱을 쓴다.
/// <para>
/// - 값 칸의 블록이 여러 틱을 쓰면(예: 값을 돌려주는 스킬) 그 블록이 끝날 때까지 기다린다.<br/>
/// - 변수가 없으면 UndeclaredVariable, 값의 타입이 변수의 타입과 다르면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class AssignBlock : Block
{
	readonly string variableName;
	// 값 칸(원본)
	readonly ValueBlock valueOriginal;
	// 이 실행에서 쓰는 값 칸의 복사본. 원본은 여러 작업이 함께 쓰므로 실행을 시작할 때 만든다
	ValueBlock value;

	public AssignBlock(string variableName, ValueBlock value)
	{
		this.variableName = variableName;
		valueOriginal = value;
	}

	protected override void OnStart(FunctionTask task)
	{
		value = (ValueBlock)valueOriginal.Clone();
	}

	protected override BlockResult Step(FunctionTask task)
	{
		BlockResult result = value.Execute(task);
		if (result != BlockResult.Done) return result;

		// 대입 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = task.CurrentVariables;
		if (!variables.Contains(variableName))
		{
			task.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}
		if (!variables.TrySet(variableName, value.Result))
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		value?.Cancel(task);
	}
}
