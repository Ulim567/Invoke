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
	const string KeySpritePath = "Assets/_HW/UI/Hud/hud_key.png";

	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Track = Hex("3A3450");
	static readonly Color Danger = Hex("F0907E");

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
		Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 204));
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

		// 돌아가기 (Esc)
		Button back = NewButton(window, "BackButton", "set_button", "set_button_selected", new Vector2(18, -112));
		TMP_Text backText = Text(back.transform, "Text", "돌아가기", 9, Color.white, TextAlignmentOptions.MidlineLeft);
		Stretch(backText.rectTransform, 0);
		backText.rectTransform.offsetMin = new Vector2(10, 0);
		RectTransform esc = NewUI("Esc", back.transform);
		Place(esc, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-8, 0), new Vector2(16, 11));
		Image escImage = esc.gameObject.AddComponent<Image>();
		escImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(KeySpritePath);
		escImage.type = Image.Type.Sliced;
		escImage.raycastTarget = false;
		TMP_Text escText = Text(esc, "Text", "Esc", 6, Label, TextAlignmentOptions.Center);
		Stretch(escText.rectTransform, 0);

		// 로그아웃 / 레이드 포기
		Button exit = NewButton(window, "ExitButton", "set_danger", "set_danger_selected", new Vector2(18, -147));
		TMP_Text exitText = Text(exit.transform, "Text", "로그아웃", 9, Danger, TextAlignmentOptions.Center);
		Stretch(exitText.rectTransform, 0);

		TMP_Text hint = Text(window, "Hint", "방향키 고르기  -  Enter 확인  -  Esc 닫기", 7, Muted, TextAlignmentOptions.Center);
		Place(hint.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -186), new Vector2(200, 10));

		// ↑ ↓ 순서: 전체 → 배경음 → 효과음 → 돌아가기 → 로그아웃
		Chain(master, bgm, sfx, back, exit);

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
		so.FindProperty("backButton").objectReferenceValue = back;
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

	// 테두리 그림이 고르면 바뀌는 버튼 (마우스를 올려도 같은 그림)
	static Button NewButton(RectTransform window, string name, string normal, string selected, Vector2 topLeft)
	{
		RectTransform r = NewUI(name, window);
		Place(r, new Vector2(0, 1), new Vector2(0, 1), topLeft, new Vector2(184, 26));
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
