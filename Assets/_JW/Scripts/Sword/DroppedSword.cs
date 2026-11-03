using UnityEngine;

/// <summary>
/// 던진 물리 검 오브젝트 (떨어진 검 프리팹에 붙는다). 포물선으로 날아가 대상에 맞거나 빗나가면 아래로 떨어져 바닥에 멈춘다.
/// 손으로 날아서 돌아오지는 않는다 (Z로만 줍는다, #232).
/// <para>
/// 만들고 없애는 일, 상태(손에 있음/날아가는 중/땅에 떨어짐)는 PhysicalSword가 맡는다. 이 컴포넌트는 움직임만 한다.<br/>
/// 물리(Rigidbody)를 쓰지 않고 직접 움직인다. 플레이어·몬스터와 부딪혀 밀리지 않게 하기 위함이다.
/// </para>
/// </summary>
public class DroppedSword : MonoBehaviour
{
	[Tooltip("떨어질 때 중력 가속도 (유닛/초²)")]
	[SerializeField, Min(0f)] float fallGravity = 40f;
	[Tooltip("이 거리 안으로 들어오면 대상에 닿은 것으로 본다 (유닛)")]
	[SerializeField, Min(0.01f)] float hitDistance = 0.4f;
	[Tooltip("포물선 높이 = 거리 × 이 값 (임시)")]
	[SerializeField, Min(0f)] float arcHeightRatio = 0.25f;
	[Tooltip("포물선 높이 최대 (유닛, 임시)")]
	[SerializeField, Min(0f)] float maxArcHeight = 2f;

	enum Phase { Idle, Flying, Falling, Landed }

	PhysicalSword owner;
	GameObject target;
	float speed;
	float flightEndTime;
	LayerMask groundLayers;
	Phase phase = Phase.Idle;
	float fallSpeed;
	float landY;
	// 포물선
	Vector2 start;
	float flightTime;
	float elapsed;
	float arcHeight;

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
		if (target == null) { StartFalling(); return; }

		// 대상까지 거리로 날아가는 시간과 포물선 높이를 정한다
		start = transform.position;
		float distance = Vector2.Distance(start, AimPoint(target));
		flightTime = Mathf.Max(distance / speed, 0.05f);
		arcHeight = Mathf.Min(distance * arcHeightRatio, maxArcHeight);
		elapsed = 0f;
		phase = Phase.Flying;
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

		// 시작점에서 대상(움직이면 따라감)까지 직선 위에 포물선 높이를 더한다
		elapsed += Time.deltaTime;
		float s = Mathf.Clamp01(elapsed / flightTime);
		Vector2 aim = AimPoint(target);
		Vector2 next = Vector2.Lerp(start, aim, s) + Vector2.up * (arcHeight * 4f * s * (1f - s));
		FaceTowards(next - (Vector2)transform.position);
		transform.position = next;

		if (s >= 1f || Vector2.Distance(next, aim) <= hitDistance)
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
		// 내가 속한 장소 씬의 물리 세계에서 땅을 찾는다 (place-scenes.md)
		RaycastHit2D ground = gameObject.scene.GetPhysicsScene2D().Raycast(transform.position, Vector2.down, 100f, groundLayers);
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
