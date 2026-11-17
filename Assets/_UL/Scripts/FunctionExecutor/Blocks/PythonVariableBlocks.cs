using System.Collections.Generic;

/// <summary>
/// Python 함수 하나의 이름 규칙 (#532). 함수 데이터에서 FunctionDataConverter가 만들어 UserFunction에 넘긴다.
/// <para>
/// - Locals: 이 함수의 지역 이름(파라미터, 대입·for·del하는 이름 가운데 global로 적지 않은 것). 읽을 때 지역 변수에서만 찾는다.<br/>
/// - Globals: global 블록에 적은 이름. 읽기·대입·del 모두 전역 변수에서 한다.<br/>
/// - 둘 다 아닌 이름: 읽기만 하므로 전역 변수에서 찾는다.
/// </para>
/// </summary>
// 블록 위치와 상관없이 함수 전체에서 미리 모으는 이유: Python은 함수를 실행하기 전에 함수 안의 모든 대입을 보고 지역 이름을 정하기 때문
// 그래서 x = x + 1처럼 전역 x를 읽고 지역 x를 만들려는 코드는 실제 Python처럼 NameError(UnboundLocalError)가 난다
internal class PythonNames
{
	public readonly HashSet<string> Locals;
	public readonly HashSet<string> Globals;

	public PythonNames(HashSet<string> locals, HashSet<string> globals)
	{
		Locals = locals ?? new HashSet<string>();
		Globals = globals ?? new HashSet<string>();
	}
}

/// <summary>
/// global 블록 (Python, #532). 예: global count. 1틱을 쓰고 아무 일도 하지 않는다.<br/>
/// 이름 규칙은 함수를 바꿀 때 PythonNames에 미리 담기므로, 실행 중에는 할 일이 없다.
/// </summary>
// 블록을 남기는 이유: 플레이어가 놓은 블록 하나 = 1틱 규칙(#90)을 지키기 위함
public class GlobalBlock : Block
{
	protected override BlockResult Step(FunctionContext context) => BlockResult.Done;
}

/// <summary>
/// del 블록 (Python, #532). 예: del orb. 이름을 지운다. 1틱을 쓴다.
/// <para>
/// - 지역 이름이면 지역 변수를, global로 적은 이름이면 전역 변수를 지운다.<br/>
/// - 지운 뒤 그 이름을 읽으면 NameError(UndeclaredVariable)다. 다시 대입하면 다시 만들어진다.<br/>
/// - 지금 없는 이름을 지우면 NameError다.<br/>
/// - 그 이름만 가리키던 heap 객체(구슬, 리스트)는 이 statement가 끝날 때 가비지 컬렉션이 해제한다.
/// </para>
/// </summary>
public class DelBlock : Block
{
	readonly string variableName;

	public DelBlock(string variableName)
	{
		this.variableName = variableName;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		VariableStore variables = context.FindVariables(variableName);
		if (variables == null || !variables.Remove(variableName))
		{
			context.RecordFailure(FunctionFailure.UndeclaredVariable);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}
}
