using System;
using UnityEngine;

/// <summary>소환검 하나의 상태</summary>
public enum SummonedSwordState
{
	Ready,   // 사용 가능: 플레이어 곁에 떠 있음
	Flying,  // 던져져서 날아가는 중
	Dropped, // 땅에 떨어짐 (회수해야 다시 씀)
	Leaked,  // 누수: 포인터를 잃어 그 자리에 멈춤. 힙과 개수는 계속 차지
}

/// <summary>
/// 소환검 오브젝트 (소환검 프리팹에 붙는다). 움직임만 맡는다.
/// <para>
/// 만들기·없애기, 포인터, 상태 변경은 SummonedSwords(플레이어)가 맡는다. 이 컴포넌트는 SummonedSwords가 정한 상태대로 움직이고, 땅에 닿으면 SummonedSwords에 알린다.<br/>
/// 그림(누수 반투명 등)은 SummonedSwordView가 StateChanged를 받아서 바꾼다.<br/>
/// - 사용 가능: 플레이어 머리 뒤쪽의 자기 자리로 따라간다.<br/>
/// - 날아가는 중: 대상을 향해 날아가 닿으면 그 자리에서 떨어진다. 대상이 없거나 시간 안에 못 닿으면 바로 떨어진다.<br/>
/// - 떨어짐·누수: 그 자리에 있다.<br/>
/// 물리(Rigidbody)를 쓰지 않고 직접 움직인다 (DroppedSword와 같은 방식).
/// </para>
/// </summary>
public class SummonedSword : MonoBehaviour
{
	[Tooltip("자기 자리로 따라가는 빠르기 (클수록 빨리 붙음)")]
	[SerializeField, Min(0.1f)] float followSharpness = 12f;
	[Tooltip("날아가는 속도 (유닛/초)")]
	[SerializeField, Min(0.1f)] float throwSpeed = 22f;
	[Tooltip("이 시간 안에 대상에 닿지 못하면 떨어진다 (초)")]
	[SerializeField, Min(0.1f)] float maxFlightTime = 1.2f;
	[Tooltip("떨어질 때 중력 가속도 (유닛/초²)")]
	[SerializeField, Min(0f)] float fallGravity = 40f;
	[Tooltip("이 거리 안으로 들어오면 대상에 맞은 것으로 본다 (유닛)")]
	[SerializeField, Min(0.01f)] float hitDistance = 0.4f;

	SummonedSwords owner;
	GameObject target;
	float flightEndTime;
	LayerMask groundLayers;
	bool falling;
	float fallSpeed;
	float landY;

	/// <summary>힙 포인터 (번호표). 해제되면 더 이상 쓸 수 없다.</summary>
	public int Pointer { get; private set; }

	/// <summary>지금 상태.</summary>
	public SummonedSwordState State { get; private set; } = SummonedSwordState.Ready;

	/// <summary>[표시] 상태가 바뀔 때. SummonedSwordView가 그림을 바꾼다.</summary>
	public event Action<SummonedSwordState> StateChanged;

	/// <summary>사용 가능 상태에서 따라갈 위치. SummonedSwords가 매 프레임 정한다.</summary>
	internal Vector2 FollowPoint { get; set; }

	// 아래 함수는 SummonedSwords만 부른다. 상태는 이 함수들로만 바뀐다
	internal void Init(SummonedSwords owner, int pointer, LayerMask groundLayers)
	{
		this.owner = owner;
		Pointer = pointer;
		this.groundLayers = groundLayers;
		SetState(SummonedSwordState.Ready);
	}

	internal void SetReady()
	{
		falling = false;
		transform.rotation = Quaternion.identity;
		SetState(SummonedSwordState.Ready);
	}

	internal void Launch(GameObject target)
	{
		this.target = target;
		flightEndTime = Time.time + maxFlightTime;
		SetState(SummonedSwordState.Flying);
		if (target == null) StartFalling();
	}

	internal void SetDropped()
	{
		falling = false;
		SetState(SummonedSwordState.Dropped);
	}

	internal void SetLeaked()
	{
		falling = false;
		SetState(SummonedSwordState.Leaked);
	}

	void SetState(SummonedSwordState state)
	{
		State = state;
		StateChanged?.Invoke(state);
	}

	void Update()
	{
		switch (State)
		{
			case SummonedSwordState.Ready:
				transform.position = Vector2.Lerp(transform.position, FollowPoint, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
				break;
			case SummonedSwordState.Flying:
				if (falling) Fall(); else Fly();
				break;
		}
	}

	void Fly()
	{
		if (target == null || !target.activeInHierarchy || Time.time >= flightEndTime)
		{
			StartFalling();
			return;
		}

		Collider2D col = target.GetComponentInChildren<Collider2D>();
		Vector2 aim = col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
		Vector2 next = Vector2.MoveTowards(transform.position, aim, throwSpeed * Time.deltaTime);
		Vector2 dir = next - (Vector2)transform.position;
		if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
		transform.position = next;

		if (Vector2.Distance(next, aim) <= hitDistance)
		{
			owner?.NotifyHit(this, target);
			StartFalling();
		}
	}

	void StartFalling()
	{
		falling = true;
		fallSpeed = 0f;
		transform.rotation = Quaternion.Euler(0f, 0f, -90f);
		RaycastHit2D ground = Physics2D.Raycast(transform.position, Vector2.down, 100f, groundLayers);
		landY = ground.collider != null ? ground.point.y : transform.position.y - 100f;
	}

	void Fall()
	{
		fallSpeed += fallGravity * Time.deltaTime;
		float y = transform.position.y - fallSpeed * Time.deltaTime;
		bool landed = y <= landY;
		if (landed) y = landY;
		transform.position = new Vector3(transform.position.x, y, transform.position.z);
		if (landed)
		{
			falling = false;
			// 상태는 SummonedSwords가 바꾼다
			if (owner != null) owner.NotifyLanded(this);
		}
	}
}
