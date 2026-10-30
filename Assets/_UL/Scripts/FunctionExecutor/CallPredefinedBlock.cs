/// <summary>
/// 함수 몸통에 놓인, 사전 정의 함수(C 스킬, 공통 함수 등)를 부르는 함수 블록. 예: Thrust(target)
/// <para>
/// - 파라미터 값 칸을 왼쪽부터 실행한 뒤, 그 값을 넘긴 스킬 복사본을 실행한다. 스킬은 TryGetArgument로 값을 읽는다.<br/>
/// - 틱: 값 칸에 끼운 블록이 없으면 스킬의 TickCount와 같다(슬롯에서 바로 시전했을 때와 같음). 끼운 블록이 있으면 그 블록들의 틱이 앞에 더해진다.<br/>
/// - 스킬에 반환 값이 있어도 쓰지 않고 버린다.<br/>
/// - 비동기 스킬(IsAsync)은 새 작업으로 시전하고, 이 블록은 1틱 뒤 끝난다.
/// </para>
/// </summary>
public class CallPredefinedBlock : Block
{
	readonly PredefinedFunction function;
	// 파라미터 값 칸(원본). Parameters와 같은 순서이고, 비운 칸은 null
	readonly ValueBlock[] argumentOriginals;
	// 이 실행의 함수 호출. 블록 복사본마다 따로 가져야 하므로 OnStart에서 만든다
	PredefinedCall call;

	public CallPredefinedBlock(PredefinedFunction function, params ValueBlock[] arguments)
	{
		this.function = function;
		argumentOriginals = arguments;
	}

	protected override void OnStart(FunctionTask task)
	{
		call = new PredefinedCall(function, argumentOriginals);
	}

	protected override BlockResult Step(FunctionTask task) => call.Step(task);

	protected override void OnCancel(FunctionTask task)
	{
		call?.Cancel(task);
	}
}
