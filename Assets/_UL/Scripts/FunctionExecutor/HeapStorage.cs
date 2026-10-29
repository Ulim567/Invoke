using System;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 하나의 힙 객체 저장소. 스킬 코드가 힙에 올린 객체(소환검 등)를 포인터 값으로 보관한다.
/// <para>
/// 사용법<br/>
/// - ProcessMemory가 만들어 가지고 있다. 다른 스크립트는 ProcessMemory.Heap으로 가져온다.<br/>
/// - Malloc으로 객체를 힙에 올리고 포인터를 받는다. 힙이 모자라면 NullPointer(0)를 받는다.<br/>
/// - TryGet으로 포인터의 객체를 꺼내고, Free로 해제한다.<br/>
/// - 다른 작업이 올려 둔 객체는 GetGlobalPointers로 포인터를 찾는다.
/// </para>
/// <para>
/// 루트 집합과 누수<br/>
/// - 루트 집합: 포인터를 보관하는 목록들. 지금은 작업마다의 포인터 목록(taskPointers)과 캐릭터의 전역 포인터 목록(globalPointers) 두 가지다.<br/>
/// - 작업이 malloc하면 그 작업의 포인터 목록에 포인터가 들어간다. 작업이 정상으로 끝나면 그 포인터들이 전역 포인터 목록으로 옮겨진다.<br/>
/// - 루트 집합의 어느 목록에도 포인터가 없는 객체는 누수가 된다. 누수 객체는 힙을 계속 차지하고, 포인터로 찾을 수 없다.
/// </para>
/// <para>
/// 이 컴포넌트는 객체를 만들거나 없애지 않는다. 오브젝트 생성·삭제는 스킬 코드의 생성·삭제 함수가 하고, 여기는 메모리 기록만 맡는다.<br/>
/// 힙 칸 수는 ProcessMemory가 센다.
/// </para>
/// </summary>
// 컴포넌트가 아닌 일반 클래스로 둔다. 프로세스 메모리 안에 스택과 힙이 함께 있는 구조를 그대로 나타내고, 프리팹에 따로 붙이지 않아도 되게 하기 위함
// 이후 Java(GC), Python(참조 카운트)의 힙은 관리 방식이 다르므로 이 클래스와 따로 만들어 ProcessMemory에 끼운다
public class HeapStorage
{
	/// <summary>
	/// null 포인터. Malloc이 실패하면 이 값을 돌려준다. 이 값으로는 어떤 객체도 찾을 수 없다.
	/// </summary>
	public const int NullPointer = 0;

	// 힙에 올린 객체 하나의 기록
	// 어느 목록이 포인터를 보관하는지는 기록에 적지 않고 루트 집합이 안다. 루트 집합에 목록이 늘어나도 기록의 형식을 바꾸지 않기 위함
	class HeapEntry
	{
		// C#의 object로 담아, MonoBehaviour든 일반 클래스든 같은 저장소에 넣는다. 꺼낼 때 TryGet이 타입을 확인한다
		public object Obj;
		// Free할 때 ProcessMemory에 돌려줄 칸 수
		public int Size;
		// 누수 표시. 기록을 지우지 않고 표시만 하는 이유: 누수 객체도 힙을 차지하고 소환검 개수에 들어가야 하기 때문
		public bool IsLeaked;
	}

	// 포인터 → 기록
	// 해제한 포인터 값은 다시 쓰지 않으므로, 배열이나 List로는 해제한 칸이 빈칸으로 계속 쌓인다. 그래서 포인터 값으로 바로 찾고 그 줄만 지울 수 있는 Dictionary를 쓴다
	readonly Dictionary<int, HeapEntry> entries = new Dictionary<int, HeapEntry>();

	// 루트 집합 1: 진행 중인 작업마다 그 작업이 malloc한 포인터 목록. C의 지역 포인터에 해당한다
	readonly Dictionary<FunctionTask, List<int>> taskPointers = new Dictionary<FunctionTask, List<int>>();

	// 루트 집합 2: 캐릭터의 전역 포인터 목록. C의 전역 포인터에 해당하며, 어느 작업에서든 이 목록의 포인터를 쓸 수 있다
	readonly HashSet<int> globalPointers = new HashSet<int>();

	// 마지막으로 붙인 포인터 값. 1부터 하나씩 늘려 붙이고, 해제한 값은 최댓값에 닿기 전까지 다시 쓰지 않는다(NextPointer)
	// 해제한 값을 바로 다시 쓰면, 해제 전에 스킬 코드가 들고 있던 포인터가 새 객체를 가리키게 되어 「해제한 포인터로 명령하면 스킬 오류」 규칙을 지킬 수 없다
	int lastPointer = NullPointer;

	// 힙 칸 수를 세는 곳. 이 저장소를 가진 ProcessMemory다
	readonly ProcessMemory memory;

	/// <summary>
	/// 객체가 누수될 때 불린다. 누수된 객체를 넘긴다.<br/>
	/// 누수된 소환검을 그 자리에 멈추고 흐리게 그리는 일은 이 이벤트를 받은 쪽이 한다.
	/// </summary>
	public event Action<object> ObjectLeaked;

