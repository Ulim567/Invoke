using UnityEngine;

/// <summary>
/// PlayerHealth의 연출 이벤트를 받아 캐릭터 그림을 깜빡이고, 숨기고, 다시 보이는 컴포넌트. 연출만 한다.
/// <para>
/// - 피격: 경직(HitStunTime) 동안은 그대로 두고(피격 모션), 이후 무적(InvincibleTime) 동안 깜빡인다.<br/>
/// - 사망: 숨길 때 그림을 끈다. 부활할 때 다시 켜고 무적 동안 깜빡인다.<br/>
/// - 피격·사망 애니메이션 재생은 하지 않는다(임혜원 「플레이어 상태에 맞춰 애니메이션 재생하기」).
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerHealthEffects : MonoBehaviour
{
	[SerializeField, Tooltip("깜빡일 그림. 비워 두면 같은 오브젝트의 SpriteRenderer를 쓴다")]
	SpriteRenderer spriteRenderer;

	[SerializeField, Min(0.02f), Tooltip("깜빡임 한 번(꺼졌다 켜짐)의 반 주기(초)")]
	float blinkInterval = 0.1f;

	PlayerHealth health;
	float blinkStartTime;
	float blinkEndTime;
	bool hidden;

	void Awake()
	{
		health = GetComponent<PlayerHealth>();
		if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
	}

	void OnEnable()
	{
		health.HitEffectPlayed += OnHit;
		health.HideEffectPlayed += OnHide;
		health.RespawnEffectPlayed += OnRespawn;
	}

	void OnDisable()
	{
		health.HitEffectPlayed -= OnHit;
		health.HideEffectPlayed -= OnHide;
		health.RespawnEffectPlayed -= OnRespawn;
		blinkEndTime = 0f;
		if (spriteRenderer != null) spriteRenderer.enabled = !hidden;
	}

	void Update()
	{
		if (hidden || blinkEndTime <= 0f) return;

		float now = Time.time;
		if (now >= blinkEndTime)
		{
			// 깜빡임이 끝나면 반드시 보이는 상태로 돌린다
			blinkEndTime = 0f;
			spriteRenderer.enabled = true;
			return;
		}
		if (now < blinkStartTime) return;

		// 시작 시각부터 blinkInterval마다 꺼짐·켜짐을 번갈아 한다
		spriteRenderer.enabled = ((int)((now - blinkStartTime) / blinkInterval) & 1) == 1;
	}

	void OnHit(int damage) => StartBlink(Time.time + health.HitStunTime);

	void OnHide()
	{
		hidden = true;
		blinkEndTime = 0f;
		spriteRenderer.enabled = false;
	}

	void OnRespawn()
	{
		hidden = false;
		spriteRenderer.enabled = true;
		StartBlink(Time.time);
	}

	void StartBlink(float startTime)
	{
		blinkStartTime = startTime;
		blinkEndTime = startTime + health.InvincibleTime;
	}
}
