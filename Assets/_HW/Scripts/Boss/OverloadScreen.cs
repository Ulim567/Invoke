using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보스 전체 공격 화면 연출 (OverloadScreen 프리팹): 모으는 동안 위쪽 경고와 남은 시간, 터질 때 화면 번쩍임. 연출만 한다.
/// GatewayOverloadView가 만들고 부른다.
/// </summary>
public class OverloadScreen : MonoBehaviour
{
	[SerializeField, Tooltip("경고 띠 (처음엔 숨김)")]
	GameObject warning;

	[SerializeField, Tooltip("남은 시간 글자")]
	TMP_Text countdownText;

	[SerializeField, Tooltip("화면 전체 번쩍임 (Image)")]
	Image flash;

	[SerializeField, Tooltip("번쩍임 색")]
	Color flashColor = new Color32(0xFF, 0xE0, 0xE6, 0xD0);

	[SerializeField, Min(0.05f), Tooltip("번쩍임이 사라지는 시간(초)")]
	float flashTime = 0.5f;

	Coroutine warningRoutine;
	Coroutine flashRoutine;

	void Awake()
	{
		if (warning != null) warning.SetActive(false);
		if (flash != null) flash.color = Clear(flashColor);
	}

	/// <summary>모으기 시작: seconds 동안 경고와 남은 시간을 보인다</summary>
	public void ShowWarning(float seconds)
	{
		if (warningRoutine != null) StopCoroutine(warningRoutine);
		warningRoutine = StartCoroutine(Warning(seconds));
	}

	/// <summary>터짐: 경고를 닫고 화면을 번쩍인다</summary>
	public void Flash()
	{
		HideWarning();
		if (flash == null) return;
		if (flashRoutine != null) StopCoroutine(flashRoutine);
		flashRoutine = StartCoroutine(FlashRoutine());
	}

	/// <summary>경고만 닫는다 (보스가 죽어서 취소될 때)</summary>
	public void HideWarning()
	{
		if (warningRoutine != null) StopCoroutine(warningRoutine);
		warningRoutine = null;
		if (warning != null) warning.SetActive(false);
	}

	IEnumerator Warning(float seconds)
	{
		if (warning != null) warning.SetActive(true);
		for (float left = seconds; left > 0; left -= Time.deltaTime)
		{
			if (countdownText != null) countdownText.text = Mathf.CeilToInt(left).ToString();
			yield return null;
		}
		HideWarning();
	}

	IEnumerator FlashRoutine()
	{
		for (float t = 0; t < flashTime; t += Time.deltaTime)
		{
			flash.color = Color.Lerp(flashColor, Clear(flashColor), t / flashTime);
			yield return null;
		}
		flash.color = Clear(flashColor);
		flashRoutine = null;
	}

	static Color Clear(Color c) => new Color(c.r, c.g, c.b, 0f);
}
