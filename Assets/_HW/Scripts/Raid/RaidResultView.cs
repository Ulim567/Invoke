using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 결과 화면 (「레이드 성공」 / 「레이드 실패」). 연출만 한다. RaidResultCanvas 프리팹 최상위에 붙이고 레이드 던전 씬(RaidDungeon)에 둔다.
/// <para>
/// - <c>RaidJudge.ResultEffectPlayed</c>(클리어면 true, 김도훈)를 받아서 띄운다. 비워 두면 같은 씬의 RaidJudge를 찾는다.<br/>
/// - RaidJudge는 결과를 부르고 Result Delay(3초) 뒤 레이드 끝 처리로 넘어가므로, 이 화면도 Show Time(3초) 동안 보인 뒤 사라진다 (raid.md 「클리어·실패 판정」).<br/>
/// - 시험: Inspector에서 이 컴포넌트 오른쪽 ⋮ → 「시험: 성공」 / 「시험: 실패」.
/// </para>
/// </summary>
public class RaidResultView : MonoBehaviour
{
	[SerializeField, Tooltip("결과를 받을 RaidJudge. 비워 두면 같은 씬에서 찾는다")]
	RaidJudge judge;

	[Header("화면")]
	[SerializeField, Tooltip("결과 화면 전체 (처음엔 숨김)")]
	GameObject root;

	[SerializeField, Tooltip("전체 투명도 (나타나고 사라질 때)")]
	CanvasGroup group;

	[SerializeField, Tooltip("가운데 띠. 세로로 펼쳐지며 나타난다")]
	RectTransform banner;

	[SerializeField, Tooltip("큰 글자 (레이드 성공 / 레이드 실패)")]
	TMP_Text titleText;

	[SerializeField, Tooltip("작은 글자 (이유)")]
	TMP_Text subtitleText;

	[SerializeField, Tooltip("띠 위아래 색 줄")]
	Image[] accents;

	[Header("글자·색")]
	[SerializeField] string clearTitle = "레이드 성공";
	[SerializeField] string clearSubtitle = "보스를 쓰러뜨렸어요!";
	[SerializeField] Color clearColor = new Color32(0xFF, 0xD7, 0x7A, 0xFF);
	[SerializeField] string failTitle = "레이드 실패";
	[SerializeField] string failSubtitle = "파티가 모두 쓰러졌어요";
	[SerializeField] Color failColor = new Color32(0xE0, 0x50, 0x60, 0xFF);

	[Header("시간 (초)")]
	[SerializeField, Min(0.5f), Tooltip("보이는 시간. RaidJudge의 Result Delay와 같게")]
	float showTime = 3f;

	[SerializeField, Min(0.05f), Tooltip("나타나고 사라지는 시간")]
	float fadeTime = 0.25f;

	void Awake()
	{
		if (root != null) root.SetActive(false);
		if (judge == null) judge = FindJudgeInScene();
	}

	void OnEnable()
	{
		if (judge != null) judge.ResultEffectPlayed += Show;
	}

	void OnDisable()
	{
		if (judge != null) judge.ResultEffectPlayed -= Show;
	}

	// 같은 씬의 RaidJudge (레이드 던전은 여러 개가 겹쳐 올라올 수 있으므로 내 씬에서만 찾는다)
	RaidJudge FindJudgeInScene()
	{
		foreach (GameObject sceneRoot in gameObject.scene.GetRootGameObjects())
		{
			RaidJudge found = sceneRoot.GetComponentInChildren<RaidJudge>(true);
			if (found != null) return found;
		}
		return null;
	}

	/// <summary>
	/// 결과 화면을 띄운다. 클리어면 true.
	/// </summary>
	public void Show(bool cleared)
	{
		if (root == null) return;
		StopAllCoroutines();
		titleText.text = cleared ? clearTitle : failTitle;
		titleText.color = cleared ? clearColor : failColor;
		if (subtitleText != null) subtitleText.text = cleared ? clearSubtitle : failSubtitle;
		if (accents != null)
			foreach (Image accent in accents) if (accent != null) accent.color = cleared ? clearColor : failColor;
		StartCoroutine(Play());
	}

	IEnumerator Play()
	{
		root.SetActive(true);
		for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
		{
			SetOpen(t / fadeTime);
			yield return null;
		}
		SetOpen(1);

		yield return new WaitForSecondsRealtime(Mathf.Max(0, showTime - fadeTime * 2));

		for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
		{
			if (group != null) group.alpha = 1 - t / fadeTime;
			yield return null;
		}
		root.SetActive(false);
	}

	// 0이면 닫힘, 1이면 다 열림: 투명도와 띠의 세로 크기
	void SetOpen(float amount)
	{
		if (group != null) group.alpha = amount;
		if (banner != null) banner.localScale = new Vector3(1, Mathf.SmoothStep(0, 1, amount), 1);
	}

	[ContextMenu("시험: 성공")]
	void TestClear() => Show(true);

	[ContextMenu("시험: 실패")]
	void TestFail() => Show(false);
}
