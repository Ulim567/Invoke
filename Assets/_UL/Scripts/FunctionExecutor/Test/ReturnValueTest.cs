using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 값을 돌려주는 함수 호출 블록(PredefinedCallBlock)과 스킬의 Return 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 콘솔 오류 3개가 나는 것이 정상이다: 반환 값이 없는 함수를 expression으로 쓴 함수의 변환 거부 1개, TestMissingReturnFunction의 「Return을 부르지 않고 끝났습니다」 2개(value slot, statement).<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ReturnValueTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	FunctionExecutor executor;
	// 함수 이름마다 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	// 함수마다 따로 두는 이유: 같은 스킬을 쓰는 함수는 앞 함수가 끝난 뒤에 시전하므로 시전한 틱이 다르기 때문
	readonly Dictionary<string, int> castTicks = new Dictionary<string, int>();
	// 앞 작업이 끝나고 스킬의 쿨타임이 지나기를 기다렸다가 시전할 함수
	// 같은 스킬은 동시에 실행되지 않고 기다리므로, 함께 시전하면 기다린 틱이 기대 틱에 섞이기 때문
	readonly List<(FunctionTask previous, PredefinedFunction skill, FunctionData data)> waitingCasts = new List<(FunctionTask, PredefinedFunction, FunctionData)>();

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestConvert();
		CastFunctions();
	}

	// 테스트 1: 변환
	void TestConvert()
	{
		// int a = TestFunctionA(); → TestFunctionA는 값을 돌려주지 않으므로 거부
		FunctionData voidValue = FunctionData.Create("VoidValue");
		voidValue.Blocks.Add(BlockData.Declare(Int, "a", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestFunctionA))));
		Check("반환 값이 없는 함수를 expression으로 쓰면 거부 (콘솔 오류 1개가 정상)", !FunctionDataConverter.TryConvert(voidValue, out _));
	}

	// 테스트 2: 실행. 끝나는 틱은 각 함수를 시전한 틱부터 센다
	void CastFunctions()
	{
		// int m = TestMissingReturnFunction(); → 틱 1에 InternalError (콘솔 오류 1개가 정상). 메모리를 먼저 돌려주도록 맨 먼저 시전한다
		FunctionData missing = FunctionData.Create("MissingReturn");
		missing.Blocks.Add(BlockData.Declare(Int, "m", SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestMissingReturnFunction))));
		FunctionTask missingTask = CastConverted(missing);

		// TestMissingReturnFunction(); → statement로 불러도 틱 1에 InternalError (콘솔 오류 1개가 정상)
		// 같은 스킬이 놓인 자리에 따라 성공하거나 실패하지 않는지 확인한다
		// 같은 스킬이라 함께 실행할 수 없으므로, MissingReturn이 끝난 뒤에 시전한다
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.TestMissingReturnFunction, out PredefinedFunction missingReturnFunction);
		FunctionData missingStatement = FunctionData.Create("MissingReturnStatement");
		missingStatement.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestMissingReturnFunction));
		CastAfter(missingTask, missingReturnFunction, missingStatement);

		// 이슈 완료 조건 3. int n = TestReturnFunction(4);
		// 틱 1~3 스킬, 틱 4 선언, 틱 5 읽기(테스트용) → n 8
		FunctionData returned = FunctionData.Create("Returned");
		returned.Blocks.Add(BlockData.Declare(Int, "n", SlotData.FromBlock(
			BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestReturnFunction, SlotData.FromLiteral(Int, "4")))));
		FunctionTask returnedTask = null;
		if (FunctionDataConverter.TryConvert(returned, out UserFunction converted))
		{
			Block read = new TestReadBlock(new VariableValue("n"), v => Check("Returned: 끝났을 때 n 8", v.ToString() == "8", $"실제 {v}"));
			returnedTask = Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(read).ToArray()));
		}
		else Check("Returned: 변환 성공", false);

		// TestReturnFunction(1); → statement로 놓으면 반환 값을 버리고 틱 3에 끝난다
		// Returned의 스킬이 끝나고 쿨타임(4틱)이 지난 뒤에 시전한다
		PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.TestReturnFunction, out PredefinedFunction returnFunction);
		FunctionData discarded = FunctionData.Create("Discarded");
		discarded.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.TestReturnFunction, SlotData.FromLiteral(Int, "1")));
		CastAfter(returnedTask, returnFunction, discarded);
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

	void OnTaskEnded(FunctionTask task)
	{
		if (!castTicks.TryGetValue(task.Name, out int castTick)) return;

		int tick = executor.CurrentTick - castTick;
		switch (task.Name)
		{
			case "Returned": CheckCompleted(task, tick, 5); break;
			case "Discarded": CheckCompleted(task, tick, 3); break;
			case "MissingReturn":
			case "MissingReturnStatement":
				CheckMissingReturn(task, tick);
				break;
		}
	}

	void CheckMissingReturn(FunctionTask task, int tick)
	{
		// 개발자 책임이므로 플레이어의 실패(Failed, Failure)로 보이지 않아야 한다
		Check($"{task.Name}: 틱 1에 InternalError로 끝나고 Failure 없음",
			task.EndReason == FunctionTaskEndReason.InternalError && !task.IsFailed && task.Failure == null && tick == 1,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료",
			task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ReturnValueTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
