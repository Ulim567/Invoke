using UnityEngine;

/// <summary>
/// 캐릭터(프로세스) 하나의 메모리. 스택과 힙의 칸 수를 센다.<br/>
/// 메모리 값은 이 컴포넌트만 바꾼다. 다른 스크립트는 스택은 TryAllocateStack, ReleaseStack을 부르고, 힙은 Heap(HeapStorage)의 함수를 부른다.
/// <para>
/// 메모리 전체 크기는 같은 오브젝트에 붙은 PlayerStats의 MemorySize다.<br/>
/// 메모리 크기가 줄어 스택 사용량보다 작아져도 실행 중인 함수는 끊지 않는다. 남은 칸이 0이 되어, 새로 시전한 함수만 오버플로로 끝난다.
/// </para>
/// <para>
/// 스택과 힙은 메모리 한 영역을 함께 쓴다. 메모리 게이지는 스택을 위에서부터, 힙을 아래에서부터 그린다.<br/>
/// 끝나는 순서 때문에 생기는 빈칸은 따지지 않으므로, 스택과 힙은 각각 칸 수만 센다. 그래서 위아래 방향은 계산에 쓰지 않는다.
/// </para>
/// <para>
/// 힙은 여기서 칸 수만 센다. 어떤 객체가 그 칸을 쓰는지는 Heap(힙 객체 저장소)이 안다.<br/>
/// 코드 영역은 이후 이슈에서 추가한다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class ProcessMemory : MonoBehaviour
{
	PlayerStats stats;

	/// <summary>
	/// 메모리 전체 칸 수. PlayerStats.MemorySize를 그대로 돌려준다.
	/// </summary>
	// 값을 따로 저장하지 않고 읽을 때마다 PlayerStats에 묻는다. 레벨업, 버프로 능력치가 바뀌어도 Changed 이벤트를 받지 않고 바로 반영하기 위함
	public int Capacity => stats.MemorySize;

	/// <summary>
	/// 지금 스택이 쓰고 있는 칸 수. 실행 중인 모든 함수의 프레임 크기 합이다.
	/// </summary>
	public int StackUsed { get; private set; }

	/// <summary>
	/// 지금 힙이 쓰고 있는 칸 수. 해제하지 않은 객체와 누수된 객체의 크기 합이다.
	/// </summary>
	public int HeapUsed { get; private set; }

	/// <summary>
	/// 이 캐릭터의 힙 객체 저장소. 스킬 코드가 힙에 올린 객체(소환검 등)를 포인터로 보관한다.
	/// </summary>
	public HeapStorage Heap { get; private set; }

	/// <summary>
	/// 남은 칸 수(Capacity - StackUsed - HeapUsed). 0보다 작아지지 않는다.
	/// </summary>
	// 힙을 함께 빼 두어, 힙을 쓰기 시작하면 스택이 쓸 수 있는 칸이 따로 고치지 않아도 줄어들게 한다
	// 메모리 크기가 줄어 사용량보다 작아지면 계산 값이 음수가 되므로 0으로 막는다. 0이면 TryAllocateStack이 새 프레임을 거절한다
	public int Free => Mathf.Max(0, Capacity - StackUsed - HeapUsed);

	void Awake()
	{
		stats = GetComponent<PlayerStats>();
		Heap = new HeapStorage(this);
	}

	/// <summary>
	/// 스택을 size칸 쓴다. 남은 칸이 모자라면 쓰지 않고 false를 돌려준다(오버플로).
	/// </summary>
	public bool TryAllocateStack(int size)
	{
		if (size > Free) return false;

		StackUsed += size;
		return true;
	}

	/// <summary>
	/// 스택 size칸을 돌려준다. TryAllocateStack으로 받은 만큼만 돌려준다.
	/// </summary>
	public void ReleaseStack(int size)
	{
		StackUsed -= size;
	}

	/// <summary>
	/// 힙을 size칸 쓴다. 남은 칸이 모자라면 쓰지 않고 false를 돌려준다(malloc 실패).
	/// </summary>
	// 스택과 같은 Free를 본다. 스택과 힙이 한 영역을 함께 쓰므로, 둘의 합이 메모리 크기를 넘지 않게 하기 위함
	// HeapStorage만 부르도록 internal로 둔다. 다른 스크립트가 칸 수만 바꾸면 HeapUsed가 저장소의 객체 크기 합과 어긋나, Clear를 불러도 0으로 돌아가지 않기 때문
	internal bool TryAllocateHeap(int size)
	{
		if (size > Free) return false;

		HeapUsed += size;
		return true;
	}

	/// <summary>
	/// 힙 size칸을 돌려준다. TryAllocateHeap으로 받은 만큼만 돌려준다.
	/// </summary>
	internal void ReleaseHeap(int size)
	{
		HeapUsed -= size;
	}
}
