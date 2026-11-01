/// <summary>
/// 선언 블록. 예: int x = 3, int x. 지금 함수의 지역 변수에 변수 하나를 만든다. 1틱을 쓴다.
/// <para>
/// - initializer가 있으면 그 값을, 비어 있으면 캐릭터 언어의 기본값(LanguageRuleData)을 넣는다.<br/>
/// - initializer에 쓴 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱에 변수를 만든다.<br/>
/// - 초기값은 캐릭터 언어의 숫자 타입 변환 규칙으로 변수 타입에 맞춘다(C: float f = 3 → 3.0, int x = 0.5 → 0).<br/>
/// - 같은 이름이 이미 있으면 DuplicateVariable, 초기값을 변수 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.
/// </para>
/// </summary>
public class DeclareBlock : Block
{
	readonly string name;
	readonly VariableType type;
	// initializer(원본). 비워 둔 칸이면 null
	readonly ValueBlock initialValueOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;

	public DeclareBlock(string name, VariableType type, ValueBlock initialValue)
	{
		this.name = name;
		this.type = type;
		initialValueOriginal = initialValue;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(initialValueOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		LanguageRuleData rule = context.Rule;
		RuntimeValue value;
		if (initialValueOriginal != null)
		{
			if (!NumericRules.TryConvertForStore(initialValueOriginal, slots[0], type, rule, out value))
			{
				context.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}
		}
		// 다룰 수 없는 타입은 FunctionDataConverter가 미리 거르므로 여기까지 오지 않는다. 온다면 LanguageRuleData가 콘솔 오류를 낸다
		else if (!rule.TryGetDefaultValue(type, out value))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		// 선언 블록은 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
		VariableStore variables = context.CurrentVariables;
		if (variables.Contains(name))
		{
			context.RecordFailure(FunctionFailure.DuplicateVariable);
			return BlockResult.Failed;
		}
		if (!variables.TryDeclare(name, type, value))
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
