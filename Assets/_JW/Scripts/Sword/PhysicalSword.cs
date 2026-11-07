using System;
using Mirror;
using UnityEngine;

/// <summary>물리 검(손에 든 레이피어)의 상태</summary>
public enum SwordState
{
	InHand,   // 손에 있음
	Flying,   // 날아가는 중
	OnGround, // 땅에 떨어짐
}

/// <summary>
/// 소환검을 던진 순서를 매기는 번호표 (#167). 검 돌아오기가 가장 먼저 던진 소환검을 고를 때 쓴다. 물리 검은 검 돌아오기로 돌아오지 않는다 (#232).
/// </summary>
public static class SwordThrowOrder
{
	static int last;

	/// <summary>다음 번호. 던질 때마다 1씩 커진다.</summary>
	public static int Next() => ++last;
}

/// <summary>
/// 손에 든 물리 검의 상태를 관리하는 컴포넌트. 플레이어에 붙인다 (노션 「손에 든 검 던지기·줍기·맨손 상태 관리하기」, #69).
/// <para>
/// - 상태는 이 컴포넌트만 바꾼다. 다른 스크립트는 Throw, TryPickUp, Recall을 부른다.<br/>
/// - 던지면 맞든 빗나가든 땅에 떨어지고, Z로 주울 때까지 맨손(IsUnarmed)이다. 검 돌아오기 스킬로는 돌아오지 않는다 (#232).<br/>
/// - 대상이 있으면 포물선으로 날아간다.<br/>
/// - 떨어진 검 오브젝트는 SpawnDroppedSword / DespawnDroppedSword로만 만들고 없앤다 (CLAUDE.md 코드 작성 규칙 3).<br/>
/// - 물리 검은 해제(free)할 수 없다. 해제는 소환검만 대상으로 한다.
/// </para>
/// <para>
/// 하지 않는 일: 검 던지기 스킬의 데미지 판정(던지기 스킬 이슈)
/// </para>
/// <para>
/// 네트워크 (#229): 상태는 서버만 바꾸고 [SyncVar]로 클라이언트에 보낸다. 클라이언트에서도 State, IsUnarmed가 맞고 StateChanged가 불린다 (손 그림, 맨손 표시).<br/>
/// 떨어진 검의 위치는 DroppedSword 프리팹의 NetworkTransform이 맞춘다. 줍기는 SwordPickUpInput이 [Command]로 서버에 요청한다.
/// </para>
/// </summary>
public class PhysicalSword : NetworkBehaviour
{
	[Header("떨어진 검")]
	[Tooltip("던졌을 때 만들어지는 떨어진 검 프리팹")]
	[SerializeField] DroppedSword droppedSwordPrefab;
	[Tooltip("검이 떨어질 바닥 레이어 (Ground, Platform)")]
	[SerializeField] LayerMask groundLayers;

	[Header("던지기")]
	[Tooltip("던지는 위치. 캐릭터 발밑 기준, x는 바라보는 방향으로 뒤집는다")]
	[SerializeField] Vector2 handOffset = new Vector2(0.33f, 1.33f);
	[Tooltip("날아가는 속도 (유닛/초)")]
	[SerializeField, Min(0.1f)] float throwSpeed = 20f;
	[Tooltip("이 시간 안에 대상에 닿지 못하면 빗나간 것으로 보고 떨어진다 (초)")]
	[SerializeField, Min(0.1f)] float maxFlightTime = 1f;

	[Header("줍기")]
	[Tooltip("떨어진 검과 이 거리 안에 있으면 주울 수 있다 (유닛)")]
	[SerializeField, Min(0f)] float pickUpRange = 1.5f;

	PlayerMovement movement;
	DroppedSword dropped;

	/// <summary>지금 상태. 처음에는 손에 있다. 클라이언트에서는 서버가 보낸 값이다 (#229).</summary>
	public SwordState State { get; private set; } = SwordState.InHand;

	// [SyncVar] 서버의 State를 클라이언트에 보낸다 (#229). 서버에서는 SetState만 바꾼다
	[SyncVar(hook = nameof(OnSyncedState))] SwordState syncedState = SwordState.InHand;

	/// <summary>
	/// 맨손이면 true (날아가는 중, 땅에 떨어짐). 맨손이면 공격 스킬을 쓸 수 없다 (DataDrivenSkill이 읽는다).
	/// </summary>
	public bool IsUnarmed => State != SwordState.InHand;

	/// <summary>떨어진(또는 날아가는) 검 오브젝트. 손에 있으면 null. 위치 표시에 쓴다.</summary>
	public DroppedSword Dropped => dropped;

	/// <summary>지금 주울 수 있으면 true (땅에 떨어져 있고, 줍기 거리 안).</summary>
	public bool CanPickUp => State == SwordState.OnGround && dropped != null
		&& Vector2.Distance(dropped.transform.position, transform.position) <= pickUpRange;

