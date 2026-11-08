using System;
using UnityEngine;

/// <summary>
/// 전역 변수 테스트용 컴포넌트(#139). 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 단계를 차례로 진행한다. 한 단계의 작업이 끝나면 다음 단계를 시작한다.<br/>
/// - 저장 검사: 칸 부족, NULL을 넣은 대상 타입, malloc 초기값을 거부 (콘솔 오류 3개가 정상). 처음 값 없는 대상 타입은 저장 (#474)<br/>
/// - 저장: int count = 3; Sword* mySword; → DataUsed 2, 초기값<br/>
/// - AddCount: count = count + 1; → 다음 함수 ReadCount가 4를 읽음<br/>
/// - Shadow: 지역 int count = 10을 선언하면 함수 안에서는 10, 전역 count는 4 그대로<br/>
/// - LocalMalloc: 지역 변수에만 담은 malloc은 함수가 끝나면 누수<br/>
/// - GlobalMalloc: mySword = malloc → 누수가 아니고 플레이어 전역 heap 2칸<br/>
/// - HoldLocal: mySword를 지역 변수에 옮기고 mySword = NULL. 그동안 다른 작업이 끝나도 누수가 아니고, free(s)가 성공<br/>
/// - SummonThenFail: 함수 안에서 사전 정의 malloc 함수를 부른 뒤 실패해도 그 객체는 숨은 시스템 전역에 남음<br/>
/// - 초기화: count 3, mySword NULL, HeapUsed 0<br/>
/// 끝나면 시작할 때의 전역 변수 파일로 되돌린다. 메모리 크기가 10칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class GlobalVariableTest : MonoBehaviour
{
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	FunctionExecutor executor;
	ProcessMemory memory;
	// 테스트를 시작할 때의 전역 변수 파일. 이 컴퓨터에 파일이 하나뿐이라, 끝나면 되돌려야 다른 테스트의 Free가 바뀌지 않는다
	GlobalVariablesData originalFile;
	// 누수된 객체 수. ObjectLeaked가 불릴 때마다 센다
	int leakedCount;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		originalFile = GlobalVariablesStorage.Load();
		executor.TaskEnded += OnTaskEnded;
		memory.Heap.ObjectLeaked += _ => leakedCount++;

		TestSaveRejected();
		TestSave();
		CastConverted("AddCount", BlockData.Assign("count", SlotData.FromBlock(
			BlockData.Arithmetic(SlotData.FromVariable("count"), ArithmeticOperator.Add, SlotData.FromLiteral(Int, "1")))));
	}

	// 저장 검사: 거부한 파일은 저장하지 않는다 (콘솔 오류 3개가 정상)
	void TestSaveRejected()
	{
		GlobalVariablesData tooMany = new GlobalVariablesData();
		for (int i = 0; i <= memory.Capacity; i++)
			tooMany.Declarations.Add(BlockData.Declare(Int, $"v{i}"));
		Check("칸보다 많은 전역 변수 거부", !executor.TrySaveGlobalVariables(tooMany));

		// 대상 타입 전역 변수는 받는다 (#474). 처음 값은 비워 두는 것만 된다. Target은 포인터가 아니라 NULL을 넣을 수 없기 때문
		Check("대상 타입 전역 변수 (처음 값 없음) 저장", executor.TrySaveGlobalVariables(FileOf(BlockData.Declare(VariableType.TargetOf(TargetKind.Bug), "t"))));
		Check("대상 타입 전역 변수에 NULL 처음 값 거부", !executor.TrySaveGlobalVariables(FileOf(
			BlockData.Declare(VariableType.TargetOf(TargetKind.Bug), "t", SlotData.FromNull()))));

		Check("malloc 초기값 거부", !executor.TrySaveGlobalVariables(FileOf(
			BlockData.Declare(SwordPointer, "p", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))))));
	}

	// 저장: int count = 3; Sword* mySword;
	void TestSave()
	{
		// 빈 파일로 먼저 초기화해, 전역 변수가 없을 때의 Free를 기준으로 삼는다
		executor.TrySaveGlobalVariables(new GlobalVariablesData());
		int freeWithoutGlobals = memory.Free;

		bool saved = executor.TrySaveGlobalVariables(FileOf(
			BlockData.Declare(Int, "count", SlotData.FromLiteral(Int, "3")),
			BlockData.Declare(SwordPointer, "mySword")));
		Check("저장 성공", saved);
		Check("DataUsed 2", memory.DataUsed == 2, $"실제 {memory.DataUsed}");
		Check("전역 변수 2개를 저장하면 Free가 2칸 줄어듦", freeWithoutGlobals - memory.Free == 2, $"저장 전 {freeWithoutGlobals}, 저장 후 {memory.Free}");
		CheckGlobal("count 초기값 3", "count", "3");
		CheckGlobal("mySword 초기값 NULL", "mySword", "포인터 0");
	}

	void OnTaskEnded(FunctionTask task)
	{
		switch (task.Name)
		{
			case "AddCount":
				CheckCompleted(task);
				executor.Cast(new UserFunction("ReadCount", 1,
					new TestReadBlock(new VariableValue("count"), v => Check("ReadCount: 다른 함수가 전역 count 4를 읽음", v.ToString() == "4", $"실제 {v}"))));
				break;

			case "ReadCount":
				CheckCompleted(task);
				CastConverted("Shadow",
					BlockData.Declare(Int, "count", SlotData.FromLiteral(Int, "10")),
					BlockData.Assign("count", SlotData.FromLiteral(Int, "20")));
				break;

			case "Shadow":
				CheckCompleted(task);
				CheckGlobal("Shadow: 지역 count에 대입해도 전역 count는 4", "count", "4");
				CastConverted("LocalMalloc", BlockData.Declare(SwordPointer, "s", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
				break;

			case "LocalMalloc":
				CheckCompleted(task);
				Check("LocalMalloc: 지역 변수에만 담은 객체는 누수", leakedCount == 1, $"누수 {leakedCount}개, {memory.Heap.GetUsageByOwner()}");
				CastConverted("GlobalMalloc", BlockData.Assign("mySword", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
				break;

			case "GlobalMalloc":
				CheckCompleted(task);
				HeapUsage usage = memory.Heap.GetUsageByOwner();
				Check("GlobalMalloc: 전역 변수에 담은 객체는 누수가 아님", leakedCount == 1, $"누수 {leakedCount}개");
				Check("GlobalMalloc: 플레이어 전역 heap 2칸, 누수 2칸", usage.PlayerGlobals == 2 && usage.Leaked == 2, usage.ToString());
				CastHoldLocal();
				break;

			case "Blip":
				break;

			case "HoldLocal":
				CheckCompleted(task);
				Check("HoldLocal: 지역 변수로 쥔 객체를 free함 (누수로 보지 않음)", leakedCount == 1 && memory.HeapUsed == 2, $"누수 {leakedCount}개, HeapUsed {memory.HeapUsed}");
				// 사전 정의 malloc 함수(5틱)가 끝난 뒤 없는 변수를 읽어 UndeclaredVariable로 실패한다
				executor.Cast(new UserFunction("SummonThenFail", 1,
					new TestMallocFunction(),
					new TestReadBlock(new VariableValue("undeclared"), _ => { })));
				break;

			case "SummonThenFail":
				Check("SummonThenFail: 실패로 끝남", task.EndReason == FunctionTaskEndReason.Failed, $"{task.EndReason}, {task.Failure}");
				usage = memory.Heap.GetUsageByOwner();
				Check("SummonThenFail: 사전 정의 함수가 올린 객체는 숨은 시스템 전역에 남음",
					memory.Heap.GetGlobalPointers<TestSword>().Count == 1 && usage.SystemGlobals == 2 && leakedCount == 1,
					$"TestSword {memory.Heap.GetGlobalPointers<TestSword>().Count}개, 누수 {leakedCount}개, {usage}");
				TestRestart();
				break;
		}
	}

	// HoldLocal: 다른 작업이 malloc한 객체(mySword)를 지역 변수에만 쥔 채, 다른 작업이 끝나도 누수로 보지 않는지 (#141 검토 1번)
	// Sword* s = mySword; mySword = NULL; (Blip 시전) (확인) (확인) free(s);
	void CastHoldLocal()
	{
		FunctionData data = FunctionData.Create("HoldLocal");
		data.Blocks.Add(BlockData.Declare(SwordPointer, "s", SlotData.FromVariable("mySword")));
		data.Blocks.Add(BlockData.Assign("mySword", SlotData.FromNull()));
		data.Blocks.Add(BlockData.Free(SlotData.FromVariable("s")));
		if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), out UserFunction converted))
		{
			Check("HoldLocal: 변환 성공", false);
			return;
		}

		var blocks = new System.Collections.Generic.List<Block>(converted.Blocks);
		// Blip은 1틱짜리 작업이다. 끝나면서 누수 판단을 부른다
		blocks.Insert(2, new TestProbeBlock(_ => executor.Cast(new UserFunction("Blip", 1, new TestProbeBlock(__ => { })))));
		blocks.Insert(3, new TestProbeBlock(_ => { }));
		blocks.Insert(4, new TestProbeBlock(_ =>
		{
			HeapUsage usage = memory.Heap.GetUsageByOwner();
			Check("HoldLocal: Blip이 끝난 뒤에도 지역 변수의 객체는 지역 2칸", leakedCount == 1 && usage.TaskLocals == 2, $"누수 {leakedCount}개, {usage}");
		}));
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, blocks.ToArray()));
	}

	// 초기화: 전역 변수는 초기값으로, heap(시스템 전역과 누수 포함)은 0으로
	void TestRestart()
	{
		executor.RestartProcess();
		CheckGlobal("초기화: count 3", "count", "3");
		CheckGlobal("초기화: mySword NULL", "mySword", "포인터 0");
		Check("초기화: HeapUsed 0", memory.HeapUsed == 0, $"실제 {memory.HeapUsed}");
		Check("초기화: DataUsed 2", memory.DataUsed == 2, $"실제 {memory.DataUsed}");

		GlobalVariablesStorage.Save(originalFile);
		executor.RestartProcess();
		Debug.Log($"[GlobalVariableTest] 끝. 시작할 때의 전역 변수 파일로 되돌림 (DataUsed {memory.DataUsed})");
	}

	// 블록들을 함수 데이터로 만들어 바꾼 뒤 시전한다. 실제 편집 화면에서 만든 함수와 같은 길로 실행하기 위함
	void CastConverted(string name, params BlockData[] blocks)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.AddRange(blocks);
		if (!FunctionDataConverter.TryConvert(data, Array.Empty<FunctionData>(), out UserFunction converted))
		{
			Check($"{name}: 변환 성공", false);
			return;
		}
		executor.Cast(converted);
	}

	static GlobalVariablesData FileOf(params BlockData[] declarations)
	{
		GlobalVariablesData file = new GlobalVariablesData();
		file.Declarations.AddRange(declarations);
		return file;
	}

	void CheckGlobal(string name, string variable, string expected)
	{
		bool found = memory.Globals.TryGet(variable, out RuntimeValue value);
		Check(name, found && value.ToString() == expected, found ? $"실제 {value}" : "전역 변수 없음");
	}

	static void CheckCompleted(FunctionTask task)
	{
		Check($"{task.Name}: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"{task.EndReason}, {task.Failure}");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[GlobalVariableTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
