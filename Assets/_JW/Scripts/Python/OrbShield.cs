using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 구슬 보호막 (py.orb_shield). 켜져 있는 동안 다음 피해 한 번을 막고 사라진다. 파이썬 플레이어 프리팹에 PlayerHealth와 함께 붙인다.
/// <para>
/// - 켜기: 구슬 보호막 스킬이 서버에서 Activate(초)를 부른다. 이미 켜져 있으면 시간만 새로 시작한다 (겹치지 않음).<br/>
/// - 막기: PlayerHealth.DamageBlocking을 받아, 켜져 있으면 그 피해를 막고(true) 꺼진다. 시간이 다 돼도 꺼진다.<br/>
/// - 보호막 상태는 이 컴포넌트만 바꾼다 (코드 작성 규칙 2). 다른 스크립트는 IsActive를 읽는다.<br/>
/// - 네트워크: 켜짐 여부는 [SyncVar]로 보내고, Changed가 모든 화면에서 불린다 (보호막 그림). 막은 순간은 [ClientRpc]로 BlockEffectPlayed를 낸다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class OrbShield : NetworkBehaviour
{
	[SyncVar(hook = nameof(OnSyncedActive))] bool syncedActive;
	bool active;
	float endTime;
	PlayerHealth health;

	static bool IsClientOnly => NetworkClient.active && !NetworkServer.active;

	/// <summary>보호막이 켜져 있으면 true. 클라이언트에서는 서버가 보낸 값이다.</summary>
	public bool IsActive => IsClientOnly ? syncedActive : active;

	/// <summary>[표시] 보호막이 켜지거나 꺼졌을 때 (켜졌으면 true). 모든 화면에서 불린다.</summary>
	public event Action<bool> Changed;

	/// <summary>[표시] 보호막이 피해를 막은 순간. 모든 화면에서 불린다.</summary>
	public event Action BlockEffectPlayed;

	void Awake() => health = GetComponent<PlayerHealth>();

	void OnEnable()
	{
		if (health != null) health.DamageBlocking += OnDamageBlocking;
	}

	void OnDisable()
	{
		if (health != null) health.DamageBlocking -= OnDamageBlocking;
	}

	/// <summary>
	/// [서버] 보호막을 seconds초 동안 켠다. 이미 켜져 있으면 시간만 새로 시작한다. 구슬 보호막 스킬이 부른다.
	/// </summary>
	public void Activate(float seconds)
	{
		if (IsClientOnly || seconds <= 0f) return;
		endTime = Time.time + seconds;
		SetActive(true);
	}

	void Update()
	{
		if (IsClientOnly || !active) return;
		if (Time.time >= endTime) SetActive(false);
	}

	// [서버] 피해가 들어오기 직전. 켜져 있으면 막고 꺼진다
	bool OnDamageBlocking(int damage)
	{
		if (!active) return false;
		SetActive(false);
		BlockEffectPlayed?.Invoke();
		if (isServer) RpcBlocked();
		return true;
	}

	void SetActive(bool value)
	{
		if (active == value) return;
		active = value;
		if (NetworkServer.active) syncedActive = value;
		Changed?.Invoke(value);
	}

	// [클라이언트] 서버가 보낸 값이 바뀜. 서버·Host는 SetActive에서 이미 알렸다
	void OnSyncedActive(bool _, bool value)
	{
		if (isServer) return;
		Changed?.Invoke(value);
	}

	[ClientRpc]
	void RpcBlocked()
	{
		if (isServer) return;
		BlockEffectPlayed?.Invoke();
	}
}
