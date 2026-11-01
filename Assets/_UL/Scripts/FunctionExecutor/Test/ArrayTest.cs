using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// array 블록(배열 만들기, 원소 읽기·쓰기, 범위 밖 index, 함수에 넘기기) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 콘솔 오류 7개가 나는 것이 정상이다: 변환 거부(변수로 정한 길이, 길이 0, 프레임 크기가 int를 넘는 array, 2차원 array, 선언 블록의 array, array 반환 타입, 비동기 함수에 array 넘기기) 7개.<br/>
/// 캐릭터의 언어 규칙 파일(LanguageRule_C)에 IndexOutOfRange 줄이 있어야 한다. 없으면 범위 밖 단계가 InternalError로 끝나 실패한다.<br/>
/// memory는 기본값 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다. 「[ArrayTest] 전체 통과」가 나오면 통과다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class ArrayTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType Float = new VariableType(VariableTypeKind.Float);

	// 실행 단계 하나. 앞 단계의 작업이 끝나야 다음 단계를 시전한다. 다른 작업의 프레임이 스택 사용량에 섞이지 않게 하기 위함
	class Step
	{
		public string Name;
		public FunctionData Data;
		// 변환한 함수 끝에 붙일 테스트 블록. 끝난 뒤의 원소 값을 읽기 위함
		public Block[] Appended = Array.Empty<Block>();
		public FunctionTaskEndReason Reason = FunctionTaskEndReason.Completed;
		public FunctionFailure? Failure;
		// 0보다 크면 진행 중에 본 가장 큰 스택 사용량이 이 값이어야 한다
		public int MaxStack;
	}

	FunctionExecutor executor;
	ProcessMemory memory;
	readonly List<Step> steps = new List<Step>();
	int stepIndex;
	bool casting;
	int maxStack;
	int failedCount;

	// 테스트에 쓰는 함수 데이터. 모두 캐릭터가 가진 함수로 변환기에 넘긴다
	readonly List<FunctionData> owned = new List<FunctionData>();
	FunctionData fill;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		executor.TaskEnded += OnTaskEnded;

		// void Fill(int a[]) { a[1] = 9; a[0] = 1; } 프레임 = 1 + array 파라미터 1칸 = 2
		// 블록을 둘 두는 이유: 1틱짜리 함수는 같은 틱 안에서 프레임을 받고 돌려주므로, 틱 사이에 재는 이 테스트가 스택을 볼 수 없기 때문
		fill = Make("Fill", new ParameterData("a", VariableType.ArrayOf(Int)));
		fill.Blocks.Add(BlockData.ArraySet("a", Literal("1"), Literal("9")));
		fill.Blocks.Add(BlockData.ArraySet("a", Literal("0"), Literal("1")));

		TestConvert();
		MakeSteps();
	}

	FunctionData Make(string name, params ParameterData[] parameters)
	{
		FunctionData data = FunctionData.Create(name);
		data.Parameters.AddRange(parameters);
		owned.Add(data);
		return data;
	}

	static SlotData Literal(string text) => SlotData.FromLiteral(Int, text);

	static BlockData IntArray(string name, string length) => BlockData.ArrayDeclare(Int, name, Literal(length));

	// 테스트 1: 변환 (거부 6개는 콘솔 오류 6개가 정상)
	void TestConvert()
	{
		// int arr[3]; → 프레임 1 + 3 = 4. int x; int arr[5]; → 1 + 1 + 5 = 7
		FunctionData frame = FunctionData.Create("Frame");
		frame.Blocks.Add(BlockData.Declare(Int, "x"));
		frame.Blocks.Add(IntArray("arr", "5"));
		Check("프레임: int x; int arr[5]; → 7칸",
			FunctionDataConverter.TryConvert(frame, owned, out UserFunction converted) && converted.FrameSize == 7, $"실제 {converted?.FrameSize}");
		Check("프레임: array 파라미터는 1칸 (Fill → 2칸)",
			FunctionDataConverter.TryConvert(fill, owned, out UserFunction convertedFill) && convertedFill.FrameSize == 2, $"실제 {convertedFill?.FrameSize}");

		FunctionData lengthVariable = FunctionData.Create("LengthVariable");
		lengthVariable.Blocks.Add(BlockData.Declare(Int, "n", Literal("3")));
		lengthVariable.Blocks.Add(BlockData.ArrayDeclare(Int, "arr", SlotData.FromVariable("n")));
		Check("변수로 정한 길이 거부", !FunctionDataConverter.TryConvert(lengthVariable, owned, out _));

		FunctionData lengthZero = FunctionData.Create("LengthZero");
		lengthZero.Blocks.Add(IntArray("arr", "0"));
		Check("길이 0 거부", !FunctionDataConverter.TryConvert(lengthZero, owned, out _));

		// 길이 15억 두 개: 프레임 크기를 int로 더하면 넘쳐 음수가 된다. 넘친 프레임이 1칸으로 잡히지 않고 거부되어야 한다
		FunctionData tooLarge = FunctionData.Create("TooLarge");
		tooLarge.Blocks.Add(IntArray("a", "1500000000"));
		tooLarge.Blocks.Add(IntArray("b", "1500000000"));
		Check("프레임 크기가 int를 넘는 array 거부", !FunctionDataConverter.TryConvert(tooLarge, owned, out _));

		FunctionData twoDimensional = FunctionData.Create("TwoDimensional");
		twoDimensional.Blocks.Add(BlockData.ArrayDeclare(VariableType.ArrayOf(Int), "grid", Literal("2")));
		Check("2차원 array 거부", !FunctionDataConverter.TryConvert(twoDimensional, owned, out _));

		FunctionData declareArray = FunctionData.Create("DeclareArray");
		declareArray.Blocks.Add(BlockData.Declare(VariableType.ArrayOf(Int), "arr"));
		Check("선언 블록으로 만든 array 거부", !FunctionDataConverter.TryConvert(declareArray, owned, out _));

		FunctionData arrayReturn = FunctionData.Create("ArrayReturn");
		arrayReturn.ReturnType = VariableType.ArrayOf(Int);
		Check("array 반환 타입 거부", !FunctionDataConverter.TryConvert(arrayReturn, owned, out _));

		// async void AsyncFill(int a[]) { } 에 array를 넘기면 거부
		FunctionData asyncFill = FunctionData.Create("AsyncFill");
		asyncFill.IsAsync = true;
		asyncFill.Parameters.Add(new ParameterData("a", VariableType.ArrayOf(Int)));
		FunctionData asyncCaller = FunctionData.Create("AsyncCaller");
		asyncCaller.Blocks.Add(IntArray("arr", "2"));
		asyncCaller.Blocks.Add(BlockData.CallUserFunction(asyncFill.Guid, SlotData.FromVariable("arr")));
		Check("비동기 함수에 array를 넘기면 거부", !FunctionDataConverter.TryConvert(asyncCaller, new[] { asyncFill }, out _));
	}

	// 테스트 2: 실행
	void MakeSteps()
	{
		// 이슈 완료 조건 2. void Basic() { int arr[3]; arr[0] = 5; arr[2] = arr[0] + 1; }
		// 끝에 붙인 블록이 원소를 읽는다: arr[2] 6, arr[1] 기본값 0. 실행 중 스택은 프레임 1 + 3 = 4
		FunctionData basic = Make("Basic");
		basic.Blocks.Add(IntArray("arr", "3"));
		basic.Blocks.Add(BlockData.ArraySet("arr", Literal("0"), Literal("5")));
		basic.Blocks.Add(BlockData.ArraySet("arr", Literal("2"),
			SlotData.FromBlock(BlockData.Arithmetic(SlotData.FromBlock(BlockData.ArrayGet("arr", Literal("0"))), ArithmeticOperator.Add, Literal("1")))));
		steps.Add(new Step
		{
			Name = "Basic", Data = basic, MaxStack = 4,
			Appended = new Block[] { ReadElement("Basic: arr[2] 6", "arr", 2, "6"), ReadElement("Basic: arr[1]은 기본값 0", "arr", 1, "0") },
		});

		// void PassMain() { int arr[2]; Fill(arr); } → Fill이 바꾼 arr[1] 9를 부른 쪽에서 읽는다. 스택: PassMain 3 + Fill 2 = 5
		FunctionData passMain = Make("PassMain");
		passMain.Blocks.Add(IntArray("arr", "2"));
		passMain.Blocks.Add(BlockData.CallUserFunction(fill.Guid, SlotData.FromVariable("arr")));
		steps.Add(new Step
		{
			Name = "Pass", Data = passMain, MaxStack = 5,
			Appended = new Block[] { ReadElement("Pass: Fill이 바꾼 arr[1] 9", "arr", 1, "9"), ReadElement("Pass: Fill이 바꾼 arr[0] 1", "arr", 0, "1") },
		});

		// int arr[3]; arr[3] = 7; → IndexOutOfRange
		FunctionData outOfRange = Make("OutOfRange");
		outOfRange.Blocks.Add(IntArray("arr", "3"));
		outOfRange.Blocks.Add(BlockData.ArraySet("arr", Literal("3"), Literal("7")));
		steps.Add(Fails("OutOfRange", outOfRange, FunctionFailure.IndexOutOfRange));

		// int arr[3]; int x = arr[-1]; → IndexOutOfRange
		FunctionData negative = Make("NegativeIndex");
		negative.Blocks.Add(IntArray("arr", "3"));
		negative.Blocks.Add(BlockData.Declare(Int, "x", SlotData.FromBlock(BlockData.ArrayGet("arr", Literal("-1")))));
		steps.Add(Fails("NegativeIndex", negative, FunctionFailure.IndexOutOfRange));

		// int arr[2]; int other[2]; arr = other; → TypeMismatch (array 통째 대입)
		FunctionData wholeAssign = Make("WholeAssign");
		wholeAssign.Blocks.Add(IntArray("arr", "2"));
		wholeAssign.Blocks.Add(IntArray("other", "2"));
		wholeAssign.Blocks.Add(BlockData.Assign("arr", SlotData.FromVariable("other")));
		steps.Add(Fails("WholeAssign", wholeAssign, FunctionFailure.TypeMismatch));

		// int arr[2]; arr[0.5] = 1; → TypeMismatch (정수가 아닌 index)
		FunctionData floatIndex = Make("FloatIndex");
		floatIndex.Blocks.Add(IntArray("arr", "2"));
		floatIndex.Blocks.Add(BlockData.ArraySet("arr", SlotData.FromLiteral(Float, "0.5"), Literal("1")));
		steps.Add(Fails("FloatIndex", floatIndex, FunctionFailure.TypeMismatch));

		// int x; x[0] = 1; → TypeMismatch (array가 아닌 변수)
		FunctionData notArray = Make("NotArray");
		notArray.Blocks.Add(BlockData.Declare(Int, "x"));
		notArray.Blocks.Add(BlockData.ArraySet("x", Literal("0"), Literal("1")));
		steps.Add(Fails("NotArray", notArray, FunctionFailure.TypeMismatch));

		// y[0] = 1; → UndeclaredVariable
		FunctionData undeclared = Make("Undeclared");
		undeclared.Blocks.Add(BlockData.ArraySet("y", Literal("0"), Literal("1")));
		steps.Add(Fails("Undeclared", undeclared, FunctionFailure.UndeclaredVariable));

		// float farr[2]; Fill(farr); → TypeMismatch (원소 타입이 다른 array)
		FunctionData elementMismatch = Make("ElementMismatch");
		elementMismatch.Blocks.Add(BlockData.ArrayDeclare(Float, "farr", Literal("2")));
		elementMismatch.Blocks.Add(BlockData.CallUserFunction(fill.Guid, SlotData.FromVariable("farr")));
		steps.Add(Fails("ElementMismatch", elementMismatch, FunctionFailure.TypeMismatch));
	}

	static Step Fails(string name, FunctionData data, FunctionFailure failure) =>
		new Step { Name = name, Data = data, Reason = FunctionTaskEndReason.Failed, Failure = failure };

	Block ReadElement(string checkName, string arrayName, int index, string expected) =>
		new TestReadBlock(new ArrayGetBlock(arrayName, new LiteralValue(Int, index.ToString())),
			v => Check(checkName, v.ToString() == expected, $"실제 {v}"));

	void FixedUpdate()
	{
		if (stepIndex >= steps.Count) return;

		if (!casting)
		{
			casting = true;
			maxStack = 0;
			CastStep(steps[stepIndex]);
			return;
		}

		maxStack = Mathf.Max(maxStack, memory.StackUsed);
	}

	void CastStep(Step step)
	{
		if (!FunctionDataConverter.TryConvert(step.Data, owned, out UserFunction converted))
		{
			Check($"{step.Name}: 변환 성공", false);
			NextStep();
			return;
		}

		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, converted.Blocks.Concat(step.Appended).ToArray()));
	}

	void OnTaskEnded(FunctionTask task)
	{
		if (stepIndex >= steps.Count) return;
		Step step = steps[stepIndex];
		if (task.Name != step.Data.Name) return;

		string expected = step.Failure.HasValue ? $"{step.Reason}({step.Failure})" : step.Reason.ToString();
		Check($"{step.Name}: {expected}로 끝남", task.EndReason == step.Reason && task.Failure == step.Failure, $"실제 {task.EndReason}, {task.Failure}");
		if (step.MaxStack > 0) Check($"{step.Name}: 실행 중 스택 {step.MaxStack}", maxStack == step.MaxStack, $"실제 가장 큰 값 {maxStack}");
		// 프레임은 끝나는 즉시 돌려주므로, array의 칸도 함께 돌아가 스택이 비어 있어야 한다
		Check($"{step.Name}: 끝나면 스택 0", memory.StackUsed == 0, $"실제 {memory.StackUsed}");
		NextStep();
	}

	void NextStep()
	{
		stepIndex++;
		casting = false;
		if (stepIndex < steps.Count) return;

		if (failedCount == 0) Debug.Log("[ArrayTest] 전체 통과");
		else Debug.LogError($"[ArrayTest] 실패 {failedCount}개");
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[ArrayTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
