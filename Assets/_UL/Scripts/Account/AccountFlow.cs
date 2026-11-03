using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>로그인·회원가입·캐릭터 화면 가운데 지금 보여 줄 화면.</summary>
public enum AccountPage
{
	Login,
	SignUp,
	Characters,
	// 게임에 들어가서 화면을 숨긴 상태
	Hidden,
}

/// <summary>안내 문장이 가리키는 입력칸. 화면은 이 칸을 강조(빨간 테두리 등)한다.</summary>
public enum AccountField
{
	None,
	ServerAddress,
	LoginId,
	Nickname,
	Password,
	PasswordConfirm,
}

/// <summary>캐릭터 화면의 언어 카드 하나의 상태.</summary>
public enum CharacterState
{
	// 보유 중: 이 언어의 캐릭터를 만들어 둠. 버튼은 「들어가기」
	Owned,
	// 미보유: 만들 수 있지만 아직 만들지 않음. 버튼은 「만들기」
	NotOwned,
	// 준비 중: 아직 열지 않은 언어. 선택할 수 없음
	Locked,
}

/// <summary>
/// 로그인·회원가입·캐릭터 화면이 쓰는 것을 하나로 모은 컴포넌트. 화면은 이 컴포넌트만 쓴다.
/// <para>
/// 하는 일<br/>
/// - 지금 화면, 응답을 기다리는 중인지, 안내 문장과 강조할 칸, 계정 닉네임, 언어 카드마다 상태와 선택한 언어, 팝업 문장을 들고 있다.<br/>
/// - 버튼이 부를 함수(Login, SignUp, Select, Confirm 등)를 준다. 회원가입은 서버에 보내기 전에 규칙과 비밀번호 확인을 먼저 검사한다.<br/>
/// - 서버의 결과, 쫓겨남, 접속 실패, 연결 끊김을 상태에 반영한다. 캐릭터 화면이나 게임 중에 끊기면 팝업 문장을 채운다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Base 씬의 NetworkManager 오브젝트에 붙인다. 화면은 AccountFlow.Current로 가져온다.<br/>
/// - 화면은 Changed 이벤트를 받을 때마다 아래 값대로 다시 그린다. 버튼은 함수만 부른다. Mirror나 InvokeAuthenticator를 직접 쓰지 않는다.
/// </para>
/// </summary>
public class AccountFlow : MonoBehaviour
{
	/// <summary>회원가입에서 비밀번호와 비밀번호 확인이 다를 때의 문장. 서버로 보내지 않고 화면에서만 검사한다.</summary>
	public const string PasswordMismatchMessage = "비밀번호가 서로 다릅니다.";
	const string ConnectFailedMessage = "서버에 접속하지 못했습니다.";
	const string DisconnectedMessage = "서버와 연결이 끊겼습니다.";
	const string EnterLabel = "들어가기";
	const string CreateLabel = "만들기";

	InvokeAuthenticator authenticator;
	readonly List<string> ownedLanguages = new List<string>();

	// 서버가 보낸 끊김 이유. 서버는 알림을 보내고 잠시 뒤에 끊으므로, 실제로 끊긴 뒤 팝업에 쓴다
	AccountFailure kickedReason;
	// 지난 프레임에 클라이언트가 켜져 있었는지. 클라이언트가 꺼지는 순간을 알아내기 위함
	bool wasClientActive;

	/// <summary>Base 씬 NetworkManager에 붙은 AccountFlow. 없으면 null.</summary>
	public static AccountFlow Current { get; private set; }

	/// <summary>상태가 바뀔 때마다. 화면은 이때 다시 그린다.</summary>
	public event Action Changed;

	/// <summary>지금 보여 줄 화면.</summary>
	public AccountPage Page { get; private set; } = AccountPage.Login;

	/// <summary>서버 응답을 기다리는 중이면 true. 화면은 버튼을 막고 「서버 응답을 기다리는 중...」을 보여 준다.</summary>
	public bool IsWaiting { get; private set; }

	/// <summary>화면 아래에 보여 줄 실패 이유. 없으면 빈 문자열.</summary>
	public string Message { get; private set; } = "";

	/// <summary>Message가 가리키는 입력칸. 없으면 None.</summary>
	public AccountField MessageField { get; private set; }

	/// <summary>로그인한 계정의 닉네임. 로그인 전이면 빈 문자열.</summary>
	public string Nickname { get; private set; } = "";

