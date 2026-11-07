using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 결과 화면 프리팹(Assets/_HW/UI/Raid/RaidResultCanvas)을 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 레이드 결과 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다).
/// </summary>
public static class RaidUIBuilder
{
	const string Dir = "Assets/_HW/UI/Raid";
	const string PrefabPath = Dir + "/RaidResultCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string PanelSprite = "Assets/_HW/UI/Frame/ui_panel.png";

	[MenuItem("Invoke/UI/레이드 결과 화면 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "Raid");
		TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("RaidResultCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30; // 편집 화면(10)보다 위
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;

		// 화면 전체: 어둡게
		RectTransform root = NewUI("Root", canvasObject.transform);
		Stretch(root);
		Image dim = root.gameObject.AddComponent<Image>();
		dim.color = new Color(0, 0, 0, 0.55f);
		dim.raycastTarget = false;
		CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;

		// 가운데 띠
		RectTransform banner = NewUI("Banner", root);
		banner.anchorMin = banner.anchorMax = new Vector2(0.5f, 0.5f);
		banner.sizeDelta = new Vector2(400, 112);
		Image bannerImage = banner.gameObject.AddComponent<Image>();
		bannerImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelSprite);
		bannerImage.type = Image.Type.Sliced;
		// 패널 그림(#254)이 아직 없으면 짙은 남색 네모로
		bannerImage.color = bannerImage.sprite != null ? Color.white : new Color32(0x16, 0x1A, 0x25, 0xF2);
		bannerImage.raycastTarget = false;

		Image top = Accent(banner, "AccentTop", true);
		Image bottom = Accent(banner, "AccentBottom", false);

		TMP_Text title = Text(banner, "Title", "레이드 클리어", 22, font, new Color32(0x5E, 0xF2, 0xD6, 0xFF));
		Place(title.rectTransform, 0, 26, 400, 30);
		TMP_Text subtitle = Text(banner, "Subtitle", "보스를 쓰러뜨렸어요", 11, font, new Color32(0xE8, 0xEC, 0xFF, 0xFF));
		Place(subtitle.rectTransform, 0, 2, 400, 16);
		// 보상 줄 (시안 12): 클리어일 때만 RaidResultView가 채운다
		TMP_Text reward = Text(banner, "Reward", "보스 코어 +1    버그 조각 +5    경험치 +200", 10, font, new Color32(0xE8, 0xEC, 0xFF, 0xFF));
		Place(reward.rectTransform, 0, -18, 400, 14);
		reward.richText = true;
		reward.gameObject.SetActive(false);
		TMP_Text hint = Text(banner, "Hint", "잠시 후 돌아가요", 9, font, new Color32(0x7C, 0x84, 0xA8, 0xFF));
		Place(hint.rectTransform, 0, -38, 400, 12);

		RaidResultView view = canvasObject.AddComponent<RaidResultView>();
		var so = new SerializedObject(view);
		so.FindProperty("root").objectReferenceValue = root.gameObject;
		so.FindProperty("group").objectReferenceValue = group;
		so.FindProperty("banner").objectReferenceValue = banner;
		so.FindProperty("titleText").objectReferenceValue = title;
		so.FindProperty("subtitleText").objectReferenceValue = subtitle;
		so.FindProperty("rewardText").objectReferenceValue = reward;
		SerializedProperty accents = so.FindProperty("accents");
		accents.arraySize = 2;
		accents.GetArrayElementAtIndex(0).objectReferenceValue = top;
		accents.GetArrayElementAtIndex(1).objectReferenceValue = bottom;
		so.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[RaidUIBuilder] {PrefabPath}를 만듦. RaidDungeon 씬에 끌어다 놓으세요.");
	}

	static Image Accent(RectTransform banner, string name, bool top)
	{
		RectTransform r = NewUI(name, banner);
		r.anchorMin = new Vector2(0, top ? 1 : 0);
		r.anchorMax = new Vector2(1, top ? 1 : 0);
		r.pivot = new Vector2(0.5f, top ? 1 : 0);
		r.sizeDelta = new Vector2(-8, 2);
		r.anchoredPosition = new Vector2(0, top ? -4 : 4);
		Image image = r.gameObject.AddComponent<Image>();
		image.raycastTarget = false;
		return image;
	}

	static TMP_Text Text(RectTransform parent, string name, string value, float size, TMP_FontAsset font, Color color)
	{
		RectTransform r = NewUI(name, parent);
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

	static void Stretch(RectTransform r)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = r.offsetMax = Vector2.zero;
	}

	static void Place(RectTransform r, float x, float y, float w, float h)
	{
		r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
		r.sizeDelta = new Vector2(w, h);
		r.anchoredPosition = new Vector2(x, y);
	}
}
