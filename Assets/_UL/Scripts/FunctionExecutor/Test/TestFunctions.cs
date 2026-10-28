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
