// ProgressStore가 서버·에디터에서만 컴파일되므로, 이 시험도 같은 조건에서만 컴파일한다 (Windows 클라이언트 빌드 실패 방지)
#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

/// <summary>
/// 실제 계정의 한꺼번에 저장(FunctionExecutorNetwork.RequestSaveAll, #506)을 확인하는 테스트.
/// <para>
/// 사용법<br/>
/// - Base 씬에서 Play → eval로 <c>SaveAllTest.StartInEditor()</c>. 호스트가 아직 켜지지 않았어야 한다.<br/>
/// - 에디터의 「로그인 건너뛰기」(Skip Login In Editor)를 이 Play 동안만 끄고, 새 계정으로 가입해 C 캐릭터로 들어간다.
///   에디터의 계정 DB(Accounts.db)에 시험 계정이 하나 남는다.<br/>
/// - 씬에 놓지 않는다.
/// </para>
/// <para>
/// 컴파일 오류가 있어도 함수·전역 변수가 함께 저장됨 → 고치면 빌드 성공 → 하나라도 거부되면 아무것도 저장되지 않음(슬롯 함수 틀, 페이지 함수 수, 개수)
/// → 메시지 크기 한도를 넘으면 보내기 전에 TooLarge → 예전 요청(함수 하나, 전역 변수)도 같은 규칙. 「[SaveAllTest] 전체 통과」면 통과.
/// </para>
/// </summary>
public class SaveAllTest : MonoBehaviour
{
	const float TimeoutSeconds = 20f;
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	int failedCount;
	PlayerDataRequest? lastRequest;
	PlayerDataRequestResult? lastAnswer;
	int receivedCount;

	FunctionExecutorNetwork net;
	ProgressStore store;
	string key;

	/// <summary>에디터 Play 모드에서 eval로 부른다.</summary>
	public static void StartInEditor()
	{
		var go = new GameObject(nameof(SaveAllTest));
		DontDestroyOnLoad(go);
		go.AddComponent<SaveAllTest>();
	}

	IEnumerator Start()
	{
		yield return Enter();
		if (net != null) yield return Run();
		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
	}

	IEnumerator Enter()
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
		string id = "s" + DateTime.Now.ToString("ddHHmmss");
		authenticator.RequestSignUp(id, "password123", id);
		yield return Wait($"가입·로그인 ({id})", () => NetworkClient.connection != null && NetworkClient.connection.isAuthenticated);

		bool created = false;
		authenticator.CreateCharacterResultReceived += _ => created = true;
		authenticator.RequestCreateCharacter(CharacterLanguages.C);
		yield return Wait("C 캐릭터 만들기", () => created);
		authenticator.RequestEnter(CharacterLanguages.C);

		FunctionExecutorNetwork found = null;
		yield return Wait("실제 계정으로 들어가 서버 데이터를 받음", () =>
			NetworkClient.localPlayer != null && (found = NetworkClient.localPlayer.GetComponent<FunctionExecutorNetwork>()) != null
			&& found.UsesServerData && found.ReceivedSlots != null && found.ReceivedFunctions != null);
		net = found;
		if (net == null) yield break;
		net.PlayerDataRequestAnswered += (request, result) => { lastRequest = request; lastAnswer = result; };
		net.PlayerDataReceived += () => receivedCount++;

