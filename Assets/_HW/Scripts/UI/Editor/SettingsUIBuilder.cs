using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 설정 창 프리팹(Assets/_HW/UI/Settings/SettingsCanvas)을 우림이 시안 8-2대로 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 설정 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). Base 씬에 끌어다 놓는다.
/// Assets/_HW/Audio/InvokeMixer가 있으면 SettingsView의 Mixer 칸에 넣는다. 캔버스 기준 640x360.
/// </summary>
public static class SettingsUIBuilder
{
	const string Dir = "Assets/_HW/UI/Settings";
	const string PrefabPath = Dir + "/SettingsCanvas.prefab";
	const string MixerPath = "Assets/_HW/Audio/InvokeMixer.mixer";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	// 체크 칸의 체크 그림. 블록 편집기의 「시전 가능」 표시와 같은 그림이다 (InvokeUIBuilder가 그림)
	const string CheckSpritePath = "Assets/_HW/UI/BlockCoding/Shapes/ui_check.png";

	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Track = Hex("3A3450");
	static readonly Color Danger = Hex("F0907E");

	// 캐릭터 선택·로그아웃 버튼 하나의 너비와 사이 간격. 둘을 합치면 창 안쪽 한 줄(184)과 같다
	const float HalfButtonWidth = 90;
	const float ButtonGap = 4;

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/설정 화면 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "Settings");
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("SettingsCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 40; // HUD(5), 편집 화면(10), 레이드 결과(30)보다 위
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 열고 닫는 부분: 화면 전체를 어둡게 덮는 판 + 가운데 창
		RectTransform panel = NewUI("Panel", canvasObject.transform);
		Stretch(panel, 0);
		Image dim = panel.gameObject.AddComponent<Image>();
		dim.sprite = LoadSprite("set_white");
		dim.color = new Color(0.04f, 0.03f, 0.08f, 0.6f); // 클릭이 뒤로 가지 않게 막기도 한다

		RectTransform window = NewUI("Window", panel);
		Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 232));
		SetImage(window.gameObject, "set_panel");

		TMP_Text title = Text(window, "Title", "> 설정", 11, Teal, TextAlignmentOptions.MidlineLeft);
		title.fontStyle = FontStyles.Bold;
		Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(16, -20), new Vector2(120, 16));

		// 소리 상자
		RectTransform box = NewUI("SoundBox", window);
		Place(box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -35), new Vector2(184, 68));
		SetImage(box.gameObject, "set_box").raycastTarget = false;
		TMP_Text sound = Text(box, "Label", "소리", 7, Label, TextAlignmentOptions.MidlineLeft);
		Place(sound.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, -11), new Vector2(60, 10));

		(Slider master, TMP_Text masterValue) = VolumeRow(box, "Master", "전체", -25);
		(Slider bgm, TMP_Text bgmValue) = VolumeRow(box, "Bgm", "배경음", -40);
		(Slider sfx, TMP_Text sfxValue) = VolumeRow(box, "Sfx", "효과음", -54);

		// 화면 상자 (#536): HUD 왼쪽 아래 콘솔 출력(printf·print 출력, 오류 알림)을 띄울지
		RectTransform displayBox = NewUI("DisplayBox", window);
		Place(displayBox, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -107), new Vector2(184, 22));
		SetImage(displayBox.gameObject, "set_box").raycastTarget = false;
		Toggle consoleOutput = CheckRow(displayBox, "ConsoleOutput", "콘솔 출력 표시", -11);

		// 돌아가기 (Esc)
		Button back = NewButton(window, "BackButton", "set_button", "set_button_selected", new Vector2(18, -140));
		TMP_Text backText = Text(back.transform, "Text", "돌아가기", 9, Color.white, TextAlignmentOptions.MidlineLeft);
		Stretch(backText.rectTransform, 0);
		backText.rectTransform.offsetMin = new Vector2(10, 0);
		// 키 표시: 상자 없이 오른쪽에 흐린 글자 (#430, key-badge.md와 같은 방식)
		RectTransform esc = NewUI("Esc", back.transform);
		Place(esc, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(30, 11));
		TMP_Text escText = Text(esc, "Text", "ESC", 7, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.MidlineRight);
		Stretch(escText.rectTransform, 0);

		// 캐릭터 선택 (#570) | 로그아웃 / 레이드 포기: 돌아가기 아래 한 줄에 반씩 나란히
		Button characterSelect = NewButton(window, "CharacterSelectButton", "set_button", "set_button_selected", new Vector2(18, -175), HalfButtonWidth);
		TMP_Text characterSelectText = Text(characterSelect.transform, "Text", "캐릭터 선택", 9, Color.white, TextAlignmentOptions.Center);
		Stretch(characterSelectText.rectTransform, 0);

		Button exit = NewButton(window, "ExitButton", "set_danger", "set_danger_selected", new Vector2(18 + HalfButtonWidth + ButtonGap, -175), HalfButtonWidth);
		TMP_Text exitText = Text(exit.transform, "Text", "로그아웃", 9, Danger, TextAlignmentOptions.Center);
		Stretch(exitText.rectTransform, 0);

		TMP_Text hint = Text(window, "Hint", "방향키 고르기  -  Enter 확인  -  Esc 닫기", 7, Muted, TextAlignmentOptions.Center);
		Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -214), new Vector2(200, 10));

		// ↑ ↓ 순서: 전체 → 배경음 → 효과음 → 콘솔 출력 표시 → 돌아가기 → 캐릭터 선택. 캐릭터 선택 ← → 로그아웃
		// 버튼이 꺼지면 SettingsView가 꺼진 버튼을 건너뛰도록 다시 잇는다
		Chain(master, bgm, sfx, consoleOutput, back, characterSelect);
		Navigation characterNav = characterSelect.navigation;
		characterNav.selectOnRight = exit;
		characterSelect.navigation = characterNav;
		exit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = back, selectOnLeft = characterSelect };

		SettingsView view = canvasObject.AddComponent<SettingsView>();
		var so = new SerializedObject(view);
		so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
		AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
		if (mixer != null) so.FindProperty("mixer").objectReferenceValue = mixer;
		else Debug.LogWarning($"[SettingsUIBuilder] {MixerPath}가 없어 Mixer 칸을 비워 둠 (settings.md)");
		so.FindProperty("masterSlider").objectReferenceValue = master;
		so.FindProperty("masterValueText").objectReferenceValue = masterValue;
		so.FindProperty("bgmSlider").objectReferenceValue = bgm;
		so.FindProperty("bgmValueText").objectReferenceValue = bgmValue;
		so.FindProperty("sfxSlider").objectReferenceValue = sfx;
		so.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
		so.FindProperty("consoleOutputToggle").objectReferenceValue = consoleOutput;
		so.FindProperty("backButton").objectReferenceValue = back;
		so.FindProperty("characterSelectButton").objectReferenceValue = characterSelect;
		so.FindProperty("characterSelectText").objectReferenceValue = characterSelectText;
		so.FindProperty("exitButton").objectReferenceValue = exit;
		so.FindProperty("exitText").objectReferenceValue = exitText;
		so.ApplyModifiedPropertiesWithoutUndo();

		panel.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[SettingsUIBuilder] {PrefabPath}를 만듦. Base 씬에 있는지 확인하세요.");
	}

	// 「전체 ━━━●━━ 80」 한 줄
	static (Slider, TMP_Text) VolumeRow(RectTransform box, string name, string label, float y)
	{
		// 고른 줄 테두리 (돌아가기 버튼과 같은 청록 테두리). 슬라이더의 색 전환이 이 그림을 켜고 끈다
		RectTransform highlight = NewUI(name + "Highlight", box);
		Place(highlight, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(3, y), new Vector2(178, 14));
		Image highlightImage = SetImage(highlight.gameObject, "set_button_selected");
		highlightImage.raycastTarget = false;

		TMP_Text labelText = Text(box, name + "Label", label, 8, Color.white, TextAlignmentOptions.MidlineLeft);
		Place(labelText.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(8, y), new Vector2(40, 12));

		RectTransform root = NewUI(name + "Slider", box);
		Place(root, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(46, y), new Vector2(107, 10));

		RectTransform background = NewUI("Background", root);
		Stretch(background, 0);
		background.offsetMin = new Vector2(0, 3);
		background.offsetMax = new Vector2(0, -3);
		Image track = background.gameObject.AddComponent<Image>();
		track.sprite = LoadSprite("set_white");
		track.color = Track;

		RectTransform fillArea = NewUI("Fill Area", root);
		Stretch(fillArea, 0);
		fillArea.offsetMin = new Vector2(0, 3);
		fillArea.offsetMax = new Vector2(-4, -3);
		RectTransform fill = NewUI("Fill", fillArea);
		Stretch(fill, 0);
		Image fillImage = fill.gameObject.AddComponent<Image>();
		fillImage.sprite = LoadSprite("set_white");
		fillImage.color = Teal;
		fillImage.raycastTarget = false;

		RectTransform handleArea = NewUI("Handle Slide Area", root);
		Stretch(handleArea, 0);
		handleArea.offsetMin = new Vector2(4, 0);
		handleArea.offsetMax = new Vector2(-4, 0);
		RectTransform handle = NewUI("Handle", handleArea);
		handle.sizeDelta = new Vector2(8, 0);
		Image handleImage = handle.gameObject.AddComponent<Image>();
		handleImage.sprite = LoadSprite("set_handle");
		handleImage.color = Teal;
		handleImage.preserveAspect = true;

		Slider slider = root.gameObject.AddComponent<Slider>();
		slider.fillRect = fill;
		slider.handleRect = handle;
		slider.targetGraphic = highlightImage;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = 0.8f;
		// 고르지 않은 줄은 테두리를 숨기고, 고르거나 마우스를 올린 줄만 보인다
		ColorBlock colors = slider.colors;
		colors.normalColor = Color.clear;
		colors.highlightedColor = Color.white;
		colors.selectedColor = Color.white;
		colors.pressedColor = Color.white;
		colors.disabledColor = Color.clear;
		colors.colorMultiplier = 1;
		colors.fadeDuration = 0f;
		slider.colors = colors;

		TMP_Text value = Text(box, name + "Value", "80", 7, Label, TextAlignmentOptions.MidlineRight);
		Place(value.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-8, y), new Vector2(24, 12));
		return (slider, value);
	}

	// 「콘솔 출력 표시            [✓]」 한 줄 (#536). 체크 칸(SettingsToggle)은 Enter·클릭으로 켜고 끄고, 방향키 왼쪽·오른쪽으로도 바꾼다
	// 체크 그림은 블록 편집기의 「시전 가능」 표시와 같은 그림(ui_check)을 쓴다. Galmuri 글꼴에 「✓」 글자가 없기 때문
	static Toggle CheckRow(RectTransform box, string name, string label, float y)
	{
		// 고른 줄 테두리 (음량 줄과 같은 청록 테두리). 체크 칸의 색 전환이 이 그림을 켜고 끈다. 줄 어디를 눌러도 켜고 끄게 줄 전체를 누를 수 있게 한다
		RectTransform row = NewUI(name + "Toggle", box);
		Place(row, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(3, y), new Vector2(178, 14));
		Image highlightImage = SetImage(row.gameObject, "set_button_selected");

		TMP_Text labelText = Text(row, "Label", label, 8, Color.white, TextAlignmentOptions.MidlineLeft);
		Place(labelText.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(5, 0), new Vector2(120, 12));

		// 체크 칸: 어두운 네모 바탕 위에 켜졌을 때만 청록 체크
		RectTransform checkBox = NewUI("Box", row);
		Place(checkBox, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(11, 11));
		Image boxImage = checkBox.gameObject.AddComponent<Image>();
		boxImage.sprite = LoadSprite("set_white");
		boxImage.color = Track;
		boxImage.raycastTarget = false;

		RectTransform mark = NewUI("Check", checkBox);
		Stretch(mark, 0);
		Image markImage = mark.gameObject.AddComponent<Image>();
		markImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(CheckSpritePath);
		if (markImage.sprite == null) Debug.LogWarning($"[SettingsUIBuilder] {CheckSpritePath} 스프라이트를 찾지 못함");
		markImage.color = Teal;
		markImage.raycastTarget = false;

		SettingsToggle toggle = row.gameObject.AddComponent<SettingsToggle>();
		toggle.targetGraphic = highlightImage;
		toggle.graphic = markImage;
		toggle.isOn = true;
		// 고르지 않은 줄은 테두리를 숨기고, 고르거나 마우스를 올린 줄만 보인다 (음량 줄과 같음)
		ColorBlock colors = toggle.colors;
		colors.normalColor = Color.clear;
		colors.highlightedColor = Color.white;
		colors.selectedColor = Color.white;
		colors.pressedColor = Color.white;
		colors.disabledColor = Color.clear;
		colors.colorMultiplier = 1;
		colors.fadeDuration = 0f;
		toggle.colors = colors;
		return toggle;
	}

	// 테두리 그림이 고르면 바뀌는 버튼 (마우스를 올려도 같은 그림). 너비는 창 안쪽 한 줄(184)이 기본
	static Button NewButton(RectTransform window, string name, string normal, string selected, Vector2 topLeft, float width = 184)
	{
		RectTransform r = NewUI(name, window);
		Place(r, new Vector2(0, 1), new Vector2(0, 1), topLeft, new Vector2(width, 26));
		Image image = SetImage(r.gameObject, normal);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		button.transition = Selectable.Transition.SpriteSwap;
		Sprite on = LoadSprite(selected);
		button.spriteState = new SpriteState { highlightedSprite = on, selectedSprite = on, pressedSprite = on, disabledSprite = LoadSprite("set_button") };
		return button;
	}

	static void Chain(params Selectable[] order)
	{
		for (int i = 0; i < order.Length; i++)
		{
			Navigation nav = new Navigation { mode = Navigation.Mode.Explicit };
			if (i > 0) nav.selectOnUp = order[i - 1];
			if (i < order.Length - 1) nav.selectOnDown = order[i + 1];
			order[i].navigation = nav;
		}
	}

	// 기본 도구 -------------------------------------------------------------------

	static TMP_Text Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment)
	{
		RectTransform r = NewUI(name, parent);
		TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = alignment;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.raycastTarget = false;
		return text;
	}

	static Image SetImage(GameObject go, string sprite)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.type = Image.Type.Sliced;
		return image;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
		if (sprite == null) Debug.LogWarning($"[SettingsUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r, float inset)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = new Vector2(inset, inset);
		r.offsetMax = new Vector2(-inset, -inset);
	}

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color color);
		return color;
	}
}
