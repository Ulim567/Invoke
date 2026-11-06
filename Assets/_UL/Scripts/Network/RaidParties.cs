using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// 레이드 한 판의 진행 상태
enum RaidPartyState
{
	// 레이드 던전을 불러오는 중. 아직 아무도 던전에 없다
	Loading,
	// 던전을 다 불러왔다. 파티원을 던전으로 옮기고 싸운다
	Playing,
	// 레이드가 끝났다. 파티원을 로비로 옮기는 중
	Ended,
}

// 서버가 시작한 레이드 한 판 (던전, 파티원, 진행 상태). RaidParties만 만들고 바꾼다
class RaidParty
{
	// 시작할 때 있던 파티원 중 아직 남은 파티원
	public readonly List<NetworkConnectionToClient> members = new List<NetworkConnectionToClient>(RaidRooms.MaxMembers);
	// 서버가 이동을 알렸고(TravelStartedMessage) 아직 옮기지 않은 파티원
	public readonly HashSet<NetworkConnectionToClient> waiting = new HashSet<NetworkConnectionToClient>();
	// waiting 중 로딩 화면이 다 덮였다고 답한(TravelReadyMessage) 파티원
	public readonly HashSet<NetworkConnectionToClient> ready = new HashSet<NetworkConnectionToClient>();
	// 파티원마다 마지막으로 알린 이동의 번호. 답 기다리기 시간이 지났을 때 그 사이 새 이동이 시작됐는지 가리기 위함
	public readonly Dictionary<NetworkConnectionToClient, int> travelIds = new Dictionary<NetworkConnectionToClient, int>();

	public RaidPartyState state;
	// LoadRaidDungeon이 넘긴 던전. dungeonLoaded가 false면 아직 비어 있다
	public Scene dungeon;
	public bool dungeonLoaded;

	// 결과 화면을 클라이언트에 보내기 위해 받아 둔 던전의 RaidJudge와 그 이벤트에 붙인 함수 (던전이 내려가기 전에 뗀다)
	public RaidJudge judge;
	public Action<bool> resultRelay;
}

/// <summary>
/// 파티 레이드의 서버 쪽. 파티장이 시작하면 파티원 모두를 레이드 던전 하나로 옮기고, 레이드가 끝나면 모두 로비로 옮긴다 (#300).
/// <para>
/// 서버가 하는 일<br/>
/// - RaidFlow.StartRequested를 받으면 레이드 한 판 기록(RaidParty)을 만들고, 던전을 한 번 불러와 파티원을 옮긴다.<br/>
/// - 옮기기 전에 파티원 클라이언트에 TravelStartedMessage를 보내고, 로딩 화면이 다 덮였다는 답(TravelReadyMessage)을 받은 파티원만 옮긴다.<br/>
/// - RaidFlow.Ended를 받으면 같은 방법으로 파티원 모두를 로비로 옮긴다. 남은 파티원이 없으면 던전을 내린다.<br/>
/// - 시작할 때 있던 파티원이 한 명이라도 빠지면(접속 끊김, 중복 로그인, 던전 밖으로 옮겨짐) 그 레이드는 실패다 (파티원 이탈 실패).<br/>
/// - 파티원이 레이드 포기를 보내면(RaidGiveUpMessage) 파티원 이탈 실패와 같이 그 레이드를 실패로 끝낸다. 포기한 파티원도 다른 파티원과 함께 로비로 간다.<br/>
/// - 던전의 RaidJudge가 결과 화면 이벤트를 부르면 파티원 클라이언트에 RaidResultMessage를 보낸다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 화면: 「레이드 포기」 버튼이 RequestGiveUp()을 부른다.<br/>
/// - 그 밖의 public 함수는 InvokeNetworkManager가 서버·클라이언트를 켜고 끌 때, 플레이어가 던전을 떠나거나 없어질 때 부른다. 다른 스크립트는 부르지 않는다.
/// </para>
/// </summary>
public static class RaidParties
{
	// 이동을 알린 뒤 클라이언트의 답(TravelReadyMessage)을 기다리는 시간. 지나면 답 없이 옮긴다.
	// 답이 오지 않는 클라이언트(로딩 화면이 없음 등) 때문에 그 파티원이 이동을 기다리는 채로 남거나 던전이 내려가지 않는 일을 막기 위함. 로딩 화면 페이드(0.25초)보다 넉넉히 길게
	const float ReadyTimeoutSeconds = 5f;

