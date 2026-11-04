using System.Collections.Generic;
using Mirror;

/// <summary>
/// 레이드 방(RaidRooms)의 네트워크. 클라이언트의 요청을 서버에 보내고, 서버의 방 목록을 클라이언트에 보낸다.
/// <para>
/// 서버가 하는 일<br/>
/// - 요청(RaidRoomRequestMessage)을 받아 그 연결의 닉네임으로 RaidRooms.Process를 부른다.<br/>
/// - 성공하면 게임에 들어온 모든 클라이언트에 방 목록을, 실패하면 요청한 클라이언트에만 이유를 보낸다.<br/>
/// - 플레이어가 게임에 들어오면 그 클라이언트에 방 목록을 보내고, 접속이 끊기거나 중복 로그인으로 쫓겨나면 그 사람을 방에서 뺀다.
/// </para>
/// <para>
/// 클라이언트가 하는 일<br/>
/// - RaidRooms.RequestSender를 넣어 Request… 함수가 서버로 보내게 한다.<br/>
/// - 받은 방 목록을 RaidRooms에 넣는다. 호스트는 서버와 RaidRooms를 같이 쓰므로 목록을 넣지 않고 닉네임만 넣는다.
/// </para>
/// <para>
/// 사용법: InvokeNetworkManager가 서버·클라이언트를 켜고 끌 때, 플레이어를 만들고 없앨 때 아래 함수를 부른다. 다른 스크립트는 부르지 않는다.
/// </para>
/// </summary>
public static class RaidRoomsNetwork
{
	#region 서버

	/// <summary>[서버] 요청 처리 함수를 등록한다. 서버를 켤 때 부른다.</summary>
	public static void StartServer()
	{
		NetworkServer.RegisterHandler<RaidRoomRequestMessage>(OnRequest);
	}

	/// <summary>[서버] 방을 모두 비운다. 서버를 끌 때 부른다. 다시 켰을 때 이전 방이 남지 않게 하기 위함이다.</summary>
	public static void StopServer()
	{
		RaidRooms.Clear();
	}

	/// <summary>[서버] 게임에 막 들어온 플레이어의 클라이언트에 지금 방 목록을 보낸다.</summary>
	public static void SendList(NetworkConnectionToClient conn)
	{
		if (!TryGetSession(conn, out AccountSession session)) return;
		conn.Send(new RaidRoomListMessage { myName = session.Nickname, rooms = BuildInfos() });
	}

	/// <summary>[서버] 중복 로그인으로 쫓겨난 연결의 플레이어를 없앨 때 부른다. 그 사람을 방에서 뺀다.</summary>
	public static void OnPlayerDespawned(NetworkConnectionToClient conn) => LeaveRoomOf(conn);

	/// <summary>[서버] 접속이 끊겼을 때 부른다. 그 사람을 방에서 뺀다.</summary>
	public static void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		// 중복 로그인으로 쫓겨난 연결은 OnPlayerDespawned에서 이미 뺐다.
		// 끊김이 늦게 도착하는 사이 같은 닉네임의 새 연결이 방에 들어갔을 수 있으므로, 다시 빼지 않는다
		if (conn.authenticationData is AccountSession session && session.IsKicked) return;
		LeaveRoomOf(conn);
	}

	// 그 연결의 사람이 방에 있으면 빼고 모두에게 방 목록을 보낸다
	static void LeaveRoomOf(NetworkConnectionToClient conn)
	{
		if (!(conn.authenticationData is AccountSession session)) return;

		RaidRoom room = RaidRooms.FindRoomOf(session.Nickname);
		if (room == null) return;

		RaidRooms.Leave(room, session.Nickname);
		SendListToAll();
	}

	static void OnRequest(NetworkConnectionToClient conn, RaidRoomRequestMessage request)
	{
		// 게임에 들어오기 전이거나, 중복 로그인으로 곧 끊길 연결의 요청은 받지 않는다
		if (!TryGetSession(conn, out AccountSession session)) return;

		if (RaidRooms.Process(request, session.Nickname, out string reason)) SendListToAll();
		else if (reason != null) conn.Send(new RaidRoomFailedMessage { reason = reason });
	}

	// 게임에 들어온 모든 클라이언트에 방 목록을 보낸다. 받는 사람마다 닉네임이 달라서 연결마다 메시지를 만든다
	static void SendListToAll()
	{
		RaidRoomInfo[] infos = BuildInfos();
		foreach (NetworkConnectionToClient conn in NetworkServer.connections.Values)
		{
			if (!TryGetSession(conn, out AccountSession session)) continue;
			conn.Send(new RaidRoomListMessage { myName = session.Nickname, rooms = infos });
		}
	}

	static RaidRoomInfo[] BuildInfos()
	{
		IReadOnlyList<RaidRoom> rooms = RaidRooms.Rooms;
		var infos = new RaidRoomInfo[rooms.Count];
		for (int i = 0; i < rooms.Count; i++)
		{
			RaidRoom room = rooms[i];
			IReadOnlyList<RaidMember> members = room.Members;
			var names = new string[members.Count];
			var ready = new bool[members.Count];
			for (int m = 0; m < members.Count; m++)
			{
				names[m] = members[m].Name;
				ready[m] = members[m].IsReady;
			}
			infos[i] = new RaidRoomInfo { id = room.Id, name = room.Name, hasPassword = room.HasPassword, memberNames = names, memberReady = ready };
		}
		return infos;
	}

	// 게임에 들어왔고 곧 끊길 연결이 아니면 true
	static bool TryGetSession(NetworkConnectionToClient conn, out AccountSession session)
	{
		session = conn.authenticationData as AccountSession;
		return session != null && session.HasEntered && !session.IsKicked;
	}

	#endregion

	#region 클라이언트

	/// <summary>[클라이언트] 받는 함수를 등록하고, RaidRooms의 요청이 서버로 가게 한다. 클라이언트를 켤 때 부른다.</summary>
	public static void StartClient()
	{
		NetworkClient.RegisterHandler<RaidRoomListMessage>(OnList);
		NetworkClient.RegisterHandler<RaidRoomFailedMessage>(msg => RaidRooms.NotifyFailed(msg.reason));
		RaidRooms.RequestSender = request => NetworkClient.Send(request);
	}

	/// <summary>
	/// [클라이언트] 요청을 다시 내 컴퓨터 안에서 처리하게 하고, 받아 둔 방 목록을 비운다. 클라이언트를 끌 때 부른다.
	/// </summary>
	public static void StopClient()
	{
		RaidRooms.RequestSender = null;
		// 호스트의 방은 서버의 원본이므로 서버를 끌 때(StopServer) 비운다
		if (!NetworkServer.active) RaidRooms.Clear();
	}

	static void OnList(RaidRoomListMessage msg)
	{
		RaidRooms.SetServerName(msg.myName);
		// 호스트는 서버와 RaidRooms를 같이 써서 이미 바뀌어 있다. 서버가 바꿀 때 RoomsChanged·RoomChanged도 이미 불렸다
		if (NetworkServer.active) return;
		RaidRooms.ApplyServerList(msg.rooms);
	}

	#endregion
}
