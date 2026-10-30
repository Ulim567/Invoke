using System.Collections.Generic;
using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어의 좌우 이동과 점프를 맡는 컴포넌트. (공용 PlayerMove를 복사해 바닥 판정을 더한 것)
/// <para>
/// - 이동 속도는 PlayerStats.MoveSpeed를 읽는다. 왼쪽 Shift(PlayerInputReader.RunHeld)를 누르는 동안은 Run Speed Multiplier배로 달린다.<br/>
/// - 공중에서는 땅을 떠난 순간의 달리기 여부를 착지할 때까지 유지한다. 공중에서 Shift를 누르거나 떼도 속도가 바뀌지 않는다.<br/>
/// - 점프는 바닥에 서 있을 때만 된다. 바닥 판정은 콜라이더 발밑의 얇은 상자로 한다.<br/>
/// - 점프는 힘이 아니라 높이(유닛)로 정한다. 중력(Rigidbody2D Gravity Scale)을 바꿔도 같은 높이로 뛴다.<br/>
/// - 떨어질 때는 중력을 더 줘서 묵직하게 떨어진다.<br/>
/// - 바라보는 방향(Facing)을 MoveX로 정한다. 멈추면 마지막 방향을 유지한다. 그림 뒤집기는 PlayerFacingView가 한다.<br/>
/// - 경직·사망 중(PlayerHealth.CanAct가 false)에는 움직이거나 점프하지 않는다.<br/>
/// - 발판(Platform) 위에서 ↓ + Space(DropPressed)를 누르면 잠깐 그 발판과 부딪히지 않게 해서 아래로 내려간다. 내 캐릭터만 통과한다.<br/>
/// - 발판을 아래에서 뚫고 오르는 것은 발판 타일맵의 Platform Effector 2D가 한다 (docs/systems/player-movement.md 「발판 설정」).<br/>
/// - 입력은 PlayerInputReader에서만 읽는다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStats), typeof(PlayerInputReader), typeof(Rigidbody2D))]
public class PlayerMovement : NetworkBehaviour
{
	[Header("점프")]
	[Tooltip("점프 최고 높이 (유닛). 중력을 바꿔도 이 높이를 유지한다.")]
	[SerializeField, Min(0f)] private float jumpHeight = 4f;
	[Tooltip("떨어질 때 중력 배율. 1이면 오를 때와 같고, 클수록 빨리 떨어져 묵직해진다.")]
	[SerializeField, Min(1f)] private float fallGravityMultiplier = 1.5f;

	[Header("달리기")]
	[Tooltip("Shift를 누르는 동안 이동 속도 배율 (임시: 기획에서 정해지면 바꾼다)")]
	[SerializeField, Min(1f)] private float runSpeedMultiplier = 1.5f;

	[Header("연결")]
	[SerializeField] private Rigidbody2D body;
	[SerializeField] private Collider2D bodyCollider;
	[SerializeField] private PlayerInputReader input;
	[SerializeField] private PlayerStats stats;
	[Tooltip("비워 두면 경직·사망 검사를 하지 않는다")]
	[SerializeField] private PlayerHealth health;

	[Header("바닥 판정")]
	[Tooltip("바닥으로 인정할 레이어 (Ground, Platform)")]
	[SerializeField] private LayerMask groundLayers;
	[Tooltip("판정 상자 너비 = 콜라이더 너비 × 이 값. 1보다 작아야 옆의 벽이 바닥으로 잡히지 않는다.")]
	[SerializeField, Range(0.1f, 1f)] private float groundCheckWidthRatio = 0.9f;
	[Tooltip("판정 상자 높이 (유닛). 발밑에서 위아래로 이 값의 절반씩 걸친다.")]
	[SerializeField, Min(0.01f)] private float groundCheckHeight = 0.1f;

	[Header("발판 내려가기")]
	[Tooltip("↓ + Space로 내려갈 수 있는 레이어 (Platform). Ground Layers에 포함되어 있어야 한다.")]
	[SerializeField] private LayerMask platformLayers;
	[Tooltip("내려갈 때 발판과 부딪히지 않는 최소 시간(초). 이 시간이 지나도 몸이 발판에 걸쳐 있으면 빠져나올 때까지 계속 통과한다.")]
	[SerializeField, Min(0.05f)] private float dropDuration = 0.3f;

