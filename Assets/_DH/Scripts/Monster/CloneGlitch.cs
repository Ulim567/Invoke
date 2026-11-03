using UnityEngine;

/// <summary>
/// 보스 분신(가짜)의 구분 힌트. 가끔 아주 짧게 색이 바뀌는 "지지직"을 보여 준다. 연출만 하고 값은 바꾸지 않는다.
/// <para>
/// 사용법: 분신 프리팹 최상위(SpriteRenderer와 같은 곳)에 붙인다. 진짜 보스에는 붙이지 않는다.<br/>
/// 위치는 건드리지 않는다(판정·동기화에 영향이 없게). 각 화면에서 스스로 돈다 (보여 주기 전용).
/// </para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CloneGlitch : MonoBehaviour
{
	[SerializeField, Tooltip("지지직 사이 간격(초). x 최소, y 최대")]
	Vector2 interval = new Vector2(2f, 3f);

	[SerializeField, Min(0.02f), Tooltip("지지직 한 번의 길이(초)")]
	float duration = 0.1f;

	[SerializeField, Tooltip("지지직 동안 그림에 곱할 색")]
	Color glitchColor = new Color(0.6f, 1f, 1f, 0.6f);

	SpriteRenderer spriteRenderer;
	Color baseColor;
	float nextTime;
	float endTime;
	bool glitching;

	void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		baseColor = spriteRenderer.color;
		ScheduleNext(Time.time);
	}

	// 애니메이션이 색을 바꾼 뒤에 덮어쓰도록 LateUpdate에서 한다
	void LateUpdate()
	{
		float now = Time.time;
		if (glitching)
		{
			if (now < endTime) return;
			glitching = false;
			spriteRenderer.color = baseColor;
			ScheduleNext(now);
			return;
		}

		if (now < nextTime) return;
		glitching = true;
		endTime = now + duration;
		spriteRenderer.color = baseColor * glitchColor;
	}

	void ScheduleNext(float now) => nextTime = now + Random.Range(interval.x, Mathf.Max(interval.x, interval.y));

	void OnDisable()
	{
		// 지지직 중에 꺼지면 원래 색으로 돌려 둔다
		glitching = false;
		if (spriteRenderer != null) spriteRenderer.color = baseColor;
	}
}
