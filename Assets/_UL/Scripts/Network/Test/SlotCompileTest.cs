using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버의 빌드(SlotCompileChecker, FunctionExecutorNetwork.IsBuildSucceeded)와 CompileError 시전 거부(#506)를 확인하는 테스트. 에디터 호스트 하나로 돌린다.
/// <para>
/// 사용법<br/>
/// - Base 씬에서 Play → eval로 <c>SlotCompileTest.StartInEditor()</c>. 서버가 꺼져 있으면 호스트(임시 계정)로 켠다.<br/>
/// - 씬에 놓지 않는다. 끝나면 이 컴퓨터에 저장한 슬롯·함수·전역 변수로 서버의 데이터를 되돌린다.
/// </para>
/// <para>
/// 서버 없이: 닿는 함수 찾기(재귀, 없는 함수), 슬롯이 부르는 함수의 오류, 부르지 않는 함수의 오류.<br/>
/// 호스트: 사냥터로 이동 → 슬롯 데이터를 넣고 빌드 결과 확인 → 빌드 성공이면 시전, 슬롯 하나라도 오류면 모든 슬롯 CompileError 거부 → 전역 변수 오류도 빌드 실패.<br/>
/// 「[SlotCompileTest] 전체 통과」면 통과.
/// </para>
/// </summary>
public class SlotCompileTest : MonoBehaviour
{
	const float TimeoutSeconds = 30f;
	static readonly VariableType Int = new VariableType(VariableTypeKind.Int);

	int failedCount;

	/// <summary>에디터 Play 모드에서 eval로 부른다.</summary>
	public static void StartInEditor()
	{
		var go = new GameObject(nameof(SlotCompileTest));
		DontDestroyOnLoad(go);
		go.AddComponent<SlotCompileTest>();
	}

	IEnumerator Start()
	{
		TestChecker();

		if (!NetworkServer.active) NetworkManager.singleton.StartHost();
		yield return Wait("호스트로 게임에 들어감", () => NetworkClient.localPlayer != null && HostScene() == PlaceScenes.Lobby);
		yield return Wait("함수 데이터 준비", () => NetworkClient.localPlayer.GetComponent<FunctionExecutorNetwork>().IsDataReady);
		// PlaceTravelClient는 접속 직후 로딩을 끝낸 뒤에야 이동 요청을 받는다
		yield return new WaitForSeconds(1f);
		// 로비는 공격할 수 없는 장소라 CompileError보다 먼저 InNoCastPlace로 거부되므로 사냥터에서 시전한다
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("사냥터에 도착", () => HostScene() == PlaceScenes.HuntingGround);
		yield return new WaitForSeconds(1.5f);

		yield return TestServer(NetworkClient.localPlayer.GetComponent<FunctionExecutorNetwork>());

		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
	}

	// 서버 없이 SlotCompileChecker만 본다
	void TestChecker()
	{
		FunctionData good = Good("good");
		FunctionData broken = Broken("broken");

		FunctionData callsBroken = Good("calls_broken");
		callsBroken.Blocks.Add(BlockData.CallUserFunction(broken.Guid));
		var owned = new List<FunctionData> { good, broken, callsBroken };
		Check("오류 없는 함수는 컴파일됨", SlotCompileChecker.Compiles(good, owned, null));
		Check("오류 있는 함수를 부르면 컴파일 실패", !SlotCompileChecker.Compiles(callsBroken, owned, null));
		Check("부르지 않는 함수의 오류는 상관없음", SlotCompileChecker.Compiles(good, owned, null));

		FunctionData callsMissing = Good("calls_missing");
		callsMissing.Blocks.Add(BlockData.CallUserFunction(Guid.NewGuid().ToString()));
		owned.Add(callsMissing);
		Check("없는 함수를 부르면 컴파일 실패", !SlotCompileChecker.Compiles(callsMissing, owned, null));

		// 서로 부르는 f → g → f, f → g → good
		FunctionData f = Good("f");
		FunctionData g = Good("g");
		f.Blocks.Add(BlockData.CallUserFunction(g.Guid));
		g.Blocks.Add(BlockData.CallUserFunction(f.Guid));
		g.Blocks.Add(BlockData.CallUserFunction(good.Guid));
		owned.Add(f);
		owned.Add(g);
		List<FunctionData> reachable = SlotCompileChecker.Reachable(f, owned);
		Check("서로 부르는 함수도 끝나고, 닿는 함수는 f, g, good 3개", reachable.Count == 3 && reachable[0] == f, $"{reachable.Count}개");
		Check("서로 부르는 함수는 컴파일됨", SlotCompileChecker.Compiles(f, owned, null));

		// 실패 경로 (#506): h → calls_broken → page 오류 함수 broken
		FunctionData h = Good("h");
		h.Blocks.Add(BlockData.CallUserFunction(callsBroken.Guid));
		owned.Add(h);
		List<List<FunctionData>> failed = SlotCompileChecker.FailedPaths(h, owned, null);
		string described = string.Join(" | ", failed.ConvertAll(path => string.Join(" → ", path.ConvertAll(x => x.Name))));
		Check("실패 경로는 h → calls_broken → broken 하나", described == "h → calls_broken → broken", described);
		Check("오류 없는 함수(서로 부르는 f)는 실패 경로 없음", SlotCompileChecker.FailedPaths(f, owned, null).Count == 0);
	}

	IEnumerator TestServer(FunctionExecutorNetwork net)
	{
		FunctionData good = Good("slot_good");
		FunctionData broken = Broken("page_broken");
		FunctionData callsBroken = Good("slot_calls_broken");
		callsBroken.Blocks.Add(BlockData.CallUserFunction(broken.Guid));
		FunctionData callsMissing = Good("slot_calls_missing");
		callsMissing.Blocks.Add(BlockData.CallUserFunction(Guid.NewGuid().ToString()));
		var functions = new List<FunctionData> { good, broken, callsBroken, callsMissing };

		int changed = 0;
		net.BuildResultChanged += () => changed++;
		var rejected = new List<(int Slot, CastRejection Reason)>();
		var accepted = new List<int>();
		net.CastRejected += (slot, reason) => rejected.Add((slot, reason));
		net.CastAccepted += (slot, task) => accepted.Add(slot);

		// 1. 슬롯 1만 오류 없는 함수. 슬롯이 부르지 않는 페이지 함수(page_broken)의 오류는 빌드에 들어가지 않는다
		net.ApplyPlayerData(Slots(good), functions, new GlobalVariablesData());
		yield return null;
		Check("슬롯이 부르지 않는 페이지 함수의 오류는 상관없이 빌드 성공", net.IsBuildSucceeded);
		accepted.Clear();
		net.RequestCast(0);
		yield return Wait("슬롯 1 시전 요청의 답", () => accepted.Contains(0) || rejected.Exists(r => r.Slot == 0));
		Check("빌드 성공이면 슬롯 1은 시전됨", accepted.Contains(0), Describe(rejected));
		yield return new WaitForSeconds(1.5f); // 슬롯 1의 wait(1)이 끝나 쿨타임 거부와 섞이지 않게

		// 2. 슬롯 2가 오류 있는 함수를 부르면 빌드 실패. 오류 없는 슬롯 1도 시전되지 않는다 (사용자 결정 (나))
		changed = 0;
		net.ApplyPlayerData(Slots(good, callsBroken), functions, null);
		yield return null;
		Check("슬롯 하나가 오류 있는 함수를 부르면 빌드 실패", !net.IsBuildSucceeded);
		// 빌드 결과가 성공에서 실패로 바뀌었으므로 불려야 한다 (같은 값이면 Mirror가 hook을 부르지 않음)
		Check("빌드 결과가 바뀌면 호스트에서도 BuildResultChanged가 불림", changed > 0, $"{changed}번");
		rejected.Clear();
		net.RequestCast(0);
		net.RequestCast(1);
		yield return Wait("슬롯 1, 2 시전 요청의 답", () => rejected.Count >= 2);
		Check("빌드 실패면 슬롯 1, 2 모두 CompileError로 거부", rejected.Count == 2 && rejected.TrueForAll(r => r.Reason == CastRejection.CompileError), Describe(rejected));

		// 3. 없는 함수를 불러도 빌드 실패
		net.ApplyPlayerData(Slots(good, callsMissing), functions, null);
		yield return null;
		Check("슬롯 하나가 없는 함수를 부르면 빌드 실패", !net.IsBuildSucceeded);

		Check("HUD 문장", FailureNotice.ForRejection(CastRejection.CompileError) == "[Cast Rejected] CompileError\n컴파일 오류가 있는 함수");

		// 4. 전역 변수 파일에 오류(같은 이름 두 번)가 있으면 빌드 실패
		var brokenGlobals = new GlobalVariablesData();
		brokenGlobals.Declarations.Add(BlockData.Declare(Int, "g"));
		brokenGlobals.Declarations.Add(BlockData.Declare(Int, "g"));
		net.ApplyPlayerData(Slots(good), functions, brokenGlobals);
		yield return null;
		Check("전역 변수 오류면 빌드 실패", !net.IsBuildSucceeded);
		rejected.Clear();
		net.RequestCast(0);
		yield return Wait("전역 변수 오류일 때 슬롯 1 시전 요청의 답", () => rejected.Exists(r => r.Slot == 0) || accepted.Count > 1);
		Check("전역 변수 오류면 슬롯 1도 CompileError로 거부", rejected.Exists(r => r.Slot == 0 && r.Reason == CastRejection.CompileError), Describe(rejected));

		// 5. 전역 변수 파일만 고쳐 다시 받으면(함수는 그대로) 빌드 성공. 전역 변수가 오지 않을 때(null)는 마지막에 받은 파일로 빌드한다
		net.ApplyPlayerData(Slots(good), functions, new GlobalVariablesData());
		net.ApplyPlayerData(Slots(good), functions, null);
		yield return null;
		Check("전역 변수를 고치면 다시 빌드 성공 (전역 변수 null로 다시 받아도 유지)", net.IsBuildSucceeded);

		// 이 컴퓨터에 저장한 데이터로 되돌린다 (임시 계정이 접속할 때 보내는 것과 같은 데이터)
		SlotLoadout loadout = net.GetComponent<SlotLoadout>();
		var original = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < original.Length; i++) original[i] = loadout.GetEntry(i);
		net.ApplyPlayerData(original, FunctionStorage.LoadAll(), GlobalVariablesStorage.Load());
	}

	// 앞에서부터 functions를 슬롯 1, 2, …에 넣고 나머지는 비운 슬롯 8칸
	static SlotLoadout.Entry[] Slots(params FunctionData[] functions)
	{
		var slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++)
			slots[i] = i < functions.Length ? SlotLoadout.Entry.Function(functions[i].Guid) : SlotLoadout.Entry.Empty;
		return slots;
	}

	// 오류 없는 함수: wait(1);
	static FunctionData Good(string name)
	{
		FunctionData function = FunctionData.Create(name);
		function.Blocks.Add(BlockData.Wait(SlotData.FromLiteral(Int, "1")));
		return function;
	}

	// 선언하지 않은 변수에 대입하는 함수: x = 1;
	static FunctionData Broken(string name)
	{
		FunctionData function = FunctionData.Create(name);
		function.Blocks.Add(BlockData.Assign("x", SlotData.FromLiteral(Int, "1")));
		return function;
	}

	static string HostScene() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject.scene.name : null;

	static string Describe(List<(int Slot, CastRejection Reason)> rejected) =>
		rejected.Count > 0 ? string.Join(",", rejected.ConvertAll(r => $"슬롯 {r.Slot + 1} {r.Reason}")) : "거부 없음";

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (!condition()) Fail($"{label}: 시간 초과");
	}

	void Check(string label, bool ok, string detail = null)
	{
		if (ok) Log($"통과: {label}");
		else Fail($"{label}{(detail != null ? $" ({detail})" : "")}");
	}

	void Fail(string message)
	{
		failedCount++;
		Debug.LogError($"[SlotCompileTest] 실패: {message}");
	}

	static void Log(string message) => Debug.Log($"[SlotCompileTest] {message}");
}
