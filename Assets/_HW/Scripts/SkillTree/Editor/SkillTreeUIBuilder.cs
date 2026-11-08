using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 트리 창(LobbyUI 프리팹 안의 SkillTreePanel)을 시안 9 모양으로 바꾸는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → UI → 스킬 트리 화면 시안대로. 여러 번 눌러도 된다.<br/>
/// - 창: 민트 테두리, 왼쪽 위 「> 스킬 트리」, 오른쪽 위 버그 조각 개수(MaterialCountText)와 X, 아래 구분선<br/>
/// - 「문법 / 스킬」 탭, 트리(오른쪽 끝 세로 스크롤 막대), 오른쪽 설명 칸(왼쪽 선, 제목·내용을 쌓고 넘치면 스크롤), 주황 「해금」 버튼, 아래 범례<br/>
/// - 크기는 시안 9(640×360)를 LobbyUI 캔버스(1920×1080) 기준으로 3배 한 값. 노드 색과 설명 글자는 SkillTreeView가 그린다.
/// </para>
/// </summary>
public static class SkillTreeUIBuilder
{
	const string PrefabPath = "Assets/_HW/Lobby/LobbyUI.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string Panel = "Assets/_HW/UI/Account/acc_panel.png";
	const string Orange = "Assets/_HW/UI/Account/acc_button.png";
	const string Outline = "Assets/_HW/UI/Settings/set_button.png";
	const string OutlineOn = "Assets/_HW/UI/Settings/set_button_selected.png";
	const string White = "Assets/_HW/UI/Settings/set_white.png";

	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Muted = Hex("8A84A8");
	static readonly Color Line = Hex("443F53");
	static readonly Color Dark = Hex("1A1E3A");

	const float W = 1680, H = 966; // 시안 560×322 × 3

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/스킬 트리 화면 시안대로")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		try
		{
			Transform panel = Find(root.transform, "SkillTreePanel");
			Transform window = panel != null ? panel.Find("Window") : null;
			if (window == null)
			{
				Debug.LogError("[SkillTreeUIBuilder] LobbyUI 안에서 SkillTreePanel/Window를 찾지 못함");
				return;
			}

			// 창: 민트 테두리 (테두리가 1920 기준에서도 보이게 두껍게)
			Image windowImage = SetImage(window.gameObject, Panel);
			windowImage.pixelsPerUnitMultiplier = 0.4f;
			windowImage.color = Color.white; // 예전 색이 곱해져 있으면 보라 바탕·민트 테두리가 어둡게 나옴
			Place((RectTransform)window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));

			// 제목 「> 스킬 트리」
			TMP_Text title = window.Find("Title").GetComponent<TMP_Text>();
			Style(title, "> 스킬 트리", 32, Teal, TextAlignmentOptions.MidlineLeft, true);
			TopLeft(title.rectTransform, 36, 18, 600, 66);

			// 오른쪽 위: 버그 조각, X
			TMP_Text materials = GetOrText(window, "Materials", "버그 조각  0", 21, Muted, TextAlignmentOptions.MidlineRight);
			TopRight(materials.rectTransform, 108, 18, 360, 66);
			if (materials.GetComponent<MaterialCountText>() == null) materials.gameObject.AddComponent<MaterialCountText>();

			Transform close = window.Find("CloseButton");
			SetButton(close, Outline, OutlineOn);
			TopRight((RectTransform)close, 24, 18, 66, 66);
			Style(close.GetComponentInChildren<TMP_Text>(), "X", 26, Color.white, TextAlignmentOptions.Center, false);

			RectTransform headerLine = Child(window, "HeaderLine");
			TopStretch(headerLine, 0, 0, 102, 3);
			Solid(headerLine.gameObject, Line);

			// 탭
			Transform syntax = window.Find("SyntaxTab");
			Transform skill = window.Find("SkillTab");
			foreach (Transform tab in new[] { syntax, skill })
			{
				SetButton(tab, White, null);
				Outline outline = tab.GetComponent<Outline>() ?? tab.gameObject.AddComponent<Outline>();
				outline.effectDistance = new Vector2(3, -3);
				Style(tab.GetComponentInChildren<TMP_Text>(), tab == syntax ? "문법" : "스킬", 21, Color.white, TextAlignmentOptions.Center, true);
			}
			TopLeft((RectTransform)syntax, 36, 126, 96, 48);
			TopLeft((RectTransform)skill, 144, 126, 96, 48);

