using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레벨업 창 프리팹(Assets/_HW/UI/LevelUp/LevelUpCanvas)을 우림이 시안 10, 10-1대로 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 레벨업 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). 만든 뒤 Base 씬에 끌어다 놓는다.
/// 캔버스 기준 640x360. 위치 숫자는 시안의 아트보드 좌표를 옮긴 것 (창 왼쪽 위 기준).
/// </summary>
public static class LevelUpUIBuilder
{
	const string Dir = "Assets/_HW/UI/LevelUp";
	const string PrefabPath = Dir + "/LevelUpCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Line = Hex("3A2E58");
	static readonly Color Track = Hex("2A2240");
	static readonly Color Dark = Hex("1A1020");

	// 능력치 표의 칸 오른쪽 끝 (표 왼쪽 기준): 이전 / 지금 / 늘어난 값
	static readonly float[] ColumnRight = { 196, 246, 292 };

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/레벨업 화면 만들기")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("LevelUpCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10; // 편집 화면과 같은 창 높이. HUD(5)보다 위, 설정(40)·로딩(100)보다 아래
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 열고 닫는 부분: 화면을 어둡게 덮는 판 + 가운데 창
		RectTransform panel = NewUI("Panel", canvasObject.transform);
		Stretch(panel);
		Image dim = panel.gameObject.AddComponent<Image>();
		dim.sprite = LoadSprite("lv_white");
		dim.color = new Color(0.04f, 0.03f, 0.08f, 0.7f);

		RectTransform window = NewUI("Window", panel);
		window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
		window.sizeDelta = new Vector2(560, 318);
		window.anchoredPosition = Vector2.zero;
		SetImage(window.gameObject, "lv_panel");

		// 제목 줄
		TMP_Text title = Text(window, "Title", "> 레벨업", 11, Color.white);
		title.fontStyle = FontStyles.Bold;
		At(title.rectTransform, 14, 16, 200, 18);
		Image divider = NewImage(window, "Divider", "lv_white", Line);
		At(divider.rectTransform, 1, 32, 558, 1, 0f);

		Button close = NewButton(window, "CloseButton", "lv_close");
		At((RectTransform)close.transform, 530, 16, 20, 20);
		TMP_Text x = Text(close.transform, "Text", "x", 9, Color.white);
		x.alignment = TextAlignmentOptions.Center;
		Fill(x.rectTransform);

		// 왼쪽: 레벨 줄 「Lv. 9 > 12」 (결과 창에서만 9와 화살표)
		RectTransform levelRow = NewUI("LevelRow", window);
		At(levelRow, 21, 70, 200, 40);
		HorizontalLayoutGroup row = levelRow.gameObject.AddComponent<HorizontalLayoutGroup>();
		row.spacing = 5;
		row.childAlignment = TextAnchor.LowerLeft;
		row.childControlWidth = row.childControlHeight = true;
		row.childForceExpandWidth = row.childForceExpandHeight = false;
		row.padding = new RectOffset(0, 0, 0, 4);
		TMP_Text lv = Text(levelRow, "Lv", "Lv.", 8, Label);
		lv.alignment = TextAlignmentOptions.BottomLeft;
		lv.margin = new Vector4(0, 0, 0, 3);
		TMP_Text fromLevel = Text(levelRow, "FromLevel", "9", 18, Muted);
		fromLevel.alignment = TextAlignmentOptions.BottomLeft;
		fromLevel.margin = new Vector4(0, 0, 0, 1);
		Image arrow = NewImage(levelRow, "Arrow", "lv_arrow", Label);
		arrow.preserveAspect = true;
		LayoutElement arrowSize = arrow.gameObject.AddComponent<LayoutElement>();
		arrowSize.preferredWidth = 13;
		arrowSize.preferredHeight = 22;
		TMP_Text level = Text(levelRow, "Level", "12", 34, Teal);
		level.fontStyle = FontStyles.Bold;
		level.alignment = TextAlignmentOptions.BottomLeft;

		TMP_Text maxLevel = Text(window, "MaxLevel", "최대 레벨 30", 6, Muted);
		At(maxLevel.rectTransform, 21, 96, 120, 9);

		TMP_Text expLabel = Text(window, "ExpLabel", "경험치", 7, Label);
		At(expLabel.rectTransform, 21, 115, 80, 10);
		TMP_Text exp = Text(window, "Exp", "0 / 100", 7, Color.white);
		exp.alignment = TextAlignmentOptions.MidlineRight;
		exp.fontStyle = FontStyles.Bold;
		At(exp.rectTransform, 111, 115, 100, 10);

		Image expFrame = NewImage(window, "ExpBar", "lv_row", Color.white);
		expFrame.type = Image.Type.Sliced;
		At(expFrame.rectTransform, 21, 128, 190, 8);
		Image expBack = NewImage(expFrame.transform, "Back", "lv_white", Track);
		Fill(expBack.rectTransform, 1);
		Image expFill = NewImage(expFrame.transform, "Fill", "lv_white", Teal);
		Fill(expFill.rectTransform, 1);
		expFill.type = Image.Type.Filled;
		expFill.fillMethod = Image.FillMethod.Horizontal;
		expFill.fillOrigin = (int)Image.OriginHorizontal.Left;
		expFill.fillAmount = 0.4f;

		TMP_Text expLeft = Text(window, "ExpLeft", "다음 레벨까지 60", 6, Label);
		At(expLeft.rectTransform, 21, 141, 190, 9);

