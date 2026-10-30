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
	// 함수 블록의 값 칸에서 받은 파라미터 값. Parameters와 같은 순서이고, 비운 칸은 null이다
	// 슬롯에서 바로 시전했거나 값 칸이 없으면 배열 자체가 null이다
	RuntimeValue?[] arguments;
	// Return으로 정한 반환 값. 정하지 않았으면 null
	RuntimeValue? returnValue;

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
		// OnStart에서 이미 Fail이나 Stop을 불렀으면 OnTick을 부르지 않는다
		if (requestedError == null && !stopRequested)
		{
			elapsedTicks++;
			OnTick(task, elapsedTicks);
		}

		// Fail과 Stop을 둘 다 불렀으면 에러를 먼저 따른다. 에러를 놓치면 안 되기 때문
		if (requestedError != null)
		{
			task.RecordFailure(FunctionFailure.SkillFailed, requestedError);
			OnFail(task, requestedError);
			return BlockResult.Failed;
		}
		if (stopRequested) return BlockResult.Done;

		return elapsedTicks >= TickCount ? BlockResult.Done : BlockResult.Running;
	}

	/// <summary>
	/// 매 틱 불린다. tick은 이번이 몇 번째 틱인지이며 1부터 TickCount까지 센다.
	/// </summary>
	protected virtual void OnTick(FunctionTask task, int tick) { }

	/// <summary>
	/// 이 스킬이 Fail로 에러를 냈을 때 한 번 불린다. 이 스킬만의 결과(예: 시전자가 피해를 입음)를 적는다.<br/>
	/// 이 다음에 OnCancel이 불리므로, 만들어 둔 오브젝트의 정리는 지금처럼 OnCancel에서 한다.<br/>
	/// 작업을 끝낼지, 페널티를 줄지 같은 처리는 여기서 하지 않는다. 캐릭터 언어의 언어 규칙 표가 정한다.
	/// </summary>
	// OnCancel과 나눈 이유: OnCancel은 실행 중단 공격, Overflow 때도 불리므로 스킬이 스스로 낸 에러만 구분할 수 없기 때문
	protected virtual void OnFail(FunctionTask task, SkillError error) { }

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
	/// 값 칸을 비웠거나 슬롯에서 바로 시전해 값이 없으면 false를 돌려준다. 그때는 슬롯에서 바로 시전했을 때처럼 동작한다.<br/>
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

			if (arguments == null || i >= arguments.Length || arguments[i] == null) return false;
			value = arguments[i].Value;
			return true;
		}

		// 이름 오타는 컴파일 오류로 잡히지 않으므로 바로 알린다
		Debug.LogError($"{Name}: 파라미터 {name}이(가) 없습니다. Parameters에 적은 이름으로 읽습니다.");
		return false;
	}

	/// <summary>
	/// 이 함수의 반환 값을 정한다. ReturnType이 Void가 아닌 함수는 끝나기 전에 반드시 부른다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 여러 번 부르면 마지막 값을 쓴다. 값이 ReturnType과 다르면 실행기가 캐릭터 언어의 숫자 타입 변환 규칙으로 맞춘다.<br/>
	/// 부르지 않고 끝나면, 값 칸에 끼운 이 함수는 MissingReturnValue로 실패한다.
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

		returnValue = value;
	}

	// 값 칸의 함수 블록(PredefinedValueBlock)이 이 함수가 끝난 뒤 반환 값을 받아 간다
	internal bool TryGetReturnValue(out RuntimeValue value)
	{
		value = returnValue ?? default;
		return returnValue.HasValue;
	}

	// 함수 블록(CallPredefinedBlock)이 값 칸을 계산한 뒤, 실행할 복사본에 넘긴다. 원본에는 넘기지 않는다
	internal void SetArguments(RuntimeValue?[] values)
	{
		arguments = values;
	}

	/// <summary>
	/// obj를 시전한 캐릭터의 힙 size칸에 올리고 포인터를 돌려준다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 힙이 모자라면 NullPointer를 돌려준다. 에러로 끝내지 않으므로, 실패했는지는 돌려받은 값이 HeapStorage.NullPointer인지로 확인한다.<br/>
	/// 이 작업이 정상으로 끝나면 포인터가 전역 포인터 목록으로 옮겨진다. 실행 중단이나 실패로 끝나면 해제하지 않은 객체는 누수가 된다.
	/// </summary>
	protected int Malloc(FunctionTask task, object obj, int size)
	{
		if (IsInCancel(nameof(Malloc))) return HeapStorage.NullPointer;
		return task.Memory.Heap.Malloc(task, obj, size);
	}

	/// <summary>
	/// 포인터의 객체를 해제하고 힙 칸을 돌려준다. OnStart, OnTick, OnEnd에서 부른다.<br/>
	/// 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터)이면 Fail(InvalidPointerError)로 에러를 내고 false를 돌려준다.<br/>
	/// 오브젝트를 없애지는 않는다. 오브젝트는 스킬 코드가 따로 없앤다.
	/// </summary>
	protected bool Free(FunctionTask task, int pointer)
	{
		if (IsInCancel(nameof(Free))) return false;
		if (task.Memory.Heap.Free(pointer)) return true;

		Fail(new InvalidPointerError(pointer));
		return false;
	}

	/// <summary>
	/// 포인터의 객체를 T 타입으로 꺼낸다.<br/>
	/// 쓸 수 없는 포인터(NullPointer, 해제한 포인터, 누수된 포인터, 타입이 T가 아닌 객체의 포인터)이면 Fail(InvalidPointerError)로 에러를 내고 false를 돌려준다.
	/// </summary>
	// C에서 null이나 해제한 포인터를 역참조하면 프로그램이 오류를 내는 것처럼, 꺼내지 못하면 바로 스킬 에러로 끝낸다
	protected bool TryGetHeapObject<T>(FunctionTask task, int pointer, out T obj)
	{
		if (task.Memory.Heap.TryGet(pointer, out obj)) return true;

		Fail(new InvalidPointerError(pointer));
		return false;
	}

	/// <summary>
	/// 전역 포인터 목록에 있는 T 타입 객체의 포인터를 malloc한 순서대로 돌려준다.<br/>
	/// 다른 작업이 올려 둔 객체(예: 먼저 시전한 검 소환의 소환검)를 찾을 때 쓴다.
	/// </summary>
	protected List<int> GetGlobalPointers<T>(FunctionTask task) => task.Memory.Heap.GetGlobalPointers<T>();

	/// <summary>
	/// 시전한 캐릭터의 힙에 있는 T 타입 객체의 수. 누수된 객체도 센다. 소환검 최대 개수 검사에 쓴다.
	/// </summary>
	protected int CountHeapObjects<T>(FunctionTask task) => task.Memory.Heap.CountObjects<T>();

	// OnCancel 안에서 힙을 바꾸지 못하게 막는다
	// OnCancel에서 Free하면 실행 중단을 당해도 누수가 생기지 않고, Malloc하면 끊기는 작업의 포인터가 되어 바로 누수가 되기 때문
	bool IsInCancel(string functionName)
	{
		if (!WasCancelled) return false;

		Debug.LogError($"{Name}: OnCancel 안에서 {functionName}을 불렀습니다. 힙에 올린 객체는 OnCancel에서 정리하지 않습니다(누수 규칙).");
		return true;
	}
}
