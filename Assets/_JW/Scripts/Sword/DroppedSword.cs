using UnityEngine;

/// <summary>
/// 던진 물리 검 오브젝트 (떨어진 검 프리팹에 붙는다). 날아가다가 대상에 맞거나 빗나가면 아래로 떨어져 바닥에 멈춘다.
/// <para>
/// 만들고 없애는 일, 상태(손에 있음/날아가는 중/땅에 떨어짐)는 PhysicalSword가 맡는다. 이 컴포넌트는 움직임만 한다.<br/>
/// 물리(Rigidbody)를 쓰지 않고 직접 움직인다. 플레이어·몬스터와 부딪혀 밀리지 않게 하기 위함이다.
/// </para>
/// </summary>
public class DroppedSword : MonoBehaviour
{
	[Tooltip("떨어질 때 중력 가속도 (유닛/초²)")]
	[SerializeField, Min(0f)] float fallGravity = 40f;
	[Tooltip("이 거리 안으로 들어오면 대상에 맞은 것으로 본다 (유닛)")]
	[SerializeField, Min(0.01f)] float hitDistance = 0.4f;

	enum Phase { Idle, Flying, Falling, Landed }

	PhysicalSword owner;
	GameObject target;
	float speed;
	float flightEndTime;
	LayerMask groundLayers;
	Phase phase = Phase.Idle;
	float fallSpeed;
	float landY;

	/// <summary>땅에 떨어져 멈췄으면 true.</summary>
	public bool IsLanded => phase == Phase.Landed;

	// PhysicalSword.Throw만 부른다
	internal void Launch(PhysicalSword owner, GameObject target, float speed, float maxFlightTime, LayerMask groundLayers)
	{
		this.owner = owner;
		this.target = target;
		this.speed = speed;
		this.groundLayers = groundLayers;
		flightEndTime = Time.time + maxFlightTime;

		// 대상이 없으면 빗나간 것: 바로 떨어진다
		if (target == null) StartFalling();
		else phase = Phase.Flying;
	}

	void Update()
	{
		switch (phase)
		{
			case Phase.Flying: Fly(); break;
			case Phase.Falling: Fall(); break;
		}
	}

	void Fly()
	{
		// 날아가는 사이 대상이 죽거나 사라졌으면, 또는 시간 안에 닿지 못하면 빗나감
		if (target == null || !target.activeInHierarchy || Time.time >= flightEndTime)
		{
			StartFalling();
			return;
		}

		Vector2 aim = AimPoint(target);
		Vector2 next = Vector2.MoveTowards(transform.position, aim, speed * Time.deltaTime);
		FaceTowards(next - (Vector2)transform.position);
		transform.position = next;

		if (Vector2.Distance(next, aim) <= hitDistance)
		{
			owner?.NotifyHit(target);
			StartFalling();
		}
	}

	void StartFalling()
	{
		phase = Phase.Falling;
		fallSpeed = 0f;
		transform.rotation = Quaternion.Euler(0f, 0f, -90f); // 칼끝이 아래로
		RaycastHit2D ground = Physics2D.Raycast(transform.position, Vector2.down, 100f, groundLayers);
		// 아래에 바닥이 없으면 멀리까지 떨어진 뒤 멈춘다
		landY = ground.collider != null ? ground.point.y : transform.position.y - 100f;
	}

	void Fall()
	{
		fallSpeed += fallGravity * Time.deltaTime;
		float y = transform.position.y - fallSpeed * Time.deltaTime;
		if (y <= landY)
		{
			y = landY;
			phase = Phase.Landed;
		}
		transform.position = new Vector3(transform.position.x, y, transform.position.z);
		if (phase == Phase.Landed) owner?.NotifyLanded(this);
	}

	// 대상의 콜라이더 가운데를 노린다. 콜라이더가 없으면 위치
	static Vector2 AimPoint(GameObject target)
	{
		Collider2D collider = target.GetComponentInChildren<Collider2D>();
		return collider != null ? (Vector2)collider.bounds.center : (Vector2)target.transform.position;
	}

	void FaceTowards(Vector2 direction)
	{
		if (direction.sqrMagnitude < 0.0001f) return;
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		transform.rotation = Quaternion.Euler(0f, 0f, angle);
	}
}
