using System;
using System.Collections.Generic;

/// <summary>
/// 게임이 통째로 제공하는 함수(C 스킬, 공통 함수, 특수 이동 함수, 입력 함수)의 기반 클래스.<br/>
/// 플레이어는 내부를 볼 수 없고, 실행기가 정해진 틱 수 동안 이 함수를 진행시킨다.
/// <para>
/// 사용법 (docs/systems/predefined-function.md)<br/>
/// - 이 클래스를 상속하고 Id, Name, TickCount를 정한다. Id는 PredefinedFunctionIds의 상수를 쓴다.<br/>
/// - CodeSize, Parameters, ReturnType, LocalVariableCount, IsAsync는 기본값과 다를 때만 덮어쓴다.<br/>
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
	/// 고정 ID. 함수 데이터의 함수 블록이 이 값으로 이 함수를 가리킨다. 화면 이름(Name)과 따로 둔다.
	/// </summary>
	public abstract string Id { get; }

	/// <summary>
	/// 이 함수의 동작 시간(틱 수).
	/// </summary>
	public abstract int TickCount { get; }

	/// <summary>
	/// 코드 영역 크기. 함수 안의 함수 블록으로 들어가든 슬롯에 바로 넣든 이 값으로 센다.
	/// </summary>
	public virtual int CodeSize => 1;

	/// <summary>
	/// 파라미터 이름과 타입. 편집 화면은 이 수와 순서대로 함수 블록의 값 칸을 만든다.
	/// </summary>
	public virtual IReadOnlyList<ParameterData> Parameters => Array.Empty<ParameterData>();

	/// <summary>
	/// 돌려주는 값의 타입. Void가 아니면 함수 블록을 값 칸에 끼울 수 있다.
	/// </summary>
	// 부를 때마다 새로 만든다. VariableType은 값을 바꿀 수 있어서, 하나를 함께 쓰면 한 곳에서 바꾼 값이 모든 함수에 퍼지기 때문
	public virtual VariableType ReturnType => new VariableType(VariableTypeKind.Void);

	/// <summary>
	/// 실행 중에 쓰는 지역 변수 수. 스택 프레임 크기 계산에 쓴다.<br/>
	/// 스킬마다 스택 부담을 조정하는 값이다. 무거운 스킬일수록 크게 정한다.
	/// </summary>
	// C 스킬의 내부는 블록이 아니라 C# 코드라서 실제 지역 변수를 셀 수 없다. 그래서 스킬 작성자가 직접 정한다
	public virtual int LocalVariableCount => 0;

	/// <summary>
	/// 다른 함수 안에서 불렸을 때 기다리지 않으면 true. 이때는 새 작업으로 실행되고, 부른 쪽은 바로 다음 블록으로 넘어간다.
	/// </summary>
	public virtual bool IsAsync => false;

	/// <summary>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수.
	/// </summary>
	// 스킬마다 직접 정하지 않고 계산한다. 파라미터를 고치고 프레임 크기를 고치지 않아 둘이 어긋나는 일을 막기 위함
	public sealed override int FrameSize => 1 + Parameters.Count + LocalVariableCount;

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
