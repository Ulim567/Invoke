using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 방 화면(방 목록, 방 만들기·비밀번호 창, 파티방)을 시안 13 모양으로 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → UI → 레이드 방 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다).<br/>
/// - 동작은 김도훈의 RaidLobbyUI, RaidRoomRowUI, RaidMemberRowUI 그대로다. 이 도구는 모양만 만들고 칸을 연결한다 (raid.md).<br/>
/// - 만드는 것: Assets/_HW/UI/Raid/RaidLobbyCanvas.prefab, 방 줄 RaidRoomRow.prefab, 파티원 줄 RaidMemberRow.prefab<br/>
/// - Lobby 씬을 열어 둔 채 누르면 씬에도 놓는다: 새 화면을 놓고, 레이드 입구(RaidLobbyOpener)의 Raid Lobby 칸을 새 화면으로 바꾸고, 예전 화면(김도훈 RaidLobby)은 끈다. 확인하고 Cmd+S로 저장한다.
/// </para>
/// </summary>
public static class RaidLobbyUIBuilder
{
	const string Dir = "Assets/_HW/UI/Raid";
	const string PrefabPath = Dir + "/RaidLobbyCanvas.prefab";
	const string RoomRowPath = Dir + "/RaidRoomRow.prefab";
	const string MemberRowPath = Dir + "/RaidMemberRow.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	// 시안 13 색 (접속·설정 화면과 같음)
	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Orange = Hex("E8A867");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Dark = Hex("1A1E3A");
	static readonly Color Red = Hex("E07070");
	static readonly Color Line = Hex("4A3A6A");

	const float WindowWidth = 380;

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/레이드 방 화면 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/_HW/UI", "Raid");
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		RaidRoomRowUI roomRow = BuildRoomRow();
		RaidMemberRowUI memberRow = BuildMemberRow();

		var canvasObject = new GameObject("RaidLobbyCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 10; // 편집 화면·레벨업 창과 같은 창 높이. HUD(5)보다 위, 레이드 결과(30)·설정(40)·로딩(100)보다 아래
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 창을 여닫는 판 (InputBlockingPanel의 Panel Root). 뒤를 어둡게 덮고 뒤의 버튼이 눌리지 않게 한다
		RectTransform root = NewUI("PanelRoot", canvasObject.transform);
		Stretch(root);
		Image dim = root.gameObject.AddComponent<Image>();
		dim.color = new Color(0.04f, 0.03f, 0.08f, 0.6f);

		// ── 방 목록 ──
		RectTransform list = Window(root, "RoomListPanel");
		(_, Button listClose) = Header(list, "레이드 방", out _);
		RectTransform scroll = NewUI("RoomScroll", list);
		Height(scroll.gameObject, 132);
		RectTransform content = ScrollArea(scroll);
		TMP_Text empty = Text(scroll, "EmptyText", "아직 방이 없어요. 방을 만들어 보세요!", 9, Muted, TextAlignmentOptions.Center);
		Stretch(empty.rectTransform);
		RectTransform listButtons = Row(list, "Buttons", 26);
		Button fakeRoom = SubButton(listButtons, "FakeRoomButton", "테스트 방", 70).Item1;
		Spacer(listButtons);
		Button create = MainButton(listButtons, "CreateButton", "방 만들기", 120).Item1;

		// ── 파티방 ──
		RectTransform party = Window(root, "PartyPanel");
		(TMP_Text partyTitle, Button partyClose) = Header(party, "[방 이름]", out TMP_Text countText);
		countText.gameObject.AddComponent<RaidPartyCountText>();
		partyClose.gameObject.AddComponent<ClickRelay>();
		Set(new SerializedObject(partyClose.GetComponent<ClickRelay>()), "target", listClose);
		RectTransform members = NewUI("MemberList", party);
		VerticalLayoutGroup memberLayout = members.gameObject.AddComponent<VerticalLayoutGroup>();
		memberLayout.spacing = 4;
		memberLayout.childControlWidth = memberLayout.childControlHeight = true;
		memberLayout.childForceExpandWidth = true;
		memberLayout.childForceExpandHeight = false;
		// 테스트 버튼은 서버에 접속하면 숨겨지므로, 자리를 차지하지 않게 창 바깥 아래에 둔다
		RectTransform tests = Row(party, "TestButtons", 18);
		tests.gameObject.GetComponent<LayoutElement>().ignoreLayout = true;
		Place(tests, new Vector2(0.5f, 0), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(WindowWidth - 24, 18));
		Button fakeMember = SubButton(tests, "FakeMemberButton", "테스트 파티원", 90, 8).Item1;
		Button fakeReady = SubButton(tests, "FakeReadyButton", "테스트 준비", 80, 8).Item1;
		Spacer(tests);
		RectTransform partyButtons = Row(party, "Buttons", 26);
		Button leave = SubButton(partyButtons, "LeaveButton", "나가기", 80).Item1;
		Spacer(partyButtons);
		(Button ready, TMP_Text readyText) = MainButton(partyButtons, "ReadyButton", "준비", 110);
		Button start = MainButton(partyButtons, "StartButton", "시작", 110).Item1;

		// ── 방 만들기 창 ──
		RectTransform createPopup = Popup(root, "CreatePopup", "방 만들기");
		TMP_InputField nameInput = Field(createPopup, "NameInput", "방 이름", "레이드 방", false);
		TMP_InputField passwordInput = Field(createPopup, "PasswordInput", "비밀번호 (비우면 공개 방)", "비밀번호", true);
		RectTransform createButtons = Row(createPopup, "Buttons", 24);
		Button createCancel = SubButton(createButtons, "CancelButton", "취소", 70).Item1;
		Spacer(createButtons);
		Button createConfirm = MainButton(createButtons, "ConfirmButton", "만들기", 100).Item1;

		// ── 비밀번호 창 ──
		RectTransform passwordPopup = Popup(root, "PasswordPopup", "비밀번호 방");
		TMP_InputField joinInput = Field(passwordPopup, "PasswordInput", "비밀번호", "비밀번호", true);
		RectTransform joinButtons = Row(passwordPopup, "Buttons", 24);
		Button joinCancel = SubButton(joinButtons, "CancelButton", "취소", 70).Item1;
		Spacer(joinButtons);
		Button joinConfirm = MainButton(joinButtons, "ConfirmButton", "입장", 100).Item1;

		// 실패 이유 (창 아래)
		TMP_Text message = Text(root, "MessageText", "", 9, Red, TextAlignmentOptions.Center);
		Place(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -136), new Vector2(WindowWidth, 14));

