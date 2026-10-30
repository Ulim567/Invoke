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

	protected override BlockResult Step(FunctionTask task)
	{
		task.CurrentVariables.TryDeclare(name, value.Type, value);
		return BlockResult.Done;
	}
}

/// <summary>
/// 값 블록 테스트용 읽기 블록. 값 칸 하나를 실행하고, 읽은 값을 onRead로 넘긴다. 값 칸이 실패하면 이 블록도 실패한다.<br/>
/// 값 칸을 가진 실제 블록(선언, 대입 등)이 값 블록을 쓰는 방식과 같다.
/// </summary>
public class TestReadBlock : Block
{
	readonly ValueBlock valueOriginal;
	readonly Action<RuntimeValue> onRead;
	// 이 실행에서 쓰는 값 칸의 복사본. 원본은 여러 작업이 함께 쓰므로 실행을 시작할 때 만든다
	ValueBlock value;

	public TestReadBlock(ValueBlock value, Action<RuntimeValue> onRead)
	{
		valueOriginal = value;
		this.onRead = onRead;
	}

	protected override void OnStart(FunctionTask task)
	{
		value = (ValueBlock)valueOriginal.Clone();
	}

	protected override BlockResult Step(FunctionTask task)
	{
		BlockResult result = value.Execute(task);
		if (result != BlockResult.Done) return result;

		onRead(value.Result);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionTask task)
	{
		value?.Cancel(task);
	}
}
