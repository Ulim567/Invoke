using System;

/// <summary>
/// 값 블록 테스트용 선언 블록. 지금 함수의 지역 변수에 변수 하나를 만든다. 1틱을 쓴다.<br/>
/// 실제 선언 블록은 3단계에서 만든다.
/// </summary>
public class TestDeclareBlock : Block
{
	readonly string name;
	readonly RuntimeValue value;

	public TestDeclareBlock(string name, RuntimeValue value)
	{
		this.name = name;
		this.value = value;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		context.CurrentVariables.TryDeclare(name, value.Type, value);
		return BlockResult.Done;
	}
}

/// <summary>
/// 값 블록 테스트용 읽기 블록. value slot 하나를 실행하고, 읽은 값을 onRead로 넘긴다. value slot이 실패하면 이 블록도 실패한다.<br/>
/// value slot을 가진 실제 블록(선언, 대입 등)이 값 블록을 쓰는 방식과 같다.
/// </summary>
public class TestReadBlock : Block
{
	readonly ValueBlock valueOriginal;
	readonly Action<RuntimeValue> onRead;
	// 이 실행에서 쓰는 value slot의 복사본. 원본은 여러 작업이 함께 쓰므로 실행을 시작할 때 만든다
	ValueBlock value;

	public TestReadBlock(ValueBlock value, Action<RuntimeValue> onRead)
	{
		valueOriginal = value;
		this.onRead = onRead;
	}

	protected override void OnStart(FunctionContext context)
	{
		value = (ValueBlock)valueOriginal.Clone();
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = value.Execute(context);
		if (result != BlockResult.Done) return result;

		onRead(value.Result);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		value?.Cancel(context);
	}
}

/// <summary>
/// 테스트용 확인 블록. 실행되면 onRun에 작업을 넘긴다. 1틱을 쓴다.<br/>
/// 함수 중간에 메모리 같은 상태를 확인할 때 블록 사이에 끼워 넣는다.
/// </summary>
public class TestProbeBlock : Block
{
	readonly Action<FunctionContext> onRun;

	public TestProbeBlock(Action<FunctionContext> onRun)
	{
		this.onRun = onRun;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		onRun(context);
		return BlockResult.Done;
	}
}
