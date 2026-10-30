using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 호출 블록(CallPredefinedBlock) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 메모리를 10칸 쓰므로 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.<br/>
/// 없는 고정 ID를 거부하는 테스트가 콘솔 오류 1개(「실행할 수 없습니다」)를 내는 것이 정상이다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class SkillCallTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	// 테스트가 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;
	// TestArgumentFunction이 읽은 값. 함께 진행하는 두 함수가 시작하는 틱이 달라서 읽은 순서로 확인한다
	readonly Queue<string> expectedArguments = new Queue<string>(new[] { "값 없음", "5" });

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		startTick = executor.CurrentTick;
		executor.TaskEnded += OnTaskEnded;
		TestArgumentFunction.ArgumentRead += OnArgumentRead;

		TestConvert();
		CastFunctions();
	}

	void OnDestroy()
	{
		TestArgumentFunction.ArgumentRead -= OnArgumentRead;
	}

	// 테스트 1: 변환
	void TestConvert()
	{
		FunctionData unknown = FunctionData.Create("Unknown");
		unknown.Blocks.Add(BlockData.CallPredefinedFunction("test.no_such_function"));
		Check("없는 고정 ID 거부 (콘솔 오류 1개가 정상)", !FunctionDataConverter.TryConvert(unknown, out _));

		// 스킬 블록을 담은 함수의 쿨타임은 그 스킬의 쿨타임(7틱)이다
		FunctionData cooldown = FunctionData.Create("Cooldown");
		cooldown.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction, SlotData.EmptyValue()));
		bool ok = FunctionDataConverter.TryConvert(cooldown, out UserFunction function);
		Check("스킬 블록을 담은 함수의 쿨타임 7", ok && function.CooldownTicks == 7, ok ? $"실제 {function.CooldownTicks}" : "변환 실패");
	}

	// 테스트 2: 실행. 모두 틱 1부터 함께 진행한다
	void CastFunctions()
	{
		// TestArgumentFunction("abc") → 틱 1에 TypeMismatch. 다른 함수보다 먼저 끝나 메모리를 돌려주도록 맨 먼저 시전한다
		FunctionData mismatch = FunctionData.Create("Mismatch");
		mismatch.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction,
			SlotData.FromLiteral(new VariableType(VariableTypeKind.String), "abc")));
		CastConverted(mismatch);

		// 이슈 완료 조건 2: 스킬 블록 하나를 넣은 함수는 슬롯에서 바로 시전한 스킬과 같은 틱(30)에 끝난다
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.TestFunctionA, out PredefinedFunction functionA);
		executor.Cast(functionA);
		FunctionData wrapped = FunctionData.Create("WrappedA");
		wrapped.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestFunctionA));
		CastConverted(wrapped);

		// TestArgumentFunction() (값 칸 비움) → 값 없음. 틱 1~5 스킬
		FunctionData empty = FunctionData.Create("EmptyArgument");
		empty.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction, SlotData.EmptyValue()));
		CastConverted(empty);

		// int x = 3; TestArgumentFunction(x + 2); → 5. 틱 1 선언, 틱 2 더하기, 틱 3~7 스킬
		FunctionData argument = FunctionData.Create("Argument");
		argument.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));
		argument.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction,
			SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable("x"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "2")))));
		CastConverted(argument);
	}

	void CastConverted(FunctionData data)
	{
		if (FunctionDataConverter.TryConvert(data, out UserFunction converted)) executor.Cast(converted);
		else Check($"{data.Name}: 변환 성공", false);
	}

	void OnArgumentRead(string value)
	{
		string expected = expectedArguments.Count > 0 ? expectedArguments.Dequeue() : "(더 없음)";
		Check($"TestArgumentFunction이 읽은 amount {expected}", value == expected, $"실제 {value}");
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "TestFunctionA": CheckCompleted(task, tick, 30); break;
			case "WrappedA": CheckCompleted(task, tick, 30); break;
			case "EmptyArgument": CheckCompleted(task, tick, 5); break;
			case "Argument": CheckCompleted(task, tick, 7); break;
			case "Mismatch":
				Check("Mismatch: 틱 1에 TypeMismatch로 실패",
					task.Failure == FunctionFailure.TypeMismatch && tick == 1, $"실제 틱 {tick}, {task.Failure}");
				break;
		}
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료",
			task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[SkillCallTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
