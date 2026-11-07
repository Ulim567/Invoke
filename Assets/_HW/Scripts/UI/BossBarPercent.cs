using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 체력 바 오른쪽 위의 「64%」 글자 (우림이 시안 8-3). 김도훈의 BossHealthBar가 채우는 빨간 막대(Fill)의 비율을 읽어서 쓰기만 한다.
/// BossHealthBar의 숫자 칸(지금 / 최대)은 시안에 없어 비워 두고, 대신 이것을 쓴다. HudCanvas 프리팹의 보스 체력 바에 붙어 있다.
/// </summary>
public class BossBarPercent : MonoBehaviour
{
	[SerializeField, Tooltip("BossHealthBar의 Fill과 같은 Image")] Image fill;
	[SerializeField] TMP_Text text;

	void LateUpdate()
	{
		if (fill == null || text == null) return;
		string value = Mathf.CeilToInt(fill.fillAmount * 100f - 0.001f) + "%";
		if (text.text != value) text.text = value;
	}
}
