using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 편집 화면(BlockEditorCanvas)과 장착 화면(SlotEquipCanvas), 블록 프리팹을 이우림의 「블록 견본」 디자인으로 한 번에 바꾸는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기.<br/>
/// - Assets/_HW/UI/Common에 ListItem, EquipItem, ListHeader, SaveButton, CompileButton, PaletteGroup, SlotCard 프리팹을 만든다 (있으면 다시 만든다).<br/>
/// - 블록 프리팹(Block, BlockValueSlot, BlockField, BlockButton)에 블록 모양 그림(Assets/_HW/UI/BlockCoding/Shapes)을 넣는다.<br/>
/// - 두 캔버스 프리팹의 배치·그림(Assets/_HW/UI/Frame)을 바꾸고, BlockEditor·SlotEquipPanel의 칸을 채운다.<br/>
/// - 여러 번 눌러도 된다. 결과가 마음에 안 들면 git으로 되돌린다 (git restore Assets/_HW/UI).
/// </para>
/// </summary>
public static class InvokeUIBuilder
{
	const string FrameDir = "Assets/_HW/UI/Frame/";
	const string ShapeDir = "Assets/_HW/UI/BlockCoding/Shapes/";
	const string BlockDir = "Assets/_HW/UI/BlockCoding/";
	const string CommonDir = "Assets/_HW/UI/Common";
	const string EditorCanvasPath = "Assets/_HW/UI/BlockCoding/BlockEditorCanvas.prefab";
	const string EquipCanvasPath = "Assets/_HW/UI/SlotEquipCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	static readonly Color Gold = Hex("E8ECFF");   // 패널 머리글 글자 (견본: 흰색)
	static readonly Color Muted = Hex("7C84A8");  // 작은 글자 (슬롯, 페이지 2 / 5, 저장 상태)
	static readonly Color Workspace = Hex("141823");

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/편집·장착 화면 새 디자인으로 만들기")]
	public static void BuildAll()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		if (font == null) Debug.LogWarning($"[InvokeUIBuilder] {FontPath}를 찾지 못해 기본 글꼴을 씀");
		if (!AssetDatabase.IsValidFolder(CommonDir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "Common");

		ListItemView item = BuildListItem("ListItem", "ui_item");
		ListItemView equipItem = BuildListItem("EquipItem", "ui_card");
		ListHeaderView header = BuildListHeader();
		Button save = BuildTopButton("SaveButton", "ui_button", "저장", Color.white);
		Button compile = BuildTopButton("CompileButton", "ui_button_accent", "컴파일", Hex("1A1E3A"));
		RectTransform paletteGroup = BuildPaletteGroup();
		RectTransform dropdown = BuildDropdownPopup();
		RectTransform marker = BuildInsertMarker();
		ErrorItemView errorItem = BuildErrorItem();
		RectTransform errorFrame = BuildErrorFrame();
		SlotCardView card = BuildSlotCard();

		StyleBlockPrefabs();
		BuildEditorCanvas(item, header, save, compile, paletteGroup, dropdown, marker, errorItem, errorFrame);
		BuildEquipCanvas(equipItem, card);

		AssetDatabase.SaveAssets();
		Debug.Log("[InvokeUIBuilder] 편집·장착 화면을 새 디자인으로 바꿈. 연습장에서 확인하세요.");
	}

	// 공용 프리팹 ---------------------------------------------------------------

	static ListItemView BuildListItem(string prefabName, string normal)
	{
		GameObject root = NewUI(prefabName, null);
		Image bg = SetImage(root, normal, Image.Type.Sliced);
		Button button = root.AddComponent<Button>();
		button.targetGraphic = bg;
		HorizontalLayoutGroup row = root.AddComponent<HorizontalLayoutGroup>();
		row.padding = new RectOffset(4, 4, 0, 0);
		row.spacing = 4;
		row.childAlignment = TextAnchor.MiddleLeft;
		row.childControlWidth = row.childControlHeight = true;
		row.childForceExpandWidth = false;
		row.childForceExpandHeight = true;
		// 높이를 늘리지 않는다 (Horizontal Layout Group이 세로로 늘어나고 싶다고 알리는 것을 막음)
		LayoutElement itemSize = root.AddComponent<LayoutElement>();
		itemSize.preferredHeight = 16;
		itemSize.flexibleHeight = 0;

		GameObject tag = NewUI("Tag", root.transform);
		Image tagImage = tag.AddComponent<Image>();
		tagImage.color = new Color(1, 1, 1, 0);
		tagImage.raycastTarget = false;
		tag.AddComponent<LayoutElement>().preferredWidth = 3;

		TMP_Text prefix = Text(root.transform, "Prefix", "1", 9, Muted, TextAlignmentOptions.Center);
		prefix.gameObject.AddComponent<LayoutElement>().preferredWidth = 8;
		TMP_Text name = Text(root.transform, "Name", "함수", 11, Color.white, TextAlignmentOptions.Left);
		LayoutElement nameLayout = name.gameObject.AddComponent<LayoutElement>();
		nameLayout.preferredWidth = 1;
		nameLayout.flexibleWidth = 1;
		TMP_Text detail = Text(root.transform, "Detail", "Q", 9, Hex("8890C0"), TextAlignmentOptions.Right);

		ListItemView view = root.AddComponent<ListItemView>();
		Set(view, "button", button);
		Set(view, "background", bg);
		Set(view, "tagImage", tagImage);
		Set(view, "prefixText", prefix);
		Set(view, "nameText", name);
		Set(view, "detailText", detail);
		Set(view, "normalSprite", LoadSprite(normal));
		Set(view, "selectedSprite", LoadSprite("ui_item_selected"));
		Set(view, "boxSprite", LoadSprite("ui_card"));
		return SavePrefab(root, prefabName).GetComponent<ListItemView>();
	}

