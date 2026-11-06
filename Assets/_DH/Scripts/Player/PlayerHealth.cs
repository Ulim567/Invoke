using System;
using Mirror;
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
/// 이벤트는 몬스터와 같이 로직용과 연출용으로 나뉜다. 로직용(Damaged, Died, Respawned, LobbyReturnRequested)은 서버에서만, 연출·표시용은 모든 화면에서 불린다.
/// </para>
/// <para>
/// 네트워크 (#310): 피해·사망·숨김·부활은 서버(또는 네트워크 없는 테스트)에서만 정한다.<br/>
/// - 상태(체력, 최대 체력, 죽음, 숨김)는 [SyncVar]로 보내 나중에 그 장소에 들어온 클라이언트도 받는다.<br/>
/// - 피격·사망·숨김·부활 연출은 [ClientRpc]로 보낸다. 받은 클라이언트는 상태를 맞추고 연출 이벤트를 부른다. 경직·무적은 받은 순간부터 센다.<br/>
/// - 그래서 클라이언트에서도 Hp, IsDead, IsHidden, CanAct가 서버와 같다 (이동·시전이 CanAct를 읽음). 호스트는 서버의 값과 이벤트를 그대로 쓴다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerHealth : NetworkBehaviour, IDamageable
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

	// 서버의 상태를 클라이언트에 보내는 칸 (#310). 서버는 상태가 바뀔 때마다 SyncState로 옮기고, 클라이언트는 받으면 아래 속성에 넣는다.
	// 속성과 따로 두는 이유: 클라이언트에서 [SyncVar]에 직접 쓰지 않고, 연출 알림([ClientRpc])으로도 같은 속성을 맞추기 위함
	[SyncVar(hook = nameof(OnSyncedHp))] int syncedHp;
	[SyncVar(hook = nameof(OnSyncedHp))] int syncedMaxHp;
	[SyncVar(hook = nameof(OnSyncedDead))] bool syncedDead;
	[SyncVar(hook = nameof(OnSyncedHidden))] bool syncedHidden;
	// 클라이언트에서 쓰는 최대 체력 (서버의 값). 다른 플레이어의 레벨이 이 화면에 맞게 와 있는지와 상관없이 서버 값을 쓰기 위함
	int clientMaxHp;

	/// <summary>지금 체력. 0이면 죽은 것이다.</summary>
	public int Hp { get; private set; }

	/// <summary>최대 체력. PlayerStats.MaxHp를 그대로 돌려준다. 클라이언트에서는 서버의 값이다.</summary>
	public int MaxHp => IsAuthority() ? stats.MaxHp : clientMaxHp;

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

	// 서버: 생성 메시지에 처음 체력이 담기도록 생성 전에 채운다 (OnStartServer는 클라이언트에 생성을 알리기 전에 불린다)
	public override void OnStartServer()
	{
		Hp = MaxHp;
		SyncState();
	}

	// 클라이언트: 생성 때 함께 온 서버의 상태를 넣는다. 호스트는 서버의 값을 그대로 쓴다
	public override void OnStartClient()
	{
		if (isServer) return;
		ApplySyncedState();
		ShowHp();
		// 이미 죽어 있거나 숨겨진 플레이어를 나중에 보게 된 경우: 연출 쪽(그림 숨기기 등)이 그 상태를 알도록 한 번 알린다
		if (IsDead) DeathEffectPlayed?.Invoke();
		if (IsHidden) HideEffectPlayed?.Invoke();
	}

	void Start()
	{
		// 클라이언트는 서버가 보낸 상태를 쓴다 (OnStartClient)
		if (!IsAuthority()) return;
		// 처음 나타난 위치(씬의 시작 위치)를 부활 위치로 기억한다
		respawnPosition = transform.position;
		Hp = MaxHp;
		SyncState();
		ShowHp();
	}

	void OnEnable() => stats.Changed += OnStatsChanged;

	void OnDisable() => stats.Changed -= OnStatsChanged;

	void Update()
	{
		// 숨김·부활 시각은 서버만 센다. 클라이언트는 서버의 알림을 기다린다
		if (!IsDead || !IsAuthority()) return;

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

		SyncState();
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
		SyncState();

		Died?.Invoke();
		PlayDeathEffect();
	}

	void Hide()
	{
		IsHidden = true;
		SyncState();
		PlayHideEffect();
	}

	void Respawn()
	{
		IsDead = false;
		IsHidden = false;
		Hp = MaxHp;
		invincibleEndTime = Time.time + invincibleTime;
		SyncState();

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
		// 클라이언트의 최대 체력은 서버의 값(syncedMaxHp)을 쓴다
		if (type != StatType.MaxHp || !IsAuthority()) return;

		// 최대 체력이 줄어 지금 체력보다 작아지면 맞춘다. 늘어날 때는 회복하지 않는다
		if (Hp > MaxHp) Hp = MaxHp;
		SyncState();
		ShowHp();
	}

	// ── 네트워크 (#310) ──

	// 서버: 바뀐 상태를 [SyncVar] 칸에 옮긴다. 값이 같으면 Mirror가 보내지 않는다. 네트워크를 켜지 않은 테스트에서는 칸에만 들어간다
	void SyncState()
	{
		syncedHp = Hp;
		syncedMaxHp = MaxHp;
		syncedDead = IsDead;
		syncedHidden = IsHidden;
	}

	// 클라이언트: 받은 상태를 속성에 넣는다
	void ApplySyncedState()
	{
		Hp = syncedHp;
		clientMaxHp = syncedMaxHp;
		IsDead = syncedDead;
		IsHidden = syncedHidden;
	}

	void OnSyncedHp(int _, int __)
	{
		if (isServer) return;
		Hp = syncedHp;
		clientMaxHp = syncedMaxHp;
		ShowHp();
	}

	void OnSyncedDead(bool _, bool dead)
	{
		if (!isServer) IsDead = dead;
	}

	void OnSyncedHidden(bool _, bool hidden)
	{
		if (!isServer) IsHidden = hidden;
	}

	// 서버이거나 네트워크를 켜지 않은 테스트면 true
	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;

	// 아래 함수들은 표시·연출만 한다 (코드 작성 규칙 1). 서버(호스트 포함)는 그 자리에서 이벤트를 부르고,
	// 다른 클라이언트에는 [ClientRpc]로 알린다. 받은 클라이언트는 상태를 맞춘 뒤 같은 이벤트를 부른다
	void ShowHp() => HpChanged?.Invoke(Hp, MaxHp);

	void PlayHitEffect(int damage)
	{
		HitEffectPlayed?.Invoke(damage);
		if (NetworkServer.active && isServer) RpcHit(damage, Hp);
	}

	void PlayDeathEffect()
	{
		DeathEffectPlayed?.Invoke();
		if (NetworkServer.active && isServer) RpcDeath();
	}

	void PlayHideEffect()
	{
		HideEffectPlayed?.Invoke();
		if (NetworkServer.active && isServer) RpcHide();
	}

	void PlayRespawnEffect()
	{
		RespawnEffectPlayed?.Invoke();
		if (NetworkServer.active && isServer) RpcRespawn(Hp);
	}

	// 경직·무적은 받은 순간부터 센다. 이동·시전(CanAct)이 서버와 거의 같은 때 막힌다
	[ClientRpc]
	void RpcHit(int damage, int hpAfter)
	{
		if (isServer) return;
		Hp = hpAfter;
		stunEndTime = Time.time + hitStunTime;
		invincibleEndTime = stunEndTime + invincibleTime;
		ShowHp();
		HitEffectPlayed?.Invoke(damage);
	}

	[ClientRpc]
	void RpcDeath()
	{
		if (isServer) return;
		Hp = 0;
		IsDead = true;
		stunEndTime = 0f;
		invincibleEndTime = 0f;
		ShowHp();
		DeathEffectPlayed?.Invoke();
	}

	[ClientRpc]
	void RpcHide()
	{
		if (isServer) return;
		IsHidden = true;
		HideEffectPlayed?.Invoke();
	}

	[ClientRpc]
	void RpcRespawn(int hp)
	{
		if (isServer) return;
		IsDead = false;
		IsHidden = false;
		Hp = hp;
		invincibleEndTime = Time.time + invincibleTime;
		ShowHp();
		RespawnEffectPlayed?.Invoke();
	}
}
