using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임이 통째로 제공하는 함수(C 스킬, 공통 함수, 특수 이동 함수, 입력 함수)의 기반 클래스.<br/>
/// 플레이어는 내부를 볼 수 없고, 실행기가 정해진 틱 수 동안 이 함수를 진행시킨다.
/// <para>
/// 사용법 (docs/systems/predefined-function.md)<br/>
/// - 이 클래스를 상속하고 Id, Name, TickCount를 정한다. Id는 PredefinedFunctionIds의 상수를 쓴다.<br/>
/// - CodeSize, Parameters, ReturnType, LocalVariableCount, IsAsync, CooldownTicks는 기본값과 다를 때만 덮어쓴다.<br/>
/// - OnStart, OnTick, OnEnd, OnCancel 중 필요한 것만 덮어써서, 몇 번째 틱에 무엇을 할지 적는다. (예: 자세잡기 → 차징 → 발사)<br/>
/// - 실행되는 것은 언제나 복사본이므로, 실행 중에 쓰는 값(예: 차징한 양)을 필드에 그대로 저장해도 된다.<br/>
/// - OnStart나 OnTick에서 실행할 수 없는 상태를 발견하면 Fail(에러)로 에러를 내고, 에러 없이 일찍 끝내려면 Stop()을 부른다.<br/>
/// - 에러로 끝날 때 이 스킬만의 결과(예: 시전자가 피해를 입음)가 있으면 OnFail에 적는다.<br/>
/// - 힙에 객체(예: 소환검)를 올리고 내릴 때는 Malloc, Free를 부르고, 포인터로 객체를 꺼낼 때는 TryGetHeapObject를 부른다.
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
	// Fail, Stop은 요청만 기록하고, Step이 이 요청을 보고 이 함수를 끝낸다
	// OnStart, OnTick 도중에 바로 끝내면 그 뒤의 스킬 코드가 끝난 함수 위에서 계속 실행되기 때문
	SkillError requestedError;
	bool stopRequested;
	// 이 함수가 Malloc으로 올린 포인터. 정상으로 끝나면 숨은 시스템 전역으로 옮긴다
	// 처음 Malloc할 때 만든다. 원본에서 만들어 두면 복사본들이 같은 목록을 함께 쓰게 되기 때문(복사는 필드의 참조만 복사함)
	List<int> mallocedPointers;

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
	/// 실행 중에 쓰는 지역 변수 수. 스택 프레임 크기 계산에 쓴다.<br/>
	/// 스킬마다 스택 부담을 조정하는 값이다. 무거운 스킬일수록 크게 정한다.
	/// </summary>
	// C 스킬의 내부는 블록이 아니라 C# 코드라서 실제 지역 변수를 셀 수 없다. 그래서 스킬 작성자가 직접 정한다
	public virtual int LocalVariableCount => 0;

	/// <summary>
	/// 쿨타임(tick 수). 이 스킬이 끝난 순간부터 이만큼 지나야 같은 고정 ID의 스킬을 다시 쓸 수 있다.<br/>
	/// 정상 종료, 실패, 실행 중단 모두 쿨타임 전체가 걸린다. 슬롯에서 바로 시전했든 사용자 정의 함수 안에서 불렸든 같다.
	/// </summary>
	// 동작 시간(TickCount)과 따로 둔다. 짧게 실행되고 오래 기다리는 스킬, 오래 실행되고 바로 다시 쓰는 스킬을 모두 만들기 위함
	public virtual int CooldownTicks => 0;

	/// <summary>
	/// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수.
	/// </summary>
	// 스킬마다 직접 정하지 않고 계산한다. 파라미터를 고치고 프레임 크기를 고치지 않아 둘이 어긋나는 일을 막기 위함
	public sealed override int FrameSize => 1 + Parameters.Count + LocalVariableCount;

	// 같은 스킬이 실행 중이거나 쿨타임 중이면 시작하지 않고 기다린다. 슬롯 직접 시전, 함수 안의 호출, 비동기 시전이 모두 여기를 지난다
	// 스킬이 실제로 실행된 동안만 실행 중으로 세고, 끝날 때 쿨타임을 건다
	// sealed: 스킬 작성자가 덮어써서 쿨타임 검사나 기록을 빠뜨리지 못하게 한다
	internal sealed override bool CanStartNow(FunctionContext context) => context.Cooldowns.CanUseSkill(Id);
	internal sealed override void AfterFrameAllocated(FunctionContext context) => context.Cooldowns.SkillStarted(this);
	/// <summary>
	/// 시작하기 전에 Target argument가 쓸 수 있는 대상인지 검사하면 true(기본). 검사에 걸리면 InvalidTarget으로 시작하지 않는다.<br/>
	/// 대상을 쓰지 않고 상태만 묻는 함수(is_valid_target)만 false로 둔다. 쓸 수 없는 대상을 받아 false를 돌려줘야 하기 때문이다 (#474).
	/// </summary>
	protected virtual bool ValidatesTargets => true;

	// 넘겨받은 대상이 쓸 수 없는 대상(대상 없음, 죽음, 없어짐)이면 시작하지 않고 InvalidTarget으로 실패한다. 대상 array는 원소 하나라도 쓸 수 없으면 실패한다
	// 함수 안의 호출(CallBlock)과 슬롯 직접 시전(FunctionExecutor.Cast의 argument)이 모두 여기를 지난다. 스킬마다 검사 코드를 넣지 않기 위함
	internal sealed override bool TryValidateStart(FunctionContext context)
	{
		if (Arguments == null || !ValidatesTargets) return true;

		foreach (RuntimeValue? argument in Arguments)
		{
			if (!argument.HasValue || HasOnlyUsableTargets(argument.Value)) continue;

			context.RecordFailure(FunctionFailure.InvalidTarget);
			return false;
		}
		return true;
	}

	// 대상 값이면 그 대상을, 대상 array이면 모든 원소를 검사한다. 대상이 아닌 값은 검사하지 않는다
	// array도 단일 대상과 같은 규칙으로 막는 이유: 여러 명을 고르는 스킬(전장 휩쓸기)과 한 명을 고르는 스킬이 죽은 대상에 다르게 반응하지 않게 하기 위함
	static bool HasOnlyUsableTargets(RuntimeValue value)
	{
		switch (value.Type?.Kind)
		{
			case VariableTypeKind.Target:
				return value.IsUsableTarget;

			case VariableTypeKind.Array when value.Type.ElementType?.Kind == VariableTypeKind.Target:
				foreach (RuntimeValue element in value.AsArray().Elements)
					if (!element.IsUsableTarget) return false;
				return true;
		}
		return true;
	}

	// 끝까지 실행한 직후 한 번 불린다. 반환 값을 확인하고, 통과하면(정상 종료) 이 함수가 malloc한 포인터를 숨은 시스템 전역으로 옮긴다
	// 이 함수가 끝나는 순간 옮긴다. 플레이어 함수 안에서 불렸어도 그 함수가 나중에 실패하든 상관없이 남기기 위함(C 라이브러리가 static 전역 변수에 저장하는 것과 같음)
	// 함수 안의 호출, 슬롯 직접 시전, 비동기 시전이 모두 여기를 지난다. 같은 스킬이 놓인 자리에 따라 성공하거나 실패하지 않게 하기 위함
	internal sealed override bool TryValidateEnd(FunctionContext context)
	{
		if (!TryValidateReturnValue(context)) return false;

		if (mallocedPointers != null) context.MoveToSystemGlobals(mallocedPointers);
		return true;
	}

	// Return으로 정한 값을 반환 타입으로 맞춘다. 맞추지 못하면 TypeMismatch가 아니라 개발자 책임으로 기록한다
	// 반환 값은 스킬 작성자(개발자)가 정하므로, 플레이어의 compile error로 보이지 않게 하기 위함
	bool TryValidateReturnValue(FunctionContext context)
	{
		if (ReturnType.Kind == VariableTypeKind.Void) return true;

		if (!ReturnValue.HasValue)
		{
			Debug.LogError($"{Name}: 반환 타입이 {ReturnType.Kind}인데 Return을 부르지 않고 끝났습니다.");
			context.RecordInternalError();
			return false;
		}
		if (!NumericRules.TryConvertForStore(ReturnValue.Value, ReturnType, context.Rule, out RuntimeValue converted))
		{
			Debug.LogError($"{Name}: Return으로 넘긴 {ReturnValue.Value.Type?.Kind} 값을 반환 타입 {ReturnType.Kind}(으)로 바꿀 수 없습니다.");
			context.RecordInternalError();
			return false;
		}

		ReturnValue = converted;
		return true;
	}

	internal sealed override void BeforeFrameReleased(FunctionContext context)
	{
		// 값을 받기 전에 끝났으면(Stop, Fail, 실행 중단) 남은 입력 요청을 지운다. 남겨 두면 같은 작업의 다음 스킬이 입력을 기다리는 것으로 보기 때문
		context.ClearInputRequest(this);
		context.Cooldowns.SkillEnded(this);
	}

	protected sealed override BlockResult Step(FunctionContext context)
	{
		// OnStart에서 이미 Fail이나 Stop을 불렀으면 OnTick을 부르지 않는다
		// 입력을 요청한 틱(OnStart에서 요청)에도 부르지 않는다. 입력을 기다리는 동안은 동작 시간(TickCount)에 넣지 않기 때문
		if (requestedError == null && !stopRequested && !context.IsWaitingInput)
		{
			elapsedTicks++;
			OnTick(context, elapsedTicks);
		}

		// Fail과 Stop을 둘 다 불렀으면 에러를 먼저 따른다. 에러를 놓치면 안 되기 때문
		if (requestedError != null)
		{
			context.RecordFailure(FunctionFailure.SkillFailed, requestedError);
			OnFail(context, requestedError);
			return BlockResult.Failed;
		}
		if (stopRequested) return BlockResult.Done;
		// 입력이 들어올 때까지 끝나지 않는다. TickCount가 지났어도 입력값으로 할 일이 남았기 때문
		if (context.IsWaitingInput) return BlockResult.Running;

		return elapsedTicks >= TickCount ? BlockResult.Done : BlockResult.Running;
	}

	/// <summary>
	/// 매 틱 불린다. tick은 이번이 몇 번째 틱인지이며 1부터 TickCount까지 센다.
	/// </summary>
	protected virtual void OnTick(FunctionContext context, int tick) { }

	/// <summary>
	/// 이 스킬이 Fail로 에러를 냈을 때 한 번 불린다. 이 스킬만의 결과(예: 시전자가 피해를 입음)를 적는다.<br/>
	/// 이 다음에 OnCancel이 불리므로, 만들어 둔 오브젝트의 정리는 지금처럼 OnCancel에서 한다.<br/>
	/// 작업을 끝낼지, 페널티를 줄지 같은 처리는 여기서 하지 않는다. 캐릭터 언어의 언어 규칙 표가 정한다.
	/// </summary>
	// OnCancel과 나눈 이유: OnCancel은 실행 중단 공격, Overflow 때도 불리므로 스킬이 스스로 낸 에러만 구분할 수 없기 때문
	protected virtual void OnFail(FunctionContext context, SkillError error) { }

	/// <summary>
	/// 이 함수를 에러로 끝낸다. OnStart나 OnTick에서 부른다.<br/>
	/// 그 틱이 끝날 때 OnEnd 대신 OnCancel이 불리고, 무엇을 할지(그 작업만 끝냄 등)는 캐릭터 언어의 언어 규칙 표가 정한다.<br/>
	/// 여러 번 부르면 처음 에러만 쓴다.
	/// </summary>
	protected void Fail(SkillError error)
	{
		if (error == null) throw new ArgumentNullException(nameof(error));
		// 이미 끝난 함수(OnEnd, OnCancel 안)에서 부르면 되돌릴 수 없으므로 스킬 코드의 실수로 알린다
		if (HasEnded)
		{
			Debug.LogError($"{Name}: 끝난 뒤에 Fail({error.Message})을 불렀습니다. Fail은 OnStart, OnTick에서만 부릅니다.");
			return;
		}

		if (requestedError != null) return;

		error.SourceName = Name;
		requestedError = error;
	}

	/// <summary>
	/// 이 함수를 에러 없이 일찍 끝낸다. OnStart나 OnTick에서 부른다.<br/>
	/// 그 틱이 끝날 때 OnEnd가 불리고, 사용자 정의 함수 안이었다면 다음 블록으로 넘어간다.
	/// </summary>
	protected void Stop()
	{
		if (HasEnded)
		{
			Debug.LogError($"{Name}: 끝난 뒤에 Stop()을 불렀습니다. Stop은 OnStart, OnTick에서만 부릅니다.");
			return;
		}

		stopRequested = true;
	}

	/// <summary>
	/// 이름이 name인 파라미터의 값을 꺼낸다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// value slot을 비웠거나 슬롯에서 바로 시전해 값이 없으면 false를 돌려준다. 그때는 슬롯에서 바로 시전했을 때처럼 동작한다.<br/>
	/// name은 Parameters에 적은 이름이다. 없는 이름이면 콘솔 오류를 내고 false를 돌려준다.
	/// </summary>
	// 번호가 아니라 이름으로 읽는 이유: 코드에서 무엇을 읽는지 바로 보이고, 파라미터 순서가 바뀌어도 읽는 코드를 고치지 않기 위함
	protected bool TryGetArgument(string name, out RuntimeValue value)
	{
		value = default;
		IReadOnlyList<ParameterData> parameters = Parameters;
		for (int i = 0; i < parameters.Count; i++)
		{
			if (parameters[i].Name != name) continue;

			if (Arguments == null || i >= Arguments.Length || Arguments[i] == null) return false;
			value = Arguments[i].Value;
			return true;
		}

		// 이름 오타는 컴파일 오류로 잡히지 않으므로 바로 알린다
		Debug.LogError($"{Name}: 파라미터 {name}이(가) 없습니다. Parameters에 적은 이름으로 읽습니다.");
		return false;
	}

	/// <summary>
	/// 이 함수의 반환 값을 정한다. ReturnType이 Void가 아닌 함수는 끝나기 전에 반드시 부른다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 여러 번 부르면 마지막 값을 쓴다. 값이 ReturnType과 다르면 실행기가 캐릭터 언어의 숫자 타입 변환 규칙으로 맞춘다.<br/>
	/// 부르지 않고 끝나거나 ReturnType으로 맞출 수 없는 값을 넘기면, 실행기가 콘솔 오류를 내고 작업을 InternalError(개발자 책임)로 끝낸다(statement로 불러도 같다).
	/// </summary>
	// 실제 C의 return과 달리 함수를 끝내지 않는다. 끝내려면 Stop을 함께 부른다
	protected void Return(RuntimeValue value)
	{
		if (ReturnType.Kind == VariableTypeKind.Void)
		{
			Debug.LogError($"{Name}: 반환 타입이 Void인데 Return을 불렀습니다. ReturnType을 먼저 정합니다.");
			return;
		}
		if (WasCancelled)
		{
			Debug.LogError($"{Name}: OnCancel 안에서 Return을 불렀습니다. 끊긴 함수의 반환 값은 쓰이지 않습니다.");
			return;
		}

		ReturnValue = value;
	}

	/// <summary>
	/// obj를 시전한 캐릭터의 힙 size칸에 올리고 포인터를 돌려준다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 힙이 모자라면 NullPointer를 돌려준다. 에러로 끝내지 않으므로, 실패했는지는 돌려받은 값이 HeapStorage.NullPointer인지로 확인한다.<br/>
	/// 이 함수가 정상으로 끝나면 포인터가 숨은 시스템 전역으로 옮겨진다(사용자 정의 함수 안에서 불렸어도 이 함수가 끝나는 순간). 실행 중단이나 실패로 끝나면 해제하지 않은 객체는 누수가 된다.
	/// </summary>
	protected int Malloc(FunctionContext context, object obj, int size)
	{
		if (IsInCancel(nameof(Malloc))) return HeapStorage.NullPointer;

		int pointer = context.Malloc(obj, size);
		if (pointer != HeapStorage.NullPointer) (mallocedPointers ??= new List<int>()).Add(pointer);
		return pointer;
	}

	/// <summary>
	/// 포인터의 객체를 해제하고 힙 칸을 돌려준다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터)이면 Fail(InvalidPointerError)로 에러를 내고 false를 돌려준다.<br/>
	/// 오브젝트를 없애지는 않는다. 오브젝트는 스킬 코드가 따로 없앤다.
	/// </summary>
	protected bool Free(FunctionContext context, int pointer)
	{
		if (IsInCancel(nameof(Free))) return false;
		if (context.Memory.Heap.Free(pointer)) return true;

		Fail(new InvalidPointerError(pointer));
		return false;
	}

	/// <summary>
	/// 포인터의 객체를 T 타입으로 꺼낸다.<br/>
	/// 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터, 타입이 T가 아닌 객체의 포인터)이면 Fail(InvalidPointerError)로 에러를 내고 false를 돌려준다.
	/// </summary>
	// C에서 null이나 해제한 포인터를 역참조하면 프로그램이 오류를 내는 것처럼, 꺼내지 못하면 바로 스킬 에러로 끝낸다
	protected bool TryGetHeapObject<T>(FunctionContext context, int pointer, out T obj)
	{
		if (context.Memory.Heap.TryGet(pointer, out obj)) return true;

		Fail(new InvalidPointerError(pointer));
		return false;
	}

	/// <summary>
	/// 숨은 시스템 전역에 있는 T 타입 객체의 포인터를 malloc한 순서대로 돌려준다.<br/>
	/// 다른 작업이 올려 둔 객체(예: 먼저 시전한 검 소환의 소환검)를 찾을 때 쓴다.
	/// </summary>
	protected List<int> GetGlobalPointers<T>(FunctionContext context) => context.Memory.Heap.GetGlobalPointers<T>();

	/// <summary>
	/// 시전한 캐릭터의 힙에 있는 T 타입 객체의 수. 누수된 객체도 센다. 소환검 최대 개수 검사에 쓴다.
	/// </summary>
	protected int CountHeapObjects<T>(FunctionContext context) => context.Memory.Heap.CountObjects<T>();

	// OnCancel 안에서 힙을 바꾸지 못하게 막는다
	// OnCancel에서 Free하면 실행 중단을 당해도 누수가 생기지 않고, Malloc하면 끊기는 작업의 포인터가 되어 바로 누수가 되기 때문
	bool IsInCancel(string functionName)
	{
		if (!WasCancelled) return false;

		Debug.LogError($"{Name}: OnCancel 안에서 {functionName}을 불렀습니다. 힙에 올린 객체는 OnCancel에서 정리하지 않습니다(누수 규칙).");
		return true;
	}
}
