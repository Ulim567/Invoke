using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 화면 프리팹(Assets/_HW/UI/Loading/LoadingCanvas)을 우림이 시안 11대로 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 로딩 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다).
/// 동작은 이우림의 LoadingScreen 그대로 (place-scenes.md). Base 씬에 놓고 PlaceTravel의 Loading Screen 칸에 이 프리팹의 LoadingScreen을 넣는다.
/// </summary>
public static class LoadingUIBuilder
{
	const string Dir = "Assets/_HW/UI/Loading";
	const string PrefabPath = Dir + "/LoadingCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	static readonly Color Background = Hex("10131E");
	static readonly Color Teal = Hex("5EF2D6");

	[MenuItem("Invoke/UI/로딩 화면 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "Loading");
		TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("LoadingCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 100; // 모든 게임 화면 위. 접속 화면(110)만 이것보다 위
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 화면 전체를 덮는 판. Raycast Target을 켜서 뒤의 버튼이 눌리지 않게 한다
		RectTransform panel = NewUI("Panel", canvasObject.transform);
		panel.anchorMin = Vector2.zero;
		panel.anchorMax = Vector2.one;
		panel.offsetMin = panel.offsetMax = Vector2.zero;
		Image background = panel.gameObject.AddComponent<Image>();
		background.sprite = LoadSprite("load_white");
		background.color = Background;
		background.raycastTarget = true;
		CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();

		RectTransform label = NewUI("Text", panel);
		Place(label, new Vector2(0, 11), new Vector2(160, 18));
		TextMeshProUGUI text = label.gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = "Loading...";
		text.fontSize = 12;
		text.fontStyle = FontStyles.Bold;
		text.characterSpacing = 4;
		text.color = Color.white;
		text.alignment = TextAlignmentOptions.Center;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.raycastTarget = false;

		// 막대: 테두리 + 안쪽에서 움직이는 청록 조각
		RectTransform bar = NewUI("Bar", panel);
		Place(bar, new Vector2(0, -15), new Vector2(180, 5));
		Image frame = bar.gameObject.AddComponent<Image>();
		frame.sprite = LoadSprite("load_frame");
		frame.type = Image.Type.Sliced;
		frame.raycastTarget = false;

		RectTransform track = NewUI("Track", bar);
		track.anchorMin = Vector2.zero;
		track.anchorMax = Vector2.one;
		track.offsetMin = new Vector2(1, 1);
		track.offsetMax = new Vector2(-1, -1);
		track.gameObject.AddComponent<RectMask2D>();

		RectTransform segment = NewUI("Segment", track);
		segment.anchorMin = new Vector2(0, 0);
		segment.anchorMax = new Vector2(0, 1);
		segment.pivot = new Vector2(0, 0.5f);
		segment.sizeDelta = new Vector2(54, 0);
		segment.anchoredPosition = new Vector2(36, 0);
		Image fill = segment.gameObject.AddComponent<Image>();
		fill.sprite = LoadSprite("load_white");
		fill.color = Teal;
		fill.raycastTarget = false;

		LoadingBarView barView = track.gameObject.AddComponent<LoadingBarView>();
		var barSo = new SerializedObject(barView);
		barSo.FindProperty("segment").objectReferenceValue = segment;
		barSo.ApplyModifiedPropertiesWithoutUndo();

		LoadingScreen screen = canvasObject.AddComponent<LoadingScreen>();
		var so = new SerializedObject(screen);
		so.FindProperty("panelRoot").objectReferenceValue = panel.gameObject;
		so.FindProperty("canvasGroup").objectReferenceValue = group;
		so.ApplyModifiedPropertiesWithoutUndo();

		panel.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[LoadingUIBuilder] {PrefabPath}를 만듦. Base 씬에 놓고 PlaceTravel의 Loading Screen 칸에 넣으세요.");
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
		if (sprite == null) Debug.LogWarning($"[LoadingUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	// 화면 가운데 기준
	static void Place(RectTransform r, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color color);
		return color;
	}
}
