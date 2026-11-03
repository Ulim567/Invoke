/// <summary>
/// 로그인한 연결이 어느 계정·캐릭터인지. 서버가 로그인에 성공한 연결의 authenticationData에 넣는다. 서버에서만 쓴다.
/// <para>
/// 사용법: 서버 코드에서 <c>conn.authenticationData as AccountSession</c>으로 가져온다. 로그인하지 않은 연결이면 null이다.
/// </para>
/// </summary>
public class AccountSession
{
	/// <summary>계정 key(GUID). AccountStore에서 그 계정의 캐릭터를 찾을 때 쓴다. 임시 계정이면 DB에 없는 값이다.</summary>
	public string AccountKey { get; }

	/// <summary>로그인할 때 입력한 아이디. 로그를 남길 때 쓴다.</summary>
	public string LoginId { get; }

	/// <summary>계정의 닉네임. 그 계정의 모든 언어 캐릭터가 같이 쓴다. 로그인할 때부터 있다.</summary>
	public string Nickname { get; }

	/// <summary>
	/// 에디터의 「로그인 건너뛰기」로 들어온 임시 계정이면 true. DB에 없으므로 진행도를 저장하지 않는다.
	/// </summary>
	public bool IsTemporary { get; }

	/// <summary>들어간 캐릭터의 key. 「들어가기」 전에는 null. 다음 이슈의 진행도는 이 값으로 저장한다.</summary>
	public string CharacterKey { get; private set; }

	/// <summary>캐릭터를 골라 게임에 들어갔으면 true. 한 연결은 한 번만 들어갈 수 있다.</summary>
	public bool HasEntered => CharacterKey != null;

	/// <summary>같은 계정이 다른 곳에서 로그인해 곧 끊길 연결이면 true. 끊길 때까지 캐릭터 요청을 받지 않는다.</summary>
	public bool IsKicked { get; private set; }

	public AccountSession(string accountKey, string loginId, string nickname, bool isTemporary = false)
	{
		AccountKey = accountKey;
		LoginId = loginId;
		Nickname = nickname;
		IsTemporary = isTemporary;
	}

	/// <summary>「들어가기」에 성공했을 때 AccountServer가 부른다.</summary>
	public void Enter(string characterKey)
	{
		CharacterKey = characterKey;
	}

	/// <summary>같은 계정이 다시 로그인해 이 연결을 끊기로 했을 때 AccountServer가 부른다.</summary>
	public void MarkKicked()
	{
		IsKicked = true;
	}
}
