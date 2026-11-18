using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD 왼쪽 위(장소 이름 아래)의 튜토리얼 대사 창. 대사 한 줄과 해 볼 일 목록을 보여 주기만 한다 (게임 값은 바꾸지 않는다).
/// <para>
/// - 해 볼 일을 하면 TutorialTracker가 알려 주고, 줄 앞 [ ]가 바로 [x]로 바뀐다.<br/>
/// - 그 쪽의 일을 모두 하면 잠깐(Next Delay) 뒤 다음 쪽의 대사로 넘어간다. 일이 없는 설명 쪽은 Enter로 넘긴다. 시작 연출 쪽만 저절로 넘어간다.<br/>
/// - Enter는 일을 안 했어도 그 쪽을 넘긴다 (GameInput.UI.SubmitPressed, 창이 열려 있으면 반응하지 않는다). 마지막 쪽을 넘기면 끝이다.<br/>
/// - 쪽마다 가야 할 곳을 TutorialGuide로 가리키고, 메모리 쪽에서는 HUD의 Memory 막대 테두리를 깜빡인다.<br/>
/// - 끝내면 이 컴퓨터에 저장한다 (PlayerPrefs, 계정 닉네임별). 다시 접속해도 안 뜬다. 진행 중인 쪽은 저장하지 않는다 (접속하면 처음부터).<br/>
/// - 내 캐릭터가 있고 레이드 던전이 아닐 때만 보인다. HUD 안에 있어서 큰 창이 열리면 HUD와 같이 숨는다.<br/>
/// - 창 모양(바탕 불투명도, 글자 크기, 너비)은 HUD 프리팹 값을 Play 때 덮어쓴다. 글이 길면 줄을 바꾸고 창 높이를 글에 맞춘다.
/// </para>
/// </summary>
public class TutorialView : MonoBehaviour
{
	const string KeyPrefix = "tutorial.done.";

	[SerializeField] TutorialTracker tracker;
	[SerializeField] TutorialGuide guide;
	[SerializeField, Tooltip("대사 창 전체. 안 보일 때 끈다")] GameObject panel;
	[SerializeField, Tooltip("대사 한 줄")] TMP_Text lineText;
	[SerializeField, Tooltip("해 볼 일 줄. 위에서부터 채우고 남는 줄은 끈다 (TutorialScript.MaxTasks개)")] TMP_Text[] taskRows;
	[SerializeField, Tooltip("Memory 막대 뒤에 깔린 테두리. 메모리 쪽에서 깜빡인다")] Image memoryHighlight;
	[SerializeField] Color doneColor = new Color32(0x9B, 0xE1, 0x5D, 0xFF);
	[SerializeField] Color todoColor = Color.white;
	[SerializeField, Min(0f), Tooltip("그 쪽의 일을 모두 한 뒤 다음 쪽으로 넘어가기까지 기다리는 시간(초)")] float nextDelay = 1.2f;

	[Header("창 모양 (HUD 프리팹 값을 Play 때 덮어쓴다)")]
	[SerializeField, Range(0f, 1f), Tooltip("대사 창 바탕의 불투명도. 1이면 불투명")] float panelOpacity = 0.6f;
	[SerializeField, Min(1f), Tooltip("글자 크기. 폰트(Galmuri11)가 픽셀 폰트라 7처럼 작게 줄이면 획이 뭉개진다. 다른 HUD 글자와 같은 8 권장")] float fontSize = 8f;
	[SerializeField, Min(60f), Tooltip("창 너비. 글이 길면 줄을 바꾸고 창 높이가 늘어난다")] float panelWidth = 240f;

	// 창 안 여백 (캔버스 640×360 기준 픽셀). 아래 여백에는 「Enter 넘기기」 안내가 들어간다
	const float PadX = 7f, PadTop = 6f, PadBottom = 17f, LineGap = 3f, RowGap = 1f;
	// 「Enter 넘기기」 안내 (Panel 아래 KeyHint, 오른쪽 아래). 프리팹 값(6)은 작아서 덮어쓴다
	const string KeyHintName = "KeyHint";
	const float KeyHintFontSize = 8f;

	readonly TutorialProgress progress = new TutorialProgress();
	GameObject player;
	string key;
	bool finished = true;
	float nextAt = -1f;
	// HUD와 함께 꺼진 때 (-1 = 꺼진 적 없음). 다시 켜지면 꺼져 있던 시간만큼 넘어가는 시간을 미룬다
	float hiddenAt = -1f;