	/// <summary>[표시] 상태가 바뀌었을 때 불린다. 손 그림, 맨손 표시에 쓴다.</summary>
	public event Action<SwordState> StateChanged;

	/// <summary>
	/// [로직] 던진 검이 대상에 맞았을 때 불린다 (맞은 대상). 데미지는 이 이벤트를 받는 쪽(검 던지기 스킬)이 준다.
	/// </summary>
	public event Action<GameObject> Hit;

	/// <summary>검을 던지는 손 위치 (발밑 + Hand Offset, 바라보는 방향으로 뒤집음)</summary>
	public Vector2 HandPosition
	{
		get
		{
			int facing = movement != null ? movement.Facing : 1;
			return (Vector2)transform.position + new Vector2(handOffset.x * facing, handOffset.y);
		}
	}

	void Reset()
	{
		groundLayers = LayerMask.GetMask("Ground", "Platform");
	}

	void Awake()
	{
		movement = GetComponent<PlayerMovement>();
	}

	/// <summary>
	/// 검을 던진다. 손에 없으면 false.<br/>
	/// target이 있으면 대상을 향해 날아가 맞은 자리에서 떨어진다. 대상이 없거나(빗나감) 도중에 사라지면 그 자리에서 바로 떨어진다.
	/// </summary>
	public bool Throw(GameObject target)
	{
		if (State != SwordState.InHand) return false;
		if (droppedSwordPrefab == null)
		{
			Debug.LogError($"[PhysicalSword] {name}: Dropped Sword Prefab이 비어 있어 던지지 못함");
			return false;
		}

		dropped = SpawnDroppedSword(HandPosition);
		// 만들지 못했으면(프리팹 설정 오류, 클라이언트에서 부름) 던지지 않는다. 원인은 NetworkSpawner가 로그로 남긴다
		if (dropped == null) return false;
		dropped.Launch(this, target, throwSpeed, maxFlightTime, groundLayers);
		SetState(SwordState.Flying);
		return true;
	}

	/// <summary>
	/// 근처에 떨어진 검을 줍는다. 주울 수 없으면 false. 줍기 키를 눌렀을 때 부른다.
	/// </summary>
	public bool TryPickUp()
	{
		if (!CanPickUp) return false;
		ReturnToHand();
		return true;
	}

	/// <summary>
	/// 거리와 상관없이 검을 바로 손으로 되돌린다. 이미 손에 있으면 false.<br/>
	/// 스킬은 쓰지 않는다 (플레이어는 Z로만 줍는다, #232). 테스트나 장소 이동처럼 시스템이 검을 정리할 때 쓴다.
	/// </summary>
	public bool Recall()
	{
		if (State == SwordState.InHand) return false;
		ReturnToHand();
		return true;
	}

	// DroppedSword가 대상에 닿았을 때 부른다
	internal void NotifyHit(GameObject target)
	{
		Hit?.Invoke(target);
	}

	// DroppedSword가 땅에 닿았을 때 부른다
	internal void NotifyLanded(DroppedSword sword)
	{
		if (sword != dropped || State != SwordState.Flying) return;
		SetState(SwordState.OnGround);
	}

	void ReturnToHand()
	{
		DespawnDroppedSword(dropped);
		dropped = null;
		SetState(SwordState.InHand);
	}

	void SetState(SwordState state)
	{
		if (State == state) return;
		State = state;
		if (isServer) syncedState = state;
		StateChanged?.Invoke(state);
	}

	// [클라이언트] 서버가 보낸 상태 (#229). Host는 서버 쪽 값을 그대로 쓴다
	void OnSyncedState(SwordState _, SwordState state)
	{
		if (isServer || State == state) return;
		State = state;
		StateChanged?.Invoke(state);
	}

	// 떨어진 검은 이 두 함수로만 만들고 없앤다 (코드 작성 규칙 3)
	// NetworkSpawner: 내 장소 씬에 만들고, 서버면 모든 클라이언트에 보인다 (place-scenes.md)
	DroppedSword SpawnDroppedSword(Vector2 position)
	{
		GameObject obj = NetworkSpawner.Spawn(droppedSwordPrefab.gameObject, position, Quaternion.identity, gameObject.scene);
		return obj != null ? obj.GetComponent<DroppedSword>() : null;
	}

	void DespawnDroppedSword(DroppedSword sword)
	{
		if (sword != null) NetworkSpawner.Despawn(sword.gameObject);
	}

	// 캐릭터가 사라지면 떨어진 검도 남기지 않는다
	// 클라이언트에서는 서버가 없애므로 부르지 않는다 (NetworkSpawner는 서버에서만 부름)
	void OnDestroy()
	{
		if (Mirror.NetworkClient.active && !Mirror.NetworkServer.active) return;
		DespawnDroppedSword(dropped);
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, pickUpRange);
	}
}
