/// <summary>
/// 값 칸에 넣은 변수(예: x). 실행되면 지금 실행 중인 사용자 정의 함수의 지역 변수에서 그 값을 읽어, 그 틱 안에서 바로 내놓는다.<br/>
/// 그 이름의 변수가 없으면 UndeclaredVariable로 실패한다.
/// </summary>
public class VariableValue : ValueBlock
{
	readonly string variableName;

	public VariableValue(string variableName)
	{
		this.variableName = variableName;
	}

	// 값 칸에 넣은 변수는 블록이 아니므로 틱을 쓰지 않는다
	internal override bool UsesTick => false;

	protected override BlockResult Step(FunctionTask task)
	{
		// 사용자 정의 함수 밖이면 지역 변수가 없으므로, 없는 변수를 읽은 것과 같게 처리한다
		VariableStore variables = task.CurrentVariables;
		if (variables == null || !variables.TryGet(variableName, out RuntimeValue value))
		{
			task.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}

		Result = value;
		return BlockResult.Done;
	}
}