	static InvokeNetworkManager manager;
	static int nextTravelId;
	static readonly List<RaidParty> parties = new List<RaidParty>();
	// 던전 씬에서 RaidJudge를 찾을 때 쓰는 최상위 오브젝트 목록
	static readonly List<GameObject> roots = new List<GameObject>(32);

	#region 서버

	/// <summary>[서버] 받을 메시지와 레이드 시작·끝 이벤트를 등록한다. 서버를 켤 때 부른다.</summary>
	public static void StartServer(InvokeNetworkManager networkManager)
	{
		manager = networkManager;
		NetworkServer.RegisterHandler<TravelReadyMessage>(OnTravelReady);
		NetworkServer.RegisterHandler<RaidGiveUpMessage>(OnGiveUp);
		RaidFlow.StartRequested += OnStartRequested;
		RaidFlow.Ended += OnEnded;
	}

	/// <summary>[서버] 기록을 모두 버린다. 서버를 끌 때 부른다. 던전 씬은 InvokeNetworkManager가 서버를 끌 때 함께 내린다.</summary>
	public static void StopServer()
	{
		RaidFlow.StartRequested -= OnStartRequested;
		RaidFlow.Ended -= OnEnded;
		foreach (RaidParty party in parties) UnwatchJudge(party);
		parties.Clear();
		manager = null;
	}

	/// <summary>
	/// [서버] 레이드 이동을 기다리는 파티원이면 true. InvokeNetworkManager가 그 연결의 다른 이동 요청을 거절할 때 묻는다.
	/// </summary>
	public static bool IsWaitingForTravel(NetworkConnectionToClient conn)
	{
		RaidParty party = FindByMember(conn);
		return party != null && party.waiting.Contains(conn);
	}

	/// <summary>
	/// [서버] 플레이어가 레이드 던전에서 다른 장소로 옮겨진 뒤 InvokeNetworkManager.TravelPlayer가 부른다.<br/>
	/// 레이드가 끝난 뒤면 기록에서 빼기만 하고, 진행 중이면 파티원 이탈이다.
	/// </summary>
	public static void OnLeftDungeon(NetworkConnectionToClient conn, Scene dungeon)
	{
		RaidParty party = FindByMember(conn);
		if (party == null || !party.dungeonLoaded || party.dungeon != dungeon) return;
		RemoveMember(party, conn);
	}

	/// <summary>
	/// [서버] 연결의 플레이어가 없어진 뒤(접속 끊김, 중복 로그인으로 쫓겨남) InvokeNetworkManager가 부른다. 레이드 중이면 파티원 이탈이다.
	/// </summary>
	public static void OnMemberGone(NetworkConnectionToClient conn)
	{
		RaidParty party = FindByMember(conn);
		if (party != null) RemoveMember(party, conn);
	}

	static void OnStartRequested(IReadOnlyList<string> memberNames)
	{
		if (manager == null) return;
		manager.StartCoroutine(BeginNextFrame(new List<string>(memberNames)));
	}

	// RaidRooms.TryStart는 StartRequested를 부른 뒤에 바뀐 방 목록을 보낸다.
	// 이동 알림이 방 목록보다 먼저 도착하면, 클라이언트가 이동을 시작한 뒤 방 목록을 받은 RaidLobbyUI가 화면을 닫으며 액션 맵을 다시 활성화한다.
	// 그래서 다음 프레임에 시작한다. 같은 연결로 보낸 메시지는 보낸 순서대로 도착하므로 방 목록이 먼저 도착한다
	static IEnumerator BeginNextFrame(List<string> memberNames)
	{
		yield return null;
		if (manager == null || !NetworkServer.active) yield break;
		Begin(memberNames);
	}

	static void Begin(List<string> memberNames)
	{
		var party = new RaidParty { state = RaidPartyState.Loading };
		foreach (string name in memberNames)
		{
			NetworkConnectionToClient conn = FindConnection(name);
			if (conn == null || FindByMember(conn) != null)
			{
				// 시작을 누른 뒤 한 프레임 사이에 접속이 끊긴 경우. 시작을 누른 파티원이 모두 있어야 시작한다 (이우림 결정, place-scenes.md).
				// 아직 아무에게도 이동을 알리지 않았으므로 모두 로비에 그대로 남는다.
				// 지금은 RaidRooms.TryStart가 이미 방을 지워서 남은 파티원이 방을 잃는다. 방 유지(김도훈)가 생기면 여기서 RaidRooms에 시작 취소를 알려 방을 대기 상태로 되돌린다
				Debug.LogWarning($"[RaidParties] 파티원 {name}을(를) 찾지 못해 레이드를 시작하지 않습니다.");
				return;
			}
			party.members.Add(conn);
		}

		parties.Add(party);
		Debug.Log($"[RaidParties] 레이드를 시작합니다: {string.Join(", ", memberNames)} (진행 중인 레이드 {parties.Count}개)");

		foreach (NetworkConnectionToClient conn in party.members) SendTravelStarted(party, conn);
		manager.LoadRaidDungeon(scene => OnDungeonLoaded(party, scene));
	}

