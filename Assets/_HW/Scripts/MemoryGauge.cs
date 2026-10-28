using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 내 캐릭터의 메모리 게이지. 메모리 한 영역을 Stack(위에서부터)과 Heap(아래에서부터)이 나눠 쓰는 모습을 막대 하나로 보여 준다.
/// 값은 <c>ProcessMemory</c>(이우림)에서 읽기만 한다.
/// <para>
/// 사용법: Canvas 안에 게이지 오브젝트를 만들고 이 컴포넌트를 붙인다.<br/>
/// - Stack Fill: Image Type = Filled, Fill Method = Vertical, Fill Origin = Top<br/>
/// - Heap Fill: Image Type = Filled, Fill Method = Vertical, Fill Origin = Bottom<br/>
/// 내 캐릭터는 Host 버튼을 누른 뒤에 생기므로, 나타나면 스스로 찾아 연결한다.
/// </para>
/// </summary>
public class MemoryGauge : MonoBehaviour
{
	[SerializeField, Tooltip("위에서부터 차는 Stack 막대 (Filled, Vertical, Top)")]
	Image stackFill;

	[SerializeField, Tooltip("아래에서부터 차는 Heap 막대 (Filled, Vertical, Bottom)")]
	Image heapFill;

	[SerializeField, Tooltip("남은 칸 수를 보여 줄 글자 (UI → Legacy → Text). 비워 두면 글자는 쓰지 않는다")]
	Text freeText;

	ProcessMemory memory;
	int lastStack = -1;
	int lastHeap = -1;
	int lastCapacity = -1;

	void Update()
	{
		if (memory == null && !FindLocalMemory()) return;

		// ProcessMemory에는 값이 바뀔 때 알려 주는 event가 없어서 매 프레임 읽고, 바뀌었을 때만 그린다
		int capacity = memory.Capacity;
		int stack = memory.StackUsed;
		int heap = memory.HeapUsed;
		if (capacity == lastCapacity && stack == lastStack && heap == lastHeap) return;

		lastCapacity = capacity;
		lastStack = stack;
		lastHeap = heap;
		Show(capacity, stack, heap);
	}

	bool FindLocalMemory()
	{
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		return localPlayer != null && localPlayer.TryGetComponent(out memory);
	}

	void Show(int capacity, int stack, int heap)
	{
		float max = Mathf.Max(1, capacity);
		if (stackFill != null) stackFill.fillAmount = Mathf.Clamp01(stack / max);
		if (heapFill != null) heapFill.fillAmount = Mathf.Clamp01(heap / max);
		if (freeText != null) freeText.text = $"{memory.Free}/{capacity}";
	}
}
