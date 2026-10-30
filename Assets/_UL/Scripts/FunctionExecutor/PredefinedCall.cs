using System.Collections.Generic;

/// <summary>
/// 사전 정의 함수 하나를 부르는 실행 한 번. 함수 몸통의 함수 블록(CallPredefinedBlock)과 값 칸의 함수 블록(PredefinedValueBlock)이 함께 쓴다.
/// <para>
/// - 파라미터 값 칸을 왼쪽부터 실행한 뒤, 그 값을 넘긴 함수 복사본을 실행한다.<br/>
/// - 비운 값 칸은 「값 없음」으로 넘긴다. 값을 파라미터 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 비동기 함수(IsAsync)는 새 작업으로 시전하고 그 틱에 Done을 돌려준다.
/// </para>
/// <para>
/// 블록의 OnStart에서 만든다. 블록 복사본마다 따로 가져야 하기 때문이다.
/// </para>
/// </summary>
internal class PredefinedCall
{
	// 부를 함수(원본). PredefinedFunctionRegistry가 보관하는 객체이므로 직접 실행하지 않는다
	readonly PredefinedFunction function;
	readonly ValueBlock[] argumentOriginals;
	readonly ValueSlots slots;

	/// <summary>
	/// 실행 중인 함수 복사본. 값 칸을 다 실행하기 전과 비동기 함수는 null이다.
	/// </summary>
	public PredefinedFunction Running { get; private set; }

	public PredefinedCall(PredefinedFunction function, ValueBlock[] arguments)
	{
		this.function = function;
		argumentOriginals = arguments;
		slots = new ValueSlots(arguments);
	}

	public BlockResult Step(FunctionTask task)
	{
		if (Running == null)
		{
			BlockResult result = slots.Step(task);
			if (result != BlockResult.Done) return result;

			if (!TryMakeArguments(task, out RuntimeValue?[] arguments))
			{
				task.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}

			PredefinedFunction copy = (PredefinedFunction)function.Clone();
			copy.SetArguments(arguments);

			// 비동기 함수는 끝나기를 기다리지 않는다. 새 작업으로 시전하고 이번 틱으로 끝난다
			if (function.IsAsync)
			{
				task.Executor.CastUnchecked(copy);
				return BlockResult.Done;
			}
			Running = copy;
		}

		// 함수 복사본을 틱마다 진행한다. 스택 프레임은 함수가 실행을 시작할 때 스스로 쓴다
		return Running.Execute(task);
	}

	public void Cancel(FunctionTask task)
	{
		slots.Cancel(task);
		Running?.Cancel(task);
	}

	// 값 칸의 결과를 파라미터 타입으로 바꿔 함수에 넘길 배열을 만든다
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
}
