using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 호출 블록(PredefinedCallBlock) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 메모리를 10칸 쓰므로 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.<br/>
/// 없는 고정 ID를 거부하는 테스트가 콘솔 오류 1개(「실행할 수 없습니다」)를 내는 것이 정상이다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class SkillCallTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	// 함수 이름마다 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	// 함수마다 따로 두는 이유: 같은 스킬을 쓰는 함수는 앞 함수가 끝난 뒤에 시전하므로 시전한 틱이 다르기 때문
	readonly Dictionary<string, int> castTicks = new Dictionary<string, int>();
	// 앞 작업이 끝나고 스킬의 쿨타임이 지나기를 기다렸다가 시전할 함수
	// 같은 스킬은 동시에 실행되지 않고 기다리므로, 함께 시전하면 기다린 틱이 기대 틱에 섞이기 때문
	readonly List<(FunctionTask previous, PredefinedFunction skill, FunctionData data)> waitingCasts = new List<(FunctionTask, PredefinedFunction, FunctionData)>();
	// TestArgumentFunction이 읽은 값. 두 함수가 차례로 실행되므로 읽은 순서로 확인한다
	readonly Queue<string> expectedArguments = new Queue<string>(new[] { "값 없음", "5" });

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
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
	}

	// 테스트 2: 실행. 끝나는 틱은 각 함수를 시전한 틱부터 센다
	void CastFunctions()
	{
		// TestArgumentFunction("abc") → 틱 1에 TypeMismatch. 다른 함수보다 먼저 끝나 메모리를 돌려주도록 맨 먼저 시전한다
		// argument를 바꾸지 못해 스킬이 시작하지 않으므로, 같은 스킬을 쓰는 EmptyArgument를 막지 않는다
		FunctionData mismatch = FunctionData.Create("Mismatch");
		mismatch.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction,
			SlotData.FromLiteral(new VariableType(VariableTypeKind.String), "abc")));
		CastConverted(mismatch);

		// 이슈 완료 조건 2: 스킬 블록 하나를 넣은 함수는 슬롯에서 바로 시전한 스킬과 같은 틱(30)에 끝난다
		// 같은 스킬이라 함께 실행할 수 없으므로, 슬롯에서 시전한 A가 끝난 뒤에 시전한다
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.TestFunctionA, out PredefinedFunction functionA);
		FunctionTask slotA = Cast(functionA);
		FunctionData wrapped = FunctionData.Create("WrappedA");
		wrapped.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestFunctionA));
		CastAfter(slotA, functionA, wrapped);

		// TestArgumentFunction() (value slot 비움) → 값 없음. 틱 1~5 스킬
		FunctionData empty = FunctionData.Create("EmptyArgument");
		empty.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction, SlotData.EmptyValue()));
		FunctionTask emptyTask = CastConverted(empty);

		// int x = 3; TestArgumentFunction(x + 2); → 5. 틱 1 선언, 틱 2 더하기, 틱 3~7 스킬
		// EmptyArgument의 스킬이 끝나고 쿨타임(7틱)이 지난 뒤에 시전한다
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.TestArgumentFunction, out PredefinedFunction argumentFunction);
		FunctionData argument = FunctionData.Create("Argument");
		argument.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "3")));
		argument.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestArgumentFunction,
			SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable("x"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "2")))));
		CastAfter(emptyTask, argumentFunction, argument);
	}

	void FixedUpdate()
	{
		// 뒤에서부터 돈다. 시전한 항목을 목록에서 빼도 아직 보지 않은 항목의 번호가 바뀌지 않게 하기 위함
		for (int i = waitingCasts.Count - 1; i >= 0; i--)
		{
			(FunctionTask previous, PredefinedFunction skill, FunctionData data) = waitingCasts[i];
			if (previous != null && !previous.IsEnded) continue;
			if (executor.GetRemainingCooldownTicks(skill) > 0) continue;

			waitingCasts.RemoveAt(i);
			CastConverted(data);
		}
	}

	FunctionTask Cast(FunctionBlock function)
	{
		castTicks[function.Name] = executor.CurrentTick;
		return executor.Cast(function);
	}

	FunctionTask CastConverted(FunctionData data)
	{
		if (FunctionDataConverter.TryConvert(data, out UserFunction converted)) return Cast(converted);

		Check($"{data.Name}: 변환 성공", false);
		return null;
	}

	// previous가 끝나고 skill의 쿨타임이 지나면 data를 바꿔 시전한다. previous가 null(시전 거부, 변환 실패)이면 쿨타임만 기다린다
	void CastAfter(FunctionTask previous, PredefinedFunction skill, FunctionData data)
	{
		waitingCasts.Add((previous, skill, data));
	}

	void OnArgumentRead(string value)
	{
		string expected = expectedArguments.Count > 0 ? expectedArguments.Dequeue() : "(더 없음)";
		Check($"TestArgumentFunction이 읽은 amount {expected}", value == expected, $"실제 {value}");
	}

	void OnTaskEnded(FunctionTask task)
	{
		if (!castTicks.TryGetValue(task.Name, out int castTick)) return;

		int tick = executor.CurrentTick - castTick;
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
