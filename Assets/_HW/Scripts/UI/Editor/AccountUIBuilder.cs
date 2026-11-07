using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 접속 화면 프리팹(Assets/_HW/UI/Account/AccountCanvas)을 우림이 시안(로그인 화면 시안 1~5, 14)대로 만드는 에디터 도구. 게임에는 들어가지 않는다.
/// <para>
/// 메뉴 Invoke → UI → 접속 화면 만들기. 여러 번 눌러도 된다 (프리팹을 다시 만든다). 만든 뒤 Base 씬에 끌어다 놓는다.<br/>
/// - 캔버스 기준 640x360 (다른 UI와 같음). 위치 숫자는 시안의 아트보드 좌표를 그대로 옮긴 것.<br/>
/// - 그림: Assets/_HW/UI/Account (패널·칸·버튼·카드 테두리, 아래 폐허 실루엣). 글꼴 Galmuri11.
/// </para>
/// </summary>
public static class AccountUIBuilder
{
	const string Dir = "Assets/_HW/UI/Account";
	const string PrefabPath = Dir + "/AccountCanvas.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";

	static readonly Color Bg = Hex("0F1220");
	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Orange = Hex("E8A867");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("7C84A8");
	static readonly Color Dark = Hex("1A1E3A");
	static readonly Color Red = Hex("E07070");
	static readonly Color ConceptGray = Hex("232A44");

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/접속 화면 만들기")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var canvasObject = new GameObject("AccountCanvas", typeof(RectTransform));
		canvasObject.layer = 5;
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 110; // 로딩 화면(PlaceTravel, 100)보다 위. 접속 직후 로그인·캐릭터 화면을 보는 동안 로딩 화면이 덮고 있기 때문 (place-scenes.md)
		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(640, 360);
		scaler.matchWidthOrHeight = 0;
		canvasObject.AddComponent<GraphicRaycaster>();
		AccountView view = canvasObject.AddComponent<AccountView>();

		// 배경 (게임 화면을 가리고 클릭을 막음)
		RectTransform screen = NewUI("Screen", canvasObject.transform);
		Stretch(screen);
		screen.gameObject.AddComponent<Image>().color = Bg;
		RectTransform skyline = NewUI("Skyline", screen);
		skyline.anchorMin = new Vector2(0, 0);
		skyline.anchorMax = new Vector2(1, 0);
		skyline.pivot = new Vector2(0.5f, 0);
		skyline.sizeDelta = new Vector2(0, 110);
		skyline.anchoredPosition = Vector2.zero;
		Image skylineImage = skyline.gameObject.AddComponent<Image>();
		skylineImage.sprite = LoadSprite("acc_skyline");
		skylineImage.raycastTarget = false;

		// 1. 로그인 -----------------------------------------------------------------
		RectTransform login = Page(screen, "LoginPage");
		Logo(login);
		RectTransform loginColumn = Column(login, "LoginColumn", new Vector2(155, 13));
		RectTransform loginPanel = Panel(loginColumn, "Panel");
		Title(loginPanel, "> 로그인");
		(GameObject loginServerRow, TMP_InputField loginServer) = Field(loginPanel, "ServerRow", "서버 주소", "", "[EC2 퍼블릭 IP]", 64, false);
		(_, TMP_InputField loginId) = Field(loginPanel, "IdRow", "아이디", "", "영문, 숫자 4~16자", AccountRules.LoginIdMaxLength, false);
		(_, TMP_InputField loginPassword) = Field(loginPanel, "PasswordRow", "비밀번호", "", "8~64자", AccountRules.PasswordMaxLength, true);
		Spacer(loginPanel, 4);
		(Button loginButton, _) = MainButton(loginPanel, "LoginButton", "로그인", 22);
		TMP_Text loginMessage = Message(loginPanel, "Message");
		Button toSignUp = Link(loginPanel, "ToSignUp", "계정이 없으신가요? <color=#5EF2D6><u>회원가입</u></color>");
		Button host = HostButton(loginColumn);

