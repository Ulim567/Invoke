using UnityEngine;

/// <summary>
/// 실행기 테스트용 반복 블록. 조건 없이 안쪽 블록을 정해진 횟수만큼 처음부터 다시 실행한다.<br/>
/// 반복문이 스택을 차지하지 않는지 확인하기 위한 블록이다. 실제 for, while 블록은 블록 실행 이슈에서 만든다.
/// <para>
/// 함수 블록이 아니므로 이 블록 자체는 스택을 쓰지 않는다. 안쪽 함수는 한 번 끝날 때마다 프레임을 돌려주므로, 반복 횟수와 상관없이 스택에는 안쪽 프레임 하나만 올라간다.<br/>
/// 안쪽 블록이 비동기 함수인 경우는 다루지 않는다.
/// </para>
/// </summary>
public class TestRepeatBlock : Block
{
	readonly int count;
	readonly Block body;

	int doneCount;
	// 지금 실행 중인 안쪽 블록의 복사본. 이 블록이 끝나야 다음 반복으로 넘어간다
	Block runningBlock;

	public TestRepeatBlock(int count, Block body)
	{
		this.count = count;
		this.body = body;
	}

	protected override BlockResult Step(FunctionTask task)
	{
		// 반복 횟수가 0이어도 최소 1틱은 쓰게 한다
		if (count <= 0) return BlockResult.Done;

		bool iterationStarted = false;
		if (runningBlock == null)
		{
			// 반복할 때마다 원본에서 새 복사본을 만들어, 지난 반복의 진행 상황이 남지 않게 한다
			runningBlock = body.Clone();
			iterationStarted = true;
		}

		BlockResult result = runningBlock.Execute(task);

		// 안쪽 함수가 프레임을 받은 뒤에 찍어야 반복 중의 스택 사용량이 보인다
		if (iterationStarted)
			Debug.Log($"[틱 {task.Executor.CurrentTick}] {task.Name}: 반복 {doneCount + 1}회째 / 스택 {task.Memory.StackUsed}/{task.Memory.Capacity}");

		if (result == BlockResult.Running) return BlockResult.Running;

		runningBlock = null;

		// 오버플로와 return은 반복을 멈추고 바깥에 그대로 알린다. return이면 바깥 함수도 남은 블록을 실행하지 않는다
		if (result == BlockResult.Overflow || result == BlockResult.Returned) return result;

		doneCount++;
		return doneCount >= count ? BlockResult.Done : BlockResult.Running;
	}

	protected override void OnCancel(FunctionTask task)
	{
		// 실행 중인 안쪽 블록도 함께 끊어서, 안쪽 함수가 프레임을 돌려주게 한다
		runningBlock?.Cancel(task);
	}
}