	/// <summary>팝업에 보여 줄 문장 (예: 다른 곳에서 로그인함). 팝업이 없으면 null. ClosePopup으로 닫는다.</summary>
	public string PopupMessage { get; private set; }

	/// <summary>
	/// 화면을 그려야 하면 true. 들어가기에 성공했거나, 이 컴퓨터가 서버만 켠 경우 false.
	/// 팝업은 이 값과 상관없이 PopupMessage가 있으면 보여 준다.
	/// </summary>
	public bool IsVisible => Page != AccountPage.Hidden && !(NetworkServer.active && !NetworkClient.active);

	/// <summary>서버 주소 칸에 보여 줄 값. 바꾸면 다음 접속에 쓴다.</summary>
	public string ServerAddress
	{
		get => NetworkManager.singleton != null ? NetworkManager.singleton.networkAddress : "";
		set { if (NetworkManager.singleton != null) NetworkManager.singleton.networkAddress = value; }
	}

	/// <summary>서버 주소 칸을 고칠 수 있으면 true. 접속한 뒤에는 바꿔도 소용이 없으므로 false.</summary>
	public bool CanEditServerAddress => !NetworkClient.active;

	/// <summary>에디터 전용 Host 버튼을 보여 줄지. 빌드에서는 늘 false.</summary>
	public bool CanStartHost => Application.isEditor && !NetworkClient.active && !NetworkServer.active;

	/// <summary>캐릭터 화면에 보여 줄 언어. 화면은 이 순서대로 카드를 그린다.</summary>
	public IReadOnlyList<string> Languages => CharacterLanguages.Shown;

	/// <summary>
	/// 캐릭터 화면에서 선택한 언어. 언제나 Languages 가운데 준비 중이 아닌 하나다.
	/// 로그인하면 보유한 첫 캐릭터의 언어, 보유한 캐릭터가 없으면 C. 화면은 이 카드를 밝게 그린다.
	/// </summary>
	public string SelectedLanguage { get; private set; } = CharacterLanguages.C;

	/// <summary>오른쪽 아래 버튼의 글자. 선택한 언어가 보유 중이면 「들어가기」, 미보유면 「만들기」.</summary>
	public string ConfirmLabel => GetCharacterState(SelectedLanguage) == CharacterState.Owned ? EnterLabel : CreateLabel;

	/// <summary>오른쪽 아래 버튼을 누를 수 있으면 true. 응답을 기다리는 중이면 false.</summary>
	public bool CanConfirm => Page == AccountPage.Characters && !IsWaiting;

	/// <summary>그 언어 카드의 상태: 보유 중, 미보유, 준비 중.</summary>
	public CharacterState GetCharacterState(string language)
	{
		// 서버가 만들 수 없는 언어는 캐릭터 목록과 상관없이 잠긴 언어로 본다
		if (!CharacterLanguages.IsKnown(language)) return CharacterState.Locked;
		return ownedLanguages.Contains(language) ? CharacterState.Owned : CharacterState.NotOwned;
	}

	void Awake()
	{
		Current = this;
	}

	void Start()
	{
		authenticator = InvokeAuthenticator.Current;
		if (authenticator == null)
		{
			Debug.LogError("[AccountFlow] InvokeAuthenticator가 없습니다. Base 씬 NetworkManager의 Authenticator 칸에 연결했는지 확인하세요.");
			enabled = false;
			return;
		}

		authenticator.AuthResultReceived += OnAuthResult;
		authenticator.CreateCharacterResultReceived += OnCreateCharacterResult;
		authenticator.EnterGameResultReceived += OnEnterGameResult;
		authenticator.Kicked += OnKicked;
	}

	void OnDestroy()
	{
		if (Current == this) Current = null;
		if (authenticator == null) return;
		authenticator.AuthResultReceived -= OnAuthResult;
		authenticator.CreateCharacterResultReceived -= OnCreateCharacterResult;
		authenticator.EnterGameResultReceived -= OnEnterGameResult;
		authenticator.Kicked -= OnKicked;
	}

	void Update()
	{
		// Mirror의 끊김 이벤트(NetworkClient.OnDisconnectedEvent)는 NetworkManager가 접속할 때마다 덮어쓰므로, 상태를 직접 살핀다
		bool clientActive = NetworkClient.active;
		if (wasClientActive && !clientActive) OnClientStopped();
		wasClientActive = clientActive;
	}

