using System;
using System.Collections.Generic;

/// <summary>
/// 함수 블록(사전 정의 함수, 사용자 정의 함수)의 기반 클래스.<br/>
/// 슬롯에 넣어 시전할 수 있는 것은 함수 블록뿐이다. return 같은 문법 블록은 함수 안에서만 쓴다.
/// <para>
/// 함수 블록은 실행을 시작할 때 FrameSize만큼 스택을 쓰고, 끝나거나 끊기는 즉시 돌려준다.
/// </para>
/// </summary>
public abstract class FunctionBlock : Block
{
	// 실제로 받은 칸 수. 실행 도중에 FrameSize가 바뀌어도 받은 만큼만 돌려주기 위해 따로 기록한다
	int allocatedStack;

	/// <summary>
	/// 함수 이름. 로그와 화면 표시에 쓴다.
	/// </summary>
	public abstract string Name { get; }

	/// <summary>
	/// 이 함수를 실행할 때 스택에 쌓이는 프레임 크기(칸).<br/>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수.<br/>
	/// 사전 정의 함수는 Parameters, LocalVariableCount로 계산한다. 사용자 정의 함수는 만들 때 받은 값이다(함수 데이터에서 바꾸면 FunctionDataConverter가 계산).
	/// </summary>
	public virtual int FrameSize => 1;

	/// <summary>
	/// 파라미터 이름과 타입. 편집 화면은 이 수와 순서대로 함수 블록의 value slot을 만든다.
	/// </summary>
	// 사전 정의 함수와 사용자 정의 함수가 함께 가진다. 함수 호출 블록(CallBlock)이 함수 종류를 나누지 않고 argument를 맞추게 하기 위함
	public virtual IReadOnlyList<ParameterData> Parameters => Array.Empty<ParameterData>();

	/// <summary>
	/// 돌려주는 값의 타입. Void가 아니면 함수 블록을 expression으로 쓸 수 있다.
	/// </summary>
	// 부를 때마다 새로 만든다. VariableType은 값을 바꿀 수 있어서, 하나를 함께 쓰면 한 곳에서 바꾼 값이 모든 함수에 퍼지기 때문
	public virtual VariableType ReturnType => new VariableType(VariableTypeKind.Void);

	/// <summary>
	/// 다른 함수 안에서 불렸을 때 기다리지 않으면 true. 이때는 새 작업으로 실행되고, 부른 쪽은 바로 다음 블록으로 넘어간다.
	/// </summary>
	public virtual bool IsAsync => false;

	// 함수 호출 블록의 value slot이나 슬롯 직접 시전에서 받은 argument. Parameters와 같은 순서이고, 비운 칸은 null이다
	// argument 없이 시전했으면 배열 자체가 null이다. 실행하는 복사본에만 넘기고 원본에는 넘기지 않는다
	internal RuntimeValue?[] Arguments { get; private set; }

	// 이 함수가 돌려준 값. 끝까지 실행한 뒤 TryValidateEnd가 반환 타입으로 맞춰 둔다. 정하지 않았으면 null
	// 함수 호출 블록(CallBlock)이 이 함수가 끝난 뒤 받아 간다
	internal RuntimeValue? ReturnValue { get; private protected set; }

