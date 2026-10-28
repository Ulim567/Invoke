using UnityEngine;
using Mirror;

/// <summary>
/// 플레이어의 좌우 이동과 점프를 맡는 컴포넌트. (공용 PlayerMove를 복사해 바닥 판정을 더한 것)
/// <para>
/// - 이동 속도는 PlayerStats.MoveSpeed를 읽는다.<br/>
/// - 점프는 바닥에 서 있을 때만 된다. 바닥 판정은 콜라이더 발밑의 얇은 상자로 한다.<br/>
/// - 점프는 힘이 아니라 높이(유닛)로 정한다. 중력(Rigidbody2D Gravity Scale)을 바꿔도 같은 높이로 뛴다.<br/>
/// - 떨어질 때는 중력을 더 줘서 묵직하게 떨어진다.<br/>
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

	[Header("연결")]
	[SerializeField] private Rigidbody2D body;
	[SerializeField] private Collider2D bodyCollider;
	[SerializeField] private PlayerInputReader input;
	[SerializeField] private PlayerStats stats;

	[Header("바닥 판정")]
	[Tooltip("바닥으로 인정할 레이어 (Ground, Platform)")]
	[SerializeField] private LayerMask groundLayers;
	[Tooltip("판정 상자 너비 = 콜라이더 너비 × 이 값. 1보다 작아야 옆의 벽이 바닥으로 잡히지 않는다.")]
	[SerializeField, Range(0.1f, 1f)] private float groundCheckWidthRatio = 0.9f;
	[Tooltip("판정 상자 높이 (유닛). 발밑에서 위아래로 이 값의 절반씩 걸친다.")]
	[SerializeField, Min(0.01f)] private float groundCheckHeight = 0.1f;

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
			return Physics2D.OverlapBox(center, size, 0f, groundLayers) != null;
		}
	}

	// 컴포넌트를 처음 붙이거나 Reset을 누르면 연결과 기본 레이어를 자동으로 채운다
	void Reset()
	{
		body = GetComponent<Rigidbody2D>();
		bodyCollider = GetComponent<Collider2D>();
		input = GetComponent<PlayerInputReader>();
		stats = GetComponent<PlayerStats>();
		groundLayers = LayerMask.GetMask("Ground", "Platform");
	}

	void Update()
	{
		if (!isLocalPlayer) return;
		Move();
	}

	void FixedUpdate()
	{
		if (!isLocalPlayer) return;
		ApplyFallGravity();
	}

	void Move()
	{
		body.linearVelocity = new Vector2(input.MoveX * stats.MoveSpeed, body.linearVelocity.y);

		if (input.JumpPressed && IsGrounded)
		{
			Jump();
		}
	}

	// 지금 중력에서 jumpHeight만큼 오르는 데 필요한 속도를 바로 넣는다 (v = √(2gh))
	void Jump()
	{
		float gravity = Mathf.Abs(Physics2D.gravity.y * body.gravityScale);
		float jumpSpeed = Mathf.Sqrt(2f * gravity * jumpHeight);
		body.linearVelocity = new Vector2(body.linearVelocity.x, jumpSpeed);
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
