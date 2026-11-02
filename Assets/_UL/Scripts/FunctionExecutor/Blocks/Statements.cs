using System.Collections.Generic;

/// <summary>
/// statement 여러 개(블록 목록 하나)를 위에서부터 차례로 실행하는 도우미. 사용자 정의 함수의 function body, 조건문·반복문의 { } 안이 쓴다.
/// <para>
/// 사용법<br/>
/// - 블록의 OnStart에서 new Statements(블록 목록 원본, 범위를 열지)로 만든다. 블록 복사본마다 따로 가져야 하기 때문이다.<br/>
/// - 블록의 Step에서 Step(context)를 부른다. Done이면 모든 statement가 끝났고, Running이면 다음 틱에 다시 부른다.<br/>
///   그 밖의 결과(Failed, Returned, Break, Continue)는 안쪽 블록이 돌려준 그대로다. 부른 블록이 어떻게 할지 정한다.<br/>
/// - 블록의 OnCancel에서 Cancel(context)를 부른다.
/// </para>
/// <para>
/// - statement 하나가 끝나면 다음 statement는 다음 틱에 시작한다.<br/>
/// - 블록 목록에 바로 들어 있는 비동기 함수(IsAsync)는 끝나기를 기다리지 않는다. 새 작업으로 시전하고, 1틱을 쓴 것으로 보고 다음 statement로 넘어간다.<br/>
///   단, 같은 스킬이 실행 중이거나 쿨타임 중이면 쓸 수 있을 때까지 기다렸다가 시전한다.<br/>
/// - opensScope가 true이면 처음 Step에서 지금 함수의 지역 변수에 범위를 열고, 끝나거나 끊길 때 닫는다. 그 안에서 선언한 변수는 닫을 때 사라진다(C의 { }).
/// </para>
/// </summary>
internal class Statements
{
	// 블록 목록(원본). 여러 작업이 함께 쓰므로 실행할 때 복사본을 만든다
	readonly IReadOnlyList<Block> blocks;
	readonly bool opensScope;
	int index;
	// 지금 실행 중인 statement의 복사본. 이 statement가 끝나야 다음으로 넘어간다
	Block running;
	// 범위를 연 지역 변수 저장소. 열지 않았거나 이미 닫았으면 null이다
	// 닫을 때 지금 문맥의 저장소가 아니라 연 저장소를 닫는다. 안쪽 사용자 정의 함수가 끊기는 도중에는 맨 위 저장소가 다를 수 있기 때문
	VariableStore scope;
	// 처음 Step을 불렀는지. 범위는 처음 한 번만 연다
	bool started;

	public Statements(IReadOnlyList<Block> blocks, bool opensScope)
	{
		this.blocks = blocks;
		this.opensScope = opensScope;
	}

	public BlockResult Step(FunctionContext context)
	{
		if (!started)
		{
			started = true;
			// 범위를 여는 statement는 사용자 정의 함수 안에만 있으므로 지역 변수가 언제나 있다
			if (opensScope)
			{
				scope = context.CurrentVariables;
				scope.OpenScope();
			}
		}

		BlockResult result = StepBlocks(context);
		if (result != BlockResult.Running) CloseScope();
		return result;
	}

	public void Cancel(FunctionContext context)
	{
		// 안쪽을 먼저 끊는다. 안쪽 사용자 정의 함수가 자기 지역 변수를 먼저 내리게 하기 위함
		running?.Cancel(context);
		running = null;
		CloseScope();
	}

	BlockResult StepBlocks(FunctionContext context)
	{
		// 비어 있는 블록 목록은 바로 끝난다. 바깥 블록이 자기 틱을 쓴다
		if (index >= blocks.Count) return BlockResult.Done;

		if (running == null)
		{
			Block next = blocks[index];

			// 원본을 그대로 넘겨도 Cast가 복사본을 만들어 실행한다
			if (next is FunctionBlock function && function.IsAsync)
			{
				// 같은 스킬이 실행 중이거나 쿨타임 중이면 쓸 수 있을 때까지 기다렸다가 요청한다
				// 바로 요청하면 반복문이 틱마다 기다리는 작업을 하나씩 쌓아, 쿨타임이 끝날 때마다 차례로 실행되기 때문
				if (!function.CanStartNow(context)) return BlockResult.Running;

				context.RequestAsyncCast(function);
				index++;
				return index >= blocks.Count ? BlockResult.Done : BlockResult.Running;
			}

			running = next.Clone();
		}

		BlockResult result = running.Execute(context);
		if (result == BlockResult.Running) return BlockResult.Running;

		running = null;

		// 실패, return, break, continue는 남은 statement를 실행하지 않고 바깥에 그대로 알린다
		if (result != BlockResult.Done) return result;

		index++;
		return index >= blocks.Count ? BlockResult.Done : BlockResult.Running;
	}

	void CloseScope()
	{
		scope?.CloseScope();
		scope = null;
	}
}
