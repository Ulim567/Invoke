using Mirror;

/// <summary>
/// 회원가입·로그인·캐릭터 만들기가 실패한 이유. None이면 성공.
/// 네트워크로는 숫자로 보내므로, 값을 더할 때는 맨 뒤에 더하고 이미 있는 값의 순서를 바꾸지 않는다.
/// </summary>
public enum AccountFailure
{
	None,
	InvalidLoginId,
	InvalidPassword,
	InvalidNickname,
	LoginIdTaken,
	WrongLoginOrPassword,
	UnknownLanguage,
	LanguageTaken,
	NicknameTaken,
	LoggedInElsewhere,
}

/// <summary>실패 이유를 화면에 보여 줄 글자로 바꾼다.</summary>
public static class AccountFailureText
{
	public static string ToMessage(this AccountFailure failure)
	{
		switch (failure)
		{
			case AccountFailure.None: return "";
			case AccountFailure.InvalidLoginId: return $"아이디는 영문과 숫자 {AccountRules.LoginIdMinLength}~{AccountRules.LoginIdMaxLength}자입니다.";
			case AccountFailure.InvalidPassword: return $"비밀번호는 {AccountRules.PasswordMinLength}~{AccountRules.PasswordMaxLength}자입니다.";
			case AccountFailure.InvalidNickname: return $"닉네임은 한글, 영문, 숫자 {AccountRules.NicknameMinLength}~{AccountRules.NicknameMaxLength}자입니다.";
			case AccountFailure.LoginIdTaken: return "이미 있는 아이디입니다.";
			case AccountFailure.WrongLoginOrPassword: return "아이디 또는 비밀번호가 틀렸습니다.";
			case AccountFailure.UnknownLanguage: return "만들 수 없는 언어입니다.";
			case AccountFailure.LanguageTaken: return "이 언어의 캐릭터가 이미 있습니다.";
			case AccountFailure.NicknameTaken: return "이미 있는 닉네임입니다.";
			case AccountFailure.LoggedInElsewhere: return "다른 곳에서 같은 계정으로 로그인했습니다.";
			default: return failure.ToString();
		}
	}
}

/// <summary>
/// 내 화면 → 서버: 로그인 또는 회원가입 요청. InvokeAuthenticator.RequestLogin / RequestSignUp이 보낸다.
/// </summary>
public struct AccountAuthRequest : NetworkMessage
{
	public string loginId;
	public string password;
	// true면 회원가입. 회원가입에 성공하면 바로 로그인된다
	public bool isSignUp;
}

/// <summary>
/// 캐릭터 화면에 보여 줄 캐릭터 하나. 캐릭터는 계정마다 언어별로 하나이므로, 내 화면은 언어로 캐릭터를 가리킨다.
/// 캐릭터 key는 서버만 알고 내 화면에는 보내지 않는다.
/// </summary>
public struct CharacterSummary
{
	public string language;
	public string nickname;
}

/// <summary>
/// 서버 → 내 화면: 로그인·회원가입 결과. 성공하면 그 계정의 캐릭터 목록이 들어 있다.
/// </summary>
public struct AccountAuthResult : NetworkMessage
{
	public AccountFailure failure;
	public CharacterSummary[] characters;

	public bool Success => failure == AccountFailure.None;
}

/// <summary>
/// 서버 → 내 화면: 서버가 곧 이 접속을 끊는다는 알림과 그 이유. 예: 다른 곳에서 같은 계정으로 로그인함.
/// </summary>
public struct AccountKickedMessage : NetworkMessage
{
	public AccountFailure reason;
}
