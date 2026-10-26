using System.Collections.Generic;

/// <summary>
/// 플레이어가 블록 코딩으로 만든 함수. 안에 든 블록들을 위에서부터 하나씩 끝까지 실행한다.
/// <para>
/// 사용법<br/>
/// - new UserFunction("이름", 블록1, 블록2, ...)로 만들고, 실행기의 Cast에 넘기거나 다른 사용자 정의 함수의 블록으로 넣는다.<br/>
/// - 블록에는 사전 정의 함수, 사용자 정의 함수, return 같은 문법 블록을 넣을 수 있다.
/// </para>
/// <para>
/// 동작 시간은 따로 정하지 않는다. 실제로 실행된 블록들의 틱을 더한 만큼 걸린다.
/// </para>
/// </summary>
public class UserFunction : FunctionBlock
{
	int blockIndex;
	// 지금 실행 중인 블록의 복사본. 이 블록이 끝나야 다음 블록으로 넘어간다
	Block runningBlock;

	public override string Name { get; }

	/// <summary>
	/// 위에서부터 차례로 실행할 블록들(원본).
	/// </summary>
	public IReadOnlyList<Block> Blocks { get; }

	public UserFunction(string name, params Block[] blocks)
	{
		Name = name;
		Blocks = blocks;
	}

	protected override BlockResult Step(FunctionTask task)
	{
		// 블록이 하나도 없는 함수도 최소 1틱은 쓰게 한다
		if (Blocks.Count == 0) return BlockResult.Done;

		// 원본은 여러 작업이 함께 쓰므로, 실행을 시작할 때 복사본을 만들어 진행 상황을 따로 갖게 한다
		if (runningBlock == null) runningBlock = Blocks[blockIndex].Clone();

		BlockResult result = runningBlock.Execute(task);
		if (result == BlockResult.Running) return BlockResult.Running;

		runningBlock = null;

		// 안쪽에서 오버플로가 났으면 이 함수도 끝까지 실행하지 못하므로 바깥에 그대로 알린다
		if (result == BlockResult.Overflow) return BlockResult.Overflow;

		// 안쪽에서 return을 만났으면 남은 블록을 실행하지 않는다. return은 이 함수에서 멈추므로 바깥에는 Done으로 알린다
		if (result == BlockResult.Returned) return BlockResult.Done;

		blockIndex++;
		return blockIndex >= Blocks.Count ? BlockResult.Done : BlockResult.Running;
	}

	protected override void OnCancel(FunctionTask task)
	{
		// 실행 중인 안쪽 블록도 함께 끊어서, 안쪽 블록들도 정리할 기회를 갖게 한다
		runningBlock?.Cancel(task);
	}
}
