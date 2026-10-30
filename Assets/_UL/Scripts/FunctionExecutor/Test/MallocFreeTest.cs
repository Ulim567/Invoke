using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// malloc 블록, free 블록 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 두 단계를 차례로 진행한다. 1단계가 끝나면 2단계를 시작한다.<br/>
/// - 1단계(이슈 완료 조건 4): malloc에서 HeapUsed가 오르고 free에서 내려가는지<br/>
/// - 2단계(이슈 완료 조건 5): heap이 모자라면 변수에 NullPointer가 담기고, 그 변수로 free하면 InvalidPointerError로 실패하는지<br/>
/// 메모리 크기가 4칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class MallocFreeTest : MonoBehaviour
{
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	FunctionExecutor executor;
	ProcessMemory memory;
	// 지금 단계를 시전한 순간의 틱과 HeapUsed. 끝나는 틱과 heap 칸은 이 값부터 센다
	int startTick;
	int baseHeapUsed;
	// 2단계에서 heap을 일부러 채운 객체의 포인터. 2단계가 끝나면 해제한다
	int fillerPointer = HeapStorage.NullPointer;
	// 테스트를 시작할 때의 HeapUsed. 테스트가 heap을 모두 돌려주었는지 마지막에 비교한다
	int heapAtStart;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		heapAtStart = memory.HeapUsed;
		executor.TaskEnded += OnTaskEnded;

		TestConvert();
		CastMallocFree();
	}

	// 변환: 목록에 없는 struct를 malloc하면 거부한다 (콘솔 오류 1개가 정상)
	void TestConvert()
	{
		FunctionData unknown = FunctionData.Create("UnknownStruct");
		VariableType shieldPointer = VariableType.PointerTo(VariableType.StructOf("Shield"));
		unknown.Blocks.Add(BlockData.Declare(shieldPointer, "p", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf("Shield")))));
		Check("목록에 없는 struct 거부 (콘솔 오류 1개가 정상)", !FunctionDataConverter.TryConvert(unknown, out _));
	}

	// Sword* s = malloc(Sword); free(s);
	FunctionData MakeMallocFree(string name)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.Add(BlockData.Declare(SwordPointer, "s", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
		data.Blocks.Add(BlockData.Free(SlotData.FromVariable("s")));
		return data;
	}

	// 1단계. 틱 1 malloc, 틱 2 선언, 틱 3 확인, 틱 4 free, 틱 5 확인
	void CastMallocFree()
	{
		if (!FunctionDataConverter.TryConvert(MakeMallocFree("MallocFree"), out UserFunction converted))
		{
			Check("MallocFree: 변환 성공", false);
			return;
		}

		List<Block> blocks = new List<Block>(converted.Blocks);
		blocks.Insert(1, new TestProbeBlock(task => CheckHeap("malloc 뒤", 2)));
		blocks.Add(new TestProbeBlock(task => CheckHeap("free 뒤", 0)));
		BeginStage();
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, blocks.ToArray()));
	}

	// 2단계. heap을 이 함수의 프레임(2칸)만 남기고 채운 뒤 시전한다
	// 틱 1 malloc(실패), 틱 2 선언, 틱 3 s 읽기, 틱 4 free → InvalidPointerError
	void CastHeapFull()
	{
		if (!FunctionDataConverter.TryConvert(MakeMallocFree("HeapFull"), out UserFunction converted))
		{
			Check("HeapFull: 변환 성공", false);
			return;
		}

		// 작업 밖에서 올리므로 owner는 null(전역 포인터 목록). 테스트가 끝나면 해제한다
		fillerPointer = memory.Heap.Malloc(null, "테스트용 채우기", memory.Free - converted.FrameSize);

		List<Block> blocks = new List<Block>(converted.Blocks);
		blocks.Insert(1, new TestReadBlock(new VariableValue("s"), v => Check("HeapFull: s에 NullPointer", v.ToString() == "포인터 0", $"실제 {v}")));
		BeginStage();
		executor.Cast(new UserFunction(converted.Name, converted.FrameSize, blocks.ToArray()));
	}

	void BeginStage()
	{
		startTick = executor.CurrentTick;
		baseHeapUsed = memory.HeapUsed;
	}

	void OnTaskEnded(FunctionTask task)
	{
		int tick = executor.CurrentTick - startTick;
		switch (task.Name)
		{
			case "MallocFree":
				Check("MallocFree: 틱 5에 정상 종료", task.EndReason == FunctionTaskEndReason.Completed && tick == 5, $"실제 틱 {tick}, {task.EndReason}");
				CastHeapFull();
				break;

			case "HeapFull":
				InvalidPointerError error = task.Error as InvalidPointerError;
				Check("HeapFull: 틱 4에 InvalidPointerError(포인터 0)로 실패",
					task.Failure == FunctionFailure.SkillFailed && error != null && error.Pointer == HeapStorage.NullPointer && tick == 4,
					$"실제 틱 {tick}, {task.Failure}, {task.Error?.Message}");
				memory.Heap.Free(fillerPointer);
				Check("테스트가 끝난 뒤 HeapUsed가 시작할 때와 같음", memory.HeapUsed == heapAtStart, $"시작 {heapAtStart}, 지금 {memory.HeapUsed}");
				break;
		}
	}

	void CheckHeap(string when, int expectedIncrease)
	{
		int increase = memory.HeapUsed - baseHeapUsed;
		Check($"MallocFree: {when} HeapUsed가 {expectedIncrease}칸 늘어남", increase == expectedIncrease, $"실제 {increase}칸");
	}

	static void Check(string name, bool passed, string detail = null)
	{
		string message = $"[MallocFreeTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else Debug.LogError(message);
	}
}