	static ListHeaderView BuildListHeader()
	{
		GameObject root = NewUI("ListHeader", null);
		HorizontalLayoutGroup row = root.AddComponent<HorizontalLayoutGroup>();
		row.padding = new RectOffset(4, 2, 1, 1);
		row.spacing = 4;
		row.childAlignment = TextAnchor.MiddleLeft;
		row.childControlWidth = row.childControlHeight = true;
		row.childForceExpandWidth = false;
		row.childForceExpandHeight = false;
		LayoutElement headerSize = root.AddComponent<LayoutElement>();
		headerSize.preferredHeight = 16;
		headerSize.flexibleHeight = 0;

		GameObject icon = NewUI("Icon", root.transform);
		Image iconImage = icon.AddComponent<Image>();
		iconImage.raycastTarget = false;
		LayoutElement iconSize = icon.AddComponent<LayoutElement>();
		iconSize.preferredWidth = 6;
		iconSize.preferredHeight = 6;
		iconSize.flexibleHeight = 0;
		TMP_Text title = Text(root.transform, "Title", "슬롯", 9, Muted, TextAlignmentOptions.Left);
		LayoutElement titleLayout = title.gameObject.AddComponent<LayoutElement>();
		titleLayout.preferredWidth = 1;
		titleLayout.flexibleWidth = 1;

		Button add = NewButton(root.transform, "AddButton", "ui_button", "+", 11, Color.white);
		LayoutElement addSize = add.gameObject.AddComponent<LayoutElement>();
		addSize.preferredWidth = 13;
		addSize.preferredHeight = 13;

		ListHeaderView view = root.AddComponent<ListHeaderView>();
		Set(view, "titleText", title);
		Set(view, "addButton", add);
		Set(view, "iconImage", iconImage);
		return SavePrefab(root, "ListHeader").GetComponent<ListHeaderView>();
	}

	static Button BuildTopButton(string prefabName, string sprite, string label, Color textColor)
	{
		Button button = NewButton(null, prefabName, sprite, label, 10, textColor);
		LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
		layout.preferredWidth = 38;
		layout.preferredHeight = 16;
		return SavePrefab(button.gameObject, prefabName).GetComponent<Button>();
	}

	// 블록 목록의 분류 하나: 칩을 줄줄이 놓는 칸
	static RectTransform BuildPaletteGroup()
	{
		GameObject root = NewUI("PaletteGroup", null);
		FlowLayoutGroup flow = root.AddComponent<FlowLayoutGroup>();
		flow.padding = new RectOffset(2, 2, 0, 4);
		return SavePrefab(root, "PaletteGroup").GetComponent<RectTransform>();
	}

	// 드롭다운: 화면 전체를 덮는 투명 버튼(바깥을 누르면 닫힘) + 고를 것 목록
	static RectTransform BuildDropdownPopup()
	{
		GameObject root = NewUI("DropdownPopup", null);
		Stretch(Rect(root), 0, 0, 0, 0);
		Image blocker = root.AddComponent<Image>();
		blocker.color = new Color(0, 0, 0, 0);
		Button close = root.AddComponent<Button>();
		close.transition = Selectable.Transition.None;

		GameObject list = NewUI("List", root.transform);
		RectTransform listRect = Rect(list);
		listRect.anchorMin = listRect.anchorMax = new Vector2(0.5f, 0.5f);
		listRect.pivot = new Vector2(0, 1);
		SetImage(list, "ui_panel", Image.Type.Sliced);
		VerticalLayoutGroup layout = list.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(2, 2, 2, 2);
		layout.spacing = 1;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		ContentSizeFitter fit = list.AddComponent<ContentSizeFitter>();
		fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
		fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return SavePrefab(root, "DropdownPopup").GetComponent<RectTransform>();
	}