	static void OnDungeonLoaded(RaidParty party, Scene scene)
	{
		// 불러오는 사이에 파티원이 모두 빠져 기록이 지워졌으면, 아무도 쓰지 않는 던전이 남지 않도록 바로 내린다
		if (!parties.Contains(party))
		{
			manager?.UnloadRaidDungeon(scene);
			return;
		}

		party.dungeon = scene;
		party.dungeonLoaded = true;
		party.state = RaidPartyState.Playing;
		WatchJudge(party);

		// 이미 다 덮었다고 답한 파티원부터 옮긴다. 나머지는 답이 올 때 OnTravelReady가 옮긴다
		foreach (NetworkConnectionToClient conn in new List<NetworkConnectionToClient>(party.ready)) MoveIfReady(party, conn);
	}

	static void OnTravelReady(NetworkConnectionToClient conn, TravelReadyMessage _)
	{
		RaidParty party = FindByMember(conn);
		if (party == null || !party.waiting.Contains(conn)) return;

		party.ready.Add(conn);
		MoveIfReady(party, conn);
	}

	// 그 파티원이 다 덮었다고 답했고 갈 곳이 준비됐으면 옮긴다. 레이드가 끝났으면 로비로, 아니면 던전으로
	static void MoveIfReady(RaidParty party, NetworkConnectionToClient conn)
	{
		if (!party.ready.Contains(conn)) return;
		GameObject player = conn.identity != null ? conn.identity.gameObject : null;
		if (player == null) return;

		Scene target;
		if (party.state == RaidPartyState.Ended)
		{
			if (!manager.TryGetPlaceScene(PlaceScenes.Lobby, out target)) return;
		}
		else
		{
			if (!party.dungeonLoaded) return;
			// 결과 화면을 보여 주는 동안(RaidJudge는 끝났고 Ended는 아직)에는 던전에 넣지 않는다.
			// 끝난 레이드에 들어가 결과 화면도 못 본 채 바로 로비로 돌아오게 되기 때문이다. Ended 때 SendAllToLobby가 이동을 취소한다
			if (party.judge != null && party.judge.IsEnded) return;
			target = party.dungeon;
		}

		party.waiting.Remove(conn);
		party.ready.Remove(conn);
		manager.TravelPlayer(conn, player, target);
	}

	static void OnEnded(Scene dungeon, bool cleared)
	{
		RaidParty party = FindByDungeon(dungeon);
		if (party == null)
		{
			Debug.LogWarning("[RaidParties] 서버가 시작하지 않은 레이드 던전의 끝을 받아 무시합니다 (네트워크 없이 시험하는 씬 등).");
			return;
		}
		if (party.state == RaidPartyState.Ended) return;

		Debug.Log($"[RaidParties] 레이드가 끝났습니다 ({(cleared ? "클리어" : "실패")}). 파티원을 로비로 옮깁니다.");
		SendAllToLobby(party);
	}

	// 레이드를 끝내고 파티원 모두를 로비로 보낸다.
	// 던전에 들어간 파티원은 서버가 시작한 이동으로 옮기고, 아직 들어가지 않은 파티원은 이동을 취소해 로비에 그대로 둔다
	static void SendAllToLobby(RaidParty party)
	{
		party.state = RaidPartyState.Ended;
		UnwatchJudge(party);

		foreach (NetworkConnectionToClient conn in new List<NetworkConnectionToClient>(party.members))
		{
			GameObject player = conn.identity != null ? conn.identity.gameObject : null;
			if (player != null && party.dungeonLoaded && player.scene == party.dungeon)
			{
				// 시작 때 받은 답이 남아 있으면 덮기 전에 옮겨지므로 지운다
				party.ready.Remove(conn);
				SendTravelStarted(party, conn);
				continue;
			}

			party.members.Remove(conn);
			party.waiting.Remove(conn);
			party.ready.Remove(conn);
			party.travelIds.Remove(conn);
			// 덮던(덮어 둔) 로딩 화면을 걷게 한다. 클라이언트는 이동이 끝난 것과 같이 처리한다
			conn.Send(new TravelEndedMessage());
		}
		RemoveIfEmpty(party);
	}

