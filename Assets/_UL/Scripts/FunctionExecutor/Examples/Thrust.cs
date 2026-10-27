using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// C 스킬 작성 규칙(docs/systems/predefined-function.md)으로 만든 예시 스킬 「검 찌르기」.<br/>
/// 20틱 동안 진행하고 10틱째에 찌르기 판정을 낸다. 판정과 데미지는 아직 없어서 로그만 남긴다.
/// <para>
/// 실제 검 찌르기는 C 스킬 담당이 이 파일을 바탕으로 판정과 데미지를 붙여 만든다.
/// </para>
/// </summary>
public class Thrust : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.Thrust;
	public override string Name => "검 찌르기";
	public override int TickCount => 20;

	// 단일 대상 스킬이므로 대상 파라미터 하나를 받는다. 값을 실제로 받는 일은 「함수 실행 중 대상 입력 기다리기」 이슈
	public override IReadOnlyList<ParameterData> Parameters => new[]
	{
		new ParameterData("target", new VariableType(VariableTypeKind.Target)),
	};

	protected override void OnTick(FunctionTask task, int tick)
	{
		if (tick == 10) Debug.Log($"{task.Caster.name}: 검 찌르기 판정 (예시, {tick}틱째)");
	}

	protected override void OnCancel(FunctionTask task)
	{
		// 실제 스킬은 여기서 만들어 둔 오브젝트를 정리한다. 예시는 끊겼다는 로그만 남긴다
		Debug.Log($"{task.Caster.name}: 검 찌르기 끊김 (OnCancel)");
	}
}
