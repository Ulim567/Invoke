using System.Collections.Generic;

/// <summary>
/// 대상 확인 함수 (#474). 넘긴 대상을 스킬에 넘길 수 있으면 true, 아니면 false를 돌려준다. 예: if (!is_valid_target(b)) b = get_bug(); thrust(b);
/// <para>
/// - false: 「대상 없음」(처음 값을 비운 Target 변수, 고를 몬스터가 없던 get_bug()), 죽은 몬스터, 오브젝트가 없어진 몬스터.<br/>
/// - 판정은 스킬에 넘길 때 InvalidTarget을 내는 검사(RuntimeValue.IsUsableTarget)와 같다. 그래서 true이면 그 대상을 스킬에 넘겨도 InvalidTarget이 나지 않는다.<br/>
/// - Target은 포인터가 아니므로 NULL과 비교하지 않고 이 함수로 확인한다.
/// </para>
/// </summary>
public class IsValidTarget : PredefinedFunction
{
	static readonly IReadOnlyList<ParameterData> TargetParameter =
		new[] { new ParameterData("target", VariableType.TargetOf(null)) };

	public override string Id => PredefinedFunctionIds.IsValidTarget;
	public override string Name => "IsValidTarget";
	public override int TickCount => 1;
	public override IReadOnlyList<ParameterData> Parameters => TargetParameter;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Bool);

	// 쓸 수 없는 대상을 받아도 시작해야 false를 돌려줄 수 있다. 기본 검사는 시작 전에 InvalidTarget으로 멈추게 하기 때문
	protected override bool ValidatesTargets => false;

	protected override void OnTick(FunctionContext context, int tick)
	{
		// 값을 넘기지 않았으면(슬롯에서 바로 시전) 확인할 대상이 없으므로 false
		bool valid = TryGetArgument("target", out RuntimeValue value) && value.IsUsableTarget;
		Return(RuntimeValue.FromBool(valid));
	}
}
