using System;
using UnityEngine;

/// <summary>
/// 플레이어에게 값(예: 대상)을 입력받는 사전 정의 함수(입력 함수)의 기반 클래스. 고정 ID는 input.으로 시작한다.
/// <para>
/// 사용법<br/>
/// - 이 클래스를 상속하고, OnStart에서 RequestInput으로 입력을 요청한다.<br/>
/// - 작업은 입력이 들어올 때까지 멈춘다(WaitingInput). 기다리는 동안의 틱은 TickCount에 넣지 않는다.<br/>
/// - 값이 들어온 다음 틱의 OnTick에서 TryTakeInput으로 값을 꺼내 Return한다.
/// </para>
/// </summary>
// 입력 요청을 이 클래스에만 둔다. 스킬은 대상을 파라미터로 받고, 플레이어의 입력은 입력 함수만 받는다는 기획을 코드로 지키기 위함
// 기다리는 동안 틱을 세지 않는 규칙은 PredefinedFunction.Step에 있다. Step이 sealed라 이 클래스가 덮어쓸 수 없기 때문
public abstract class InputFunction : PredefinedFunction
{
	/// <summary>
	/// 플레이어에게 type 타입의 값을 입력받는다. OnStart나 OnTick에서 부른다.<br/>
	/// 실행기가 InputRequested로 입력을 받는 쪽에 알리고, 값이 들어오면 다음 틱부터 이어서 진행한다.
	/// </summary>
	protected void RequestInput(FunctionContext context, VariableType type)
	{
		if (type == null) throw new ArgumentNullException(nameof(type));
		if (HasEnded)
		{
			Debug.LogError($"{Name}: 끝난 뒤에 RequestInput을 불렀습니다. RequestInput은 OnStart, OnTick에서만 부릅니다.");
			return;
		}
		if (context.InputRequester != null)
		{
			Debug.LogError($"{Name}: 이미 입력을 요청했습니다. 들어온 값을 TryTakeInput으로 꺼낸 뒤에 다시 요청합니다.");
			return;
		}

		context.RequestInput(this, type);
	}

	/// <summary>
	/// RequestInput으로 요청한 값을 꺼낸다. 값이 들어온 뒤의 OnTick, OnEnd에서 부른다. 아직 들어오지 않았으면 false를 돌려준다.
	/// </summary>
	protected bool TryTakeInput(FunctionContext context, out RuntimeValue value) => context.TryTakeInput(out value);
}
