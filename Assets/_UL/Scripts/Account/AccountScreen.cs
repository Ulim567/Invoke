using UnityEngine;

/// <summary>
/// 로그인·회원가입·캐릭터 화면을 OnGUI로 그리는 임시 화면. NetworkManagerHUD를 대신한다.
/// <para>
/// 상태와 동작은 모두 AccountFlow에 있고, 이 컴포넌트는 그 값대로 그리고 버튼이 눌리면 AccountFlow의 함수를 부르기만 한다.
/// 꾸민 화면(Canvas)을 새로 만들 때도 같은 방식으로 AccountFlow만 쓰면 된다.
/// </para>
/// <para>
/// 사용법: Base 씬의 NetworkManager 오브젝트에 AccountFlow와 함께 붙인다.
/// </para>
/// </summary>
public class AccountScreen : MonoBehaviour
{
	[Tooltip("화면을 그릴 영역. 왼쪽 위 모서리 위치와 너비")]
	[SerializeField] Rect area = new Rect(10, 40, 300, 9999);

	// 입력칸의 값. 입력 중인 글자는 화면이 들고, 버튼을 누를 때 AccountFlow에 넘긴다
	string loginId = "";
	string password = "";
	string passwordConfirm = "";
	string nickname = "";

	void OnGUI()
	{
		AccountFlow flow = AccountFlow.Current;
		if (flow == null) return;

		if (flow.PopupMessage != null)
		{
			DrawPopup(flow);
			return;
		}
		if (!flow.IsVisible) return;

		GUILayout.BeginArea(area);

		GUI.enabled = !flow.IsWaiting;
		if (flow.Page == AccountPage.Login) DrawLogin(flow);
		else if (flow.Page == AccountPage.SignUp) DrawSignUp(flow);
		else DrawCharacters(flow);
		GUI.enabled = true;

		if (flow.IsWaiting) GUILayout.Label("서버 응답을 기다리는 중...");
		if (flow.Message.Length > 0) GUILayout.Label(flow.Message);

		GUILayout.EndArea();
	}

	void DrawLogin(AccountFlow flow)
	{
		GUILayout.Label("> 로그인");
		DrawServerAddress(flow);
		loginId = TextField(flow, "아이디", AccountField.LoginId, loginId, AccountRules.LoginIdMaxLength);
		password = PasswordField(flow, "비밀번호", AccountField.Password, password);

		if (GUILayout.Button("로그인")) flow.Login(loginId, password);
		if (GUILayout.Button("계정이 없으신가요? 회원가입")) flow.ShowSignUp();

		// 팀원이 에디터에서 서버와 클라이언트를 한 번에 켜는 버튼. 빌드에서는 보이지 않는다
		if (flow.CanStartHost && GUILayout.Button("Host (에디터)")) flow.StartHost();
	}

	void DrawSignUp(AccountFlow flow)
	{
		GUILayout.Label("> 회원가입");
		DrawServerAddress(flow);
		loginId = TextField(flow, "아이디 · 영문·숫자 4~16자", AccountField.LoginId, loginId, AccountRules.LoginIdMaxLength);
		nickname = TextField(flow, "닉네임 · 한글·영문·숫자 2~10자", AccountField.Nickname, nickname, AccountRules.NicknameMaxLength);
		password = PasswordField(flow, "비밀번호 · 8~64자", AccountField.Password, password);
		passwordConfirm = PasswordField(flow, "비밀번호 확인", AccountField.PasswordConfirm, passwordConfirm);

		if (GUILayout.Button("가입하기")) flow.SignUp(loginId, password, passwordConfirm, nickname);
		if (GUILayout.Button("이미 계정이 있으신가요? 로그인")) flow.ShowLogin();
	}

	void DrawCharacters(AccountFlow flow)
	{
		// 로그인한 뒤에는 화면에 비밀번호를 남겨 둘 이유가 없다
		password = "";
		passwordConfirm = "";

		GUILayout.Label("> 캐릭터");
		GUILayout.Label($"닉네임: {flow.Nickname}");

		// 언어 카드: 선택한 카드는 밝게, 나머지는 어둡게, 준비 중인 카드는 누를 수 없게 (Canvas 화면에서는 컨셉 아트가 배경에 깔린다)
		bool wasEnabled = GUI.enabled;
		GUILayout.BeginHorizontal();
		foreach (string language in flow.Languages)
		{
			CharacterState state = flow.GetCharacterState(language);
			Color previous = GUI.color;
			if (language != flow.SelectedLanguage) GUI.color = new Color(1f, 1f, 1f, 0.45f);
			GUI.enabled = wasEnabled && state != CharacterState.Locked;
			if (GUILayout.Button($"{language}\n{StateLabel(state)}", GUILayout.Height(48))) flow.Select(language);
			GUI.color = previous;
		}
		GUILayout.EndHorizontal();

		GUI.enabled = wasEnabled && flow.CanConfirm;
		if (GUILayout.Button(flow.ConfirmLabel)) flow.Confirm();
		GUI.enabled = wasEnabled;
	}

	static string StateLabel(CharacterState state)
	{
		switch (state)
		{
			case CharacterState.Owned: return "보유 중";
			case CharacterState.NotOwned: return "미보유";
			default: return "준비 중";
		}
	}

	void DrawPopup(AccountFlow flow)
	{
		var rect = new Rect((Screen.width - 320) / 2f, (Screen.height - 120) / 2f, 320, 120);
		GUILayout.BeginArea(rect, GUI.skin.box);
		GUILayout.Label("! 접속이 끊겼습니다");
		GUILayout.Label(flow.PopupMessage);
		if (GUILayout.Button("확인")) flow.ClosePopup();
		GUILayout.EndArea();
	}

	void DrawServerAddress(AccountFlow flow)
	{
		if (!flow.CanEditServerAddress) return;
		flow.ServerAddress = TextField(flow, "서버 주소", AccountField.ServerAddress, flow.ServerAddress, 64);
	}

	// 안내 문장이 이 칸을 가리키면 이름표를 빨갛게 그린다 (Canvas 화면의 빨간 테두리 대신)
	static string TextField(AccountFlow flow, string label, AccountField field, string value, int maxLength)
	{
		Label(flow, label, field);
		return GUILayout.TextField(value, maxLength);
	}

	static string PasswordField(AccountFlow flow, string label, AccountField field, string value)
	{
		Label(flow, label, field);
		return GUILayout.PasswordField(value, '*', AccountRules.PasswordMaxLength);
	}

	static void Label(AccountFlow flow, string label, AccountField field)
	{
		Color previous = GUI.color;
		if (flow.MessageField == field) GUI.color = new Color(1f, 0.6f, 0.55f);
		GUILayout.Label(label);
		GUI.color = previous;
	}
}