	void Awake()
	{
		if (panel != null) panel.SetActive(false);
		if (memoryHighlight != null) memoryHighlight.gameObject.SetActive(false);
		ApplyStyle();
	}

	// 창 바탕을 반투명하게, 글자는 또렷한 크기로 줄바꿈해서 쓴다
	void ApplyStyle()
	{
		if (panel != null && panel.TryGetComponent(out Image background))
		{
			Color c = background.color;
			c.a = panelOpacity;
			background.color = c;
		}
		StyleText(lineText);
		for (int i = 0; taskRows != null && i < taskRows.Length; i++) StyleText(taskRows[i]);

		Transform hintTransform = panel != null ? panel.transform.Find(KeyHintName) : null;
		if (hintTransform != null && hintTransform.TryGetComponent(out TMP_Text hint))
		{
			hint.fontSize = KeyHintFontSize;
			hint.rectTransform.sizeDelta = new Vector2(100f, 11f);
		}
	}

	void StyleText(TMP_Text text)
	{
		if (text == null) return;
		text.fontSize = fontSize;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.overflowMode = TextOverflowModes.Overflow;
		text.richText = true;
	}

	void OnEnable()
	{
		if (tracker != null) tracker.Signaled += OnSignaled;
		if (hiddenAt >= 0f && nextAt >= 0f) nextAt += Time.unscaledTime - hiddenAt;
		hiddenAt = -1f;
	}