			// 트리 (왼쪽 아래 넓은 칸)
			RectTransform tree = (RectTransform)window.Find("TreeScroll");
			Stretch(tree, 36, 480 + 24, 192, 72);
			Image treeImage = tree.GetComponent<Image>();
			if (treeImage != null) treeImage.color = new Color(0, 0, 0, 0);
			BuildScrollbar(tree);

			// 설명 칸 (오른쪽, 왼쪽에 세로선)
			RectTransform detail = (RectTransform)window.Find("Detail");
			detail.anchorMin = new Vector2(1, 0);
			detail.anchorMax = new Vector2(1, 1);
			detail.pivot = new Vector2(1, 0.5f);
			detail.offsetMin = new Vector2(-480, 36);
			detail.offsetMax = new Vector2(-6, -108);
			Image detailImage = detail.GetComponent<Image>();
			if (detailImage != null) detailImage.color = new Color(0, 0, 0, 0);
			RectTransform detailLine = Child(detail, "Line");
			detailLine.anchorMin = new Vector2(0, 0);
			detailLine.anchorMax = new Vector2(0, 1);
			detailLine.pivot = new Vector2(0, 0.5f);
			detailLine.offsetMin = new Vector2(0, -36);
			detailLine.offsetMax = new Vector2(3, 0);
			Solid(detailLine.gameObject, Line);

			// 제목과 설명은 Content에 위에서부터 쌓는다 (Vertical Layout Group). 제목이 두 줄이 되면 설명이 그만큼 밀린다.
			// 해금 버튼 위까지가 보이는 칸이고, 넘치면 세로 스크롤 (DetailScroll)
			RectTransform content = BuildDetailScroll(detail);
			TMP_Text detailTitle = Find(detail, "DetailTitle").GetComponent<TMP_Text>();
			Style(detailTitle, detailTitle.text, 27, Color.white, TextAlignmentOptions.TopLeft, true);
			detailTitle.textWrappingMode = TextWrappingModes.Normal;
			TMP_Text detailBody = Find(detail, "DetailBody").GetComponent<TMP_Text>();
			Style(detailBody, detailBody.text, 20, Color.white, TextAlignmentOptions.TopLeft, false);
			detailBody.lineSpacing = 28;
			detailBody.textWrappingMode = TextWrappingModes.Normal;
			detailTitle.transform.SetParent(content, false);
			detailBody.transform.SetParent(content, false);
			detailTitle.transform.SetSiblingIndex(0);
			detailBody.transform.SetSiblingIndex(1);

