using UnityEngine;

/// <summary>
/// 레이드 보스 Gateway(분신 포함)의 상태를 읽어서 알맞은 애니메이션을 재생하는 컴포넌트. 연출만 한다.
/// <para>
/// 사용법: Gateway, GatewayClone 프리팹 최상위(SpriteRenderer와 같은 곳)에 Animator(Gateway.controller)와 함께 붙인다.<br/>
/// - 값을 바꾸지 않고 읽기만 한다. Animator Controller에는 상태만 두고 Transition은 만들지 않는다.<br/>
/// - 좌우 뒤집기(flipX)는 하지 않는다. GatewayAI가 한다.<br/>
/// - 읽는 곳 (김도훈). 없는 컴포넌트는 건너뛴다 (분신에는 GatewayAI·GatewayPhases·ProjectileAttack이 없어서 Idle·Hit·Death만 나온다)<br/>
///   · 던지기·웜 소환: <c>ProjectileAttack.FireEffectPlayed</c>, <c>GatewayAI.SummonEffectPlayed</c> → Throw<br/>
///   · 패킷 낙하·때리기: <c>GatewayAI.SlamEffectPlayed</c> → Slam (6번째 장면 0.42초에 내려침, Slam Delay·Smash Delay와 맞음)<br/>
///   · 전체 공격 모으기: <c>GatewayPhases.ChargeEffectPlayed</c>(모으는 시간) → Charge. 클립이 끝나면 마지막 장면으로 모으는 시간 동안 버틴다<br/>
///   · 그로기: <c>GatewayPhases.GroggyEffectPlayed</c>(그로기 시간) → Groggy (반복)<br/>
///   · 피격·사망: <c>Monster.HitEffectPlayed</c>, <c>DeathEffectPlayed</c>
/// </para>
/// </summary>
[RequireComponent(typeof(Animator))]
public class GatewayAnimator : MonoBehaviour
{
	// Animator Controller(Gateway.controller) 안의 상태 이름
	const string IdleState = "Idle";
	const string ThrowState = "Throw";
	const string SlamState = "Slam";
	const string ChargeState = "Charge";
	const string GroggyState = "Groggy";
	const string HitState = "Hit";
	const string DeathState = "Death";

	[SerializeField, Tooltip("비워 두면 같은 오브젝트의 Animator를 쓴다")]
	Animator animator;

	[Header("시간 (클립 길이에 맞춘다, 12fps)")]
	[SerializeField, Min(0f), Tooltip("Throw 클립 길이 (9프레임 = 0.75초)")]
	float throwDuration = 0.75f;
	[SerializeField, Min(0f), Tooltip("Slam 클립 길이 (9프레임 = 0.75초)")]
	float slamDuration = 0.75f;
	[SerializeField, Min(0f), Tooltip("Hit 클립 길이 (5프레임 ≈ 0.42초)")]
	float hitDuration = 0.42f;

	Monster monster;
	ProjectileAttack projectileAttack;
	GatewayAI ai;
	GatewayPhases phases;

	// 상태마다 끝나는 시각. 지금 시각보다 크면 그 상태를 보여 준다
	float attackEndTime;
	string attackState;
	float chargeEndTime;
	float groggyEndTime;
	float hitEndTime;
	bool dead;
	string currentState;

	void Awake()
	{
		if (animator == null) animator = GetComponent<Animator>();
		monster = GetComponent<Monster>();
		projectileAttack = GetComponent<ProjectileAttack>();
		ai = GetComponent<GatewayAI>();
		phases = GetComponent<GatewayPhases>();
	}

	void OnEnable()
	{
		if (monster != null)
		{
			monster.HitEffectPlayed += OnHit;
			monster.DeathEffectPlayed += OnDeath;
		}
		if (projectileAttack != null) projectileAttack.FireEffectPlayed += OnFire;
		if (ai != null)
		{
			ai.SummonEffectPlayed += OnSummon;
			ai.SlamEffectPlayed += OnSlam;
		}
		if (phases != null)
		{
			phases.ChargeEffectPlayed += OnCharge;
			phases.GroggyEffectPlayed += OnGroggy;
		}
	}

	void OnDisable()
	{
		if (monster != null)
		{
			monster.HitEffectPlayed -= OnHit;
			monster.DeathEffectPlayed -= OnDeath;
		}
		if (projectileAttack != null) projectileAttack.FireEffectPlayed -= OnFire;
		if (ai != null)
		{
			ai.SummonEffectPlayed -= OnSummon;
			ai.SlamEffectPlayed -= OnSlam;
		}
		if (phases != null)
		{
			phases.ChargeEffectPlayed -= OnCharge;
			phases.GroggyEffectPlayed -= OnGroggy;
		}
	}

	void Update()
	{
		Play(ChooseState());
	}

	/// <summary>
	/// 지금 재생할 상태를 고른다. 위에 있을수록 먼저다: 사망 > 그로기 > 모으기 > 공격(Throw·Slam) > 피격 > 대기
	/// 보스는 자주 맞으므로 피격 모션은 다른 동작이 없을 때만 보여 준다 (큰 동작이 끊기지 않게)
	/// </summary>
	string ChooseState()
	{
		float now = Time.time;
		if (dead) return DeathState;
		if (now < groggyEndTime) return GroggyState;
		if (now < chargeEndTime) return ChargeState;
		if (now < attackEndTime) return attackState;
		if (now < hitEndTime) return HitState;
		return IdleState;
	}

	void Play(string state)
	{
		// 같은 상태를 매 프레임 다시 틀면 첫 장면에서 멈춰 버리므로, 바뀔 때만 처음부터 재생한다
		if (state == currentState) return;
		currentState = state;
		animator.Play(state, 0, 0f);
	}

	void StartAttack(string state, float duration)
	{
		if (dead) return;
		attackState = state;
		attackEndTime = Time.time + duration;
		currentState = null; // 연달아 공격해도 처음부터 다시 튼다
	}

	void OnFire(Vector2 direction) => StartAttack(ThrowState, throwDuration);
	void OnSummon() => StartAttack(ThrowState, throwDuration);
	void OnSlam() => StartAttack(SlamState, slamDuration);

	void OnCharge(float seconds)
	{
		if (dead) return;
		chargeEndTime = Time.time + seconds;
		currentState = null;
	}

	void OnGroggy(float seconds)
	{
		if (dead) return;
		groggyEndTime = Time.time + seconds;
		chargeEndTime = 0f; // 그로기에 들어가면 모으던 자세는 끝낸다
		currentState = null;
	}

	void OnHit(int damage)
	{
		if (dead) return;
		hitEndTime = Time.time + hitDuration;
		if (ChooseStateIsIdleOrHit()) currentState = null; // 대기·피격 중에 맞으면 피격 모션을 처음부터
	}

	bool ChooseStateIsIdleOrHit()
	{
		string state = ChooseState();
		return state == HitState || state == IdleState;
	}

	void OnDeath()
	{
		// Death 클립(18프레임 = 1.5초)은 반복하지 않고 마지막 빈 장면에서 멈춘다. 오브젝트는 Monster의 Despawn Delay(1.5초) 뒤에 없어진다
		dead = true;
	}
}
