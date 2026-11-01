/// <summary>
/// 대상 입력 함수. 플레이어가 고른 몬스터 하나를 Target(종류 Bug)으로 돌려준다. 예: Target b = GetBug(); Thrust(b);
/// <para>
/// - 실행되면 작업이 멈추고, 실행기가 InputRequested로 입력을 받는 쪽(SlotCaster)에 알린다.<br/>
/// - 입력을 받는 쪽이 ProvideInput으로 몬스터를 넣으면 다음 틱에 그 몬스터를 돌려주고 끝난다.<br/>
/// - 고를 몬스터가 없어 「대상 없음」이 들어오면 그대로 돌려준다. 그 값을 스킬에 넘기면 InvalidTarget이 난다.
/// </para>
/// </summary>
public class GetBug : InputFunction
{
	public override string Id => PredefinedFunctionIds.GetBug;
	public override string Name => "GetBug";
	// 입력을 받은 뒤 값을 돌려주는 1틱. 기다리는 동안의 틱은 세지 않는다
	public override int TickCount => 1;
	public override VariableType ReturnType => VariableType.TargetOf(TargetKind.Bug);

	protected override void OnStart(FunctionContext context)
	{
		RequestInput(context, VariableType.TargetOf(TargetKind.Bug));
	}

	protected override void OnTick(FunctionContext context, int tick)
	{
		// OnTick은 입력이 들어온 뒤에만 불리므로 값은 언제나 있다
		if (TryTakeInput(context, out RuntimeValue value)) Return(value);
	}
}