	/// <summary>
	/// Clear로 객체가 힙에서 지워질 때 객체마다 불린다. 지워진 객체를 넘긴다.<br/>
	/// 이 저장소는 오브젝트를 없애지 않으므로, 소환검 오브젝트를 없애는 일은 이 이벤트를 받은 소환검 코드가 한다.
	/// </summary>
	public event Action<object> ObjectCleared;

	// ProcessMemory만 만든다
	internal HeapStorage(ProcessMemory memory)
	{
		this.memory = memory;
	}

	/// <summary>
	/// obj를 힙 size칸에 올리고 포인터를 돌려준다. 그 포인터는 owner(malloc하는 작업)의 포인터 목록에 들어간다.<br/>
	/// owner가 null이면 전역 포인터 목록에 들어간다(작업 밖의 코드가 올리는 경우).<br/>
	/// 힙이 모자라면 올리지 않고 NullPointer를 돌려준다(C의 malloc처럼 실패해도 에러로 끝내지 않는다).
	/// </summary>
	public int Malloc(FunctionTask owner, object obj, int size)
	{
		// 빈 객체나 음수 크기는 스킬 코드의 실수로 보고 바로 드러나게 한다. 음수 크기를 받으면 힙 사용량이 줄어 남은 칸이 늘어나기 때문
		if (obj == null) throw new ArgumentNullException(nameof(obj));
		if (size < 0) throw new ArgumentOutOfRangeException(nameof(size), size, "힙 크기는 0 이상이어야 합니다.");

		if (!memory.TryAllocateHeap(size)) return NullPointer;

		int pointer = NextPointer();
		entries.Add(pointer, new HeapEntry { Obj = obj, Size = size });

		if (owner == null)
		{
			globalPointers.Add(pointer);
		}
		else
		{
			if (!taskPointers.TryGetValue(owner, out List<int> pointers))
			{
				pointers = new List<int>();
				taskPointers.Add(owner, pointers);
			}
			pointers.Add(pointer);
		}
		return pointer;
	}

	// 다음에 붙일 포인터 값을 고른다
	// 최댓값(약 21억)에 닿으면 1로 되돌린다. 매 틱 malloc해도 약 497일이 걸리지만, 오래 접속한 플레이어가 그 뒤로 malloc을 못 쓰게 되는 일을 막기 위함
	// 되돌린 뒤에는 아직 해제하지 않은 객체의 포인터 값을 건너뛴다. 같은 포인터가 두 객체를 가리키면 안 되기 때문
	int NextPointer()
	{
		do
		{
			lastPointer = lastPointer == int.MaxValue ? NullPointer + 1 : lastPointer + 1;
		}
		while (entries.ContainsKey(lastPointer));

		return lastPointer;
	}

	/// <summary>
	/// 포인터의 객체를 해제하고 힙 칸을 돌려준다.<br/>
	/// NullPointer, 없는 포인터, 이미 해제한 포인터, 누수된 포인터이면 아무것도 하지 않고 false를 돌려준다.<br/>
	/// 오브젝트를 없애지는 않는다. 오브젝트는 스킬 코드가 따로 없앤다.
	/// </summary>
	public bool Free(int pointer)
	{
		if (!TryGetReachable(pointer, out HeapEntry entry)) return false;

		entries.Remove(pointer);
		// 해제한 포인터를 루트 집합에 남겨 두면, 최댓값에서 값을 되돌린 뒤 새 객체가 같은 값을 받았을 때 엉뚱한 목록이 새 객체의 포인터를 보관하게 된다
		globalPointers.Remove(pointer);
		foreach (List<int> pointers in taskPointers.Values)
			pointers.Remove(pointer);

		memory.ReleaseHeap(entry.Size);
		return true;
	}

	/// <summary>
	/// 포인터의 객체를 T 타입으로 꺼낸다.<br/>
	/// NullPointer, 없는 포인터, 해제한 포인터, 누수된 포인터, 타입이 T가 아닌 객체, Destroy된 Unity 오브젝트이면 false를 돌려준다.
	/// </summary>
	public bool TryGet<T>(int pointer, out T obj)
	{
		obj = default;
		if (!TryGetReachable(pointer, out HeapEntry entry)) return false;
		if (IsDestroyed(entry.Obj)) return false;

		// (T)로 바로 바꾸지 않고 is로 검사한다. 타입이 다를 때 예외가 나면 실행기의 그 틱이 중간에 멈추기 때문
		if (!(entry.Obj is T typed)) return false;

		obj = typed;
		return true;
	}

	/// <summary>
	/// 전역 포인터 목록에 있는 T 타입 객체의 포인터를 값 순서(malloc한 순서)대로 돌려준다.<br/>
	/// 다른 작업이 올려 둔 객체(예: 먼저 시전한 검 소환의 소환검)를 찾을 때 쓴다. 아직 진행 중인 작업이 malloc한 객체는 나오지 않는다.
	/// </summary>
	public List<int> GetGlobalPointers<T>()
	{
		List<int> pointers = new List<int>();
		foreach (int pointer in globalPointers)
		{
			HeapEntry entry = entries[pointer];
			if (IsDestroyed(entry.Obj) || !(entry.Obj is T)) continue;

			pointers.Add(pointer);
		}

		// HashSet은 순서를 지켜 주지 않으므로 포인터 값으로 다시 줄 세운다
		pointers.Sort();
		return pointers;
	}