	// 오류 창의 오류 하나: 빨간 세로줄, 오류가 난 줄, 이유, 「블록 보기」
	static ErrorItemView BuildErrorItem()
	{
		GameObject root = NewUI("ErrorItem", null);
		SetImage(root, "ui_card", Image.Type.Sliced);
		VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(7, 4, 4, 4);
		layout.spacing = 2;
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = layout.childForceExpandHeight = false;
		LayoutElement size = root.AddComponent<LayoutElement>();
		size.flexibleHeight = 0;

		GameObject bar = NewUI("Bar", root.transform);
		Image barImage = bar.AddComponent<Image>();
		barImage.color = Hex("E05060");
		barImage.raycastTarget = false;
		bar.AddComponent<LayoutElement>().ignoreLayout = true;
		RectTransform barRect = Rect(bar);
		barRect.anchorMin = new Vector2(0, 0);
		barRect.anchorMax = new Vector2(0, 1);
		barRect.pivot = new Vector2(0, 0.5f);
		barRect.offsetMin = new Vector2(1, 1);
		barRect.offsetMax = new Vector2(3, -1);

		TMP_Text code = Text(root.transform, "Code", "hp = hpp - 1;", 10, Color.white, TextAlignmentOptions.TopLeft);
		code.textWrappingMode = TextWrappingModes.Normal;
		code.overflowMode = TextOverflowModes.Overflow;
		code.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
		TMP_Text message = Text(root.transform, "Message", "선언하지 않은 변수 hpp", 9, Muted, TextAlignmentOptions.TopLeft);
		message.textWrappingMode = TextWrappingModes.Normal;
		message.overflowMode = TextOverflowModes.Overflow;
		message.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
		Button show = NewButton(root.transform, "ShowButton", "ui_button", "블록 보기", 9, Color.white);
		LayoutElement showSize = show.gameObject.AddComponent<LayoutElement>();
		showSize.preferredWidth = 44;
		showSize.preferredHeight = 13;
		showSize.flexibleWidth = 0;

		ErrorItemView view = root.AddComponent<ErrorItemView>();
		Set(view, "codeText", code);
		Set(view, "messageText", message);
		Set(view, "showButton", show);
		return SavePrefab(root, "ErrorItem").GetComponent<ErrorItemView>();
	}

	// 오류가 난 블록에 씌우는 빨간 테두리와 오른쪽 위 !
	static RectTransform BuildErrorFrame()
	{
		GameObject root = NewUI("ErrorFrame", null);
		Stretch(Rect(root), -2, -2, -2, -2);
		Image frame = root.AddComponent<Image>();
		frame.sprite = LoadSprite("ui_frame");
		frame.type = Image.Type.Sliced;
		frame.color = Hex("E05060");
		frame.raycastTarget = false;
		root.AddComponent<LayoutElement>().ignoreLayout = true;

		GameObject badge = NewUI("Badge", root.transform);
		Place(Rect(badge), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(10, 10));
		Image badgeImage = SetImage(badge, "ui_badge", Image.Type.Simple);
		badgeImage.color = Hex("E05060");
		badgeImage.raycastTarget = false;
		TMP_Text mark = Text(badge.transform, "Text", "!", 9, Color.white, TextAlignmentOptions.Center);
		mark.overflowMode = TextOverflowModes.Overflow;
		Stretch(mark.rectTransform, 0, 0, 0, 1);
		return SavePrefab(root, "ErrorFrame").GetComponent<RectTransform>();
	}

