using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그인·회원가입·캐릭터 선택 화면과 접속 끊김 팝업 (시안 1~5, 14). 그리기만 하고, 값과 동작은 모두 이우림의 AccountFlow가 갖는다 (account.md).
/// <para>
/// - AccountFlow.Changed를 받을 때마다 AccountFlow의 값대로 다시 그린다. 버튼은 AccountFlow의 함수만 부른다. Mirror나 메시지는 직접 쓰지 않는다.<br/>
/// - 입력 중인 글자(아이디, 비밀번호 등)는 이 화면의 입력칸이 들고 있다가 버튼을 누를 때 넘긴다. 서버 주소 칸만 AccountFlow.ServerAddress에 바로 쓴다.<br/>
/// - 안내 문장(시안 5): 실패 이유는 빨간 문장(A) + 가리키는 칸의 빨간 테두리(B), 응답 기다리는 중은 회색 문장(C), 접속 끊김은 팝업(D).<br/>
/// - Enter: 입력칸에서 누르면 그 화면의 확인 버튼. 입력칸이 아닌 곳에서는 UIShortcut(이우림 #298)이 같은 함수를 부른다. ESC: 회원가입이면 로그인으로, 팝업이면 확인.<br/>
/// - AccountCanvas 프리팹 최상위에 붙이고 Base 씬에 둔다. 프리팹은 메뉴 Invoke → UI → 접속 화면 만들기(AccountUIBuilder)가 만든다.<br/>
/// - 이 화면을 쓰면 임시 OnGUI 화면(AccountScreen)은 꺼 둔다 (Base NetworkManager의 컴포넌트 체크 해제).
/// </para>
/// </summary>
public class AccountView : MonoBehaviour
{
	const string WaitingMessage = "서버 응답을 기다리는 중...";

	[Header("전체")]
	[SerializeField, Tooltip("배경과 세 화면을 담은 오브젝트. 게임에 들어가면 숨긴다")]
	GameObject screenRoot;
	[SerializeField] GameObject loginPage;
	[SerializeField] GameObject signUpPage;
	[SerializeField] GameObject charactersPage;

	[Header("로그인")]
	[SerializeField] GameObject loginServerRow;
	[SerializeField] TMP_InputField loginServer;
	[SerializeField] TMP_InputField loginId;
	[SerializeField] TMP_InputField loginPassword;
	[SerializeField] Button loginButton;
	[SerializeField] Button toSignUpButton;
	[SerializeField] Button hostButton;
	[SerializeField] TMP_Text loginMessage;

	[Header("회원가입")]
	[SerializeField] GameObject signUpServerRow;
	[SerializeField] TMP_InputField signUpServer;
	[SerializeField] TMP_InputField signUpId;
	[SerializeField] TMP_InputField signUpNickname;
	[SerializeField] TMP_InputField signUpPassword;
	[SerializeField] TMP_InputField signUpConfirm;
	[SerializeField] Button signUpButton;
	[SerializeField] Button toLoginButton;
	[SerializeField] TMP_Text signUpMessage;

	[Header("캐릭터")]
	[SerializeField, Tooltip("화면 전체 배경의 컨셉 아트 자리 (지금은 큰 글자)")]
	TMP_Text conceptArt;
	[SerializeField] TMP_Text characterTitle;
	[SerializeField] TMP_Text characterIcon;
	[SerializeField] TMP_Text characterState;
	[SerializeField] TMP_Text characterIntro;
	[SerializeField] TMP_Text confirmHint;
	[SerializeField] Button confirmButton;
	[SerializeField] TMP_Text confirmLabel;
	[SerializeField] TMP_Text characterMessage;
	[SerializeField, Tooltip("언어 카드. AccountFlow.Languages 순서대로 쓴다 (C, Java, Python)")]
	Button[] cards;
	[SerializeField] Sprite cardSprite;
	[SerializeField] Sprite cardSelectedSprite;

