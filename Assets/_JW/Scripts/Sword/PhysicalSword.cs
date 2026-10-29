using System;
using UnityEngine;

/// <summary>물리 검(손에 든 레이피어)의 상태</summary>
public enum SwordState
{
	InHand,   // 손에 있음
	Flying,   // 날아가는 중
	OnGround, // 땅에 떨어짐
}

/// <summary>
/// 손에 든 물리 검의 상태를 관리하는 컴포넌트. 플레이어에 붙인다 (노션 「손에 든 검 던지기·줍기·맨손 상태 관리하기」, #69).
/// <para>
/// - 상태는 이 컴포넌트만 바꾼다. 다른 스크립트는 Throw, TryPickUp, Recall을 부른다.<br/>
/// - 던지면 맞든 빗나가든 땅에 떨어지고, 줍거나 검 돌아오기로 회수할 때까지 맨손(IsUnarmed)이다.<br/>
/// - 떨어진 검 오브젝트는 SpawnDroppedSword / DespawnDroppedSword로만 만들고 없앤다 (CLAUDE.md 코드 작성 규칙 3).<br/>
/// - 물리 검은 해제(free)할 수 없다. 해제는 소환검만 대상으로 한다.
/// </para>
/// <para>
/// 하지 않는 일: 던지기·검 돌아오기 스킬의 데미지와 경로 판정(던지기 스킬 이슈), 네트워크 동기화(서버 동기화 이슈)
/// </para>
/// </summary>
public class PhysicalSword : MonoBehaviour
{
	[Header("떨어진 검")]
	[Tooltip("던졌을 때 만들어지는 떨어진 검 프리팹")]
	[SerializeField] DroppedSword droppedSwordPrefab;
	[Tooltip("검이 떨어질 바닥 레이어 (Ground, Platform)")]
	[SerializeField] LayerMask groundLayers;

	[Header("던지기")]
	[Tooltip("던지는 위치. 캐릭터 발밑 기준, x는 바라보는 방향으로 뒤집는다")]
	[SerializeField] Vector2 handOffset = new Vector2(0.5f, 2f);
	[Tooltip("날아가는 속도 (유닛/초)")]
	[SerializeField, Min(0.1f)] float throwSpeed = 20f;
	[Tooltip("이 시간 안에 대상에 닿지 못하면 빗나간 것으로 보고 떨어진다 (초)")]
	[SerializeField, Min(0.1f)] float maxFlightTime = 1f;

	[Header("줍기")]
	[Tooltip("떨어진 검과 이 거리 안에 있으면 주울 수 있다 (유닛)")]
	[SerializeField, Min(0f)] float pickUpRange = 1.5f;

	PlayerMovement movement;
	DroppedSword dropped;

	/// <summary>지금 상태. 처음에는 손에 있다.</summary>
	public SwordState State { get; private set; } = SwordState.InHand;

	/// <summary>
	/// 맨손이면 true (날아가는 중, 땅에 떨어짐). 맨손이면 공격 스킬을 쓸 수 없다 (DataDrivenSkill이 읽는다).
	/// </summary>
	public bool IsUnarmed => State != SwordState.InHand;

	/// <summary>떨어진(또는 날아가는) 검 오브젝트. 손에 있으면 null. 위치 표시, 검 돌아오기 경로에 쓴다.</summary>
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

		int facing = movement != null ? movement.Facing : 1;
		Vector2 hand = (Vector2)transform.position + new Vector2(handOffset.x * facing, handOffset.y);
		dropped = SpawnDroppedSword(hand);
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
	/// 거리와 상관없이 검을 손으로 되돌린다 (검 돌아오기). 이미 손에 있으면 false.<br/>
	/// 돌아오는 경로의 데미지와 연출은 검 돌아오기 스킬이 이 함수를 부르기 전에 처리한다.
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
		StateChanged?.Invoke(state);
	}

	// 떨어진 검은 이 두 함수로만 만들고 없앤다. 네트워크를 붙일 때 여기만 NetworkServer.Spawn / Destroy로 바꾼다
	DroppedSword SpawnDroppedSword(Vector2 position)
	{
		return Instantiate(droppedSwordPrefab, position, Quaternion.identity);
	}

	void DespawnDroppedSword(DroppedSword sword)
	{
		if (sword != null) Destroy(sword.gameObject);
	}

	// 캐릭터가 사라지면 떨어진 검도 남기지 않는다
	void OnDestroy()
	{
		DespawnDroppedSword(dropped);
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireSphere(transform.position, pickUpRange);
	}
}
