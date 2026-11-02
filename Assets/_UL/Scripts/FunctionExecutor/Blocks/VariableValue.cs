/// <summary>
/// value slot에 넣은 변수(예: x). 실행되면 지금 실행 중인 사용자 정의 함수의 지역 변수에서 그 값을 읽고, 지역 변수에 없으면 캐릭터의 전역 변수에서 읽어, 그 틱 안에서 바로 내놓는다.<br/>
/// 두 곳 모두에 그 이름의 변수가 없으면 UndeclaredVariable로 실패한다.
/// </summary>
public class VariableValue : ValueBlock
{
	readonly string variableName;

	// value slot에 넣은 변수는 블록이 아니므로 틱을 쓰지 않는다
	internal override bool UsesTick => false;

	public VariableValue(string variableName)
	{
		this.variableName = variableName;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		VariableStore variables = context.FindVariables(variableName);
		if (variables == null || !variables.TryGet(variableName, out RuntimeValue value))
		{
			context.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}

		Result = value;
		return BlockResult.Done;
	}
}
