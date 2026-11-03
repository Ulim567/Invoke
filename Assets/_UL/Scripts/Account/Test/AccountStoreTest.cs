using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// AccountStore(계정·캐릭터 DB)와 PasswordHasher를 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - 아무 씬의 아무 오브젝트에 붙이고 Play한다. 네트워크를 켜지 않아도 된다.<br/>
/// - Console에 항목마다 [통과]/[실패]가 나오고, 마지막에 통과 개수가 나온다.<br/>
/// - 실제 계정 DB와 섞이지 않도록 테스트 전용 파일(AccountsTest.db)을 쓰고, Play할 때마다 지우고 새로 만든다.
/// </para>
/// </summary>
public class AccountStoreTest : MonoBehaviour
{
	const string FileName = "AccountsTest.db";

	int passed;
	int total;

	void Start()
	{
		string path = Path.Combine(Application.persistentDataPath, FileName);
		if (File.Exists(path)) File.Delete(path);

		string firstKey;
		using (var store = new AccountStore(path))
		{
			// 회원가입
			Check("회원가입 성공", store.SignUp("TestUser", "password123", out firstKey) == SignUpResult.Success && firstKey != null);
			Check("대소문자만 다른 아이디는 거부", store.SignUp("testuser", "other-password", out _) == SignUpResult.LoginIdTaken);

			// 로그인
			var watch = Stopwatch.StartNew();
			bool loggedIn = store.TryLogin("TESTUSER", "password123", out string loginKey);
			watch.Stop();
			Check("맞는 비밀번호로 로그인 (아이디 대소문자 무시)", loggedIn && loginKey == firstKey);
			Debug.Log($"[AccountStoreTest] 로그인 한 번 걸린 시간: {watch.ElapsedMilliseconds}ms (반복 {PasswordHasher.DefaultIterations}회)");
			Check("틀린 비밀번호는 거부", !store.TryLogin("testuser", "wrong-password", out _));
			Check("없는 아이디는 거부", !store.TryLogin("nobody", "password123", out _));

			// 캐릭터
			Check("처음에는 캐릭터 없음", store.GetCharacters(firstKey).Count == 0);
			Check("C 캐릭터 만들기", store.CreateCharacter(firstKey, CharacterLanguages.C, "BugHunter", out CharacterRecord created) == CreateCharacterResult.Success && created != null);
			Check("같은 계정에 C 캐릭터 하나 더는 거부", store.CreateCharacter(firstKey, CharacterLanguages.C, "Another", out _) == CreateCharacterResult.LanguageTaken);
			Check("모르는 언어는 거부", store.CreateCharacter(firstKey, "Java", "JavaKing", out _) == CreateCharacterResult.UnknownLanguage);

			store.SignUp("second", "password456", out string secondKey);
			Check("다른 계정도 대소문자만 다른 닉네임은 거부", store.CreateCharacter(secondKey, CharacterLanguages.C, "bughunter", out _) == CreateCharacterResult.NicknameTaken);
			Check("다른 계정의 한글 닉네임 캐릭터 만들기", store.CreateCharacter(secondKey, CharacterLanguages.C, "버그사냥꾼", out _) == CreateCharacterResult.Success);
		}

		// DB를 닫았다가 다시 열어도 남아 있는지 (서버를 껐다 켠 경우)
		using (var store = new AccountStore(path))
		{
			Check("다시 열어도 로그인", store.TryLogin("testuser", "password123", out string key) && key == firstKey);

			var characters = store.GetCharacters(firstKey);
			Check("다시 열어도 캐릭터가 남아 있음", characters.Count == 1 && characters[0].Nickname == "BugHunter" && characters[0].Language == CharacterLanguages.C);
		}

		string summary = $"[AccountStoreTest] {passed}/{total} 통과\n파일: {path}";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	void Check(string name, bool ok)
	{
		total++;
		if (ok)
		{
			passed++;
			Debug.Log($"[AccountStoreTest] [통과] {name}");
		}
		else
		{
			Debug.LogError($"[AccountStoreTest] [실패] {name}");
		}
	}
}
