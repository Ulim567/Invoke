using UnityEngine;

/// <summary>
/// 플레이어 상태(이동·공중·시전·피격·사망)를 읽어서 알맞은 애니메이션을 재생하는 컴포넌트. 연출만 한다.
/// <para>
/// - 값을 바꾸지 않고 읽기만 한다. 네트워크를 붙이면 상태만 동기화되고, 이 컴포넌트는 각 화면에서 그대로 돈다.<br/>
/// - Animator Controller에는 상태 6개(Idle, Run, Jump, Cast, Hit, Death)만 두고 화살표(Transition)는 만들지 않는다.
///   어떤 상태를 재생할지는 이 스크립트가 정해서 <c>Animator.Play</c>로 바꾼다.<br/>
/// - 읽는 곳 (없으면 임시 값으로 대신한다)<br/>
///   · 이동: Rigidbody2D 가로 속도<br/>
///   · 공중: <c>PlayerMovement.IsGrounded</c> (박진우). 없으면 세로 속도로 임시 판단. 더블점프(<c>PlayerMovement.AirJumped</c>)면 Jump를 처음부터 다시 튼다<br/>
///   · 시전: <c>FunctionExecutorNetwork.TaskStarted</c> (이우림). 함수는 서버에서만 실행되므로 모든 화면에 알려 주는 이쪽을 받는다.
///     FunctionExecutorNetwork가 없는 시험용 플레이어는 <c>FunctionExecutor.TaskStarted</c><br/>
///   · 피격·사망: <c>PlayerHealth.HitEffectPlayed</c>, <c>DeathEffectPlayed</c>, <c>RespawnEffectPlayed</c> (김도훈)
/// </para>
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimator : MonoBehaviour
{
	// Animator Controller 안의 상태 이름. 컨트롤러에서 이름을 바꾸면 여기도 같이 바꾼다.
	const string IdleState = "Idle";
	const string RunState = "Run";
	const string JumpState = "Jump";
	const string CastState = "Cast";
	const string HitState = "Hit";
	const string DeathState = "Death";

	[SerializeField, Tooltip("비워 두면 같은 오브젝트의 Animator를 쓴다")]
	Animator animator;

	[SerializeField, Min(0f), Tooltip("가로 속도가 이 값보다 크면 이동 중으로 본다 (유닛/초)")]
	float runThreshold = 0.1f;

	[SerializeField, Min(0f), Tooltip("시전 애니메이션을 보여 주는 시간(초). Cast 클립 길이에 맞춘다")]
	float castDuration = 0.5f;

	[SerializeField, Min(0f), Tooltip("PlayerHealth가 없을 때 쓰는 임시 피격 시간(초)")]
	float fallbackHitTime = 0.3f;

	[SerializeField, Range(0f, 1f), Tooltip("Jump 클립을 어디서부터 틀지 (0~1). 앞부분의 웅크리는 장면은 이미 몸이 떠오른 뒤라 건너뛴다")]
	float jumpStartTime = 0.25f;

	[SerializeField, Tooltip("PlayerMovement가 없을 때 바닥으로 볼 레이어")]
	LayerMask fallbackGroundLayers;

	Rigidbody2D body;
	PlayerMovement movement;
	PlayerHealth health;
	FunctionExecutor executor;
	FunctionExecutorNetwork executorNetwork;

	float castEndTime;
	float hitEndTime;
	bool dead;
	string currentState;

	void Awake()
	{
		if (animator == null) animator = GetComponent<Animator>();
		body = GetComponent<Rigidbody2D>();
		movement = GetComponent<PlayerMovement>();
		health = GetComponent<PlayerHealth>();
		executor = GetComponent<FunctionExecutor>();
		executorNetwork = GetComponent<FunctionExecutorNetwork>();
		if (fallbackGroundLayers.value == 0) fallbackGroundLayers = LayerMask.GetMask("Ground", "Platform");
	}

	void OnEnable()
	{
		if (health != null)
		{
			health.HitEffectPlayed += OnHit;
			health.DeathEffectPlayed += OnDeath;
			health.RespawnEffectPlayed += OnRespawn;
		}
		// 둘 다 있으면 네트워크 쪽만 받는다. FunctionExecutorNetwork는 네트워크 없이 돌 때도 TaskStarted를 불러 주므로 두 번 받지 않게 한다
		if (executorNetwork != null) executorNetwork.TaskStarted += OnNetworkCastStarted;
		else if (executor != null) executor.TaskStarted += OnCastStarted;
		if (movement != null) movement.AirJumped += OnAirJumped;
	}

	void OnDisable()
	{
		if (health != null)
		{
			health.HitEffectPlayed -= OnHit;
			health.DeathEffectPlayed -= OnDeath;
			health.RespawnEffectPlayed -= OnRespawn;
		}
		if (executorNetwork != null) executorNetwork.TaskStarted -= OnNetworkCastStarted;
		else if (executor != null) executor.TaskStarted -= OnCastStarted;
		if (movement != null) movement.AirJumped -= OnAirJumped;
	}

	// 더블점프: 이미 공중이라 Jump 상태가 바뀌지 않으므로, 상태를 비워서 Jump를 처음부터 다시 튼다 (PlayerMovement.AirJumped, 박진우 #256)
	void OnAirJumped() => currentState = null;

	void Update()
	{
		Play(ChooseState());
	}

	/// <summary>
	/// 지금 재생할 상태를 고른다. 위에 있을수록 먼저다: 사망 > 피격 > 시전 > 공중 > 이동 > 대기
	/// </summary>
	string ChooseState()
	{
		float now = Time.time;
		if (dead) return DeathState;
		if (now < hitEndTime) return HitState;
		if (now < castEndTime) return CastState;
		if (!IsGrounded()) return JumpState;
		if (body != null && Mathf.Abs(body.linearVelocity.x) > runThreshold) return RunState;
		return IdleState;
	}

	bool IsGrounded()
	{
		if (movement != null) return movement.IsGrounded;
		if (body == null) return true;
		// 임시: PlayerMovement가 없으면 Ground·Platform에 닿아 있고 올라가는 중이 아닐 때 바닥으로 본다.
		// 세로 속도만 보면 점프 꼭대기(속도 0)에서 바닥으로 잘못 판단해 점프 모션이 두 번 나온다.
		return body.IsTouchingLayers(fallbackGroundLayers) && body.linearVelocity.y <= 0.01f;
	}

	void Play(string state)
	{
		// 같은 상태를 매 프레임 다시 틀면 첫 장면에서 멈춰 버리므로, 바뀔 때만 처음부터 재생한다
		if (state == currentState) return;
		currentState = state;
		animator.Play(state, 0, state == JumpState ? jumpStartTime : 0f);
	}

	void OnHit(int damage)
	{
		hitEndTime = Time.time + (health != null ? health.HitStunTime : fallbackHitTime);
		currentState = null; // 연달아 맞아도 피격 모션을 처음부터 다시 튼다
	}

	void OnDeath()
	{
		dead = true;
	}

	void OnRespawn()
	{
		dead = false;
		hitEndTime = 0f;
		castEndTime = 0f;
	}

	// FunctionExecutorNetwork.TaskStarted (작업 번호, 슬롯, 함수 이름). 모든 화면에서 불린다
	void OnNetworkCastStarted(int taskId, int slot, string functionName) => StartCast();

	// FunctionExecutor.TaskStarted. FunctionExecutorNetwork가 없는 시험용 플레이어만
	void OnCastStarted(FunctionTask task) => StartCast();

	void StartCast()
	{
		castEndTime = Time.time + castDuration;
		currentState = null; // 연달아 시전해도 처음부터 다시 튼다
	}
}
