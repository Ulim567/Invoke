using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어 상태(이동·공중·시전·피격·사망)를 읽어서 알맞은 애니메이션을 재생하는 컴포넌트. 연출만 한다.
/// <para>
/// - 값을 바꾸지 않고 읽기만 한다. 네트워크를 붙이면 상태만 동기화되고, 이 컴포넌트는 각 화면에서 그대로 돈다.<br/>
/// - Animator Controller에는 상태 6개(Idle, Run, Jump, Cast, Hit, Death)만 두고 화살표(Transition)는 만들지 않는다.
///   어떤 상태를 재생할지는 이 스크립트가 정해서 <c>Animator.Play</c>로 바꾼다.<br/>
/// - 읽는 곳 (없으면 임시 값으로 대신한다)<br/>
///   · 이동: Rigidbody2D 가로 속도. 다른 플레이어의 캐릭터는 Kinematic이고 위치만 옮겨져 속도가 0이므로, 프레임마다 위치 변화로 계산한다 (<c>HorizontalSpeed</c>)<br/>
///   · 공중: <c>PlayerMovement.IsGrounded</c> (박진우). 없으면 세로 속도로 임시 판단. 더블점프(<c>PlayerMovement.AirJumped</c>)면 Jump를 처음부터 다시 튼다<br/>
///   · 스킬 모션: <c>SkillEffectEvents.Started</c>(스킬 ID, 박진우 #152). 스킬마다 자기 이름의 Animator 상태(예: c_wide_slash)를 튼다.
///     어떤 임시 모션을 넣었는지, 몇 초 보여 줄지는 <c>SkillMotionTable</c>에 있다. 이동 계열은 모션 없음 (이동이 보여 준다).
///     스킬 상태가 컨트롤러에 없으면 임시 모션 상태(Cast, Slash, Throw, Summon, Lunge)를, 표에 없는 새 스킬은 스킬 계열로 고른 임시 모션을 튼다<br/>
///   · SkillEffectEvents가 없는 시험용 플레이어는 예전처럼 함수 시작(<c>FunctionExecutorNetwork.TaskStarted</c> / <c>FunctionExecutor.TaskStarted</c>)에 Cast<br/>
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
	const string CastState = "Cast"; // 찌르기 (공용 시전 모션)
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
	NetworkIdentity identity;
	NetworkTransformBase networkTransform;
	PlayerMovement movement;
	PlayerHealth health;
	FunctionExecutor executor;
	FunctionExecutorNetwork executorNetwork;
	SkillEffectEvents skillEvents;
	string castState = CastState; // 지금 시전 중인 모션

	float castEndTime;
	float hitEndTime;
	bool dead;
	string currentState;

	// 다른 플레이어의 위치 변화로 계산하는 가로 속도 (MeasureHorizontalSpeed)
	Vector3 lastPosition;
	bool hasLastPosition;
	float horizontalSpeed;

	/// <summary>
	/// 이 화면에서 보이는 캐릭터의 가로 속도(유닛/초). 내 캐릭터는 Rigidbody2D 속도, 다른 플레이어는 위치 변화로 계산한 값이다.
	/// 이동 모션(Run)을 고르는 데 쓰며, 시험이 읽기도 한다.
	/// </summary>
	public float HorizontalSpeed => horizontalSpeed;

	void Awake()
	{
		if (animator == null) animator = GetComponent<Animator>();
		body = GetComponent<Rigidbody2D>();
		identity = GetComponent<NetworkIdentity>();
		networkTransform = GetComponent<NetworkTransformBase>();
		movement = GetComponent<PlayerMovement>();
		health = GetComponent<PlayerHealth>();
		executor = GetComponent<FunctionExecutor>();
		executorNetwork = GetComponent<FunctionExecutorNetwork>();
		skillEvents = GetComponent<SkillEffectEvents>();
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
		// 스킬 모션은 SkillEffectEvents.Started로 고른다. 없을 때만 함수 시작에 공용 Cast를 튼다
		if (skillEvents != null) skillEvents.Started += OnSkillStarted;
		else if (executorNetwork != null) executorNetwork.TaskStarted += OnNetworkCastStarted;
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
		if (skillEvents != null) skillEvents.Started -= OnSkillStarted;
		else if (executorNetwork != null) executorNetwork.TaskStarted -= OnNetworkCastStarted;
		else if (executor != null) executor.TaskStarted -= OnCastStarted;
		if (movement != null) movement.AirJumped -= OnAirJumped;
	}

	// 더블점프: 이미 공중이라 Jump 상태가 바뀌지 않으므로, 상태를 비워서 Jump를 처음부터 다시 튼다 (PlayerMovement.AirJumped, 박진우 #256)
	void OnAirJumped() => currentState = null;

	void Update()
	{
		horizontalSpeed = MeasureHorizontalSpeed();
		Play(ChooseState());
	}

	// 다른 플레이어의 캐릭터인가. 네트워크를 켜지 않은 시험용 플레이어와 내 캐릭터는 Rigidbody2D 속도가 맞다
	bool IsRemotePlayer()
	{
		return identity != null && networkTransform != null && (NetworkClient.active || NetworkServer.active) && !identity.isLocalPlayer;
	}

	// 가로 속도를 잰다. 다른 플레이어의 캐릭터는 Kinematic이고 NetworkRigidbodyReliable2D가 위치만 옮겨서 Rigidbody2D 속도가 0이므로 위치 변화로 계산한다
	float MeasureHorizontalSpeed()
	{
		if (!IsRemotePlayer()) return body != null ? body.linearVelocity.x : 0f;

		Vector3 position = transform.position;
		// 순간이동(ServerTeleport)과 처음 나타난 때는 Mirror가 보간 목록을 비우므로, 목록이 비었거나 하나뿐인 프레임의 위치 변화는 이동이 아니다.
		// 거리 기준으로 순간이동을 가르면 빠른 낙하나 검 타기와 구분이 흐려져서, Mirror의 상태를 따른다.
		// 호스트에서 다른 클라이언트의 캐릭터는 서버 쪽 목록, 클라이언트에서는 클라이언트 쪽 목록으로 보간한다
		var snapshots = NetworkServer.active ? networkTransform.serverSnapshots : networkTransform.clientSnapshots;
		bool moved = hasLastPosition && snapshots.Count > 1 && Time.deltaTime > 0f;
		float speed = moved ? (position.x - lastPosition.x) / Time.deltaTime : 0f;

		lastPosition = position;
		hasLastPosition = true;
		return speed;
	}

	/// <summary>
	/// 지금 재생할 상태를 고른다. 위에 있을수록 먼저다: 사망 > 피격 > 시전 > 공중 > 이동 > 대기
	/// </summary>
	string ChooseState()
	{
		float now = Time.time;
		if (dead) return DeathState;
		if (now < hitEndTime) return HitState;
		if (now < castEndTime) return castState;
		if (!IsGrounded()) return JumpState;
		if (Mathf.Abs(horizontalSpeed) > runThreshold) return RunState;
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

	void StartCast() => StartCast(CastState, castDuration);

	void StartCast(string state, float duration)
	{
		castState = state;
		castEndTime = Time.time + duration;
		currentState = null; // 연달아 시전해도 처음부터 다시 튼다
	}

	// SkillEffectEvents.Started (스킬 ID). 모든 화면에서 불린다. 스킬 전용 상태를 튼다 (SkillMotionTable)
	void OnSkillStarted(string skillId)
	{
		if (SkillMotionTable.TryGet(skillId, out SkillMotionTable.Entry motion))
		{
			if (motion.Base == null) return; // 이동 계열: 움직임이 보여 준다
			string skillState = motion.StateName;
			StartCast(HasState(skillState) ? skillState : motion.Base, motion.Duration);
			return;
		}

		// 표에 없는 새 스킬: 스킬 계열로 임시 모션을 고른다. SkillMotionTable에 한 줄 더하면 전용 상태를 쓴다
		if (!SkillTable.TryGet(skillId, out SkillData skill))
		{
			StartCast();
			return;
		}
		switch (skill.Family)
		{
			case SkillFamily.Slash: StartCast(SkillMotionTable.Slash, castDuration); break;
			case SkillFamily.Throw: StartCast(SkillMotionTable.Throw, castDuration); break;
			case SkillFamily.Summon: StartCast(SkillMotionTable.Summon, castDuration); break;
			case SkillFamily.Movement: break;
			default: StartCast(); break; // 찌르기
		}
	}

	bool HasState(string state) => animator.HasState(0, Animator.StringToHash(state));
}
