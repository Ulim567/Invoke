/// <summary>
/// 게임이 통째로 제공하는 함수(C 스킬, 공통 함수, 특수 이동 함수)의 기반 클래스.<br/>
/// 플레이어는 내부를 볼 수 없고, 실행기가 정해진 틱 수 동안 이 함수를 진행시킨다.
/// <para>
/// 사용법<br/>
/// - 이 클래스를 상속하고 Name, TickCount를 정한다.<br/>
/// - OnStart, OnTick, OnEnd, OnCancel 중 필요한 것만 덮어써서, 몇 번째 틱에 무엇을 할지 적는다. (예: 자세잡기 → 차징 → 발사)<br/>
/// - 실행되는 것은 언제나 복사본이므로, 실행 중에 쓰는 값(예: 차징한 양)을 필드에 그대로 저장해도 된다.
/// </para>
/// <para>
/// 한 틱에서 부르는 순서: OnStart(첫 틱만) → OnTick → OnEnd(마지막 틱만)<br/>
/// 도중에 끊기면 OnEnd 대신 OnCancel이 불린다. 만들어 둔 오브젝트를 없애는 등의 정리는 OnCancel에서 한다.<br/>
/// TickCount가 1 이하이면 1틱짜리로 진행한다. 모든 블록이 최소 1틱을 써야 무한 반복이 한 틱 안에 갇히지 않는다.
/// </para>
/// </summary>
public abstract class PredefinedFunction : FunctionBlock
{
	int elapsedTicks;

	/// <summary>
	/// 이 함수의 동작 시간(틱 수).
	/// </summary>
	public abstract int TickCount { get; }

	protected sealed override BlockResult Step(FunctionTask task)
	{
		elapsedTicks++;
		OnTick(task, elapsedTicks);
		return elapsedTicks >= TickCount ? BlockResult.Done : BlockResult.Running;
	}

	/// <summary>
	/// 매 틱 불린다. tick은 이번이 몇 번째 틱인지이며 1부터 TickCount까지 센다.
	/// </summary>
	protected virtual void OnTick(FunctionTask task, int tick) { }
}