	// 내려가는 중에 충돌을 끈 발판들. 최소 시간이 지나고 몸이 발판에서 완전히 빠져나오면 다시 켠다
	readonly List<Collider2D> droppedPlatforms = new List<Collider2D>();
	float dropEndTime;

	// 달리는 속도로 움직이는지. 땅에 있을 때만 Shift 상태로 바꾸고, 공중에서는 땅을 떠난 순간의 값을 유지한다
	bool runMode;

	/// <summary>
	/// 달리는 속도로 좌우로 움직이고 있으면 true (멈춰 있으면 false). 달리기 애니메이션에서 읽는다.<br/>
	/// 공중에서는 땅을 떠난 순간의 달리기 여부를 유지한다.
	/// </summary>
	public bool IsRunning => runMode && Mathf.Abs(input.MoveX) > 0f;

	/// <summary>달리기 속도 배율. 이동 속도 = PlayerStats.MoveSpeed × 이 값 (달릴 때).</summary>
	public float RunSpeedMultiplier => runSpeedMultiplier;

	/// <summary>발판 아래로 내려가는 중이면 true. 이 동안 발판은 바닥으로 치지 않는다.</summary>
	public bool IsDropping => droppedPlatforms.Count > 0;

	/// <summary>
	/// 바라보는 방향. 오른쪽 1, 왼쪽 -1. 처음에는 오른쪽이다.
	/// 스킬이 앞쪽 방향(판정, 투사체)을 정할 때, 그림 뒤집기(PlayerFacingView)가 읽는다.
	/// </summary>
	// TODO: 네트워크를 붙이면 [SyncVar]로 다른 플레이어 화면에도 맞춘다 (코드 작성 규칙 2)
	public int Facing { get; private set; } = 1;

	/// <summary>
	/// 지금 바닥에 서 있으면 true. 올라가는 중(발판을 아래에서 뚫고 오르는 중 등)에는 false다.
	/// 애니메이션이나 이동 스킬에서 바닥에 있는지 확인할 때 읽는다.
	/// </summary>
	public bool IsGrounded
	{
		get
		{
			// 올라가는 중에는 발판 안을 지나가도 바닥으로 치지 않는다 (공중 재점프 방지)
			if (body.linearVelocity.y > 0.01f) return false;
			GetGroundCheckBox(out Vector2 center, out Vector2 size);
			// 내려가는 중에는 발판을 바닥으로 치지 않는다 (발판 안을 지나가는 동안 다시 착지한 것으로 보지 않게)
			LayerMask mask = IsDropping ? (groundLayers & ~platformLayers) : groundLayers;
			return Physics2D.OverlapBox(center, size, 0f, mask) != null;
		}
	}

	// 컴포넌트를 처음 붙이거나 Reset을 누르면 연결과 기본 레이어를 자동으로 채운다
	void Reset()
	{
		body = GetComponent<Rigidbody2D>();
		bodyCollider = GetComponent<Collider2D>();
		input = GetComponent<PlayerInputReader>();
		stats = GetComponent<PlayerStats>();
		health = GetComponent<PlayerHealth>();
		groundLayers = LayerMask.GetMask("Ground", "Platform");
		platformLayers = LayerMask.GetMask("Platform");
	}

	void Update()
	{
		if (!isLocalPlayer) return;
		Move();
		if (IsDropping && Time.time >= dropEndTime && !IsInsideDroppedPlatform()) EndDrop();
	}

	// 캐릭터가 사라지거나 꺼질 때 꺼 둔 충돌을 남기지 않는다
	void OnDisable() => EndDrop();

	void FixedUpdate()
	{
		if (!isLocalPlayer) return;
		ApplyFallGravity();
	}