	[Header("팝업")]
	[SerializeField] GameObject popupRoot;
	[SerializeField] TMP_Text popupMessage;
	[SerializeField] Button popupButton;

	[Header("그림")]
	[SerializeField, Tooltip("입력칸 기본 테두리")] Sprite fieldSprite;
	[SerializeField, Tooltip("안내 문장이 가리키는 칸 (빨간 테두리)")] Sprite fieldErrorSprite;
	[SerializeField, Tooltip("비밀번호 가림 상태의 눈 (누르면 보임)")] Sprite eyeSprite;
	[SerializeField, Tooltip("비밀번호 보임 상태의 눈 (누르면 가림)")] Sprite eyeOffSprite;

	[Header("색")]
	[SerializeField] Color errorColor = new Color32(0xE0, 0x70, 0x70, 0xFF);
	[SerializeField] Color waitingColor = new Color32(0x8A, 0x90, 0xA8, 0xFF);
	[SerializeField] Color ownedColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	[SerializeField] Color notOwnedColor = new Color32(0xF0, 0xA0, 0x60, 0xFF);

	AccountFlow flow;
	readonly Dictionary<AccountField, List<Image>> fieldFrames = new Dictionary<AccountField, List<Image>>();

	void Start()
	{
		flow = AccountFlow.Current;
		if (flow == null)
		{
			Debug.LogWarning("[AccountView] AccountFlow를 찾지 못해 접속 화면을 숨깁니다 (Base 씬의 NetworkManager 확인).", this);
			gameObject.SetActive(false);
			return;
		}

		AddFrame(AccountField.ServerAddress, loginServer, signUpServer);
		AddFrame(AccountField.LoginId, loginId, signUpId);
		AddFrame(AccountField.Nickname, signUpNickname);
		AddFrame(AccountField.Password, loginPassword, signUpPassword);
		AddFrame(AccountField.PasswordConfirm, signUpConfirm);

		if (loginButton != null) loginButton.onClick.AddListener(SubmitLogin);
		if (toSignUpButton != null) toSignUpButton.onClick.AddListener(() => flow.ShowSignUp());
		if (hostButton != null) hostButton.onClick.AddListener(() => flow.StartHost());
		if (signUpButton != null) signUpButton.onClick.AddListener(SubmitSignUp);
		if (toLoginButton != null) toLoginButton.onClick.AddListener(BackToLogin);
		if (confirmButton != null) confirmButton.onClick.AddListener(Confirm);
		if (popupButton != null) popupButton.onClick.AddListener(ClosePopup);
		for (int i = 0; i < cards.Length; i++)
		{
			int index = i;
			if (cards[i] != null) cards[i].onClick.AddListener(() => SelectCard(index));
		}

		// 입력칸에서 Enter: 그 화면의 확인 버튼 (입력칸이 선택돼 있으면 UIShortcut은 양보하므로 여기서 받는다)
		foreach (TMP_InputField field in new[] { loginServer, loginId, loginPassword }) Submit(field, SubmitLogin);
		foreach (TMP_InputField field in new[] { signUpServer, signUpId, signUpNickname, signUpPassword, signUpConfirm }) Submit(field, SubmitSignUp);
		foreach (TMP_InputField field in new[] { loginServer, signUpServer })
		{
			if (field != null) field.onValueChanged.AddListener(value => flow.ServerAddress = value);
		}

		// 비밀번호 칸 오른쪽 눈: 누르면 글자를 보이거나 가린다
		foreach (TMP_InputField field in new[] { loginPassword, signUpPassword, signUpConfirm }) SetupEye(field);

		flow.Changed += Refresh;
		Refresh();
	}

	void SetupEye(TMP_InputField field)
	{
		if (field == null) return;
		Transform eye = field.transform.Find("Eye");
		Button button = eye != null ? eye.GetComponent<Button>() : null;
		if (button == null) return;
		button.onClick.AddListener(() => SetPasswordVisible(field, field.contentType == TMP_InputField.ContentType.Password));
		SetPasswordVisible(field, false);
	}