	// 파티원을 기록에서 뺀다. 레이드가 끝나기 전이면 파티원 이탈 실패
	static void RemoveMember(RaidParty party, NetworkConnectionToClient conn)
	{
		if (!party.members.Remove(conn)) return;
		party.waiting.Remove(conn);
		party.ready.Remove(conn);
		party.travelIds.Remove(conn);

		if (party.state != RaidPartyState.Ended)
		{
			Debug.Log($"[RaidParties] 파티원(connId={conn.connectionId})이 빠져서 레이드를 실패로 끝냅니다.");
			FailRaid(party);
		}
		RemoveIfEmpty(party);
	}

	static void OnGiveUp(NetworkConnectionToClient conn, RaidGiveUpMessage _)
	{
		RaidParty party = FindByMember(conn);
		GameObject player = conn.identity != null ? conn.identity.gameObject : null;

		// 던전에 들어와 싸우는 중인 파티원만 포기할 수 있다. 불러오는 중·끝난 뒤·던전 밖에서 온 요청은 버튼을 볼 수 없는 때라 무시한다
		if (party == null || party.state != RaidPartyState.Playing || player == null || player.scene != party.dungeon)
		{
			Debug.LogWarning($"[RaidParties] 싸우는 중인 레이드 던전에 있지 않은 연결(connId={conn.connectionId})의 레이드 포기를 무시합니다.");
			return;
		}

		Debug.Log($"[RaidParties] 파티원(connId={conn.connectionId})이 레이드를 포기해서 실패로 끝냅니다.");
		// 파티원 이탈과 달리 포기한 파티원은 기록에 남는다. 다른 파티원과 함께 결과 화면을 보고 로비로 간다
		FailRaid(party);
	}

	// 레이드를 실패로 끝낸다 (파티원 이탈 실패, 레이드 포기).
	// 싸우는 중이면 RaidJudge가 모두 죽었을 때와 같이 끝낸다: 결과 화면 → Result Delay 뒤 RaidFlow.Ended → OnEnded가 로비로 (place-scenes.md 「파티 레이드」).
	// 이미 클리어로 끝났으면 FailByServer가 무시하므로 클리어가 실패로 바뀌지 않는다.
	// 불러오는 중이거나 RaidJudge가 없으면 보여 줄 결과 화면이 없으므로 바로 로비로 보낸다
	static void FailRaid(RaidParty party)
	{
		if (party.state == RaidPartyState.Playing && party.judge != null)
		{
			party.judge.FailByServer();
			return;
		}
		SendAllToLobby(party);
	}

	// 남은 파티원이 없으면 기록을 지우고 던전을 내린다. 불러오는 중이면 OnDungeonLoaded가 기록이 없는 것을 보고 내린다
	static void RemoveIfEmpty(RaidParty party)
	{
		if (party.members.Count > 0 || !parties.Remove(party)) return;

		UnwatchJudge(party);
		if (party.dungeonLoaded) manager?.UnloadRaidDungeon(party.dungeon);
		Debug.Log($"[RaidParties] 레이드 기록을 지웠습니다. 진행 중인 레이드 {parties.Count}개");
	}

	static void SendTravelStarted(RaidParty party, NetworkConnectionToClient conn)
	{
		party.waiting.Add(conn);
		int travelId = ++nextTravelId;
		party.travelIds[conn] = travelId;
		conn.Send(new TravelStartedMessage());
		manager?.StartCoroutine(MoveWithoutReplyAfterTimeout(party, conn, travelId));
	}

	// 답 기다리기 시간이 지나도 답이 없으면 답한 것으로 보고 옮긴다. 화면이 덮이지 않은 채 옮겨질 수 있지만, 이동을 기다리는 채로 남는 것보다 낫다
	static IEnumerator MoveWithoutReplyAfterTimeout(RaidParty party, NetworkConnectionToClient conn, int travelId)
	{
		yield return new WaitForSecondsRealtime(ReadyTimeoutSeconds);

		// 그 사이 옮겼거나, 기록에서 빠졌거나, 새 이동을 알렸으면 할 일이 없다
		if (!parties.Contains(party) || !party.waiting.Contains(conn) || party.ready.Contains(conn)) yield break;
		if (!party.travelIds.TryGetValue(conn, out int current) || current != travelId) yield break;

		Debug.LogWarning($"[RaidParties] 파티원(connId={conn.connectionId})이 {ReadyTimeoutSeconds}초 안에 이동 준비를 답하지 않아 답 없이 옮깁니다.");
		party.ready.Add(conn);
		MoveIfReady(party, conn);
	}