	// 순서: 프레임 쓰기 → AfterFrameAllocated → OnStart → … → OnEnd/OnCancel → BeforeFrameReleased → 프레임 돌려주기
	// 이 순서여야 UserFunction이 OnStart/OnEnd에서 쌓고 내리는 변수 저장소와 프레임이 어긋나지 않는다
	// sealed: C 스킬이나 사용자 정의 함수가 덮어써서 이 순서를 깨지 못하게 한다. OnStart에 두지 않는 이유도 같다(C 스킬이 base를 부르지 않아도 됨)
	internal sealed override BlockResult Execute(FunctionContext context)
	{
		if (State == BlockState.NotStarted)
		{
			// 아직 시작할 수 없으면(예: 같은 스킬이 쿨타임 중) 프레임을 받지 않고 다음 틱에 다시 확인한다
			// 실패로 끝내지 않는 이유: 반복문 안의 스킬이 쿨타임 때문에 함수 전체를 실패시키지 않고, 쓸 수 있을 때까지 기다리게 하기 위함
			if (!CanStartNow(context)) return BlockResult.Running;

			// 시작할 수 없는 값을 받았으면(예: 죽은 대상) 프레임을 받지 않고 실패한다. 실패 종류는 TryValidateStart가 기록한다
			if (!TryValidateStart(context)) return BlockResult.Failed;

			// 프레임을 쌓을 공간이 없으면 시작하지 않는다. 시작하지 않았으므로 정리할 것도 없다
			if (!TryAllocateFrame(context))
			{
				context.RecordFailure(FunctionFailure.Overflow);
				return BlockResult.Failed;
			}
			AfterFrameAllocated(context);
		}

		BlockResult result = base.Execute(context);
		// 끝까지 실행했어도 반환 값이 잘못되었으면(예: return 누락) 실패로 바꾼다. 실패 종류는 TryValidateEnd가 기록한다
		if (result == BlockResult.Done && !TryValidateEnd(context)) result = BlockResult.Failed;
		// 함수가 끝나는 즉시 프레임을 반환한다. 실제 프로그램에서 return하는 순간 프레임이 빠지는 것과 같다
		if (result != BlockResult.Running) ReleaseFrame(context);
		return result;
	}

	internal sealed override void Cancel(FunctionContext context)
	{
		base.Cancel(context);
		ReleaseFrame(context);
	}

	// 시작하기 전, 틱마다 불린다. false이면 이번 틱에는 시작하지 않고 기다린다
	// 기다리는 동안 끊기면 시작하지 않은 것이므로 정리할 것이 없다
	internal virtual bool CanStartNow(FunctionContext context) => true;

	// 기다림이 끝난 뒤, 프레임을 받기 직전에 한 번 불린다. false이면 시작하지 않고 실패하며, 실패 종류를 이 함수가 기록한다
	// 기다림(CanStartNow)이 끝난 뒤에 보는 이유: 쿨타임을 기다리는 동안 대상이 죽을 수 있기 때문
	internal virtual bool TryValidateStart(FunctionContext context) => true;

	// 끝까지 실행한 직후(OnEnd 다음, 프레임을 돌려주기 전)에 한 번 불린다. false이면 이 함수는 실패로 끝나며, 실패 종류를 이 함수가 기록한다
	// 반환 값 확인을 함수마다 두는 이유: 사전 정의 함수의 잘못은 개발자 책임, 사용자 정의 함수의 잘못은 플레이어 책임이라 처리가 다르기 때문
	internal virtual bool TryValidateEnd(FunctionContext context) => true;

	// 함수 호출 블록이 value slot을 계산한 뒤, 또는 실행기가 슬롯 직접 시전의 argument를 실행할 복사본에 넘긴다
	internal void SetArguments(RuntimeValue?[] values)
	{
		Arguments = values;
	}

	// 프레임을 받은 직후에 한 번 불린다. 프레임을 받지 못해(Overflow) 시작하지 않은 함수에는 불리지 않는다
	// 블록의 실행 단계(OnStart)가 아니라 프레임 시점에 두는 이유: 스킬 작성자가 덮어쓰는 OnStart와 섞이지 않고, BeforeFrameReleased와 짝이 항상 맞게 하기 위함
	internal virtual void AfterFrameAllocated(FunctionContext context) { }

	// 프레임을 돌려주기 직전에 한 번 불린다. 정상 종료, 실패, 실행 중단 모두 이곳을 지난다
	internal virtual void BeforeFrameReleased(FunctionContext context) { }

	bool TryAllocateFrame(FunctionContext context)
	{
		if (!context.Memory.TryAllocateStack(FrameSize)) return false;

		allocatedStack = FrameSize;
		return true;
	}

	// 받은 칸이 없으면(시작하지 않음, 이미 돌려줌) 아무것도 하지 않는다. 메모리를 두 번 돌려주지 않기 위함
	void ReleaseFrame(FunctionContext context)
	{
		if (allocatedStack == 0) return;

		BeforeFrameReleased(context);
		context.Memory.ReleaseStack(allocatedStack);
		allocatedStack = 0;
	}
}
