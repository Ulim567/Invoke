using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 화면 전체를 덮어, 장소 씬을 언로드하고 로드하는 모습을 가리는 로딩 화면. 네트워크는 쓰지 않는다.
/// <para>
/// - PlaceTravelClient만 Show, ShowImmediate, Hide를 부른다.<br/>
/// - 보이고 숨길 때 panelRoot의 CanvasGroup alpha를 바꿔 페이드 인·아웃한다.<br/>
/// - 화면을 덮는 Image의 Raycast Target을 체크해 두면, 로딩 화면이 떠 있는 동안 뒤의 UI 버튼이 눌리지 않는다.
/// </para>
/// </summary>
public class LoadingScreen : MonoBehaviour
{
	[SerializeField, Tooltip("화면 전체를 덮는 UI의 부모. 로딩 화면을 보일 때만 활성화한다")]
	GameObject panelRoot;

	[SerializeField, Tooltip("panelRoot에 붙인 CanvasGroup. alpha로 페이드한다")]
	CanvasGroup canvasGroup;

	[SerializeField, Min(0f), Tooltip("페이드 인·아웃에 걸리는 시간(초)")]
	float fadeSeconds = 0.25f;

	Coroutine fading;

	void Awake()
	{
		// 게임을 시작할 때는 보이지 않게 한다
		canvasGroup.alpha = 0f;
		panelRoot.SetActive(false);
	}

	/// <summary>
	/// 로딩 화면을 페이드 인으로 보인다. 화면을 다 덮으면 onShown을 부른다.<br/>
	/// 씬을 바꾸는 요청은 onShown에서 보낸다. 덮기 전에 씬이 바뀌면 바뀌는 모습이 보이기 때문이다.
	/// </summary>
	public void Show(Action onShown = null)
	{
		panelRoot.SetActive(true);
		Fade(1f, onShown);
	}

	/// <summary>
	/// 로딩 화면을 페이드 없이 바로 보인다. 접속 직후처럼 첫 화면부터 덮어야 할 때 쓴다.
	/// </summary>
	public void ShowImmediate()
	{
		StopFade();
		panelRoot.SetActive(true);
		canvasGroup.alpha = 1f;
	}

	/// <summary>
	/// 로딩 화면을 페이드 아웃으로 숨긴다.
	/// </summary>
	public void Hide()
	{
		if (!panelRoot.activeSelf) return;
		Fade(0f, () => panelRoot.SetActive(false));
	}

	void Fade(float targetAlpha, Action onDone)
	{
		// 페이드 중에 반대로 바뀌면 이전 페이드의 onDone은 부르지 않는다 (숨기던 중에 다시 보이면 비활성화하지 않아야 함)
		StopFade();
		fading = StartCoroutine(FadeRoutine(targetAlpha, onDone));
	}

	void StopFade()
	{
		if (fading == null) return;
		StopCoroutine(fading);
		fading = null;
	}

	IEnumerator FadeRoutine(float targetAlpha, Action onDone)
	{
		// 씬을 로드하는 동안 Time.timeScale이 바뀌어도 같은 시간에 끝나도록 unscaledDeltaTime을 쓴다
		float speed = fadeSeconds > 0f ? 1f / fadeSeconds : float.MaxValue;
		while (!Mathf.Approximately(canvasGroup.alpha, targetAlpha))
		{
			canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, speed * Time.unscaledDeltaTime);
			yield return null;
		}

		fading = null;
		onDone?.Invoke();
	}
}
