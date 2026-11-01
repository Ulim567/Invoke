using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 사용자 정의 함수 호출(파라미터, 반환 값, 스택 프레임, 재귀, 비동기)과 return 블록 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 콘솔 오류 4개가 나는 것이 정상이다: 변환 거부(없는 GUID, argument 모자람, 비운 argument, 비동기 함수를 expression으로 씀) 4개.<br/>
/// 캐릭터의 언어 규칙 파일(LanguageRule_C)에 MissingReturn 줄이 있어야 한다. 없으면 MissingReturn, EmptyReturn 단계가 InternalError로 끝나 실패한다.<br/>
/// memory는 기본값 10칸 이상이어야 한다(재귀 단계가 8칸을 씀).<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[UserCallTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class UserCallTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Bool = new VariableType(VariableTypeKind.Bool);

	// 테스트 단계. 앞 단계의 작업이 끝나야 다음 단계를 시전한다. 다른 작업의 프레임이 스택 사용량에 섞이지 않게 하기 위함
	enum Step { Call, Recursion, Overflow, MissingReturn, EmptyReturn, VoidReturnsValue, Async, Done }

	FunctionExecutor executor;
	ProcessMemory memory;
	Step step = Step.Call;
	bool casting;
	int failedCount;
	// 진행 중에 본 가장 큰 스택 사용량
	int maxStack;
	// 비동기 단계에서 시작한 작업 이름들
	readonly List<string> startedTasks = new List<string>();

	// 테스트에 쓰는 함수 데이터. 모두 캐릭터가 가진 함수로 변환기에 넘긴다
	readonly List<FunctionData> owned = new List<FunctionData>();
	FunctionData a, b, main, fact, loop, noReturn, emptyReturn, voidReturnsValue, asyncCaller, asyncFunction;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		executor.TaskStarted += OnTaskStarted;
		executor.TaskEnded += OnTaskEnded;

		MakeFunctions();
		TestConvert();
		TestAlwaysReturnsValue();
	}

	// 테스트에 쓰는 함수들을 만든다. 블록이 서로의 GUID를 가리키므로 모두 먼저 만들고 블록을 채운다
	void MakeFunctions()
	{
		a = Make("A");
		b = Make("B", Int, new ParameterData("v", Int));
		main = Make("Main");
		fact = Make("Fact", Int, new ParameterData("n", Int));
		loop = Make("Loop");
		noReturn = Make("NoReturn", Int);
		emptyReturn = Make("EmptyReturn", Int);
		voidReturnsValue = Make("VoidReturnsValue");
		asyncCaller = Make("AsyncCaller");
		asyncFunction = Make("AsyncFunction");
		asyncFunction.IsAsync = true;

		// 이슈 완료 조건 1. void A() { int r = 0; r = B(5); } / int B(int v) { int y = v * 2; return y; }
		// 프레임: A = 1 + 지역 변수 r = 2, B = 1 + 파라미터 v + 지역 변수 y = 3. B가 실행되는 동안 스택 5, 끝나면 2
		a.Blocks.Add(BlockData.Declare(Int, "r", Literal("0")));
		a.Blocks.Add(BlockData.Assign("r", SlotData.FromBlock(BlockData.CallUserFunction(b.Guid, Literal("5")))));
		b.Blocks.Add(BlockData.Declare(Int, "y", SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable("v"), ArithmeticOperator.Multiply, Literal("2")))));
		b.Blocks.Add(BlockData.Return(SlotData.FromVariable("y")));

		// 재귀. void Main() { int r = Fact(3); } / int Fact(int n) { if (n <= 1) { return 1; } return n * Fact(n - 1); }
		// 가장 깊을 때 스택: Main 2칸 + Fact 2칸 × 3단계 = 8. 기본 memory 10칸 안에 들어가게 3으로 한다
		main.Blocks.Add(BlockData.Declare(Int, "r", SlotData.FromBlock(BlockData.CallUserFunction(fact.Guid, Literal("3")))));
		BlockData baseCase = BlockData.Conditional(ConditionalBranch.If,
			SlotData.FromBlock(BlockData.Compare(SlotData.FromVariable("n"), CompareOperator.LessOrEqual, Literal("1"))));
		baseCase.InnerBlocks.Add(BlockData.Return(Literal("1")));
		fact.Blocks.Add(baseCase);
		SlotData factOfLess = SlotData.FromBlock(BlockData.CallUserFunction(fact.Guid,
			SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable("n"), ArithmeticOperator.Subtract, Literal("1")))));
		fact.Blocks.Add(BlockData.Return(SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromVariable("n"), ArithmeticOperator.Multiply, factOfLess))));

		// 끝나지 않는 재귀. void Loop() { Loop(); }
		loop.Blocks.Add(BlockData.CallUserFunction(loop.Guid));

		// return 누락. int NoReturn() { if (false) { return 1; } } / int EmptyReturn() { return; } / void VoidReturnsValue() { return 3; }
		BlockData never = BlockData.Conditional(ConditionalBranch.If, SlotData.FromLiteral(Bool, "false"));
		never.InnerBlocks.Add(BlockData.Return(Literal("1")));
		noReturn.Blocks.Add(never);
		emptyReturn.Blocks.Add(BlockData.Return());
		voidReturnsValue.Blocks.Add(BlockData.Return(Literal("3")));

		// 비동기 사용자 정의 함수. void AsyncCaller() { AsyncFunction(); } / async void AsyncFunction() { int z = 1; }
		asyncCaller.Blocks.Add(BlockData.CallUserFunction(asyncFunction.Guid));
		asyncFunction.Blocks.Add(BlockData.Declare(Int, "z", Literal("1")));
	}

	FunctionData Make(string name, VariableType returnType = null, params ParameterData[] parameters)
	{
		FunctionData data = FunctionData.Create(name);
		if (returnType != null) data.ReturnType = returnType;
		data.Parameters.AddRange(parameters);
		owned.Add(data);
		return data;
	}

	static SlotData Literal(string text) => SlotData.FromLiteral(Int, text);

	// 테스트 1: 변환 (거부 4개는 콘솔 오류 4개가 정상)
	void TestConvert()
	{
		FunctionData missing = FunctionData.Create("MissingGuid");
		missing.Blocks.Add(BlockData.CallUserFunction("no-such-guid"));
		Check("없는 GUID 거부", !FunctionDataConverter.TryConvert(missing, owned, out _));

		FunctionData fewer = FunctionData.Create("FewerArguments");
		fewer.Blocks.Add(BlockData.CallUserFunction(b.Guid));
		Check("argument가 모자란 호출 거부", !FunctionDataConverter.TryConvert(fewer, owned, out _));

		FunctionData empty = FunctionData.Create("EmptyArgument");
		empty.Blocks.Add(BlockData.CallUserFunction(b.Guid, SlotData.EmptyValue()));
		Check("비운 argument 거부", !FunctionDataConverter.TryConvert(empty, owned, out _));

		// int q = AsyncInt(); → 비동기 함수는 반환 값을 받을 수 없으므로 거부
		FunctionData asyncInt = FunctionData.Create("AsyncInt");
		asyncInt.ReturnType = Int;
		asyncInt.IsAsync = true;
		asyncInt.Blocks.Add(BlockData.Return(Literal("1")));
		FunctionData asyncValue = FunctionData.Create("AsyncValue");
		asyncValue.Blocks.Add(BlockData.Declare(Int, "q", SlotData.FromBlock(BlockData.CallUserFunction(asyncInt.Guid))));
		Check("비동기 함수를 expression으로 쓰면 거부", !FunctionDataConverter.TryConvert(asyncValue, new[] { asyncInt }, out _));

		// 자기 자신을 부르는 함수도 변환된다
		Check("재귀 함수 변환", FunctionDataConverter.TryConvert(fact, owned, out _));
	}

	// 테스트 2: 저장 검사가 쓸 return 누락 판정 (실행기는 쓰지 않음)
	void TestAlwaysReturnsValue()
	{
		Check("AlwaysReturnsValue: return 값;", BlockData.AlwaysReturnsValue(new[] { BlockData.Return(Literal("1")) }));
		Check("AlwaysReturnsValue: return;은 값 없음", !BlockData.AlwaysReturnsValue(new[] { BlockData.Return() }));
		Check("AlwaysReturnsValue: else 없는 if", !BlockData.AlwaysReturnsValue(noReturn.Blocks));
		Check("AlwaysReturnsValue: if 다음의 return", BlockData.AlwaysReturnsValue(fact.Blocks));

		BlockData ifReturns = BlockData.Conditional(ConditionalBranch.If, SlotData.FromLiteral(Bool, "true"));
		ifReturns.InnerBlocks.Add(BlockData.Return(Literal("1")));
		BlockData elseReturns = BlockData.Conditional(ConditionalBranch.Else);
		elseReturns.InnerBlocks.Add(BlockData.Return(Literal("2")));
		BlockData elseEmpty = BlockData.Conditional(ConditionalBranch.Else);
		Check("AlwaysReturnsValue: 모든 갈래가 return하는 if-else", BlockData.AlwaysReturnsValue(new[] { ifReturns, elseReturns }));
		Check("AlwaysReturnsValue: return 없는 else", !BlockData.AlwaysReturnsValue(new[] { ifReturns, elseEmpty }));

		BlockData whileReturns = BlockData.While(SlotData.FromLiteral(Bool, "true"));
		whileReturns.InnerBlocks.Add(BlockData.Return(Literal("1")));
		Check("AlwaysReturnsValue: 반복문 안의 return은 세지 않음", !BlockData.AlwaysReturnsValue(new[] { whileReturns }));
	}

	void FixedUpdate()
	{
		if (step == Step.Done) return;

		if (!casting)
		{
			casting = true;
			maxStack = 0;
			CastStep();
			return;
		}

		maxStack = Mathf.Max(maxStack, memory.StackUsed);
	}

	void CastStep()
	{
		switch (step)
		{
			case Step.Call:
			{
				// A 끝에 테스트 블록을 붙인다. B가 끝난 뒤의 스택과 B가 돌려준 값을 읽기 위함
				if (!TryConvert(a, out UserFunction converted)) return;
				Block stack = new TestStackProbeBlock(used => Check("Call: B가 끝나면 스택이 A의 프레임(2)으로 내려감", used == 2, $"실제 {used}"));
				Block read = new TestReadBlock(new VariableValue("r"), v => Check("Call: B(5)가 돌려준 r 10", v.ToString() == "10", $"실제 {v}"));
				executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(stack).Append(read).ToArray()));
				break;
			}

			case Step.Recursion:
			{
				if (!TryConvert(main, out UserFunction converted)) return;
				Block read = new TestReadBlock(new VariableValue("r"), v => Check("Recursion: Fact(3) 6", v.ToString() == "6", $"실제 {v}"));
				executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Append(read).ToArray()));
				break;
			}

			case Step.Overflow: CastConverted(loop); break;
			case Step.MissingReturn: CastConverted(noReturn); break;
			case Step.EmptyReturn: CastConverted(emptyReturn); break;
			case Step.VoidReturnsValue: CastConverted(voidReturnsValue); break;

			case Step.Async:
				startedTasks.Clear();
				CastConverted(asyncCaller);
				break;
		}
	}

	void CastConverted(FunctionData data)
	{
		if (TryConvert(data, out UserFunction converted)) executor.Cast(converted);
	}

	bool TryConvert(FunctionData data, out UserFunction converted)
	{
		if (FunctionDataConverter.TryConvert(data, owned, out converted)) return true;

		Check($"{data.Name}: 변환 성공", false);
		NextStep();
		return false;
	}

	void OnTaskStarted(FunctionTask task)
	{
		if (step == Step.Async) startedTasks.Add(task.Name);
	}

	void OnTaskEnded(FunctionTask task)
	{
		switch (step)
		{
			case Step.Call:
				// 이슈 완료 조건 1: A가 B를 부르면 스택이 B의 프레임(3)만큼 올라 5가 된다
				Check("Call: B가 실행되는 동안 스택 5", maxStack == 5, $"실제 가장 큰 값 {maxStack}");
				CheckEnd(task, FunctionTaskEndReason.Completed, null);
				break;

			case Step.Recursion:
				// Main 2칸 + Fact 2칸 × 3단계
				Check("Recursion: 가장 깊을 때 스택 8", maxStack == 8, $"실제 가장 큰 값 {maxStack}");
				CheckEnd(task, FunctionTaskEndReason.Completed, null);
				break;

			case Step.Overflow: CheckEnd(task, FunctionTaskEndReason.Failed, FunctionFailure.Overflow); break;
			case Step.MissingReturn: CheckEnd(task, FunctionTaskEndReason.Failed, FunctionFailure.MissingReturn); break;
			case Step.EmptyReturn: CheckEnd(task, FunctionTaskEndReason.Failed, FunctionFailure.MissingReturn); break;
			case Step.VoidReturnsValue: CheckEnd(task, FunctionTaskEndReason.Failed, FunctionFailure.TypeMismatch); break;

			case Step.Async:
				// 부른 쪽이 먼저 끝나고, 비동기 함수는 새 작업으로 그 뒤에 끝난다
				if (task.Name != "AsyncFunction") return;
				Check("Async: 부른 함수와 비동기 함수가 따로 작업으로 시작",
					startedTasks.SequenceEqual(new[] { "AsyncCaller", "AsyncFunction" }), $"실제 {string.Join(", ", startedTasks)}");
				CheckEnd(task, FunctionTaskEndReason.Completed, null);
				break;
		}
	}

	void CheckEnd(FunctionTask task, FunctionTaskEndReason reason, FunctionFailure? failure)
	{
		string expected = failure.HasValue ? $"{reason}({failure})" : reason.ToString();
		Check($"{step}: {expected}로 끝남", task.EndReason == reason && task.Failure == failure, $"실제 {task.EndReason}, {task.Failure}");
		// 프레임은 끝나는 즉시 돌려주므로, 이 작업이 끝났을 때 스택은 비어 있어야 한다
		Check($"{step}: 끝나면 스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
		NextStep();
	}

	void NextStep()
	{
		step++;
		casting = false;
		if (step != Step.Done) return;

		if (failedCount == 0) Debug.Log("[UserCallTest] 전체 통과");
		else Debug.LogError($"[UserCallTest] 실패 {failedCount}개");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[UserCallTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}

/// <summary>
/// 실행되는 순간의 스택 사용량을 읽는 테스트용 블록. 1틱을 쓴다.
/// </summary>
public class TestStackProbeBlock : Block
{
	readonly Action<int> onProbe;

	public TestStackProbeBlock(Action<int> onProbe)
	{
		this.onProbe = onProbe;
	}

	protected override BlockResult Step(FunctionContext context)
	{
		onProbe(context.Memory.StackUsed);
		return BlockResult.Done;
	}
}
