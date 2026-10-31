using UnityEngine;

/// <summary>
/// 몬스터 상태(이동·발사·피격·사망)를 읽어서 알맞은 애니메이션을 재생하는 컴포넌트. 연출만 한다.
/// <para>
/// - 값을 바꾸지 않고 읽기만 한다. 네트워크를 붙이면 상태만 동기화되고, 이 컴포넌트는 각 화면에서 그대로 돈다.<br/>
/// - Animator Controller에는 상태만 두고 화살표(Transition)는 만들지 않는다. 어떤 상태를 재생할지는 이 스크립트가 정한다.<br/>
///   Leak: Move, Hit, Death / Loop: Hover, Shoot, Hit, Death<br/>
/// - 읽는 곳 (김도훈)<br/>
///   · 피격·사망: <c>Monster.HitEffectPlayed</c>, <c>DeathEffectPlayed</c><br/>
///   · 이동: <c>GroundChaseAI.IsMoving</c>. 멈춰 있으면 기본 상태를 천천히 튼다. GroundChaseAI가 없으면(날아다니는 몬스터) 늘 보통 속도<br/>
///   · 발사: <c>ProjectileAttack.FireEffectPlayed</c>. 투사체는 Fire Delay(Loop 0.25초) 뒤에 나가서 Shoot 모션 4번째 프레임과 맞는다<br/>
/// - 좌우 뒤집기(flipX)는 하지 않는다. 몬스터 AI가 한다.
/// </para>
/// </summary>
[RequireComponent(typeof(Animator))]
public class MonsterAnimator : MonoBehaviour
{
	[SerializeField, Tooltip("비워 두면 같은 오브젝트의 Animator를 쓴다")]
	Animator animator;

	[Header("상태 이름 (Animator Controller와 같게)")]
	[SerializeField, Tooltip("평소 상태. Leak은 Move, Loop는 Hover")]
	string baseState = "Move";
	[SerializeField] string hitState = "Hit";
	[SerializeField] string deathState = "Death";
	[SerializeField, Tooltip("발사 상태. 쏘지 않는 몬스터는 비워 둔다")]
	string shootState = "";

	[Header("시간 (클립 길이에 맞춘다)")]
	[SerializeField, Min(0f), Tooltip("피격 모션을 보여 주는 시간(초). Hit 클립 길이 (5프레임 ÷ 12fps ≈ 0.42)")]
	float hitDuration = 0.42f;
	[SerializeField, Min(0f), Tooltip("발사 모션을 보여 주는 시간(초). Shoot 클립 길이 (7프레임 ÷ 12fps ≈ 0.58)")]
	float shootDuration = 0.58f;

	[SerializeField, Range(0f, 1f), Tooltip("순찰하다 멈춰 있을 때 기본 상태를 트는 빠르기. 1이면 보통, 0이면 멈춤")]
	float idleSpeed = 0.35f;

	Monster monster;
	GroundChaseAI chase;
	ProjectileAttack projectileAttack;

	float hitEndTime;
	float shootEndTime;
	bool dead;
	string currentState;

	void Awake()
	{
		if (animator == null) animator = GetComponent<Animator>();
		monster = GetComponent<Monster>();
		chase = GetComponent<GroundChaseAI>();
		projectileAttack = GetComponent<ProjectileAttack>();
	}

	void OnEnable()
	{
		if (monster != null)
		{
			monster.HitEffectPlayed += OnHit;
			monster.DeathEffectPlayed += OnDeath;
		}
		if (projectileAttack != null) projectileAttack.FireEffectPlayed += OnFire;
	}

	void OnDisable()
	{
		if (monster != null)
		{
			monster.HitEffectPlayed -= OnHit;
			monster.DeathEffectPlayed -= OnDeath;
		}
		if (projectileAttack != null) projectileAttack.FireEffectPlayed -= OnFire;
	}

	void Update()
	{
		string state = ChooseState();
		Play(state);

		// 멈춰서 기다릴 때는 기본 상태를 천천히 틀어 숨 쉬는 것처럼 보이게 한다. 다른 상태는 보통 속도
		bool idle = state == baseState && chase != null && !chase.IsMoving;
		animator.speed = idle ? idleSpeed : 1f;
	}

	// ProjectileAttack이 쏘기 시작할 때 (투사체는 Fire Delay 뒤에 나간다)
	void OnFire(Vector2 direction) => PlayShoot();

	/// <summary>
	/// 발사 모션을 튼다. 연출만 하고 투사체는 만들지 않는다.
	/// ProjectileAttack이 있으면 FireEffectPlayed를 받아 저절로 불리므로, 보통은 따로 부를 일이 없다.
	/// </summary>
	public void PlayShoot()
	{
		if (dead || string.IsNullOrEmpty(shootState)) return;
		shootEndTime = Time.time + shootDuration;
		currentState = null; // 연달아 쏴도 처음부터 다시 튼다
	}

	/// <summary>
	/// 지금 재생할 상태를 고른다. 위에 있을수록 먼저다: 사망 > 피격 > 발사 > 기본
	/// </summary>
	string ChooseState()
	{
		float now = Time.time;
		if (dead) return deathState;
		if (now < hitEndTime) return hitState;
		if (now < shootEndTime && !string.IsNullOrEmpty(shootState)) return shootState;
		return baseState;
	}

	void Play(string state)
	{
		// 같은 상태를 매 프레임 다시 틀면 첫 장면에서 멈춰 버리므로, 바뀔 때만 처음부터 재생한다
		if (state == currentState) return;
		currentState = state;
		animator.Play(state, 0, 0f);
	}

	void OnHit(int damage)
	{
		if (dead) return;
		hitEndTime = Time.time + hitDuration;
		currentState = null; // 연달아 맞아도 피격 모션을 처음부터 다시 튼다
	}

	void OnDeath()
	{
		// Death 클립은 반복하지 않고 마지막 빈 장면에서 멈춘다. 오브젝트는 Monster의 Despawn Delay 뒤에 없어진다
		dead = true;
	}
}
