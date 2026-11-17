using System;

/// <summary>
/// 기다리기 블록. 예: wait(0.5초); function body에 놓는다 (#502).
/// <para>
/// - 적은 초만큼 틱을 쓰고 다음 블록으로 넘어간다. 블록이 쓰는 틱 전체가 적은 시간이고, 나누어떨어지지 않으면 올린다. 예: 1틱이 0.02초일 때 wait(0.5초) 25틱, wait(0.03초) 2틱.<br/>
/// - 0초, 음수: 최소 1틱을 쓰고 끝난다.<br/>
/// - value slot에 쓴 블록(연산 등)이 있으면 그 블록이 자기 틱을 쓴 뒤, 다음 틱부터 센다.<br/>
/// - 값은 float 변수에 넣을 때와 같은 규칙으로 바꾼다. 바꿀 수 없으면 TypeMismatch로 실패한다.<br/>
/// - 기다리는 동안 작업은 Running 그대로이고, 이 블록을 담은 함수의 프레임도 그대로 남는다.
/// </para>
/// </summary>
// 입력 대기(WaitingInput)처럼 작업을 멈추지 않는다. 끝나는 틱이 시작할 때 정해지므로, 그 틱까지 Running을 돌려주기만 하면 되기 때문
// 기다리는 틱은 이 블록을 담은 함수의 틱에 들어간다. 입력 대기와 달리 기다리기가 블록 자신의 동작이기 때문
public class WaitBlock : Block
{
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);

	readonly ValueBlock secondsOriginal;
	// 이 실행에서 쓰는 value slot. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	ValueSlots slots;
	// 기다릴 전체 틱 수. value slot이 끝난 틱에 정한다. 0이면 아직 정하지 않았다
	int totalTicks;
	// 지금까지 쓴 틱 수. value slot이 끝난 틱부터 센다
	int elapsedTicks;

	public WaitBlock(ValueBlock seconds)
	{
		secondsOriginal = seconds;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(secondsOriginal);
		totalTicks = 0;
		elapsedTicks = 0;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		if (totalTicks == 0)
		{
			BlockResult result = slots.Step(context);
			if (result != BlockResult.Done) return result;

			if (!NumericRules.TryConvertForStore(secondsOriginal, slots[0], Float, context.Rule, context.Family, out RuntimeValue seconds))
			{
				context.RecordFailure(FunctionFailure.TypeMismatch);
				return BlockResult.Failed;
			}
			totalTicks = ToTicks(seconds.AsFloat(), context.SecondsPerTick);
		}

		elapsedTicks++;
		return elapsedTicks >= totalTicks ? BlockResult.Done : BlockResult.Running;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}

	/// <summary>
	/// seconds초를 기다리는 데 쓸 틱 수. 최소 1이다.
	/// </summary>
	// 적은 시간보다 짧게 기다리지 않도록 올린다. 0초·음수는 블록 하나 = 최소 1틱 규칙대로 1틱이다
	// 새 실패 종류를 만들지 않고 1틱으로 넘어가는 것은 기획 결정이다(#502). 실제 C의 sleep(-1)은 부호 없는 정수로 바뀌어 아주 오래 잔다
	internal static int ToTicks(float seconds, float secondsPerTick)
	{
		if (!(secondsPerTick > 0)) return 1;

		// 0.5 / 0.02처럼 나누어떨어져야 할 값이 float 오차로 25.0000005가 되어 26틱으로 올라가지 않게, 아주 작은 차이는 버린다
		double ticks = Math.Ceiling((double)seconds / secondsPerTick - 1e-4);
		// NaN도 이 비교에서 걸러져 1틱이 된다
		if (!(ticks >= 1)) return 1;
		// 상한은 두지 않는다(끝나지 않는 반복문과 같음). 다만 int로 셀 수 있는 만큼까지만 센다
		return ticks >= int.MaxValue ? int.MaxValue : (int)ticks;
	}
}
