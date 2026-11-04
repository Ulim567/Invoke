using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 안전지대 그림과 보스 전체 공격 화면 연출을 프리팹에 붙이는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → 보스 → 안전지대·전체 공격 연출 만들기. 여러 번 눌러도 된다.<br/>
/// - Assets/_HW/Boss/SafeZone/OverloadScreen.prefab을 만든다 (경고 띠, 남은 시간, 번쩍임).<br/>
/// - SafeZone 프리팹(김도훈): 자식 Glow·Floor에 그림을 넣고 SafeZoneView를 붙인다.<br/>
/// - Gateway 프리팹(김도훈): GatewayOverloadView를 붙이고 OverloadScreen을 넣는다.
/// </para>
/// </summary>
public static class BossFxBuilder
{
	const string ArtDir = "Assets/_HW/Boss/SafeZone/";
	const string ScreenPath = ArtDir + "OverloadScreen.prefab";
	const string SafeZonePath = "Assets/_DH/Prefabs/SafeZone.prefab";
	const string GatewayPath = "Assets/_DH/Prefabs/Gateway.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	[MenuItem("Invoke/보스/안전지대·전체 공격 연출 만들기")]
	public static void Build()
	{
		OverloadScreen screen = BuildScreen();
		EditSafeZone();
		EditGateway(screen);
		AssetDatabase.SaveAssets();
		Debug.Log("[BossFxBuilder] 안전지대 그림, 전체 공격 경고·번쩍임을 붙임");
	}

	static OverloadScreen BuildScreen()
	{
		TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		var root = new GameObject("OverloadScreen", typeof(RectTransform));
		root.layer = 5;
		Canvas canvas = root.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 28;
		CanvasScaler scaler = root.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;

		RectTransform flash = NewUI("Flash", root.transform);
		flash.anchorMin = Vector2.zero;
		flash.anchorMax = Vector2.one;
		flash.offsetMin = flash.offsetMax = Vector2.zero;
		Image flashImage = flash.gameObject.AddComponent<Image>();
		flashImage.color = new Color(1, 1, 1, 0);
		flashImage.raycastTarget = false;

		RectTransform warning = NewUI("Warning", root.transform);
		warning.anchorMin = warning.anchorMax = new Vector2(0.5f, 1);
		warning.pivot = new Vector2(0.5f, 1);
		warning.anchoredPosition = new Vector2(0, -40);
		warning.sizeDelta = new Vector2(300, 38);
		Image back = warning.gameObject.AddComponent<Image>();
		back.color = new Color32(0x16, 0x1A, 0x25, 0xEB);
		back.raycastTarget = false;
		RectTransform bar = NewUI("Bar", warning);
		bar.anchorMin = new Vector2(0, 0);
		bar.anchorMax = new Vector2(1, 0);
		bar.pivot = new Vector2(0.5f, 0);
		bar.sizeDelta = new Vector2(0, 2);
		Image barImage = bar.gameObject.AddComponent<Image>();
		barImage.color = new Color32(0xE0, 0x50, 0x60, 0xFF);
		barImage.raycastTarget = false;

		Text(warning, "Title", "전체 공격! 초록 안전지대로 피하세요", 11, font, new Color32(0xFF, 0x8A, 0x96, 0xFF), 7);
		TMP_Text countdown = Text(warning, "Countdown", "3", 11, font, Color.white, -9);

		OverloadScreen view = root.AddComponent<OverloadScreen>();
		var so = new SerializedObject(view);
		so.FindProperty("warning").objectReferenceValue = warning.gameObject;
		so.FindProperty("countdownText").objectReferenceValue = countdown;
		so.FindProperty("flash").objectReferenceValue = flashImage;
		so.ApplyModifiedPropertiesWithoutUndo();

		GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
		Object.DestroyImmediate(root);
		return saved.GetComponent<OverloadScreen>();
	}

	static void EditSafeZone()
	{
		GameObject root = PrefabUtility.LoadPrefabContents(SafeZonePath);
		try
		{
			SpriteRenderer glow = Renderer(root, "Glow", "SafeZone_Glow_0", 0);
			SpriteRenderer floor = Renderer(root, "Floor", "SafeZone_Floor", 1);
			if (!root.TryGetComponent(out SafeZoneView view)) view = root.AddComponent<SafeZoneView>();
			var so = new SerializedObject(view);
			so.FindProperty("glow").objectReferenceValue = glow;
			so.FindProperty("floor").objectReferenceValue = floor;
			SerializedProperty frames = so.FindProperty("glowFrames");
			frames.arraySize = 4;
			for (int i = 0; i < 4; i++) frames.GetArrayElementAtIndex(i).objectReferenceValue = Sprite($"SafeZone_Glow_{i}");
			so.ApplyModifiedPropertiesWithoutUndo();
			PrefabUtility.SaveAsPrefabAsset(root, SafeZonePath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// 자식 그림: 아래 가운데가 기준인 그림이라 위치 0, 크기 1 (그림이 4칸 폭)
	static SpriteRenderer Renderer(GameObject root, string child, string sprite, int order)
	{
		Transform t = root.transform.Find(child);
		if (t == null)
		{
			Debug.LogError($"[BossFxBuilder] SafeZone 프리팹에 {child}가 없음");
			return null;
		}
		t.localPosition = Vector3.zero;
		t.localScale = Vector3.one;
		SpriteRenderer r = t.GetComponent<SpriteRenderer>();
		r.sprite = Sprite(sprite);
		r.color = Color.white;
		r.sortingOrder = order;
		return r;
	}

	static void EditGateway(OverloadScreen screen)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(GatewayPath);
		try
		{
			if (!root.TryGetComponent(out GatewayOverloadView view)) view = root.AddComponent<GatewayOverloadView>();
			var so = new SerializedObject(view);
			so.FindProperty("screenPrefab").objectReferenceValue = screen;
			so.ApplyModifiedPropertiesWithoutUndo();
			PrefabUtility.SaveAsPrefabAsset(root, GatewayPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static Sprite Sprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
		if (sprite == null) Debug.LogWarning($"[BossFxBuilder] {ArtDir}{name}.png를 찾지 못함");
		return sprite;
	}

	static TMP_Text Text(RectTransform parent, string name, string value, float size, TMP_FontAsset font, Color color, float y)
	{
		RectTransform r = NewUI(name, parent);
		r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
		r.sizeDelta = new Vector2(300, 16);
		r.anchoredPosition = new Vector2(0, y);
		TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = TextAlignmentOptions.Center;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.raycastTarget = false;
		return text;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}
}
