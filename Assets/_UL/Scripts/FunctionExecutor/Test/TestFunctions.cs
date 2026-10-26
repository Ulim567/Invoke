/// <summary>
/// 실행기 테스트용 함수. 30틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionA : PredefinedFunction
{
	public override string Name => "TestFunctionA";
	public override int TickCount => 30;
}

/// <summary>
/// 실행기 테스트용 함수. 50틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionB : PredefinedFunction
{
	public override string Name => "TestFunctionB";
	public override int TickCount => 50;
}
