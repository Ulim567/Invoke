using System.Collections.Generic;

/// <summary>
/// 사전 정의 함수(C 스킬, 공통 함수 등)를 부르는 함수 블록. 예: Thrust(target)
/// <para>
/// - 파라미터 값 칸을 왼쪽부터 실행한 뒤, 그 값을 넘긴 스킬 복사본을 실행한다. 스킬은 TryGetArgument로 값을 읽는다.<br/>
/// - 틱: 값 칸에 끼운 블록이 없으면 스킬의 TickCount와 같다(슬롯에서 바로 시전했을 때와 같음). 끼운 블록이 있으면 그 블록들의 틱이 앞에 더해진다.<br/>
/// - 비운 값 칸은 「값 없음」으로 넘긴다. 값을 파라미터 타입으로 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 비동기 스킬(IsAsync)은 새 작업으로 시전하고, 이 블록은 1틱 뒤 끝난다.
/// </para>
/// </summary>
public class CallPredefinedBlock : Block
{
	// 부를 스킬(원본). PredefinedFunctionRegistry가 보관하는 객체이므로 직접 실행하지 않는다
	readonly PredefinedFunction function;
	// 파라미터 값 칸(원본). Parameters와 같은 순서이고, 비운 칸은 null
	readonly ValueBlock[] argumentOriginals;
	// 이 실행에서 쓰는 값 칸과 스킬 복사본. 블록 복사본마다 따로 가져야 하므로 실행 중에 만든다
	ValueSlots slots;
	PredefinedFunction running;

	/// <summary>
	/// 부를 스킬의 쿨타임. 이 블록을 담은 사용자 정의 함수의 쿨타임에 더해진다.
	/// </summary>
	internal int CooldownTicks => function.CooldownTicks;

	public CallPredefinedBlock(PredefinedFunction function, params ValueBlock[] arguments)
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
			BlockResult result = slots.Step(task);
			if (result != BlockResult.Done) return result;

			if (!TryMakeArguments(task, out RuntimeValue?[] arguments))
			{
				task.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}

			PredefinedFunction copy = (PredefinedFunction)function.Clone();
			copy.SetArguments(arguments);

			// 비동기 스킬은 끝나기를 기다리지 않는다. 새 작업으로 시전하고 이 블록은 이번 틱으로 끝난다
			if (function.IsAsync)
			{
				task.Executor.CastUnchecked(copy);
				return BlockResult.Done;
			}
			running = copy;
		}

		// 스킬 복사본을 이 블록의 틱마다 진행한다. 스택 프레임은 스킬이 실행을 시작할 때 스스로 쓴다
		return running.Execute(task);
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

	protected override void OnCancel(FunctionTask task)
	{
		slots?.Cancel(task);
		running?.Cancel(task);
	}
}