			Transform unlock = detail.Find("UnlockButton");
			Button unlockButton = SetButton(unlock, Orange, null);
			ColorBlock colors = unlockButton.colors;
			colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
			colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);
			unlockButton.colors = colors;
			RectTransform unlockRect = (RectTransform)unlock;
			unlockRect.anchorMin = new Vector2(0, 0);
			unlockRect.anchorMax = new Vector2(1, 0);
			unlockRect.pivot = new Vector2(0.5f, 0);
			unlockRect.offsetMin = new Vector2(36, 36);
			unlockRect.offsetMax = new Vector2(-30, 108);
			Style(unlock.GetComponentInChildren<TMP_Text>(), "해금", 24, Dark, TextAlignmentOptions.Center, true);

			// 아래 범례
			Transform oldLegend = window.Find("Legend");
			if (oldLegend != null) Object.DestroyImmediate(oldLegend.gameObject);
			RectTransform legend = NewUI("Legend", window);
			legend.anchorMin = legend.anchorMax = new Vector2(0, 0);
			legend.pivot = new Vector2(0, 0);
			legend.anchoredPosition = new Vector2(36, 24);
			legend.sizeDelta = new Vector2(1100, 36);
			HorizontalLayoutGroup row = legend.gameObject.AddComponent<HorizontalLayoutGroup>();
			row.spacing = 12;
			row.childAlignment = TextAnchor.MiddleLeft;
			row.childControlWidth = row.childControlHeight = true;
			row.childForceExpandWidth = row.childForceExpandHeight = false;
			LegendItem(legend, Teal, Color.clear, "해금됨");
			LegendItem(legend, Hex("1A1228"), Hex("E8A867"), "해금할 수 있음");
			LegendItem(legend, Hex("1A1228"), Hex("3A3550"), "선행 노드를 아직 안 열었음");
			LegendItem(legend, Hex("1A1228"), Color.white, "지금 고른 노드");

			PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			Debug.Log("[SkillTreeUIBuilder] 스킬 트리 창을 시안 9 모양으로 바꿈 (LobbyUI 프리팹)");
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// 설명 칸의 스크롤 영역: DetailScroll(Scroll Rect) → Viewport(Rect Mask 2D) → Content(Vertical Layout Group + Content Size Fitter).
	// 설명 칸 위쪽부터 해금 버튼 바로 위까지. Content를 돌려주고, 제목·설명 글자는 부르는 쪽이 Content 아래로 옮긴다
	static RectTransform BuildDetailScroll(RectTransform detail)
	{
		RectTransform area = Child(detail, "DetailScroll");
		Stretch(area, 0, 0, 24, 120); // 아래 120: 해금 버튼(아래 36, 높이 72) 위 12

		RectTransform viewport = Child(area, "Viewport");
		Stretch(viewport, 0, 0, 0, 0);
		if (!viewport.TryGetComponent(out RectMask2D _)) viewport.gameObject.AddComponent<RectMask2D>();
		// 빈 곳에서도 휠·끌기가 먹도록 보이지 않는 Image를 깐다
		Image viewportImage = Solid(viewport.gameObject, new Color(0, 0, 0, 0));
		viewportImage.raycastTarget = true;

		RectTransform content = Child(viewport, "Content");
		content.anchorMin = new Vector2(0, 1);
		content.anchorMax = new Vector2(1, 1);
		content.pivot = new Vector2(0.5f, 1);
		content.anchoredPosition = Vector2.zero;
		content.sizeDelta = Vector2.zero;
		if (!content.TryGetComponent(out VerticalLayoutGroup column)) column = content.gameObject.AddComponent<VerticalLayoutGroup>();
		column.padding = new RectOffset(36, 30, 0, 0); // 예전 글자 칸의 왼쪽 36, 오른쪽 30
		column.spacing = 18; // 예전 제목 아래(72)와 설명 위(90) 사이
		column.childAlignment = TextAnchor.UpperLeft;
		column.childControlWidth = column.childControlHeight = true;
		column.childForceExpandWidth = true;
		column.childForceExpandHeight = false;
		if (!content.TryGetComponent(out ContentSizeFitter fitter)) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
		fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		if (!area.TryGetComponent(out ScrollRect scroll)) scroll = area.gameObject.AddComponent<ScrollRect>();
		scroll.content = content;
		scroll.viewport = viewport;
		scroll.horizontal = false;
		scroll.vertical = true;
		scroll.movementType = ScrollRect.MovementType.Clamped; // 짧은 내용이 튕기지 않게
		scroll.scrollSensitivity = 10; // 트리 칸과 같은 휠 감도 (SkillTreeView.WheelSensitivity)

		BuildScrollbar(area);
		return content;
	}

	// 스크롤 영역 오른쪽 끝의 세로 스크롤 막대 (트리 칸, 설명 칸). 내용이 칸보다 길 때 더 내려갈 수 있다는 것을 보여 준다.
	// 보이기·숨기기는 Scroll Rect가 한다 (AutoHide: 내용이 칸보다 길 때만 보임). 이미 있으면 다시 꾸미기만 한다
	static void BuildScrollbar(RectTransform area)
	{
		ScrollRect scroll = area.GetComponent<ScrollRect>();
		if (scroll == null)
		{
			Debug.LogError($"[SkillTreeUIBuilder] {area.name}에 Scroll Rect가 없어서 스크롤 막대를 만들지 못함");
			return;
		}

		// 너비 12: 캔버스 9의 얇은 막대(8px, 1280 기준)를 1920 기준으로 키운 값
		RectTransform bar = Child(area, "Scrollbar");
		bar.anchorMin = new Vector2(1, 0);
		bar.anchorMax = new Vector2(1, 1);
		bar.pivot = new Vector2(1, 0.5f);
		bar.offsetMin = new Vector2(-12, 0);
		bar.offsetMax = Vector2.zero;
		bar.SetAsLastSibling(); // Viewport보다 위에 그린다
		Image track = Solid(bar.gameObject, Hex("1A1228"));
		track.raycastTarget = true; // 막대 바탕을 누르면 그쪽으로 이동

		RectTransform slidingArea = Child(bar, "Sliding Area");
		Stretch(slidingArea, 0, 0, 0, 0);
		RectTransform handle = Child(slidingArea, "Handle");
		Stretch(handle, 0, 0, 0, 0);
		Image handleImage = Solid(handle.gameObject, Line);
		handleImage.raycastTarget = true; // 끌기

		// ??는 쓰지 않는다. 에디터에서 없는 컴포넌트의 GetComponent는 진짜 null이 아닌 값을 돌려줄 수 있음
		if (!bar.TryGetComponent(out Scrollbar scrollbar)) scrollbar = bar.gameObject.AddComponent<Scrollbar>();
		scrollbar.direction = Scrollbar.Direction.BottomToTop;
		scrollbar.handleRect = handle;
		scrollbar.targetGraphic = handleImage;
		// Tab·방향키 이동이 막대에 멈추지 않게 한다
		scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };

		scroll.verticalScrollbar = scrollbar;
		scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
	}

	static void LegendItem(RectTransform legend, Color fill, Color border, string label)
	{
		RectTransform square = NewUI("Box", legend);
		LayoutElement size = square.gameObject.AddComponent<LayoutElement>();
		size.preferredWidth = size.minWidth = 18;
		size.preferredHeight = size.minHeight = 18;
		Image image = Solid(square.gameObject, fill);
		if (border.a > 0)
		{
			Outline outline = square.gameObject.AddComponent<Outline>();
			outline.effectColor = border;
			outline.effectDistance = new Vector2(3, -3);
		}
		TMP_Text text = GetOrText(legend, "Text", label, 18, Muted, TextAlignmentOptions.MidlineLeft);
		text.gameObject.name = "Label";
		RectTransform gap = NewUI("Gap", legend);
		gap.gameObject.AddComponent<LayoutElement>().preferredWidth = 18;
	}

	// 기본 도구 -------------------------------------------------------------------

	static Transform Find(Transform parent, string name)
	{
		if (parent.name == name) return parent;
		foreach (Transform child in parent)
		{
			Transform found = Find(child, name);
			if (found != null) return found;
		}
		return null;
	}

	static Button SetButton(Transform t, string sprite, string hoverSprite)
	{
		Image image = SetImage(t.gameObject, sprite);
		image.color = Color.white;
		Button button = t.GetComponent<Button>();
		button.targetGraphic = image;
		if (hoverSprite != null)
		{
			Sprite on = AssetDatabase.LoadAssetAtPath<Sprite>(hoverSprite);
			button.transition = Selectable.Transition.SpriteSwap;
			button.spriteState = new SpriteState { highlightedSprite = on, selectedSprite = on, pressedSprite = on };
		}
		else button.transition = Selectable.Transition.ColorTint;
		return button;
	}

	static Image SetImage(GameObject go, string path)
	{
		Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (image.sprite == null) Debug.LogWarning($"[SkillTreeUIBuilder] {path}를 찾지 못함");
		image.type = Image.Type.Sliced;
		return image;
	}

	static Image Solid(GameObject go, Color color)
	{
		Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(White);
		image.type = Image.Type.Simple;
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	static void Style(TMP_Text text, string value, float size, Color color, TextAlignmentOptions alignment, bool bold)
	{
		if (text == null) return;
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = alignment;
		text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
		text.extraPadding = false;
		text.richText = true;
	}

	static TMP_Text GetOrText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment)
	{
		Transform found = parent.Find(name);
		TMP_Text text = found != null ? found.GetComponent<TMP_Text>() : NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		Style(text, value, size, color, alignment, false);
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.raycastTarget = false;
		return text;
	}

	static RectTransform Child(Transform parent, string name)
	{
		Transform t = parent.Find(name);
		return t != null ? (RectTransform)t : NewUI(name, parent);
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static void TopLeft(RectTransform r, float x, float y, float w, float h) =>
		Place(r, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

	static void TopRight(RectTransform r, float right, float y, float w, float h) =>
		Place(r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-right, -y), new Vector2(w, h));

	static void TopStretch(RectTransform r, float left, float right, float top, float height)
	{
		r.anchorMin = new Vector2(0, 1);
		r.anchorMax = new Vector2(1, 1);
		r.pivot = new Vector2(0.5f, 1);
		r.offsetMin = new Vector2(left, -top - height);
		r.offsetMax = new Vector2(-right, -top);
	}

	static void Stretch(RectTransform r, float left, float right, float top, float bottom)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.pivot = new Vector2(0.5f, 0.5f);
		r.offsetMin = new Vector2(left, bottom);
		r.offsetMax = new Vector2(-right, -top);
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
