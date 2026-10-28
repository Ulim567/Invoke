using UnityEngine;

/// <summary>
/// 테스트용. 피격·사망·부활 이벤트를 로그로 보여 주고, Inspector의 컴포넌트 메뉴(⋮)에서 데미지를 주거나 자동 부활을 끄고 켠다.
/// PR 전에 지운다.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerHealthTester : MonoBehaviour
{
	PlayerHealth health;

	void Awake() => health = GetComponent<PlayerHealth>();

	void OnEnable()
	{
		health.Damaged += LogDamaged;
		health.Died += LogDied;
		health.HideEffectPlayed += LogHidden;
		health.LobbyReturnRequested += LogLobby;
		health.Respawned += LogRespawned;
	}

	void OnDisable()
	{
		health.Damaged -= LogDamaged;
		health.Died -= LogDied;
		health.HideEffectPlayed -= LogHidden;
		health.LobbyReturnRequested -= LogLobby;
		health.Respawned -= LogRespawned;
	}

	[ContextMenu("Take 30 Damage")]
	void Take30() => health.TakeDamage(30);

	[ContextMenu("Take 999 Damage (Kill)")]
	void Kill() => health.TakeDamage(999);

	[ContextMenu("Toggle Auto Respawn")]
	void ToggleAutoRespawn()
	{
		health.SetAutoRespawn(!health.AutoRespawn);
		Debug.Log($"[{name}] 자동 부활: {health.AutoRespawn}");
	}

	void LogDamaged(int damage) => Debug.Log($"[{name}] {damage} 피해 → 체력 {health.Hp}/{health.MaxHp} (시각 {Time.time:F2})");

	void LogDied() => Debug.Log($"[{name}] 사망 (시각 {Time.time:F2})");

	void LogHidden() => Debug.Log($"[{name}] 숨김, 관전 전환 (시각 {Time.time:F2})");

	void LogLobby() => Debug.Log($"[{name}] 로비로 보내 달라는 요청");

	void LogRespawned() => Debug.Log($"[{name}] 부활 → 체력 {health.Hp}/{health.MaxHp} (시각 {Time.time:F2})");
}
