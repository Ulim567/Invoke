using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어의 체력·피격·사망·부활을 맡는 컴포넌트. 체력은 이 컴포넌트만 바꾼다.
/// <para>
/// 흐름 (시간은 임혜원과 정한 값, #28)<br/>
/// - 피격: 경직 hitStunTime초(피격 모션) → 무적 invincibleTime초(깜빡임). 피격 순간부터 둘이 끝날 때까지 다시 맞지 않는다.<br/>
/// - 사망: 체력 0 → 사망 모션 → hideDelay초 뒤 숨김(관전으로 전환) → respawnDelay초 뒤 최대 체력으로 부활(직후 무적 깜빡임).<br/>
/// - 부활 시각이 되면 먼저 RespawnDue로 알린다. 서버에서는 씬 이동 담당이 받아, 옮길 곳이 있으면 DeferRespawn()으로 부활을 미루고 옮긴 직후(로딩 화면이 덮인 채) CompleteRespawn()으로 살린다 (#310, #391).<br/>
/// - 아무도 미루지 않으면 그 자리에서 바로 부활한다. 미뤄진 채 maxDeferTime초가 지나도 그 자리에서 부활한다(안전장치). 네트워크를 켜지 않은 테스트에서는 처음 나타난 위치(시작 위치)에서 부활한다.<br/>
/// - AutoRespawn이 꺼져 있으면(레이드) 부활하지 않고 숨긴 채로 둔다.
/// </para>
/// <para>
/// 이벤트는 몬스터와 같이 로직용과 연출용으로 나뉜다. 로직용(Damaged, Died, Respawned, RespawnDue)은 서버에서만, 연출·표시용은 모든 화면에서 불린다.
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

	[SerializeField, Min(0f), Tooltip("부활을 미룬 채 기다리는 최대 시간(초). 넘으면 그 자리에서 부활한다. 로딩 화면 답을 기다리는 시간(5초)보다 길게 둔다")]
	float maxDeferTime = 10f;

	PlayerStats stats;
	Rigidbody2D body;
	Vector2 respawnPosition;
	float stunEndTime;
	float invincibleEndTime;
	float hideTime;
	float respawnTime;
	// 이번 죽음에서 RespawnDue를 이미 알렸는지, 받은 쪽이 부활을 미뤘는지 (서버)
	bool respawnDueNotified;
	bool respawnDeferred;

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

	/// <summary>
	/// 사망 후 부활하기까지 시간(초). 레이드처럼 부활하지 않는 곳에서도 「부활했을 시간」을 맞출 때 읽는다.<br/>
	/// 사망 화면 효과(DeathScreenEffect, #424)가 레이드에서 죽었을 때 효과를 끝낼 시각으로 쓴다.
	/// </summary>
	public float RespawnDelay => respawnDelay;

	/// <summary>[로직] 피해를 받아 체력이 줄었을 때 불린다. 받은 데미지를 넘긴다.</summary>
	public event Action<int> Damaged;

	/// <summary>[로직] 체력이 0이 된 순간 한 번 불린다. 레이드 실패 판정(파티 전원 사망) 등에 쓴다.</summary>
	public event Action Died;

	/// <summary>[로직] 부활한 순간 불린다.</summary>
	public event Action Respawned;

	/// <summary>
	/// [로직] 부활할 때가 되었을 때 죽음마다 한 번 불린다. 받은 쪽이 이 안에서 DeferRespawn()을 부르면 죽은 채로 기다리고, 부르지 않으면 바로 부활한다.<br/>
	/// 씬 이동 담당(이우림)이 받아 옮길 곳이 있으면 미룬다 (#391).
	/// </summary>
	public event Action RespawnDue;

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
		if (!IsAuthority())
		{
			RegisterInRoster();
			return;
		}
		// 처음 나타난 위치(씬의 시작 위치)를 부활 위치로 기억한다
		respawnPosition = transform.position;
		Hp = MaxHp;
		SyncState();
		ShowHp();
		RegisterInRoster();
	}

	// 이 화면의 플레이어 목록(PlayerRoster)에서 뺀다. 클라이언트에서는 장소를 떠나거나 접속이 끊겨 Mirror가 오브젝트를 없앨 때다
	void OnDestroy() => PlayerRoster.Unregister(this);

	// 이 화면의 플레이어 목록(PlayerRoster)에 넣는다 (#310). 체력 값이 맞춰진 뒤(Start)에 넣어, 목록을 받은 UI가 처음부터 맞는 체력을 읽게 한다.
	// 플레이어 하나에 PlayerHealth가 둘 붙은 변형 프리팹(Player_HW, Player_JW)이 있어서, 첫 번째 것만 넣어 한 사람이 두 번 들어가지 않게 한다
	void RegisterInRoster()
	{
		if (GetComponent<PlayerHealth>() == this) PlayerRoster.Register(this);
	}

	void OnEnable() => stats.Changed += OnStatsChanged;

	void OnDisable() => stats.Changed -= OnStatsChanged;

	void Update()
	{
		// 숨김·부활 시각은 서버만 센다. 클라이언트는 서버의 알림을 기다린다
		if (!IsDead || !IsAuthority()) return;

		float now = Time.time;
		if (!IsHidden && now >= hideTime) Hide();
		if (!AutoRespawn || now < respawnTime) return;

		// 부활할 때가 되면 먼저 알려, 받은 쪽이 미룰 기회를 준다
		if (!respawnDueNotified)
		{
			respawnDueNotified = true;
			RespawnDue?.Invoke();
		}
		// 미뤄진 채 너무 오래 지나면 그 자리에서 살린다 (옮기기가 끝내 안 되어도 죽은 채로 갇히지 않게 하는 안전장치)
		if (!respawnDeferred || now >= respawnTime + maxDeferTime) Respawn();
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

	/// <summary>
	/// [서버] RespawnDue를 받은 쪽이 부활을 미룬다. 죽은 채로 기다리다가 CompleteRespawn()이 불리면 살아난다.<br/>
	/// maxDeferTime초가 지나도록 CompleteRespawn()이 불리지 않으면 그 자리에서 살아난다. 죽어 있지 않으면 무시한다.
	/// </summary>
	public void DeferRespawn()
	{
		if (IsDead) respawnDeferred = true;
	}

	/// <summary>
	/// [서버] 무적·경직과 상관없이 바로 사망시킨다. 레이드가 실패로 끝날 때 RaidJudge가 던전의 살아 있는 플레이어 모두에게 부른다 (#428).<br/>
	/// 죽는 순서는 체력이 0이 될 때와 같다 (Died → 사망 연출 → 숨김 → 부활 시각에 RespawnDue). 이미 죽어 있으면 무시한다.
	/// </summary>
	public void Kill()
	{
		if (!IsAuthority() || IsDead) return;

		Hp = 0;
		SyncState();
		ShowHp();
		Die();
	}

	/// <summary>
	/// [서버] 살아 있으면 체력을 최대 체력으로 채운다. 로비에 도착했을 때 InvokeNetworkManager가 부른다 (#435).<br/>
	/// 죽어 있으면 무시한다 (부활할 때 최대 체력으로 살아난다). 클라이언트 체력 바는 [SyncVar]로 맞춰진다.
	/// </summary>
	public void RestoreFullHp()
	{
		if (!IsAuthority() || IsDead || Hp >= MaxHp) return;

		Hp = MaxHp;
		SyncState();
		ShowHp();
	}

	/// <summary>
	/// [서버] 미뤄 둔 부활을 실행한다. 옮기기가 끝난 직후 불러, 도착한 장소에서 살아나게 한다. 죽어 있지 않으면 무시한다.
	/// </summary>
	public void CompleteRespawn()
	{
		if (IsDead && IsAuthority()) Respawn();
	}

	void Die()
	{
		float now = Time.time;
		IsDead = true;
		respawnDueNotified = false;
		respawnDeferred = false;
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
		respawnDeferred = false;
		Hp = MaxHp;
		invincibleEndTime = Time.time + invincibleTime;
		SyncState();

		// 네트워크를 켜지 않은 테스트에서만 처음 위치로 옮긴다. 서버에서는 위치를 소유 클라이언트가 정하므로 여기서 옮겨도 덮어써진다
		if (!NetworkServer.active) MoveTo(respawnPosition);

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