	void SetPasswordVisible(TMP_InputField field, bool visible)
	{
		if (field == null) return;
		TMP_InputField.ContentType type = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
		if (field.contentType != type)
		{
			field.contentType = type;
			field.ForceLabelUpdate();
		}
		Transform eye = field.transform.Find("Eye");
		Image icon = eye != null ? eye.GetComponent<Image>() : null;
		if (icon != null && eyeSprite != null) icon.sprite = visible ? eyeOffSprite : eyeSprite;
	}

	void OnDestroy()
	{
		if (flow != null) flow.Changed -= Refresh;
	}

	// 버튼·단축키 (UIShortcut에도 연결) -----------------------------------------------

	public void SubmitLogin()
	{
		if (flow == null || flow.IsWaiting || flow.Page != AccountPage.Login) return;
		flow.Login(Text(loginId), Text(loginPassword));
	}

	public void SubmitSignUp()
	{
		if (flow == null || flow.IsWaiting || flow.Page != AccountPage.SignUp) return;
		flow.SignUp(Text(signUpId), Text(signUpPassword), Text(signUpConfirm), Text(signUpNickname));
	}

	public void BackToLogin()
	{
		if (flow != null && !flow.IsWaiting) flow.ShowLogin();
	}

	public void Confirm()
	{
		if (flow != null && flow.CanConfirm) flow.Confirm();
	}

	public void ClosePopup()
	{
		if (flow != null) flow.ClosePopup();
	}

	void SelectCard(int index)
	{
		if (flow == null || index >= flow.Languages.Count) return;
		flow.Select(flow.Languages[index]);
	}

	// 그리기 -------------------------------------------------------------------

	void Refresh()
	{
		bool popup = flow.PopupMessage != null;
		if (popupRoot != null) popupRoot.SetActive(popup);
		if (popupMessage != null && popup) popupMessage.text = flow.PopupMessage;

		bool visible = flow.IsVisible;
		if (screenRoot != null) screenRoot.SetActive(visible);
		if (!visible) return;

		AccountPage page = flow.Page;
		SetActive(loginPage, page == AccountPage.Login);
		SetActive(signUpPage, page == AccountPage.SignUp);
		SetActive(charactersPage, page == AccountPage.Characters);

		bool canEdit = !flow.IsWaiting;
		SetActive(loginServerRow, flow.CanEditServerAddress);
		SetActive(signUpServerRow, flow.CanEditServerAddress);
		SyncServerAddress(loginServer);
		SyncServerAddress(signUpServer);
		if (hostButton != null) hostButton.gameObject.SetActive(page == AccountPage.Login && flow.CanStartHost);
		foreach (Button button in new[] { loginButton, toSignUpButton, hostButton, signUpButton, toLoginButton }) SetInteractable(button, canEdit);

		// 안내 문장: 기다리는 중이면 회색, 실패 이유면 빨강 + 칸 강조
		TMP_Text message = page == AccountPage.Login ? loginMessage : page == AccountPage.SignUp ? signUpMessage : characterMessage;
		foreach (TMP_Text text in new[] { loginMessage, signUpMessage, characterMessage })
		{
			if (text != null && text != message) text.text = "";
		}
		if (message != null)
		{
			message.text = flow.IsWaiting ? WaitingMessage : flow.Message;
			message.color = flow.IsWaiting ? waitingColor : errorColor;
		}
		foreach (KeyValuePair<AccountField, List<Image>> pair in fieldFrames)
		{
			bool marked = !flow.IsWaiting && flow.Message.Length > 0 && flow.MessageField == pair.Key;
			foreach (Image frame in pair.Value)
			{
				if (fieldSprite != null && fieldErrorSprite != null) frame.sprite = marked ? fieldErrorSprite : fieldSprite;
				else frame.color = marked ? errorColor : Color.white;
			}
		}

		if (page == AccountPage.Characters) RefreshCharacters();
	}

