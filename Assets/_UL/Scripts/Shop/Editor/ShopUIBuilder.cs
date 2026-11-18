using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 로비 상점의 창과 자리를 만드는 에디터 도구 (#534). 게임에는 들어가지 않는다. 여러 번 눌러도 된다.
/// <para>
/// 1. 메뉴 Invoke → UI → 상점 창 만들기: LobbyUI 프리팹 안에 ShopPanel(카드 8장, 구매 확인 창)을 만든다. 시안 18~18-2(1280×720)를 1920×1080 캔버스에 맞춰 1.5배로 그린다.<br/>
///    다시 누르면 ShopPanel 오브젝트와 컴포넌트는 두고 안쪽만 다시 만든다 (씬의 상점 자리가 가리키는 연결이 끊기지 않게).<br/>
/// 2. 메뉴 Invoke → 맵 → 로비 상점 자리 놓기: Lobby 씬에 상점 자리(ShopStation, PanelStation)를 만들고 ShopPanel을 연결한 뒤, 「[↑] 상점」 안내판을 붙이고 씬을 저장한다.<br/>
///    이미 있으면 자리는 옮기지 않고 연결과 안내판만 고친다. 처음 자리는 레이드 문 오른쪽이므로 Scene 창에서 옮긴다.
/// </para>
/// </summary>
public static class ShopUIBuilder
{
	const string PrefabPath = "Assets/_HW/Lobby/LobbyUI.prefab";
	const string LobbyScenePath = "Assets/Scenes/Lobby.unity";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string PanelSprite = "Assets/_HW/UI/Account/acc_panel.png";
	const string OrangeSprite = "Assets/_HW/UI/Account/acc_button.png";
	const string OutlineSprite = "Assets/_HW/UI/Settings/set_button.png";
	const string OutlineOnSprite = "Assets/_HW/UI/Settings/set_button_selected.png";
	const string WhiteSprite = "Assets/_HW/UI/Settings/set_white.png";
	const string RoundSprite = "Assets/_HW/UI/BlockCoding/Shapes/blk_round.png";
	const string MaterialSheet = "Assets/_JW/Effects/Materials/material_{0}.png";

	// 시안 18의 색
	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Orange = Hex("F2A65A");
	static readonly Color Line = Hex("4A3D5C");
	static readonly Color CardFill = Hex("160F24");
	static readonly Color WindowFill = Hex("210D2E");
	static readonly Color Muted = Hex("B8B3C7");
	static readonly Color Faint = Hex("8A84A0");
	static readonly Color Dark = Hex("0F1220");

	// 시안(1280×720) → LobbyUI 캔버스(1920×1080)
	const float S = 1.5f;
	const float WindowW = 1120 * S, WindowH = 640 * S;
	// 아래 줄은 시안(64)보다 높게: 새로고침 버튼이 창의 굵은 테두리에 붙어 보이지 않게 위아래로 띄운다
	const float HeaderH = 64 * S, FooterH = 80 * S, Pad = 24 * S;
	const float CardW = 252 * S, CardGap = 16 * S, CardH = 170 * S;

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/상점 창 만들기")]
	public static void BuildPanel()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		try
		{
			// ShopPanel 오브젝트는 남기고 안쪽만 다시 만든다
			Transform panelTransform = root.transform.Find("ShopPanel");
			RectTransform panel = panelTransform != null ? (RectTransform)panelTransform : NewUI("ShopPanel", root.transform);
			Stretch(panel);
			for (int i = panel.childCount - 1; i >= 0; i--) Object.DestroyImmediate(panel.GetChild(i).gameObject);
			ShopPanel shop = GetOrAdd<ShopPanel>(panel.gameObject);

			RectTransform panelRoot = NewUI("Root", panel);
			Stretch(panelRoot);
			// 창 뒤 게임 화면을 어둡게 하고, 창 밖 클릭이 HUD에 닿지 않게 막는다
			RectTransform dim = NewUI("Dim", panelRoot);
			Stretch(dim);
			Solid(dim, new Color(8 / 255f, 8 / 255f, 16 / 255f, 0.6f), true);

			RectTransform window = NewUI("Window", panelRoot);
			Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WindowW, WindowH));
			Image windowImage = SetSprite(window.gameObject, PanelSprite);
			windowImage.pixelsPerUnitMultiplier = 0.4f;

			// 위 줄: 「> 상점」, 재료 두 가지, 닫기
			RectTransform header = NewUI("Header", window);
			TopStretch(header, 0, 0, 0, HeaderH);
			TMP_Text title = Text(header, "Title", "> 상점", 33, Teal, TextAlignmentOptions.MidlineLeft, true);
			Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(Pad, 0), new Vector2(400, HeaderH));

			Button close = OutlineButton(header, "CloseButton", "닫기");
			Place((RectTransform)close.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16 * S, 0), new Vector2(150, 63));

			RectTransform money = NewUI("Materials", header);
			Place(money, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-16 * S - 150 - 30, 0), new Vector2(600, HeaderH));
			var row = money.gameObject.AddComponent<HorizontalLayoutGroup>();
			row.childAlignment = TextAnchor.MiddleRight;
			row.spacing = 12;
			row.childControlWidth = row.childControlHeight = true;
			row.childForceExpandWidth = row.childForceExpandHeight = false;
			Sprite bugIcon = MaterialIconSprite(ShopRules.BugFragment);
			Sprite coreIcon = MaterialIconSprite(ShopRules.BossCore);
			Icon(money, "BugFragmentIcon", bugIcon, 27);
			TMP_Text bugText = LayoutText(money, "BugFragment", "버그 조각 0", 24);
			Spacer(money, 18);
			Icon(money, "BossCoreIcon", coreIcon, 27);
			TMP_Text coreText = LayoutText(money, "BossCore", "보스 코어 0", 24);

			Solid(TopLine(window, "HeaderLine", HeaderH), Line, false);

			// 가운데: 문법 블록 4장, 함수 4장
			var cards = new List<ShopCardView>();
			float y = HeaderH + 14 * S;
			TMP_Text syntaxLabel = Text(window, "SyntaxLabel", "문법 블록", 24, Teal, TextAlignmentOptions.MidlineLeft, true);
			TopStretch(syntaxLabel.rectTransform, Pad, Pad, y, 33);
			y += 33 + 8 * S;
			for (int i = 0; i < ShopRules.SyntaxSlotCount; i++) cards.Add(Card(window, $"Card{i}", i, y));
			y += CardH + 10 * S;
			TMP_Text functionLabel = Text(window, "FunctionLabel", "함수 (스킬 포함)", 24, Teal, TextAlignmentOptions.MidlineLeft, true);
			TopStretch(functionLabel.rectTransform, Pad, Pad, y, 33);
			y += 33 + 8 * S;
			for (int i = 0; i < ShopRules.FunctionSlotCount; i++) cards.Add(Card(window, $"Card{ShopRules.SyntaxSlotCount + i}", i, y));

			// 아래 줄: 안내, 새로고침
			RectTransform footerLine = NewUI("FooterLine", window);
			footerLine.anchorMin = new Vector2(0, 0);
			footerLine.anchorMax = new Vector2(1, 0);
			footerLine.pivot = new Vector2(0.5f, 0);
			footerLine.offsetMin = new Vector2(0, FooterH);
			footerLine.offsetMax = new Vector2(0, FooterH + 3);
			Solid(footerLine, Line, false);

			TMP_Text hint = Text(window, "Hint", "구매한 자리에는 다른 블록이나 함수가 새로 나타납니다.", 21, Muted, TextAlignmentOptions.MidlineLeft, false);
			hint.textWrappingMode = TextWrappingModes.Normal;
			Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(Pad, 0), new Vector2(WindowW - Pad * 2 - 420, FooterH));

			RectTransform refresh = NewUI("RefreshButton", window);
			Place(refresh, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-Pad, (FooterH - 66) / 2), new Vector2(390, 66));
			Image refreshImage = SetSprite(refresh.gameObject, OrangeSprite);
			refreshImage.pixelsPerUnitMultiplier = 0.4f;
			Button refreshButton = refresh.gameObject.AddComponent<Button>();
			refreshButton.targetGraphic = refreshImage;
			// 버그 조각이 모자라 비활성화되면 주황 버튼을 어둡고 흐리게
			ColorBlock refreshColors = refreshButton.colors;
			refreshColors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
			refreshButton.colors = refreshColors;
			TMP_Text refreshLabel = Text(refresh, "Label", "새로고침", 27, Dark, TextAlignmentOptions.MidlineLeft, true);
			Place(refreshLabel.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(27, 0), new Vector2(150, 66));
			TMP_Text refreshCost = Text(refresh, "Cost", "버그 조각 ×10", 21, Dark, TextAlignmentOptions.MidlineRight, false);
			Place(refreshCost.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-27, 0), new Vector2(200, 66));

			// 알림 칩: 위 줄 바로 아래 가운데. 「상처 가르기 함수를 구매했습니다.」 글자 길이에 맞춰 넓어진다
			RectTransform chip = NewUI("Chip", window);
			chip.anchorMin = chip.anchorMax = new Vector2(0.5f, 1);
			chip.pivot = new Vector2(0.5f, 1);
			chip.anchoredPosition = new Vector2(0, -(HeaderH + 9));
			// 둥근 바탕: 블록 편집 화면의 둥근 모양(흰 9×9, 모서리 4픽셀)을 늘려 알약처럼
			Image chipImage = SetSprite(chip.gameObject, RoundSprite);
			chipImage.color = Teal;
			chipImage.pixelsPerUnitMultiplier = 0.18f;
			chipImage.raycastTarget = false;
			var chipGroup = chip.gameObject.AddComponent<CanvasGroup>();
			chipGroup.alpha = 0f;
			chipGroup.blocksRaycasts = chipGroup.interactable = false;
			var chipRow = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
			chipRow.padding = new RectOffset(30, 30, 9, 9);
			chipRow.childAlignment = TextAnchor.MiddleCenter;
			chipRow.childControlWidth = chipRow.childControlHeight = true;
			chipRow.childForceExpandWidth = chipRow.childForceExpandHeight = false;
			var chipFit = chip.gameObject.AddComponent<ContentSizeFitter>();
			chipFit.horizontalFit = chipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			TMP_Text chipText = LayoutText(chip, "Text", "If 블록을 구매했습니다.", 24);
			chipText.color = Dark;
			chipText.fontStyle = FontStyles.Bold;
			chip.gameObject.SetActive(false);

			ShopConfirmDialog confirm = Confirm(panelRoot);

			// 칸 연결
			var so = new SerializedObject(shop);
			so.FindProperty("panelRoot").objectReferenceValue = panelRoot.gameObject;
			SerializedProperty cardList = so.FindProperty("cards");
			cardList.arraySize = cards.Count;
			for (int i = 0; i < cards.Count; i++) cardList.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
			so.FindProperty("bugFragmentText").objectReferenceValue = bugText;
			so.FindProperty("bossCoreText").objectReferenceValue = coreText;
			so.FindProperty("closeButton").objectReferenceValue = close;
			so.FindProperty("refreshButton").objectReferenceValue = refreshButton;
			so.FindProperty("refreshCostText").objectReferenceValue = refreshCost;
			so.FindProperty("hintText").objectReferenceValue = hint;
			so.FindProperty("confirm").objectReferenceValue = confirm;
			so.FindProperty("chip").objectReferenceValue = chipGroup;
			so.FindProperty("chipText").objectReferenceValue = chipText;
			SerializedProperty icons = so.FindProperty("materialIcons");
			icons.arraySize = 2;
			SetIcon(icons.GetArrayElementAtIndex(0), ShopRules.BugFragment, bugIcon);
			SetIcon(icons.GetArrayElementAtIndex(1), ShopRules.BossCore, coreIcon);
			so.ApplyModifiedPropertiesWithoutUndo();

			panelRoot.gameObject.SetActive(false);
			PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			Debug.Log("[ShopUIBuilder] LobbyUI 프리팹에 상점 창(ShopPanel)을 만듦. 다음: 메뉴 Invoke → 맵 → 로비 상점 자리 놓기");
			KeyBadgeInstaller.InstallLater(); // 닫기·예·아니오 버튼의 키 표시 (#430)
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	[MenuItem("Invoke/맵/로비 상점 자리 놓기")]
	public static void PlaceStation()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		string original = SceneManager.GetActiveScene().path;
		Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

		ShopPanel shop = Object.FindAnyObjectByType<ShopPanel>(FindObjectsInactive.Include);
		if (shop == null)
		{
			Debug.LogError("[ShopUIBuilder] Lobby 씬에서 ShopPanel을 찾지 못함. 먼저 메뉴 Invoke → UI → 상점 창 만들기를 누르십시오 (LobbyUI 프리팹이 Lobby 씬에 있어야 함)");
			return;
		}

		LobbyEntrance[] doors = Object.FindObjectsByType<LobbyEntrance>(FindObjectsInactive.Include);
		GameObject station = GameObject.Find("ShopStation");
		if (station == null)
		{
			station = new GameObject("ShopStation");
			SceneManager.MoveGameObjectToScene(station, scene);
			// 처음 자리: 가장 오른쪽 문에서 6칸 오른쪽, 문과 같은 바닥 높이. 실제 자리(캔버스 17의 상점)는 Scene 창에서 맞춘다
			Vector3 position = new Vector3(8f, -4f, 0f);
			foreach (LobbyEntrance door in doors)
				if (door.transform.position.x + 6f > position.x) position = new Vector3(door.transform.position.x + 6f, door.transform.position.y, door.transform.position.z);
			station.transform.position = position;
		}

		// 범위: 문과 같은 1.5×2.75칸 (DoorSignBuilder의 문 범위)
		BoxCollider2D area = GetOrAdd<BoxCollider2D>(station);
		area.isTrigger = true;
		area.size = new Vector2(1.5f, 2.75f);
		area.offset = new Vector2(0f, 1.375f);

		// 상점 그림 자리: 그림은 아직 없다 (임혜원에게 부탁). 마름모 표시가 하늘 뒤에 가리지 않게 문과 같은 그리기 층을 쓴다
		SpriteRenderer art = GetOrAdd<SpriteRenderer>(station);
		foreach (LobbyEntrance door in doors)
		{
			if (!door.TryGetComponent(out SpriteRenderer doorArt)) continue;
			art.sortingLayerID = doorArt.sortingLayerID;
			art.sortingOrder = doorArt.sortingOrder;
			break;
		}

		PanelStation panelStation = GetOrAdd<PanelStation>(station);
		var so = new SerializedObject(panelStation);
		so.FindProperty("panel").objectReferenceValue = shop;
		so.ApplyModifiedPropertiesWithoutUndo();

		DoorSignBuilder.MakeSignFor(panelStation, "상점");

		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log($"[ShopUIBuilder] Lobby 씬에 상점 자리(ShopStation)를 놓음: X {station.transform.position.x}, Y {station.transform.position.y}. 자리는 Scene 창에서 옮겨도 됨");
		if (!string.IsNullOrEmpty(original) && original != LobbyScenePath) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
	}

	// 카드 한 장 (시안 18의 카드 252×170을 1.5배). index: 줄 안의 순서
	static ShopCardView Card(RectTransform window, string name, int index, float top)
	{
		RectTransform card = NewUI(name, window);
		Place(card, new Vector2(0, 1), new Vector2(0, 1), new Vector2(Pad + index * (CardW + CardGap), -top), new Vector2(CardW, CardH));
		Image border = Solid(card, Line, true);

		RectTransform fill = NewUI("Fill", card);
		Stretch(fill, 3);
		Image fillImage = Solid(fill, CardFill, true);
		Button button = card.gameObject.AddComponent<Button>();
		button.targetGraphic = fillImage;
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1);
		colors.disabledColor = Color.white;
		button.colors = colors;

		RectTransform content = NewUI("Content", card);
		Stretch(content, 15);

		// 이름·표기 칸: 첫 줄 이름(함수는 한글 이름), 둘째 줄 형식·원형. 긴 원형(bool is_valid_target(Target target))은 두 줄로 나눈다
		const float TileH = 114;
		RectTransform tile = NewUI("Tile", content);
		TopStretch(tile, 0, 0, 0, TileH);
		Image tileImage = Solid(tile, Hex("241B36"), false);
		// 왼쪽 세로 띠: 분류 색 (ShopCardView가 칠함)
		const float StripeW = 6;
		RectTransform stripe = NewUI("Stripe", tile);
		stripe.anchorMin = new Vector2(0, 0);
		stripe.anchorMax = new Vector2(0, 1);
		stripe.pivot = new Vector2(0, 0.5f);
		stripe.offsetMin = Vector2.zero;
		stripe.offsetMax = new Vector2(StripeW, 0);
		Image stripeImage = Solid(stripe, Hex("9E7BFF"), false);
		TMP_Text title = Text(tile, "Title", "If", 27, Hex("F4F1FA"), TextAlignmentOptions.BottomLeft, true);
		title.overflowMode = TextOverflowModes.Ellipsis;
		TopStretch(title.rectTransform, StripeW + 15, 12, 9, 40);
		TMP_Text notation = Text(tile, "Notation", "if (condition) { body }", 18, Muted, TextAlignmentOptions.TopLeft, false);
		notation.textWrappingMode = TextWrappingModes.Normal;
		notation.overflowMode = TextOverflowModes.Ellipsis;
		TopStretch(notation.rectTransform, StripeW + 15, 12, 54, 54);

		// 가격 줄 두 개 (버그 조각, 보스 코어). 쓰지 않는 줄은 ShopCardView가 끈다. 이름 칸과 떨어뜨려 따로 읽히게 한다
		RectTransform prices = NewUI("Prices", content);
		TopStretch(prices, 0, 0, TileH + 24, 60);
		var priceIcons = new Image[2];
		var priceTexts = new TMP_Text[2];
		for (int i = 0; i < 2; i++)
		{
			priceIcons[i] = Icon(prices, $"PriceIcon{i}", null, 27);
			Place((RectTransform)priceIcons[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 30), new Vector2(27, 27));
			priceTexts[i] = Text(prices, $"Price{i}", "버그 조각 ×7", 21, Color.white, TextAlignmentOptions.MidlineLeft, false);
			Place(priceTexts[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -i * 30), new Vector2(CardW - 66, 27));
		}

		TMP_Text owned = Text(content, "Owned", "가진 개수 0", 18, Muted, TextAlignmentOptions.BottomLeft, false);
		owned.richText = true;
		Place(owned.rectTransform, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(CardW - 30, 27));

		TMP_Text empty = Text(card, "Empty", "빈 칸", 21, Faint, TextAlignmentOptions.Center, false);
		Stretch(empty.rectTransform);
		empty.gameObject.SetActive(false);

		ShopCardView view = card.gameObject.AddComponent<ShopCardView>();
		var so = new SerializedObject(view);
		so.FindProperty("button").objectReferenceValue = button;
		so.FindProperty("border").objectReferenceValue = border;
		so.FindProperty("tile").objectReferenceValue = tileImage;
		so.FindProperty("stripe").objectReferenceValue = stripeImage;
		so.FindProperty("title").objectReferenceValue = title;
		so.FindProperty("notation").objectReferenceValue = notation;
		SerializedProperty iconList = so.FindProperty("priceIcons");
		SerializedProperty textList = so.FindProperty("priceTexts");
		iconList.arraySize = textList.arraySize = 2;
		for (int i = 0; i < 2; i++)
		{
			iconList.GetArrayElementAtIndex(i).objectReferenceValue = priceIcons[i];
			textList.GetArrayElementAtIndex(i).objectReferenceValue = priceTexts[i];
		}
		so.FindProperty("owned").objectReferenceValue = owned;
		so.FindProperty("content").objectReferenceValue = content.gameObject;
		so.FindProperty("emptyText").objectReferenceValue = empty;
		so.FindProperty("shakeTarget").objectReferenceValue = card;
		so.ApplyModifiedPropertiesWithoutUndo();
		return view;
	}

	// 구매 확인 창 (시안 18-1의 520폭 창을 1.5배). 예(Enter)는 오른쪽 주황, 아니오(ESC)는 왼쪽 테두리: 게임 전체 규칙 (#430)
	static ShopConfirmDialog Confirm(RectTransform panelRoot)
	{
		RectTransform confirm = NewUI("Confirm", panelRoot);
		Stretch(confirm);
		RectTransform dim = NewUI("Dim", confirm);
		Stretch(dim);
		Solid(dim, new Color(8 / 255f, 8 / 255f, 16 / 255f, 0.6f), true);

		const float W = 520 * S, H = 300 * S;
		RectTransform window = NewUI("Window", confirm);
		Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));
		Solid(window, Orange, true);
		RectTransform inner = NewUI("Fill", window);
		Stretch(inner, 4);
		Solid(inner, WindowFill, true);

		TMP_Text title = Text(window, "Title", "For 블록 1개를 구매하시겠습니까?", 30, Color.white, TextAlignmentOptions.TopLeft, true);
		title.textWrappingMode = TextWrappingModes.Normal;
		TopStretch(title.rectTransform, 42, 42, 42, 84);

		RectTransform box = NewUI("CostBox", window);
		TopStretch(box, 42, 42, 135, 165);
		Solid(box, Line, false);
		RectTransform boxFill = NewUI("Fill", box);
		Stretch(boxFill, 3);
		Solid(boxFill, CardFill, false);
		TMP_Text costLabel = Text(box, "CostLabel", "소비하는 재료", 19, Muted, TextAlignmentOptions.MidlineLeft, false);
		TopStretch(costLabel.rectTransform, 24, 24, 12, 27);
		// 재료 줄 두 개 (버그 조각, 보스 코어): 재료마다 그림 + 「버그 조각 ×12」. 쓰지 않는 줄은 ShopConfirmDialog가 끈다
		var costIcons = new Image[2];
		var costTexts = new TMP_Text[2];
		for (int i = 0; i < 2; i++)
		{
			float top = 45 + i * 36;
			costIcons[i] = Icon(box, $"CostIcon{i}", null, 30);
			Place((RectTransform)costIcons[i].transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -top), new Vector2(30, 30));
			costTexts[i] = Text(box, $"Cost{i}", "버그 조각 ×11", 24, Color.white, TextAlignmentOptions.MidlineLeft, false);
			Place(costTexts[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + 40, -top), new Vector2(W - 84 - 48 - 40, 30));
		}
		TMP_Text owned = Text(box, "Owned", "가진 개수 3 → 4", 21, Muted, TextAlignmentOptions.BottomLeft, false);
		Place(owned.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 12), new Vector2(W - 84 - 48, 30));

		float buttonW = (W - 84 - 18) / 2;
		Button no = OutlineButton(window, "NoButton", "아니오");
		Place((RectTransform)no.transform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(42, 36), new Vector2(buttonW, 72));
		RectTransform yesRect = NewUI("YesButton", window);
		Place(yesRect, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-42, 36), new Vector2(buttonW, 72));
		Image yesImage = SetSprite(yesRect.gameObject, OrangeSprite);
		yesImage.pixelsPerUnitMultiplier = 0.4f;
		Button yes = yesRect.gameObject.AddComponent<Button>();
		yes.targetGraphic = yesImage;
		TMP_Text yesLabel = Text(yesRect, "Label", "예", 27, Dark, TextAlignmentOptions.Center, true);
		Stretch(yesLabel.rectTransform);

		confirm.gameObject.AddComponent<UIShortcut>();
		ShopConfirmDialog dialog = confirm.gameObject.AddComponent<ShopConfirmDialog>();
		var so = new SerializedObject(dialog);
		so.FindProperty("title").objectReferenceValue = title;
		SerializedProperty iconList = so.FindProperty("costIcons");
		SerializedProperty textList = so.FindProperty("costTexts");
		iconList.arraySize = textList.arraySize = 2;
		for (int i = 0; i < 2; i++)
		{
			iconList.GetArrayElementAtIndex(i).objectReferenceValue = costIcons[i];
			textList.GetArrayElementAtIndex(i).objectReferenceValue = costTexts[i];
		}
		so.FindProperty("owned").objectReferenceValue = owned;
		so.FindProperty("yesButton").objectReferenceValue = yes;
		so.FindProperty("noButton").objectReferenceValue = no;
		so.ApplyModifiedPropertiesWithoutUndo();
		confirm.gameObject.SetActive(false);
		return dialog;
	}

	// 도구 ---------------------------------------------------------------------

	static Button OutlineButton(Transform parent, string name, string label)
	{
		RectTransform rect = NewUI(name, parent);
		Image image = SetSprite(rect.gameObject, OutlineSprite);
		image.pixelsPerUnitMultiplier = 0.4f;
		Button button = rect.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		Sprite on = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineOnSprite);
		if (on != null)
		{
			button.transition = Selectable.Transition.SpriteSwap;
			button.spriteState = new SpriteState { highlightedSprite = on, selectedSprite = on, pressedSprite = on };
		}
		TMP_Text text = Text(rect, "Label", label, 24, Color.white, TextAlignmentOptions.Center, false);
		Stretch(text.rectTransform);
		return button;
	}

	static Sprite MaterialIconSprite(string materialId)
	{
		string path = string.Format(MaterialSheet, materialId);
		Sprite first = null;
		foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
			if (asset is Sprite sprite && (first == null || string.CompareOrdinal(sprite.name, first.name) < 0)) first = sprite;
		if (first == null) Debug.LogWarning($"[ShopUIBuilder] {path}에서 그림을 찾지 못함");
		return first;
	}

	static void SetIcon(SerializedProperty element, string materialId, Sprite icon)
	{
		element.FindPropertyRelative("MaterialId").stringValue = materialId;
		element.FindPropertyRelative("Icon").objectReferenceValue = icon;
	}

	static Image Icon(Transform parent, string name, Sprite sprite, float size)
	{
		RectTransform rect = NewUI(name, parent);
		Image image = rect.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.preserveAspect = true;
		image.raycastTarget = false;
		var element = rect.gameObject.AddComponent<LayoutElement>();
		element.preferredWidth = element.preferredHeight = size;
		rect.sizeDelta = new Vector2(size, size);
		return image;
	}

	static void Spacer(Transform parent, float width)
	{
		var element = NewUI("Space", parent).gameObject.AddComponent<LayoutElement>();
		element.preferredWidth = width;
	}

	static TMP_Text LayoutText(Transform parent, string name, string value, float size)
	{
		TMP_Text text = Text(parent, name, value, size, Color.white, TextAlignmentOptions.MidlineLeft, false);
		// 너비는 가로 레이아웃이 글자 길이(preferred)로 정한다
		text.textWrappingMode = TextWrappingModes.NoWrap;
		return text;
	}

	static TMP_Text Text(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment, bool bold)
	{
		TMP_Text text = NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = alignment;
		text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.extraPadding = false;
		text.raycastTarget = false;
		text.richText = true;
		return text;
	}

	// 에디터의 GetComponent는 컴포넌트가 없을 때 진짜 null 대신 가짜 null 객체를 돌려준다.
	// `??`는 이 객체를 null로 보지 않아 AddComponent를 건너뛰므로, TryGetComponent로 찾는다
	static T GetOrAdd<T>(GameObject go) where T : Component
	{
		return go.TryGetComponent(out T component) ? component : go.AddComponent<T>();
	}

	static Image SetSprite(GameObject go, string path)
	{
		Image image = GetOrAdd<Image>(go);
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (image.sprite == null) Debug.LogWarning($"[ShopUIBuilder] {path}를 찾지 못함");
		image.type = Image.Type.Sliced;
		image.color = Color.white;
		return image;
	}

	static Image Solid(RectTransform rect, Color color, bool blocksClicks)
	{
		Image image = GetOrAdd<Image>(rect.gameObject);
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSprite);
		image.type = Image.Type.Simple;
		image.color = color;
		image.raycastTarget = blocksClicks;
		return image;
	}

	static RectTransform TopLine(RectTransform parent, string name, float top)
	{
		RectTransform line = NewUI(name, parent);
		TopStretch(line, 0, 0, top, 3);
		return line;
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
		r.pivot = new Vector2(0.5f, 0.5f);
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

	static void TopStretch(RectTransform r, float left, float right, float top, float height)
	{
		r.anchorMin = new Vector2(0, 1);
		r.anchorMax = new Vector2(1, 1);
		r.pivot = new Vector2(0.5f, 1);
		r.offsetMin = new Vector2(left, -top - height);
		r.offsetMax = new Vector2(-right, -top);
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
