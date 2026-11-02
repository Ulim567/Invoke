using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// struct 객체 등록 자리(StructObjectRegistry), HeapStorage.ObjectFreed·ObjectLeaked, 대상 array(FromBugs, AsBugs, 시작할 때의 InvalidTarget) 테스트용 컴포넌트.<br/>
/// 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다. 「[StructObjectTest] 전체 통과」면 통과.<br/>
/// - 실제 소환검 대신 가짜 객체(FakeSword)를 만드는 함수를 이 캐릭터의 Sword에 등록한다. 같은 오브젝트에 SummonedSwords를 붙이지 않는다.<br/>
/// - 씬에 살아 있는 몬스터가 있으면 그 몬스터로 대상 array를 확인한다. 없으면 그 단계만 건너뛴다(경고).<br/>
/// - 단계를 차례로 진행한다. 앞 단계의 작업이 끝나야 다음 단계를 시전한다. 메모리 크기가 4칸 이상이어야 한다. 다른 테스트 컴포넌트와 함께 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class StructObjectTest : MonoBehaviour
{
	static readonly VariableType SwordPointer = VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword));

	// 등록한 함수가 만드는 가짜 소환검. 몇 번째로 만들었는지만 가진다
	class FakeSword
	{
		public int Number;
		public override string ToString() => $"FakeSword {Number}";
	}

	enum Step { Registered, Refused, HeapFull, Leak, ArrayUsable, ArrayWithNoTarget, ArrayEmpty, Done }

	FunctionExecutor executor;
	ProcessMemory memory;
	Step step;
	int startTick;
	int baseHeapUsed;
	// 테스트를 시작할 때의 HeapUsed. 테스트가 heap을 모두 돌려주었는지 마지막에 비교한다
	int heapAtStart;
	int failedCount;

	// 등록한 함수의 동작과 기록
	bool canCreate = true;
	int createdCount;
	readonly List<(FakeSword Sword, int Pointer)> allocated = new List<(FakeSword, int)>();
	readonly List<object> freed = new List<object>();
	readonly List<object> leaked = new List<object>();

	// HeapFull 단계에서 heap을 일부러 채운 객체의 포인터
	int fillerPointer = HeapStorage.NullPointer;
	// 대상 array 단계에 쓰는 살아 있는 몬스터. 없으면 그 단계를 건너뛴다
	Monster liveBug;
	// TestTargetArrayFunction이 읽은 몬스터들
	List<Monster> readTargets;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		heapAtStart = memory.HeapUsed;
		executor.TaskEnded += OnTaskEnded;
		memory.Heap.ObjectFreed += OnObjectFreed;
		memory.Heap.ObjectLeaked += OnObjectLeaked;
		TestTargetArrayFunction.TargetsRead += OnTargetsRead;

		memory.StructObjects.Register<FakeSword>(PredefinedStructs.Sword, CreateSword, (sword, pointer) => allocated.Add((sword, pointer)));
		TestRegisterErrors();

		foreach (Monster bug in FindObjectsByType<Monster>(FindObjectsSortMode.None))
		{
			if (bug.IsDead) continue;
			liveBug = bug;
			break;
		}
		TestFromBugs();

		CastRegistered();
	}

	void OnDestroy()
	{
		TestTargetArrayFunction.TargetsRead -= OnTargetsRead;
		if (memory != null && memory.Heap != null)
		{
			memory.Heap.ObjectFreed -= OnObjectFreed;
			memory.Heap.ObjectLeaked -= OnObjectLeaked;
			memory.StructObjects.Unregister(PredefinedStructs.Sword);
		}
	}

	FakeSword CreateSword()
	{
		if (!canCreate) return null;
		createdCount++;
		return new FakeSword { Number = createdCount };
	}

	// 잘못된 등록은 예외로 드러난다
	void TestRegisterErrors()
	{
		Check("목록에 없는 struct 등록은 예외", Throws(() => memory.StructObjects.Register<FakeSword>("Shield", CreateSword, (s, p) => { })));
		Check("같은 struct 두 번 등록은 예외", Throws(() => memory.StructObjects.Register<FakeSword>(PredefinedStructs.Sword, CreateSword, (s, p) => { })));
	}

	// FromBugs로 만든 값의 타입과, AsBugs로 꺼낸 몬스터의 순서
	void TestFromBugs()
	{
		RuntimeValue value = RuntimeValue.FromBugs(new[] { liveBug, null });
		Check("FromBugs: 타입이 Bug 대상 array",
			VariableStore.IsSameType(value.Type, VariableType.ArrayOf(VariableType.TargetOf(TargetKind.Bug))), $"실제 {value.Type?.Kind}");

		List<Monster> bugs = value.AsBugs();
		Check("AsBugs: 넣은 순서대로, null은 null", bugs.Count == 2 && bugs[0] == liveBug && bugs[1] == null, $"실제 {bugs.Count}개");
	}

	// Sword* s = malloc(Sword); free(s);
	UserFunction MakeMallocFree(string name, System.Action<RuntimeValue> onReadPointer)
	{
		FunctionData data = FunctionData.Create(name);
		data.Blocks.Add(BlockData.Declare(SwordPointer, "s", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
		data.Blocks.Add(BlockData.Free(SlotData.FromVariable("s")));
		if (!FunctionDataConverter.TryConvert(data, System.Array.Empty<FunctionData>(), out UserFunction converted))
		{
			Check($"{name}: 변환 성공", false);
			return null;
		}

		// 선언 다음(free 앞)에 s를 읽는다
		List<Block> blocks = new List<Block>(converted.Blocks);
		blocks.Insert(1, new TestReadBlock(new VariableValue("s"), onReadPointer));
		return new UserFunction(converted.Name, converted.FrameSize, blocks.ToArray());
	}

	// 1단계. 등록한 함수로 만든 객체가 heap에 올라가고, 포인터를 알리고, free에서 ObjectFreed로 알린다
	void CastRegistered()
	{
		step = Step.Registered;
		Cast(MakeMallocFree("Registered", s =>
		{
			Check("Registered: 만들기 함수가 한 번 불림", createdCount == 1, $"실제 {createdCount}번");
			Check("Registered: 포인터 알리기 함수가 s와 같은 포인터를 받음",
				allocated.Count == 1 && allocated[0].Pointer == s.AsPointer() && s.AsPointer() != HeapStorage.NullPointer, $"s {s}, 알림 {allocated.Count}번");
			Check("Registered: HeapUsed가 Sword 크기만큼 늘어남", memory.HeapUsed - baseHeapUsed == SwordSize, $"실제 {memory.HeapUsed - baseHeapUsed}칸");
			Check("Registered: heap의 객체가 만든 객체", allocated.Count == 1 && memory.Heap.TryGet(s.AsPointer(), out FakeSword sword) && sword == allocated[0].Sword);
		}));
	}

	// 2단계. 만들기 함수가 null을 돌려주면 heap에 올리지 않고 NULL
	void CastRefused()
	{
		step = Step.Refused;
		canCreate = false;
		Cast(MakeMallocFree("Refused", s =>
		{
			Check("Refused: s가 NULL", s.AsPointer() == HeapStorage.NullPointer, $"실제 {s}");
			Check("Refused: HeapUsed 그대로", memory.HeapUsed == baseHeapUsed, $"실제 {memory.HeapUsed - baseHeapUsed}칸 늘어남");
			Check("Refused: 포인터 알리기 함수가 불리지 않음", allocated.Count == 1);
		}));
	}

	// 3단계. heap이 모자라면 만들기 함수를 부르지 않고 NULL
	void CastHeapFull()
	{
		step = Step.HeapFull;
		canCreate = true;
		UserFunction function = MakeMallocFree("HeapFull", s =>
		{
			Check("HeapFull: s가 NULL", s.AsPointer() == HeapStorage.NullPointer, $"실제 {s}");
			Check("HeapFull: 만들기 함수가 불리지 않음", createdCount == 1, $"실제 {createdCount}번");
			Check("HeapFull: 포인터 알리기 함수가 불리지 않음", allocated.Count == 1);
		});
		if (function == null) return;

		// 작업 밖에서 올리므로 owner는 null(숨은 시스템 전역). 이 단계가 끝나면 해제한다
		fillerPointer = memory.Heap.Malloc(null, "테스트용 채우기", memory.Free - function.FrameSize);
		Cast(function);
	}

	// 4단계. Sword* s = malloc(Sword);만 하고 끝나면, 지역 변수에만 담은 객체가 누수되어 ObjectLeaked가 그 객체를 넘긴다
	void CastLeak()
	{
		step = Step.Leak;
		FunctionData data = FunctionData.Create("Leak");
		data.Blocks.Add(BlockData.Declare(SwordPointer, "s", SlotData.FromBlock(BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)))));
		if (!FunctionDataConverter.TryConvert(data, System.Array.Empty<FunctionData>(), out UserFunction converted))
		{
			Check("Leak: 변환 성공", false);
			return;
		}
		Cast(converted);
	}

	// 5~7단계. 대상 array: 모두 쓸 수 있음 → 정상 종료, 「대상 없음」이 섞임 → InvalidTarget, 빈 array → 정상 종료
	void CastArray(Step arrayStep)
	{
		step = arrayStep;
		readTargets = null;
		Monster[] bugs = arrayStep switch
		{
			Step.ArrayUsable => new[] { liveBug },
			Step.ArrayWithNoTarget => new[] { liveBug, null },
			_ => new Monster[0],
		};
		if (arrayStep == Step.ArrayUsable && liveBug == null)
		{
			Debug.LogWarning("[StructObjectTest] 씬에 살아 있는 몬스터가 없어 ArrayUsable 단계를 건너뜁니다.");
			CastArray(Step.ArrayWithNoTarget);
			return;
		}
		BeginStage();
		executor.Cast(new TestTargetArrayFunction(), RuntimeValue.FromBugs(bugs));
	}

	void Cast(UserFunction function)
	{
		if (function == null) return;
		BeginStage();
		executor.Cast(function);
	}

	void BeginStage()
	{
		startTick = executor.CurrentTick;
		baseHeapUsed = memory.HeapUsed;
	}

	void OnObjectFreed(object obj) => freed.Add(obj);

	void OnObjectLeaked(object obj) => leaked.Add(obj);

	void OnTargetsRead(List<Monster> targets) => readTargets = targets;

	void OnTaskEnded(FunctionTask task)
	{
		switch (step)
		{
			case Step.Registered when task.Name == "Registered":
				Check("Registered: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				Check("Registered: free 뒤 ObjectFreed가 만든 객체를 넘김", freed.Count == 1 && allocated.Count == 1 && freed[0] == allocated[0].Sword, $"실제 {freed.Count}번");
				Check("Registered: free 뒤 HeapUsed가 원래대로", memory.HeapUsed == baseHeapUsed, $"실제 {memory.HeapUsed - baseHeapUsed}칸 남음");
				CastRefused();
				break;

			case Step.Refused when task.Name == "Refused":
				Check("Refused: 정상 종료 (free(NULL))", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				CastHeapFull();
				break;

			case Step.HeapFull when task.Name == "HeapFull":
				Check("HeapFull: 정상 종료 (free(NULL))", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				memory.Heap.Free(fillerPointer);
				CastLeak();
				break;

			// 누수 표시는 TaskEnded보다 먼저 하므로(FunctionExecutor) 여기서 확인할 수 있다
			case Step.Leak when task.Name == "Leak":
				Check("Leak: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				Check("Leak: ObjectLeaked가 만든 객체를 넘김", allocated.Count == 2 && leaked.Count == 1 && leaked[0] == allocated[1].Sword, $"만듦 {allocated.Count}번, 누수 {leaked.Count}번");
				Check("Leak: 누수된 객체도 heap을 차지함", memory.HeapUsed - baseHeapUsed == SwordSize, $"실제 {memory.HeapUsed - baseHeapUsed}칸");
				// 누수 객체는 영역 비우기로만 돌아오므로, 마지막 HeapUsed 비교를 위해 여기서 비운다
				memory.Heap.Clear();
				CastArray(Step.ArrayUsable);
				break;

			case Step.ArrayUsable:
				Check("ArrayUsable: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				Check("ArrayUsable: 스킬이 넘긴 몬스터를 읽음", readTargets != null && readTargets.Count == 1 && readTargets[0] == liveBug, $"실제 {readTargets?.Count}개");
				CastArray(Step.ArrayWithNoTarget);
				break;

			case Step.ArrayWithNoTarget:
				Check("ArrayWithNoTarget: InvalidTarget으로 실패", task.IsFailed && task.Failure == FunctionFailure.InvalidTarget, $"실제 {task.EndReason}, {task.Failure}");
				Check("ArrayWithNoTarget: 스킬이 시작하지 않음", readTargets == null);
				CastArray(Step.ArrayEmpty);
				break;

			case Step.ArrayEmpty:
				Check("ArrayEmpty: 정상 종료", task.EndReason == FunctionTaskEndReason.Completed, $"실제 {task.EndReason}, {task.Failure}");
				Check("ArrayEmpty: 스킬이 빈 목록을 읽음", readTargets != null && readTargets.Count == 0, $"실제 {readTargets?.Count}개");
				Finish();
				break;
		}
	}

	void Finish()
	{
		step = Step.Done;
		Check("테스트가 끝난 뒤 HeapUsed가 시작할 때와 같음", memory.HeapUsed == heapAtStart, $"시작 {heapAtStart}, 지금 {memory.HeapUsed}");
		if (failedCount == 0) Debug.Log("[StructObjectTest] 전체 통과");
		else Debug.LogError($"[StructObjectTest] 실패 {failedCount}개");
	}

	static int SwordSize => PredefinedStructs.TryGet(PredefinedStructs.Sword, out StructDefinition definition) ? definition.HeapSize : -1;

	static bool Throws(System.Action action)
	{
		try { action(); }
		catch (System.Exception) { return true; }
		return false;
	}

	void Check(string name, bool passed, string detail = null)
	{
		string message = $"[StructObjectTest] {name}: {(passed ? "통과" : "실패")}";
		if (detail != null) message += $" ({detail})";

		if (passed) Debug.Log(message);
		else
		{
			failedCount++;
			Debug.LogError(message);
		}
	}
}