	// 블록을 끄는 동안 들어갈 자리에 보이는 보라색 줄
	static RectTransform BuildInsertMarker()
	{
		GameObject root = NewUI("InsertMarker", null);
		Place(Rect(root), new Vector2(0.5f, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(70, 2));
		Image image = root.AddComponent<Image>();
		image.color = Hex("8B9CFF");
		image.raycastTarget = false;
		return SavePrefab(root, "InsertMarker").GetComponent<RectTransform>();
	}

	// 블록 프리팹 ----------------------------------------------------------------

	static void StyleBlockPrefabs()
	{
		EditPrefab(BlockDir + "Block.prefab", root =>
		{
			SetShape(root, "blk_stmt", Color.white);
			VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(root);
			layout.spacing = 0;
			layout.childControlWidth = layout.childControlHeight = true;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			// 블록은 자기 내용만큼만 넓다 (편집 영역 폭을 꽉 채우지 않음)
			LayoutElement blockSize = GetOrAdd<LayoutElement>(root);
			blockSize.flexibleWidth = 0;
			blockSize.flexibleHeight = 0;

			Transform row = root.transform.Find("Row");
			HorizontalLayoutGroup rowLayout = GetOrAdd<HorizontalLayoutGroup>(row.gameObject);
			rowLayout.spacing = 3;
			rowLayout.childAlignment = TextAnchor.MiddleLeft;

			Transform inner = root.transform.Find("Inner");
			SetShape(inner.gameObject, "blk_inner", Workspace);
			VerticalLayoutGroup innerLayout = GetOrAdd<VerticalLayoutGroup>(inner.gameObject);
			innerLayout.padding = new RectOffset(12, 2, 3, 3);
			innerLayout.spacing = 2;
			innerLayout.childAlignment = TextAnchor.UpperLeft;
			innerLayout.childControlWidth = innerLayout.childControlHeight = true;
			innerLayout.childForceExpandWidth = false;
			innerLayout.childForceExpandHeight = false;
			LayoutElement innerSize = GetOrAdd<LayoutElement>(inner.gameObject);
			innerSize.minHeight = 14;

			BlockView view = root.GetComponent<BlockView>();
			Set(view, "statementSprite", LoadShape("blk_stmt"));
			Set(view, "valueSprite", LoadShape("blk_value"));
			Set(view, "chipSprite", LoadShape("blk_chip"));
			Set(view, "innerSprite", LoadShape("blk_inner"));
			Set(view, "nameFieldSprite", LoadShape("blk_underline"));
			Set(view, "variableFieldSprite", LoadShape("blk_dashed"));
		});

		EditPrefab(BlockDir + "BlockValueSlot.prefab", root =>
		{
			SetShape(root, "blk_slot", new Color(0, 0, 0, 0.35f));
			HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(root);
			layout.padding = new RectOffset(4, 4, 1, 1);
			layout.childAlignment = TextAnchor.MiddleCenter;
			LayoutElement size = GetOrAdd<LayoutElement>(root);
			size.minWidth = 18;
			size.minHeight = 13;
			size.flexibleWidth = 0;
			size.flexibleHeight = 0;
		});

		// 글자 칸 바탕은 투명 (값 칸의 알약, 이름 칸의 밑줄이 대신 보인다)
		EditPrefab(BlockDir + "BlockField.prefab", root =>
		{
			Image image = GetOrAdd<Image>(root);
			image.sprite = null;
			image.color = new Color(0, 0, 0, 0);
		});

		// 타입·연산자 고르기 버튼: 밝은 작은 네모
		EditPrefab(BlockDir + "BlockButton.prefab", root =>
		{
			SetShape(root, "blk_option", new Color(1, 1, 1, 0.22f));
			HorizontalLayoutGroup layout = GetOrAdd<HorizontalLayoutGroup>(root);
			layout.padding = new RectOffset(3, 3, 0, 0);
			layout.spacing = 2;
			layout.childAlignment = TextAnchor.MiddleCenter;
			// 드롭다운 표시 ▾ (작은 삼각형 그림)
			Transform arrow = root.transform.Find("Arrow");
			GameObject arrowObject = arrow != null ? arrow.gameObject : NewUI("Arrow", root.transform);
			Image arrowImage = GetOrAdd<Image>(arrowObject);
			arrowImage.sprite = LoadShape("blk_arrow");
			arrowImage.color = new Color(1, 1, 1, 0.8f);
			arrowImage.raycastTarget = false;
			LayoutElement arrowSize = GetOrAdd<LayoutElement>(arrowObject);
			arrowSize.preferredWidth = 5;
			arrowSize.preferredHeight = 3;
			arrowSize.flexibleHeight = 0;
			arrowObject.transform.SetAsLastSibling();
		});
	}

	static void EditPrefab(string path, System.Action<GameObject> edit)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(path);
		try
		{
			edit(root);
			PrefabUtility.SaveAsPrefabAsset(root, path);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	static void SetShape(GameObject go, string sprite, Color color)
	{
		Image image = GetOrAdd<Image>(go);
		image.sprite = LoadShape(sprite);
		image.type = Image.Type.Sliced;
		image.color = color;
	}

	static Sprite LoadShape(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShapeDir + name + ".png");
		if (sprite == null) Debug.LogWarning($"[InvokeUIBuilder] {ShapeDir}{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static SlotCardView BuildSlotCard()
	{
		GameObject root = NewUI("SlotCard", null);
		Rect(root).sizeDelta = new Vector2(65, 40);
		Image bg = SetImage(root, "ui_slot", Image.Type.Sliced);
		Button button = root.AddComponent<Button>();
		button.targetGraphic = bg;

		GameObject key = NewUI("Key", root.transform);
		Image keyImage = SetImage(key, "ui_key", Image.Type.Simple);
		keyImage.raycastTarget = false;
		Place(Rect(key), new Vector2(0, 1), new Vector2(0, 1), new Vector2(3, -3), new Vector2(13, 13));
		TMP_Text keyText = Text(key.transform, "Text", "Q", 9, Hex("141829"), TextAlignmentOptions.Center);
		Stretch(keyText.rectTransform, 0, 0, 0, 1);

		TMP_Text name = Text(root.transform, "Name", "비어 있음", 11, Color.white, TextAlignmentOptions.Center);
		Stretch(name.rectTransform, 2, 2, 14, 2);
		name.enableAutoSizing = true;
		name.fontSizeMin = 7;
		name.fontSizeMax = 11;

		SlotCardView view = root.AddComponent<SlotCardView>();
		Set(view, "button", button);
		Set(view, "background", bg);
		Set(view, "keyText", keyText);
		Set(view, "nameText", name);
		Set(view, "normalSprite", LoadSprite("ui_slot"));
		Set(view, "selectedSprite", LoadSprite("ui_slot_selected"));
		return SavePrefab(root, "SlotCard").GetComponent<SlotCardView>();
	}

	// 편집 화면 ------------------------------------------------------------------

	static void BuildEditorCanvas(ListItemView item, ListHeaderView header, Button save, Button compile, RectTransform paletteGroup, RectTransform dropdown, RectTransform marker, ErrorItemView errorItem, RectTransform errorFrame)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(EditorCanvasPath);
		try
		{
			Transform window = root.transform.Find("Window");
			SetImage(window.gameObject, "ui_window", Image.Type.Sliced);

			// 견본에는 제목줄이 없다. 지난번에 만든 제목줄이 있으면 닫기 버튼을 꺼내고 지운다
			Transform close = window.Find("CloseButton") ?? window.Find("TitleBar/CloseButton");
			if (close != null) close.SetParent(window, false);
			Transform oldTitle = window.Find("TitleBar");
			if (oldTitle != null) Object.DestroyImmediate(oldTitle.gameObject);

			// 왼쪽 함수 목록 (전체 높이)
			Transform functionPanel = Panel(window, "FunctionPanel", "함수", 22, 11);
			LeftStretch(Rect(functionPanel), 0, 136, 0, 0);
			RectTransform functionList = Child(functionPanel, "List");
			Stretch(functionList, 4, 4, 26, 4);
			VerticalList(functionList.gameObject, 1);

			// 위쪽 줄: 함수 이름, 저장 상태, 컴파일, 저장
			RectTransform topBar = (RectTransform)window.Find("TopBar");
			TopStretch(topBar, 136, 0, 0, 22);
			SetImage(topBar.gameObject, "ui_titlebar", Image.Type.Sliced);
			HorizontalLayoutGroup topRow = GetOrAdd<HorizontalLayoutGroup>(topBar.gameObject);
			topRow.padding = new RectOffset(8, 24, 3, 3);
			topRow.spacing = 6;
			topRow.childAlignment = TextAnchor.MiddleLeft;
			topRow.childControlWidth = topRow.childControlHeight = true;
			topRow.childForceExpandWidth = topRow.childForceExpandHeight = false;

			Transform oldStatus = window.Find("StatusBar");
			Transform oldMessage = oldStatus != null ? oldStatus.Find("MessageText") : null;
			if (oldMessage != null) oldMessage.SetParent(topBar, false);
			TMP_Text message = GetOrText(topBar, "MessageText", "", 9, Muted, TextAlignmentOptions.MidlineLeft);
			LayoutElement messageSize = GetOrAdd<LayoutElement>(message.gameObject);
			messageSize.preferredWidth = 1;
			messageSize.flexibleWidth = 1;

			if (close != null)
			{
				SetImage(close.gameObject, "ui_close", Image.Type.Simple);
				Place(Rect(close), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(14, 14));
			}

			// 오른쪽 블록 목록
			RectTransform palette = (RectTransform)window.Find("Palette");
			RightStretch(palette, 14, 110, 22, 0);
			SetImage(palette.gameObject, "ui_panel", Image.Type.Sliced);
			Transform content = palette.Find("Content");
			if (content != null)
			{
				VerticalLayoutGroup list = GetOrAdd<VerticalLayoutGroup>(content.gameObject);
				list.padding = new RectOffset(4, 4, 4, 4);
				list.spacing = 2;
				list.childControlWidth = list.childControlHeight = true;
				list.childForceExpandWidth = true;
				list.childForceExpandHeight = false;
			}

			// 오류 창 (블록 목록과 같은 자리. 오른쪽 세로 막대로 바꾼다)
			Transform errorPanel = Panel(window, "ErrorView", "오류", 16, 9);
			RightStretch(Rect(errorPanel), 14, 110, 22, 0);
			TMP_Text errorText = GetOrText(errorPanel, "ErrorText", "", 9, Muted, TextAlignmentOptions.TopLeft);
			errorText.textWrappingMode = TextWrappingModes.Normal;
			errorText.overflowMode = TextOverflowModes.Overflow;
			Stretch(errorText.rectTransform, 6, 6, 22, 6);
			RectTransform errorList = Child(errorPanel, "ErrorList");
			Stretch(errorList, 4, 4, 20, 4);
			VerticalList(errorList.gameObject, 4);

			// 오른쪽 끝 세로 막대: 블록 / 오류
			RectTransform strip = Child(window, "TabStrip");
			RightStretch(strip, 0, 14, 22, 0);
			SetImage(strip.gameObject, "ui_titlebar", Image.Type.Sliced).raycastTarget = false;
			Button blockTab = Tab(strip, "BlockTab", "블\n록", 4, 40);
			Button errorTab = Tab(strip, "ErrorTab", "오\n류", 48, 54);
			// 오류 수 빨간 동그라미 (오류 탭 아래쪽)
			RectTransform tabBadge = Child(errorTab.transform, "Badge");
			Place(tabBadge, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 3), new Vector2(10, 10));
			SetImage(tabBadge.gameObject, "ui_badge", Image.Type.Simple).color = Hex("E05060");
			tabBadge.GetComponent<Image>().raycastTarget = false;
			TMP_Text tabBadgeText = GetOrText(tabBadge, "Count", "1", 8, Color.white, TextAlignmentOptions.Center);
			tabBadgeText.overflowMode = TextOverflowModes.Overflow;
			Stretch(tabBadgeText.rectTransform, 0, 0, 0, 1);

			// 컴파일 성공 알림: 편집 영역 위 가운데 초록 알약
			RectTransform okBadge = Child(window, "CompileOk");
			Place(okBadge, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(6, -28), new Vector2(84, 16));
			Image okImage = GetOrAdd<Image>(okBadge.gameObject);
			okImage.sprite = LoadShape("blk_value");
			okImage.type = Image.Type.Sliced;
			okImage.color = Hex("7ED6A0");
			okImage.raycastTarget = false;
			TMP_Text okText = GetOrText(okBadge, "Text", "", 10, Hex("0F3A22"), TextAlignmentOptions.Center);
			okText.text = "컴파일 성공";
			okText.overflowMode = TextOverflowModes.Overflow;
			Stretch(okText.rectTransform, 0, 0, 0, 1);

			// 휴지통: 놓인 블록을 끄는 동안 블록 목록 위에 보인다
			RectTransform trash = Child(window, "TrashZone");
			RightStretch(trash, 0, 124, 22, 0);
			Image trashImage = GetOrAdd<Image>(trash.gameObject);
			trashImage.sprite = LoadSprite("ui_panel");
			trashImage.type = Image.Type.Sliced;
			trashImage.color = new Color(0.69f, 0.31f, 0.37f, 0.92f);
			TMP_Text trashText = GetOrText(trash, "Text", "", 11, Color.white, TextAlignmentOptions.Center);
			trashText.text = "여기에 놓으면\n블록을 지워요";
			trashText.textWrappingMode = TextWrappingModes.Normal;
			Stretch(trashText.rectTransform, 4, 4, 4, 4);

			// 가운데 편집 영역 (블록은 자기 너비만큼)
			RectTransform workspace = (RectTransform)window.Find("Workspace");
			Stretch(workspace, 136, 124, 22, 16);
			SetImage(workspace.gameObject, "ui_grid", Image.Type.Tiled);
			VerticalLayoutGroup blocks = GetOrAdd<VerticalLayoutGroup>(workspace.gameObject);
			blocks.padding = new RectOffset(12, 12, 12, 12);
			blocks.spacing = 2;
			blocks.childAlignment = TextAnchor.UpperLeft;
			blocks.childForceExpandWidth = false;

			// 아래 줄: 블록 수·코드 크기
			RectTransform statusBar = Child(window, "StatusBar");
			BottomStretch(statusBar, 136, 124, 0, 16);
			SetImage(statusBar.gameObject, "ui_bar", Image.Type.Sliced);
			TMP_Text info = GetOrText(statusBar, "InfoText", "", 9, Muted, TextAlignmentOptions.MidlineRight);
			Stretch(info.rectTransform, 4, 6, 0, 0);
			TMP_Text hint = GetOrText(statusBar, "HintText", "", 9, Hex("5A6290"), TextAlignmentOptions.MidlineLeft);
			hint.text = "블록 지우기: 오른쪽 블록 목록으로 끌어다 놓기";
			Stretch(hint.rectTransform, 6, 90, 0, 0);

			okBadge.SetAsLastSibling();
			trash.SetAsLastSibling();
			if (close != null) close.SetAsLastSibling();
			Transform dragLayer = window.Find("DragLayer");
			if (dragLayer != null) dragLayer.SetAsLastSibling(); // 끄는 블록이 가장 위에 그려지게

			BlockEditor editor = root.GetComponent<BlockEditor>();
			Set(editor, "functionItemPrefab", item);
			Set(editor, "headerPrefab", header);
			Set(editor, "functionList", functionList);
			Set(editor, "saveButtonPrefab", save);
			Set(editor, "compileButtonPrefab", compile);
			Set(editor, "paletteGroupPrefab", paletteGroup);
			Set(editor, "infoLabel", info);
			Set(editor, "messageLabel", message);
			Set(editor, "trashZone", trash.gameObject);
			Set(editor, "dropdownPrefab", dropdown);
			Set(editor, "insertMarkerPrefab", marker);
			Set(editor, "paletteView", palette.gameObject);
			Set(editor, "errorView", errorPanel.gameObject);
			Set(editor, "errorText", errorText);
			Set(editor, "blockTabButton", blockTab);
			Set(editor, "errorTabButton", errorTab);
			Set(editor, "errorTabLabel", errorTab.transform.Find("Text").GetComponent<TMP_Text>());
			Set(editor, "errorTabBadge", tabBadge.gameObject);
			Set(editor, "errorTabBadgeText", tabBadgeText);
			Set(editor, "errorItemPrefab", errorItem);
			Set(editor, "errorList", errorList);
			Set(editor, "errorFramePrefab", errorFrame);
			Set(editor, "compileOkBadge", okBadge.gameObject);

			PrefabUtility.SaveAsPrefabAsset(root, EditorCanvasPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// 장착 화면 ------------------------------------------------------------------

	static void BuildEquipCanvas(ListItemView item, SlotCardView card)
	{
		GameObject root = PrefabUtility.LoadPrefabContents(EquipCanvasPath);
		try
		{
			Transform window = root.transform.Find("Window");
			SetImage(window.gameObject, "ui_window", Image.Type.Sliced);
			RectTransform windowRect = Rect(window);
			Place(windowRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 320));

			BuildTitleBar(window, "스킬 장착");

			// 슬롯 카드 줄
			Transform slotPanel = Panel(window, "SlotPanel", "슬롯", 16, 9);
			TopStretch(Rect(slotPanel), 6, 6, -26, 66);
			Transform slotHeader = slotPanel.Find("Header");
			TMP_Text hint = GetOrText(slotHeader, "HintText", "", 9, Muted, TextAlignmentOptions.MidlineRight);
			Stretch(hint.rectTransform, 40, 4, 0, 0);
			RectTransform slotRow = Reparent(window, slotPanel, "SlotRow", "SlotRow");
			Stretch(slotRow, 4, 4, 20, 4);
			HorizontalLayoutGroup slotLayout = GetOrAdd<HorizontalLayoutGroup>(slotRow.gameObject);
			slotLayout.spacing = 4;
			slotLayout.childControlWidth = slotLayout.childControlHeight = true;
			slotLayout.childForceExpandWidth = slotLayout.childForceExpandHeight = true;

			// 왼쪽 내 함수
			Transform functionPanel = Panel(window, "FunctionPanel", "내 함수", 16, 9);
			LeftStretch(Rect(functionPanel), 6, 150, 98, 30);
			RectTransform functionList = Child(functionPanel, "List");
			Stretch(functionList, 4, 4, 20, 4);
			VerticalList(functionList.gameObject, 2);

			// 오른쪽 C 스킬
			Transform skillPanel = Panel(window, "SkillPanel", "C 스킬", 16, 9);
			Stretch(Rect(skillPanel), 162, 6, 98, 30);
			RectTransform skillList = Reparent(window, skillPanel, "List", "SkillList");
			Stretch(skillList, 4, 4, 20, 4);
			GridLayoutGroup grid = GetOrAdd<GridLayoutGroup>(skillList.gameObject);
			grid.cellSize = new Vector2(136, 16);
			grid.spacing = new Vector2(3, 3);
			grid.childAlignment = TextAnchor.UpperLeft;
			grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
			grid.constraintCount = 3;

			// 아래 줄
			RectTransform bottomBar = Child(window, "BottomBar");
			BottomStretch(bottomBar, 6, 6, 6, 20);
			SetImage(bottomBar.gameObject, "ui_bar", Image.Type.Sliced);
			RectTransform infoRect = Reparent(window, bottomBar, "InfoText", "InfoText");
			Stretch(infoRect, 6, 70, 0, 0);
			TMP_Text info = infoRect.GetComponent<TMP_Text>();
			StyleText(info, 10, Color.white, TextAlignmentOptions.MidlineLeft);
			info.richText = true;
			Transform clearTransform = bottomBar.Find("ClearButton");
			Button clear = clearTransform != null ? clearTransform.GetComponent<Button>() : NewButton(bottomBar, "ClearButton", "ui_button_red", "칸 비우기", 10, Color.white);
			Place(Rect(clear.gameObject), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-3, 0), new Vector2(60, 16));

			SlotEquipPanel panel = root.GetComponent<SlotEquipPanel>();
			Set(panel, "slotCardPrefab", card);
			Set(panel, "itemPrefab", item);
			Set(panel, "slotRow", slotRow);
			Set(panel, "functionList", functionList);
			Set(panel, "skillList", skillList);
			Set(panel, "infoText", info);
			Set(panel, "clearButton", clear);
			Set(panel, "hintText", hint);

			PrefabUtility.SaveAsPrefabAsset(root, EquipCanvasPath);
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// 같이 쓰는 부품 --------------------------------------------------------------

	// 제목줄을 만들고 닫기 버튼을 그 안 오른쪽으로 옮긴다
	static void BuildTitleBar(Transform window, string title)
	{
		RectTransform bar = Child(window, "TitleBar");
		TopStretch(bar, 0, 0, 0, 20);
		SetImage(bar.gameObject, "ui_titlebar", Image.Type.Sliced).raycastTarget = false;
		TMP_Text text = GetOrText(bar, "Title", title, 11, Color.white, TextAlignmentOptions.MidlineLeft);
		text.text = title;
		Stretch(text.rectTransform, 8, 24, 0, 2);

		Transform close = window.Find("CloseButton") ?? bar.Find("CloseButton");
		if (close == null) return;
		close.SetParent(bar, false);
		for (int i = close.childCount - 1; i >= 0; i--) Object.DestroyImmediate(close.GetChild(i).gameObject);
		SetImage(close.gameObject, "ui_close", Image.Type.Simple);
		Place(Rect(close), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 1), new Vector2(14, 14));
		bar.SetAsLastSibling();
	}

	// 세로 막대의 탭 버튼 하나 (위에서 y만큼 아래)
	static Button Tab(Transform strip, string name, string label, float y, float height)
	{
		Transform found = strip.Find(name);
		Button tab = found != null ? found.GetComponent<Button>() : NewButton(strip, name, "ui_button", label, 9, Color.white);
		Place(Rect(tab.gameObject), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -y), new Vector2(12, height));
		TMP_Text text = tab.transform.Find("Text").GetComponent<TMP_Text>();
		Stretch(text.rectTransform, 0, 0, 2, 14); // 아래 14는 오류 수 동그라미 자리
		text.text = label;
		text.textWrappingMode = TextWrappingModes.Normal;
		text.lineSpacing = -10;
		return tab;
	}

	// 패널(바탕 + 머리글)을 만든다
	static Transform Panel(Transform parent, string name, string title, float headerHeight, float fontSize)
	{
		RectTransform panel = Child(parent, name);
		SetImage(panel.gameObject, "ui_panel", Image.Type.Sliced);
		RectTransform header = Child(panel, "Header");
		TopStretch(header, 0, 0, 0, headerHeight);
		SetImage(header.gameObject, "ui_panel_header", Image.Type.Sliced).raycastTarget = false;
		TMP_Text text = GetOrText(header, "Title", title, fontSize, Gold, TextAlignmentOptions.MidlineLeft);
		text.text = title;
		Stretch(text.rectTransform, 5, 4, 0, 1);
		return panel;
	}

	static void VerticalList(GameObject go, float spacing)
	{
		VerticalLayoutGroup list = GetOrAdd<VerticalLayoutGroup>(go);
		list.spacing = spacing;
		list.childAlignment = TextAnchor.UpperLeft;
		list.childControlWidth = list.childControlHeight = true;
		list.childForceExpandWidth = true;
		list.childForceExpandHeight = false;
	}

	// from 아래의 name을 to 아래로 옮긴다 (이미 옮겼으면 to에서 찾는다)
	static RectTransform Reparent(Transform from, Transform to, string name, string newName)
	{
		Transform t = from.Find(name) ?? to.Find(newName) ?? to.Find(name);
		if (t == null) t = NewUI(newName, to).transform;
		t.SetParent(to, false);
		t.name = newName;
		return (RectTransform)t;
	}

	static RectTransform Child(Transform parent, string name)
	{
		Transform t = parent.Find(name);
		return t != null ? (RectTransform)t : Rect(NewUI(name, parent));
	}

	static GameObject NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5; // UI
		if (parent != null) go.transform.SetParent(parent, false);
		return go;
	}

	static Button NewButton(Transform parent, string name, string sprite, string label, float size, Color color)
	{
		GameObject go = NewUI(name, parent);
		Image image = SetImage(go, sprite, Image.Type.Sliced);
		Button button = go.AddComponent<Button>();
		button.targetGraphic = image;
		TMP_Text text = Text(go.transform, "Text", label, size, color, TextAlignmentOptions.Center);
		text.overflowMode = TextOverflowModes.Overflow; // 작은 버튼(+)에서 글자가 잘려 사라지지 않게
		Stretch(text.rectTransform, 0, 0, 0, 1);
		return button;
	}

	static TMP_Text GetOrText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align)
	{
		Transform t = parent.Find(name);
		if (t != null && t.TryGetComponent(out TMP_Text found))
		{
			StyleText(found, size, color, align);
			return found;
		}
		return Text(parent, name, value, size, color, align);
	}

