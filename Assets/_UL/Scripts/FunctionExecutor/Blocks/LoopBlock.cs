using System.Collections.Generic;

/// <summary>
/// 반복문(for, while)의 기반 클래스. 검사와 { } 실행을 번갈아 한다.
/// <para>
/// - 검사 한 번에 1틱을 쓴다. 검사에 expression으로 쓴 블록이 있으면 그 블록이 먼저 자기 틱을 쓴다.<br/>
/// - 검사가 참이면 다음 틱부터 { }를 실행한다. 거짓이면 검사한 틱에 끝난다.<br/>
/// - { }는 바퀴마다 새로 범위를 연다. 그래서 { } 안에서 선언한 변수는 바퀴마다 새 변수다.<br/>
/// - { }가 비어 있으면 바로 다음 틱에 다시 검사한다. 한 바퀴는 검사 1틱이므로 while (true) { }도 틱이 나뉘어 게임이 멈추지 않는다.<br/>
/// - break: 그 틱에 반복문이 끝난다. 마지막 검사는 하지 않는다. continue: 다음 틱에 다음 검사를 한다.<br/>
/// - { } 안의 실패와 return은 반복을 멈추고 바깥에 그대로 알린다.
/// </para>
/// </summary>
// 반복문은 문법 블록이므로 stack을 쓰지 않는다. 바퀴를 몇 번 돌아도 { } 안의 함수 블록이 끝날 때마다 stack을 돌려준다
public abstract class LoopBlock : Block
{
	// { } 안의 블록들(원본)
	readonly IReadOnlyList<Block> body;
	// 이번 바퀴의 { }. null이면 검사할 차례다
	Statements lap;

	protected LoopBlock(IReadOnlyList<Block> body)
	{
		this.body = body;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		if (lap != null)
		{
			BlockResult result = lap.Step(context);
			switch (result)
			{
				case BlockResult.Running:
					return BlockResult.Running;
				// break 블록의 틱에 반복문도 끝난다
				case BlockResult.Break:
					return BlockResult.Done;
				// 바퀴가 끝났거나 continue를 만났다. 다음 검사는 다음 틱에 한다
				case BlockResult.Done:
				case BlockResult.Continue:
					lap = null;
					OnLapEnd();
					return BlockResult.Running;
				// 실패, return은 반복을 멈추고 바깥에 그대로 알린다
				default:
					return result;
			}
		}

		BlockResult checkResult = Check(context, out bool loop);
		if (checkResult != BlockResult.Done) return checkResult;
		if (!loop) return BlockResult.Done;

		// 빈 { }는 틱을 쓰지 않는다. 다음 틱에 바로 다음 검사를 한다
		if (body.Count == 0)
		{
			OnLapEnd();
			return BlockResult.Running;
		}
		lap = new Statements(body, true);
		return BlockResult.Running;
	}

	/// <summary>
	/// 반복할지 검사한다. 검사를 마친 틱에 Done을 돌려주고 결과를 loop에 담는다.<br/>
	/// expression이 틱을 쓰는 중이면 Running, 실패하면 실패 종류를 기록하고 Failed를 돌려준다.
	/// </summary>
	protected abstract BlockResult Check(FunctionContext context, out bool loop);

	/// <summary>
	/// 한 바퀴가 끝났을 때(continue 포함) 다음 검사 전에 한 번 불린다.
	/// </summary>
	protected virtual void OnLapEnd() { }

	// 덮어쓰는 클래스는 이 함수를 먼저 부른다. 안쪽 { }를 먼저 끊어야 안쪽 범위가 먼저 닫히기 때문
	protected override void OnCancel(FunctionContext context)
	{
		lap?.Cancel(context);
	}
}
