using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

/// <summary>
/// 실제 계정의 페이지 함수 지우기(FunctionExecutorNetwork.RequestDeleteFunction, #494)를 확인하는 테스트.
/// <para>
/// 사용법<br/>
/// - Base 씬에서 Play → eval로 <c>FunctionDeleteTest.StartInEditor()</c>. 호스트가 아직 켜지지 않았어야 한다.<br/>
/// - 에디터의 「로그인 건너뛰기」(Skip Login In Editor)를 이 Play 동안만 끄고, 새 계정으로 가입해 C 캐릭터로 들어간다.
///   에디터의 계정 DB(Accounts.db)에 시험 계정이 하나 남는다.<br/>
/// - 씬에 놓지 않는다.
/// </para>
/// <para>
/// 페이지 함수를 저장 → 슬롯 함수 지우기는 SlotFunctionLocked → 페이지 함수 지우기는 Saved이고 ReceivedFunctions와 서버 DB에서 빠짐
/// → 같은 함수를 다시 지우면 UnknownFunction. 「[FunctionDeleteTest] 전체 통과」면 통과.
/// </para>
/// </summary>
public class FunctionDeleteTest : MonoBehaviour
{
	const float TimeoutSeconds = 20f;

	int failedCount;
	PlayerDataRequestResult? lastAnswer;

	/// <summary>에디터 Play 모드에서 eval로 부른다.</summary>
	public static void StartInEditor()
	{
		var go = new GameObject(nameof(FunctionDeleteTest));
		DontDestroyOnLoad(go);
		go.AddComponent<FunctionDeleteTest>();
	}

	IEnumerator Start()
	{
		yield return Run();
		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
	}

	IEnumerator Run()
	{
		InvokeAuthenticator authenticator = InvokeAuthenticator.Current;
		if (authenticator == null || NetworkServer.active)
		{
			Fail("Base 씬에서, 호스트를 켜기 전에 시작해야 함");
			yield break;
		}

		// 이 Play 동안만 로그인 건너뛰기를 끈다. 켜 두면 임시 계정이 되어 서버 DB를 쓰지 않기 때문
		typeof(InvokeAuthenticator).GetField("skipLoginInEditor", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(authenticator, false);
		NetworkManager.singleton.StartHost();
		yield return Wait("호스트 접속", () => NetworkClient.isConnected);

		// 닉네임으로도 쓰므로 10자 안 (AccountRules.NicknameMaxLength)
		string id = "d" + DateTime.Now.ToString("ddHHmmss");
		authenticator.RequestSignUp(id, "password123", id);
		yield return Wait($"가입·로그인 ({id})", () => NetworkClient.connection != null && NetworkClient.connection.isAuthenticated);

		bool created = false;
		authenticator.CreateCharacterResultReceived += _ => created = true;
		authenticator.RequestCreateCharacter(CharacterLanguages.C);
		yield return Wait("C 캐릭터 만들기", () => created);
		authenticator.RequestEnter(CharacterLanguages.C);

		FunctionExecutorNetwork net = null;
		yield return Wait("실제 계정으로 들어가 서버 데이터를 받음", () =>
			NetworkClient.localPlayer != null && (net = NetworkClient.localPlayer.GetComponent<FunctionExecutorNetwork>()) != null
			&& net.UsesServerData && net.ReceivedSlots != null && net.ReceivedFunctions != null);
		if (net == null) yield break;
		net.PlayerDataRequestAnswered += (request, result) => lastAnswer = result;

		// 페이지 함수 하나를 저장한다
		FunctionData page = FunctionData.Create("delete_me");
		yield return Answer("페이지 함수 저장", () => net.RequestSaveFunction(page), PlayerDataRequestResult.Saved);
		Check("저장한 페이지 함수가 ReceivedFunctions에 있음", net.ReceivedFunctions.Any(f => f.Guid == page.Guid));
		int countBefore = net.ReceivedFunctions.Count;

		// 슬롯 함수는 지울 수 없다
		string slot1 = net.ReceivedSlots[0].Id;
		yield return Answer("슬롯 함수 지우기는 거부", () => net.RequestDeleteFunction(slot1), PlayerDataRequestResult.SlotFunctionLocked);
		Check("슬롯 함수는 그대로", net.ReceivedFunctions.Any(f => f.Guid == slot1));

		// 페이지 함수 지우기
		yield return Answer("페이지 함수 지우기", () => net.RequestDeleteFunction(page.Guid), PlayerDataRequestResult.Saved);
		Check("지운 함수가 ReceivedFunctions에서 빠짐", net.ReceivedFunctions.All(f => f.Guid != page.Guid) && net.ReceivedFunctions.Count == countBefore - 1);
#if UNITY_SERVER || UNITY_EDITOR
		// 호스트라 서버의 컴포넌트가 같은 오브젝트다. 서버 DB에서도 지워졌는지 본다
		var store = (ProgressStore)typeof(FunctionExecutorNetwork).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(net);
		var key = (string)typeof(FunctionExecutorNetwork).GetField("characterKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(net);
		Check("서버 DB에서도 지워짐", store != null && !store.HasFunction(key, page.Guid) && store.CountPageFunctions(key) == 0);
#endif

		// 없는 함수
		yield return Answer("이미 지운 함수 지우기는 거부", () => net.RequestDeleteFunction(page.Guid), PlayerDataRequestResult.UnknownFunction);
	}

	// 요청을 보내고 결과를 기다려 기대한 결과인지 본다
	IEnumerator Answer(string label, Action send, PlayerDataRequestResult expected)
	{
		lastAnswer = null;
		send();
		yield return Wait($"{label}: 답", () => lastAnswer != null);
		Check($"{label}: {lastAnswer} (기대 {expected})", lastAnswer == expected);
	}

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (condition()) Pass(label);
		else Fail($"{label}: 시간 초과");
	}

	void Check(string step, bool ok)
	{
		if (ok) Pass(step);
		else Fail(step);
	}

	void Pass(string step) => Log($"통과: {step}");

	void Fail(string step)
	{
		failedCount++;
		Debug.LogError($"[FunctionDeleteTest] 실패: {step}");
	}

	static void Log(string message) => Debug.Log($"[FunctionDeleteTest] {message}");
}
