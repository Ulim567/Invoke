using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 설명 카드(ItemCardView)와 가운데에 뜨는 카드 창(ItemCardPopup)의 프리팹을 만드는 에디터 도구 (#544, 캔버스 9-3). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 설명 카드 만들기. 여러 번 눌러도 된다 (프리팹을 새로 만들어 덮어씀).
/// <para>
/// - 그림(Assets/_HW/UI/ItemCard/ic_*.png)도 코드로 그려 저장한다: 카드 테두리(민트 1픽셀 + 어두운 보라), 코드 칸, 알약, 블록, ㄷ자 팔, Esc 키.<br/>
/// - 프리팹은 Assets/_HW/Resources/UI/에 둔다. 게임 중에 Resources.Load로 꺼내므로 씬이나 다른 프리팹에 연결하지 않아도 된다.<br/>
/// - 단위: 640×360 기준 캔버스. 팝업 카드 가로 300 (ItemCardView.PopupWidth).
/// </para>
/// </summary>
public static class ItemCardUIBuilder
{
	const string SpriteDir = "Assets/_HW/UI/ItemCard";
	const string ResourceDir = "Assets/_HW/Resources/UI";
	const string CardPath = ResourceDir + "/ItemCard.prefab";
	const string PopupPath = ResourceDir + "/ItemCardPopup.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	// 캔버스 9-3의 색
	static readonly Color Mint = Hex("5EF2D6");
	static readonly Color CardBack = Hex("1E0E2B");
	static readonly Color BoxBack = Hex("131021");
	static readonly Color BoxLine = Hex("2E2350");
	static readonly Color Orange = Hex("F2A65A");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Dim = Hex("897B95");
	static readonly Color Soft = Hex("B8BED6");
	static readonly Color BlockText = Hex("F2F2F8");
	static readonly Color Line = Hex("3A2E58");
	static readonly Color Dark = Hex("1A1020");

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/설명 카드 만들기")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		DrawSprites();
		if (!AssetDatabase.IsValidFolder("Assets/_HW/Resources")) AssetDatabase.CreateFolder("Assets/_HW", "Resources");
		if (!AssetDatabase.IsValidFolder(ResourceDir)) AssetDatabase.CreateFolder("Assets/_HW/Resources", "UI");

		GameObject cardPrefab = BuildCard();
		BuildPopup(cardPrefab);
		AssetDatabase.SaveAssets();
		Debug.Log($"[ItemCardUIBuilder] {CardPath}, {PopupPath}를 만듦. 씬에 놓지 않아도 게임 중에 불러 쓴다.");
	}

	// 카드 ---------------------------------------------------------------------------

	static GameObject BuildCard()
	{
		RectTransform root = NewUI("ItemCard", null);
		root.sizeDelta = new Vector2(ItemCardView.PopupWidth, 100);
		LayoutElement fit = root.gameObject.AddComponent<LayoutElement>();
		fit.preferredHeight = 100;

		// 몸통: 왼쪽 위 기준. 가로는 ItemCardView가 놓인 자리에 맞춘다
		RectTransform card = NewUI("Card", root);
		card.anchorMin = card.anchorMax = card.pivot = new Vector2(0, 1);
		card.sizeDelta = new Vector2(ItemCardView.PopupWidth, 100);
		card.anchoredPosition = Vector2.zero;
		Image cardImage = Sliced(card.gameObject, "ic_frame", Color.white);
		cardImage.raycastTarget = true; // 팝업에서 카드를 눌러도 뒤의 어두운 판(닫기)이 받지 않게
		VerticalLayoutGroup column = Column(card, 5);
		column.padding = new RectOffset(12, 12, 10, 12);
		ContentSizeFitter sizer = card.gameObject.AddComponent<ContentSizeFitter>();
		sizer.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// 머리줄: 「> 스킬 해금!」 ……… [Esc] x, 아래 구분선
		RectTransform header = NewUI("Header", card);
		Column(header, 5);
		RectTransform bar = NewUI("Bar", header);
		HorizontalLayoutGroup barRow = Row(bar, 4, TextAnchor.MiddleLeft);
		barRow.childForceExpandWidth = false;
		TMP_Text headerText = Text(bar, "Title", "> 스킬 해금!", 10, Orange);
		headerText.fontStyle = FontStyles.Bold;
		Flexible(headerText.gameObject);
		Image esc = Sliced(NewUI("Esc", bar).gameObject, "ic_key", Color.white);
		Size(esc.gameObject, 18, 10);
		TMP_Text escText = Text(esc.transform, "Text", "Esc", 5, Muted);
		escText.alignment = TextAlignmentOptions.Center;
		Fill(escText.rectTransform);
		RectTransform closeRect = NewUI("CloseButton", bar);
		Image closeImage = closeRect.gameObject.AddComponent<Image>();
		closeImage.sprite = LoadSprite("ic_white");
		closeImage.color = new Color(1, 1, 1, 0); // 누르는 자리만 (보이지 않음)
		Button close = closeRect.gameObject.AddComponent<Button>();
		close.targetGraphic = closeImage;
		Size(closeRect.gameObject, 12, 12);
		TMP_Text x = Text(closeRect, "Text", "x", 9, Color.white);
		x.alignment = TextAlignmentOptions.Center;
		Fill(x.rectTransform);
		Image divider = Sliced(NewUI("Divider", header).gameObject, "ic_white", Line);
		Size(divider.gameObject, -1, 1);

		// 이름 줄: 꼬챙이 ……… [함수 > 스킬], 그 아래 영문 이름 skewer
		// 영문 이름을 옆에 두면 좁은 곳(스킬 트리 설명 칸)에서 이름·태그와 겹쳐서 아래 줄로 내린다
		RectTransform titleRow = NewUI("TitleRow", card);
		HorizontalLayoutGroup titleLayout = Row(titleRow, 6, TextAnchor.MiddleLeft);
		titleLayout.childForceExpandWidth = false;
		TMP_Text title = Text(titleRow, "Title", "꼬챙이", 14, Color.white);
		title.fontStyle = FontStyles.Bold;
		title.textWrappingMode = TextWrappingModes.Normal;
		Flexible(title.gameObject);
		Image tag = Sliced(NewUI("Tag", titleRow).gameObject, "ic_pill", Color.white);
		HorizontalLayoutGroup tagRow = Row((RectTransform)tag.transform, 0, TextAnchor.MiddleCenter);
		tagRow.padding = new RectOffset(6, 6, 2, 3);
		TMP_Text tagText = Text(tag.transform, "Text", "함수 > 스킬", 6, Dark);
		TMP_Text name = Text(card, "Name", "skewer", 8, Dim);

		// 기능
		Text(card, "SummaryLabel", "기능", 6, Muted);
		TMP_Text summary = Text(card, "Summary", "일직선 위의 몬스터를 모두 꿰뚫습니다.", 8, Color.white);
		Wrap(summary);

		// 함수 원형 / 형식
		RectTransform codeRow = NewUI("CodeRow", card);
		Column(codeRow, 3);
		TMP_Text codeLabel = Text(codeRow, "Label", "함수 원형", 6, Muted);
		Image codeBox = Sliced(NewUI("Box", codeRow).gameObject, "ic_box", Color.white);
		Column((RectTransform)codeBox.transform, 0).padding = new RectOffset(7, 7, 5, 5);
		TMP_Text code = Text(codeBox.transform, "Code", "void skewer(Target target);", 7, Color.white);
		Wrap(code);

		// 블록 사용 예 (블록 그림)
		TMP_Text pictureLabel = Text(card, "PictureLabel", "블록 사용 예", 6, Muted);
		Image pictureBox = Sliced(NewUI("Picture", card).gameObject, "ic_box", Color.white);
		VerticalLayoutGroup pictureColumn = Column((RectTransform)pictureBox.transform, 0);
		pictureColumn.padding = new RectOffset(8, 8, 7, 7);
		pictureColumn.childControlWidth = true;
		pictureColumn.childForceExpandWidth = false; // 블록은 글자만큼의 가로
		pictureColumn.childAlignment = TextAnchor.UpperLeft;

		// 사용 방법: [함수 탭] 에서 코드로 끌어다 놓기
		RectTransform usageRow = NewUI("Usage", card);
		HorizontalLayoutGroup usageLayout = Row(usageRow, 4, TextAnchor.MiddleLeft);
		usageLayout.childForceExpandWidth = false;
		Text(usageRow, "Label", "사용 방법", 6, Muted);
		Image chip = Sliced(NewUI("Tab", usageRow).gameObject, "ic_pill", BoxLine);
		Row((RectTransform)chip.transform, 0, TextAnchor.MiddleCenter).padding = new RectOffset(5, 5, 1, 2);
		TMP_Text usageTab = Text(chip.transform, "Text", "함수 탭", 6, Color.white);
		TMP_Text usageText = Text(usageRow, "Text", "에서 코드로 끌어다 놓기", 6.5f, Soft);
		Wrap(usageText);
		Flexible(usageText.gameObject);

		// 자세히 > / 펼친 내용
		RectTransform detailsRect = NewUI("Details", card);
		Image detailsImage = detailsRect.gameObject.AddComponent<Image>();
		detailsImage.sprite = LoadSprite("ic_white");
		detailsImage.color = new Color(1, 1, 1, 0);
		Button details = detailsRect.gameObject.AddComponent<Button>();
		details.targetGraphic = detailsImage;
		HorizontalLayoutGroup detailsRow = Row(detailsRect, 0, TextAnchor.MiddleLeft);
		detailsRow.childForceExpandWidth = false;
		TMP_Text detailsLabel = Text(detailsRect, "Text", "자세히 >", 6.5f, Mint);
		TMP_Text expanded = Text(card, "Expanded", "", 6.5f, Color.white);
		Wrap(expanded);
		expanded.lineSpacing = 8;

		// < 1 / 3 >
		RectTransform pager = NewUI("Pager", card);
		HorizontalLayoutGroup pagerRow = Row(pager, 10, TextAnchor.MiddleCenter);
		pagerRow.childForceExpandWidth = false;
		Button prev = TextButton(pager, "Prev", "<");
		TMP_Text page = Text(pager, "Page", "1 / 3", 6.5f, Soft);
		Button next = TextButton(pager, "Next", ">");

		// 블록 그림 부품 (꺼 둔 견본). 카드 안 맨 끝에 두고 꺼 두므로 배치에 끼지 않는다
		RectTransform templates = NewUI("Templates", card);
		LayoutElement ignore = templates.gameObject.AddComponent<LayoutElement>();
		ignore.ignoreLayout = true;
		Image rowTemplate = Piece(templates, "Row", "ic_block", new RectOffset(7, 7, 3, 4));
		Image valueTemplate = Piece(templates, "Value", "ic_pill", new RectOffset(7, 7, 2, 3));
		Image innerTemplate = Piece(templates, "Inner", "ic_arm", new RectOffset(14, 8, 4, 5));
		TMP_Text innerText = innerTemplate.GetComponentInChildren<TMP_Text>();
		innerText.color = new Color(1, 1, 1, 0.35f);
		Image bottomTemplate = Sliced(NewUI("Bottom", templates).gameObject, "ic_block", Color.white);
		Size(bottomTemplate.gameObject, 48, 5);

		ItemCardView view = root.gameObject.AddComponent<ItemCardView>();
		var so = new SerializedObject(view);
		Set(so, "card", card);
		Set(so, "header", header.gameObject);
		Set(so, "headerText", headerText);
		Set(so, "closeButton", close);
		Set(so, "titleText", title);
		Set(so, "nameText", name);
		Set(so, "categoryTag", tag);
		Set(so, "categoryText", tagText);
		Set(so, "summaryText", summary);
		Set(so, "codeLabel", codeLabel);
		Set(so, "codeRow", codeRow.gameObject);
		Set(so, "codeText", code);
		Set(so, "pictureLabel", pictureLabel);
		Set(so, "pictureBox", pictureBox.rectTransform);
		Set(so, "usageRow", usageRow.gameObject);
		Set(so, "usageTab", usageTab);
		Set(so, "usageText", usageText);
		Set(so, "detailsButton", details);
		Set(so, "detailsLabel", detailsLabel);
		Set(so, "expandedText", expanded);
		Set(so, "pager", pager.gameObject);
		Set(so, "prevButton", prev);
		Set(so, "nextButton", next);
		Set(so, "pageText", page);
		Set(so, "rowTemplate", rowTemplate);
		Set(so, "valueTemplate", valueTemplate);
		Set(so, "innerTemplate", innerTemplate);
		Set(so, "bottomTemplate", bottomTemplate);
		so.ApplyModifiedPropertiesWithoutUndo();

		templates.gameObject.SetActive(false);
		expanded.gameObject.SetActive(false);
		GameObject saved = PrefabUtility.SaveAsPrefabAsset(root.gameObject, CardPath);
		Object.DestroyImmediate(root.gameObject);
		return saved;
	}

	// 블록 그림 부품 하나: 모양 그림 + 가운데 글자, 글자만큼의 가로
	static Image Piece(RectTransform parent, string name, string sprite, RectOffset padding)
	{
		Image image = Sliced(NewUI(name, parent).gameObject, sprite, Color.white);
		HorizontalLayoutGroup row = Row(image.rectTransform, 0, TextAnchor.MiddleLeft);
		row.childForceExpandWidth = false;
		row.padding = padding;
		Text(image.transform, "Text", "", 7, BlockText);
		return image;
	}

	// 팝업 -----------------------------------------------------------------------------

	static void BuildPopup(GameObject cardPrefab)
	{
		RectTransform canvasRect = NewUI("ItemCardPopup", null);
		Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 30; // 편집 화면·로비 창(10)보다 위, 설정(40)·로딩(100)보다 아래
		CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasRect.gameObject.AddComponent<GraphicRaycaster>();

		RectTransform root = NewUI("Root", canvasRect);
		Fill(root);
		root.gameObject.AddComponent<UIShortcut>();

		// 어두운 판 (누르면 닫기)
		Image dim = Sliced(NewUI("Dim", root).gameObject, "ic_white", new Color(0.04f, 0.03f, 0.08f, 0.6f));
		dim.raycastTarget = true;
		Fill(dim.rectTransform);
		Button dimButton = dim.gameObject.AddComponent<Button>();
		dimButton.transition = Selectable.Transition.None;

		// 카드 칸: 가운데, 가로 300. 높이는 카드에 맞추고(최대 330) 넘치면 스크롤
		RectTransform scrollRect = NewUI("Scroll", root);
		scrollRect.anchorMin = scrollRect.anchorMax = scrollRect.pivot = new Vector2(0.5f, 0.5f);
		scrollRect.sizeDelta = new Vector2(ItemCardView.PopupWidth, 200);
		scrollRect.gameObject.AddComponent<RectMask2D>();
		ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
		scroll.horizontal = false;
		scroll.movementType = ScrollRect.MovementType.Clamped;
		scroll.scrollSensitivity = 20;

		RectTransform content = NewUI("Content", scrollRect);
		content.anchorMin = new Vector2(0, 1);
		content.anchorMax = new Vector2(1, 1);
		content.pivot = new Vector2(0.5f, 1);
		content.sizeDelta = Vector2.zero;
		Column(content, 0);
		content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		scroll.viewport = scrollRect;
		scroll.content = content;

		ItemCardPopup popup = canvasRect.gameObject.AddComponent<ItemCardPopup>();
		var so = new SerializedObject(popup);
		Set(so, "root", root.gameObject);
		Set(so, "dim", dimButton);
		Set(so, "cardSlot", content);
		Set(so, "scroll", scroll);
		Set(so, "viewport", scrollRect);
		so.ApplyModifiedPropertiesWithoutUndo();

		root.gameObject.SetActive(false);
		PrefabUtility.SaveAsPrefabAsset(canvasRect.gameObject, PopupPath);
		Object.DestroyImmediate(canvasRect.gameObject);
	}

	// 그림 --------------------------------------------------------------------------------
	// 흰색·투명으로 그리고 색은 Image.color로 칠하는 것과, 색까지 그린 것(카드 테두리, 코드 칸)이 있다. 1픽셀 = 캔버스 1칸 (PPU 100, 640 기준)

	static void DrawSprites()
	{
		if (!AssetDatabase.IsValidFolder(SpriteDir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "ItemCard");
		Save("ic_white", 4, 4, (x, y) => Color.white, 1);
		// 카드: 민트 1픽셀 테두리 + 어두운 보라 바탕 (캔버스 9-3)
		Save("ic_frame", 8, 8, (x, y) => x == 0 || y == 0 || x == 7 || y == 7 ? Mint : CardBack, 2);
		// 코드 칸·그림 칸: 더 어두운 바탕 + 흐린 보라 테두리
		Save("ic_box", 8, 8, (x, y) => x == 0 || y == 0 || x == 7 || y == 7 ? BoxLine : BoxBack, 2);
		// 알약 (분류 태그, 탭 칩, 값 블록): 위아래 끝이 둥근 흰 네모
		Save("ic_pill", 12, 12, (x, y) => Rounded(x, y, 12, 12, 4) ? Color.white : Color.clear, 5);
		// 블록 한 줄: 모서리 2픽셀 둥근 흰 네모 (편집 화면 블록 모양)
		Save("ic_block", 8, 8, (x, y) => Rounded(x, y, 8, 8, 2) ? Color.white : Color.clear, 3);
		// ㄷ자 블록의 팔: 왼쪽 5픽셀만 칠하고 나머지는 흐린 점선 테두리 (안쪽에 블록이 들어갈 자리)
		Save("ic_arm", 16, 8, (x, y) =>
		{
			if (x < 5) return Color.white;
			bool edge = x == 15 || y == 0 || y == 7;
			return edge && (x + y) % 2 == 0 ? new Color(1, 1, 1, 0.35f) : Color.clear;
		}, 0, new Vector4(6, 2, 2, 2));
		// Esc 키 표시: 흐린 테두리만
		Save("ic_key", 8, 8, (x, y) => (x == 0 || y == 0 || x == 7 || y == 7) && !((x == 0 || x == 7) && (y == 0 || y == 7)) ? Muted : Color.clear, 2);
		AssetDatabase.Refresh();
	}

	static bool Rounded(int x, int y, int w, int h, int r)
	{
		float cx = Mathf.Clamp(x + 0.5f, r, w - r), cy = Mathf.Clamp(y + 0.5f, r, h - r);
		float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
		return dx * dx + dy * dy <= r * r + 0.25f;
	}

	static void Save(string name, int w, int h, System.Func<int, int, Color> pixel, int border, Vector4? borders = null)
	{
		var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
		for (int y = 0; y < h; y++)
			for (int x = 0; x < w; x++)
				texture.SetPixel(x, y, pixel(x, y));
		texture.Apply();
		string path = $"{SpriteDir}/{name}.png";
		File.WriteAllBytes(path, texture.EncodeToPNG());
		Object.DestroyImmediate(texture);
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
		if (AssetImporter.GetAtPath(path) is TextureImporter importer)
		{
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			importer.spritePixelsPerUnit = 100;
			importer.filterMode = FilterMode.Point;
			importer.mipmapEnabled = false;
			importer.alphaIsTransparency = true;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.spriteBorder = borders ?? new Vector4(border, border, border, border);
			importer.SaveAndReimport();
		}
	}

	// 기본 도구 -------------------------------------------------------------------------

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		if (parent != null) go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static VerticalLayoutGroup Column(RectTransform r, float spacing)
	{
		VerticalLayoutGroup layout = r.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = spacing;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		return layout;
	}

	static HorizontalLayoutGroup Row(RectTransform r, float spacing, TextAnchor align)
	{
		HorizontalLayoutGroup layout = r.gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = spacing;
		layout.childAlignment = align;
		layout.childControlWidth = layout.childControlHeight = true;
		// 남는 가로를 Flexible로 정한 칸에만 준다. true면 그 줄이 늘어나는 칸으로 보여서, 팝업처럼 넓은 곳에서 분류 태그·탭 칩이 길게 늘어났다
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		return layout;
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
		text.richText = true;
		text.raycastTarget = false;
		return text;
	}

	static void Wrap(TMP_Text text)
	{
		text.textWrappingMode = TextWrappingModes.Normal;
		text.alignment = TextAlignmentOptions.TopLeft;
	}

	static Button TextButton(RectTransform parent, string name, string label)
	{
		RectTransform r = NewUI(name, parent);
		Image image = r.gameObject.AddComponent<Image>();
		image.sprite = LoadSprite("ic_white");
		image.color = new Color(1, 1, 1, 0);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.disabledColor = new Color(1, 1, 1, 0);
		button.colors = colors;
		Size(r.gameObject, 14, 12);
		TMP_Text text = Text(r, "Text", label, 9, Mint);
		text.alignment = TextAlignmentOptions.Center;
		Fill(text.rectTransform);
		return button;
	}

	static Image Sliced(GameObject go, string sprite, Color color)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.type = Image.Type.Sliced;
		image.color = color;
		image.raycastTarget = false;
		return image;
	}

	// 고정 크기 (w가 -1이면 가로는 배치가 정함)
	static void Size(GameObject go, float w, float h)
	{
		if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
		if (w >= 0) { element.minWidth = w; element.preferredWidth = w; }
		element.minHeight = h;
		element.preferredHeight = h;
	}

	static void Flexible(GameObject go)
	{
		if (!go.TryGetComponent(out LayoutElement element)) element = go.AddComponent<LayoutElement>();
		element.flexibleWidth = 1;
	}

	static void Fill(RectTransform r)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = r.offsetMax = Vector2.zero;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteDir}/{name}.png");
		if (sprite == null) Debug.LogWarning($"[ItemCardUIBuilder] {SpriteDir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;

	static Color Hex(string hex)
	{
		ColorUtility.TryParseHtmlString("#" + hex, out Color color);
		return color;
	}
}