		// 호스트라 서버의 컴포넌트가 같은 오브젝트다. 서버 DB에 쓰였는지 직접 본다
		store = (ProgressStore)typeof(FunctionExecutorNetwork).GetField("store", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(net);
		key = (string)typeof(FunctionExecutorNetwork).GetField("characterKey", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(net);

		// 전역 변수 선언 블록을 쓸 수 있게 개수를 준다. 새 캐릭터는 처음 슬롯 함수가 쓰는 개수만 가져서, 선언을 저장하면 CountExceeded가 나기 때문
		net.GetComponent<PlayerProgress>().AddItem(BlockUsageCounter.KeyOf(BlockData.Declare(Int, "g")), 5);
	}

	IEnumerator Run()
	{
		FunctionData slot1 = SlotCopy(0), slot2 = SlotCopy(1), slot3 = SlotCopy(2), slot4 = SlotCopy(3), slot5 = SlotCopy(4);

		// 1. 컴파일 오류가 있어도 함수 둘과 전역 변수가 함께 저장된다
		slot2.Blocks.Clear();
		slot2.Blocks.Add(CallMissing());
		FunctionData page1 = FunctionData.Create("page_one");
		GlobalVariablesData brokenGlobals = Globals("g", "g");
		receivedCount = 0;
		yield return Answer("오류 있는 슬롯 함수 + 새 페이지 + 오류 있는 전역 변수 저장",
			() => net.RequestSaveAll(new[] { slot2, page1 }, brokenGlobals), PlayerDataRequest.SaveAll, PlayerDataRequestResult.Saved);
		Check("PlayerDataReceived가 한 번 불림", receivedCount == 1, $"{receivedCount}번");
		Check("ReceivedFunctions에 새 페이지와 고친 슬롯 함수", Has(net.ReceivedFunctions, page1) && Same(Find(net.ReceivedFunctions, slot2.Guid), slot2));
		Check("ReceivedGlobals가 새 전역 변수", net.ReceivedGlobals != null && net.ReceivedGlobals.Declarations.Count == 2);
		CharacterProgressData saved = store.Load(key);
		Check("서버 DB에 새 페이지, 고친 슬롯 함수, 전역 변수가 쓰임",
			Has(saved.Functions, page1) && Same(Find(saved.Functions, slot2.Guid), slot2) && saved.Globals.Declarations.Count == 2);
		Check("전역 변수 오류라 빌드 실패", !net.IsBuildSucceeded);

		// 2. 고쳐서 저장하면 슬롯이 다시 컴파일된다
		slot2.Blocks.Clear();
		yield return Answer("슬롯 2와 전역 변수를 고쳐 저장",
			() => net.RequestSaveAll(new[] { slot2 }, Globals("g")), PlayerDataRequest.SaveAll, PlayerDataRequestResult.Saved);
		Check("고친 뒤 빌드 성공", net.IsBuildSucceeded);

		// 3. 하나라도 거부되면 아무것도 저장되지 않는다: 슬롯 함수의 이름을 바꿈
		FunctionData slot3Before = Find(net.ReceivedFunctions, slot3.Guid);
		slot3.Blocks.Add(CallMissing());
		slot4.Name = "renamed";
		yield return Answer("슬롯 4의 이름을 바꾸면 거부",
			() => net.RequestSaveAll(new[] { slot3, slot4 }, null), PlayerDataRequest.SaveAll, PlayerDataRequestResult.SlotFunctionLocked);
		Check("함께 보낸 슬롯 3도 저장되지 않음 (받은 데이터, 서버 DB)",
			Same(Find(net.ReceivedFunctions, slot3.Guid), slot3Before) && Same(Find(store.Load(key).Functions, slot3.Guid), slot3Before));

		// 4. 페이지 함수 수: 이미 1개 + 새로 5개 = 6개 > 5
		var pages = Enumerable.Range(0, ProgressRules.MaxPageFunctions).Select(i => FunctionData.Create("many_" + i)).ToArray();
		yield return Answer("새 페이지 5개를 더하면 6개라 거부",
			() => net.RequestSaveAll(pages, null), PlayerDataRequest.SaveAll, PlayerDataRequestResult.TooManyFunctions);
		Check("새 페이지가 하나도 저장되지 않음", store.CountPageFunctions(key) == 1 && pages.All(p => !Has(net.ReceivedFunctions, p)));

		// 5. 개수: 가진 것보다 많은 thrust
		slot5.Blocks.Clear();
		for (int i = 0; i < 5; i++) slot5.Blocks.Add(BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust, SlotData.EmptyValue()));
		FunctionData page2 = FunctionData.Create("page_two");
		yield return Answer("thrust 5개를 쓰면 거부",
			() => net.RequestSaveAll(new[] { slot5, page2 }, null), PlayerDataRequest.SaveAll, PlayerDataRequestResult.CountExceeded);
		Check("함께 보낸 새 페이지도 저장되지 않음", !Has(net.ReceivedFunctions, page2) && !store.HasFunction(key, page2.Guid));

		// 6. 메시지 크기 한도: 함수 하나가 문자열 한도(약 64KB)를 넘으면 보내지 않고 바로 답한다
		FunctionData huge = FunctionData.Create("huge");
		for (int i = 0; i < 2000; i++) huge.Blocks.Add(CallMissing());
		lastAnswer = null;
		net.RequestSaveAll(new[] { huge }, null);
		Check($"너무 큰 저장은 보내기 전에 TooLarge ({lastAnswer})", lastRequest == PlayerDataRequest.SaveAll && lastAnswer == PlayerDataRequestResult.TooLarge);
		Check("너무 큰 함수는 저장되지 않음", !store.HasFunction(key, huge.Guid));

		// 7. 예전 요청도 같은 규칙: 전역 변수는 오류가 있어도 저장된다
		FunctionData page3 = FunctionData.Create("page_three");
		yield return Answer("RequestSaveFunction으로 새 페이지", () => net.RequestSaveFunction(page3), PlayerDataRequest.SaveFunction, PlayerDataRequestResult.Saved);
		Check("새 페이지가 저장됨", Has(net.ReceivedFunctions, page3) && store.HasFunction(key, page3.Guid));
		yield return Answer("RequestSaveGlobals로 오류 있는 전역 변수", () => net.RequestSaveGlobals(Globals("h", "h")), PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.Saved);
		Check("전역 변수 오류라 빌드 실패 (RequestSaveGlobals)", !net.IsBuildSucceeded);
		yield return Answer("RequestSaveGlobals로 고침", () => net.RequestSaveGlobals(new GlobalVariablesData()), PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.Saved);
		Check("고친 뒤 빌드 성공 (RequestSaveGlobals)", net.IsBuildSucceeded);
	}

