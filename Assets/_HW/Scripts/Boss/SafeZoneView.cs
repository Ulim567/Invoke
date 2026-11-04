using UnityEngine;

/// <summary>
/// 보스 전체 공격의 안전지대 그림 (빛나는 바닥 + 위로 올라가는 빛). 연출만 한다. SafeZone 프리팹(김도훈) 최상위에 붙인다.
/// <para>
/// - 자식 Glow(빛 기둥, 4×5칸)는 Glow Frames를 차례로 바꿔 빛 알갱이가 올라가게 하고, 밝기를 천천히 깜빡인다.<br/>
/// - 자식 Floor(바닥, 가로 4칸)는 같이 밝아졌다 어두워진다.<br/>
/// - 생기면 Fade In 동안 서서히 나타난다. 판정은 SafeZone이 한다 (monster.md 「GatewayPhases」).
/// </para>
/// </summary>
public class SafeZoneView : MonoBehaviour
{
	[SerializeField, Tooltip("빛 기둥 (자식 Glow)")]
	SpriteRenderer glow;

	[SerializeField, Tooltip("빛 기둥 장면들 (SafeZone_Glow_0~3)")]
	Sprite[] glowFrames;

	[SerializeField, Min(1f), Tooltip("빛 기둥 장면 바꾸는 빠르기 (초당 장면)")]
	float fps = 8f;

	[SerializeField, Tooltip("바닥 (자식 Floor)")]
	SpriteRenderer floor;

	[SerializeField, Min(0f), Tooltip("깜빡이는 빠르기")]
	float pulseSpeed = 4f;

	[SerializeField, Range(0f, 1f), Tooltip("깜빡일 때 어두워지는 정도")]
	float pulseDepth = 0.35f;

	[SerializeField, Min(0.01f), Tooltip("나타나는 시간(초)")]
	float fadeIn = 0.3f;

	float startTime;

	void OnEnable() => startTime = Time.time;

	void Update()
	{
		float age = Time.time - startTime;
		float appear = Mathf.Clamp01(age / fadeIn);
		float pulse = 1f - pulseDepth * (0.5f + 0.5f * Mathf.Sin(age * pulseSpeed));

		if (glow != null)
		{
			if (glowFrames != null && glowFrames.Length > 0) glow.sprite = glowFrames[(int)(age * fps) % glowFrames.Length];
			glow.color = new Color(1f, 1f, 1f, appear * pulse);
		}
		if (floor != null)
		{
			float light = Mathf.Lerp(0.75f, 1f, pulse);
			floor.color = new Color(light, light, light, appear);
		}
	}
}