	static TMP_Text Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions align)
	{
		GameObject go = NewUI(name, parent);
		TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
		text.text = value;
		StyleText(text, size, color, align);
		return text;
	}

	static void StyleText(TMP_Text text, float size, Color color, TextAlignmentOptions align)
	{
		if (font != null) text.font = font;
		text.fontSize = size;
		text.color = color;
		text.alignment = align;
		text.raycastTarget = false;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.overflowMode = TextOverflowModes.Ellipsis;
	}

	static Image SetImage(GameObject go, string sprite, Image.Type type)
	{
		Image image = GetOrAdd<Image>(go);
		image.sprite = LoadSprite(sprite);
		image.type = type;
		image.color = Color.white;
		return image;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrameDir + name + ".png");
		if (sprite == null) Debug.LogWarning($"[InvokeUIBuilder] {FrameDir}{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static GameObject SavePrefab(GameObject root, string name)
	{
		GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, $"{CommonDir}/{name}.prefab");
		Object.DestroyImmediate(root);
		return saved;
	}

	static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();
	static RectTransform Rect(GameObject go) => go.GetComponent<RectTransform>();
	static RectTransform Rect(Component c) => c.GetComponent<RectTransform>();

	static void Set(Object target, string field, Object value)
	{
		var so = new SerializedObject(target);
		SerializedProperty property = so.FindProperty(field);
		if (property == null)
		{
			Debug.LogError($"[InvokeUIBuilder] {target.GetType().Name}에 {field} 칸이 없음");
			return;
		}
		property.objectReferenceValue = value;
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;

	// 배치 (값은 캔버스 640x360 기준 픽셀) -------------------------------------------

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static void Stretch(RectTransform r, float left, float right, float top, float bottom)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.pivot = new Vector2(0.5f, 0.5f);
		r.offsetMin = new Vector2(left, bottom);
		r.offsetMax = new Vector2(-right, -top);
	}

	static void TopStretch(RectTransform r, float left, float right, float y, float height)
	{
		r.anchorMin = new Vector2(0, 1);
		r.anchorMax = new Vector2(1, 1);
		r.pivot = new Vector2(0.5f, 1);
		r.sizeDelta = new Vector2(-(left + right), height);
		r.anchoredPosition = new Vector2((left - right) / 2f, y);
	}

	static void BottomStretch(RectTransform r, float left, float right, float y, float height)
	{
		r.anchorMin = new Vector2(0, 0);
		r.anchorMax = new Vector2(1, 0);
		r.pivot = new Vector2(0.5f, 0);
		r.sizeDelta = new Vector2(-(left + right), height);
		r.anchoredPosition = new Vector2((left - right) / 2f, y);
	}

	static void LeftStretch(RectTransform r, float x, float width, float top, float bottom)
	{
		r.anchorMin = new Vector2(0, 0);
		r.anchorMax = new Vector2(0, 1);
		r.pivot = new Vector2(0, 0.5f);
		r.offsetMin = new Vector2(x, bottom);
		r.offsetMax = new Vector2(x + width, -top);
	}

	static void RightStretch(RectTransform r, float margin, float width, float top, float bottom)
	{
		r.anchorMin = new Vector2(1, 0);
		r.anchorMax = new Vector2(1, 1);
		r.pivot = new Vector2(1, 0.5f);
		r.offsetMin = new Vector2(-margin - width, bottom);
		r.offsetMax = new Vector2(-margin, -top);
	}
}