	void RefreshCharacters()
	{
		// 로그인한 뒤에는 비밀번호를 화면에 남겨 두지 않는다
		foreach (TMP_InputField field in new[] { loginPassword, signUpPassword, signUpConfirm })
		{
			Clear(field);
			SetPasswordVisible(field, false);
		}

		string selected = flow.SelectedLanguage;
		CharacterState state = flow.GetCharacterState(selected);
		if (characterTitle != null) characterTitle.text = $"> 캐릭터 선택 - {flow.Nickname}";
		if (characterIcon != null) characterIcon.text = IconOf(selected);
		if (conceptArt != null) conceptArt.text = IconOf(selected);
		if (characterState != null)
		{
			characterState.text = state == CharacterState.Owned ? "보유 중" : "미보유";
			characterState.color = state == CharacterState.Owned ? ownedColor : notOwnedColor;
		}
		if (characterIntro != null) characterIntro.text = IntroOf(selected);
		if (confirmHint != null) confirmHint.text = state == CharacterState.Owned ? "이 캐릭터로 게임에 들어갑니다" : $"{selected} 캐릭터를 만듭니다";
		if (confirmLabel != null) confirmLabel.text = flow.ConfirmLabel;
		SetInteractable(confirmButton, flow.CanConfirm);

		for (int i = 0; i < cards.Length; i++)
		{
			Button card = cards[i];
			if (card == null) continue;
			bool exists = i < flow.Languages.Count;
			card.gameObject.SetActive(exists);
			if (!exists) continue;

			string language = flow.Languages[i];
			CharacterState cardState = flow.GetCharacterState(language);
			bool isSelected = language == selected;
			card.interactable = cardState != CharacterState.Locked && !flow.IsWaiting;
			if (card.image != null && cardSprite != null) card.image.sprite = isSelected ? cardSelectedSprite : cardSprite;

			TMP_Text[] texts = card.GetComponentsInChildren<TMP_Text>(true);
			if (texts.Length >= 3)
			{
				texts[0].text = IconOf(language);
				texts[1].text = language;
				texts[2].text = cardState == CharacterState.Locked ? "준비 중" : cardState == CharacterState.Owned ? "보유 중" : "";
			}
		}
	}

	// 도구 ---------------------------------------------------------------------

	void AddFrame(AccountField field, params TMP_InputField[] inputs)
	{
		var frames = new List<Image>();
		foreach (TMP_InputField input in inputs)
		{
			if (input != null && input.image != null) frames.Add(input.image);
		}
		fieldFrames[field] = frames;
	}

	static void Submit(TMP_InputField field, UnityEngine.Events.UnityAction action)
	{
		if (field != null) field.onSubmit.AddListener(_ => action());
	}

	void SyncServerAddress(TMP_InputField field)
	{
		if (field == null || field.isFocused || field.text == flow.ServerAddress) return;
		field.SetTextWithoutNotify(flow.ServerAddress ?? "");
	}

	static string Text(TMP_InputField field) => field != null ? field.text : "";

	static void Clear(TMP_InputField field)
	{
		if (field != null && field.text.Length > 0) field.SetTextWithoutNotify("");
	}

	static void SetActive(GameObject obj, bool active)
	{
		if (obj != null && obj.activeSelf != active) obj.SetActive(active);
	}

	static void SetInteractable(Button button, bool interactable)
	{
		if (button != null) button.interactable = interactable;
	}

	// 카드와 컨셉 아트 자리에 쓰는 글자. 컨셉 아트 그림이 생기면 그림으로 바꾼다
	static string IconOf(string language) => language switch
	{
		CharacterLanguages.Python => "Py",
		_ => string.IsNullOrEmpty(language) ? "?" : language.Substring(0, 1),
	};

	// 캐릭터 소개 (임시 문장. 기획이 정하면 바꾼다)
	static string IntroOf(string language) => language switch
	{
		CharacterLanguages.C => "레이피어 한 자루로 버그를 찌르는 검사.\n포인터로 소환검을 부린다.",
		_ => "",
	};
}
