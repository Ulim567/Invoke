using System.Text.RegularExpressions;

/// <summary>
/// 아이디, 비밀번호, 닉네임의 입력 규칙. 규칙에 맞으면 AccountFailure.None, 아니면 실패 이유를 돌려준다.
/// <para>
/// 서버는 AccountStore를 부르기 전에 반드시 이 검사를 한다.
/// 화면도 같은 함수로 미리 검사해서, 서버에 보내기 전에 이유를 보여 줄 수 있다.
/// </para>
/// </summary>
public static class AccountRules
{
	public const int LoginIdMinLength = 4;
	public const int LoginIdMaxLength = 16;
	public const int PasswordMinLength = 8;
	// 아주 긴 비밀번호는 서버가 해시를 계산하는 시간을 늘리므로 상한을 둔다
	public const int PasswordMaxLength = 64;
	public const int NicknameMinLength = 2;
	public const int NicknameMaxLength = 10;

	// \A, \z: 문자열의 처음과 끝. $는 끝에 붙은 줄바꿈 앞에서도 맞다고 보므로 쓰지 않는다. 쓰면 끝에 줄바꿈을 붙여 보이는 모양이 같은 닉네임을 만들 수 있다
	static readonly Regex LoginIdPattern = new Regex(@"\A[A-Za-z0-9]+\z");
	// 가-힣: 완성형 한글 (가~힣)
	static readonly Regex NicknamePattern = new Regex(@"\A[가-힣A-Za-z0-9]+\z");

	/// <summary>아이디: 영문과 숫자 4~16자. 대소문자는 AccountStore가 구분하지 않는다.</summary>
	public static AccountFailure CheckLoginId(string loginId)
	{
		if (!IsLengthIn(loginId, LoginIdMinLength, LoginIdMaxLength) || !LoginIdPattern.IsMatch(loginId))
			return AccountFailure.InvalidLoginId;
		return AccountFailure.None;
	}

	/// <summary>비밀번호: 8~64자. 글자 종류는 제한하지 않는다.</summary>
	public static AccountFailure CheckPassword(string password)
	{
		if (!IsLengthIn(password, PasswordMinLength, PasswordMaxLength))
			return AccountFailure.InvalidPassword;
		return AccountFailure.None;
	}

	/// <summary>닉네임: 한글, 영문, 숫자 2~10자.</summary>
	public static AccountFailure CheckNickname(string nickname)
	{
		if (!IsLengthIn(nickname, NicknameMinLength, NicknameMaxLength) || !NicknamePattern.IsMatch(nickname))
			return AccountFailure.InvalidNickname;
		return AccountFailure.None;
	}

	static bool IsLengthIn(string text, int min, int max)
	{
		return text != null && text.Length >= min && text.Length <= max;
	}
}
