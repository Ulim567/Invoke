using System.Collections.Generic;

/// <summary>
/// 함수(사전 정의 함수, 사용자 정의 함수)를 부르는 블록. statement(예: Thrust(target);)와 expression(예: int n = CountSwords();의 CountSwords()) 어디에든 놓인다.
/// <para>
/// - argument를 왼쪽부터 실행한 뒤, 그 값을 넘긴 함수 복사본을 실행한다.<br/>
/// - 비운 value slot은 「값 없음」으로 넘긴다. 값을 파라미터 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 대상(Target) argument가 「대상 없음」이거나 죽었거나 오브젝트가 없어졌으면 스킬이 시작하기 직전에 InvalidTarget으로 실패한다(PredefinedFunction이 검사).<br/>
/// - 틱: expression으로 쓴 블록이 없으면 그 함수의 틱과 같다(슬롯에서 바로 시전했을 때와 같음). 끼운 블록이 있으면 그 블록들의 틱이 앞에 더해진다.<br/>
/// - 같은 스킬이 실행 중이거나 쿨타임 중이면, argument를 계산한 뒤 쓸 수 있을 때까지 실패 없이 기다린다. 기다린 틱도 더해진다.<br/>
/// - 반환 타입이 Void가 아니면 함수가 끝난 뒤 반환 값을 Result에 넣는다. statement로 놓이면 아무도 Result를 읽지 않으므로 값은 버려진다.<br/>
/// - 반환 값이 잘못되었을 때(return 누락 등)의 처리는 불린 함수가 끝날 때 스스로 한다(FunctionBlock.TryValidateEnd).<br/>
/// - 비동기 함수(IsAsync)는 새 작업으로 시전하고, 이 블록은 1틱 뒤 끝난다. 반환 값을 받을 수 없으므로 FunctionDataConverter가 expression으로 쓰지 못하게 한다.<br/>
///   같은 스킬이 실행 중이거나 쿨타임 중이면 이 블록이 쓸 수 있을 때까지 기다렸다가 시전한다.
/// </para>
/// </summary>
// statement로 부른 호출과 expression으로 부른 호출을 한 클래스로 두는 이유: C에서 두 호출은 같은 함수 호출이고, 반환 값을 쓰는지만 다르기 때문
// 사전 정의 함수와 사용자 정의 함수를 한 클래스로 부르는 이유: argument 계산, 반환 값 받기, 끊기가 같으므로 코드를 두 벌로 두지 않기 위함
public class CallBlock : ValueBlock
{
	// 부를 함수(원본). 여러 작업이 함께 쓰므로 직접 실행하지 않는다
	readonly FunctionBlock function;
	// argument(원본). Parameters와 같은 순서이고, 비운 칸은 null
	readonly ValueBlock[] argumentOriginals;
	// 이 실행에서 쓰는 value slot과 함수 복사본. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;
	// 실행 중인 함수 복사본. value slot을 다 실행하기 전과 비동기 함수는 null이다
	FunctionBlock running;
	// 시전을 기다리는 비동기 함수 복사본. argument를 넘긴 채로 쓸 수 있을 때까지 들고 있는다
	FunctionBlock waitingAsync;

	public CallBlock(FunctionBlock function, params ValueBlock[] arguments)
	{
		this.function = function;
		argumentOriginals = arguments;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(argumentOriginals);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		if (running == null && waitingAsync == null)
		{
			BlockResult slotResult = slots.Step(context);
			if (slotResult != BlockResult.Done) return slotResult;

			if (!TryMakeArguments(context, out RuntimeValue?[] arguments))
			{
				context.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}

			FunctionBlock copy = (FunctionBlock)function.Clone();
			copy.SetArguments(arguments);

			if (function.IsAsync) waitingAsync = copy;
			else running = copy;
		}

		// 비동기 함수는 끝나기를 기다리지 않는다. 새 작업으로 시전하고 이번 틱으로 끝난다
		if (waitingAsync != null)
		{
			// 같은 스킬이 실행 중이거나 쿨타임 중이면 쓸 수 있을 때까지 기다렸다가 요청한다
			// 바로 요청하면 반복문이 틱마다 기다리는 작업을 하나씩 쌓아, 쿨타임이 끝날 때마다 차례로 실행되기 때문
			if (!waitingAsync.CanStartNow(context)) return BlockResult.Running;

			context.RequestAsyncCast(waitingAsync);
			waitingAsync = null;
			return BlockResult.Done;
		}

		// 함수 복사본을 틱마다 진행한다. 스택 프레임은 함수가 실행을 시작할 때 스스로 쓴다
		BlockResult result = running.Execute(context);
		if (result != BlockResult.Done) return result;

		// 반환 값은 함수가 끝날 때 이미 반환 타입으로 맞춰 두었다(TryValidateEnd). 맞추지 못했으면 Done이 아니라 Failed로 끝났다
		if (function.ReturnType.Kind != VariableTypeKind.Void) Result = running.ReturnValue.Value;
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
		running?.Cancel(context);
	}

	// value slot의 결과를 파라미터 타입으로 바꿔 함수에 넘길 배열을 만든다
	// 쓸 수 없는 대상은 여기서 거르지 않는다. 스킬이 시작하기 직전에 PredefinedFunction.TryValidateStart가 InvalidTarget으로 막는다
	bool TryMakeArguments(FunctionContext context, out RuntimeValue?[] arguments)
	{
		IReadOnlyList<ParameterData> parameters = function.Parameters;
		arguments = new RuntimeValue?[parameters.Count];
		for (int i = 0; i < argumentOriginals.Length && i < parameters.Count; i++)
		{
			if (argumentOriginals[i] == null) continue;
			if (!NumericRules.TryConvertForStore(argumentOriginals[i], slots[i], parameters[i].Type, context.Rule, out RuntimeValue value)) return false;
			arguments[i] = value;
		}
		return true;
	}
}
