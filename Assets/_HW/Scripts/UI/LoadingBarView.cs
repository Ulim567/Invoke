using UnityEngine;

/// <summary>
/// 로딩 화면의 막대 안에서 청록 조각이 왼쪽에서 오른쪽으로 계속 지나가게 한다 (우림이 시안 11). 진행률은 보여 주지 않는다 (얼마나 남았는지 모르므로).
/// <para>
/// - 막대(조각의 부모)에 RectMask2D를 붙여 막대 밖으로 나간 부분을 자른다.<br/>
/// - 씬을 불러오는 동안 Time.timeScale이 바뀌어도 같은 빠르기로 움직이도록 unscaledTime을 쓴다.<br/>
/// - LoadingCanvas 프리팹(Base 씬)에 들어 있다. 프리팹은 메뉴 Invoke → UI → 로딩 화면 만들기.
/// </para>
/// </summary>
public class LoadingBarView : MonoBehaviour
{
	[SerializeField, Tooltip("움직이는 청록 조각. 막대의 자식, 왼쪽 가운데 기준(Anchor·Pivot x = 0)")]
	RectTransform segment;

	[SerializeField, Min(0.1f), Tooltip("조각이 막대를 한 번 지나가는 시간(초)")]
	float period = 1.2f;

	RectTransform bar;

	void Awake() => bar = (RectTransform)transform;

	void Update()
	{
		if (segment == null) return;
		float width = bar.rect.width;
		float size = segment.rect.width;
		float t = Time.unscaledTime / period % 1f;
		// 왼쪽 밖에서 들어와 오른쪽 밖으로 나간다
		segment.anchoredPosition = new Vector2(Mathf.Lerp(-size, width, t), segment.anchoredPosition.y);
	}
}
