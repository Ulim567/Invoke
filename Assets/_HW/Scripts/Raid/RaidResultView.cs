using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 결과 화면 (「레이드 클리어」 / 「레이드 실패」). 연출만 한다. RaidResultCanvas 프리팹 최상위에 붙이고 레이드 던전 씬(RaidDungeon)에 둔다.
/// <para>
/// - <c>RaidJudge.ResultEffectPlayed</c>(클리어면 true, 김도훈)를 받아서 띄운다. 비워 두면 같은 씬의 RaidJudge를 찾는다.<br/>
/// - RaidJudge는 결과를 부르고 Result Delay(3초) 뒤 레이드 끝 처리로 넘어가므로, 이 화면도 Show Time(3초) 동안 보인 뒤 사라진다 (raid.md 「클리어·실패 판정」).<br/>
/// - 보상 줄 (시안 12, #383): 클리어면 「보스 코어 +1  버그 조각 +5  경험치 +200」처럼 이번 레이드에서 받은 것을 보여 준다.
///   내 캐릭터가 레이드 던전에 들어온 순간의 PlayerProgress(레벨·경험치·재료)를 기억해 두고, 결과 화면이 떠 있는 동안 지금 값과의 차이를 그린다.
///   보상은 서버가 주고 Mirror가 조금 늦게 맞춰 주므로, 화면이 떠 있는 동안 매 프레임 다시 센다. 받은 게 없으면 줄을 숨긴다.<br/>
/// - 내 던전의 결과만 띄운다 (#424). Host는 다른 파티의 던전 씬도 함께 띄워 두므로, 내 캐릭터가 있는 씬의 결과 화면만 보인다.<br/>
/// - 「레이드 실패」가 떠 있는 동안 MyFailureShowing이 true다. 사망 화면 효과(DeathScreenEffect, 이우림)가 읽어 파티원 모두의 화면을 무채색으로 맞춘다 (#424).<br/>
/// - 시험: Inspector에서 이 컴포넌트 오른쪽 ⋮ → 「시험: 성공」 / 「시험: 실패」.
/// </para>
/// </summary>
public class RaidResultView : MonoBehaviour
{
	// 지금 「레이드 실패」를 띄우고 있는 내 던전의 결과 화면. 없으면 null.
	// 던전 씬이 내려가 오브젝트가 사라지면 Unity의 null 비교로 null과 같아진다
	static RaidResultView showingFailure;

	/// <summary>
	/// 내 던전의 「레이드 실패」 결과 화면이 떠 있으면 true (#424). 「레이드 클리어」일 때는 false.<br/>
	/// 사망 화면 효과(DeathScreenEffect)가 매 프레임 읽어, 실패 결과 화면 동안 파티원 모두의 화면을 무채색으로 하고 「사망했습니다」 글자를 숨긴다.
	/// </summary>
	public static bool MyFailureShowing => showingFailure != null;

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

	[SerializeField, Tooltip("보상 줄 (클리어일 때만). 비워 두면 보상을 안 보여 준다")]
	TMP_Text rewardText;

	[Header("글자·색")]
	[SerializeField] string clearTitle = "레이드 클리어"; // 시안 12
	[SerializeField] string clearSubtitle = "보스를 쓰러뜨렸어요";
	[SerializeField] Color clearColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF); // 민트 (시안 12)
	[SerializeField] string failTitle = "레이드 실패";
	[SerializeField] string failSubtitle = "파티가 모두 쓰러졌어요";
	[SerializeField] Color failColor = new Color32(0xE0, 0x50, 0x60, 0xFF);

	[Header("시간 (초)")]
	[SerializeField, Min(0.5f), Tooltip("보이는 시간. RaidJudge의 Result Delay와 같게")]
	float showTime = 3f;

	[SerializeField, Min(0.05f), Tooltip("나타나고 사라지는 시간")]
	float fadeTime = 0.25f;

	// 보상 줄에 보여 줄 재료 (재료 ID, 이름). 순서대로 쓴다
	static readonly (string id, string name)[] RewardMaterials =
	{
		("boss_core", "보스 코어"),
		("bug_fragment", "버그 조각"),
	};

	// 레이드 던전에 들어온 순간의 내 진행도
	PlayerProgress tracked;
	int startTotalExp;
	readonly Dictionary<string, int> startMaterials = new Dictionary<string, int>();
	bool showingReward;

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
		StopShowingFailure();
	}

	// 이 결과 화면이 「레이드 실패」를 띄우던 중이었으면 내린다
	void StopShowingFailure()
	{
		if (showingFailure == this) showingFailure = null;
	}

	// 이 결과 화면이 내 캐릭터가 있는 던전의 것이면 true.
	// Host(서버)는 모든 파티의 던전 씬을 띄워 두므로 내 캐릭터가 있는 씬과 같은지 본다.
	// 클라이언트는 자기 던전 씬만 불러오고, 네트워크를 켜지 않은 시험에는 다른 던전이 없으므로 늘 내 것이다
	bool IsMyDungeon()
	{
		if (!NetworkServer.active) return true;
		return NetworkClient.localPlayer != null && NetworkClient.localPlayer.gameObject.scene == gameObject.scene;
	}

	void Update()
	{
		TrackStart();
		if (showingReward) RefreshReward();
	}

	// 내 캐릭터가 레이드 던전에 있으면 들어온 순간의 값을 기억한다. 던전 밖이면 잊는다 (다음 레이드 때 다시 기억)
	void TrackStart()
	{
		bool inRaid = PlayerPlace.Mine == PlaceScenes.RaidDungeon;
		if (!inRaid)
		{
			if (!showingReward) tracked = null;
			return;
		}
		if (tracked != null) return;
		if (NetworkClient.localPlayer == null || !NetworkClient.localPlayer.TryGetComponent(out PlayerProgress progress)) return;
		tracked = progress;
		startTotalExp = TotalExp(progress);
		startMaterials.Clear();
		foreach (var (id, _) in RewardMaterials) startMaterials[id] = progress.GetMaterialCount(id);
	}

	// 레벨 1부터 지금까지 모은 경험치. 레벨이 올라도 받은 경험치를 그대로 셀 수 있게
	static int TotalExp(PlayerProgress progress)
	{
		int total = progress.Exp;
		for (int level = 1; level < progress.Level; level++) total += LevelRules.ExpToNext(level);
		return total;
	}

	// 보상 줄: 들어올 때보다 늘어난 것만 「이름 +n」으로
	void RefreshReward()
	{
		if (rewardText == null) return;
		if (tracked == null)
		{
			rewardText.gameObject.SetActive(false);
			return;
		}
		var parts = new List<string>();
		foreach (var (id, label) in RewardMaterials)
		{
			int gained = tracked.GetMaterialCount(id) - (startMaterials.TryGetValue(id, out int start) ? start : 0);
			if (gained > 0) parts.Add($"{label} <color=#5EF2D6>+{gained}</color>");
		}
		int exp = TotalExp(tracked) - startTotalExp;
		if (exp > 0) parts.Add($"경험치 <color=#5EF2D6>+{exp}</color>");
		rewardText.text = string.Join("    ", parts);
		rewardText.gameObject.SetActive(parts.Count > 0);
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
		// 다른 파티의 레이드 결과는 Host 화면에 띄우지 않는다
		if (!IsMyDungeon()) return;
		StopAllCoroutines();
		if (cleared) StopShowingFailure();
		else showingFailure = this;
		showingReward = cleared;
		if (rewardText != null) rewardText.gameObject.SetActive(false);
		if (cleared) RefreshReward();
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
		showingReward = false;
		StopShowingFailure();
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
