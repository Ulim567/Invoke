using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>소환검 하나의 상태</summary>
public enum SummonedSwordState
{
	Ready,   // 사용 가능: 플레이어 곁에 떠 있음
	Flying,  // 던져져서 날아가는 중
	Dropped, // 땅에 떨어짐 (회수해야 다시 씀)
	Leaked,  // 누수: 포인터를 잃어 그 자리에 멈춤. 힙과 개수는 계속 차지
	Stabbing, // 소환검 찌르기: 대상에게 날아가 찌르고 돌아오는 중 (#165)
	Returning, // 검 돌아오기: 곁으로 날아오는 중 (#167)
	Dancing,  // 검무: 대상 주위를 도는 중 (#243)
	Riding,   // 검 타기: 플레이어 발밑에 붙어 있음 (#256)
}

/// <summary>
/// 소환검 오브젝트 (소환검 프리팹에 붙는다). 움직임만 맡는다.
/// <para>
/// 만들기·없애기, 포인터, 상태 변경은 SummonedSwords(플레이어)가 맡는다. 이 컴포넌트는 SummonedSwords가 정한 상태대로 움직이고, 땅에 닿으면 SummonedSwords에 알린다.<br/>
/// 그림(누수 반투명 등)은 SummonedSwordView가 StateChanged를 받아서 바꾼다.<br/>
/// - 사용 가능: 플레이어 머리 뒤쪽의 자기 자리로 따라간다.<br/>
/// - 날아가는 중 (#167): 던진 순간의 방향으로 일직선으로 날아간다. 한쪽 방향 발판(Platform)은 통과한다. 대상에 닿으면 알리고 몬스터 몸에 꽂혀 따라다닌다(몬스터가 죽으면 떨어짐). 벽·땅에 닿으면 거기 꽂힌다. 최대 거리를 넘으면 떨어진다. 대상 없이 던지면 바로 떨어진다.<br/>
/// - 돌아오는 중 (#167): 곁으로 날아오며 지나가는 몬스터를 알리고, 닿으면 알린다.<br/>
/// - 떨어짐·누수: 그 자리에 있다.<br/>
/// - 찌르는 중: 대상에게 날아가 닿으면 SummonedSwords에 알리고, 자기 자리로 돌아와 닿으면 알린다 (#165).<br/>
/// 물리(Rigidbody)를 쓰지 않고 직접 움직인다 (DroppedSword와 같은 방식).
/// </para>
/// <para>
/// 네트워크 (#229): 움직임은 서버에서만 한다. 위치·회전은 프리팹의 NetworkTransform이 클라이언트에 맞춘다.<br/>
/// 포인터, 상태, 주인 플레이어는 [SyncVar]로 클라이언트에 보낸다. 클라이언트에서는 주인의 SummonedSwords 목록에 스스로 들어가고, 상태가 바뀌면 StateChanged가 불린다 (HUD, 누수 반투명).
/// </para>
/// </summary>
public class SummonedSword : NetworkBehaviour
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
	[Tooltip("소환검 찌르기에서 날아가고 돌아오는 속도 (유닛/초, 임시)")]
	[SerializeField, Min(0.1f)] float stabSpeed = 35f;
	[Tooltip("던졌을 때 일직선으로 날아가는 최대 거리. 넘으면 떨어진다 (유닛, 임시)")]
	[SerializeField, Min(0.1f)] float maxThrowDistance = 14f;
	[Tooltip("검 돌아오기에서 곁으로 날아오는 속도 (유닛/초, 임시)")]
	[SerializeField, Min(0.1f)] float returnSpeed = 30f;
	[Tooltip("표식 남기기에서 대상 옆 이 거리 지점까지 간 뒤 안쪽으로 찌른다 (유닛, 임시)")]
	[SerializeField, Min(0.1f)] float stabSideDistance = 1.5f;
	[Tooltip("검무에서 대상 주위를 도는 반지름 (유닛, 임시)")]
	[SerializeField, Min(0.1f)] float danceRadius = 1.2f;
	[Tooltip("검무에서 도는 빠르기 (도/초, 임시)")]
	[SerializeField] float danceDegreesPerSecond = 540f;
	[Tooltip("돌아오는 동안 몬스터를 베는 판정 반지름 (유닛, 임시)")]
	[SerializeField, Min(0.05f)] float returnHitRadius = 0.6f;

	SummonedSwords owner;
	GameObject target;
	float flightEndTime;
	LayerMask groundLayers;
	bool falling;
	float fallSpeed;
	float landY;
	bool returning;
	Vector2 throwDirection;
	float travelled;
	Monster stuckTo;
	Vector2 stuckOffset;
	// 표식 남기기 (#243): 찌르기 전에 들를 옆쪽(-1 왼쪽, 1 오른쪽, 0 바로 찌름)과 들렀는지
	int stabSide;
	bool sideReached;
	// 검무 (#243): 대상 둘레의 지금 각도 (도)
	float danceAngle;
	readonly HashSet<Monster> returnHits = new HashSet<Monster>();

	/// <summary>힙 포인터 (번호표). 해제되면 더 이상 쓸 수 없다. 클라이언트에서는 서버가 보낸 값이다 (#229).</summary>
	public int Pointer { get; private set; }

	/// <summary>지금 상태. 클라이언트에서는 서버가 보낸 값이다 (#229).</summary>
	public SummonedSwordState State { get; private set; } = SummonedSwordState.Ready;

	// [SyncVar] 서버의 Pointer, State, 주인을 클라이언트에 보낸다 (#229). 서버에서는 Init, SetState만 바꾼다
	// 주인을 마지막에 둔다: 한 번에 같이 오면 포인터·상태가 먼저 들어간 뒤 주인 목록에 들어가게 하기 위함
	[SyncVar(hook = nameof(OnSyncedPointer))] int syncedPointer;
	[SyncVar(hook = nameof(OnSyncedState))] SummonedSwordState syncedState = SummonedSwordState.Ready;
	[SyncVar(hook = nameof(OnSyncedOwner))] NetworkIdentity syncedOwner;

	// [클라이언트] 들어가 있는 주인 목록
	SummonedSwords clientOwner;

	/// <summary>마지막으로 던진 순서 (SwordThrowOrder). 검 돌아오기가 가장 먼저 던진 검을 고를 때 읽는다 (#167)</summary>
	public int ThrowOrder { get; private set; }

	/// <summary>몬스터 몸에 꽂혀 있으면 그 몬스터. 아니면 null (#167)</summary>
	public Monster StuckTo => stuckTo;

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
		if (isServer)
		{
			syncedPointer = pointer;
			syncedOwner = owner.GetComponent<NetworkIdentity>();
		}
	}

	internal void SetReady()
	{
		falling = false;
		stuckTo = null;
		transform.rotation = Quaternion.identity;
		SetState(SummonedSwordState.Ready);
	}

	// 대상 쪽으로 일직선으로 던진다. 대상이 없으면 바로 떨어진다
	internal void Launch(GameObject target)
	{
		if (target == null) { LaunchStraight(null, Vector2.zero); return; }
		LaunchStraight(target, AimPoint(target) - (Vector2)transform.position);
	}

	// 방향으로 일직선으로 던진다. target이 있으면 그 대상에만 맞는다. 방향이 없으면 바로 떨어진다
	internal void LaunchStraight(GameObject target, Vector2 direction)
	{
		this.target = target;
		throwDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.zero;
		travelled = 0f;
		stuckTo = null;
		falling = false;
		ThrowOrder = SwordThrowOrder.Next();
		SetState(SummonedSwordState.Flying);
		if (throwDirection == Vector2.zero) StartFalling();
	}

	internal void LaunchReturn()
	{
		stuckTo = null;
		falling = false;
		returnHits.Clear();
		SetState(SummonedSwordState.Returning);
	}

	internal void LaunchStab(GameObject target, int side = 0)
	{
		stuckTo = null;
		this.target = target;
		stabSide = side;
		sideReached = side == 0;
		flightEndTime = Time.time + maxFlightTime;
		returning = false;
		falling = false;
		SetState(SummonedSwordState.Stabbing);
	}

	// 검무 (#243): 대상 둘레를 startAngle부터 돈다. 끝내는 일은 SummonedSwords.EndDance(SetReady)
	internal void LaunchDance(GameObject target, float startAngle)
	{
		stuckTo = null;
		falling = false;
		this.target = target;
		danceAngle = startAngle;
		SetState(SummonedSwordState.Dancing);
	}

	// 검 타기가 끝나는 시각 (#256). SummonedSwords가 이 시각에 EndRide한다
	internal float RideEndTime { get; private set; }

	// 검 타기 (#256): 플레이어 발밑에 붙는다. 끝내는 일은 SummonedSwords.EndRide(SetReady)
	internal void LaunchRide(float seconds)
	{
		RideEndTime = Time.time + seconds;
		stuckTo = null;
		falling = false;
		transform.rotation = Quaternion.identity;
		SetState(SummonedSwordState.Riding);
	}

	internal void SetDropped()
	{
		falling = false;
		SetState(SummonedSwordState.Dropped);
	}

	internal void SetLeaked()
	{
		falling = false;
		stuckTo = null;
		SetState(SummonedSwordState.Leaked);
	}

	void SetState(SummonedSwordState state)
	{
		State = state;
		if (isServer) syncedState = state;
		StateChanged?.Invoke(state);
	}

	// [클라이언트] 서버가 보낸 값 (#229). Host는 서버 쪽 값을 그대로 쓰므로 하지 않는다
	void OnSyncedPointer(int _, int pointer)
	{
		if (isServer) return;
		Pointer = pointer;
		if (clientOwner != null) clientOwner.ClientSwordChanged(this);
	}

	void OnSyncedState(SummonedSwordState _, SummonedSwordState state)
	{
		if (isServer) return;
		State = state;
		StateChanged?.Invoke(state);
		if (clientOwner != null) clientOwner.ClientSwordChanged(this);
	}

	void OnSyncedOwner(NetworkIdentity _, NetworkIdentity identity)
	{
		if (isServer) return;
		JoinClientOwner(identity);
	}

	// 이미 주인이 정해진 채로 처음 보일 때 (늦게 들어온 클라이언트)
	public override void OnStartClient()
	{
		if (isServer) return;
		Pointer = syncedPointer;
		State = syncedState;
		StateChanged?.Invoke(State);
		JoinClientOwner(syncedOwner);
	}

	public override void OnStopClient()
	{
		if (isServer) return;
		if (clientOwner != null) clientOwner.ClientRemove(this);
		clientOwner = null;
	}

	void JoinClientOwner(NetworkIdentity identity)
	{
		SummonedSwords next = identity != null ? identity.GetComponent<SummonedSwords>() : null;
		if (next == clientOwner) return;
		if (clientOwner != null) clientOwner.ClientRemove(this);
		clientOwner = next;
		if (clientOwner != null) clientOwner.ClientAdd(this);
	}

	void Update()
	{
		// 클라이언트에서는 움직이지 않는다. 위치는 NetworkTransform이 서버에 맞춘다 (#229)
		if (isClientOnly) return;
		switch (State)
		{
			case SummonedSwordState.Ready:
				transform.position = Vector2.Lerp(transform.position, FollowPoint, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
				break;
			case SummonedSwordState.Flying:
				if (falling) Fall(); else Fly();
				break;
			case SummonedSwordState.Dropped:
				FollowStuck();
				if (falling) Fall();
				break;
			case SummonedSwordState.Returning:
				Return();
				break;
			case SummonedSwordState.Stabbing:
				Stab();
				break;
			case SummonedSwordState.Dancing:
				Dance();
				break;
			case SummonedSwordState.Riding:
				// 플레이어 발밑으로 바짝 따라간다
				if (owner != null) transform.position = Vector2.MoveTowards(transform.position, owner.RidePoint, stabSpeed * Time.deltaTime);
				break;
		}
	}

	// 던진 방향으로 일직선. 벽·땅에 닿으면 꽂히고, 대상에 닿으면 몸에 꽂힌다
	void Fly()
	{
		Vector2 pos = transform.position;
		float step = throwSpeed * Time.deltaTime;
		transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(throwDirection.y, throwDirection.x) * Mathf.Rad2Deg);

		// 내가 속한 장소 씬의 물리 세계에서 벽·땅을 찾는다 (place-scenes.md). 한쪽 방향 발판(Platform)은 통과한다
		int wallLayers = groundLayers & ~LayerMask.GetMask("Platform");
		RaycastHit2D wall = gameObject.scene.GetPhysicsScene2D().Raycast(pos, throwDirection, step, wallLayers);
		if (wall.collider != null)
		{
			transform.position = wall.point;
			owner?.NotifyLanded(this);
			return;
		}

		Vector2 next = pos + throwDirection * step;
		transform.position = next;
		travelled += step;

		if (target != null && target.activeInHierarchy)
		{
			Collider2D col = target.GetComponentInChildren<Collider2D>();
			Vector2 closest = col != null ? col.ClosestPoint(next) : (Vector2)target.transform.position;
			if (Vector2.Distance(closest, next) <= hitDistance)
			{
				Monster monster = target.GetComponentInParent<Monster>();
				owner?.NotifyHit(this, target);
				if (monster != null && !monster.IsDead)
				{
					stuckTo = monster;
					stuckOffset = next - (Vector2)monster.transform.position;
				}
				owner?.NotifyLanded(this);
				if (stuckTo == null) StartFalling();
				return;
			}
		}

		if (travelled >= maxThrowDistance) StartFalling();
	}

	// 몬스터 몸에 꽂혀 있으면 따라다닌다. 몬스터가 죽거나 사라지면 떨어진다
	void FollowStuck()
	{
		if (stuckTo == null && !ReferenceEquals(stuckTo, null)) { stuckTo = null; StartFalling(); return; }
		if (stuckTo == null) return;
		if (stuckTo.IsDead || !stuckTo.gameObject.activeInHierarchy)
		{
			stuckTo = null;
			StartFalling();
			return;
		}
		transform.position = (Vector2)stuckTo.transform.position + stuckOffset;
	}

	// 검 돌아오기: 곁으로 날아오며 지나가는 몬스터를 한 번씩 알린다
	void Return()
	{
		if (owner == null) return;
		Vector2 aim = owner.ReturnPoint;
		Vector2 next = Vector2.MoveTowards(transform.position, aim, returnSpeed * Time.deltaTime);
		Vector2 dir = next - (Vector2)transform.position;
		if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
		transform.position = next;

		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(LayerMask.GetMask("Monster"));
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		gameObject.scene.GetPhysicsScene2D().OverlapCircle(next, returnHitRadius, filter, hits);
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead || !returnHits.Add(monster)) continue;
			owner.NotifyReturnHit(this, monster.gameObject);
		}

		if (Vector2.Distance(next, aim) <= hitDistance) owner.NotifyReturned(this);
	}

	static Vector2 AimPoint(GameObject target)
	{
		Collider2D col = target.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
	}

	// 대상에게 날아가 찌르고(닿으면 알림) 자기 자리(FollowPoint)로 돌아온다
	void Stab()
	{
		if (!returning && (target == null || !target.activeInHierarchy || Time.time >= flightEndTime)) returning = true;

		Vector2 aim;
		bool toSide = false;
		if (returning) aim = FollowPoint;
		else
		{
			Collider2D col = target.GetComponentInChildren<Collider2D>();
			aim = col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
			// 표식 남기기: 먼저 대상 옆으로 가서 안쪽으로 찌른다 (#243)
			if (!sideReached)
			{
				aim += new Vector2(stabSide * stabSideDistance, 0f);
				toSide = true;
			}
		}

		Vector2 next = Vector2.MoveTowards(transform.position, aim, stabSpeed * Time.deltaTime);
		Vector2 dir = next - (Vector2)transform.position;
		if (dir.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
		transform.position = next;

		if (Vector2.Distance(next, aim) > hitDistance) return;
		if (toSide)
		{
			sideReached = true;
			return;
		}
		if (!returning)
		{
			returning = true;
			owner?.NotifyStabHit(this, target);
		}
		else
		{
			// 상태는 SummonedSwords가 바꾼다
			owner?.NotifyStabReturned(this);
		}
	}

	// 검무 (#243): 대상 둘레를 돌며 칼끝이 대상 쪽을 본다. 대상이 사라지면 그 자리에 떠 있다 (스킬이 끝나면 곁으로)
	void Dance()
	{
		if (target == null || !target.activeInHierarchy) return;
		Collider2D col = target.GetComponentInChildren<Collider2D>();
		Vector2 center = col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
		danceAngle += danceDegreesPerSecond * Time.deltaTime;
		float rad = danceAngle * Mathf.Deg2Rad;
		Vector2 point = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * danceRadius;
		transform.position = Vector2.MoveTowards(transform.position, point, stabSpeed * Time.deltaTime);
		Vector2 toCenter = center - (Vector2)transform.position;
		if (toCenter.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg);
	}

	void StartFalling()
	{
		falling = true;
		fallSpeed = 0f;
		transform.rotation = Quaternion.Euler(0f, 0f, -90f);
		// 내가 속한 장소 씬의 물리 세계에서 땅을 찾는다 (place-scenes.md)
		RaycastHit2D ground = gameObject.scene.GetPhysicsScene2D().Raycast(transform.position, Vector2.down, 100f, groundLayers);
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
