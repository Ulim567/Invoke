/// <summary>
/// 블록 하나가 가진 value slot들을 왼쪽부터 차례로 실행하는 도우미. 선언, 대입, 연산 블록이 쓴다.
/// <para>
/// 사용법<br/>
/// - 블록의 OnStart에서 new ValueSlots(value slot 원본들)로 만든다. 블록 복사본마다 따로 가져야 하기 때문이다.<br/>
/// - 블록의 Step에서 Step(context)를 부른다. Done이면 모든 value slot이 끝났고 이번 틱에 블록이 자기 일을 하면 된다. Running, Failed이면 그대로 돌려준다.<br/>
/// - 블록의 OnCancel에서 Cancel(context)를 부른다.
/// </para>
/// <para>
/// 틱 규칙(ValueBlock): expression으로 쓴 블록이 자기 틱을 쓰고 끝나면, 다음 value slot이나 바깥 블록의 일은 다음 틱으로 넘긴다.
/// </para>
/// </summary>
internal class ValueSlots
{
	// 이 실행에서 쓰는 value slot의 복사본. 원본은 여러 작업이 함께 쓰므로 만들 때 복사한다. 비워 둔 value slot은 null
	readonly ValueBlock[] running;
	int index;

	public ValueSlots(params ValueBlock[] originals)
	{
		running = new ValueBlock[originals.Length];
		for (int i = 0; i < originals.Length; i++)
			running[i] = (ValueBlock)originals[i]?.Clone();
	}

	/// <summary>
	/// i번째 value slot의 결과. Step이 Done을 돌려준 뒤에만 쓴다.
	/// </summary>
	public RuntimeValue this[int i] => running[i].Result;

	/// <summary>
	/// i번째 value slot의 복사본. Step이 Done을 돌려준 뒤에만 쓴다. 비운 칸이면 null.<br/>
	/// 값과 함께 그 값이 어디서 왔는지(직접 입력한 0인지 등)를 다른 블록에 넘길 때 쓴다.
	/// </summary>
	public ValueBlock Get(int i) => running[i];

	public BlockResult Step(FunctionContext context)
	{
		while (index < running.Length)
		{
			ValueBlock slot = running[index];
			if (slot == null)
			{
				index++;
				continue;
			}

			BlockResult result = slot.Execute(context);
			if (result != BlockResult.Done) return result;

			index++;
			// 이 value slot이 이번 틱을 썼으므로, 남은 일은 다음 틱에 한다
			if (slot.UsesTick) return BlockResult.Running;
		}
		return BlockResult.Done;
	}

	public void Cancel(FunctionContext context)
	{
		foreach (ValueBlock slot in running)
			slot?.Cancel(context);
	}
}
