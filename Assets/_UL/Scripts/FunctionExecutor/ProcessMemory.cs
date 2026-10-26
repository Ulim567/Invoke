using UnityEngine;

/// <summary>
/// 캐릭터(프로세스) 하나의 메모리. 지금은 스택만 다룬다.<br/>
/// 메모리 값은 이 컴포넌트만 바꾼다. 다른 스크립트는 TryAllocateStack, ReleaseStack을 부른다.
/// <para>
/// 힙과 코드 영역은 이후 이슈에서 추가한다. 기획에서 스택과 힙은 메모리 하나를 나눠 쓰므로, Capacity도 하나만 둔다.
/// </para>
/// </summary>
public class ProcessMemory : MonoBehaviour
{
	[SerializeField, Min(0), Tooltip("메모리 전체 칸 수")]
	int capacity = 10;

	/// <summary>
	/// 메모리 전체 칸 수.
	/// </summary>
	public int Capacity => capacity;

	/// <summary>
	/// 지금 스택이 쓰고 있는 칸 수. 실행 중인 모든 함수의 프레임 크기 합이다.
	/// </summary>
	public int StackUsed { get; private set; }

	/// <summary>
	/// 남은 칸 수.
	/// </summary>
	public int Free => capacity - StackUsed;

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