		// 1-1. 회원가입 ---------------------------------------------------------------
		RectTransform signUp = Page(screen, "SignUpPage");
		Logo(signUp);
		RectTransform signUpColumn = Column(signUp, "SignUpColumn", new Vector2(155, 0));
		RectTransform signUpPanel = Panel(signUpColumn, "Panel");
		Title(signUpPanel, "> 회원가입");
		(GameObject signUpServerRow, TMP_InputField signUpServer) = Field(signUpPanel, "ServerRow", "서버 주소", "", "[EC2 퍼블릭 IP]", 64, false);
		(_, TMP_InputField signUpId) = Field(signUpPanel, "IdRow", "아이디", "영문, 숫자 4~16자", "", AccountRules.LoginIdMaxLength, false);
		(_, TMP_InputField signUpNickname) = Field(signUpPanel, "NicknameRow", "닉네임", "한글, 영문, 숫자 2~10자", "", AccountRules.NicknameMaxLength, false);
		(_, TMP_InputField signUpPassword) = Field(signUpPanel, "PasswordRow", "비밀번호", "8~64자", "", AccountRules.PasswordMaxLength, true);
		(_, TMP_InputField signUpConfirm) = Field(signUpPanel, "ConfirmRow", "비밀번호 확인", "", "", AccountRules.PasswordMaxLength, true);
		Spacer(signUpPanel, 4);
		(Button signUpButton, _) = MainButton(signUpPanel, "SignUpButton", "가입하기", 22);
		TMP_Text signUpMessage = Message(signUpPanel, "Message");
		Button toLogin = Link(signUpPanel, "ToLogin", "이미 계정이 있으신가요? <color=#5EF2D6><u>로그인</u></color>");

