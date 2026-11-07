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
/// - 입력은 PlayerInputReader에서만 읽는다. 위아래(MoveY)는 검 타기 중에만 쓴다 (#317).<br/>
/// - 움직임 잠그기(SetMovementLocked, #207): 장소 이동처럼 씬을 바꾸는 동안 그 자리에 멈춰 둔다. 잠근 동안 속도 0, 중력 없음, 입력·돌진 무시.
/// </para>
/// <para>
/// 네트워크 (#223, network.md 「경우별 처리 방법」 3번)<br/>
/// - 플레이어 위치는 소유 클라이언트가 정한다 (NetworkRigidbodyReliable2D, Client To Server). 그래서 서버의 스킬이 StartLunge / StopLunge / FaceTowards를 부르면,<br/>
///   그 플레이어가 다른 컴퓨터의 플레이어일 때 [TargetRpc]로 소유 클라이언트에 요청하고 거기서 움직인다. Host의 내 캐릭터, 네트워크 없는 테스트는 그 자리에서 한다.<br/>
/// - Facing은 [SyncVar]다. 이 컴포넌트의 Sync Direction을 Client To Server로 둬서, 소유 클라이언트가 바꾼 방향이 서버와 다른 클라이언트에 맞춰진다.
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

	[Header("돌진 (찌르기 스킬)")]
	[Tooltip("스킬이 대상 앞까지 돌진할 때 속도 (유닛/초, 임시)")]
	[SerializeField, Min(0.1f)] private float lungeSpeed = 30f;
	[Tooltip("돌진 최대 시간(초). 벽 등에 막혀 못 닿아도 이 시간이 지나면 멈춘다")]
	[SerializeField, Min(0.05f)] private float lungeMaxTime = 0.35f;

	[Header("대시 (이동 스킬, #256)")]
	[Tooltip("대시로 가는 거리 (유닛, 임시)")]
	[SerializeField, Min(0.1f)] private float dashDistance = 4f;
	[Tooltip("대시에 걸리는 시간 (초, 임시)")]
	[SerializeField, Min(0.02f)] private float dashDuration = 0.15f;

	[Header("더블점프 (#256)")]
	[Tooltip("PlayerProgress(해금 목록)가 없는 테스트 씬에서 더블점프를 쓸 수 있게 할지. 실제 게임은 스킬 트리 해금으로 정한다")]
	[SerializeField] private bool allowDoubleJumpWithoutProgress = true;

	[Header("검 타기 (#256)")]
	[Tooltip("검을 타고 좌우로 나는 속도 (유닛/초, 임시)")]
	[SerializeField, Min(0.1f)] private float rideSpeed = 9f;
	[Tooltip("검을 타고 위아래(↑↓)로 나는 속도 (유닛/초, 임시, #317)")]
	[SerializeField, Min(0.1f)] private float rideVerticalSpeed = 6f;
	[Tooltip("검을 타고 나는 시간 (초, 임시). 스킬이 끝나도 이 시간 동안 계속 난다")]
	[SerializeField, Min(0.1f)] private float rideSeconds = 30f;

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

	// 돌진 중이면 true. 이 동안은 좌우 입력 대신 lungeTargetX를 향해 움직인다
	bool lunging;
	// 움직임 잠그기 (#207). 잠그기 전 중력 배율을 기억했다가 풀 때 되돌린다
	bool movementLocked;
	float gravityScaleBeforeLock;
	float lungeTargetX;
	float lungeEndTime;

	// 달리는 속도로 움직이는지. 땅에 있을 때만 Shift 상태로 바꾸고, 공중에서는 땅을 떠난 순간의 값을 유지한다
	bool runMode;

	// 이동 스킬 (#256). 대시·검 타기 동안은 중력을 끄고, 끝나면 처음 중력(normalGravityScale)으로 되돌린다
	float normalGravityScale = -1f;
	bool dashing;
	int dashDirection;
	float dashEndTime;
	bool riding;
	float rideEndTime;
	// 공중 점프(더블점프)를 이번 공중에서 이미 썼는지. 땅에 닿으면 다시 쓸 수 있다
	bool airJumpUsed;
	PlayerProgress progress;

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
	/// 스킬이 앞쪽 방향(판정, 투사체)을 정할 때, 그림 뒤집기(PlayerFacingView)가 읽는다.<br/>
	/// [SyncVar] (#223): 소유 클라이언트가 바꾸면 서버와 다른 클라이언트에 맞춰진다 (Sync Direction: Client To Server).
	/// </summary>
	public int Facing => facing;

	// 바꾸는 곳은 UpdateFacing, FaceTowards 두 군데뿐이다 (코드 작성 규칙 2)
	[SyncVar] int facing = 1;

	/// <summary>움직임이 잠겨 있으면 true (#207).</summary>
	public bool IsMovementLocked => movementLocked;

	/// <summary>
	/// 움직임을 잠그거나 푼다 (#207). 장소 이동(PlaceTravelClient)이 씬을 바꾸는 동안 잠근다.<br/>
	/// 잠근 동안: 속도 0, 중력 없음(그 자리에 떠 있음), 진행 중인 돌진·내려가기 중단, 좌우·점프·내려가기 입력과 StartLunge 무시.<br/>
	/// 풀 때: 원래 중력으로 돌아가고 속도 0에서 시작한다. 이미 그 상태면 아무 일도 없다.
	/// </summary>
	public void SetMovementLocked(bool locked)
	{
		if (movementLocked == locked) return;
		// 대시·검 타기 중이면 먼저 끝내서 중력을 되돌린 뒤 잠근다 (잠글 때 저장하는 중력이 0이 되지 않게)
		if (locked)
		{
			EndDashLocal();
			EndRideLocal();
		}
		movementLocked = locked;
		if (locked)
		{
			lunging = false;
			EndDrop();
			gravityScaleBeforeLock = body.gravityScale;
			body.gravityScale = 0f;
		}
		else
		{
			body.gravityScale = gravityScaleBeforeLock;
		}
		body.linearVelocity = Vector2.zero;
	}

	/// <summary>돌진 중이면 true. 찌르기 스킬이 돌진이 끝났는지 읽는다.</summary>
	public bool IsLunging => lunging;

	/// <summary>
	/// x 위치까지 좌우로 돌진한다 (찌르기 스킬이 멀리 있는 대상 앞까지 갈 때). 돌진하는 쪽을 바라본다.<br/>
	/// 돌진하는 동안 좌우 입력은 무시한다. 닿거나 Lunge Max Time이 지나거나 경직·사망하면 멈춘다. 위아래(중력, 점프)는 그대로다.
	/// </summary>
	public void StartLunge(float targetX)
	{
		// 다른 컴퓨터의 플레이어면 소유 클라이언트에 돌진을 맡긴다 (#223). 서버에서는 바라보는 방향만 먼저 맞춰 둔다
		if (OwnedByRemoteClient)
		{
			SetFacingTowards(targetX);
			TargetStartLunge(targetX);
			return;
		}
		// 움직임이 잠겨 있으면 돌진하지 않는다 (#207)
		if (movementLocked) return;
		lunging = true;
		lungeTargetX = targetX;
		lungeEndTime = Time.time + lungeMaxTime;
		FaceTowards(targetX);
	}

	/// <summary>돌진을 멈춘다. 스킬이 끝나거나 끊길 때 부른다.</summary>
	public void StopLunge()
	{
		if (OwnedByRemoteClient)
		{
			TargetStopLunge();
			return;
		}
		if (!lunging) return;
		lunging = false;
		body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
	}

	/// <summary>x 위치 쪽을 바라본다. 스킬이 대상 쪽으로 돌아설 때 부른다.</summary>
	public void FaceTowards(float x)
	{
		SetFacingTowards(x);
		// 방향은 소유 클라이언트가 정하므로, 다른 컴퓨터의 플레이어면 거기에도 알린다 (#223). 서버의 값은 위에서 먼저 바꿔 둬서 같은 틱의 스킬이 바로 읽는다
		if (OwnedByRemoteClient) TargetFaceTowards(x);
	}

	/// <summary>
	/// [연출] 더블점프(공중 점프)를 한 순간 (#256). 점프 모션을 처음부터 다시 틀 때 쓴다 (PlayerAnimator, 임혜원).<br/>
	/// 첫 점프는 공중 상태로 바뀌는 것으로 알 수 있지만, 더블점프는 이미 공중이라 따로 알린다. 내 화면(소유 클라이언트)에서 불린다.
	/// </summary>
	public event System.Action AirJumped;

	/// <summary>대시 중이면 true (#256). 다른 컴퓨터의 플레이어면 서버에서는 늘 false (대시는 소유 클라이언트가 함)</summary>
	public bool IsDashing => dashing;

	/// <summary>검을 타고 나는 중이면 true (#256). 다른 컴퓨터의 플레이어면 서버에서는 늘 false</summary>
	public bool IsRiding => riding;

	/// <summary>검을 타고 나는 시간 (초, Inspector Ride Seconds). 검 타기 스킬이 소환검을 붙여 둘 시간으로도 쓴다 (#256)</summary>
	public float RideSeconds => rideSeconds;

	/// <summary>검 타기 남은 시간 (초). 타는 중이 아니면 0. 타이머 표시(SwordRideTimerView)가 읽는다 (#256)</summary>
	public float RideRemaining => riding ? Mathf.Max(0f, rideEndTime - Time.time) : 0f;

	/// <summary>
	/// 이번 공중에서 더블점프를 이미 썼으면 true (#256). 땅에 닿으면 false로 돌아온다.
	/// </summary>
	public bool AirJumpUsed => airJumpUsed;

	/// <summary>
	/// 더블점프를 쓸 수 있으면 true (#256): 스킬 트리에서 common.double_jump를 해금했는지 (PlayerProgress.Unlocked).<br/>
	/// PlayerProgress가 없는 테스트 씬에서는 Allow Double Jump Without Progress를 따른다.
	/// </summary>
	public bool DoubleJumpUnlocked
	{
		get
		{
			if (progress == null) progress = GetComponent<PlayerProgress>();
			if (progress == null) return allowDoubleJumpWithoutProgress;
			foreach (string id in progress.Unlocked) if (id == PredefinedFunctionIds.DoubleJump) return true;
			return false;
		}
	}

	/// <summary>
	/// 대시 (#256): direction(1 오른쪽, -1 왼쪽) 쪽으로 Dash Distance(임시 4칸)를 Dash Duration(0.15초)에 간다. 그쪽을 바라본다.<br/>
	/// 공중에서도 된다. 대시 중에는 중력·좌우 입력·점프가 없고, 벽에 막히면 거기서 멈춘다. 이동 스킬 대시가 부른다.<br/>
	/// 서버에서 다른 컴퓨터의 플레이어에 부르면 소유 클라이언트에서 한다 (#223과 같은 방식). 잠겨 있거나 경직·사망 중이면 하지 않는다.
	/// </summary>
	public void StartDash(int direction)
	{
		int dir = direction >= 0 ? 1 : -1;
		if (OwnedByRemoteClient)
		{
			facing = dir;
			TargetStartDash(dir);
			return;
		}
		if (movementLocked || (health != null && !health.CanAct)) return;
		lunging = false;
		EndRideLocal();
		facing = dir;
		dashDirection = dir;
		dashEndTime = Time.time + dashDuration;
		dashing = true;
		SuspendGravity();
		body.linearVelocity = new Vector2(dir * dashDistance / dashDuration, 0f);
	}

	/// <summary>
	/// 공중 점프 (#256): 공중이고 이번 공중에서 아직 안 썼으면 한 번 더 뛴다. 블록코딩의 더블점프(common.double_jump)가 부른다.<br/>
	/// 플레이어가 Space로 하는 더블점프는 이 컴포넌트가 해금 여부(DoubleJumpUnlocked)를 보고 스스로 한다. 서버에서 다른 컴퓨터의 플레이어에 부르면 소유 클라이언트에서 한다.
	/// </summary>
	public void RequestAirJump()
	{
		if (OwnedByRemoteClient)
		{
			TargetAirJump();
			return;
		}
		TryAirJump();
	}

	/// <summary>
	/// 검 타기 시작 (#256): 중력 없이 그 높이에 떠서 ←→로 Ride Speed(임시 9), ↑↓로 Ride Vertical Speed(임시 6, #317)로 Ride Seconds(임시 30초) 동안 난다.<br/>
	/// ↑↓를 떼면 그 높이에 멈춘다. ↓로 내려갈 때는 발판(Platform)을 뚫고 내려가고 땅(Ground)에서 멈춘다. 시간이 다 되거나 StopRide를 부르면 내려온다. 검 타기 스킬이 부른다. 서버에서 다른 컴퓨터의 플레이어에 부르면 소유 클라이언트에서 한다.
	/// </summary>
	public void StartRide()
	{
		if (OwnedByRemoteClient)
		{
			TargetStartRide();
			return;
		}
		if (movementLocked || riding) return;
		lunging = false;
		EndDashLocal();
		riding = true;
		rideEndTime = Time.time + rideSeconds;
		SuspendGravity();
		body.linearVelocity = Vector2.zero;
	}

	/// <summary>검 타기를 끝낸다 (#256). 중력이 돌아와 떨어진다. 검 타기 스킬이 끝나거나 끊길 때 부른다.</summary>
	public void StopRide()
	{
		if (OwnedByRemoteClient)
		{
			TargetStopRide();
			return;
		}
		EndRideLocal();
	}

	// [소유 클라이언트] 서버의 이동 스킬이 요청한 대시·공중 점프·검 타기 (#256)
	[TargetRpc]
	void TargetStartDash(int direction) => StartDash(direction);

	[TargetRpc]
	void TargetAirJump() => TryAirJump();

	[TargetRpc]
	void TargetStartRide() => StartRide();

	[TargetRpc]
	void TargetStopRide() => StopRide();

	bool TryAirJump()
	{
		if (movementLocked || dashing || riding || airJumpUsed || IsGrounded) return false;
		if (health != null && !health.CanAct) return false;
		airJumpUsed = true;
		Jump();
		AirJumped?.Invoke();
		return true;
	}

	// 대시·검 타기 동안 중력을 끈다. 처음 중력은 한 번만 기억한다
	void SuspendGravity()
	{
		if (normalGravityScale < 0f) normalGravityScale = body.gravityScale;
		body.gravityScale = 0f;
	}

	void RestoreGravity()
	{
		if (normalGravityScale >= 0f && !dashing && !riding) body.gravityScale = normalGravityScale;
	}

	void EndDashLocal()
	{
		if (!dashing) return;
		dashing = false;
		body.linearVelocity = new Vector2(0f, 0f);
		RestoreGravity();
	}

	void EndRideLocal()
	{
		if (!riding) return;
		riding = false;
		RestoreGravity();
	}

	// 서버에서 불렸고, 이 플레이어를 다른 컴퓨터(원격 클라이언트)가 움직이면 true. Host의 내 캐릭터, 네트워크 없는 테스트는 false
	bool OwnedByRemoteClient => isServer && !isLocalPlayer && connectionToClient != null;

	void SetFacingTowards(float x)
	{
		float dx = x - transform.position.x;
		if (dx > 0.01f) facing = 1;
		else if (dx < -0.01f) facing = -1;
	}

	// [소유 클라이언트] 서버의 스킬이 요청한 돌진·멈춤·돌아서기 (#223). 받은 쪽은 내 캐릭터이므로 그대로 움직인다
	[TargetRpc]
	void TargetStartLunge(float targetX) => StartLunge(targetX);

	[TargetRpc]
	void TargetStopLunge() => StopLunge();

	[TargetRpc]
	void TargetFaceTowards(float x) => SetFacingTowards(x);

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
			return ScenePhysics().OverlapBox(center, size, 0f, mask) != null;
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
		if (!isLocalPlayer || movementLocked) return;
		ApplyFallGravity();
	}

	void Move()
	{
		// 잠근 동안은 그 자리에 멈춰 있는다 (#207)
		if (movementLocked)
		{
			body.linearVelocity = Vector2.zero;
			return;
		}

		// 경직·사망 중에는 좌우 입력을 받지 않고 멈춘다 (떨어지는 것은 그대로)
		if (health != null && !health.CanAct)
		{
			lunging = false;
			body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
			return;
		}

		if (lunging)
		{
			UpdateLunge();
			return;
		}

		// 대시 중: 정한 속도로 가다가 시간이 되면 멈춘다 (#256)
		if (dashing)
		{
			if (Time.time >= dashEndTime) EndDashLocal();
			else body.linearVelocity = new Vector2(dashDirection * dashDistance / dashDuration, 0f);
			return;
		}

		// 검 타기 중: 중력 없이 ←→, ↑↓(MoveY, #317)로 난다 (#256). 키를 떼면 그 자리에 멈춘다.
		// 슬롯 키를 누르는 동안은 MoveX·MoveY가 0이라(player-input.md) 대상을 고르는 동안 그 자리에 떠 있다
		if (riding && Time.time >= rideEndTime) EndRideLocal();
		if (riding)
		{
			float rideX = input.MoveX;
			float rideY = input.MoveY;
			body.linearVelocity = new Vector2(rideX * rideSpeed, rideY * rideVerticalSpeed);
			// ↓로 내려가는 동안은 발판을 뚫고 내려간다 (#317). 위로는 발판 설정(One Way)으로 원래 뚫린다. 땅(Ground)에서는 멈춘다
			if (rideY < 0f) PassThroughPlatformsUnderFeet();
			UpdateFacing(rideX);
			return;
		}

		bool grounded = IsGrounded;
		// 땅에 닿으면 더블점프를 다시 쓸 수 있다 (#256)
		if (grounded) airJumpUsed = false;
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
		// 공중에서 Space: 더블점프를 해금했으면 한 번 더 (#256)
		else if (input.JumpPressed && !grounded && DoubleJumpUnlocked)
		{
			TryAirJump();
		}
	}

	// 돌진: 남은 거리가 이번 프레임에 갈 거리보다 짧으면 딱 맞게 멈춘다
	void UpdateLunge()
	{
		float dx = lungeTargetX - transform.position.x;
		if (Mathf.Abs(dx) < 0.05f || Time.time >= lungeEndTime)
		{
			StopLunge();
			return;
		}
		float speed = Mathf.Min(lungeSpeed, Mathf.Abs(dx) / Mathf.Max(Time.deltaTime, 0.0001f));
		body.linearVelocity = new Vector2(Mathf.Sign(dx) * speed, body.linearVelocity.y);
	}

	// 움직이는 방향을 바라본다. 멈추면(0) 마지막 방향을 유지한다
	void UpdateFacing(float moveX)
	{
		if (moveX > 0f) facing = 1;
		else if (moveX < 0f) facing = -1;
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
		if (ScenePhysics().OverlapBox(center, size, 0f, solidGround) != null) return;

		PassThroughPlatformsUnderFeet();
	}

	// 발밑에 겹친 발판(Platform)과 내 콜라이더의 충돌을 끄고 내려가는 중(IsDropping)으로 둔다. 땅(Ground)은 끄지 않는다.
	// ↓ + Space 내려가기와, 검 타기 중 ↓로 발판을 뚫고 내려갈 때(#317) 쓴다. 다시 켜는 것은 Update가 한다 (Drop Duration이 지나고 몸이 빠져나오면)
	void PassThroughPlatformsUnderFeet()
	{
		GetGroundCheckBox(out Vector2 center, out Vector2 size);
		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(platformLayers);
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> platforms = new List<Collider2D>();
		if (ScenePhysics().OverlapBox(center, size, 0f, filter, platforms) == 0) return;

		// 발판 전체를 끄지 않고 내 콜라이더와의 충돌만 끈다. 다른 플레이어는 그대로 발판 위에 선다
		foreach (Collider2D platform in platforms)
		{
			if (droppedPlatforms.Contains(platform)) continue;
			Physics2D.IgnoreCollision(bodyCollider, platform, true);
			droppedPlatforms.Add(platform);
		}
		dropEndTime = Time.time + dropDuration;
	}

	// 내가 속한 장소 씬의 물리 세계에 묻는다. 서버는 씬마다 물리 세계가 따로 있어서 Physics2D로는 못 찾는다 (place-scenes.md). 씬이 바뀔 수 있어 저장하지 않고 매번 가져온다
	PhysicsScene2D ScenePhysics() => gameObject.scene.GetPhysicsScene2D();

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
