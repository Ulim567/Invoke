using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 소환검(malloc으로 만든 검)들의 목록, 포인터, 상태를 관리하는 컴포넌트. 플레이어 프리팹에 ProcessMemory와 함께 붙인다 (#99).
/// <para>
/// - 포인터는 이우림 heap(ProcessMemory.Heap)의 포인터를 그대로 쓴다. 소환검 하나 = heap 객체 하나.<br/>
/// - heap에 올리고 내리는 일(malloc, free)은 검 소환·해제 스킬(PredefinedFunction.Malloc / Free)이나 플레이어의 malloc(Sword) / free 블록이 한다. 이 컴포넌트는 오브젝트와 목록만 맡는다.<br/>
/// - malloc(Sword) 블록용 만들기 함수를 ProcessMemory.StructObjects에 등록하고, heap의 ObjectFreed를 받아 해제된 소환검을 없앤다 (#191, #220).<br/>
/// - 최대 5개. 누수된 소환검도 개수에 들어간다 (HeapStorage.CountObjects).<br/>
/// - 소환검 오브젝트는 SpawnSword / DespawnSword로만 만들고 없앤다 (코드 작성 규칙 3).<br/>
/// - 던진 소환검은 회수해야 다시 쓴다: 검 돌아오기(Recall), 줍기(TryPickUpNearest), 해제 후 다시 소환.<br/>
/// - 슬롯에서 바로 쓰는 스킬은 가장 먼저 소환한(포인터가 작은) 사용 가능한 검부터 쓴다 (TryGetFirstReady). sword 파라미터(Sword*)로 넘기면 그 검을 쓴다 (#220).<br/>
/// - 해제한 포인터는 heap에서 찾을 수 없다. 스킬이 TryGetHeapObject / Free로 쓰면 InvalidPointerError로 그 작업만 끝난다 (추가 페널티는 기획 결정 후).
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
	[Tooltip("검 타기에서 검이 붙는 자리: 발밑 기준 (유닛, #256)")]
	[SerializeField] Vector2 rideOffset = new Vector2(0f, -0.15f);
	[Tooltip("떨어진 소환검과 이 거리 안이면 주울 수 있다 (유닛)")]
	[SerializeField, Min(0f)] float pickUpRange = 1.5f;

	ProcessMemory memory;
	PlayerMovement movement;
	// 소환한 순서(포인터 순서). 해제하면 빠지고, 누수돼도 남는다
	readonly List<SummonedSword> swords = new List<SummonedSword>();

	/// <summary>소환검 목록 (포인터 순서). HUD, 스킬이 읽는다.</summary>
	public IReadOnlyList<SummonedSword> Swords => swords;

	/// <summary>heap에 있는 소환검 수 (누수 포함). 최대 개수 검사에 쓴다. 클라이언트에서는 서버가 알려 준 소환검 목록의 수다 (#229).</summary>
	public int Count => IsClientOnly ? swords.Count : memory.Heap.CountObjects<SummonedSword>();

	// 클라이언트로만 접속한 화면이면 true. 이때 heap은 비어 있고 목록은 SummonedSword가 스스로 채운다 (#229)
	static bool IsClientOnly => Mirror.NetworkClient.active && !Mirror.NetworkServer.active;

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

	/// <summary>[로직] 소환검 찌르기(Stab)로 보낸 검이 대상에 닿았을 때 (검, 대상). 데미지는 받은 스킬이 준다 (#165)</summary>
	public event Action<SummonedSword, GameObject> StabHit;

	/// <summary>[로직] 검 돌아오기로 날아오는 소환검이 몬스터를 지나갔을 때 (검, 몬스터). 몬스터마다 한 번. 데미지는 받은 스킬이 준다 (#167)</summary>
	public event Action<SummonedSword, GameObject> ReturnHit;

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
		memory.Heap.ObjectFreed += OnObjectFreed;
		// 블록코딩의 malloc(Sword)도 진짜 소환검을 만들게 등록한다 (#191, #220). heap이 모자라면 실행기가 만들기 함수를 부르지 않는다
		memory.StructObjects.Register<SummonedSword>(PredefinedStructs.Sword, () => CanSummon ? SpawnSword() : null, Register);
	}

	/// <summary>
	/// 소환검 오브젝트를 하나 만든다. 아직 목록에 없고 포인터도 없다. 검 소환 스킬이 부른다.<br/>
	/// 순서: SpawnSword → PredefinedFunction.Malloc → 성공하면 Register, NullPointer면 DespawnSword.<br/>
	/// 소환검은 이 함수로만 만든다 (코드 작성 규칙 3). NetworkSpawner로 내 장소 씬에 만든다 (place-scenes.md). 서버에서만 부른다.
	/// </summary>
	public SummonedSword SpawnSword()
	{
		Vector2 at = (Vector2)transform.position + followOffset;
		GameObject obj = NetworkSpawner.Spawn(swordPrefab.gameObject, at, Quaternion.identity, gameObject.scene);
		return obj != null ? obj.GetComponent<SummonedSword>() : null;
	}

	/// <summary>
	/// malloc에 성공한 소환검을 포인터와 함께 목록에 올린다. 검 소환 스킬이 Malloc 다음에 부른다.
	/// </summary>
	public void Register(SummonedSword sword, int pointer)
	{
		if (sword == null || pointer == HeapStorage.NullPointer || swords.Contains(sword)) return;
		sword.Init(this, pointer, groundLayers);
		swords.Add(sword);
		NotifyChanged();
	}

	/// <summary>
	/// 소환검 오브젝트를 없애고 목록에서 뺀다. heap에서 해제되면(ObjectFreed: 해제 스킬, free 블록) 이 컴포넌트가 직접 부르고, 검 소환 스킬은 malloc에 실패했을 때 부른다.<br/>
	/// heap은 건드리지 않는다 (heap 해제는 스킬의 Free). 소환검은 이 함수로만 없앤다 (코드 작성 규칙 3).
	/// </summary>
	public void DespawnSword(SummonedSword sword)
	{
		if (sword == null) return;
		bool listed = swords.Remove(sword);
		NetworkSpawner.Despawn(sword.gameObject);
		if (listed) NotifyChanged();
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
	/// 소환검을 방향으로 일직선으로 던진다 (소환검 던지기, #167). target이 있으면 그 대상에 닿았을 때만 Hit을 알리고 몸에 꽂힌다.<br/>
	/// target이 null이면(빗나감) 맞지 않고 날아가다 벽·땅에 꽂히거나 최대 거리에서 떨어진다.
	/// </summary>
	public bool Throw(SummonedSword sword, GameObject target, Vector2 direction)
	{
		if (sword == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Ready) return false;
		sword.LaunchStraight(target, direction);
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 검 돌아오기 (#167): 던진(날아가는 중, 떨어진·꽂힌) 소환검을 지금 자리에서 곁으로 날아오게 한다.<br/>
	/// 지나가는 몬스터는 ReturnHit으로 알리고, 닿으면 다시 사용 가능. 누수된 검은 안 된다.
	/// </summary>
	public bool StartReturn(SummonedSword sword)
	{
		if (sword == null || !swords.Contains(sword)) return false;
		if (sword.State != SummonedSwordState.Flying && sword.State != SummonedSwordState.Dropped) return false;
		sword.LaunchReturn();
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 소환검을 대상에게 보내 찌르게 한다 (소환검 찌르기, #165). 사용 가능한 검만 보낼 수 있다.<br/>
	/// 닿으면 StabHit을 알리고 곁으로 돌아와 다시 사용 가능해진다. 대상이 사라지거나 시간 안에 못 닿으면 그냥 돌아온다.
	/// </summary>
	public bool Stab(SummonedSword sword, GameObject target) => Stab(sword, target, 0);

	/// <summary>
	/// Stab과 같고, side(-1 왼쪽, 1 오른쪽)면 먼저 대상의 그쪽 옆으로 간 뒤 안쪽으로 찌른다 (표식 남기기, #243). 0이면 바로 찌른다.
	/// </summary>
	public bool Stab(SummonedSword sword, GameObject target, int side)
	{
		if (sword == null || target == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Ready) return false;
		sword.LaunchStab(target, Mathf.Clamp(side, -1, 1));
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 검무 (#243): 사용 가능한 검을 대상 둘레로 보내 startAngle(도)부터 돌게 한다. 데미지는 스킬이 준다.<br/>
	/// 도는 동안은 Dancing 상태라 다른 스킬에 쓰이지 않는다. EndDance로 끝내면 곁으로 돌아온다.
	/// </summary>
	public bool Dance(SummonedSword sword, GameObject target, float startAngle)
	{
		if (sword == null || target == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Ready) return false;
		sword.LaunchDance(target, startAngle);
		NotifyChanged();
		return true;
	}

	/// <summary>
	/// 검 타기 (#256): 사용 가능한 검을 플레이어 발밑(Ride Offset)으로 보내 seconds초 동안 붙인다. Riding 상태라 다른 스킬에 쓰이지 않는다.<br/>
	/// 시간이 다 되면 이 컴포넌트가 스스로 EndRide해서 곁으로 돌아온다. 그 전에 EndRide를 부르면 바로 돌아온다.
	/// </summary>
	public bool Ride(SummonedSword sword, float seconds)
	{
		if (sword == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Ready) return false;
		sword.LaunchRide(seconds);
		NotifyChanged();
		return true;
	}

	/// <summary>검 타기를 끝낸다 (#256). 타고 있던 검만 다시 사용 가능으로 바뀌고 곁의 자리로 날아온다.</summary>
	public void EndRide(SummonedSword sword)
	{
		if (sword == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Riding) return;
		sword.SetReady();
		NotifyChanged();
	}

	// 검 타기에서 검이 붙을 자리: 플레이어 발밑
	internal Vector2 RidePoint => (Vector2)transform.position + rideOffset;

	/// <summary>검무를 끝낸다 (#243). 도는 중인 검만 다시 사용 가능으로 바뀌고 곁의 자리로 날아온다.</summary>
	public void EndDance(SummonedSword sword)
	{
		if (sword == null || !swords.Contains(sword) || sword.State != SummonedSwordState.Dancing) return;
		sword.SetReady();
		NotifyChanged();
	}

	/// <summary>
	/// 던진(날아가는 중, 떨어진) 소환검을 거리와 상관없이 곁으로 되돌린다 (검 돌아오기). 누수된 검은 되돌릴 수 없다.
	/// </summary>
	public bool Recall(SummonedSword sword)
	{
		if (sword == null || !swords.Contains(sword)) return false;
		if (sword.State != SummonedSwordState.Flying && sword.State != SummonedSwordState.Dropped && sword.State != SummonedSwordState.Returning) return false;
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

	internal void NotifyStabHit(SummonedSword sword, GameObject target) => StabHit?.Invoke(sword, target);

	internal void NotifyReturnHit(SummonedSword sword, GameObject monster) => ReturnHit?.Invoke(sword, monster);

	// 검 돌아오기로 날아온 소환검이 곁에 닿았을 때
	internal void NotifyReturned(SummonedSword sword)
	{
		if (!swords.Contains(sword) || sword.State != SummonedSwordState.Returning) return;
		sword.SetReady();
		NotifyChanged();
	}

	// 돌아오는 소환검이 향할 곳: 머리 뒤쪽 첫 자리
	internal Vector2 ReturnPoint
	{
		get
		{
			int facing = movement != null ? movement.Facing : 1;
			return (Vector2)transform.position + new Vector2(-facing * followOffset.x, followOffset.y);
		}
	}

	// 찌르고 돌아온 소환검을 다시 사용 가능으로
	internal void NotifyStabReturned(SummonedSword sword)
	{
		if (!swords.Contains(sword) || sword.State != SummonedSwordState.Stabbing) return;
		sword.SetReady();
		NotifyChanged();
	}

	// 던진 소환검이 땅에 닿았을 때 SummonedSword가 부른다
	internal void NotifyLanded(SummonedSword sword)
	{
		if (!swords.Contains(sword) || sword.State != SummonedSwordState.Flying) return;
		sword.SetDropped();
		NotifyChanged();
	}

	void NotifyChanged() => Changed?.Invoke();

	// [클라이언트] 서버가 보낸 소환검이 주인 목록에 들어오고 나간다 (#229). SummonedSword만 부른다
	// 클라이언트에는 heap이 없어서, 서버의 목록 대신 소환검 오브젝트가 알려 주는 포인터·상태로 목록을 만든다 (HUD, 여러 명 선택 최대 인원이 읽음)
	internal void ClientAdd(SummonedSword sword)
	{
		if (!IsClientOnly || sword == null || swords.Contains(sword)) return;
		swords.Add(sword);
		ClientSwordChanged(sword);
	}

	internal void ClientRemove(SummonedSword sword)
	{
		if (!IsClientOnly) return;
		if (swords.Remove(sword)) NotifyChanged();
	}

	// 포인터 순서(소환 순서)로 다시 줄 세우고 알린다
	internal void ClientSwordChanged(SummonedSword sword)
	{
		if (!IsClientOnly || !swords.Contains(sword)) return;
		swords.Sort((a, b) => a.Pointer.CompareTo(b.Pointer));
		NotifyChanged();
	}

	void LateUpdate()
	{
		// 클라이언트에서는 소환검이 움직이지 않으므로 자리를 정하지 않는다 (#229)
		if (IsClientOnly) return;
		// 검 타기 시간이 다 된 검은 곁으로 (#256)
		for (int i = swords.Count - 1; i >= 0; i--)
		{
			SummonedSword s = swords[i];
			if (s != null && s.State == SummonedSwordState.Riding && Time.time >= s.RideEndTime) EndRide(s);
		}
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

	// heap에서 해제된 소환검(해제 스킬, 플레이어의 free 블록): 오브젝트를 없앤다 (#191, #220). 스킬 쪽에서는 지우지 않는다
	void OnObjectFreed(object obj)
	{
		if (obj is SummonedSword sword && swords.Contains(sword)) DespawnSword(sword);
	}

	// 영역 비우기(재부팅 등)로 heap에서 지워진 소환검: 오브젝트도 없앤다
	void OnObjectCleared(object obj)
	{
		if (obj is SummonedSword sword && swords.Contains(sword)) DespawnSword(sword);
	}

	// 캐릭터가 사라지면 소환검도 남기지 않는다
	void OnDestroy()
	{
		if (memory != null && memory.Heap != null)
		{
			memory.Heap.ObjectLeaked -= OnObjectLeaked;
			memory.Heap.ObjectCleared -= OnObjectCleared;
			memory.Heap.ObjectFreed -= OnObjectFreed;
		}
		memory?.StructObjects?.Unregister(PredefinedStructs.Sword);
		// 클라이언트에서는 서버가 없애므로 부르지 않는다 (NetworkSpawner는 서버에서만 부름)
		// 캐릭터가 사라지는 중이라 Changed는 알리지 않아도 된다
		if (!(Mirror.NetworkClient.active && !Mirror.NetworkServer.active))
			foreach (SummonedSword s in swords.ToArray()) if (s != null) NetworkSpawner.Despawn(s.gameObject);
		swords.Clear();
	}
}
