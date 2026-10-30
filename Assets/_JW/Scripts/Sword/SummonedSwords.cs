using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 소환검(malloc으로 만든 검)들의 목록, 포인터, 상태를 관리하는 컴포넌트. 플레이어 프리팹에 ProcessMemory와 함께 붙인다 (#99).
/// <para>
/// - 포인터는 이우림 heap(ProcessMemory.Heap)의 포인터를 그대로 쓴다. 소환검 하나 = heap 객체 하나.<br/>
/// - 최대 5개. 누수된 소환검도 개수에 들어간다 (HeapStorage.CountObjects).<br/>
/// - 소환검 오브젝트는 SpawnSword / DespawnSword로만 만들고 없앤다 (코드 작성 규칙 3).<br/>
/// - 던진 소환검은 회수해야 다시 쓴다: 검 돌아오기(Recall), 줍기(TryPickUpNearest), 해제(Free) 후 다시 소환.<br/>
/// - 슬롯에서 바로 쓰는 스킬은 가장 먼저 소환한(포인터가 작은) 사용 가능한 검부터 쓴다 (TryGetFirstReady).<br/>
/// - 해제한 포인터는 heap에서 찾을 수 없다. 스킬이 TryGetHeapObject로 꺼내면 InvalidPointerError로 그 작업만 끝난다 (추가 페널티는 기획 결정 후).
/// </para>
/// </summary>
[RequireComponent(typeof(ProcessMemory))]
public class SummonedSwords : MonoBehaviour
{
	/// <summary>소환검 최대 개수</summary>
	public const int MaxCount = 5;

	[Tooltip("소환검 프리팹")]
	[SerializeField] SummonedSword swordPrefab;
	[Tooltip("떨어진 소환검이 멈출 바닥 레이어 (Ground, Platform)")]
	[SerializeField] LayerMask groundLayers;
	[Tooltip("사용 가능한 소환검이 떠 있는 자리: 발밑 기준. x는 바라보는 반대쪽으로, 검마다 X Spacing씩 벌어진다")]
	[SerializeField] Vector2 followOffset = new Vector2(0.6f, 3.2f);
	[SerializeField, Min(0f)] float followSpacing = 0.45f;
	[Tooltip("떨어진 소환검과 이 거리 안이면 주울 수 있다 (유닛)")]
	[SerializeField, Min(0f)] float pickUpRange = 1.5f;

	ProcessMemory memory;
	PlayerMovement movement;
	// 소환한 순서(포인터 순서). 해제하면 빠지고, 누수돼도 남는다
	readonly List<SummonedSword> swords = new List<SummonedSword>();

	/// <summary>소환검 목록 (포인터 순서). HUD, 스킬이 읽는다.</summary>
	public IReadOnlyList<SummonedSword> Swords => swords;

	/// <summary>heap에 있는 소환검 수 (누수 포함). 최대 개수 검사에 쓴다.</summary>
	public int Count => memory.Heap.CountObjects<SummonedSword>();

	/// <summary>더 소환할 수 있으면 true (최대 5개).</summary>
	public bool CanSummon => Count < MaxCount;

	/// <summary>사용 가능한(플레이어 곁에 있는) 소환검 수. 소환검이 필요한 스킬, 여러 명 선택 최대 인원이 읽는다.</summary>
	public int ReadyCount
	{
		get
		{
			int count = 0;
			foreach (SummonedSword sword in swords) if (sword != null && sword.State == SummonedSwordState.Ready) count++;
			return count;
		}
	}

	/// <summary>[표시] 소환검 목록이나 상태가 바뀌었을 때. HUD가 다시 그린다.</summary>
	public event Action Changed;

	/// <summary>[로직] 던진 소환검이 대상에 맞았을 때 (검, 맞은 대상). 데미지는 이 이벤트를 받는 쪽(스킬)이 준다.</summary>
	public event Action<SummonedSword, GameObject> Hit;

	void Reset()
	{
		groundLayers = LayerMask.GetMask("Ground", "Platform");
	}

	void Awake()
	{
		memory = GetComponent<ProcessMemory>();
		movement = GetComponent<PlayerMovement>();
	}

	// heap은 ProcessMemory.Awake에서 만들어지므로 Start에서 구독한다 (컴포넌트 순서와 상관없게)
	void Start()
	{
		memory.Heap.ObjectLeaked += OnObjectLeaked;
		memory.Heap.ObjectCleared += OnObjectCleared;
	}

	/// <summary>
	/// 소환검 하나를 만들어 heap heapSize칸에 올리고 포인터를 돌려준다 (malloc). owner는 malloc하는 작업(검 소환 스킬의 task).<br/>
	/// 최대 개수거나 heap이 모자라면 만들지 않고 HeapStorage.NullPointer를 돌려준다.
	/// </summary>
	public int Summon(FunctionTask owner, int heapSize)
	{
		if (!CanSummon) return HeapStorage.NullPointer;

		SummonedSword sword = SpawnSword();
		int pointer = memory.Heap.Malloc(owner, sword, heapSize);
		if (pointer == HeapStorage.NullPointer)
		{
			DespawnSword(sword);
			return HeapStorage.NullPointer;
		}

		sword.Init(this, pointer, groundLayers);
		swords.Add(sword);
		NotifyChanged();
		return pointer;
	}

	/// <summary>
	/// 포인터의 소환검을 해제한다 (free). heap 칸을 돌려주고 오브젝트를 없앤다.<br/>
	/// 해제한 포인터, 누수된 포인터, 소환검이 아닌 포인터이면 false. 스킬은 false면 InvalidPointerError로 끝낸다.
	/// </summary>
	public bool Free(int pointer)
	{
		if (!memory.Heap.TryGet(pointer, out SummonedSword sword)) return false;
		if (!memory.Heap.Free(pointer)) return false;
		swords.Remove(sword);
		DespawnSword(sword);
		NotifyChanged();
		return true;
	}

	/// <summary>포인터로 소환검을 찾는다. 해제·누수된 포인터이면 false.</summary>
	public bool TryGetSword(int pointer, out SummonedSword sword) => memory.Heap.TryGet(pointer, out sword);

	/// <summary>가장 먼저 소환한(포인터가 작은) 사용 가능한 소환검. 슬롯에서 바로 쓰는 스킬이 쓴다.</summary>
	public bool TryGetFirstReady(out SummonedSword sword)
	{
		foreach (SummonedSword s in swords)
		{
			if (s != null && s.State == SummonedSwordState.Ready) { sword = s; return true; }
		}
		sword = null;
		return false;
	}

	/// <summary>
	/// 소환검을 던진다. 사용 가능한 검만 던질 수 있다. target이 없으면 바로 떨어진다(빗나감).
	/// </summary>
	public bool Throw(SummonedSword sword, GameObject target)
	{
		if (sword == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Ready) return false;
		sword.Launch(target);
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 던진(날아가는 중, 떨어진) 소환검을 거리와 상관없이 곁으로 되돌린다 (검 돌아오기). 누수된 검은 되돌릴 수 없다.
	/// </summary>
	public bool Recall(SummonedSword sword)
	{
		if (sword == null || !swords.Contains(sword)) return false;
		if (sword.State != SummonedSwordState.Flying && sword.State != SummonedSwordState.Dropped) return false;
		sword.SetReady();
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 던진 검 중 가장 먼저 소환한 것을 되돌린다. 검 돌아오기 스킬이 쓴다.
	/// </summary>
	public bool RecallFirstThrown()
	{
		foreach (SummonedSword s in swords)
		{
			if (s != null && (s.State == SummonedSwordState.Flying || s.State == SummonedSwordState.Dropped)) return Recall(s);
		}
		return false;
	}

	/// <summary>
	/// 근처(Pick Up Range)에 떨어진 소환검 중 가장 가까운 것을 줍는다. 주울 게 없으면 false. 줍기 키(Z)에서 부른다.
	/// </summary>
	public bool TryPickUpNearest()
	{
		SummonedSword nearest = null;
		float best = pickUpRange;
		foreach (SummonedSword s in swords)
		{
			if (s == null || s.State != SummonedSwordState.Dropped) continue;
			float d = Vector2.Distance(s.transform.position, transform.position);
			if (d <= best) { best = d; nearest = s; }
		}
		if (nearest == null) return false;
		nearest.SetReady();
		NotifyChanged();
		return true;
	}

	internal void NotifyHit(SummonedSword sword, GameObject target) => Hit?.Invoke(sword, target);

	internal void NotifyChanged() => Changed?.Invoke();

	void LateUpdate()
	{
		// 사용 가능한 검을 머리 뒤쪽에 순서대로 줄 세운다
		int facing = movement != null ? movement.Facing : 1;
		int slot = 0;
		foreach (SummonedSword s in swords)
		{
			if (s == null || s.State != SummonedSwordState.Ready) continue;
			float x = -facing * (followOffset.x + followSpacing * slot);
			s.FollowPoint = (Vector2)transform.position + new Vector2(x, followOffset.y);
			slot++;
		}
	}

	// heap이 포인터를 잃은 소환검: 그 자리에 멈춘다. 목록과 개수에는 남는다
	void OnObjectLeaked(object obj)
	{
		if (obj is SummonedSword sword && swords.Contains(sword))
		{
			sword.SetLeaked();
			NotifyChanged();
		}
	}

	// 영역 비우기(재부팅 등)로 heap에서 지워진 소환검: 오브젝트도 없앤다
	void OnObjectCleared(object obj)
	{
		if (obj is SummonedSword sword && swords.Remove(sword))
		{
			DespawnSword(sword);
			NotifyChanged();
		}
	}

	// 소환검은 이 두 함수로만 만들고 없앤다. 네트워크를 붙일 때 여기만 NetworkServer.Spawn / Destroy로 바꾼다
	SummonedSword SpawnSword()
	{
		Vector2 at = (Vector2)transform.position + followOffset;
		return Instantiate(swordPrefab, at, Quaternion.identity);
	}

	void DespawnSword(SummonedSword sword)
	{
		if (sword != null) Destroy(sword.gameObject);
	}

	// 캐릭터가 사라지면 소환검도 남기지 않는다
	void OnDestroy()
	{
		if (memory != null && memory.Heap != null)
		{
			memory.Heap.ObjectLeaked -= OnObjectLeaked;
			memory.Heap.ObjectCleared -= OnObjectCleared;
		}
		foreach (SummonedSword s in swords) DespawnSword(s);
		swords.Clear();
	}
}
