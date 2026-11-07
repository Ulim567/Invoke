using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 공격할 수 없는 장소(PlaceScenes.NoCastPlaces, 지금은 로비)에서 서버가 시전을 거부하는지(#313) 확인하는 테스트. 에디터 호스트 하나로 돌린다.
/// <para>
/// 사용법<br/>
/// - Base 씬에서 Play → eval로 <c>NoCastPlaceTest.StartInEditor()</c>. 서버가 꺼져 있으면 호스트로 켠다.<br/>
/// - 씬에 놓지 않는다.
/// </para>
/// <para>
/// 로비에서 시전 요청 → InNoCastPlace 거부 → 사냥터로 이동 → 시전 요청이 InNoCastPlace로 거부되지 않음 → 로비로 돌아와 다시 InNoCastPlace 거부.<br/>
/// 「[NoCastPlaceTest] 전체 통과」면 통과.
/// </para>
/// </summary>
public class NoCastPlaceTest : MonoBehaviour
{
	// 씬 로드와 이동 시간을 넉넉히 기다린다
	const float TimeoutSeconds = 30f;

	int failedCount;

	/// <summary>에디터 Play 모드에서 eval로 부른다.</summary>
	public static void StartInEditor()
	{
		var go = new GameObject(nameof(NoCastPlaceTest));
		DontDestroyOnLoad(go);
		go.AddComponent<NoCastPlaceTest>();
	}

	IEnumerator Start()
	{
		if (!NetworkServer.active) NetworkManager.singleton.StartHost();
		yield return Wait("호스트로 게임에 들어감", () => NetworkClient.localPlayer != null && HostScene() == PlaceScenes.Lobby);
		yield return Wait("함수 데이터 준비", () => !NetworkClient.localPlayer.TryGetComponent(out FunctionExecutorNetwork n) || n.IsDataReady);
		// PlaceTravelClient는 접속 직후 로딩을 끝낸 뒤에야 이동 요청을 받는다
		yield return new WaitForSeconds(1f);

		FunctionExecutorNetwork net = NetworkClient.localPlayer.GetComponent<FunctionExecutorNetwork>();
		var rejected = new List<CastRejection>();
		net.CastRejected += (slot, reason) => rejected.Add(reason);

		Check("로비 이름이 공격할 수 없는 장소 목록에 있음", PlaceScenes.IsNoCastPlace(PlaceScenes.Lobby));
		Check("사냥터 이름은 목록에 없음", !PlaceScenes.IsNoCastPlace(PlaceScenes.HuntingGround));

		// 로비: 거부
		Check("로비의 플레이어는 공격할 수 없는 장소에 있음", PlaceScenes.IsPlayerInNoCastPlace(net.gameObject));
		rejected.Clear();
		net.RequestCast(0);
		yield return Wait("로비에서 시전 요청의 답", () => rejected.Count > 0);
		Check("로비에서 시전하면 InNoCastPlace 거부", rejected.Count == 1 && rejected[0] == CastRejection.InNoCastPlace, Describe(rejected));

		// 사냥터: InNoCastPlace로는 거부되지 않음 (슬롯이 비었으면 NoFunction, 있으면 시전됨)
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.HuntingGround);
		yield return Wait("사냥터에 도착", () => HostScene() == PlaceScenes.HuntingGround);
		// 답(TravelEndedMessage)을 받기 전의 새 요청은 무시되므로 기다린다
		yield return new WaitForSeconds(1.5f);
		Check("사냥터의 플레이어는 공격할 수 없는 장소에 있지 않음", !PlaceScenes.IsPlayerInNoCastPlace(net.gameObject));
		rejected.Clear();
		net.RequestCast(0);
		yield return new WaitForSeconds(1f);
		Check("사냥터에서 시전하면 InNoCastPlace로 거부되지 않음", !rejected.Contains(CastRejection.InNoCastPlace), Describe(rejected));

		// 다시 로비: 거부
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
		yield return Wait("로비로 돌아옴", () => HostScene() == PlaceScenes.Lobby);
		yield return new WaitForSeconds(1.5f);
		rejected.Clear();
		net.RequestCast(0);
		yield return Wait("다시 로비에서 시전 요청의 답", () => rejected.Count > 0);
		Check("로비로 돌아와 시전하면 다시 InNoCastPlace 거부", rejected.Count == 1 && rejected[0] == CastRejection.InNoCastPlace, Describe(rejected));

		Log(failedCount == 0 ? "전체 통과" : $"실패 {failedCount}개");
	}

	// 호스트에서 로컬 플레이어는 서버의 플레이어와 같은 오브젝트라 장소 씬에 있다
	static string HostScene() => NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject.scene.name : null;

	static string Describe(List<CastRejection> rejected) => rejected.Count > 0 ? string.Join(",", rejected) : "거부 없음";

	IEnumerator Wait(string label, Func<bool> condition)
	{
		float end = Time.time + TimeoutSeconds;
		while (!condition() && Time.time < end) yield return null;
		if (!condition()) Fail($"{label}: 시간 초과");
	}

	void Check(string label, bool passed, string actual = "")
	{
		if (passed) Log($"{label}: 통과");
		else Fail($"{label}: 실패 ({actual})");
	}

	void Fail(string message)
	{
		failedCount++;
		Debug.LogError($"[NoCastPlaceTest] {message}");
	}

	static void Log(string message) => Debug.Log($"[NoCastPlaceTest] {message}");
}
