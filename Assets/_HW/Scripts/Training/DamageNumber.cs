using UnityEngine;

/// <summary>
/// 툭 튀어나와 포물선을 그리며 사라지는 데미지 숫자 하나. 연출만 한다 (#442: 픽셀 숫자 그림 + 팝).
/// <para>
/// - DamageNumberView가 만들고 Show를 부른다. Life Time이 지나면 스스로 없어진다.<br/>
/// - 숫자 한 자리마다 자식 SpriteRenderer를 만들어 Digits(일반) 또는 Critical Digits(표식 맞힘) 그림으로 그린다. 그림은 박진우 _JW/Effects/Damage, 프리팹은 메뉴 Invoke → JW → 데미지 숫자 프리팹 만들기.<br/>
/// - 처음 Pop Time 동안 Pop Scale배에서 원래 크기로 줄어들고(팝), 좌우 랜덤 속도로 위로 튀었다가 중력으로 내려오며 Fade Start 뒤 흐려진다.
/// </para>
/// </summary>
public class DamageNumber : MonoBehaviour
{
	[Header("그림")]
	[SerializeField, Tooltip("일반 숫자 0~9 (순서대로)")]
	Sprite[] digits = new Sprite[10];

	[SerializeField, Tooltip("표식 맞힘(치명타) 숫자 0~9 (순서대로)")]
	Sprite[] criticalDigits = new Sprite[10];

	[SerializeField, Min(1f), Tooltip("숫자 사이 간격 (픽셀). 칸이 9px이고 테두리 1px을 겹친다")]
	float digitSpacingPixels = 8f;

	[SerializeField, Min(1f), Tooltip("그림 Pixels Per Unit")]
	float pixelsPerUnit = 16f;

	[SerializeField, Tooltip("Sorting Layer")]
	string sortingLayer = "WorldUI";

	[SerializeField, Tooltip("Order in Layer (체력 바·상태 아이콘보다 위)")]
	int sortingOrder = 20;

	[Header("움직임")]
	[SerializeField, Min(0.1f), Tooltip("보이는 시간(초)")]
	float lifeTime = 0.9f;

	[SerializeField, Min(1f), Tooltip("처음에 이 배율로 커졌다가 원래 크기로 줄어든다")]
	float popScale = 1.7f;

	[SerializeField, Min(0.01f), Tooltip("팝 시간(초)")]
	float popTime = 0.12f;

	[SerializeField, Tooltip("튀어 오르는 가로 속도 범위 (유닛/초)")]
	Vector2 launchSpeedX = new Vector2(-1.2f, 1.2f);

	[SerializeField, Min(0f), Tooltip("튀어 오르는 세로 속도 (유닛/초)")]
	float launchSpeedY = 4.5f;

	[SerializeField, Min(0f), Tooltip("중력 (유닛/초²)")]
	float gravity = 9f;

	[SerializeField, Range(0f, 1f), Tooltip("이 비율이 지나면 흐려지기 시작한다")]
	float fadeStart = 0.55f;

	SpriteRenderer[] parts = new SpriteRenderer[0];
	float startTime;
	float baseScale = 1f;
	Vector2 velocity;
	bool shown;

	/// <summary>보여 주는 숫자 (테스트용).</summary>
	public int Value { get; private set; }
	/// <summary>표식 맞힘(치명타) 그림인지 (테스트용).</summary>
	public bool IsCritical { get; private set; }
	/// <summary>자리 수 (테스트용).</summary>
	public int DigitCount => parts.Length;

	/// <summary>
	/// 숫자를 정하고 튀어 오르기 시작한다. critical이면 표식 맞힘 그림, scale은 크기 배율(데미지가 클수록 크게).
	/// </summary>
	public void Show(int damage, bool critical = false, float scale = 1f)
	{
		Value = Mathf.Max(0, damage);
		IsCritical = critical;
		baseScale = scale;
		BuildDigits(Value.ToString(), critical ? criticalDigits : digits);
		velocity = new Vector2(Random.Range(launchSpeedX.x, launchSpeedX.y), launchSpeedY);
		startTime = Time.time;
		transform.localScale = Vector3.one * (baseScale * popScale);
		shown = true;
	}

	// 자리마다 SpriteRenderer 하나. 가운데 맞춤
	void BuildDigits(string text, Sprite[] set)
	{
		foreach (SpriteRenderer p in parts) if (p != null) Destroy(p.gameObject);
		parts = new SpriteRenderer[text.Length];
		float step = digitSpacingPixels / pixelsPerUnit;
		float left = -step * (text.Length - 1) * 0.5f;
		for (int i = 0; i < text.Length; i++)
		{
			var obj = new GameObject("Digit" + i);
			obj.transform.SetParent(transform, false);
			obj.transform.localPosition = new Vector3(left + step * i, 0f, 0f);
			var sr = obj.AddComponent<SpriteRenderer>();
			int d = text[i] - '0';
			sr.sprite = set != null && d >= 0 && d < set.Length ? set[d] : null;
			sr.sortingLayerName = sortingLayer;
			sr.sortingOrder = sortingOrder;
			parts[i] = sr;
		}
	}

	void Update()
	{
		if (!shown) return;
		float age = Time.time - startTime;
		if (age >= lifeTime)
		{
			Destroy(gameObject);
			return;
		}

		// 팝: 크게 시작해서 원래 크기로
		float pop = Mathf.Clamp01(age / popTime);
		float s = Mathf.Lerp(popScale, 1f, 1f - (1f - pop) * (1f - pop));
		transform.localScale = Vector3.one * (baseScale * s);

		// 포물선
		velocity.y -= gravity * Time.deltaTime;
		transform.position += (Vector3)(velocity * Time.deltaTime);

		// 흐려짐
		float t = age / lifeTime;
		float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);
		foreach (SpriteRenderer p in parts)
		{
			if (p == null) continue;
			Color c = p.color;
			c.a = alpha;
			p.color = c;
		}
	}
}
