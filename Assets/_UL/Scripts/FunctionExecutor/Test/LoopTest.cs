using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 반복문(ForBlock, WhileBlock), break, continue와 BlockData.HasJumpOutsideLoop 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 연결한 언어 규칙 파일이 C 규칙이고, 실패 처리 표에 NotInLoop 줄(처리 EndTask, 분류 CompileError)과 UndeclaredVariable 줄이 있어야 모두 통과한다.<br/>
/// 두 번에 나누어 시전한다. 처음 함수 3개가 모두 끝나면 다음 함수 2개를 시전한다. 한 번에 메모리를 7칸까지 쓰므로 메모리 크기가 7칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class LoopTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);

	FunctionExecutor executor;
	// 지금 묶음을 시전한 순간의 틱. 끝나는 틱은 이 틱부터 센다. 캐릭터가 네트워크로 늦게 생성되면 실행기가 먼저 돌고 있기 때문
	int startTick;
	// 처음 묶음에서 끝난 함수 수. 3개가 모두 끝나면 다음 묶음을 시전한다
	int firstEndedCount;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		executor.TaskEnded += OnTaskEnded;

		TestHasJumpOutsideLoop();
		CastFirst();
	}

	// 테스트 1: 반복문 밖의 break, continue 찾기
	void TestHasJumpOutsideLoop()
	{
		Check("[break] → 반복문 밖", BlockData.HasJumpOutsideLoop(new[] { BlockData.Break() }));
		Check("while { break } → 반복문 안", !BlockData.HasJumpOutsideLoop(new[] { While(Literal(Bool, "true"), BlockData.Break()) }));
		Check("if { continue } → 반복문 밖", BlockData.HasJumpOutsideLoop(new[] { If(Literal(Bool, "true"), BlockData.Continue()) }));
		Check("for { if { break } } → 반복문 안", !BlockData.HasJumpOutsideLoop(new[] { For("i", "0", Literal(Int, "3"), If(Literal(Bool, "true"), BlockData.Break())) }));
	}

	// 테스트 2: 처음 묶음. 블록 하나 = 1틱, 반복문은 검사 한 번 = 1틱
	void CastFirst()
	{
		startTick = executor.CurrentTick;

		// int s = 0; for (int i = 0; i < 3; i++) { s = s + i; }
		// 틱 1 선언, 틱 2 검사, 틱 3~4 더하기·대입, 바퀴마다 3틱 × 3, 틱 11 마지막 검사(거짓), 틱 12 읽기 → s 3
		FunctionData forSum = FunctionData.Create("ForSum");
		forSum.Blocks.Add(BlockData.Declare(Int, "s", Literal(Int, "0")));
		forSum.Blocks.Add(For("i", "0", Literal(Int, "3"), AddTo("s", "i")));
		CastWithReads(forSum, ("s", "3"));

		// int n = 0; int s = 0; while (true) { n = n + 1; if (n == 2) { continue; } if (n > 3) { break; } s = s + n; }
		// n 1: s 1, n 2: continue, n 3: s 4, n 4: break → n 4, s 4
		// 틱 1~2 선언, 바퀴 1: 틱 3 검사 ~ 틱 11, 바퀴 2: 틱 12 검사 ~ 틱 17 continue, 바퀴 3: 틱 18 검사 ~ 틱 26, 바퀴 4: 틱 27 검사 ~ 틱 34 break, 틱 35~36 읽기
		FunctionData whileJump = FunctionData.Create("WhileJump");
		whileJump.Blocks.Add(BlockData.Declare(Int, "n", Literal(Int, "0")));
		whileJump.Blocks.Add(BlockData.Declare(Int, "s", Literal(Int, "0")));
		whileJump.Blocks.Add(While(Literal(Bool, "true"),
			AddTo("n", null),
			If(Compare("n", CompareOperator.Equal, "2"), BlockData.Continue()),
			If(Compare("n", CompareOperator.Greater, "3"), BlockData.Break()),
			AddTo("s", "n")));
		CastWithReads(whileJump, ("n", "4"), ("s", "4"));

		// break; → 틱 1에 NotInLoop (반복문 밖)
		FunctionData breakOutside = FunctionData.Create("BreakOutside");
		breakOutside.Blocks.Add(BlockData.Break());
		if (Convert(breakOutside, out UserFunction converted)) executor.Cast(converted);
	}

	// 테스트 3: 다음 묶음. 처음 묶음이 끝난 뒤 시전한다. 함께 시전하면 메모리를 14칸 쓰기 때문
	void CastSecond()
	{
		startTick = executor.CurrentTick;

		// int e = 3; int c = 0; for (int i = 0; i < e; i++) { e = 1; c = c + 1; }
		// end를 검사마다 다시 읽으므로 두 번째 검사(1 < 1)에서 끝난다 → c 1
		// 틱 1~2 선언, 틱 3 검사, 틱 4 대입, 틱 5~6 더하기·대입, 틱 7 검사(거짓), 틱 8 읽기
		FunctionData endReread = FunctionData.Create("EndReread");
		endReread.Blocks.Add(BlockData.Declare(Int, "e", Literal(Int, "3")));
		endReread.Blocks.Add(BlockData.Declare(Int, "c", Literal(Int, "0")));
		endReread.Blocks.Add(For("i", "0", SlotData.FromVariable("e"),
			BlockData.Assign("e", Literal(Int, "1")),
			AddTo("c", null)));
		CastWithReads(endReread, ("c", "1"));

		// for (int i = 0; i < 2; i++) { int t = i; } 읽기 i;
		// { } 안의 t는 바퀴마다 새 변수이므로 DuplicateVariable이 나지 않는다. i는 for가 끝나면 사라진다
		// 틱 1 검사, 틱 2 선언, 틱 3 검사, 틱 4 선언, 틱 5 검사(거짓), 틱 6 i 읽기 → UndeclaredVariable
		FunctionData scopePerLap = FunctionData.Create("ScopePerLap");
		scopePerLap.Blocks.Add(For("i", "0", Literal(Int, "2"), BlockData.Declare(Int, "t", SlotData.FromVariable("i"))));
		CastWithReads(scopePerLap, ("i", "(없음)"));
	}

	static SlotData Literal(VariableType type, string literal) => SlotData.FromLiteral(type, literal);

	static SlotData Compare(string variableName, CompareOperator op, string literal) =>
		SlotData.FromBlock(BlockData.Compare(SlotData.FromVariable(variableName), op, Literal(Int, literal)));

	// variableName = variableName + (addend 변수, 없으면 1). 더하기 1틱, 대입 1틱
	static BlockData AddTo(string variableName, string addend)
	{
		SlotData right = addend == null ? Literal(Int, "1") : SlotData.FromVariable(addend);
		return BlockData.Assign(variableName, SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable(variableName), ArithmeticOperator.Add, right)));
	}

	static BlockData If(SlotData condition, params BlockData[] body) =>
		WithBody(BlockData.Conditional(ConditionalBranch.If, condition), body);

	static BlockData While(SlotData condition, params BlockData[] body) => WithBody(BlockData.While(condition), body);

	static BlockData For(string indexName, string start, SlotData end, params BlockData[] body) =>
		WithBody(BlockData.For(indexName, Literal(Int, start), end), body);

	static BlockData WithBody(BlockData block, IEnumerable<BlockData> body)
	{
		block.InnerBlocks.AddRange(body);
		return block;
	}

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
			case "ForSum": CheckCompleted(task, tick, 12); break;
			case "WhileJump": CheckCompleted(task, tick, 36); break;
			case "BreakOutside": CheckFailed(task, tick, 1, FunctionFailure.NotInLoop, FailureCategory.CompileError); break;

			case "EndReread": CheckCompleted(task, tick, 8); return;
			case "ScopePerLap": CheckFailed(task, tick, 6, FunctionFailure.UndeclaredVariable, FailureCategory.CompileError); return;
			default: return;
		}

		if (++firstEndedCount == 3) CastSecond();
	}

	static void CheckCompleted(FunctionTask task, int tick, int expectedTick)
	{
		Check($"{task.Name}: 틱 {expectedTick}에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}");
	}

	void CheckFailed(FunctionTask task, int tick, int expectedTick, FunctionFailure expected, FailureCategory expectedCategory)
	{
		FailureCategory? category = task.Failure.HasValue ? GetComponent<LanguageRule>().GetFailureCategory(task.Failure.Value) : null;
		Check($"{task.Name}: 틱 {expectedTick}에 {expected}({expectedCategory})로 실패",
			task.Failure == expected && category == expectedCategory && tick == expectedTick,
			$"실제 틱 {tick}, {task.EndReason}, {task.Failure}, {category}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[LoopTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
