using System;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 하나의 힙 객체 저장소. 스킬 코드가 힙에 올린 객체(소환검 등)를 포인터 값으로 보관한다.
/// <para>
/// 사용법<br/>
/// - ProcessMemory가 만들어 가지고 있다. 다른 스크립트는 ProcessMemory.Heap으로 가져온다.<br/>
/// - Malloc으로 객체를 힙에 올리고 포인터를 받는다. 힙이 모자라면 NullPointer(0)를 받는다.<br/>
/// - TryGet으로 포인터의 객체를 꺼내고, Free로 해제한다.<br/>
/// - 다른 작업이 올려 둔 객체(예: 소환검)는 GetGlobalPointers로 숨은 시스템 전역에서 포인터를 찾는다.
/// </para>
/// <para>
/// 루트 집합과 누수<br/>
/// - 루트 집합: 포인터를 보관하는 곳들. 작업마다의 포인터 목록(taskPointers), 숨은 시스템 전역(globalPointers), 플레이어의 전역 변수(ProcessMemory.Globals)에 담긴 포인터 세 가지다.<br/>
/// - 작업이 malloc하면 그 작업의 포인터 목록에 포인터가 들어간다(C의 지역 포인터). 작업이 끝나면 그 목록은 사라진다.<br/>
/// - 사전 정의 함수가 정상으로 끝나면, 그 함수가 malloc한 포인터는 숨은 시스템 전역으로 옮겨진다(C 라이브러리의 static 전역 변수처럼 플레이어에게 보이지 않음).<br/>
/// - 작업이 끝날 때 루트 집합의 어디에도 포인터가 없는 객체는 누수가 된다. 누수 객체는 힙을 계속 차지하고, 포인터로 찾을 수 없다.
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

	// 루트 집합 2: 숨은 시스템 전역. C 라이브러리의 static 전역 변수에 해당하며, 플레이어는 볼 수 없고 사전 정의 함수만 GetGlobalPointers로 쓴다
	// 루트 집합 3(플레이어의 전역 변수)은 ProcessMemory.Globals가 가지므로 여기 따로 두지 않는다. 누수 판단 때마다 읽는다
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
	/// owner가 null이면 숨은 시스템 전역에 들어간다(작업 밖의 코드가 올리는 경우).<br/>
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
	/// 숨은 시스템 전역에 있는 T 타입 객체의 포인터를 값 순서(malloc한 순서)대로 돌려준다.<br/>
	/// 다른 작업이 올려 둔 객체(예: 먼저 시전한 검 소환의 소환검)를 찾을 때 쓴다. 아직 진행 중인 사전 정의 함수가 malloc한 객체는 나오지 않는다.
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
		// 진행 중인 작업의 포인터도 지운다. 남겨 두면 그 작업의 사전 정의 함수가 끝날 때 숨은 시스템 전역으로 옮겨져, 없는 기록을 가리키게 되기 때문
		taskPointers.Clear();
		globalPointers.Clear();
		memory.ReleaseHeap(releasedSize);

		// 이벤트는 다 지운 뒤에 알린다. 받은 쪽이 Malloc을 불러도 지우는 중인 기록과 섞이지 않게 하기 위함
		foreach (object obj in clearedObjects)
			ObjectCleared?.Invoke(obj);
	}

	// 사전 정의 함수가 정상으로 끝났을 때 부른다. 그 함수가 malloc한 포인터를 owner 작업의 포인터 목록에서 숨은 시스템 전역으로 옮긴다
	// 작업이 아니라 함수가 끝날 때 옮기는 이유: 플레이어 함수 안에서 부른 검 소환도, 그 플레이어 함수가 나중에 실패하든 상관없이 검 소환이 끝난 순간 소환검을 남기기 위함
	// 그 사이에 해제했거나 Clear로 지운 포인터는 옮기지 않는다
	internal void MoveToSystemGlobals(FunctionTask owner, IEnumerable<int> pointers)
	{
		taskPointers.TryGetValue(owner, out List<int> ownerPointers);
		foreach (int pointer in pointers)
		{
			if (!entries.ContainsKey(pointer)) continue;

			ownerPointers?.Remove(pointer);
			globalPointers.Add(pointer);
		}
	}

	/// <summary>
	/// 작업이 끝났을 때 실행기가 부른다. 그 작업의 포인터 목록(지역 포인터)을 지운 뒤, 루트 집합의 어디에도 포인터가 없게 된 객체를 누수로 표시한다.
	/// </summary>
	// 끝난 이유(정상 종료, 실패, 실행 중단)와 상관없이 지역 포인터는 사라진다. 남길 포인터는 그 전에 전역 변수나 숨은 시스템 전역에 들어가 있다
	// malloc하지 않은 작업도 누수를 판단한다. 그 작업이 전역 변수의 포인터를 바꿔(예: g = NULL) 예전 객체를 놓쳤을 수 있기 때문
	internal void HandleTaskEnded(FunctionTask task)
	{
		taskPointers.Remove(task);
		MarkUnreachableAsLeaked();
	}

	// 누수 판단. 루트 집합의 어디에도 포인터가 없는 객체를 누수로 표시한다(실제 C의 누수 검사, Java GC와 같은 도달 가능성 검사)
	void MarkUnreachableAsLeaked()
	{
		HashSet<int> reachable = new HashSet<int>(globalPointers);
		foreach (List<int> pointers in taskPointers.Values)
			reachable.UnionWith(pointers);
		reachable.UnionWith(GetPlayerGlobalPointers());

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

	/// <summary>
	/// 힙 칸 수를 주인별로 나눈 값. 메모리 화면이나 테스트 로그에서 읽는다.<br/>
	/// 한 객체를 여러 곳이 가리켜도 한 번만 센다. 우선순위: 플레이어 전역 변수 → 숨은 시스템 전역 → 작업의 지역 포인터. 네 값의 합은 ProcessMemory.HeapUsed와 같다.
	/// </summary>
	public HeapUsage GetUsageByOwner()
	{
		HashSet<int> playerGlobals = new HashSet<int>(GetPlayerGlobalPointers());
		HashSet<int> taskLocals = new HashSet<int>();
		foreach (List<int> pointers in taskPointers.Values)
			taskLocals.UnionWith(pointers);

		int playerGlobalSize = 0, systemGlobalSize = 0, taskLocalSize = 0, leakedSize = 0;
		foreach (KeyValuePair<int, HeapEntry> pair in entries)
		{
			int size = pair.Value.Size;
			if (pair.Value.IsLeaked) leakedSize += size;
			else if (playerGlobals.Contains(pair.Key)) playerGlobalSize += size;
			else if (globalPointers.Contains(pair.Key)) systemGlobalSize += size;
			else if (taskLocals.Contains(pair.Key)) taskLocalSize += size;
			// 루트 집합에 없지만 아직 누수로 표시되지 않은 객체(작업이 끝나기 전에 전역 변수의 포인터를 바꾼 경우)는 다음 누수 판단 전까지 지역으로 센다
			else taskLocalSize += size;
		}
		return new HeapUsage(playerGlobalSize, systemGlobalSize, taskLocalSize, leakedSize);
	}

	// 플레이어의 전역 변수에 담긴 포인터 값들(루트 집합 3). NULL은 뺀다
	IEnumerable<int> GetPlayerGlobalPointers()
	{
		if (memory.Globals == null) yield break;

		foreach (RuntimeValue value in memory.Globals.AllValues())
		{
			if (value.Type?.Kind != VariableTypeKind.Pointer) continue;

			int pointer = value.AsPointer();
			if (pointer != NullPointer) yield return pointer;
		}
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
