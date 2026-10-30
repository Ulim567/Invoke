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
	/// 이 실행의 지역 변수. 실행을 시작할 때(OnStart) 새로 만들어지며, 시작하기 전에는 null이다.
	/// </summary>
	// 필드에 미리 만들어 두지 않는다. Clone은 얕은 복사라서, 같은 함수를 두 번 시전하면 두 작업이 같은 저장소를 함께 쓰게 되기 때문
	internal VariableStore Variables { get; private set; }

	/// <summary>
	/// 위에서부터 차례로 실행할 블록들(원본).
	/// </summary>
	public IReadOnlyList<Block> Blocks { get; }

	/// <summary>
	/// 안에 든 함수 블록들의 쿨타임을 더한 값. 같은 스킬을 여러 번 넣으면 그만큼 늘어난다.
	/// </summary>
	// 반복문 같은 문법 블록 안의 함수는 아직 세지 않는다. 몇 번 실행될지 미리 알 수 없으므로 블록 실행 이슈에서 정한다
	public override int CooldownTicks
	{
		get
		{
			int sum = 0;
			foreach (Block block in Blocks)
				if (block is FunctionBlock function) sum += function.CooldownTicks;
			return sum;
		}
	}

	public UserFunction(string name, params Block[] blocks)
	{
		Name = name;
		Blocks = blocks;
	}

	protected override BlockResult Step(FunctionTask task)
	{
		// 블록이 하나도 없는 함수도 최소 1틱은 쓰게 한다
		if (Blocks.Count == 0) return BlockResult.Done;

		if (runningBlock == null)
		{
			Block next = Blocks[blockIndex];

			// 비동기 함수는 끝나기를 기다리지 않는다. 새 작업으로 시전하고, 이 블록은 1틱을 쓴 것으로 보고 다음 블록으로 넘어간다
			// 원본을 그대로 넘겨도 Cast가 복사본을 만들어 실행한다
			// 조건문, for, while 블록이 생기면 그 블록들도 안쪽 블록을 실행하므로 같은 처리가 필요하다. 그때 이 처리를 한 함수로 빼서 함께 쓴다
			if (next is PredefinedFunction function && function.IsAsync)
			{
				task.Executor.CastUnchecked(function);
				blockIndex++;
				return blockIndex >= Blocks.Count ? BlockResult.Done : BlockResult.Running;
			}

			// 원본은 여러 작업이 함께 쓰므로, 실행을 시작할 때 복사본을 만들어 진행 상황을 따로 갖게 한다
			runningBlock = next.Clone();
		}

		BlockResult result = runningBlock.Execute(task);
		if (result == BlockResult.Running) return BlockResult.Running;

		runningBlock = null;

		// 안쪽에서 실패했으면 이 함수도 끝까지 실행하지 못하므로 바깥에 그대로 알린다
		if (result == BlockResult.Failed) return BlockResult.Failed;

		// 안쪽에서 return을 만났으면 남은 블록을 실행하지 않는다. return은 이 함수에서 멈추므로 바깥에는 Done으로 알린다
		if (result == BlockResult.Returned) return BlockResult.Done;

		blockIndex++;
		return blockIndex >= Blocks.Count ? BlockResult.Done : BlockResult.Running;
	}

	protected override void OnStart(FunctionTask task)
	{
		Variables = new VariableStore();
	}

	protected override void OnCancel(FunctionTask task)
	{
		// 실행 중인 안쪽 블록도 함께 끊어서, 안쪽 블록들도 정리할 기회를 갖게 한다
		runningBlock?.Cancel(task);
	}
}
