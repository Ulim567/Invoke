using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 사전 정의 함수(C 스킬, 공통 함수 등)를 부르는 블록. 함수 본문의 한 줄(예: Thrust(target);)과 값 칸(예: int n = CountSwords();) 어디에든 놓인다.
/// <para>
/// - 파라미터 값 칸을 왼쪽부터 실행한 뒤, 그 값을 넘긴 스킬 복사본을 실행한다. 스킬은 TryGetArgument로 값을 읽는다.<br/>
/// - 비운 값 칸은 「값 없음」으로 넘긴다. 값을 파라미터 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 틱: 값 칸에 끼운 블록이 없으면 스킬의 TickCount와 같다(슬롯에서 바로 시전했을 때와 같음). 끼운 블록이 있으면 그 블록들의 틱이 앞에 더해진다.<br/>
/// - 반환 타입이 Void가 아니면 스킬이 끝난 뒤 반환 값을 반환 타입으로 맞춰 Result에 넣는다. 함수 본문의 한 줄로 놓이면 아무도 Result를 읽지 않으므로 값은 버려진다.<br/>
/// - 스킬이 Return을 부르지 않았거나 반환 값을 반환 타입으로 맞출 수 없으면, 콘솔 오류를 내고 MissingReturnValue로 실패한다(스킬 작성자의 실수).<br/>
/// - 비동기 스킬(IsAsync)은 새 작업으로 시전하고, 이 블록은 1틱 뒤 끝난다. 반환 값을 받을 수 없으므로 FunctionDataConverter가 값 칸에 넣지 않는다.
/// </para>
/// </summary>
// 함수 본문의 호출과 값 칸의 호출을 한 클래스로 두는 이유: C에서 두 호출은 같은 함수 호출이고, 반환 값을 쓰는지만 다르기 때문
// 반환 값을 함수 본문에서도 확인하는 이유: 같은 스킬이 놓인 자리에 따라 성공하거나 실패하지 않게 하기 위함
public class PredefinedCallBlock : ValueBlock
{
	// 부를 함수(원본). PredefinedFunctionRegistry가 보관하는 객체이므로 직접 실행하지 않는다
	readonly PredefinedFunction function;
	// 파라미터 값 칸(원본). Parameters와 같은 순서이고, 비운 칸은 null
	readonly ValueBlock[] argumentOriginals;
	// 이 실행에서 쓰는 값 칸과 스킬 복사본. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;
	// 실행 중인 스킬 복사본. 값 칸을 다 실행하기 전과 비동기 스킬은 null이다
	PredefinedFunction running;

	public PredefinedCallBlock(PredefinedFunction function, params ValueBlock[] arguments)
	{
		this.function = function;
		argumentOriginals = arguments;
	}

	protected override void OnStart(FunctionTask task)
	{
		slots = new ValueSlots(argumentOriginals);
	}

	protected override BlockResult Step(FunctionTask task)
	{
		if (running == null)
		{
			BlockResult slotResult = slots.Step(task);
			if (slotResult != BlockResult.Done) return slotResult;

			if (!TryMakeArguments(task, out RuntimeValue?[] arguments))
			{
				task.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}

			PredefinedFunction copy = (PredefinedFunction)function.Clone();
			copy.SetArguments(arguments);

			// 비동기 스킬은 끝나기를 기다리지 않는다. 새 작업으로 시전하고 이번 틱으로 끝난다
			if (function.IsAsync)
			{
				task.Executor.CastUnchecked(copy);
				return BlockResult.Done;
			}
			running = copy;
		}

		// 스킬 복사본을 틱마다 진행한다. 스택 프레임은 스킬이 실행을 시작할 때 스스로 쓴다
		BlockResult result = running.Execute(task);
		if (result != BlockResult.Done) return result;

		if (function.ReturnType.Kind == VariableTypeKind.Void) return BlockResult.Done;
		return TryTakeReturnValue(task) ? BlockResult.Done : BlockResult.Failed;
	}

	protected override void OnCancel(FunctionTask task)
	{
		slots?.Cancel(task);
		running?.Cancel(task);
	}

	// 값 칸의 결과를 파라미터 타입으로 바꿔 스킬에 넘길 배열을 만든다
	bool TryMakeArguments(FunctionTask task, out RuntimeValue?[] arguments)
	{
		IReadOnlyList<ParameterData> parameters = function.Parameters;
		arguments = new RuntimeValue?[parameters.Count];
		for (int i = 0; i < argumentOriginals.Length && i < parameters.Count; i++)
		{
			if (argumentOriginals[i] == null) continue;
			if (!NumericRules.TryConvertForStore(slots[i], parameters[i].Type, task.Executor.Rule, out RuntimeValue value)) return false;
			arguments[i] = value;
		}
		return true;
	}

	// 스킬이 Return으로 정한 값을 반환 타입으로 맞춰 Result에 넣는다
	// 맞추지 못하면 TypeMismatch가 아니라 MissingReturnValue로 기록한다. 반환 값은 스킬 작성자가 정하므로, 플레이어 코드의 잘못(compile error)으로 보이지 않게 하기 위함
	bool TryTakeReturnValue(FunctionTask task)
	{
		if (!running.TryGetReturnValue(out RuntimeValue value))
		{
			Debug.LogError($"{function.Name}: 반환 타입이 {function.ReturnType.Kind}인데 Return을 부르지 않고 끝났습니다.");
			task.RecordFailure(FunctionFailure.MissingReturnValue);
			return false;
		}
		if (!NumericRules.TryConvertForStore(value, function.ReturnType, task.Executor.Rule, out RuntimeValue converted))
		{
			Debug.LogError($"{function.Name}: Return으로 넘긴 {value.Type?.Kind} 값을 반환 타입 {function.ReturnType.Kind}(으)로 바꿀 수 없습니다.");
			task.RecordFailure(FunctionFailure.MissingReturnValue);
			return false;
		}

		Result = converted;
		return true;
	}
}