	// 던전의 RaidJudge가 결과 화면 이벤트를 부르면 파티원 클라이언트에 보내도록 붙인다
	static void WatchJudge(RaidParty party)
	{
		party.judge = FindJudge(party.dungeon);
		if (party.judge == null)
		{
			Debug.LogWarning("[RaidParties] 레이드 던전에 RaidJudge가 없어서 결과 화면을 클라이언트에 보내지 않습니다.");
			return;
		}
		party.resultRelay = cleared => SendResult(party, cleared);
		party.judge.ResultEffectPlayed += party.resultRelay;
	}

	static void UnwatchJudge(RaidParty party)
	{
		if (party.judge != null && party.resultRelay != null) party.judge.ResultEffectPlayed -= party.resultRelay;
		party.judge = null;
		party.resultRelay = null;
	}

	static void SendResult(RaidParty party, bool cleared)
	{
		if (party.state != RaidPartyState.Playing) return;
		foreach (NetworkConnectionToClient conn in party.members)
		{
			// 호스트의 클라이언트는 서버의 RaidJudge를 같이 쓰므로 이미 결과 화면이 떴다
			if (conn is LocalConnectionToClient) continue;
			// 아직 던전에 들어오지 않은 파티원의 클라이언트는 던전 씬이 없어 결과 화면을 띄울 곳이 없다
			GameObject player = conn.identity != null ? conn.identity.gameObject : null;
			if (player == null || player.scene != party.dungeon) continue;
			conn.Send(new RaidResultMessage { cleared = cleared });
		}
	}

	// 게임에 들어온 연결 중 닉네임이 같은 연결. 곧 끊길 연결(중복 로그인으로 쫓겨남)과 플레이어가 없는 연결은 빼고 찾는다
	static NetworkConnectionToClient FindConnection(string nickname)
	{
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
		{
			if (!(conn.authenticationData is AccountSession session)) continue;
			if (!session.HasEntered || session.IsKicked || conn.identity == null) continue;
			if (session.Nickname == nickname) return conn;
		}
		return null;
	}

	static RaidParty FindByMember(NetworkConnectionToClient conn)
	{
		for (int i = 0; i < parties.Count; i++)
			if (parties[i].members.Contains(conn)) return parties[i];
		return null;
	}

	static RaidParty FindByDungeon(Scene dungeon)
	{
		for (int i = 0; i < parties.Count; i++)
			if (parties[i].dungeonLoaded && parties[i].dungeon == dungeon) return parties[i];
		return null;
	}

	#endregion

	#region 클라이언트

	/// <summary>[클라이언트] 받는 함수를 등록한다. 클라이언트를 켤 때 부른다.</summary>
	public static void StartClient()
	{
		NetworkClient.RegisterHandler<RaidResultMessage>(OnResult);
	}

	/// <summary>
	/// [클라이언트] 서버에 레이드 포기를 보낸다. 레이드 던전의 「레이드 포기」 버튼이 부른다.<br/>
	/// 서버가 그 레이드를 실패로 끝내고, 포기한 사람을 포함한 파티원 모두를 로비로 옮긴다. 싸우는 중이 아니면 서버가 무시한다.
	/// </summary>
	public static void RequestGiveUp()
	{
		if (!NetworkClient.isConnected)
		{
			Debug.LogWarning("[RaidParties] 서버에 접속하지 않아 레이드 포기를 보내지 않습니다.");
			return;
		}
		NetworkClient.Send(new RaidGiveUpMessage());
	}

	static void OnResult(RaidResultMessage msg)
	{
		RaidJudge judge = FindLoadedDungeonJudge();
		if (judge == null)
		{
			Debug.LogWarning("[RaidParties] 레이드 결과를 받았지만 불러온 레이드 던전에 RaidJudge가 없습니다.");
			return;
		}

		// 이 클라이언트의 RaidJudge는 판정하지 않으므로, 결과 화면 이벤트만 부르게 해서 RaidResultView가 결과 화면을 띄운다
		judge.PlayResultOnly(msg.cleared);
	}

	// 클라이언트는 자기 플레이어가 있는 레이드 던전 하나만 불러온다
	static RaidJudge FindLoadedDungeonJudge()
	{
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (scene.isLoaded && scene.name == PlaceScenes.RaidDungeon) return FindJudge(scene);
		}
		return null;
	}

	#endregion

	static RaidJudge FindJudge(Scene scene)
	{
		scene.GetRootGameObjects(roots);
		RaidJudge found = null;
		for (int i = 0; i < roots.Count && found == null; i++) found = roots[i].GetComponentInChildren<RaidJudge>(true);
		roots.Clear();
		return found;
	}
}
