/// <summary>
/// 테스트용 { } 블록. 안쪽 블록들을 범위를 여는 Statements로 실행한다. 조건문·반복문이 생기기 전에 블록 범위를 확인하기 위함이다.<br/>
/// 블록 자체는 틱을 따로 쓰지 않는다. 안쪽 블록들의 틱 합만큼 걸린다.
/// </summary>
public class TestScopeBlock : Block
{
	readonly Block[] blocks;
	// 이 실행에서 쓰는 도우미. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	Statements statements;

	public TestScopeBlock(params Block[] blocks)
	{
		this.blocks = blocks;
	}

	protected override void OnStart(FunctionContext context)
	{
		statements = new Statements(blocks, true);
	}

	protected override BlockResult Step(FunctionContext context) => statements.Step(context);

	protected override void OnCancel(FunctionContext context)
	{
		statements?.Cancel(context);
	}
}
