using UnityEngine;

/// <summary>
/// 화면 없이 InvokeAuthenticator의 로그인·회원가입·캐릭터 만들기·들어가기를 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - Base 씬의 아무 오브젝트에 붙인다. NetworkManager에 InvokeAuthenticator가 연결되어 있어야 한다.<br/>
/// - InvokeAuthenticator의 Skip Login In Editor를 끄고 Play → Host로 시작한다. 로그인하기 전이므로 플레이어는 아직 생기지 않는다.<br/>
/// - Inspector에서 값을 고치고, 이 컴포넌트의 ⋮ 메뉴에서 요청을 고른다. 결과가 Console에 나온다.<br/>
/// - 「들어가기」에 성공하면 로비에 플레이어가 생긴다.
/// </para>
/// </summary>
public class AuthenticatorTest : MonoBehaviour
{
	[SerializeField] string loginId = "tester01";
	[SerializeField] string password = "password123";
	// 회원가입할 때만 쓴다
	[SerializeField] string nickname = "BugHunter";
	[SerializeField] string language = CharacterLanguages.C;

	// 이벤트를 등록한 InvokeAuthenticator. 두 번 등록하지 않고, 사라질 때 해제하기 위해 기억한다
	InvokeAuthenticator subscribed;

	[ContextMenu("회원가입 요청")]
	void RequestSignUp()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 회원가입 요청: {loginId} ({nickname})");
		authenticator.RequestSignUp(loginId, password, nickname);
	}

	[ContextMenu("로그인 요청")]
	void RequestLogin()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 로그인 요청: {loginId}");
		authenticator.RequestLogin(loginId, password);
	}

	[ContextMenu("캐릭터 만들기 요청")]
	void RequestCreateCharacter()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 캐릭터 만들기 요청: [{language}]");
		authenticator.RequestCreateCharacter(language);
	}

	[ContextMenu("들어가기 요청")]
	void RequestEnter()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 들어가기 요청: [{language}]");
		authenticator.RequestEnter(language);
	}

	InvokeAuthenticator FindAuthenticator()
	{
		InvokeAuthenticator authenticator = InvokeAuthenticator.Current;
		if (authenticator == null)
		{
			Debug.LogWarning("[AuthenticatorTest] InvokeAuthenticator가 없습니다. Base 씬 NetworkManager의 Authenticator 칸에 연결했는지 확인하세요.");
			return null;
		}

		if (subscribed != authenticator)
		{
			Unsubscribe();
			subscribed = authenticator;
			subscribed.AuthResultReceived += OnAuthResult;
			subscribed.CreateCharacterResultReceived += OnCreateCharacterResult;
			subscribed.EnterGameResultReceived += OnEnterGameResult;
			subscribed.Kicked += OnKicked;
		}
		return authenticator;
	}

	void OnAuthResult(AccountAuthResult result)
	{
		if (result.Success) Debug.Log($"[AuthenticatorTest] 로그인 성공. 닉네임: {result.nickname}, 캐릭터: {Describe(result.characters)}");
		else Debug.LogWarning($"[AuthenticatorTest] 로그인 실패: {result.failure} ({result.failure.ToMessage()})");
	}

	void OnCreateCharacterResult(CreateCharacterResultMessage result)
	{
		if (result.Success) Debug.Log($"[AuthenticatorTest] 캐릭터 만들기 성공. 캐릭터: {Describe(result.characters)}");
		else Debug.LogWarning($"[AuthenticatorTest] 캐릭터 만들기 실패: {result.failure} ({result.failure.ToMessage()}) 캐릭터: {Describe(result.characters)}");
	}

	void OnEnterGameResult(EnterGameResultMessage result)
	{
		if (result.Success) Debug.Log("[AuthenticatorTest] 들어가기 성공");
		else Debug.LogWarning($"[AuthenticatorTest] 들어가기 실패: {result.failure} ({result.failure.ToMessage()})");
	}

	void OnKicked(AccountFailure reason)
	{
		Debug.LogWarning($"[AuthenticatorTest] 서버가 접속을 끊습니다: {reason} ({reason.ToMessage()})");
	}

	static string Describe(CharacterSummary[] characters)
	{
		if (characters == null || characters.Length == 0) return "없음";

		string text = "";
		foreach (CharacterSummary character in characters)
			text += $"[{character.language}] ";
		return text;
	}

	void Unsubscribe()
	{
		if (subscribed == null) return;
		subscribed.AuthResultReceived -= OnAuthResult;
		subscribed.CreateCharacterResultReceived -= OnCreateCharacterResult;
		subscribed.EnterGameResultReceived -= OnEnterGameResult;
		subscribed.Kicked -= OnKicked;
		subscribed = null;
	}

	void OnDestroy()
	{
		Unsubscribe();
	}
}
