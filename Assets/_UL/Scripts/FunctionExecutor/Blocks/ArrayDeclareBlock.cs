/// <summary>
/// 배열 만들기 블록. 예: int arr[5]. 지금 함수의 지역 변수에 array 하나를 만든다. 길이와 상관없이 1틱을 쓴다.
/// <para>
/// - 모든 원소를 캐릭터 언어의 기본값(LanguageRuleData)으로 채운다. C의 지역 array는 처음 값이 정해지지 않지만, 초기값을 비운 변수와 같은 규칙을 따른다.<br/>
/// - 길이는 함수 데이터에 직접 입력한 1 이상의 정수다(FunctionDataConverter가 확인). 메모리 칸은 함수가 시작할 때 프레임에 이미 들어 있다.<br/>
/// - 범위와 이름 규칙은 선언 블록과 같다. 같은 범위에 같은 이름이 있으면 DuplicateVariable, { }가 닫히면 사라진다.
/// </para>
/// </summary>
public class ArrayDeclareBlock : Block
{
	readonly string name;
	readonly VariableType elementType;
	readonly int length;

	public ArrayDeclareBlock(string name, VariableType elementType, int length)
	{
		this.name = name;
		this.elementType = elementType;
		this.length = length;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		LanguageRuleData rule = context.Rule;
		// 다룰 수 없는 원소 타입은 FunctionDataConverter가 미리 거르므로 여기까지 오지 않는다. 온다면 LanguageRuleData가 콘솔 오류를 낸다
		if (!rule.TryGetDefaultValue(elementType, out RuntimeValue initialValue))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		// 배열 만들기 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = context.CurrentVariables;
		if (!variables.CanDeclare(name, rule.AllowShadowing))
		{
			context.RecordFailure(FunctionFailure.DuplicateVariable);
			return BlockResult.Failed;
		}

		// 실행할 때마다 새 저장소를 만든다. 반복문 안이면 바퀴마다 새 array이고, 같은 함수를 두 번 시전해도 원소가 섞이지 않는다
		VariableType arrayType = VariableType.ArrayOf(elementType);
		RuntimeValue array = RuntimeValue.FromArray(arrayType, new ArrayStorage(elementType, length, initialValue));
		if (!variables.TryDeclare(name, arrayType, array))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}
		return BlockResult.Done;
	}
}
