using System.Collections.Generic;
using Mirror;
using UnityEngine.SceneManagement;

// 서버가 플레이어 한 명을 위해 연 연습장 하나 (주인, 씬). TrainingRooms만 만들고 지운다
class TrainingRoom
{
	// 이 연습장을 쓰는 플레이어의 연결. 연습장에는 주인 한 명만 들어간다
	public readonly NetworkConnectionToClient owner;
	// InstanceScenes.Load가 넘긴 연습장 씬. sceneLoaded가 false면 아직 비어 있다
	public Scene scene;
	public bool sceneLoaded;

	public TrainingRoom(NetworkConnectionToClient owner)
	{
		this.owner = owner;
	}
}

/// <summary>
/// 연습장의 서버 쪽. 플레이어가 연습장으로 이동을 요청하면 그 플레이어를 위해 연습장 씬을 하나 열고, 나가거나 없어지면 내린다 (#301).
/// <para>
/// 서버가 하는 일<br/>
/// - 연습장을 연 플레이어마다 기록(TrainingRoom) 하나를 만든다. 연결 하나에 연습장 하나다.<br/>
/// - 씬을 다 불러오면 플레이어를 연습장으로 옮긴다.<br/>
/// - 플레이어가 로비로 나가거나, 접속이 끊기거나, 없어지면 그 기록을 지우고 연습장 씬을 내린다.<br/>
/// - 씬을 불러오고 내리는 일 자체는 InstanceScenes가 한다.
/// </para>
/// <para>
/// 사용법<br/>
/// - InvokeNetworkManager가 서버를 켜고 끌 때, 연습장 이동 요청을 받을 때, 플레이어가 이동·접속 끊김·없어짐을 겪을 때 부른다. 다른 스크립트는 부르지 않는다.
/// </para>
/// </summary>
public static class TrainingRooms
{
	static InvokeNetworkManager manager;
	static readonly List<TrainingRoom> rooms = new List<TrainingRoom>();

	/// <summary>[서버] 서버를 켤 때 InvokeNetworkManager가 부른다.</summary>
	public static void StartServer(InvokeNetworkManager networkManager)
	{
		manager = networkManager;
	}

	/// <summary>[서버] 기록을 모두 버린다. 서버를 끌 때 부른다. 연습장 씬은 InvokeNetworkManager가 서버를 끌 때 함께 내린다.</summary>
	public static void StopServer()
	{
		rooms.Clear();
		manager = null;
	}

	/// <summary>
	/// [서버] 그 연결의 플레이어를 위해 연습장을 불러오고, 다 불러오면 플레이어를 옮긴다. InvokeNetworkManager.OnTravelRequest가 부른다.<br/>
	/// 그 연결의 연습장이 이미 있거나 불러오는 중이면 false(요청을 무시해야 함). 씬을 불러오는 동안 같은 연결의 요청이 또 오면 씬이 두 개 열리므로 막는다.
	/// </summary>
	public static bool TryOpen(NetworkConnectionToClient conn)
	{
		if (Find(conn) != null) return false;

		var room = new TrainingRoom(conn);
		rooms.Add(room);
		InstanceScenes.Load(PlaceScenes.TrainingRoom, scene => OnSceneLoaded(room, scene));
		return true;
	}

	/// <summary>
	/// [서버] 플레이어의 접속이 끊기거나 플레이어가 없어졌을 때 부른다. 그 연결의 연습장 기록을 지우고, 씬이 불러와져 있으면 내린다.<br/>
	/// 플레이어를 없앤 뒤에 불러야 플레이어가 연습장과 함께 사라지지 않는다. 씬을 불러오는 중이면 기록만 지우고,
	/// 다 불러온 뒤에 OnSceneLoaded가 기록이 없는 것을 보고 내린다.
	/// </summary>
	public static void OnPlayerGone(NetworkConnectionToClient conn)
	{
		TrainingRoom room = Find(conn);
		if (room == null) return;

		rooms.Remove(room);
		if (room.sceneLoaded) InstanceScenes.Unload(room.scene);
	}

	/// <summary>
	/// [서버] 플레이어가 다른 장소로 옮겨진 뒤 InvokeNetworkManager.TravelPlayer가 부른다.
	/// 떠난 씬(previous)이 그 연결의 연습장이면 기록을 지우고 연습장을 내린다.
	/// </summary>
	public static void OnLeftRoom(NetworkConnectionToClient conn, Scene previous)
	{
		TrainingRoom room = Find(conn);
		if (room == null || !room.sceneLoaded || room.scene != previous) return;

		rooms.Remove(room);
		InstanceScenes.Unload(room.scene);
	}

	// 씬을 다 불러왔다. 기록이 아직 있고 플레이어가 있으면 옮긴다
	static void OnSceneLoaded(TrainingRoom room, Scene scene)
	{
		room.scene = scene;
		room.sceneLoaded = true;

		// 불러오는 사이에 접속이 끊겼거나 플레이어가 없어졌으면(기록이 지워졌으면), 아무도 쓰지 않는 연습장이 남지 않도록 바로 내린다
		NetworkConnectionToClient conn = room.owner;
		if (!rooms.Contains(room) || !NetworkServer.connections.ContainsKey(conn.connectionId) || conn.identity == null)
		{
			rooms.Remove(room);
			InstanceScenes.Unload(scene);
			return;
		}
		manager.TravelPlayer(conn, conn.identity.gameObject, scene);
	}

	static TrainingRoom Find(NetworkConnectionToClient conn)
	{
		foreach (TrainingRoom room in rooms)
			if (room.owner == conn) return room;
		return null;
	}
}