	#region 버튼이 부르는 함수

	/// <summary>로그인 화면으로 바꾼다. 「이미 계정이 있으신가요? 로그인」 링크.</summary>
	public void ShowLogin() => ShowPage(AccountPage.Login);

	/// <summary>회원가입 화면으로 바꾼다. 「계정이 없으신가요? 회원가입」 링크.</summary>
	public void ShowSignUp() => ShowPage(AccountPage.SignUp);

	/// <summary>로그인 버튼. 서버에 접속하지 않았으면 먼저 접속한다. 결과는 Changed로 알린다 (성공하면 캐릭터 화면).</summary>
	public void Login(string loginId, string password)
	{
		if (!CanSend()) return;
		BeginWaiting();
		if (!authenticator.RequestLogin(loginId, password)) StopWaiting();
	}

	/// <summary>
	/// 가입하기 버튼. 규칙(아이디·닉네임·비밀번호)과 비밀번호 확인을 먼저 검사하고, 통과하면 서버에 보낸다.
	/// 성공하면 바로 로그인되어 캐릭터 화면으로 간다. 결과는 Changed로 알린다.
	/// </summary>
	public void SignUp(string loginId, string password, string passwordConfirm, string nickname)
	{
		if (!CanSend()) return;

		// 서버와 같은 순서로 검사한다. 화면에서 위에 있는 칸부터 알려 주기 위함
		if (ShowFailure(AccountRules.CheckLoginId(loginId))) return;
		if (ShowFailure(AccountRules.CheckNickname(nickname))) return;
		if (ShowFailure(AccountRules.CheckPassword(password))) return;
		if (password != passwordConfirm)
		{
			SetMessage(PasswordMismatchMessage, AccountField.PasswordConfirm);
			return;
		}

		BeginWaiting();
		if (!authenticator.RequestSignUp(loginId, password, nickname)) StopWaiting();
	}

	/// <summary>캐릭터 화면의 언어 카드. 그 언어를 선택한다. 준비 중인 언어는 무시한다 (화면은 그 카드를 누를 수 없게 그린다).</summary>
	public void Select(string language)
	{
		if (IsWaiting || Page != AccountPage.Characters || language == SelectedLanguage) return;
		// Shown에 없는 언어는 IsKnown도 false이므로 Locked로 함께 걸러진다
		if (GetCharacterState(language) == CharacterState.Locked) return;
		SelectedLanguage = language;
		Notify();
	}

	/// <summary>
	/// 캐릭터 화면의 오른쪽 아래 버튼. 선택한 언어가 보유 중이면 들어가고, 미보유면 만든다.
	/// 결과는 Changed로 알린다. 만들기에 성공하면 같은 언어가 보유 중이 되고, 들어가기에 성공하면 Page가 Hidden이 된다.
	/// </summary>
	public void Confirm()
	{
		if (!CanSend() || !CanConfirm) return;
		if (GetCharacterState(SelectedLanguage) == CharacterState.Owned) Enter(SelectedLanguage);
		else CreateCharacter(SelectedLanguage);
	}

	/// <summary>팝업의 확인 버튼. 팝업을 닫는다 (뒤에는 로그인 화면이 있다).</summary>
	public void ClosePopup()
	{
		if (PopupMessage == null) return;
		PopupMessage = null;
		Notify();
	}

	/// <summary>[에디터 전용] Host 버튼. 서버와 클라이언트를 함께 켠다. Skip Login In Editor가 켜져 있으면 로그인을 건너뛴다.</summary>
	public void StartHost()
	{
		if (!CanStartHost) return;
		SetMessage("", AccountField.None);
		NetworkManager.singleton.StartHost();
	}

	#endregion

	#region 결과 받기

	void OnAuthResult(AccountAuthResult result)
	{
		IsWaiting = false;
		if (!result.Success)
		{
			SetMessage(result.failure.ToMessage(), FieldOf(result.failure));
			return;
		}

		Nickname = result.nickname ?? "";
		SetOwnedLanguages(result.characters);
		SelectFirstOwned();
		Page = AccountPage.Characters;
		SetMessage("", AccountField.None);
	}

