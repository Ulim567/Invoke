using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>
/// 설정 화면 프리팹(Assets/_HW/UI/Settings/SettingsCanvas)을 만드는 에디터 도구 (#292). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 설정 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). Base 씬에 끌어다 놓는다.
/// Assets/_HW/Audio/InvokeMixer가 있으면 SettingsView의 Mixer 칸에 넣는다.
/// </summary>
public static class SettingsUIBuilder
{
	const string Dir = "Assets/_HW/UI/Settings";
	const string PrefabPath = Dir + "/SettingsCanvas.prefab";
	const string MixerPath = "Assets/_HW/Audio/InvokeMixer.mixer";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string FrameDir = "Assets/_HW/UI/Frame/";

	static readonly Color Muted = new Color32(0x7C, 0x84, 0xA8, 0xFF);
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
		canvas.sortingOrder = 40; // 편집 화면(10), 레이드 결과(30)보다 위
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 오른쪽 위 「설정」 버튼
		Button open = NewButton(canvasObject.transform, "OpenButton", "ui_button", "설정", 9);
		Place((RectTransform)open.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(32, 14));

		// 창
		RectTransform window = NewUI("Window", canvasObject.transform);
		Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 132));
		SetImage(window.gameObject, "ui_window", Image.Type.Sliced);

		RectTransform bar = NewUI("TitleBar", window);
		bar.anchorMin = new Vector2(0, 1);
		bar.anchorMax = new Vector2(1, 1);
		bar.pivot = new Vector2(0.5f, 1);
		bar.sizeDelta = new Vector2(0, 20);
		bar.anchoredPosition = Vector2.zero;
		SetImage(bar.gameObject, "ui_titlebar", Image.Type.Sliced).raycastTarget = false;
		TMP_Text title = Text(bar, "Title", "설정", 11, Color.white, TextAlignmentOptions.MidlineLeft);
		Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 1), new Vector2(120, 16));

		var closeObject = NewUI("CloseButton", bar);
		Image closeImage = SetImage(closeObject.gameObject, "ui_close", Image.Type.Simple);
		Button close = closeObject.gameObject.AddComponent<Button>();
		close.targetGraphic = closeImage;
		Place(closeObject, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 1), new Vector2(14, 14));

		// 음량 두 줄
		Label(window, "BgmLabel", "BGM", -36);
		Slider bgm = NewSlider(window, "BgmSlider", -36);
		TMP_Text bgmValue = Value(window, "BgmValue", -36);
		Label(window, "SfxLabel", "효과음", -58);
		Slider sfx = NewSlider(window, "SfxSlider", -58);
		TMP_Text sfxValue = Value(window, "SfxValue", -58);

		// 화면 모드
		Label(window, "ScreenLabel", "화면", -82);
		Button windowed = NewButton(window, "WindowedButton", "ui_button", "창 모드", 9);
		Place((RectTransform)windowed.transform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(64, -82), new Vector2(70, 16));
		Button fullscreen = NewButton(window, "FullscreenButton", "ui_button", "전체 화면", 9);
		Place((RectTransform)fullscreen.transform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(140, -82), new Vector2(70, 16));

		TMP_Text hint = Text(window, "Hint", "창 모드 / 전체 화면은 빌드에서만 바뀌어요", 8, Muted, TextAlignmentOptions.Center);
		Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(220, 12));

		SettingsView view = canvasObject.AddComponent<SettingsView>();
		var so = new SerializedObject(view);
		AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
		if (mixer != null) so.FindProperty("mixer").objectReferenceValue = mixer;
		else Debug.LogWarning($"[SettingsUIBuilder] {MixerPath}가 없어 Mixer 칸을 비워 둠. Mixer를 만들고 메뉴를 다시 누르세요 (settings.md)");
		so.FindProperty("window").objectReferenceValue = window.gameObject;
		so.FindProperty("openButton").objectReferenceValue = open;
		so.FindProperty("closeButton").objectReferenceValue = close;
		so.FindProperty("bgmSlider").objectReferenceValue = bgm;
		so.FindProperty("bgmValueText").objectReferenceValue = bgmValue;
		so.FindProperty("sfxSlider").objectReferenceValue = sfx;
		so.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
		so.FindProperty("windowedButton").objectReferenceValue = windowed;
		so.FindProperty("fullscreenButton").objectReferenceValue = fullscreen;
		so.FindProperty("normalSprite").objectReferenceValue = LoadSprite("ui_button");
		so.FindProperty("selectedSprite").objectReferenceValue = LoadSprite("ui_button_accent");
		so.ApplyModifiedPropertiesWithoutUndo();

		window.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[SettingsUIBuilder] {PrefabPath}를 만듦. Base 씬에 끌어다 놓으세요.");
	}

	static void Label(RectTransform window, string name, string value, float y)
	{
		TMP_Text text = Text(window, name, value, 10, Color.white, TextAlignmentOptions.MidlineLeft);
		Place(text.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(12, y), new Vector2(50, 14));
	}

	static TMP_Text Value(RectTransform window, string name, float y)
	{
		TMP_Text text = Text(window, name, "80%", 9, Muted, TextAlignmentOptions.MidlineRight);
		Place(text.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-12, y), new Vector2(30, 14));
		return text;
	}

	static Slider NewSlider(RectTransform window, string name, float y)
	{
		RectTransform root = NewUI(name, window);
		Place(root, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(64, y), new Vector2(110, 10));

		RectTransform background = NewUI("Background", root);
		Stretch(background, 0, 0, 2, 2);
		SetImage(background.gameObject, "ui_field", Image.Type.Sliced);

		RectTransform fillArea = NewUI("Fill Area", root);
		Stretch(fillArea, 1, 1, 3, 3);
		RectTransform fill = NewUI("Fill", fillArea);
		Stretch(fill, 0, 0, 0, 0);
		SetImage(fill.gameObject, "ui_button_accent", Image.Type.Sliced).raycastTarget = false;

		RectTransform handleArea = NewUI("Handle Slide Area", root);
		Stretch(handleArea, 4, 4, 0, 0);
		RectTransform handle = NewUI("Handle", handleArea);
		handle.sizeDelta = new Vector2(8, 0);
		Image handleImage = SetImage(handle.gameObject, "ui_button", Image.Type.Sliced);

		Slider slider = root.gameObject.AddComponent<Slider>();
		slider.fillRect = fill;
		slider.handleRect = handle;
		slider.targetGraphic = handleImage;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = 0.8f;
		return slider;
	}

	static Button NewButton(Transform parent, string name, string sprite, string label, float size)
	{
		RectTransform r = NewUI(name, parent);
		Image image = SetImage(r.gameObject, sprite, Image.Type.Sliced);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		TMP_Text text = Text(r, "Text", label, size, Color.white, TextAlignmentOptions.Center);
		Stretch(text.rectTransform, 0, 0, 0, 1);
		return button;
	}

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

	static Image SetImage(GameObject go, string sprite, Image.Type type)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.type = image.sprite != null ? type : Image.Type.Simple;
		// 그림이 없으면 짙은 남색 네모로
		image.color = image.sprite != null ? Color.white : new Color32(0x16, 0x1A, 0x25, 0xF2);
		return image;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameDir + name + ".png");
		if (sprite == null) Debug.LogWarning($"[SettingsUIBuilder] {FrameDir}{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r, float left, float right, float top, float bottom)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = new Vector2(left, bottom);
		r.offsetMax = new Vector2(-right, -top);
	}

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}
}
