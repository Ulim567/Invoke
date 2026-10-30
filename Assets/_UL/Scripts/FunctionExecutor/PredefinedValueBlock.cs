using UnityEngine;

/// <summary>
/// 값 칸에 끼운, 값을 돌려주는 사전 정의 함수 블록. 예: int n = CountSwords();
/// <para>
/// - 파라미터 값 칸을 실행한 뒤 함수를 TickCount만큼 실행하고, 함수가 Return으로 정한 값을 내놓는다. 바깥 블록은 그 다음 틱에 진행한다.<br/>
/// - 반환 값은 함수의 ReturnType으로 맞춘다(캐릭터 언어의 숫자 타입 변환 규칙). 맞출 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 함수가 Return을 부르지 않고 끝나면 콘솔 오류를 내고 MissingReturnValue로 실패한다(스킬 작성자의 실수).<br/>
/// - 반환 타입이 Void인 함수, 비동기 함수는 FunctionDataConverter가 값 칸에 넣지 않는다.
/// </para>
/// </summary>
public class PredefinedValueBlock : ValueBlock
{
	readonly PredefinedFunction function;
	readonly ValueBlock[] argumentOriginals;
	// 이 실행의 함수 호출. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	PredefinedCall call;

	public PredefinedValueBlock(PredefinedFunction function, params ValueBlock[] arguments)
	{
		this.function = function;
		argumentOriginals = arguments;
	}

	protected override void OnStart(FunctionTask task)
	{
		call = new PredefinedCall(function, argumentOriginals);
	}

	protected override BlockResult Step(FunctionTask task)
	{
		BlockResult result = call.Step(task);
		if (result != BlockResult.Done) return result;

		if (!call.Running.TryGetReturnValue(out RuntimeValue value))
		{
			Debug.LogError($"{function.Name}: 반환 타입이 {function.ReturnType.Kind}인데 Return을 부르지 않고 끝났습니다.");
			task.RecordFailure(FunctionFailure.MissingReturnValue);
			return BlockResult.Failed;
		}
		if (!NumericRules.TryConvertForStore(value, function.ReturnType, task.Executor.Rule, out RuntimeValue converted))
		{
			task.RecordFailure(FunctionFailure.TypeMismatch);
			return BlockResult.Failed;
		}

		Result = converted;
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		call?.Cancel(task);
	}
}
