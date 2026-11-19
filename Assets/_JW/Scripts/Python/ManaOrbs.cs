using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 마나 구슬 하나 (heap 객체). 파이썬 마법사의 구슬 생성(py.orb_create)이 heap에 올리고 Orb 값으로 돌려준다 (#532).
/// <para>
/// - 플레이어는 Orb 값을 변수·리스트에 담아 구슬 스킬에 넘긴다.<br/>
/// - 구슬 스킬은 구슬을 해제하지 않고 「다 쓴 구슬」(IsSpent)로만 바꾼다. 스킬은 넘겨받은 값이 변수에서 왔는지 리스트에서 왔는지 알 수 없기 때문이다.<br/>
/// - heap에서 지우는 일은 가비지 컬렉션만 한다(가리키는 이름이 하나도 없을 때).<br/>
/// - 다 쓴 구슬은 쓸 수 있는 개수(ManaOrbs.Count)에서 빠지지만, 최대 5개 제한에는 계속 들어간다(C의 누수 소환검과 같음). 머리 위와 HUD에 빨갛게 보인다.
/// </para>
/// </summary>
public class ManaOrb
{
	/// <summary>구슬 스킬이 이미 쓴 구슬이면 true. 다시 넘기면 UsedManaOrbError.</summary>
	public bool IsSpent { get; private set; }

	/// <summary>
	/// 슬롯에서 바로 시전한 구슬 생성이 만든 구슬이면 true (#593). 돌려받을 변수·리스트가 없어서 숨은 시스템 전역에 둔다.
	/// 담은 곳이 없어 다 쓴 구슬로 남기면 가비지 컬렉션이 영영 해제하지 못하므로, 구슬 스킬이 쓰는 순간 바로 해제한다.
	/// </summary>
	public bool KeptBySlot { get; }

	public ManaOrb(bool keptBySlot = false) => KeptBySlot = keptBySlot;

	/// <summary>[서버] 구슬을 「다 쓴 구슬」로 바꾼다. 구슬 스킬(OrbSkill)만 부른다.</summary>
	public void Spend() => IsSpent = true;
}

/// <summary>
/// 쓸 수 있는 마나 구슬이 없어서 구슬이 필요한 스킬을 쓸 수 없다.
/// </summary>
public class NoManaOrbError : SkillError
{
	// HUD 알림에 그대로 보인다(#480). 일어난 현상만 짧게 적는다
	public override string Message => "쓸 수 있는 마나 구슬이 없음";
}

/// <summary>
/// 구슬 스킬에 이미 쓴 마나 구슬(ManaOrb.IsSpent)을 넘겼다 (#532). 확인은 is_valid_orb(orb)로 한다.
/// </summary>
public class UsedManaOrbError : SkillError
{
	public override string Message => "이미 쓴 마나 구슬";
}

/// <summary>
/// 파이썬 마법사의 마나 구슬 개수와 자리를 관리하는 컴포넌트. 파이썬 플레이어 프리팹에 ProcessMemory와 함께 붙인다.
/// <para>
/// - 구슬 하나 = heap 객체(ManaOrb) 하나. 올리고 내리는 일은 스킬(PredefinedFunction.Malloc / Free)이 한다. 이 컴포넌트는 개수와 자리만 맡는다.<br/>
/// - 최대 MaxCount(5)개. 다 쓴 구슬도 센다(TotalCount). 구슬 생성 스킬이 이 수를 보고 만든다.<br/>
/// - 개수는 heap에서 센다 (HeapStorage.CountObjects). 구슬 생성 스킬은 Malloc 뒤에 Refresh를 부르고, 해제·영역 비우기는 heap 이벤트로 받는다.<br/>
/// - Python은 가비지 컬렉션(LanguageRuleData.GarbageCollected)이라 끊긴 구슬 생성의 구슬은 누수되지 않고 바로 사라진다.<br/>
/// - 네트워크: 개수는 [SyncVar]로 클라이언트에 보낸다. 클라이언트의 Count는 서버가 보낸 값이고, Changed가 모든 화면에서 불린다 (구슬 그림, HUD).<br/>
/// - 구슬 자리(OrbPosition)는 캐릭터 위치·방향으로 계산해서 서버(구슬을 쏘는 스킬)와 각 화면(ManaOrbView)이 같은 자리를 쓴다.
/// </para>
/// </summary>
[RequireComponent(typeof(ProcessMemory))]
public class ManaOrbs : NetworkBehaviour
{
	/// <summary>마나 구슬 최대 개수</summary>
	public const int MaxCount = 5;

	[Header("구슬 자리: 머리 위 반원")]
	[Tooltip("반원 가운데가 머리 꼭대기(콜라이더 윗변)에서 올라간 높이 (유닛)")]
	[SerializeField] float arcAboveHead = 0.35f;
	[Tooltip("반원 반지름 (유닛)")]
	[SerializeField, Min(0f)] float arcRadius = 0.9f;
	[Tooltip("구슬 사이 각도 (도). 가운데 구슬은 바로 위, 양옆으로 이만큼씩 벌어진다")]
	[SerializeField, Range(0f, 60f)] float arcAngleStep = 32f;

	ProcessMemory memory;
	Collider2D body;

	// [서버 → 클라이언트] 구슬 수(다 쓴 구슬 포함)와 다 쓴 구슬 표시(i번째 비트 = i번째 구슬이 다 씀, #532). 서버만 바꾼다 (Refresh)
	// 클라이언트에는 구슬마다의 값이 없어서, 머리 위·HUD가 몇 번째 구슬이 다 쓴 것인지 알 수 있게 비트로 보낸다
	[SyncVar(hook = nameof(OnSyncedChanged))] int syncedCount;
	[SyncVar(hook = nameof(OnSyncedChanged))] int syncedSpentMask;
	// 서버(Host, 네트워크 없는 테스트)가 마지막으로 알린 값. 바뀔 때만 Changed를 내기 위함
	int lastCount;
	int lastSpentMask;

	// 클라이언트로만 접속한 화면이면 true. 이때 heap은 비어 있어 서버가 보낸 개수를 쓴다
	static bool IsClientOnly => NetworkClient.active && !NetworkServer.active;

	// heap을 읽을 수 없으면(클라이언트) 서버가 보낸 값을 쓴다
	bool UsesSynced => IsClientOnly || memory == null || memory.Heap == null;

	/// <summary>
	/// heap에 있는 마나 구슬 수. 다 쓴 구슬도 센다 (#532). 최대 5개 제한, 머리 위 그림, HUD 「마나 구슬 n/5」가 이 값을 쓴다. 클라이언트에서는 서버가 보낸 값이다.
	/// </summary>
	public int TotalCount => UsesSynced ? syncedCount : AllPointers().Count;

	/// <summary>지금 쓸 수 있는 마나 구슬 수(다 쓴 구슬 제외, #532). 찍을 수 있는 대상 수(SlotCaster)가 쓴다. 클라이언트에서는 서버가 보낸 값으로 센다.</summary>
	public int Count => TotalCount - SpentCount;

	/// <summary>다 쓴 구슬 수 (#532).</summary>
	public int SpentCount
	{
		get
		{
			int spent = 0;
			for (int mask = SpentMask; mask != 0; mask &= mask - 1) spent++;
			return spent;
		}
	}

	/// <summary>
	/// index번째 구슬(heap 주소 순서, OrbPosition과 같은 순서)이 다 쓴 구슬이면 true (#532). 머리 위 그림과 HUD 칸 색이 쓴다.
	/// </summary>
	public bool IsSpentAt(int index) => index >= 0 && index < 32 && (SpentMask & (1 << index)) != 0;

	int SpentMask => UsesSynced ? syncedSpentMask : ComputeSpentMask();

	// [서버] heap의 구슬들을 주소 순서로 보며 다 쓴 구슬의 자리에 비트를 켠다
	int ComputeSpentMask()
	{
		int mask = 0;
		System.Collections.Generic.List<int> pointers = AllPointers();
		for (int i = 0; i < pointers.Count && i < 32; i++)
			if (memory.Heap.TryGet(pointers[i], out ManaOrb orb) && orb.IsSpent) mask |= 1 << i;
		return mask;
	}

	// [서버] heap의 모든 구슬(다 쓴 구슬 포함)의 포인터. 주소 순서
	System.Collections.Generic.List<int> AllPointers() =>
		memory == null || memory.Heap == null ? new System.Collections.Generic.List<int>() : memory.Heap.GetPointers<ManaOrb>();

	/// <summary>
	/// [서버] 쓸 수 있는 구슬(다 쓰지 않은 구슬)의 heap 포인터. heap 주소 순서 (#532).
	/// </summary>
	public System.Collections.Generic.List<int> UsablePointers()
	{
		var usable = new System.Collections.Generic.List<int>();
		if (memory == null || memory.Heap == null) return usable;
		foreach (int pointer in AllPointers())
			if (memory.Heap.TryGet(pointer, out ManaOrb orb) && !orb.IsSpent) usable.Add(pointer);
		return usable;
	}

	/// <summary>
	/// [서버] 포인터 pointer인 구슬의 자리(머리 위 반원의 몇 번째, 다 쓴 구슬 포함). heap에 없는 구슬이면 -1 (#532).
	/// 다 쓴 구슬도 자리를 지키므로, 구슬을 써도 다른 구슬이 움직이지 않는다.
	/// </summary>
	public int IndexOf(int pointer) => AllPointers().IndexOf(pointer);

	/// <summary>더 만들 수 있으면 true (다 쓴 구슬까지 세어 최대 5개).</summary>
	public bool CanCreate => TotalCount < MaxCount;

	/// <summary>[표시] 구슬 수나 다 쓴 구슬이 바뀌었을 때. 구슬 그림(ManaOrbView)이 TotalCount와 IsSpentAt을 다시 읽는다. 모든 화면에서 불린다.</summary>
	public event Action Changed;

	void Awake()
	{
		memory = GetComponent<ProcessMemory>();
		body = GetComponent<Collider2D>();
	}

	// heap은 ProcessMemory.Awake에서 만들어지므로 Start에서 구독한다 (컴포넌트 순서와 상관없게)
	void Start()
	{
		if (IsClientOnly) return;
		memory.Heap.ObjectFreed += OnHeapObjectGone;
		memory.Heap.ObjectCleared += OnHeapObjectGone;
		memory.Heap.ObjectLeaked += OnHeapObjectGone;
	}

	void OnDestroy()
	{
		if (memory == null || memory.Heap == null) return;
		memory.Heap.ObjectFreed -= OnHeapObjectGone;
		memory.Heap.ObjectCleared -= OnHeapObjectGone;
		memory.Heap.ObjectLeaked -= OnHeapObjectGone;
	}

	void OnHeapObjectGone(object obj)
	{
		if (obj is ManaOrb) Refresh();
	}

	/// <summary>
	/// [서버] heap의 구슬 수와 다 쓴 구슬을 다시 세어 알린다. 구슬 스킬이 구슬을 다 쓴 구슬로 바꾼 뒤에도 부른다. 구슬 생성 스킬이 Malloc에 성공한 뒤 부른다 (heap은 올릴 때 이벤트를 내지 않기 때문).<br/>
	/// 해제·영역 비우기는 이 컴포넌트가 heap 이벤트를 받아 스스로 부른다. 개수가 그대로면 아무 일도 없다.
	/// </summary>
	public void Refresh()
	{
		if (IsClientOnly) return;
		int count = TotalCount;
		int spentMask = SpentMask;
		if (count == lastCount && spentMask == lastSpentMask) return;
		lastCount = count;
		lastSpentMask = spentMask;
		if (NetworkServer.active)
		{
			syncedCount = count;
			syncedSpentMask = spentMask;
		}
		Changed?.Invoke();
	}

	// [클라이언트] 서버가 보낸 구슬 수나 다 쓴 구슬 표시가 바뀜. 서버·Host는 Refresh에서 이미 알렸다
	void OnSyncedChanged(int _, int __)
	{
		if (isServer) return;
		Changed?.Invoke();
	}

	/// <summary>
	/// index번째 구슬의 월드 자리 (0부터, heap 주소 순서). count는 지금 구슬 수(TotalCount, 다 쓴 구슬 포함).<br/>
	/// 머리 위 반원에 고르게 놓는다. 서버의 구슬 스킬(쏘는 자리)과 각 화면의 ManaOrbView(그림 자리)가 같이 쓴다.
	/// </summary>
	public Vector2 OrbPosition(int index, int count)
	{
		Bounds b = body != null ? body.bounds : new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
		Vector2 center = new Vector2(b.center.x, b.max.y + arcAboveHead - arcRadius);
		// 가운데가 90도(바로 위). 구슬이 늘면 양옆으로 벌어진다
		float angle = 90f + (index - (Mathf.Max(1, count) - 1) * 0.5f) * arcAngleStep;
		float rad = angle * Mathf.Deg2Rad;
		return center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * arcRadius;
	}
}