	void OnCreateCharacterResult(CreateCharacterResultMessage result)
	{
		IsWaiting = false;
		SetOwnedLanguages(result.characters);
		SetMessage(result.Success ? "" : result.failure.ToMessage(), AccountField.None);
	}

	void OnEnterGameResult(EnterGameResultMessage result)
	{
		IsWaiting = false;
		if (result.Success) Page = AccountPage.Hidden;
		SetMessage(result.Success ? "" : result.failure.ToMessage(), AccountField.None);
	}

	void OnKicked(AccountFailure reason)
	{
		kickedReason = reason;
	}

	void OnClientStopped()
	{
		bool wasLoggedIn = Page == AccountPage.Characters || Page == AccountPage.Hidden;
		bool wasConnecting = IsWaiting && !wasLoggedIn;

		IsWaiting = false;
		Nickname = "";
		ownedLanguages.Clear();

		if (wasLoggedIn)
		{
			// 캐릭터 화면이나 게임 중에 끊기면 한 줄 문장은 놓치기 쉬우므로 팝업으로 알리고 로그인 화면으로 돌아간다
			PopupMessage = kickedReason != AccountFailure.None ? kickedReason.ToMessage() : DisconnectedMessage;
			Page = AccountPage.Login;
			SetMessage("", AccountField.None);
		}
		else if (wasConnecting) SetMessage(ConnectFailedMessage, AccountField.ServerAddress);
		else SetMessage(DisconnectedMessage, AccountField.None);

		kickedReason = AccountFailure.None;
	}

	#endregion

	void ShowPage(AccountPage page)
	{
		if (IsWaiting || (page != AccountPage.Login && page != AccountPage.SignUp) || Page == AccountPage.Characters || Page == AccountPage.Hidden) return;
		Page = page;
		SetMessage("", AccountField.None);
	}

	// 화면은 Confirm만 부른다. 버튼 하나가 상태에 맞는 요청을 고르게 하기 위해 두 요청은 숨긴다
	void CreateCharacter(string language)
	{
		BeginWaiting();
		if (!authenticator.RequestCreateCharacter(language)) StopWaiting();
	}

	void Enter(string language)
	{
		BeginWaiting();
		if (!authenticator.RequestEnter(language)) StopWaiting();
	}

	// 로그인했을 때의 선택: 화면 순서에서 보유한 첫 캐릭터, 없으면 C
	void SelectFirstOwned()
	{
		SelectedLanguage = CharacterLanguages.C;
		foreach (string language in CharacterLanguages.Shown)
		{
			if (GetCharacterState(language) != CharacterState.Owned) continue;
			SelectedLanguage = language;
			return;
		}
	}

	// 응답을 기다리는 중에는 같은 요청을 두 번 보내지 않는다
	bool CanSend() => authenticator != null && !IsWaiting;

	void BeginWaiting()
	{
		IsWaiting = true;
		SetMessage("", AccountField.None);
	}

	// 요청을 보내지 못했을 때 (예: 이미 로그인함). 응답이 오지 않으므로 기다리기를 바로 끝낸다
	void StopWaiting()
	{
		IsWaiting = false;
		Notify();
	}

	// 실패면 문장과 칸을 채우고 true
	bool ShowFailure(AccountFailure failure)
	{
		if (failure == AccountFailure.None) return false;
		SetMessage(failure.ToMessage(), FieldOf(failure));
		return true;
	}

	void SetMessage(string message, AccountField field)
	{
		Message = message;
		MessageField = field;
		Notify();
	}

	void SetOwnedLanguages(CharacterSummary[] characters)
	{
		ownedLanguages.Clear();
		if (characters == null) return;
		foreach (CharacterSummary character in characters) ownedLanguages.Add(character.language);
	}

	void Notify() => Changed?.Invoke();

	// 실패 이유 → 강조할 입력칸. 로그인 실패(WrongLoginOrPassword)는 어느 칸이 틀렸는지 알리지 않는다
	static AccountField FieldOf(AccountFailure failure)
	{
		switch (failure)
		{
			case AccountFailure.InvalidLoginId:
			case AccountFailure.LoginIdTaken:
				return AccountField.LoginId;
			case AccountFailure.InvalidNickname:
			case AccountFailure.NicknameTaken:
				return AccountField.Nickname;
			case AccountFailure.InvalidPassword:
				return AccountField.Password;
			default:
				return AccountField.None;
		}
	}
}
