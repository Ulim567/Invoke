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
	// 이 실행에서 블록 목록을 실행하는 도우미. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	Statements statements;
	// 만들 때 정한 쿨타임. 정하지 않았으면 null이고, 그때는 Blocks에서 계산한다
	readonly int? cooldownTicks;

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
	/// 안에 든 함수 블록들의 쿨타임을 더한 값. 같은 스킬을 여러 번 넣으면 그만큼 늘어난다.<br/>
	/// 함수 데이터에서 바꾼 함수는 FunctionDataConverter가 expression 안의 스킬까지 세어 넣는다.
	/// </summary>
	// 직접 만든 함수(테스트 등)는 Blocks에 바로 들어 있는 함수 블록만 센다
	public override int CooldownTicks
	{
		get
		{
			if (cooldownTicks.HasValue) return cooldownTicks.Value;

			int sum = 0;
			foreach (Block block in Blocks)
				if (block is FunctionBlock function) sum += function.CooldownTicks;
			return sum;
		}
	}

	/// <summary>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수. 함수 데이터에서 바꾼 함수는 FunctionDataConverter가 계산해 넣는다.
	/// </summary>
	public override int FrameSize { get; }

	/// <summary>
	/// 프레임 크기 1(파라미터, 지역 변수 없음)인 함수를 만든다.
	/// </summary>
	public UserFunction(string name, params Block[] blocks) : this(name, 1, blocks) { }

	/// <summary>
	/// 프레임 크기를 정해 함수를 만든다. 1보다 작으면 1로 본다.
	/// </summary>
	public UserFunction(string name, int frameSize, params Block[] blocks)
	{
		Name = name;
		// 호출 자리 1칸은 언제나 쓴다
		FrameSize = frameSize < 1 ? 1 : frameSize;
		Blocks = blocks;
	}

	/// <summary>
	/// 프레임 크기와 쿨타임을 정해 함수를 만든다. FunctionDataConverter가 쓴다.
	/// </summary>
	public UserFunction(string name, int frameSize, int cooldownTicks, params Block[] blocks) : this(name, frameSize, blocks)
	{
		this.cooldownTicks = cooldownTicks;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		// 블록이 하나도 없는 함수도 Statements가 바로 Done을 돌려주므로 최소 1틱은 쓴다
		BlockResult result = statements.Step(context);

		// 안쪽에서 return을 만났으면 남은 블록을 실행하지 않는다. return은 이 함수에서 멈추므로 바깥에는 Done으로 알린다
		// 실패는 이 함수도 끝까지 실행하지 못한 것이므로 바깥에 그대로 알린다
		return result == BlockResult.Returned ? BlockResult.Done : result;
	}

	// 안쪽 블록은 실행 문맥만 받으므로, 이 함수의 지역 변수를 실행 문맥에 올려 두어 안쪽 블록이 찾게 한다
	// Block은 OnStart가 불린 뒤 OnEnd, OnCancel 중 정확히 하나를 부르므로, 올리고 내리는 짝이 어긋나지 않는다
	protected override void OnStart(FunctionContext context)
	{
		Variables = new VariableStore();
		context.PushVariables(Variables);
		// function body는 범위를 따로 열지 않는다. C에서 function body의 맨 바깥 범위는 파라미터와 같은 범위이기 때문
		statements = new Statements(Blocks, false);
	}

	protected override void OnEnd(FunctionContext context)
	{
		context.PopVariables();
	}

	protected override void OnCancel(FunctionContext context)
	{
		// 실행 중인 안쪽 블록도 함께 끊어서, 안쪽 블록들도 정리할 기회를 갖게 한다
		// 안쪽을 먼저 끊는다. 안쪽 사용자 정의 함수가 자기 지역 변수를 먼저 내려야 이 함수의 것이 맨 위가 되기 때문
		statements?.Cancel(context);
		context.PopVariables();
	}
}
