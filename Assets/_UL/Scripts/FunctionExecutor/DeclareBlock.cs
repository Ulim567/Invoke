/// <summary>
/// 선언 블록. 예: int x = 3, int x. 지금 함수의 지역 변수에 변수 하나를 만든다. 1틱을 쓴다.
/// <para>
/// - 초기값 칸이 있으면 그 값을, 비어 있으면 캐릭터 언어의 기본값(LanguageRule)을 넣는다.<br/>
/// - 초기값 칸의 블록이 여러 틱을 쓰면(예: 값을 돌려주는 스킬) 그 블록이 끝날 때까지 기다린다.<br/>
/// - 같은 이름이 이미 있으면 DuplicateVariable, 초기값의 타입이 다르면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class DeclareBlock : Block
{
	readonly string name;
	readonly VariableType type;
	// 초기값 칸(원본). 비워 둔 칸이면 null
	readonly ValueBlock initialValueOriginal;
	// 이 실행에서 쓰는 초기값 칸의 복사본. 원본은 여러 작업이 함께 쓰므로 실행을 시작할 때 만든다
	ValueBlock initialValue;

	public DeclareBlock(string name, VariableType type, ValueBlock initialValue)
	{
		this.name = name;
		this.type = type;
		initialValueOriginal = initialValue;
	}

	protected override void OnStart(FunctionTask task)
	{
		initialValue = (ValueBlock)initialValueOriginal?.Clone();
	}

	protected override BlockResult Step(FunctionTask task)
	{
		RuntimeValue value;
		if (initialValue != null)
		{
			BlockResult result = initialValue.Execute(task);
			if (result != BlockResult.Done) return result;
			value = initialValue.Result;
		}
		// 다룰 수 없는 타입은 FunctionDataConverter가 미리 거르므로 여기까지 오지 않는다. 온다면 LanguageRule이 콘솔 오류를 낸다
		else if (!task.Executor.TryGetDefaultValue(type, out value))
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		// 선언 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = task.CurrentVariables;
		if (variables.Contains(name))
		{
			task.RecordFailure(FunctionFailure.DuplicateVariable);
			return BlockResult.Failed;
		}
		if (!variables.TryDeclare(name, type, value))
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		initialValue?.Cancel(task);
	}
}
