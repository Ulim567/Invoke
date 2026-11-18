using UnityEngine;

/// <summary>
/// 자세를 다시 정하는 함수의 공통 부분 (#538). 시전 시간이 지난 뒤 자세를 1~100 무작위 숫자로 다시 정하고 그 숫자를 돌려준다.
/// <para>
/// - 시전 시간 2.5초(125 tick), 쿨타임 3초(150 tick). 원하는 자세가 나올 때까지 다시 뽑으면 그만큼 시간이 든다.<br/>
/// - 숫자는 마지막 tick에 정한다. 도중에 끊기면 자세는 그대로다.<br/>
/// - 언어마다 고정 ID만 다르다. C는 CInitStance, Python은 PyInitStance(#546). 효과는 시전자 언어의 자세 규칙(StanceRule.For)이 정한다.
/// </para>
/// </summary>
// 스킬이 아니라 기본 함수다. 스킬 트리 노드가 없고, 슬롯에 직접 넣지 않는다(SlotLoadout.IsEquippableSkill). 그래서 자세의 stack +1칸·자해도 받지 않는다
public abstract class InitStanceFunction : PredefinedFunction
{
	/// <summary>시전 시간(tick). 2.5초</summary>
	public const int CastTicks = 125;
	/// <summary>쿨타임(tick). 3초</summary>
	public const int Cooldown = 150;

	public override int TickCount => CastTicks;
	public override int CooldownTicks => Cooldown;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Int);

	protected override void OnTick(FunctionContext context, int tick)
	{
		if (tick < TickCount) return;
		PlayerStance stance = StanceFunctions.StanceOf(context, Name);
		Return(RuntimeValue.FromInt(stance != null ? stance.Roll() : PlayerStance.NoStance));
	}
}

/// <summary>
/// 지금 자세 숫자를 돌려주는 함수의 공통 부분 (#538). 자세가 없으면 0. 다른 함수와 같은 1 tick만 쓰고 추가 비용은 없다.
/// </summary>
public abstract class GetStanceFunction : PredefinedFunction
{
	public override int TickCount => 1;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Int);

	protected override void OnTick(FunctionContext context, int tick)
	{
		PlayerStance stance = StanceFunctions.StanceOf(context, Name);
		Return(RuntimeValue.FromInt(stance != null ? stance.Stance : PlayerStance.NoStance));
	}
}

/// <summary>C의 자세 다시 정하기 int init_stance() (#538). 예: int n = init_stance(); printf(n);</summary>
public class CInitStance : InitStanceFunction
{
	public override string Id => PredefinedFunctionIds.InitStance;
	public override string Name => "init_stance";
}

/// <summary>C의 자세 확인 int get_stance() (#538). 예: if (get_stance() % 2 == 1) thrust(get_bug());</summary>
public class CGetStance : GetStanceFunction
{
	public override string Id => PredefinedFunctionIds.GetStance;
	public override string Name => "get_stance";
}

/// <summary>Python의 자세 다시 정하기 init_stance() (#546). 예: n = init_stance(); print(n)</summary>
public class PyInitStance : InitStanceFunction
{
	public override string Id => PredefinedFunctionIds.PyInitStance;
	public override string Name => "init_stance";
}

/// <summary>Python의 자세 확인 get_stance() (#546). 예: if get_stance() > 50: magic_missile(get_bug())</summary>
public class PyGetStance : GetStanceFunction
{
	public override string Id => PredefinedFunctionIds.PyGetStance;
	public override string Name => "get_stance";
}

static class StanceFunctions
{
	// 시전한 캐릭터의 자세. 프리팹에 PlayerStance를 붙이지 않았으면 콘솔에 알리고 null(함수는 0을 돌려줌)
	internal static PlayerStance StanceOf(FunctionContext context, string functionName)
	{
		if (context.Caster != null && context.Caster.TryGetComponent(out PlayerStance stance)) return stance;
		Debug.LogError($"{functionName}: 시전한 캐릭터에 PlayerStance 컴포넌트가 없습니다. 플레이어 프리팹 최상위에 PlayerStance를 붙입니다.");
		return null;
	}
}