		// 오른쪽: 능력치 표
		RectTransform table = NewUI("StatTable", window);
		table.anchorMin = table.anchorMax = table.pivot = new Vector2(0, 1);
		table.anchoredPosition = new Vector2(235, -45);
		table.sizeDelta = new Vector2(302, 210);

		TMP_Text statHeader = Text(table, "Header", "능력치", 6, Muted);
		At(statHeader.rectTransform, 10, 6, 80, 9);
		var headers = new TMP_Text[3];
		for (int c = 0; c < 3; c++)
		{
			headers[c] = Text(table, "Header" + c, "", 6, Muted);
			headers[c].alignment = TextAlignmentOptions.MidlineRight;
			At(headers[c].rectTransform, ColumnRight[c] - 50, 6, 50, 9);
		}

		int rows = LevelUpView.Rows;
		var before = new TMP_Text[rows];
		var now = new TMP_Text[rows];
		var diff = new TMP_Text[rows];
		for (int i = 0; i < rows; i++)
		{
			Image rowImage = NewImage(table, "Row" + i, "lv_row", Color.white);
			rowImage.type = Image.Type.Sliced;
			At(rowImage.rectTransform, 0, 17 + i * 32 + 13, 302, 26);

			TMP_Text name = Text(rowImage.transform, "Name", LevelUpView.StatName(i), 8, Color.white);
			At(name.rectTransform, 10, 13, 100, 12);
			before[i] = Value(rowImage.transform, "Before", 0, 7, Muted, false);
			now[i] = Value(rowImage.transform, "Now", 1, 8, Color.white, true);
			diff[i] = Value(rowImage.transform, "Diff", 2, 7, Muted, true);
		}

		// 확인 [Enter] (결과 창)
		Button confirm = NewButton(window, "ConfirmButton", "lv_button");
		At((RectTransform)confirm.transform, 467, 290, 71, 25);
		TMP_Text ok = Text(confirm.transform, "Text", "확인", 9, Dark);
		ok.fontStyle = FontStyles.Bold;
		ok.alignment = TextAlignmentOptions.Center;
		At(ok.rectTransform, 10, 12.5f, 34, 14);
		Image key = NewImage(confirm.transform, "Enter", "lv_key", Color.white);
		key.type = Image.Type.Sliced;
		At(key.rectTransform, 44, 12.5f, 22, 11);
		TMP_Text enter = Text(key.transform, "Text", "Enter", 5, Color.white);
		enter.alignment = TextAlignmentOptions.Center;
		Fill(enter.rectTransform);

		LevelUpView view = canvasObject.AddComponent<LevelUpView>();
		var so = new SerializedObject(view);
		Set(so, "panelRoot", panel.gameObject);
		Set(so, "titleText", title);
		Set(so, "closeButton", close);
		Set(so, "confirmButton", confirm);
		Set(so, "fromLevelText", fromLevel);
		Set(so, "arrow", arrow.gameObject);
		Set(so, "levelText", level);
		Set(so, "maxLevelText", maxLevel);
		Set(so, "expText", exp);
		Set(so, "expFill", expFill);
		Set(so, "expLeftText", expLeft);
		Array(so, "headerTexts", headers);
		Array(so, "beforeTexts", before);
		Array(so, "nowTexts", now);
		Array(so, "diffTexts", diff);
		so.ApplyModifiedPropertiesWithoutUndo();

		panel.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[LevelUpUIBuilder] {PrefabPath}를 만듦. Base 씬에 끌어다 놓으세요.");
	}

	// 표 한 칸 (오른쪽 정렬)
	static TMP_Text Value(Transform row, string name, int column, float size, Color color, bool bold)
	{
		TMP_Text text = Text(row, name, "", size, color);
		text.alignment = TextAlignmentOptions.MidlineRight;
		if (bold) text.fontStyle = FontStyles.Bold;
		At(text.rectTransform, ColumnRight[column] - 50, 13, 50, 12);
		return text;
	}

	// 기본 도구 -------------------------------------------------------------------

	// 부모 왼쪽 위 기준. x는 왼쪽 끝, y는 세로 가운데 (pivotY 0.5)
	static void At(RectTransform r, float x, float y, float width, float height, float pivotY = 0.5f)
	{
		r.anchorMin = r.anchorMax = new Vector2(0, 1);
		r.pivot = new Vector2(0, pivotY);
		r.sizeDelta = new Vector2(width, height);
		r.anchoredPosition = new Vector2(x, -y);
	}

	static void Fill(RectTransform r, float inset = 0)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = new Vector2(inset, inset);
		r.offsetMax = new Vector2(-inset, -inset);
	}

	static void Stretch(RectTransform r) => Fill(r);

	static Button NewButton(RectTransform parent, string name, string sprite)
	{
		RectTransform r = NewUI(name, parent);
		Image image = SetImage(r.gameObject, sprite);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		return button;
	}

	static Image NewImage(Transform parent, string name, string sprite, Color color)
	{
		RectTransform r = NewUI(name, parent);
		Image image = r.gameObject.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	static TMP_Text Text(Transform parent, string name, string value, float size, Color color)
	{
		RectTransform r = NewUI(name, parent);
		TextMeshProUGUI text = r.gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = TextAlignmentOptions.MidlineLeft;
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
		if (sprite == null) Debug.LogWarning($"[LevelUpUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;

	static void Array(SerializedObject so, string name, Object[] values)
	{
		SerializedProperty property = so.FindProperty(name);
		property.arraySize = values.Length;
		for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
	}

	static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color color);
		return color;
	}
}
