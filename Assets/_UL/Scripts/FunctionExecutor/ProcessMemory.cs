using UnityEngine;

/// <summary>
/// 캐릭터(프로세스) 하나의 메모리. 지금은 스택만 다룬다.<br/>
/// 메모리 값은 이 컴포넌트만 바꾼다. 다른 스크립트는 TryAllocateStack, ReleaseStack을 부른다.
/// <para>
/// 메모리 전체 크기는 같은 오브젝트에 붙은 PlayerStats의 MemorySize다.<br/>
/// 메모리 크기가 줄어 스택 사용량보다 작아져도 실행 중인 함수는 끊지 않는다. 남은 칸이 0이 되어, 새로 시전한 함수만 오버플로로 끝난다.
/// </para>
/// <para>
/// 힙과 코드 영역은 이후 이슈에서 추가한다. 기획에서 스택과 힙은 메모리 하나를 나눠 쓰므로, Capacity도 하나만 둔다.
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
	/// 남은 칸 수. 0보다 작아지지 않는다.
	/// </summary>
	// 메모리 크기가 줄어 스택 사용량보다 작아지면 계산 값이 음수가 되므로 0으로 막는다. 0이면 TryAllocateStack이 새 프레임을 거절한다
	public int Free => Mathf.Max(0, Capacity - StackUsed);

	void Awake()
	{
		stats = GetComponent<PlayerStats>();
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
}