	void Move()
	{
		// 경직·사망 중에는 좌우 입력을 받지 않고 멈춘다 (떨어지는 것은 그대로)
		if (health != null && !health.CanAct)
		{
			body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
			return;
		}

		bool grounded = IsGrounded;
		// 땅에 있을 때만 Shift를 읽는다. 공중에서는 땅을 떠난 순간(점프, 내려가기, 낭떠러지)의 값을 그대로 쓴다
		if (grounded) runMode = input.RunHeld;

		float moveX = input.MoveX;
		float speed = stats.MoveSpeed * (runMode ? runSpeedMultiplier : 1f);
		body.linearVelocity = new Vector2(moveX * speed, body.linearVelocity.y);
		UpdateFacing(moveX);

		// ↓ + Space일 때 PlayerInputReader는 JumpPressed를 false로 준다 (입력 체계 「Complexity Consumption」)
		if (input.DropPressed && grounded)
		{
			TryDrop();
		}
		else if (input.JumpPressed && grounded)
		{
			Jump();
		}
	}

	// 움직이는 방향을 바라본다. 멈추면(0) 마지막 방향을 유지한다
	void UpdateFacing(float moveX)
	{
		if (moveX > 0f) Facing = 1;
		else if (moveX < 0f) Facing = -1;
	}

	// 지금 중력에서 jumpHeight만큼 오르는 데 필요한 속도를 바로 넣는다 (v = √(2gh))
	void Jump()
	{
		float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
		float jumpSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
		body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
	}

	// 발밑이 발판뿐일 때만 내려간다. 땅(Ground)을 밟고 있으면 내려가지 않는다
	void TryDrop()
	{
		GetGroundCheckBox(out Vector2 center, out Vector2 size);
		LayerMask solidGround = groundLayers & ~platformLayers;
		if (Physics2D.OverlapBox(center, size, 0f, solidGround) != null) return;

		Collider2D[] platforms = Physics2D.OverlapBoxAll(center, size, 0f, platformLayers);
		if (platforms.Length == 0) return;

		// 발판 전체를 끄지 않고 내 콜라이더와의 충돌만 끈다. 다른 플레이어는 그대로 발판 위에 선다
		foreach (Collider2D platform in platforms)
		{
			Physics2D.IgnoreCollision(bodyCollider, platform, true);
			droppedPlatforms.Add(platform);
		}
		dropEndTime = Time.time + dropDuration;
	}

	// 몸이 아직 발판에 걸쳐 있는지. 걸친 채로 충돌을 다시 켜면 발판 속에 끼어 버린다
	bool IsInsideDroppedPlatform()
	{
		foreach (Collider2D platform in droppedPlatforms)
		{
			if (platform != null && bodyCollider.Distance(platform).isOverlapped) return true;
		}
		return false;
	}

	void EndDrop()
	{
		foreach (Collider2D platform in droppedPlatforms)
		{
			if (platform != null && bodyCollider != null) Physics2D.IgnoreCollision(bodyCollider, platform, false);
		}
		droppedPlatforms.Clear();
	}

	// 떨어지는 중에만 기본 중력에 (배율 - 1)만큼을 더 준다
	void ApplyFallGravity()
	{
		if (body.linearVelocity.y >= 0f) return;
		float extra = Physics2D.gravity.y * body.gravityScale * (fallGravityMultiplier - 1f);
		body.linearVelocity += Vector2.up * extra * Time.fixedDeltaTime;
	}

	// 콜라이더 아래쪽 가운데에 얇은 상자를 둔다. 너비를 콜라이더보다 좁게 해서 벽 옆면은 닿지 않게 한다
	void GetGroundCheckBox(out Vector2 center, out Vector2 size)
	{
		Bounds bounds = bodyCollider.bounds;
		center = new Vector2(bounds.center.x, bounds.min.y);
		size = new Vector2(bounds.size.x * groundCheckWidthRatio, groundCheckHeight);
	}

	// Scene 창에서 플레이어를 선택하면 판정 상자를 보여 준다 (바닥이면 초록, 아니면 빨강)
	void OnDrawGizmosSelected()
	{
		if (bodyCollider == null || body == null) return;
		GetGroundCheckBox(out Vector2 center, out Vector2 size);
		Gizmos.color = Application.isPlaying && IsGrounded ? Color.green : Color.red;
		Gizmos.DrawWireCube(center, size);
	}
}
