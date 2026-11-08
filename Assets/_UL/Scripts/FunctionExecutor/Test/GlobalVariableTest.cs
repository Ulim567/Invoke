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
/// - 전역 array (#478) 저장 검사: 선언 블록의 array 타입, 길이 0, 칸 합계 초과를 거부 (콘솔 오류 3개가 정상). Target array는 저장하고 원소는 「대상 없음」<br/>
/// - 전역 array 저장: int last; int scores[3]; Sword* swords[2]; → DataUsed 6, 원소는 기본값<br/>
/// - SetArray: scores[1] = 7; swords[0] = malloc(Sword); → swords의 객체는 누수가 아니고 플레이어 전역 heap 2칸<br/>
/// - ReadArray: last = scores[1]; → 다른 함수가 7을 읽음<br/>
/// - 전역 array 초기화: 원소가 기본값으로, HeapUsed 0, DataUsed 6<br/>
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
	// 전역 array 단계를 시작할 때의 누수 수. 그 단계에서 누수가 늘지 않았는지 볼 때 쓴다 (#478)
	int leakedBeforeArray;

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

			case "SetArray":
				CheckCompleted(task);
				usage = memory.Heap.GetUsageByOwner();
				// 전역 array의 원소에 담은 포인터도 루트로 센다(HeapStorage.PointersIn)
				Check("SetArray: 전역 array 원소에 담은 객체는 누수가 아님", leakedCount == leakedBeforeArray, $"누수 {leakedCount - leakedBeforeArray}개 늘어남");
				Check("SetArray: 플레이어 전역 heap 2칸", usage.PlayerGlobals == 2 && usage.Leaked == 0, usage.ToString());
				CastConverted("ReadArray", BlockData.Assign("last", SlotData.FromBlock(BlockData.ArrayGet("scores", SlotData.FromLiteral(Int, "1")))));
				break;

			case "ReadArray":
				CheckCompleted(task);
				CheckGlobal("ReadArray: 다른 함수가 전역 scores[1] 7을 읽음", "last", "7");
				TestGlobalArrayRestart();
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

		TestGlobalArray();
	}

	// 전역 array (#478): 저장 검사, 저장, 원소 기본값. 이어서 SetArray를 시전한다
	void TestGlobalArray()
	{
		// 저장 검사: 거부한 파일은 저장하지 않는다 (콘솔 오류 3개가 정상)
		Check("전역 array: 선언 블록의 array 타입 거부", !executor.TrySaveGlobalVariables(FileOf(BlockData.Declare(VariableType.ArrayOf(Int), "a"))));
		Check("전역 array: 길이 0 거부", !executor.TrySaveGlobalVariables(FileOf(BlockData.ArrayDeclare(Int, "a", SlotData.FromLiteral(Int, "0")))));
		// 변수 하나(1칸) + 메모리 크기만큼의 array = 메모리보다 1칸 많음. 선언 수(2개)가 아니라 칸 수 합계로 세는지 본다
		Check("전역 array: 칸 수 합계가 메모리보다 많으면 거부", !executor.TrySaveGlobalVariables(FileOf(
			BlockData.Declare(Int, "x"),
			BlockData.ArrayDeclare(Int, "big", SlotData.FromLiteral(Int, memory.Capacity.ToString())))));

		Check("전역 array: Target array 저장", executor.TrySaveGlobalVariables(FileOf(
			BlockData.ArrayDeclare(VariableType.TargetOf(TargetKind.Bug), "targets", SlotData.FromLiteral(Int, "2")))));
		CheckElement("전역 array: Target 원소는 대상 없음", "targets", 1, "(대상 없음)");

		// 빈 파일로 먼저 초기화해, 전역 변수가 없을 때의 Free를 기준으로 삼는다
		executor.TrySaveGlobalVariables(new GlobalVariablesData());
		int freeWithoutGlobals = memory.Free;
		bool saved = executor.TrySaveGlobalVariables(FileOf(
			BlockData.Declare(Int, "last"),
			BlockData.ArrayDeclare(Int, "scores", SlotData.FromLiteral(Int, "3")),
			BlockData.ArrayDeclare(SwordPointer, "swords", SlotData.FromLiteral(Int, "2"))));
		Check("전역 array: 저장 성공", saved);
		Check("전역 array: DataUsed 6 (1 + 3 + 2)", memory.DataUsed == 6, $"실제 {memory.DataUsed}");
		Check("전역 array: Free가 6칸 줄어듦", freeWithoutGlobals - memory.Free == 6, $"저장 전 {freeWithoutGlobals}, 저장 후 {memory.Free}");
		CheckElement("전역 array: scores[2] 기본값 0", "scores", 2, "0");
		CheckElement("전역 array: swords[1] 기본값 NULL", "swords", 1, "포인터 0");

		leakedBeforeArray = leakedCount;
		CastConverted("SetArray",
			BlockData.ArraySet("scores", SlotData.FromLiteral(Int, "1"), SlotData.FromLiteral(Int, "7")),
			BlockData.ArraySet("swords", SlotData.FromLiteral(Int, "0"), SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
	}

	// 전역 array 초기화: 원소는 기본값으로, heap은 0으로. 끝나면 시작할 때의 전역 변수 파일로 되돌린다
	void TestGlobalArrayRestart()
	{
		executor.RestartProcess();
		CheckElement("전역 array 초기화: scores[1] 0", "scores", 1, "0");
		CheckElement("전역 array 초기화: swords[0] NULL", "swords", 0, "포인터 0");
		Check("전역 array 초기화: HeapUsed 0", memory.HeapUsed == 0, $"실제 {memory.HeapUsed}");
		Check("전역 array 초기화: DataUsed 6", memory.DataUsed == 6, $"실제 {memory.DataUsed}");

		GlobalVariablesStorage.Save(originalFile);
		// TrySaveGlobalVariables가 정해 둔 테스트 파일 대신 되돌린 파일을 읽게 한다 (#478)
		executor.SetGlobalVariablesFile(null);
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

	// 전역 array variable의 index번 원소
	void CheckElement(string name, string variable, int index, string expected)
	{
		bool found = memory.Globals.TryGet(variable, out RuntimeValue value) && value.Type?.Kind == VariableTypeKind.Array;
		string actual = found ? value.AsArray().Get(index).ToString() : null;
		Check(name, found && actual == expected, found ? $"실제 {actual}" : "전역 array 없음");
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
