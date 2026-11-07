using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 게임 중 HUD 프리팹(Assets/_HW/UI/Hud/HudCanvas)을 우림이 시안 8·8-1대로 만드는 에디터 도구 (#324). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 게임 HUD 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). 만든 뒤 Base 씬에 끌어다 놓는다.
/// 캔버스 기준 640x360. 위치 숫자는 시안의 아트보드 좌표를 옮긴 것.
/// </summary>
public static class HudUIBuilder
{
	const string Dir = "Assets/_HW/UI/Hud";
	const string PrefabPath = Dir + "/HudCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	static readonly string[] Keys = { "Q", "W", "E", "R", "A", "S", "D", "F" };

	static readonly Color Band = Hex("1A1430");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color HpRed = Hex("E07070");
	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color DataTeal = Hex("2F8F86");
	static readonly Color StackPurple = Hex("6A5AC9");

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/게임 HUD 만들기")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("HudCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 5; // 게임 화면 바로 위. 편집 화면(10), 레이드 결과(30), 설정(40), 로딩(100)보다 아래
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();
		HudView view = canvasObject.AddComponent<HudView>();

		RectTransform root = NewUI("Root", canvasObject.transform);
		Stretch(root);

		// 왼쪽 위 띠: 설정(Esc) | Lv | 버그 조각 | 보스 코어 ------------------------------------
		RectTransform bar = NewUI("TopBar", root);
		bar.anchorMin = bar.anchorMax = new Vector2(0, 1);
		bar.pivot = new Vector2(0, 1);
		bar.anchoredPosition = new Vector2(12, -10);
		bar.sizeDelta = new Vector2(0, 20);
		SetImage(bar.gameObject, "hud_bar");
		HorizontalLayoutGroup barLayout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
		barLayout.padding = new RectOffset(6, 6, 3, 3);
		barLayout.spacing = 6;
		barLayout.childAlignment = TextAnchor.MiddleLeft;
		barLayout.childControlWidth = barLayout.childControlHeight = true;
		barLayout.childForceExpandWidth = barLayout.childForceExpandHeight = false;
		bar.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

		// 설정 버튼: 「설정」 + Esc 키 표시
		RectTransform settings = NewUI("SettingsButton", bar);
		HorizontalLayoutGroup settingsLayout = settings.gameObject.AddComponent<HorizontalLayoutGroup>();
		settingsLayout.spacing = 3;
		settingsLayout.childAlignment = TextAnchor.MiddleLeft;
		settingsLayout.childControlWidth = settingsLayout.childControlHeight = true;
		settingsLayout.childForceExpandWidth = settingsLayout.childForceExpandHeight = false;
		Image settingsHit = settings.gameObject.AddComponent<Image>();
		settingsHit.color = new Color(0, 0, 0, 0);
		Button settingsButton = settings.gameObject.AddComponent<Button>();
		settingsButton.targetGraphic = settingsHit;
		Fit(Text(settings, "Label", "설정", 8, Color.white), 22);
		RectTransform esc = NewUI("Esc", settings);
		SetImage(esc.gameObject, "hud_key");
		Layout(esc.gameObject, 16, 11);
		TMP_Text escText = Text(esc, "Text", "Esc", 6, Label);
		Stretch(escText.rectTransform);
		escText.alignment = TextAlignmentOptions.Center;

		Divider(bar);
		TMP_Text level = Text(bar, "Level", "Lv. 1", 8, Label);
		Fit(level, 30);
		Divider(bar);
		Icon(bar);
		TMP_Text bug = Text(bar, "BugFragment", "버그 조각 0", 8, Color.white);
		Fit(bug, 52);
		Divider(bar);
		Icon(bar);
		TMP_Text core = Text(bar, "BossCore", "보스 코어 0", 8, Color.white);
		Fit(core, 48);

		// 장소 이름
		TMP_Text place = Text(root, "Place", "> 사냥터", 12, Color.white);
		place.fontStyle = FontStyles.Bold;
		Place(place.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -38), new Vector2(200, 18));

		// 아래 띠 ------------------------------------------------------------------
		RectTransform band = NewUI("BottomBand", root);
		band.anchorMin = new Vector2(0, 0);
		band.anchorMax = new Vector2(1, 0);
		band.pivot = new Vector2(0.5f, 0);
		band.sizeDelta = new Vector2(0, 60);
		band.anchoredPosition = Vector2.zero;
		Image bandImage = band.gameObject.AddComponent<Image>();
		bandImage.color = Band;
		bandImage.raycastTarget = false;

		RectTransform center = NewUI("Center", band);
		Place(center, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(240, 60));

		// HP
		TMP_Text hpLabel = Text(center, "HpLabel", "HP", 6, Label);
		Place(hpLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-110, 44), new Vector2(26, 8));
		(Image hpFill, _) = Gauge(center, "HpBar", new Vector2(-5, 44), HpRed, Image.OriginHorizontal.Left);
		TMP_Text hpText = Text(center, "HpText", "100 / 100", 6, Label);
		hpText.alignment = TextAlignmentOptions.MidlineRight;
		Place(hpText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(118, 44), new Vector2(40, 8));

		// Memory: Data·Heap 왼쪽부터, Stack 오른쪽부터
		TMP_Text memoryLabel = Text(center, "MemoryLabel", "Memory", 6, Label);
		Place(memoryLabel.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 0.5f), new Vector2(-110, 35), new Vector2(26, 8));
		(Image heapFill, RectTransform memoryBar) = Gauge(center, "MemoryBar", new Vector2(-5, 35), Teal, Image.OriginHorizontal.Left);
		Image dataFill = Fill(memoryBar, "DataFill", DataTeal, Image.OriginHorizontal.Left);
		Image stackFill = Fill(memoryBar, "StackFill", StackPurple, Image.OriginHorizontal.Right);
		TMP_Text memoryText = Text(center, "MemoryText", "0 / 0", 6, Label);
		memoryText.alignment = TextAlignmentOptions.MidlineRight;
		Place(memoryText.rectTransform, new Vector2(0.5f, 0), new Vector2(1, 0.5f), new Vector2(118, 35), new Vector2(40, 8));

		// 슬롯 8칸
		var frames = new Image[8];
		var keys = new TMP_Text[8];
		var cooldowns = new Image[8];
		var running = new Image[8];
		for (int i = 0; i < 8; i++)
		{
			RectTransform slot = NewUI("Slot" + Keys[i], center);
			Place(slot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-99 + i * 28, 5), new Vector2(22, 22));
			frames[i] = SetImage(slot.gameObject, "hud_slot");
			frames[i].raycastTarget = false;

			RectTransform runRect = NewUI("Running", slot);
			Stretch(runRect, 1);
			running[i] = runRect.gameObject.AddComponent<Image>();
			running[i].sprite = LoadSprite("hud_white");
			running[i].color = new Color(0.37f, 0.95f, 0.84f, 0.45f);
			running[i].raycastTarget = false;
			running[i].enabled = false;

			RectTransform coolRect = NewUI("Cooldown", slot);
			Stretch(coolRect, 1);
			cooldowns[i] = coolRect.gameObject.AddComponent<Image>();
			cooldowns[i].sprite = LoadSprite("hud_white");
			cooldowns[i].color = new Color(0f, 0f, 0f, 0.65f);
			cooldowns[i].type = Image.Type.Filled;
			cooldowns[i].fillMethod = Image.FillMethod.Radial360;
			cooldowns[i].fillOrigin = (int)Image.Origin360.Top;
			cooldowns[i].fillClockwise = true;
			cooldowns[i].raycastTarget = false;
			cooldowns[i].enabled = false;

			keys[i] = Text(slot, "Key", Keys[i], 9, Color.white);
			keys[i].alignment = TextAlignmentOptions.Center;
			keys[i].fontStyle = FontStyles.Bold;
			Stretch(keys[i].rectTransform);
		}

		// 오른쪽 아래: 스킬 트리 · 레벨업
		RectTransform shortcuts = NewUI("Shortcuts", band);
		Place(shortcuts, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 4), new Vector2(90, 28));
		SetImage(shortcuts.gameObject, "hud_bar");
		Button skillTree = Shortcut(shortcuts, "SkillTreeButton", "스킬 트리", new Vector2(3, 0));
		Button levelUp = Shortcut(shortcuts, "LevelUpButton", "레벨업", new Vector2(46, 0));

		// 칸 연결 ------------------------------------------------------------------
		var so = new SerializedObject(view);
		Set(so, "root", root.gameObject);
		Set(so, "settingsButton", settingsButton);
		Set(so, "levelText", level);
		Set(so, "bugFragmentText", bug);
		Set(so, "bossCoreText", core);
		Set(so, "placeText", place);
		Set(so, "hpFill", hpFill);
		Set(so, "hpText", hpText);
		Set(so, "heapFill", heapFill);
		Set(so, "dataFill", dataFill);
		Set(so, "stackFill", stackFill);
		Set(so, "memoryText", memoryText);
		Array(so, "slotFrames", frames);
		Array(so, "slotKeys", keys);
		Array(so, "slotCooldowns", cooldowns);
		Array(so, "slotRunning", running);
		Set(so, "slotSprite", LoadSprite("hud_slot"));
		Set(so, "slotEmptySprite", LoadSprite("hud_slot_empty"));
		Set(so, "slotRejectedSprite", LoadSprite("hud_slot_rejected"));
		Set(so, "skillTreeButton", skillTree);
		Set(so, "levelUpButton", levelUp);
		so.ApplyModifiedPropertiesWithoutUndo();

		// Esc: 다른 화면이 열려 있지 않을 때 설정 열기 (UIShortcut #298, 맨 위 화면만 반응)
		UIShortcut shortcut = root.gameObject.AddComponent<UIShortcut>();
		Bind(shortcut, "onCancel", view.OpenSettings);

		root.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[HudUIBuilder] {PrefabPath}를 만듦. Base 씬에 끌어다 놓으세요.");
	}

	// 부품 ---------------------------------------------------------------------

	static void Divider(RectTransform bar)
	{
		RectTransform line = NewUI("Divider", bar);
		Image image = line.gameObject.AddComponent<Image>();
		image.sprite = LoadSprite("hud_white");
		image.color = Hex("4A3A6A");
		image.raycastTarget = false;
		Layout(line.gameObject, 1, 12);
	}

	static void Icon(RectTransform bar)
	{
		RectTransform icon = NewUI("Icon", bar);
		SetImage(icon.gameObject, "hud_icon").raycastTarget = false;
		Layout(icon.gameObject, 9, 9);
	}

	// 바탕 + 왼쪽(또는 오른쪽)부터 차는 막대 하나
	static (Image, RectTransform) Gauge(RectTransform parent, string name, Vector2 position, Color color, Image.OriginHorizontal origin)
	{
		RectTransform gauge = NewUI(name, parent);
		Place(gauge, new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), position, new Vector2(156, 5));
		SetImage(gauge.gameObject, "hud_gauge").raycastTarget = false;
		return (Fill(gauge, "Fill", color, origin), gauge);
	}

	static Image Fill(RectTransform gauge, string name, Color color, Image.OriginHorizontal origin)
	{
		RectTransform r = NewUI(name, gauge);
		Stretch(r, 1);
		Image fill = r.gameObject.AddComponent<Image>();
		fill.sprite = LoadSprite("hud_white");
		fill.color = color;
		fill.type = Image.Type.Filled;
		fill.fillMethod = Image.FillMethod.Horizontal;
		fill.fillOrigin = (int)origin;
		fill.fillAmount = 0;
		fill.raycastTarget = false;
		return fill;
	}

	static Button Shortcut(RectTransform parent, string name, string label, Vector2 left)
	{
		RectTransform r = NewUI(name, parent);
		Place(r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), left, new Vector2(41, 24));
		Image hit = r.gameObject.AddComponent<Image>();
		hit.color = new Color(0, 0, 0, 0);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = hit;
		RectTransform icon = NewUI("Icon", r);
		Place(icon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -2), new Vector2(9, 9));
		SetImage(icon.gameObject, "hud_icon").raycastTarget = false;
		TMP_Text text = Text(r, "Label", label, 6, Label);
		text.alignment = TextAlignmentOptions.Bottom;
		Place(text.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(41, 9));
		return button;
	}

	static void Bind(UIShortcut shortcut, string fieldName, UnityAction action)
	{
		FieldInfo field = typeof(UIShortcut).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field == null) return;
		var unityEvent = field.GetValue(shortcut) as UnityEvent;
		if (unityEvent == null)
		{
			unityEvent = new UnityEvent();
			field.SetValue(shortcut, unityEvent);
		}
		UnityEventTools.AddPersistentListener(unityEvent, action);
	}

	// 기본 도구 -------------------------------------------------------------------

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

	// 글자 길이만큼 넓어지되 최소 너비는 지킨다 (띠의 HorizontalLayoutGroup이 글자의 preferred width를 씀)
	static void Fit(TMP_Text text, float minWidth)
	{
		LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
		element.minWidth = minWidth;
		element.minHeight = element.preferredHeight = 12;
	}

	static void Layout(GameObject go, float width, float height)
	{
		LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		element.minWidth = element.preferredWidth = width;
		element.minHeight = element.preferredHeight = height;
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
		if (sprite == null) Debug.LogWarning($"[HudUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static void Set(SerializedObject so, string property, Object value)
	{
		SerializedProperty p = so.FindProperty(property);
		if (p == null) Debug.LogWarning($"[HudUIBuilder] HudView에 {property} 칸이 없음");
		else p.objectReferenceValue = value;
	}

	static void Array(SerializedObject so, string property, Object[] values)
	{
		SerializedProperty p = so.FindProperty(property);
		p.arraySize = values.Length;
		for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r, float inset = 0)
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

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