	// 서버가 보낸 slot번 슬롯 함수의 복사본. 받은 데이터를 바꾸지 않기 위함
	FunctionData SlotCopy(int slot)
	{
		FunctionData original = Find(net.ReceivedFunctions, net.ReceivedSlots[slot].Id);
		FunctionDataJson.TryFromJson(FunctionDataJson.ToJson(original), out FunctionData copy);
		return copy;
	}

	// 없는 함수를 부르는 블록: 컴파일 오류(선언하지 않은 함수). 함수 블록은 처음 캐릭터도 쓸 수 있다
	static BlockData CallMissing() => BlockData.CallUserFunction(Guid.NewGuid().ToString());

	static GlobalVariablesData Globals(params string[] names)
	{
		var globals = new GlobalVariablesData();
		foreach (string name in names) globals.Declarations.Add(BlockData.Declare(Int, name));
		return globals;
	}

	static FunctionData Find(IEnumerable<FunctionData> functions, string guid) => functions.FirstOrDefault(f => f.Guid == guid);
	static bool Has(IEnumerable<FunctionData> functions, FunctionData function) => functions.Any(f => f.Guid == function.Guid);
	static bool Same(FunctionData a, FunctionData b) => a != null && b != null && FunctionDataJson.ToJson(a) == FunctionDataJson.ToJson(b);

	// 요청을 보내고 결과를 기다려 기대한 결과인지 본다
	IEnumerator Answer(string label, Action send, PlayerDataRequest request, PlayerDataRequestResult expected)
	{
		lastRequest = null;
		lastAnswer = null;
		send();
		yield return Wait($"{label}: 답", () => lastAnswer != null);
		Check($"{label}: {lastRequest} {lastAnswer} (기대 {request} {expected})", lastRequest == request && lastAnswer == expected);
	}

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (!condition()) Fail($"{label}: 시간 초과");
	}

	void Check(string step, bool ok, string detail = null)
	{
		if (ok) Log($"통과: {step}");
		else Fail($"{step}{(detail != null ? $" ({detail})" : "")}");
	}

	void Fail(string step)
	{
		failedCount++;
		Debug.LogError($"[SaveAllTest] 실패: {step}");
	}

	static void Log(string message) => Debug.Log($"[SaveAllTest] {message}");
}
#endif
