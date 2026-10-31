using UnityEngine;

/// <summary>
/// 실행기 테스트용 함수. 30틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionA : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestFunctionA;
	public override string Name => "TestFunctionA";
	public override int TickCount => 30;
}

/// <summary>
/// 실행기 테스트용 함수. 50틱 동안 틱만 쓰고 아무것도 하지 않는다.
/// </summary>
public class TestFunctionB : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestFunctionB;
	public override string Name => "TestFunctionB";
	public override int TickCount => 50;
}

/// <summary>
/// 실행기 테스트용 비동기 함수. 다른 함수 안에서 불리면 새 작업으로 20틱 동안 진행한다.
/// </summary>
public class TestAsyncFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestAsyncFunction;
	public override string Name => "TestAsyncFunction";
	public override int TickCount => 20;
	public override bool IsAsync => true;
}

/// <summary>
/// 시전 전 메모리 검사 테스트용 함수. 프레임이 21칸(호출 1 + 지역 변수 20)이라 기본 메모리 10칸으로는 시전할 수 없다.
/// </summary>
public class TestHeavyFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestHeavyFunction;
	public override string Name => "TestHeavyFunction";
	public override int TickCount => 10;
	public override int LocalVariableCount => 20;
}

/// <summary>
/// 스킬 에러 테스트용 공격 스킬. 시작할 때 맨손이면 UnarmedError를 낸다. 10틱 동안 진행한다.
/// </summary>
public class TestAttackFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestAttackFunction;
	public override string Name => "TestAttackFunction";
	public override int TickCount => 10;

	protected override void OnStart(FunctionTask task)
	{
		if (task.Caster.GetComponent<TempSwordState>().IsUnarmed) Fail(new UnarmedError());
	}

	// 실제 스킬은 여기서 시전자에게 피해를 주는 등 이 스킬만의 결과를 적는다. 테스트는 불렸는지만 확인한다
	protected override void OnFail(FunctionTask task, SkillError error)
	{
		Debug.Log($"{Name}: OnFail ({error.Message})");
	}
}

/// <summary>
/// 스킬 에러 테스트용 소환검 스킬. 시작할 때 전역 포인터 목록에 TestSword가 없으면 NoSummonedSwordError를 낸다. 10틱 동안 진행한다.
/// </summary>
public class TestSummonedSwordFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestSummonedSwordFunction;
	public override string Name => "TestSummonedSwordFunction";
	public override int TickCount => 10;

	protected override void OnStart(FunctionTask task)
	{
		if (GetGlobalPointers<TestSword>(task).Count <= 0) Fail(new NoSummonedSwordError());
	}
}

/// <summary>
/// Stop 테스트용 함수. 50틱짜리지만 5틱째에 Stop을 불러 에러 없이 일찍 끝난다.
/// </summary>
public class TestStopFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestStopFunction;
	public override string Name => "TestStopFunction";
	public override int TickCount => 50;

	protected override void OnTick(FunctionTask task, int tick)
	{
		if (tick == 5) Stop();
	}
}

/// <summary>
/// 쿨타임 테스트용 함수. 10틱 동안 진행하고, 쿨타임은 20틱이다.
/// </summary>
public class TestCooldownFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestCooldownFunction;
	public override string Name => "TestCooldownFunction";
	public override int TickCount => 10;
	public override int CooldownTicks => 20;
}

/// <summary>
/// 힙 테스트용 객체. 소환검 대신 힙에 올린다. 오브젝트가 아니라서 프리팹이 필요 없다.
/// </summary>
public class TestSword { }

/// <summary>
/// 힙 테스트용 malloc 함수. 1틱째에 TestSword를 힙 2칸에 올리고, 5틱 동안 진행한다. free하지 않는다.
/// </summary>
public class TestMallocFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestMallocFunction;
	public override string Name => "TestMallocFunction";
	public override int TickCount => 5;

	protected override void OnTick(FunctionTask task, int tick)
	{
		if (tick != 1) return;

		int pointer = Malloc(task, new TestSword(), 2);
		Debug.Log($"{Name}: malloc → {(pointer == HeapStorage.NullPointer ? "NullPointer" : pointer.ToString())}");
	}
}

