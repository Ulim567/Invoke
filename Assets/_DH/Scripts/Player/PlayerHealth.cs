using System;
using UnityEngine;

/// <summary>
/// 플레이어의 체력·피격·사망·부활을 맡는 컴포넌트. 체력은 이 컴포넌트만 바꾼다.
/// <para>
/// 흐름 (시간은 임혜원과 정한 값, #28)<br/>
/// - 피격: 경직 hitStunTime초(피격 모션) → 무적 invincibleTime초(깜빡임). 피격 순간부터 둘이 끝날 때까지 다시 맞지 않는다.<br/>
/// - 사망: 체력 0 → 사망 모션 → hideDelay초 뒤 숨김(관전으로 전환) → respawnDelay초 뒤 최대 체력으로 부활(직후 무적 깜빡임).<br/>
/// - 부활할 때 로비로 보내 달라고 LobbyReturnRequested로 알린다. 씬 이동이 생기기 전까지는 처음 나타난 위치(시작 위치)에서 부활한다.<br/>
/// - AutoRespawn이 꺼져 있으면(레이드) 부활하지 않고 숨긴 채로 둔다.
/// </para>
/// <para>
/// 이벤트는 몬스터와 같이 로직용과 연출용으로 나뉜다. 네트워크를 붙이면 로직용은 서버에서만, 연출용은 각 플레이어 화면에서만 불린다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
	[SerializeField, Min(0f), Tooltip("맞았을 때 경직 시간(초). 피격 모션 길이")]
	float hitStunTime = 0.3f;

	[SerializeField, Min(0f), Tooltip("경직이 끝난 뒤 무적 시간(초). 이 동안 깜빡인다")]
	float invincibleTime = 1f;

	[SerializeField, Min(0f), Tooltip("사망 후 캐릭터를 숨기기까지 시간(초). 사망 모션 길이")]
	float hideDelay = 1f;

	[SerializeField, Min(0f), Tooltip("사망 후 부활하기까지 시간(초)")]
	float respawnDelay = 3f;

	PlayerStats stats;
	Rigidbody2D body;
	Vector2 respawnPosition;
	float stunEndTime;
	float invincibleEndTime;
	float hideTime;
	float respawnTime;

	/// <summary>지금 체력. 0이면 죽은 것이다.</summary>
	public int Hp { get; private set; }

	/// <summary>최대 체력. PlayerStats.MaxHp를 그대로 돌려준다.</summary>
	public int MaxHp => stats.MaxHp;

	/// <summary>죽었으면 true. 부활하면 false가 된다.</summary>
	public bool IsDead { get; private set; }

	/// <summary>사망 후 숨겨진 상태(관전 중)면 true.</summary>
	public bool IsHidden { get; private set; }

	/// <summary>경직 중이면 true.</summary>
	public bool IsStunned => Time.time < stunEndTime;

	/// <summary>피해를 받지 않는 상태(경직, 무적)면 true.</summary>
	public bool IsInvincible => Time.time < invincibleEndTime;

	/// <summary>
	/// 움직이거나 함수를 시전할 수 있으면 true. 경직 중이거나 죽었으면 false.<br/>
	/// 이동(PlayerMove)과 시전 담당이 이 값을 읽어 막는다.
	/// </summary>
	public bool CanAct => !IsDead && !IsStunned;

	/// <summary>
	/// 죽었을 때 자동으로 부활하는지. 기본은 true이고, 레이드에 들어갈 때 SetAutoRespawn(false)로 끈다.
	/// </summary>
	public bool AutoRespawn { get; private set; } = true;

	/// <summary>경직 시간(초). 연출이 피격 모션 길이를 맞출 때 읽는다.</summary>
	public float HitStunTime => hitStunTime;

	/// <summary>경직 뒤 무적 시간(초). 연출이 깜빡임 길이를 맞출 때 읽는다.</summary>
	public float InvincibleTime => invincibleTime;

	/// <summary>[로직] 피해를 받아 체력이 줄었을 때 불린다. 받은 데미지를 넘긴다.</summary>
	public event Action<int> Damaged;

	/// <summary>[로직] 체력이 0이 된 순간 한 번 불린다. 레이드 실패 판정(파티 전원 사망) 등에 쓴다.</summary>
	public event Action Died;

	/// <summary>[로직] 부활한 순간 불린다.</summary>
	public event Action Respawned;

	/// <summary>[로직] 부활할 때 로비(마을)로 보내 달라는 요청. 씬 이동 담당(이우림)이 받는다.</summary>
	public event Action LobbyReturnRequested;

	/// <summary>[표시] 체력이나 최대 체력이 바뀌었을 때 불린다. (지금 체력, 최대 체력)을 넘긴다. 체력 바에 쓴다.</summary>
	public event Action<int, int> HpChanged;

	/// <summary>[연출] 맞았을 때 불린다. 피격 모션, 이후 무적 깜빡임을 시작한다. 죽는 공격이면 대신 DeathEffectPlayed만 불린다.</summary>
	public event Action<int> HitEffectPlayed;

	/// <summary>[연출] 죽었을 때 불린다. 사망 모션을 재생한다.</summary>
	public event Action DeathEffectPlayed;

	/// <summary>[연출] 사망 후 숨길 때 불린다. 캐릭터를 숨기고 카메라를 관전으로 넘긴다.</summary>
	public event Action HideEffectPlayed;

	/// <summary>[연출] 부활할 때 불린다. 캐릭터를 다시 보이고, 카메라를 내 캐릭터로 돌리고, 무적 깜빡임을 시작한다.</summary>
	public event Action RespawnEffectPlayed;

	void Awake()
	{
		stats = GetComponent<PlayerStats>();
		body = GetComponent<Rigidbody2D>();
	}

	void Start()
	{
		// 처음 나타난 위치(씬의 시작 위치)를 부활 위치로 기억한다
		respawnPosition = transform.position;
		Hp = MaxHp;
		ShowHp();
	}

	void OnEnable() => stats.Changed += OnStatsChanged;

	void OnDisable() => stats.Changed -= OnStatsChanged;

	void Update()
	{
		if (!IsDead) return;

		float now = Time.time;
		if (!IsHidden && now >= hideTime) Hide();
		if (AutoRespawn && now >= respawnTime) Respawn();
	}

	/// <summary>
	/// damage만큼 체력을 줄인다. 0 이하이거나, 죽었거나, 경직·무적 중이면 무시한다. 체력이 0이 되면 사망한다.
	/// </summary>
	public void TakeDamage(int damage)
	{
		if (damage <= 0 || IsDead || IsInvincible) return;

		float now = Time.time;
		Hp = Mathf.Max(Hp - damage, 0);
		stunEndTime = now + hitStunTime;
		invincibleEndTime = stunEndTime + invincibleTime;

		Damaged?.Invoke(damage);
		ShowHp();

		// 죽는 공격이면 피격 모션·깜빡임 대신 사망 모션만 재생한다
		if (Hp == 0) Die();
		else PlayHitEffect(damage);
	}

	/// <summary>
	/// 죽었을 때 자동으로 부활할지 정한다. 레이드에 들어갈 때 false, 나올 때 true로 바꾼다.<br/>
	/// 이미 죽어 있는 상태에서 true로 바꾸면 부활 시각이 지났을 경우 바로 부활한다.
	/// </summary>
	public void SetAutoRespawn(bool value) => AutoRespawn = value;

	void Die()
	{
		float now = Time.time;
		IsDead = true;
		// 사망 중에는 경직·무적을 쓰지 않는다
		stunEndTime = 0f;
		invincibleEndTime = 0f;
		hideTime = now + hideDelay;
		respawnTime = now + respawnDelay;

		Died?.Invoke();
		PlayDeathEffect();
	}

	void Hide()
	{
		IsHidden = true;
		PlayHideEffect();
	}

	void Respawn()
	{
		IsDead = false;
		IsHidden = false;
		Hp = MaxHp;
		invincibleEndTime = Time.time + invincibleTime;

		LobbyReturnRequested?.Invoke();
		// 씬 이동이 생기기 전까지는 시작 위치로 옮긴다. 씬 이동이 생기면 로비의 시작 위치로 옮겨진다
		MoveTo(respawnPosition);

		Respawned?.Invoke();
		ShowHp();
		PlayRespawnEffect();
	}

	void MoveTo(Vector2 position)
	{
		if (body != null)
		{
			body.linearVelocity = Vector2.zero;
			body.position = position;
		}
		transform.position = position;
	}

	void OnStatsChanged(StatType type)
	{
		if (type != StatType.MaxHp) return;

		// 최대 체력이 줄어 지금 체력보다 작아지면 맞춘다. 늘어날 때는 회복하지 않는다
		if (Hp > MaxHp) Hp = MaxHp;
		ShowHp();
	}

	// 아래 함수들은 표시·연출만 한다. 네트워크를 붙이면 Hp는 [SyncVar]의 hook에서, 연출은 [ClientRpc]로 부른다 (코드 작성 규칙 1)
	void ShowHp() => HpChanged?.Invoke(Hp, MaxHp);

	void PlayHitEffect(int damage) => HitEffectPlayed?.Invoke(damage);

	void PlayDeathEffect() => DeathEffectPlayed?.Invoke();

	void PlayHideEffect() => HideEffectPlayed?.Invoke();

	void PlayRespawnEffect() => RespawnEffectPlayed?.Invoke();
}