	/// <summary>
	/// 힙에 있는 T 타입 객체의 수. 누수된 객체도 센다. 소환검 최대 개수 검사에 쓴다.
	/// </summary>
	public int CountObjects<T>()
	{
		int count = 0;
		foreach (HeapEntry entry in entries.Values)
			if (entry.Obj is T) count++;
		return count;
	}

	/// <summary>
	/// 힙을 전부 비운다(영역 비우기). 누수된 객체뿐 아니라 아직 쓰던 객체까지 모두 지우고, 힙 사용량을 0으로 만든다.<br/>
	/// 재부팅과 메모리 비우기 스킬이 부른다. 지운 뒤에는 어떤 포인터로도 객체를 찾을 수 없다.
	/// </summary>
	public void Clear()
	{
		int releasedSize = 0;
		List<object> clearedObjects = new List<object>(entries.Count);
		foreach (HeapEntry entry in entries.Values)
		{
			releasedSize += entry.Size;
			clearedObjects.Add(entry.Obj);
		}

		entries.Clear();
		// 진행 중인 작업의 포인터도 지운다. 남겨 두면 그 작업이 끝날 때 전역 포인터 목록으로 옮겨져, 없는 기록을 가리키게 되기 때문
		taskPointers.Clear();
		globalPointers.Clear();
		memory.ReleaseHeap(releasedSize);

		// 이벤트는 다 지운 뒤에 알린다. 받은 쪽이 Malloc을 불러도 지우는 중인 기록과 섞이지 않게 하기 위함
		foreach (object obj in clearedObjects)
			ObjectCleared?.Invoke(obj);
	}

	/// <summary>
	/// 작업이 끝났을 때 실행기가 부른다. 루트 집합을 갱신한 뒤, 루트 집합의 어느 목록에도 포인터가 없게 된 객체를 누수로 표시한다.
	/// </summary>
	internal void HandleTaskEnded(FunctionTask task)
	{
		// malloc하지 않은 작업이 끝나면 루트 집합에서 빠지는 포인터가 없으므로, 새로 누수될 객체도 없다
		if (!taskPointers.TryGetValue(task, out List<int> pointers)) return;

		taskPointers.Remove(task);
		HandOverPointers(task, pointers);
		MarkUnreachableAsLeaked();
	}

	// 끝난 작업의 포인터 목록을 어디로 옮길지 정한다(루트 집합을 채우는 규칙)
	// 정리 블록이 끝까지 실행된 것은 정상 종료뿐이므로, 정상 종료일 때만 전역 포인터 목록으로 옮긴다. 실행 중단과 실패는 포인터를 잃는다
	// 블록 실행 이슈에서 변수가 생기면 이 함수만 바꾼다. 누수 판단(MarkUnreachableAsLeaked)은 그대로 둔다
	void HandOverPointers(FunctionTask task, List<int> pointers)
	{
		if (task.EndReason == FunctionTaskEndReason.Completed)
			globalPointers.UnionWith(pointers);
	}

	// 누수 판단. 루트 집합의 어느 목록에도 포인터가 없는 객체를 누수로 표시한다(실제 C의 누수 검사, Java GC와 같은 도달 가능성 검사)
	void MarkUnreachableAsLeaked()
	{
		HashSet<int> reachable = new HashSet<int>(globalPointers);
		foreach (List<int> pointers in taskPointers.Values)
			reachable.UnionWith(pointers);

		List<object> leakedObjects = null;
		foreach (KeyValuePair<int, HeapEntry> pair in entries)
		{
			HeapEntry entry = pair.Value;
			if (entry.IsLeaked || reachable.Contains(pair.Key)) continue;

			entry.IsLeaked = true;
			leakedObjects ??= new List<object>();
			leakedObjects.Add(entry.Obj);
		}

		// 이벤트는 다 돈 뒤에 알린다. 받은 쪽이 Malloc이나 Free를 부르면 도는 중인 entries가 바뀌어 오류가 나기 때문
		if (leakedObjects == null) return;
		foreach (object obj in leakedObjects)
			ObjectLeaked?.Invoke(obj);
	}

	// 포인터로 찾을 수 있는 기록. 누수된 기록은 포인터 값을 알아도 찾을 수 없다
	// 포인터 값은 1부터 커지는 숫자라 짐작할 수 있으므로, 막지 않으면 누수 객체를 해제해 벌칙 없이 힙을 돌려받을 수 있기 때문
	bool TryGetReachable(int pointer, out HeapEntry entry)
	{
		return entries.TryGetValue(pointer, out entry) && !entry.IsLeaked;
	}

	// Destroy된 Unity 오브젝트는 C# 참조가 남아 있어도 쓸 수 없다. Unity는 이런 오브젝트를 == null로만 알려 준다
	static bool IsDestroyed(object obj)
	{
		return obj is UnityEngine.Object unityObject && unityObject == null;
	}
}
