/// <summary>
/// 로그인한 연결이 어느 계정인지. 서버가 로그인에 성공한 연결의 authenticationData에 넣는다. 서버에서만 쓴다.
/// <para>
/// 사용법: 서버 코드에서 <c>conn.authenticationData as AccountSession</c>으로 가져온다. 로그인하지 않은 연결이면 null이다.
/// </para>
/// </summary>
public class AccountSession
{
	/// <summary>계정 key(GUID). AccountStore에서 그 계정의 캐릭터를 찾을 때 쓴다.</summary>
	public string AccountKey { get; }

	/// <summary>로그인할 때 입력한 아이디. 로그를 남길 때 쓴다.</summary>
	public string LoginId { get; }

	public AccountSession(string accountKey, string loginId)
	{
		AccountKey = accountKey;
		LoginId = loginId;
	}
}
