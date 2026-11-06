using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 서버가 시작하는 장소 이동의 공통 부분 (#310). 레이드 시작·끝(RaidParties)과 부활할 때 로비로 보내기(InvokeNetworkManager)가 함께 쓴다.
/// <para>
/// 서버가 하는 일<br/>
/// - 클라이언트에 이동 시작(TravelStartedMessage)을 알리고, 로딩 화면이 다 덮였다는 답(TravelReadyMessage)이 오면 부르는 쪽이 넘긴 할 일을 한 번 부른다.<br/>
/// - 답 기다리기 시간(5초)이 지나도 답이 없으면 답 없이 할 일을 부른다. 답이 오지 않는 클라이언트(로딩 화면이 없음 등) 때문에 이동을 기다리는 채로 남지 않게 하기 위함이다.<br/>
/// - 같은 연결에 새 이동을 알리면 이전 이동의 할 일은 버린다. 그 뒤에 이전 이동의 답 기다리기 시간이 지나도 무시한다.<br/>
/// - TravelReadyMessage를 받는 곳은 여기 하나다. Mirror는 메시지 종류마다 받는 함수를 하나만 등록하므로, 이 메시지를 쓰는 이동은 모두 여기를 거친다.
/// </para>
/// <para>
/// 사용법: 옮길 곳과 때는 부르는 쪽이 정하고 Begin(연결, 할 일)을 부른다. 할 일 안에서 보통 InvokeNetworkManager.TravelPlayer를 부른다.
/// 이동을 그만두면 Cancel을 부르고, 로딩 화면을 걷도록 TravelEndedMessage는 부르는 쪽이 보낸다.
/// InvokeNetworkManager가 서버를 켜고 끌 때, 연결의 플레이어가 없어질 때 StartServer·StopServer·OnPlayerGone을 부른다.
/// </para>
/// </summary>
public static class ServerTravel
{
	// 이동을 알린 뒤 클라이언트의 답을 기다리는 시간. 로딩 화면 페이드(0.25초)보다 넉넉히 길게
	const float ReadyTimeoutSeconds = 5f;

	static MonoBehaviour coroutineRunner;
	static int nextTravelId;
	// 연결마다 지금 기다리는 이동: 이동 번호(답 기다리기 시간이 지났을 때 그 사이 새 이동이 시작됐는지 가리기 위함)와 다 덮였을 때 할 일
	static readonly Dictionary<NetworkConnectionToClient, (int id, Action onReady)> pending = new Dictionary<NetworkConnectionToClient, (int, Action)>();

	/// <summary>[서버] TravelReadyMessage를 받도록 등록한다. 서버를 켤 때 InvokeNetworkManager가 부른다. runner는 답 기다리기 코루틴을 돌릴 오브젝트</summary>
	public static void StartServer(MonoBehaviour runner)
	{
		coroutineRunner = runner;
		NetworkServer.RegisterHandler<TravelReadyMessage>(OnTravelReady);
	}

	/// <summary>[서버] 기다리던 이동을 모두 버린다. 서버를 끌 때 부른다.</summary>
	public static void StopServer()
	{
		pending.Clear();
		coroutineRunner = null;
	}

	/// <summary>
	/// [서버] conn의 클라이언트에 이동 시작을 알린다. 로딩 화면이 다 덮였다는 답이 오거나 답 기다리기 시간이 지나면 onReady를 한 번 부른다.
	/// 이미 기다리던 이동이 있으면 그 이동의 onReady는 버린다.
	/// </summary>
	public static void Begin(NetworkConnectionToClient conn, Action onReady)
	{
		int id = ++nextTravelId;
		pending[conn] = (id, onReady);
		conn.Send(new TravelStartedMessage());
		coroutineRunner?.StartCoroutine(ReadyAfterTimeout(conn, id));
	}

	/// <summary>[서버] conn이 서버가 알린 이동의 답을 기다리는 중이면 true</summary>
	public static bool IsWaiting(NetworkConnectionToClient conn) => pending.ContainsKey(conn);

	/// <summary>[서버] conn이 기다리던 이동의 할 일을 부르지 않고 버린다. 클라이언트의 로딩 화면을 걷는 TravelEndedMessage는 부르는 쪽이 보낸다.</summary>
	public static void Cancel(NetworkConnectionToClient conn) => pending.Remove(conn);

	/// <summary>[서버] 연결의 플레이어가 없어진 뒤(접속 끊김, 중복 로그인으로 쫓겨남) InvokeNetworkManager가 부른다.</summary>
	public static void OnPlayerGone(NetworkConnectionToClient conn) => pending.Remove(conn);

	static void OnTravelReady(NetworkConnectionToClient conn, TravelReadyMessage _) => Complete(conn, null);

	// 답 기다리기 시간이 지나도 답이 없으면 답한 것으로 보고 할 일을 부른다. 화면이 덮이지 않은 채 옮겨질 수 있지만, 이동을 기다리는 채로 남는 것보다 낫다
	static IEnumerator ReadyAfterTimeout(NetworkConnectionToClient conn, int id)
	{
		yield return new WaitForSecondsRealtime(ReadyTimeoutSeconds);
		if (!pending.TryGetValue(conn, out var travel) || travel.id != id) yield break;

		Debug.LogWarning($"[ServerTravel] connId={conn.connectionId}이(가) {ReadyTimeoutSeconds}초 안에 이동 준비를 답하지 않아 답 없이 옮깁니다.");
		Complete(conn, id);
	}

	// id가 있으면 그 이동일 때만 (답 기다리기 시간이 지난 경우), 없으면 지금 기다리는 이동 (답이 온 경우)
	static void Complete(NetworkConnectionToClient conn, int? id)
	{
		if (!pending.TryGetValue(conn, out var travel) || (id.HasValue && travel.id != id.Value)) return;
		pending.Remove(conn);
		travel.onReady?.Invoke();
	}
}