	void OnDisable()
	{
		hiddenAt = Time.unscaledTime;
		if (tracker != null) tracker.Signaled -= OnSignaled;
		ShowGuides(false);
	}

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player)
		{
			player = me;
			progress.Reset();
			nextAt = -1f;
			// 닉네임은 서버가 정한 뒤에 오므로 정해질 때까지 기다린다
			key = null;
			finished = true;
		}
		if (player == null) { SetVisible(false); return; }

		if (key == null)
		{
			string nickname = player.TryGetComponent(out PlayerPublicInfo info) ? info.Nickname : "";
			if (string.IsNullOrEmpty(nickname) && NetworkClient.active && !NetworkServer.active) return;
			key = KeyPrefix + (string.IsNullOrEmpty(nickname) ? "temp" : nickname);
			finished = PlayerPrefs.GetInt(key, 0) == 1;
			if (tracker != null) tracker.enabled = !finished;
			if (!finished) ApplyPage();
		}

		if (finished) { SetVisible(false); return; }

		// 창(상점처럼 HUD를 남기는 창 포함)이 열려 있는 동안은 시간으로 넘어가지 않는다. 닫으면 남은 시간부터 다시 센다
		if (nextAt >= 0f && InputBlockingPanel.AnyOpen) nextAt += Time.unscaledDeltaTime;
		else if (nextAt >= 0f && Time.unscaledTime >= nextAt) GoNext();
		else if (!InputBlockingPanel.AnyOpen && GameInput.UI.SubmitPressed) GoNext();

		bool visible = !finished && PlayerPlace.Mine != PlaceScenes.RaidDungeon;
		SetVisible(visible);
		ShowGuides(visible);
		if (visible) PulseHighlight();
	}

	void OnSignaled(TutorialTask task)
	{
		if (finished || !progress.Complete(task)) return;
		Redraw();
		if (progress.PageCompleted && nextAt < 0f) nextAt = Time.unscaledTime + nextDelay;
	}

	void GoNext()
	{
		nextAt = -1f;
		progress.Next();
		if (progress.Finished) { Finish(); return; }
		ApplyPage();
	}

	// 쪽이 바뀔 때 한 번: 글자, 가리킬 곳, 시간으로 넘어가는 쪽의 타이머
	void ApplyPage()
	{
		if (CannotDo(progress.Page)) { GoNext(); return; }
		Redraw();
		TutorialScript.Page page = progress.Page;
		if (page.AutoSeconds > 0f) nextAt = Time.unscaledTime + page.AutoSeconds;
		// 미리 해 둔 일로 이미 다 끝난 쪽이면 바로 이어서 넘어간다
		else if (progress.PageCompleted) nextAt = Time.unscaledTime + nextDelay;
	}

	// 내 캐릭터가 할 수 없는 일이 있는 쪽. 실제 계정(FunctionExecutorNetwork.UsesServerData)은 장착대가 숨겨져 있어(#470) 장착 쪽을 건너뛴다
	bool CannotDo(TutorialScript.Page page)
	{
		if (System.Array.IndexOf(page.Tasks, TutorialTask.OpenEquip) < 0) return false;
		return player != null && player.TryGetComponent(out FunctionExecutorNetwork network) && network.UsesServerData;
	}

	void Finish()
	{
		finished = true;
		nextAt = -1f;
		if (key != null)
		{
			PlayerPrefs.SetInt(key, 1);
			PlayerPrefs.Save();
		}
		if (tracker != null) tracker.enabled = false;
		SetVisible(false);
		ShowGuides(false);
	}

	void Redraw()
	{
		if (progress.Finished) return;
		TutorialScript.Page page = progress.Page;
		if (lineText != null) lineText.text = KeepWords(page.Line);
		for (int i = 0; taskRows != null && i < taskRows.Length; i++)
		{
			TMP_Text row = taskRows[i];
			if (row == null) continue;
			bool has = i < page.Tasks.Length;
			if (row.gameObject.activeSelf != has) row.gameObject.SetActive(has);
			if (!has) continue;
			bool done = progress.IsDone(page.Tasks[i]);
			row.text = (done ? "[x] " : "[ ] ") + KeepWords(TutorialScript.Label(page.Tasks[i]));
			row.color = done ? doneColor : todoColor;
		}
		Layout();
	}

	// 대사와 해 볼 일 줄을 위에서부터 쌓고, 글 길이에 맞춰 창 높이를 정한다 (창 바탕 Panel은 이 오브젝트 크기를 따라간다)
	void Layout()
	{
		float inner = panelWidth - PadX * 2f;
		float y = PadTop;
		if (lineText != null) y = Stack(lineText, y, inner) + LineGap;
		for (int i = 0; taskRows != null && i < taskRows.Length; i++)
		{
			TMP_Text row = taskRows[i];
			if (row != null && row.gameObject.activeSelf) y = Stack(row, y, inner) + RowGap;
		}
		if (transform is RectTransform box) box.sizeDelta = new Vector2(panelWidth, y + PadBottom);
	}

	// 글 한 덩어리를 y(위에서부터 픽셀) 자리에 놓고, 그 아래 끝을 돌려준다
	static float Stack(TMP_Text text, float y, float width)
	{
		RectTransform rect = text.rectTransform;
		float height = Mathf.Ceil(text.GetPreferredValues(text.text, width, 0f).y);
		rect.anchoredPosition = new Vector2(PadX, -y);
		rect.sizeDelta = new Vector2(width, height);
		return y + height;
	}

	// 한글은 TMP 기본 설정에서 글자 단위로 줄이 바뀌어 낱말이 잘린다. 낱말마다 <nobr>로 묶어 띄어쓰기에서만 줄을 바꾼다
	// 대사에 적어 둔 \n(문장 끝 줄바꿈)은 그대로 둔다
	static string KeepWords(string text)
	{
		if (string.IsNullOrEmpty(text)) return text;
		string[] lines = text.Split('\n');
		for (int l = 0; l < lines.Length; l++)
		{
			string[] words = lines[l].Split(' ');
			for (int i = 0; i < words.Length; i++)
				if (words[i].Length > 0) words[i] = "<nobr>" + words[i] + "</nobr>";
			lines[l] = string.Join(" ", words);
		}
		return string.Join("\n", lines);
	}

	// 가리킬 곳과 Memory 테두리. 보이지 않을 때(레이드, 끝남)는 모두 끈다
	void ShowGuides(bool visible)
	{
		bool on = visible && !finished && !progress.Finished;
		if (guide != null) guide.Goal = on ? progress.Page.Goal : TutorialGoal.None;
		bool highlight = on && progress.Page.HighlightMemory;
		if (memoryHighlight != null && memoryHighlight.gameObject.activeSelf != highlight)
			memoryHighlight.gameObject.SetActive(highlight);
	}

	void PulseHighlight()
	{
		if (memoryHighlight == null || !memoryHighlight.gameObject.activeSelf) return;
		Color c = memoryHighlight.color;
		c.a = 0.35f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f));
		memoryHighlight.color = c;
	}

	void SetVisible(bool visible)
	{
		if (panel != null && panel.activeSelf != visible) panel.SetActive(visible);
	}

	/// <summary>[테스트] 저장한 「끝냄」을 지우고 처음부터 다시 보여 준다 (Play 중 Inspector 점 세 개 메뉴)</summary>
	[ContextMenu("튜토리얼 다시 보기")]
	void ResetForTest()
	{
		if (key != null) PlayerPrefs.DeleteKey(key);
		finished = false;
		progress.Reset();
		nextAt = -1f;
		if (tracker != null) tracker.enabled = true;
		ApplyPage();
	}
}
