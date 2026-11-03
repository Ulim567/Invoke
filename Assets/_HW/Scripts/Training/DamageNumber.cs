using TMPro;
using UnityEngine;

/// <summary>
/// 떠오르며 사라지는 데미지 숫자 하나. DamageNumber 프리팹 최상위에 TextMeshPro(월드용, UI가 아님)와 함께 붙인다. 연출만 한다.
/// <para>
/// DamageNumberView가 만들고 Show를 부른다. Life Time이 지나면 스스로 없어진다.
/// </para>
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class DamageNumber : MonoBehaviour
{
	[SerializeField, Min(0.1f), Tooltip("보이는 시간(초)")]
	float lifeTime = 0.8f;

	[SerializeField, Tooltip("1초에 떠오르는 높이 (유닛)")]
	float riseSpeed = 1.5f;

	[SerializeField, Range(0f, 1f), Tooltip("이 비율이 지나면 흐려지기 시작한다")]
	float fadeStart = 0.5f;

	TMP_Text text;
	float startTime;
	Color baseColor;

	void Awake()
	{
		text = GetComponent<TMP_Text>();
		baseColor = text.color;
	}

	/// <summary>
	/// 숫자를 정하고 떠오르기 시작한다.
	/// </summary>
	public void Show(int damage)
	{
		text.text = damage.ToString();
		startTime = Time.time;
	}

	void Update()
	{
		float age = Time.time - startTime;
		if (age >= lifeTime)
		{
			Destroy(gameObject);
			return;
		}

		transform.position += Vector3.up * (riseSpeed * Time.deltaTime);
		float t = age / lifeTime;
		float alpha = t < fadeStart ? 1f : 1f - (t - fadeStart) / (1f - fadeStart);
		text.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
	}
}
