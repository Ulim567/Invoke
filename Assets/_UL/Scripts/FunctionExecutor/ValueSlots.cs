/// <summary>
/// 블록 하나가 가진 값 칸들을 왼쪽부터 차례로 실행하는 도우미. 선언, 대입, 연산 블록이 쓴다.
/// <para>
/// 사용법<br/>
/// - 블록의 OnStart에서 new ValueSlots(값 칸 원본들)로 만든다. 블록 복사본마다 따로 가져야 하기 때문이다.<br/>
/// - 블록의 Step에서 Step(task)를 부른다. Done이면 모든 값 칸이 끝났고 이번 틱에 블록이 자기 일을 하면 된다. Running, Failed이면 그대로 돌려준다.<br/>
/// - 블록의 OnCancel에서 Cancel(task)를 부른다.
/// </para>
/// <para>
/// 틱 규칙(ValueBlock): 값 칸에 끼운 블록이 자기 틱을 쓰고 끝나면, 다음 값 칸이나 바깥 블록의 일은 다음 틱으로 넘긴다.
/// </para>
/// </summary>
internal class ValueSlots
{
	// 이 실행에서 쓰는 값 칸의 복사본. 원본은 여러 작업이 함께 쓰므로 만들 때 복사한다. 비워 둔 값 칸은 null
	readonly ValueBlock[] running;
	int index;

	public ValueSlots(params ValueBlock[] originals)
	{
		running = new ValueBlock[originals.Length];
		for (int i = 0; i < originals.Length; i++)
			running[i] = (ValueBlock)originals[i]?.Clone();
	}

	/// <summary>
	/// i번째 값 칸의 결과. Step이 Done을 돌려준 뒤에만 쓴다.
	/// </summary>
	public RuntimeValue this[int i] => running[i].Result;

	public BlockResult Step(FunctionTask task)
	{
		while (index < running.Length)
		{
			ValueBlock slot = running[index];
			if (slot == null)
			{
				index++;
				continue;
			}

			BlockResult result = slot.Execute(task);
			if (result != BlockResult.Done) return result;

			index++;
			// 이 값 칸이 이번 틱을 썼으므로, 남은 일은 다음 틱에 한다
			if (slot.UsesTick) return BlockResult.Running;
		}
		return BlockResult.Done;
	}

	public void Cancel(FunctionTask task)
	{
		foreach (ValueBlock slot in running)
			slot?.Cancel(task);
	}
}
