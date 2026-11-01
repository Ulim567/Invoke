using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사전 정의 함수(C 스킬, 공통 함수 등)를 부르는 블록. statement(예: Thrust(target);)와 expression(예: int n = CountSwords();의 CountSwords()) 어디에든 놓인다.
/// <para>
/// - argument를 왼쪽부터 실행한 뒤, 그 값을 넘긴 스킬 복사본을 실행한다. 스킬은 TryGetArgument로 값을 읽는다.<br/>
/// - 비운 value slot은 「값 없음」으로 넘긴다. 값을 파라미터 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 대상(Target) argument가 「대상 없음」이거나 죽었거나 오브젝트가 없어졌으면 InvalidTarget으로 실패한다.<br/>
/// - 틱: expression으로 쓴 블록이 없으면 스킬의 TickCount와 같다(슬롯에서 바로 시전했을 때와 같음). 끼운 블록이 있으면 그 블록들의 틱이 앞에 더해진다.<br/>
/// - 같은 스킬이 실행 중이거나 쿨타임 중이면, argument를 계산한 뒤 쓸 수 있을 때까지 실패 없이 기다린다. 기다린 틱도 더해진다.<br/>
/// - 반환 타입이 Void가 아니면 스킬이 끝난 뒤 반환 값을 반환 타입으로 맞춰 Result에 넣는다. statement로 놓이면 아무도 Result를 읽지 않으므로 값은 버려진다.<br/>
/// - 스킬이 Return을 부르지 않았거나 반환 값을 반환 타입으로 맞출 수 없으면, 콘솔 오류를 내고 작업을 InternalError로 끝낸다(개발자 책임).<br/>
/// - 비동기 스킬(IsAsync)은 새 작업으로 시전하고, 이 블록은 1틱 뒤 끝난다. 반환 값을 받을 수 없으므로 FunctionDataConverter가 expression으로 쓰지 못하게 한다.<br/>
///   같은 스킬이 실행 중이거나 쿨타임 중이면 이 블록이 쓸 수 있을 때까지 기다렸다가 시전한다.
/// </para>
/// </summary>
// statement로 부른 호출과 expression으로 부른 호출을 한 클래스로 두는 이유: C에서 두 호출은 같은 함수 호출이고, 반환 값을 쓰는지만 다르기 때문
// 반환 값을 statement에서도 확인하는 이유: 같은 스킬이 놓인 자리에 따라 성공하거나 실패하지 않게 하기 위함
public class PredefinedCallBlock : ValueBlock
{
	// 부를 함수(원본). PredefinedFunctionRegistry가 보관하는 객체이므로 직접 실행하지 않는다
	readonly PredefinedFunction function;
	// argument(원본). Parameters와 같은 순서이고, 비운 칸은 null
	readonly ValueBlock[] argumentOriginals;
	// 이 실행에서 쓰는 value slot과 스킬 복사본. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;
	// 실행 중인 스킬 복사본. value slot을 다 실행하기 전과 비동기 스킬은 null이다
	PredefinedFunction running;
	// 시전을 기다리는 비동기 스킬 복사본. argument를 넘긴 채로 쓸 수 있을 때까지 들고 있는다
	PredefinedFunction waitingAsync;

	public PredefinedCallBlock(PredefinedFunction function, params ValueBlock[] arguments)
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

			if (!TryMakeArguments(context, out RuntimeValue?[] arguments, out FunctionFailure failure))
			{
				context.RecordFailure(failure);
				return BlockResult.Failed;
			}

			PredefinedFunction copy = (PredefinedFunction)function.Clone();
			copy.SetArguments(arguments);

			if (function.IsAsync) waitingAsync = copy;
			else running = copy;
		}

		// 비동기 스킬은 끝나기를 기다리지 않는다. 새 작업으로 시전하고 이번 틱으로 끝난다
		if (waitingAsync != null)
		{
			// 같은 스킬이 실행 중이거나 쿨타임 중이면 쓸 수 있을 때까지 기다렸다가 요청한다
			// 바로 요청하면 반복문이 틱마다 기다리는 작업을 하나씩 쌓아, 쿨타임이 끝날 때마다 차례로 실행되기 때문
			if (!context.Cooldowns.CanUseSkill(function.Id)) return BlockResult.Running;

			context.RequestAsyncCast(waitingAsync);
			waitingAsync = null;
			return BlockResult.Done;
		}

		// 스킬 복사본을 틱마다 진행한다. 스택 프레임은 스킬이 실행을 시작할 때 스스로 쓴다
		BlockResult result = running.Execute(context);
		if (result != BlockResult.Done) return result;

		if (function.ReturnType.Kind == VariableTypeKind.Void) return BlockResult.Done;
		return TryTakeReturnValue(context) ? BlockResult.Done : BlockResult.Failed;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
		running?.Cancel(context);
	}

	// value slot의 결과를 파라미터 타입으로 바꿔 스킬에 넘길 배열을 만든다
	// 바꿀 수 없으면 TypeMismatch, 쓸 수 없는 대상(대상 없음, 죽음, 없어짐)이면 InvalidTarget을 failure에 담는다
	// 대상을 여기서 검사하는 이유: 스킬마다 검사 코드를 넣지 않고, 스킬에 넘기는 한 곳에서 막기 위함
	bool TryMakeArguments(FunctionContext context, out RuntimeValue?[] arguments, out FunctionFailure failure)
	{
		failure = FunctionFailure.TypeMismatch;
		IReadOnlyList<ParameterData> parameters = function.Parameters;
		arguments = new RuntimeValue?[parameters.Count];
		for (int i = 0; i < argumentOriginals.Length && i < parameters.Count; i++)
		{
			if (argumentOriginals[i] == null) continue;
			if (!NumericRules.TryConvertForStore(argumentOriginals[i], slots[i], parameters[i].Type, context.Rule, out RuntimeValue value)) return false;
			if (value.Type.Kind == VariableTypeKind.Target && !value.IsUsableTarget)
			{
				failure = FunctionFailure.InvalidTarget;
				return false;
			}
			arguments[i] = value;
		}
		return true;
	}

	// 스킬이 Return으로 정한 값을 반환 타입으로 맞춰 Result에 넣는다
	// 맞추지 못하면 TypeMismatch가 아니라 개발자 책임으로 기록한다. 반환 값은 스킬 작성자(개발자)가 정하므로, 플레이어의 compile error로 보이지 않게 하기 위함
	bool TryTakeReturnValue(FunctionContext context)
	{
		if (!running.TryGetReturnValue(out RuntimeValue value))
		{
			Debug.LogError($"{function.Name}: 반환 타입이 {function.ReturnType.Kind}인데 Return을 부르지 않고 끝났습니다.");
			context.RecordInternalError();
			return false;
		}
		if (!NumericRules.TryConvertForStore(value, function.ReturnType, context.Rule, out RuntimeValue converted))
		{
			Debug.LogError($"{function.Name}: Return으로 넘긴 {value.Type?.Kind} 값을 반환 타입 {function.ReturnType.Kind}(으)로 바꿀 수 없습니다.");
			context.RecordInternalError();
			return false;
		}

		Result = converted;
		return true;
	}
}
