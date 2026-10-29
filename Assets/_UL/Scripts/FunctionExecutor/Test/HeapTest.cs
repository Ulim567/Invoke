using UnityEngine;

/// <summary>
/// 힙(malloc, free, 누수, 영역 비우기) 테스트용 컴포넌트. 실행기, FunctionExecutorLogger와 같은 오브젝트에 붙이고 콘솔 로그로 결과를 확인한다.<br/>
/// 메모리 크기는 기본값 10칸으로 둔다. 틱마다 단계를 하나씩 진행하며, 각 단계의 주석에 기대하는 로그를 적었다.<br/>
/// 다른 테스트 컴포넌트와 함께 붙이지 않는다. 다른 테스트가 메모리를 먼저 쓰면 결과가 달라지기 때문
/// </summary>
[RequireComponent(typeof(FunctionExecutor))]
public class HeapTest : MonoBehaviour
{
	FunctionExecutor executor;
	ProcessMemory memory;
	// 지금 진행할 단계. 그 단계의 틱이 되면 FixedUpdate가 실행하고 다음 단계로 넘어간다
	int step;

	void Start()
	{
		executor = GetComponent<FunctionExecutor>();
		memory = GetComponent<ProcessMemory>();
		memory.Heap.ObjectLeaked += obj => Debug.Log($"HeapTest: 누수 ({obj})");
		memory.Heap.ObjectCleared += obj => Debug.Log($"HeapTest: 영역 비우기로 지움 ({obj})");

		// 틱 1에 malloc → 1, 틱 5에 Completed
		executor.Cast(new TestMallocFunction());
	}

	void FixedUpdate()
	{
		int tick = executor.CurrentTick;
		switch (step)
		{
			case 0 when tick >= 3:
				// 기대: HeapUsed 2, 전역 0, TestSword 1 (진행 중인 작업이 포인터를 쥐고 있어 아직 전역 포인터 목록에 없음)
				LogHeap("malloc한 작업이 진행 중");
				break;

			case 1 when tick >= 6:
				// 기대: HeapUsed 2, 전역 1, TestSword 1 (정상 종료해서 전역 포인터 목록으로 옮겨짐)
				LogHeap("malloc한 작업이 정상 종료");
				// 틱 7에 free 1
				executor.Cast(new TestFreeFunction());
				break;

			case 2 when tick >= 8:
				// 기대: HeapUsed 0, 전역 0, TestSword 0
				LogHeap("free 후");
				// 틱 9에 malloc → 2, free 없이 틱 13에 Completed
				executor.Cast(new TestMallocFunction());
				break;

			case 3 when tick >= 14:
				// 기대: HeapUsed 2, 전역 1, TestSword 1 (free하지 않고 끝나도 힙에 남음)
				LogHeap("free 없이 정상 종료");
				// 틱 15에 malloc → 3. 틱 34에 끝나는 작업이라, 아래 단계에서 도중에 끊는다
				executor.Cast(new TestLongMallocFunction());
				break;

			case 4 when tick >= 17:
				// 기대: TestLongMallocFunction이 Interrupted로 끝나고 「HeapTest: 누수 (TestSword)」
				Debug.Log("HeapTest: 실행 중단 공격");
				executor.InterruptAll();
				break;

			case 5 when tick >= 20:
				// 기대: HeapUsed 4, 전역 1, TestSword 2 (누수 객체도 힙을 차지하고 개수에 들어감)
				LogHeap("실행 중단 후");
				// 기대: 둘 다 False (해제한 포인터 1, 누수된 포인터 3은 포인터 값을 알아도 찾을 수 없음)
				Debug.Log($"HeapTest: 해제한 포인터 1 TryGet {memory.Heap.TryGet(1, out TestSword _)}, 누수된 포인터 3 TryGet {memory.Heap.TryGet(3, out TestSword _)}");
				// 틱 21에 free 2
				executor.Cast(new TestFreeFunction());
				break;

			case 6 when tick >= 22:
				// 기대: 전역 포인터 목록이 비어 NullPointer를 free하므로, 틱 23에 Failed: SkillFailed - 잘못된 포인터 0
				executor.Cast(new TestFreeFunction());
				break;

			case 7 when tick >= 25:
				// 기대: HeapUsed 2, 전역 0, TestSword 1 (누수 객체만 남음)
				LogHeap("전역 포인터를 모두 free한 후");
				// 네 작업은 틱 26에 시전한 순서대로 진행하며, 각각 스택 1칸을 쓴 뒤 힙 2칸을 malloc한다. 남은 칸은 8 → 5 → 2 → 1 → 0
				// 기대: 틱 26에 malloc → 4, 5, 그다음 둘은 NullPointer (스택과 힙의 합이 메모리 크기를 넘을 수 없음)
				for (int i = 0; i < 4; i++)
					executor.Cast(new TestMallocFunction());
				break;

			case 8 when tick >= 32:
				// 기대: HeapUsed 6, 전역 2, TestSword 3
				LogHeap("힙이 모자랄 때까지 malloc한 후");
				// 기대: 「HeapTest: 영역 비우기로 지움 (TestSword)」 3번
				memory.Heap.Clear();
				// 기대: HeapUsed 0, 전역 0, TestSword 0 (누수 객체까지 모두 지워짐)
				LogHeap("영역 비우기 후");
				break;

			default:
				return;
		}
		step++;
	}

	void LogHeap(string label)
	{
		Debug.Log($"HeapTest [{label}] 틱 {executor.CurrentTick}: HeapUsed {memory.HeapUsed}, 전역 {memory.Heap.GetGlobalPointers<TestSword>().Count}, TestSword {memory.Heap.CountObjects<TestSword>()}, Free {memory.Free}");
	}
}