		RaidLobbyUI view = canvasObject.AddComponent<RaidLobbyUI>();
		var so = new SerializedObject(view);
		Set(so, "panelRoot", root.gameObject);
		so.FindProperty("openOnStart").boolValue = false;
		Set(so, "messageText", message);
		Set(so, "roomListPanel", list.gameObject);
		Set(so, "roomListContent", content);
		Set(so, "roomRowPrefab", roomRow);
		Set(so, "emptyText", empty.gameObject);
		Set(so, "createButton", create);
		Set(so, "closeButton", listClose);
		Set(so, "createPopup", createPopup.gameObject);
		Set(so, "createNameInput", nameInput);
		Set(so, "createPasswordInput", passwordInput);
		Set(so, "createConfirmButton", createConfirm);
		Set(so, "createCancelButton", createCancel);
		Set(so, "passwordPopup", passwordPopup.gameObject);
		Set(so, "joinPasswordInput", joinInput);
		Set(so, "joinConfirmButton", joinConfirm);
		Set(so, "joinCancelButton", joinCancel);
		Set(so, "partyPanel", party.gameObject);
		Set(so, "partyTitleText", partyTitle);
		Set(so, "memberListContent", members);
		Set(so, "memberRowPrefab", memberRow);
		Set(so, "readyButton", ready);
		Set(so, "readyButtonText", readyText);
		Set(so, "startButton", start);
		Set(so, "leaveButton", leave);
		Set(so, "addFakeRoomButton", fakeRoom);
		Set(so, "addFakeMemberButton", fakeMember);
		Set(so, "toggleFakeReadyButton", fakeReady);
		so.ApplyModifiedPropertiesWithoutUndo();

		root.gameObject.SetActive(false);
		createPopup.gameObject.SetActive(false);
		passwordPopup.gameObject.SetActive(false);
		party.gameObject.SetActive(false);

		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[RaidLobbyUIBuilder] {PrefabPath}를 만듦");

