using System.Linq;
using UnityEngine;

/// <summary>
/// 조건문 블록(ConditionalBlock)과 조건문 변환(FunctionDataConverter) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙이고, 실패 처리 표에 TypeMismatch 줄이 있어야 모두 통과한다.<br/>
/// 앞에 if가 없는 갈래를 변환하지 않는지 확인하므로 콘솔 오류 2개가 정상이다.<br/>
/// 두 번에 나누어 시전한다. 처음 함수 4개가 모두 끝나면 다음 함수 3개를 시전한다. 한 번에 메모리를 8칸까지 쓰므로 메모리 크기가 8칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ConditionalTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);
	static readonly VariableType String = new VariableType(VariableTypeKind.String);

	FunctionExecutor executor;
	// 지금 묶음을 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;
	// 처음 묶음에서 끝난 함수 수. 4개가 모두 끝나면 다음 묶음을 시전한다
	int firstEndedCount;
	// ReturnInIf에서 return 뒤의 블록이 실행되었는지
	bool ranAfterReturn;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestConvertRejects();
		CastFirst();
	}

	// 테스트 1: 앞에 if가 없는 갈래는 변환하지 않는다 (콘솔 오류 2개가 정상)
	void TestConvertRejects()
	{
		FunctionData elseFirst = FunctionData.Create("ElseFirst");
		elseFirst.Blocks.Add(BlockData.Conditional(ConditionalBranch.Else));
		Check("ElseFirst: if 없이 놓인 else는 변환 거부", !FunctionDataConverter.TryConvert(elseFirst, out _));

		// if (true) { } else { } else if (true) { } → else 뒤의 else if는 앞에 if가 없는 것으로 본다
		FunctionData afterElse = FunctionData.Create("ElseIfAfterElse");
		afterElse.Blocks.Add(BlockData.Conditional(ConditionalBranch.If, SlotData.FromLiteral(Bool, "true")));
		afterElse.Blocks.Add(BlockData.Conditional(ConditionalBranch.Else));
		afterElse.Blocks.Add(BlockData.Conditional(ConditionalBranch.ElseIf, SlotData.FromLiteral(Bool, "true")));
		Check("ElseIfAfterElse: else 뒤의 else if는 변환 거부", !FunctionDataConverter.TryConvert(afterElse, out _));
	}

	// 테스트 2: 처음 묶음. 블록 하나 = 1틱, 실행이 닿은 갈래마다 1틱
	void CastFirst()
	{
		startTick = executor.CurrentTick;

		// int x = 시작값; if (x > 3) { x = 1; } else if (x == 0) { x = 2; } else { x = 3; }
		// 5: 틱 1 선언, 틱 2 비교, 틱 3 if, 틱 4 대입, 틱 5 읽기 → x 1
		// 0: 틱 3 if(거짓), 틱 4 비교, 틱 5 else if, 틱 6 대입, 틱 7 읽기 → x 2
		// -1: 틱 5 else if(거짓), 틱 6 else, 틱 7 대입, 틱 8 읽기 → x 3
		CastWithReads(MakeChain("Chain5", "5"), ("x", "1"));
		CastWithReads(MakeChain("Chain0", "0"), ("x", "2"));
		CastWithReads(MakeChain("ChainMinus1", "-1"), ("x", "3"));

		// int x = 7; if (false) { x = 1; } → 틱 1 선언, 틱 2 if(고른 갈래 없음), 틱 3 읽기 → x 7
		FunctionData noBranch = FunctionData.Create("NoBranch");
		noBranch.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, "7")));
		noBranch.Blocks.Add(If(SlotData.FromLiteral(Bool, "false"), BlockData.Assign("x", SlotData.FromLiteral(Int, "1"))));
		CastWithReads(noBranch, ("x", "7"));
	}

	static FunctionData MakeChain(string name, string start)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromLiteral(Int, start)));
		data.Blocks.Add(If(Compare("x", CompareOperator.Greater, "3"), BlockData.Assign("x", SlotData.FromLiteral(Int, "1"))));
		data.Blocks.Add(Branch(ConditionalBranch.ElseIf, Compare("x", CompareOperator.Equal, "0"), BlockData.Assign("x", SlotData.FromLiteral(Int, "2"))));
		data.Blocks.Add(Branch(ConditionalBranch.Else, null, BlockData.Assign("x", SlotData.FromLiteral(Int, "3"))));
		return data;
	}

	// 테스트 3: 다음 묶음. 처음 묶음이 끝난 뒤 시전한다. 함께 시전하면 메모리를 14칸 쓰기 때문
	void CastSecond()
	{
		startTick = executor.CurrentTick;

		// if (true) { int y = 1; } int y = 2; → 틱 1 if, 틱 2 { } 안 선언, 틱 3 선언, 틱 4 읽기 → y 2 (}에서 안쪽 y가 사라졌으므로 다시 선언할 수 있음)
		FunctionData scope = FunctionData.Create("ScopeAfterIf");
		scope.Blocks.Add(If(SlotData.FromLiteral(Bool, "true"), BlockData.Declare(Int, "y", SlotData.FromLiteral(Int, "1"))));
		scope.Blocks.Add(BlockData.Declare(Int, "y", SlotData.FromLiteral(Int, "2")));
		CastWithReads(scope, ("y", "2"));

		// if (true) { return; } 확인 블록; → 틱 1 if, 틱 2 return → 틱 2에 정상 종료, 확인 블록은 실행되지 않음
		// 함수 데이터의 return 블록은 아직 바꾸지 않으므로 블록을 직접 만든다
		executor.Cast(new UserFunction("ReturnInIf",
			new ConditionalBlock(new ConditionalBlock.Branch(new LiteralValue(Bool, "true"), new ReturnBlock())),
			new TestProbeBlock(context => ranAfterReturn = true)));

		// string s = "a"; if (s) { } → 틱 2에 if가 TypeMismatch (string은 조건으로 쓸 수 없음)
		FunctionData stringCondition = FunctionData.Create("StringCondition");
		stringCondition.Blocks.Add(BlockData.Declare(String, "s", SlotData.FromLiteral(String, "a")));
		stringCondition.Blocks.Add(If(SlotData.FromVariable("s")));
		if (Convert(stringCondition, out UserFunction converted)) executor.Cast(converted);
	}

	static BlockData If(SlotData condition, params BlockData[] body) => Branch(ConditionalBranch.If, condition, body);

	static BlockData Branch(ConditionalBranch branch, SlotData condition, params BlockData[] body)
	{
		BlockData block = BlockData.Conditional(branch, condition);
		block.InnerBlocks.AddRange(body);
		return block;
	}

	static SlotData Compare(string variableName, CompareOperator op, string literal) =>
		SlotData.FromBlock(BlockData.Compare(SlotData.FromVariable(variableName), op, SlotData.FromLiteral(Int, literal)));

	// 바꾼 함수 끝에 테스트용 읽기 블록들을 붙여 시전한다. 함수가 끝났을 때의 변수 값을 확인하기 위함
	void CastWithReads(FunctionData data, params (string name, string expected)[] reads)
	{
		if (!Convert(data, out UserFunction converted)) return;

		Block[] readBlocks = reads.Select(r => (Block)new TestReadBlock(new VariableValue(r.name),
			v => Check($"{data.Name}: 끝났을 때 {r.name} {r.expected}", v.ToString() == r.expected, $"실제 {v}"))).ToArray();
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Concat(readBlocks).ToArray()));
	}

	static bool Convert(FunctionData data, out UserFunction converted)
	{
		bool ok = FunctionDataConverter.TryConvert(data, out converted);
		if (!ok) Check($"{data.Name}: 변환 성공", false);
		return ok;
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "Chain5": CheckCompleted(task, tick, 5); break;
			case "Chain0": CheckCompleted(task, tick, 7); break;
			case "ChainMinus1": CheckCompleted(task, tick, 8); break;
			case "NoBranch": CheckCompleted(task, tick, 3); break;

			case "ScopeAfterIf": CheckCompleted(task, tick, 4); return;
			case "ReturnInIf":
				CheckCompleted(task, tick, 2);
				Check("ReturnInIf: return 뒤의 블록은 실행되지 않음", !ranAfterReturn);
				return;
			case "StringCondition":
				Check($"{task.Name}: 틱 2에 TypeMismatch로 실패", task.Failure == FunctionFailure.TypeMismatch && tick == 2,
					$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
				return;
			default: return;
		}

		if (++firstEndedCount == 4) CastSecond();
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ConditionalTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
