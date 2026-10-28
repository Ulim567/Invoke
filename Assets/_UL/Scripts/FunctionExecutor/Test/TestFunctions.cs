using UnityEngine;

/// <summary>
/// 실행기 테스트용 함수. 30틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionA : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestFunctionA;
	public override string Name => "TestFunctionA";
	public override int TickCount => 30;
}

/// <summary>
/// 실행기 테스트용 함수. 50틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionB : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestFunctionB;
	public override string Name => "TestFunctionB";
	public override int TickCount => 50;
}

/// <summary>
/// 실행기 테스트용 비동기 함수. 다른 함수 안에서 불리면 새 작업으로 20틱 동안 진행한다.
/// </summary>
public class TestAsyncFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestAsyncFunction;
	public override string Name => "TestAsyncFunction";
	public override int TickCount => 20;
	public override bool IsAsync => true;
}

/// <summary>
/// 시전 전 메모리 검사 테스트용 함수. 프레임이 21칸(호출 1 + 지역 변수 20)이라 기본 메모리 10칸으로는 시전할 수 없다.
/// </summary>
public class TestHeavyFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestHeavyFunction;
	public override string Name => "TestHeavyFunction";
	public override int TickCount => 10;
	public override int LocalVariableCount => 20;
}

/// <summary>
/// 스킬 에러 테스트용 공격 스킬. 시작할 때 맨손이면 UnarmedError를 낸다. 10틱 동안 진행한다.
/// </summary>
public class TestAttackFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestAttackFunction;
	public override string Name => "TestAttackFunction";
	public override int TickCount => 10;

	protected override void OnStart(FunctionTask task)
	{
		if (task.Caster.GetComponent<TempSwordState>().IsUnarmed) Fail(new UnarmedError());
	}

	// 실제 스킬은 여기서 시전자에게 피해를 주는 등 이 스킬만의 결과를 적는다. 테스트는 불렸는지만 확인한다
	protected override void OnFail(FunctionTask task, SkillError error)
	{
		Debug.Log($"{Name}: OnFail ({error.Message})");
	}
}

/// <summary>
/// 스킬 에러 테스트용 소환검 스킬. 시작할 때 소환검이 없으면 NoSummonedSwordError를 낸다. 10틱 동안 진행한다.
/// </summary>
public class TestSummonedSwordFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestSummonedSwordFunction;
	public override string Name => "TestSummonedSwordFunction";
	public override int TickCount => 10;

	protected override void OnStart(FunctionTask task)
	{
		if (task.Caster.GetComponent<TempSwordState>().SummonedSwordCount <= 0) Fail(new NoSummonedSwordError());
	}
}

/// <summary>
/// Stop 테스트용 함수. 50틱짜리지만 5틱째에 Stop을 불러 에러 없이 일찍 끝난다.
/// </summary>
public class TestStopFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestStopFunction;
	public override string Name => "TestStopFunction";
	public override int TickCount => 50;

	protected override void OnTick(FunctionTask task, int tick)
	{
		if (tick == 5) Stop();
	}
}