		PlaceInLobby(prefab);
	}

	// Lobby 씬이 열려 있으면: 새 화면을 놓고, 레이드 입구가 새 화면을 열게 바꾸고, 예전 화면을 끈다
	static void PlaceInLobby(GameObject prefab)
	{
		var scene = EditorSceneManager.GetActiveScene();
		if (scene.name != PlaceScenes.Lobby)
		{
			Debug.Log("[RaidLobbyUIBuilder] Lobby 씬을 열고 다시 누르면 씬에도 놓아요");
			return;
		}

		GameObject placed = null;
		foreach (GameObject go in scene.GetRootGameObjects())
			if (go.name == prefab.name) placed = go;
		if (placed == null)
		{
			placed = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
			Undo.RegisterCreatedObjectUndo(placed, "Place Raid Lobby");
		}
		RaidLobbyUI ours = placed.GetComponent<RaidLobbyUI>();

		int turnedOff = 0;
		foreach (GameObject go in scene.GetRootGameObjects())
		{
			foreach (RaidLobbyUI other in go.GetComponentsInChildren<RaidLobbyUI>(true))
			{
				if (other == ours || !other.gameObject.activeSelf) continue;
				Undo.RecordObject(other.gameObject, "Turn off old Raid Lobby");
				other.gameObject.SetActive(false);
				turnedOff++;
			}
			foreach (RaidLobbyOpener opener in go.GetComponentsInChildren<RaidLobbyOpener>(true))
			{
				var so = new SerializedObject(opener);
				so.FindProperty("raidLobby").objectReferenceValue = ours;
				so.ApplyModifiedProperties();
			}
		}
		EditorSceneManager.MarkSceneDirty(scene);
		Debug.Log($"[RaidLobbyUIBuilder] Lobby 씬에 새 레이드 방 화면을 놓고 레이드 입구에 연결함. 예전 화면 {turnedOff}개를 끔. Cmd+S로 저장하세요");
	}

	// 방 목록의 한 줄: 방 이름, 잠김, 인원, 입장
	static RaidRoomRowUI BuildRoomRow()
	{
		RectTransform row = NewUI("RaidRoomRow", null);
		Height(row.gameObject, 24);
		SetImage(row.gameObject, "Assets/_HW/UI/Account/acc_card.png");
		TMP_Text name = Text(row, "Name", "[방 이름]", 9, Color.white, TextAlignmentOptions.MidlineLeft);
		Place(name.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(200, 16));
		TMP_Text lockText = Text(row, "Lock", "비밀번호", 8, Orange, TextAlignmentOptions.MidlineRight);
		Place(lockText.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-122, 0), new Vector2(50, 14));
		TMP_Text count = Text(row, "Count", "1 / 3", 9, Teal, TextAlignmentOptions.MidlineRight);
		Place(count.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-66, 0), new Vector2(50, 14));
		RectTransform joinRect = NewUI("JoinButton", row);
		Place(joinRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(54, 18));
		Button join = ButtonOn(joinRect, "Assets/_HW/UI/Settings/set_button.png", "Assets/_HW/UI/Settings/set_button_selected.png");
		Stretch(Text(joinRect, "Text", "입장", 9, Color.white, TextAlignmentOptions.Center).rectTransform);

		RaidRoomRowUI ui = row.gameObject.AddComponent<RaidRoomRowUI>();
		var so = new SerializedObject(ui);
		Set(so, "nameText", name);
		Set(so, "countText", count);
		Set(so, "lockIcon", lockText.gameObject);
		Set(so, "joinButton", join);
		so.FindProperty("fullColor").colorValue = Red;
		so.ApplyModifiedPropertiesWithoutUndo();
		return SavePrefab(row.gameObject, RoomRowPath).GetComponent<RaidRoomRowUI>();
	}

	// 파티원 한 줄: 이름(내 줄은 「나」 표시), 상태(파티장 / 준비 완료 / 준비 중)
	static RaidMemberRowUI BuildMemberRow()
	{
		RectTransform row = NewUI("RaidMemberRow", null);
		Height(row.gameObject, 26);
		Image background = SetImage(row.gameObject, "Assets/_HW/UI/Account/acc_card.png");
		// 이름 + 「나」 표시를 가로로 붙여 둔다 (이름 길이만큼만 차지)
		RectTransform nameRow = NewUI("NameRow", row);
		Place(nameRow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(240, 16));
		HorizontalLayoutGroup nameLayout = nameRow.gameObject.AddComponent<HorizontalLayoutGroup>();
		nameLayout.spacing = 6;
		nameLayout.childAlignment = TextAnchor.MiddleLeft;
		nameLayout.childControlWidth = nameLayout.childControlHeight = true;
		nameLayout.childForceExpandWidth = nameLayout.childForceExpandHeight = false;
		TMP_Text name = Text(nameRow, "Name", "[닉네임]", 9, Color.white, TextAlignmentOptions.MidlineLeft);
		RectTransform badge = NewUI("MeBadge", nameRow);
		Width(badge.gameObject, 16);
		Height(badge.gameObject, 11);
		Image badgeImage = badge.gameObject.AddComponent<Image>();
		badgeImage.sprite = LoadSprite("Assets/_HW/UI/Settings/set_white.png");
		badgeImage.color = Teal;
		badgeImage.raycastTarget = false;
		Stretch(Text(badge, "Text", "나", 8, Dark, TextAlignmentOptions.Center).rectTransform);
		badge.gameObject.SetActive(false);
		TMP_Text state = Text(row, "State", "준비 중", 9, Muted, TextAlignmentOptions.MidlineRight);
		Place(state.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(80, 16));
		state.fontStyle = FontStyles.Bold;

		RaidMemberRowUI ui = row.gameObject.AddComponent<RaidMemberRowUI>();
		var so = new SerializedObject(ui);
		Set(so, "nameText", name);
		Set(so, "stateText", state);
		so.FindProperty("readyColor").colorValue = Teal;
		so.FindProperty("waitingColor").colorValue = Muted;
		so.FindProperty("leaderColor").colorValue = Orange;
		so.ApplyModifiedPropertiesWithoutUndo();

		RaidMemberRowStyle style = row.gameObject.AddComponent<RaidMemberRowStyle>();
		var styleSo = new SerializedObject(style);
		Set(styleSo, "nameText", name);
		Set(styleSo, "meBadge", badge.gameObject);
		Set(styleSo, "background", background);
		Set(styleSo, "normalSprite", LoadSprite("Assets/_HW/UI/Account/acc_card.png"));
		Set(styleSo, "mineSprite", LoadSprite("Assets/_HW/UI/Account/acc_card_selected.png"));
		styleSo.ApplyModifiedPropertiesWithoutUndo();
		return SavePrefab(row.gameObject, MemberRowPath).GetComponent<RaidMemberRowUI>();
	}

	// 부품 -------------------------------------------------------------------

	// 보라 창 + 민트 테두리 (시안 13). 내용을 위에서부터 쌓고 높이를 내용에 맞춘다
	static RectTransform Window(RectTransform root, string name)
	{
		RectTransform window = NewUI(name, root);
		Place(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(WindowWidth, 0));
		SetImage(window.gameObject, "Assets/_HW/UI/Account/acc_panel.png");
		VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(12, 12, 8, 12);
		layout.spacing = 8;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		window.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return window;
	}

	// 맨 위 줄: 「> 제목」 + 작은 회색 글자 + 오른쪽 X, 그 아래 구분선
	static (TMP_Text, Button) Header(RectTransform window, string title, out TMP_Text small)
	{
		RectTransform header = NewUI("Header", window);
		Height(header.gameObject, 24);
		TMP_Text arrow = Text(header, "Arrow", ">", 13, Teal, TextAlignmentOptions.MidlineLeft);
		arrow.fontStyle = FontStyles.Bold;
		Place(arrow.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0), new Vector2(12, 20));
		TMP_Text titleText = Text(header, "Title", title, 13, Teal, TextAlignmentOptions.MidlineLeft);
		titleText.fontStyle = FontStyles.Bold;
		titleText.textWrappingMode = TextWrappingModes.NoWrap;
		titleText.overflowMode = TextOverflowModes.Ellipsis;
		// 제목 길이만큼만 차지하고, 회색 글자가 바로 뒤에 붙게
		RectTransform titleRow = NewUI("TitleRow", header);
		Place(titleRow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(300, 20));
		HorizontalLayoutGroup titleLayout = titleRow.gameObject.AddComponent<HorizontalLayoutGroup>();
		titleLayout.spacing = 6;
		titleLayout.childAlignment = TextAnchor.MiddleLeft;
		titleLayout.childControlWidth = titleLayout.childControlHeight = true;
		titleLayout.childForceExpandWidth = titleLayout.childForceExpandHeight = false;
		titleText.transform.SetParent(titleRow, false);
		small = Text(titleRow, "Count", "", 8, Muted, TextAlignmentOptions.MidlineLeft);

		RectTransform close = NewUI("CloseButton", header);
		Place(close, new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(20, 20));
		Button button = ButtonOn(close, "Assets/_HW/UI/Settings/set_button.png", "Assets/_HW/UI/Settings/set_button_selected.png");
		Stretch(Text(close, "Text", "X", 10, Color.white, TextAlignmentOptions.Center).rectTransform);

		RectTransform line = NewUI("Line", window);
		Height(line.gameObject, 1);
		Image lineImage = line.gameObject.AddComponent<Image>();
		lineImage.color = Line;
		lineImage.raycastTarget = false;
		return (titleText, button);
	}

	// 방 목록 스크롤. 돌려주는 값은 방 줄을 넣을 Content
	static RectTransform ScrollArea(RectTransform scroll)
	{
		ScrollRect rect = scroll.gameObject.AddComponent<ScrollRect>();
		rect.horizontal = false;
		rect.movementType = ScrollRect.MovementType.Clamped;
		rect.scrollSensitivity = 20;
		RectTransform viewport = NewUI("Viewport", scroll);
		Stretch(viewport);
		viewport.gameObject.AddComponent<RectMask2D>();
		viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // 빈 곳에서도 휠이 먹게
		RectTransform content = NewUI("Content", viewport);
		content.anchorMin = new Vector2(0, 1);
		content.anchorMax = new Vector2(1, 1);
		content.pivot = new Vector2(0.5f, 1);
		content.offsetMin = content.offsetMax = Vector2.zero;
		VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 4;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		rect.viewport = viewport;
		rect.content = content;
		return content;
	}

	// 가운데 작은 창 (주황 테두리, 접속 끊김 팝업과 같은 그림)
	static RectTransform Popup(RectTransform root, string name, string title)
	{
		RectTransform popup = NewUI(name, root);
		Place(popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(240, 0));
		SetImage(popup.gameObject, "Assets/_HW/UI/Account/acc_popup.png");
		VerticalLayoutGroup layout = popup.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(14, 14, 10, 12);
		layout.spacing = 6;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		popup.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		TMP_Text titleText = Text(popup, "Title", title, 11, Orange, TextAlignmentOptions.MidlineLeft);
		titleText.fontStyle = FontStyles.Bold;
		Height(titleText.gameObject, 16);
		return popup;
	}

	// 이름표 + 입력칸. 글자 겹침이 없게 Extra Padding과 기울임을 쓰지 않는다 (#342)
	static TMP_InputField Field(RectTransform parent, string name, string label, string placeholder, bool password)
	{
		TMP_Text labelText = Text(parent, name + "Label", label, 8, Muted, TextAlignmentOptions.MidlineLeft);
		Height(labelText.gameObject, 11);
		RectTransform box = NewUI(name, parent);
		Height(box.gameObject, 20);
		Image boxImage = SetImage(box.gameObject, "Assets/_HW/UI/Account/acc_field.png");
		TMP_InputField input = box.gameObject.AddComponent<TMP_InputField>();
		input.targetGraphic = boxImage;
		RectTransform area = NewUI("Text Area", box);
		Stretch(area);
		area.offsetMin = new Vector2(6, 2);
		area.offsetMax = new Vector2(-6, -2);
		area.gameObject.AddComponent<RectMask2D>();
		TMP_Text placeholderText = Text(area, "Placeholder", placeholder, 9, Hex("4A5070"), TextAlignmentOptions.MidlineLeft);
		Stretch(placeholderText.rectTransform);
		TMP_Text text = Text(area, "Text", "", 9, Color.white, TextAlignmentOptions.MidlineLeft);
		Stretch(text.rectTransform);
		input.textViewport = area;
		input.textComponent = text;
		input.placeholder = placeholderText;
		if (font != null) input.fontAsset = font;
		input.pointSize = 9;
		input.characterLimit = 20;
		input.lineType = TMP_InputField.LineType.SingleLine;
		input.caretColor = Teal;
		input.customCaretColor = true;
		input.selectionColor = new Color(0.37f, 0.95f, 0.84f, 0.35f);
		if (password)
		{
			input.contentType = TMP_InputField.ContentType.Password;
			input.asteriskChar = '*';
		}
		return input;
	}

	// 가로 줄 (버튼 줄). 버튼은 정한 너비, Spacer가 남는 자리를 채운다
	static RectTransform Row(RectTransform parent, string name, float height)
	{
		RectTransform row = NewUI(name, parent);
		Height(row.gameObject, height);
		HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.spacing = 8;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = true; // 버튼이 줄 높이를 꽉 채우게 (버튼은 높이를 따로 안 정함)
		return row;
	}

	static void Spacer(RectTransform row)
	{
		RectTransform spacer = NewUI("Spacer", row);
		spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
	}

	// 주황 버튼 (시작, 준비, 방 만들기)
	static (Button, TMP_Text) MainButton(RectTransform row, string name, string label, float width)
	{
		RectTransform r = NewUI(name, row);
		Width(r.gameObject, width);
		Button button = ButtonOn(r, "Assets/_HW/UI/Account/acc_button.png", null);
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
		colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f);
		button.colors = colors;
		TMP_Text text = Text(r, "Text", label, 10, Dark, TextAlignmentOptions.Center);
		text.fontStyle = FontStyles.Bold;
		Stretch(text.rectTransform);
		return (button, text);
	}

	// 테두리만 있는 어두운 버튼 (나가기, 취소, 테스트)
	static (Button, TMP_Text) SubButton(RectTransform row, string name, string label, float width, float size = 10)
	{
		RectTransform r = NewUI(name, row);
		Width(r.gameObject, width);
		Button button = ButtonOn(r, "Assets/_HW/UI/Settings/set_button.png", "Assets/_HW/UI/Settings/set_button_selected.png");
		TMP_Text text = Text(r, "Text", label, size, Color.white, TextAlignmentOptions.Center);
		Stretch(text.rectTransform);
		return (button, text);
	}

	static Button ButtonOn(RectTransform r, string sprite, string hoverSprite)
	{
		Image image = SetImage(r.gameObject, sprite);
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		if (hoverSprite != null)
		{
			Sprite on = LoadSprite(hoverSprite);
			button.transition = Selectable.Transition.SpriteSwap;
			button.spriteState = new SpriteState { highlightedSprite = on, selectedSprite = on, pressedSprite = on };
		}
		return button;
	}

	// 기본 도구 -------------------------------------------------------------------

	static GameObject SavePrefab(GameObject go, string path)
	{
		GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
		Object.DestroyImmediate(go);
		return prefab;
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
		text.richText = true;
		text.raycastTarget = false;
		return text;
	}

	static Image SetImage(GameObject go, string path)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(path);
		image.type = Image.Type.Sliced;
		image.color = Color.white;
		return image;
	}

	static Sprite LoadSprite(string path)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (sprite == null) Debug.LogWarning($"[RaidLobbyUIBuilder] {path} 스프라이트를 찾지 못함");
		return sprite;
	}

	static void Height(GameObject go, float height)
	{
		LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		element.preferredHeight = height;
		element.minHeight = height;
	}

	static void Width(GameObject go, float width)
	{
		LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		element.preferredWidth = width;
		element.minWidth = width;
	}

	static void Set(SerializedObject so, string property, Object value)
	{
		SerializedProperty p = so.FindProperty(property);
		if (p == null) Debug.LogWarning($"[RaidLobbyUIBuilder] {so.targetObject.GetType().Name}에 {property} 칸이 없음");
		else
		{
			p.objectReferenceValue = value;
			so.ApplyModifiedPropertiesWithoutUndo();
		}
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		if (parent != null) go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Stretch(RectTransform r)
	{
		r.anchorMin = Vector2.zero;
		r.anchorMax = Vector2.one;
		r.offsetMin = r.offsetMax = Vector2.zero;
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
