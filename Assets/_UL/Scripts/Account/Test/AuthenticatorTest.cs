using UnityEngine;

/// <summary>
/// 화면 없이 InvokeAuthenticator의 로그인·회원가입을 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - Base 씬의 아무 오브젝트에 붙인다. NetworkManager에 InvokeAuthenticator가 연결되어 있어야 한다.<br/>
/// - Play → Host로 시작한다. 로그인하기 전이므로 플레이어는 아직 생기지 않는다.<br/>
/// - Inspector에서 아이디·비밀번호를 고치고, 이 컴포넌트의 ⋮ 메뉴에서 「회원가입 요청」 또는 「로그인 요청」을 고른다.<br/>
/// - 결과가 Console에 나온다. 성공하면 로비에 플레이어가 생긴다.
/// </para>
/// </summary>
public class AuthenticatorTest : MonoBehaviour
{
	[SerializeField] string loginId = "tester01";
	[SerializeField] string password = "password123";

	// 이벤트를 등록한 InvokeAuthenticator. 두 번 등록하지 않고, 사라질 때 해제하기 위해 기억한다
	InvokeAuthenticator subscribed;

	[ContextMenu("회원가입 요청")]
	void RequestSignUp()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 회원가입 요청: {loginId}");
		authenticator.RequestSignUp(loginId, password);
	}

	[ContextMenu("로그인 요청")]
	void RequestLogin()
	{
		InvokeAuthenticator authenticator = FindAuthenticator();
		if (authenticator == null) return;

		Debug.Log($"[AuthenticatorTest] 로그인 요청: {loginId}");
		authenticator.RequestLogin(loginId, password);
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
			subscribed.Kicked += OnKicked;
		}
		return authenticator;
	}

	void OnAuthResult(AccountAuthResult result)
	{
		if (!result.Success)
		{
			Debug.LogWarning($"[AuthenticatorTest] 실패: {result.failure} ({result.failure.ToMessage()})");
			return;
		}

		string characters = result.characters.Length == 0 ? "없음" : "";
		foreach (CharacterSummary character in result.characters)
			characters += $"[{character.language}] {character.nickname} ";
		Debug.Log($"[AuthenticatorTest] 성공. 캐릭터: {characters}");
	}

	void OnKicked(AccountFailure reason)
	{
		Debug.LogWarning($"[AuthenticatorTest] 서버가 접속을 끊습니다: {reason} ({reason.ToMessage()})");
	}

	void Unsubscribe()
	{
		if (subscribed == null) return;
		subscribed.AuthResultReceived -= OnAuthResult;
		subscribed.Kicked -= OnKicked;
		subscribed = null;
	}

	void OnDestroy()
	{
		Unsubscribe();
	}
}