		// 2·3. 캐릭터 선택 --------------------------------------------------------------
		RectTransform characters = Page(screen, "CharactersPage");
		TMP_Text concept = Text(characters, "ConceptArt", "C", 220, ConceptGray, TextAlignmentOptions.Center);
		Place(concept.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-182, 72), new Vector2(260, 260));

		RectTransform info = NewUI("InfoPanel", characters);
		Place(info, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(190, 108), new Vector2(210, 95));
		info.gameObject.AddComponent<Image>().color = new Color32(0x11, 0x15, 0x2A, 0xE6);
		TMP_Text charTitle = Text(info, "Title", "> 캐릭터 선택 - [닉네임]", 8, Label, TextAlignmentOptions.TopLeft);
		Place(charTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -8), new Vector2(190, 12));
		TMP_Text charIcon = Text(info, "Icon", "C", 20, Teal, TextAlignmentOptions.TopLeft);
		Place(charIcon.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -22), new Vector2(60, 24));
		TMP_Text charState = Text(info, "State", "미보유", 9, Orange, TextAlignmentOptions.TopLeft);
		Place(charState.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -48), new Vector2(190, 12));
		TMP_Text charIntro = Text(info, "Intro", "[C 캐릭터 소개]", 8, Color.white, TextAlignmentOptions.TopLeft);
		charIntro.textWrappingMode = TextWrappingModes.Normal;
		Place(charIntro.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -63), new Vector2(190, 28));

		Color[] iconColors = { Teal, Hex("C08A5A"), Hex("7A88D8") };
		string[] iconTexts = { "C", "J", "Py" };
		string[] names = { "C", "Java", "Python" };
		var cards = new Button[3];
		for (int i = 0; i < 3; i++)
		{
			RectTransform card = NewUI("Card" + i, characters);
			Place(card, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24 + i * 68, 20), new Vector2(60, 60));
			Image cardImage = SetImage(card.gameObject, "acc_card");
			Button button = card.gameObject.AddComponent<Button>();
			button.targetGraphic = cardImage;
			ColorBlock colors = button.colors;
			colors.disabledColor = new Color(1, 1, 1, 0.6f);
			button.colors = colors;
			TMP_Text icon = Text(card, "Icon", iconTexts[i], 16, iconColors[i], TextAlignmentOptions.Center);
			Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(56, 20));
			TMP_Text cardName = Text(card, "Name", names[i], 7, Label, TextAlignmentOptions.Center);
			Place(cardName.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(56, 10));
			TMP_Text cardState = Text(card, "State", i == 0 ? "" : "준비 중", 6, Muted, TextAlignmentOptions.Center);
			Place(cardState.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(56, 9));
			cards[i] = button;
		}

		TMP_Text charMessage = Text(characters, "Message", "", 8, Red, TextAlignmentOptions.BottomRight);
		Place(charMessage.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 78), new Vector2(300, 12));
		TMP_Text hint = Text(characters, "Hint", "C 캐릭터를 만듭니다", 8, Label, TextAlignmentOptions.BottomRight);
		Place(hint.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 58), new Vector2(200, 12));
		(Button confirm, TMP_Text confirmText) = MainButton(characters, "ConfirmButton", "만들기", 32, 12);
		Place((RectTransform)confirm.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 20), new Vector2(120, 32));

		// 4·14. 접속 끊김 팝업 (게임 중에도 뜨므로 Screen 밖) ------------------------------------
		RectTransform popupRoot = NewUI("Popup", canvasObject.transform);
		Stretch(popupRoot);
		popupRoot.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.55f);
		RectTransform popup = NewUI("Window", popupRoot);
		Place(popup, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220, 96));
		SetImage(popup.gameObject, "acc_popup");
		TMP_Text popupTitle = Text(popup, "Title", "! 접속이 끊겼습니다", 11, Hex("F0A060"), TextAlignmentOptions.TopLeft);
		Place(popupTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -12), new Vector2(190, 16));
		TMP_Text popupText = Text(popup, "Message", "서버와 연결이 끊겼습니다.", 8, Color.white, TextAlignmentOptions.TopLeft);
		popupText.textWrappingMode = TextWrappingModes.Normal;
		Place(popupText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -32), new Vector2(190, 24));
		(Button popupButton, _) = MainButton(popup, "OkButton", "확인", 22);
		Place((RectTransform)popupButton.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(186, 22));

		// 칸 연결 ------------------------------------------------------------------
		var so = new SerializedObject(view);
		Set(so, "screenRoot", screen.gameObject);
		Set(so, "loginPage", login.gameObject);
		Set(so, "signUpPage", signUp.gameObject);
		Set(so, "charactersPage", characters.gameObject);
		Set(so, "loginServerRow", loginServerRow);
		Set(so, "loginServer", loginServer);
		Set(so, "loginId", loginId);
		Set(so, "loginPassword", loginPassword);
		Set(so, "loginButton", loginButton);
		Set(so, "toSignUpButton", toSignUp);
		Set(so, "hostButton", host);
		Set(so, "loginMessage", loginMessage);
		Set(so, "signUpServerRow", signUpServerRow);
		Set(so, "signUpServer", signUpServer);
		Set(so, "signUpId", signUpId);
		Set(so, "signUpNickname", signUpNickname);
		Set(so, "signUpPassword", signUpPassword);
		Set(so, "signUpConfirm", signUpConfirm);
		Set(so, "signUpButton", signUpButton);
		Set(so, "toLoginButton", toLogin);
		Set(so, "signUpMessage", signUpMessage);
		Set(so, "conceptArt", concept);
		Set(so, "characterTitle", charTitle);
		Set(so, "characterIcon", charIcon);
		Set(so, "characterState", charState);
		Set(so, "characterIntro", charIntro);
		Set(so, "confirmHint", hint);
		Set(so, "confirmButton", confirm);
		Set(so, "confirmLabel", confirmText);
		Set(so, "characterMessage", charMessage);
		SerializedProperty cardList = so.FindProperty("cards");
		cardList.arraySize = cards.Length;
		for (int i = 0; i < cards.Length; i++) cardList.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
		Set(so, "cardSprite", LoadSprite("acc_card"));
		Set(so, "fieldSprite", LoadSprite("acc_field"));
		Set(so, "fieldErrorSprite", LoadSprite("acc_field_error"));
		Set(so, "eyeSprite", LoadSprite("acc_eye"));
		Set(so, "eyeOffSprite", LoadSprite("acc_eye_off"));
		Set(so, "cardSelectedSprite", LoadSprite("acc_card_selected"));
		Set(so, "popupRoot", popupRoot.gameObject);
		Set(so, "popupMessage", popupText);
		Set(so, "popupButton", popupButton);
		so.ApplyModifiedPropertiesWithoutUndo();

		// Enter·ESC (UIShortcut, #298): 화면마다 붙이고 같은 함수를 연결
		Shortcut(login.gameObject, view.SubmitLogin, null);
		Shortcut(signUp.gameObject, view.SubmitSignUp, view.BackToLogin);
		Shortcut(characters.gameObject, view.Confirm, null);
		Shortcut(popupRoot.gameObject, view.ClosePopup, view.ClosePopup);

		signUp.gameObject.SetActive(false);
		characters.gameObject.SetActive(false);
		popupRoot.gameObject.SetActive(false);

		PrefabUtility.SaveAsPrefabAsset(canvasObject, PrefabPath);
		Object.DestroyImmediate(canvasObject);
		AssetDatabase.SaveAssets();
		Debug.Log($"[AccountUIBuilder] {PrefabPath}를 만듦. Base 씬에 끌어다 놓고, NetworkManager의 AccountScreen(OnGUI 임시 화면)은 체크를 끄세요.");
	}

	// 부품 ---------------------------------------------------------------------

	static RectTransform Page(RectTransform screen, string name)
	{
		RectTransform page = NewUI(name, screen);
		Stretch(page);
		return page;
	}

	static void Logo(RectTransform page)
	{
		TMP_Text logo = Text(page, "Logo", "INVOKE", 30, Orange, TextAlignmentOptions.Center);
		logo.characterSpacing = 8;
		logo.fontStyle = FontStyles.Bold;
		Place(logo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-171, 2), new Vector2(240, 44));
	}

	// 패널 + 그 아래 Host 버튼을 세로로 쌓는 기둥. 높이는 내용에 맞춘다
	static RectTransform Column(RectTransform page, string name, Vector2 position)
	{
		RectTransform column = NewUI(name, page);
		Place(column, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(210, 0));
		VerticalLayoutGroup layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 6;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		column.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		return column;
	}

	static RectTransform Panel(RectTransform column, string name)
	{
		RectTransform panel = NewUI(name, column);
		SetImage(panel.gameObject, "acc_panel");
		VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.padding = new RectOffset(16, 16, 12, 12);
		layout.spacing = 4;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;
		return panel;
	}

	static void Title(RectTransform panel, string value)
	{
		TMP_Text title = Text(panel, "Title", value, 13, Teal, TextAlignmentOptions.MidlineLeft);
		Height(title.gameObject, 20);
	}

	// 이름표 + 입력칸 한 줄. hint는 이름표 옆 회색 글자, placeholder는 빈 칸에 보이는 글자
	static (GameObject, TMP_InputField) Field(RectTransform panel, string name, string label, string hint, string placeholder, int limit, bool password)
	{
		RectTransform row = NewUI(name, panel);
		VerticalLayoutGroup layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
		layout.spacing = 2;
		layout.childControlWidth = layout.childControlHeight = true;
		layout.childForceExpandWidth = true;
		layout.childForceExpandHeight = false;

		string labelText = hint.Length > 0 ? $"{label}  <color=#7C84A8>- {hint}</color>" : label;
		TMP_Text labelUI = Text(row, "Label", labelText, 8, Label, TextAlignmentOptions.MidlineLeft);
		Height(labelUI.gameObject, 11);

		RectTransform box = NewUI("Input", row);
		Height(box.gameObject, 20);
		Image boxImage = SetImage(box.gameObject, "acc_field");
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
		input.characterLimit = limit;
		input.lineType = TMP_InputField.LineType.SingleLine;
		input.caretColor = Teal;
		input.customCaretColor = true;
		input.selectionColor = new Color(0.37f, 0.95f, 0.84f, 0.35f);
		if (password)
		{
			input.contentType = TMP_InputField.ContentType.Password;
			input.asteriskChar = '*';
			// 오른쪽 눈 버튼 (AccountView가 누르면 보이기·가리기). 글자가 눈 밑으로 들어가지 않게 글자 칸을 줄인다
			area.offsetMax = new Vector2(-20, -2);
			RectTransform eye = NewUI("Eye", box);
			Place(eye, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(11, 7));
			Image eyeImage = eye.gameObject.AddComponent<Image>();
			eyeImage.sprite = LoadSprite("acc_eye");
			Button eyeButton = eye.gameObject.AddComponent<Button>();
			eyeButton.targetGraphic = eyeImage;
			// 눈을 누르기 쉽게 투명한 판을 더 넓게 깐다
			RectTransform hit = NewUI("Hit", eye);
			Place(hit, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18, 16));
			hit.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0);
		}
		return (row.gameObject, input);
	}

	static (Button, TMP_Text) MainButton(RectTransform parent, string name, string label, float height, float size = 10)
	{
		RectTransform r = NewUI(name, parent);
		Height(r.gameObject, height);
		Image image = SetImage(r.gameObject, "acc_button");
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		ColorBlock colors = button.colors;
		colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
		colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
		button.colors = colors;
		TMP_Text text = Text(r, "Text", label, size, Dark, TextAlignmentOptions.Center);
		text.fontStyle = FontStyles.Bold;
		Stretch(text.rectTransform);
		return (button, text);
	}

	static TMP_Text Message(RectTransform panel, string name)
	{
		TMP_Text message = Text(panel, name, "", 8, Red, TextAlignmentOptions.MidlineLeft);
		message.textWrappingMode = TextWrappingModes.Normal;
		Height(message.gameObject, 11);
		return message;
	}

	static Button Link(RectTransform panel, string name, string value)
	{
		TMP_Text text = Text(panel, name, value, 8, Label, TextAlignmentOptions.MidlineLeft);
		text.raycastTarget = true;
		Height(text.gameObject, 12);
		Button button = text.gameObject.AddComponent<Button>();
		button.targetGraphic = text;
		button.transition = Selectable.Transition.None;
		return button;
	}

	static Button HostButton(RectTransform column)
	{
		RectTransform r = NewUI("HostButton", column);
		Height(r.gameObject, 22);
		Image image = SetImage(r.gameObject, "acc_host");
		Button button = r.gameObject.AddComponent<Button>();
		button.targetGraphic = image;
		TMP_Text text = Text(r, "Text", "Host (에디터에서만 보임)", 8, Muted, TextAlignmentOptions.Center);
		Stretch(text.rectTransform);
		return button;
	}

	static void Spacer(RectTransform panel, float height)
	{
		Height(NewUI("Spacer", panel).gameObject, height);
	}

	static void Shortcut(GameObject target, UnityAction submit, UnityAction cancel)
	{
		UIShortcut shortcut = target.AddComponent<UIShortcut>();
		Bind(shortcut, "onSubmit", submit);
		Bind(shortcut, "onCancel", cancel);
	}

	static void Bind(UIShortcut shortcut, string fieldName, UnityAction action)
	{
		if (action == null) return;
		FieldInfo field = typeof(UIShortcut).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
		if (field == null)
		{
			Debug.LogWarning($"[AccountUIBuilder] UIShortcut에 {fieldName} 칸이 없어 연결하지 못함");
			return;
		}
		var unityEvent = field.GetValue(shortcut) as UnityEvent;
		if (unityEvent == null)
		{
			unityEvent = new UnityEvent();
			field.SetValue(shortcut, unityEvent);
		}
		UnityEventTools.AddPersistentListener(unityEvent, action);
	}

	// 기본 도구 -------------------------------------------------------------------

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

	static Image SetImage(GameObject go, string sprite)
	{
		Image image = go.AddComponent<Image>();
		image.sprite = LoadSprite(sprite);
		image.type = Image.Type.Sliced;
		image.color = Color.white;
		return image;
	}

	static Sprite LoadSprite(string name)
	{
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{Dir}/{name}.png");
		if (sprite == null) Debug.LogWarning($"[AccountUIBuilder] {Dir}/{name}.png 스프라이트를 찾지 못함");
		return sprite;
	}

	static void Height(GameObject go, float height)
	{
		LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
		element.preferredHeight = height;
		element.minHeight = height;
	}

	static void Set(SerializedObject so, string property, Object value)
	{
		SerializedProperty p = so.FindProperty(property);
		if (p == null) Debug.LogWarning($"[AccountUIBuilder] AccountView에 {property} 칸이 없음");
		else p.objectReferenceValue = value;
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

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