/// <summary>
/// 힙 테스트용 malloc 함수. 1틱째에 TestSword를 힙 2칸에 올리고, 20틱 동안 진행한다. 도중에 실행 중단해서 누수를 만들 때 쓴다.
/// </summary>
public class TestLongMallocFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestLongMallocFunction;
	public override string Name => "TestLongMallocFunction";
	public override int TickCount => 20;

	protected override void OnTick(FunctionTask task, int tick)
	{
		if (tick != 1) return;

		int pointer = Malloc(task, new TestSword(), 2);
		Debug.Log($"{Name}: malloc → {pointer}");
	}
}

/// <summary>
/// 힙 테스트용 free 함수. 시작할 때 전역 포인터 목록의 첫 TestSword를 free한다. 1틱짜리다.<br/>
/// 전역 포인터 목록에 TestSword가 없으면 NullPointer를 free해서 InvalidPointerError를 낸다.
/// </summary>
public class TestFreeFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestFreeFunction;
	public override string Name => "TestFreeFunction";
	public override int TickCount => 1;

	protected override void OnStart(FunctionTask task)
	{
		var pointers = GetGlobalPointers<TestSword>(task);
		int pointer = pointers.Count > 0 ? pointers[0] : HeapStorage.NullPointer;
		if (Free(task, pointer)) Debug.Log($"{Name}: free {pointer}");
	}
}

/// <summary>
/// 파라미터 값 테스트용 함수. int 파라미터 amount 하나를 받고, 시작할 때 읽은 값을 ArgumentRead로 알린다. 5틱 동안 진행하고 쿨타임은 7틱이다.
/// </summary>
public class TestArgumentFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestArgumentFunction;
	public override string Name => "TestArgumentFunction";
	public override int TickCount => 5;
	public override int CooldownTicks => 7;
	public override System.Collections.Generic.IReadOnlyList<ParameterData> Parameters =>
		new[] { new ParameterData("amount", new VariableType(VariableTypeKind.Int)) };

	/// <summary>
	/// 시작할 때 읽은 amount. 값이 없으면 "값 없음"을 넘긴다. 테스트 컴포넌트가 결과를 확인할 때 쓴다.
	/// </summary>
	public static event System.Action<string> ArgumentRead;

	protected override void OnStart(FunctionTask task)
	{
		ArgumentRead?.Invoke(TryGetArgument("amount", out RuntimeValue amount) ? amount.ToString() : "값 없음");
	}
}

/// <summary>
/// 반환 값 테스트용 함수. int 파라미터 amount를 받아 마지막 틱에 amount × 2를 돌려준다. 3틱 동안 진행하고 쿨타임은 4틱이다.
/// </summary>
public class TestReturnFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestReturnFunction;
	public override string Name => "TestReturnFunction";
	public override int TickCount => 3;
	public override int CooldownTicks => 4;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Int);
	public override System.Collections.Generic.IReadOnlyList<ParameterData> Parameters =>
		new[] { new ParameterData("amount", new VariableType(VariableTypeKind.Int)) };

	protected override void OnEnd(FunctionTask task)
	{
		int amount = TryGetArgument("amount", out RuntimeValue value) ? value.AsInt() : 0;
		Return(RuntimeValue.FromInt(amount * 2));
	}
}

/// <summary>
/// 반환 값 테스트용 함수. 반환 타입이 int인데 Return을 부르지 않는 잘못 만든 스킬이다. 1틱 동안 진행한다.
/// </summary>
public class TestMissingReturnFunction : PredefinedFunction
{
	public override string Id => PredefinedFunctionIds.TestMissingReturnFunction;
	public override string Name => "TestMissingReturnFunction";
	public override int TickCount => 1;
	public override VariableType ReturnType => new VariableType(VariableTypeKind.Int);
}
