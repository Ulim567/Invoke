using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이드 파티방의 파티원 한 명. 값은 RaidRooms만 바꾼다.
/// </summary>
public class RaidMember
{
	/// <summary>파티원 이름 (방 안에서 겹치지 않는다)</summary>
	public string Name { get; }

	/// <summary>준비를 마쳤으면 true. 파티장은 준비 없이 시작 버튼을 누른다</summary>
	public bool IsReady { get; internal set; }

	internal RaidMember(string name) => Name = name;
}

/// <summary>
/// 레이드 파티방 하나 (방 이름, 비밀번호, 파티원). 값은 RaidRooms만 바꾸고, 다른 스크립트는 읽기만 한다.
/// </summary>
public class RaidRoom
{
	readonly string password;
	internal readonly List<RaidMember> members = new List<RaidMember>(RaidRooms.MaxMembers);

	/// <summary>방 번호 (만든 순서, 1부터)</summary>
	public int Id { get; }

	/// <summary>방 이름</summary>
	public string Name { get; }

	/// <summary>비밀번호가 걸린 방이면 true</summary>
	public bool HasPassword { get; }

	/// <summary>파티원 목록. 0번이 파티장이다</summary>
	public IReadOnlyList<RaidMember> Members => members;

	/// <summary>파티장. 방을 만든 사람이고, 나가면 다음 사람이 이어받는다</summary>
	public RaidMember Leader => members.Count > 0 ? members[0] : null;

	/// <summary>인원이 다 찼으면 true</summary>
	public bool IsFull => members.Count >= RaidRooms.MaxMembers;

	/// <summary>파티장을 뺀 모두가 준비했으면 true (혼자여도 true)</summary>
	public bool AllReady
	{
		get
		{
			for (int i = 1; i < members.Count; i++)
				if (!members[i].IsReady) return false;
			return true;
		}
	}

	internal RaidRoom(int id, string name, string password)
	{
		Id = id;
		Name = name;
		this.password = password;
		HasPassword = !string.IsNullOrEmpty(password);
	}

	// 클라이언트가 서버의 방 목록으로 만드는 사본. 서버는 비밀번호를 보내지 않으므로 걸려 있는지만 받는다
	internal RaidRoom(int id, string name, bool hasPassword)
	{
		Id = id;
		Name = name;
		HasPassword = hasPassword;
	}

	internal bool CheckPassword(string input) => !HasPassword || input == password;

	internal RaidMember Find(string memberName)
	{
		for (int i = 0; i < members.Count; i++)
			if (members[i].Name == memberName) return members[i];
		return null;
	}
}

/// <summary>
/// 레이드 방 목록과 파티방을 맡는다 (방 만들기, 들어가기, 준비, 나가기, 시작). 방과 파티원 값은 여기서만 바꾼다.
/// <para>
/// - 방 목록의 원본은 서버에 있다 (#242). 서버는 방이 바뀔 때마다 모든 클라이언트에 목록을 보내고, 클라이언트는 받은 목록을 여기에 넣는다.<br/>
/// - 서버에 접속하지 않았으면(테스트 씬) 요청을 내 컴퓨터 안에서 바로 처리한다.<br/>
/// - 파티원은 이름(계정 닉네임)으로 구분한다. 인원은 최대 MaxMembers(3)명이다.<br/>
/// - 시작하면 방을 목록에서 없애고, 파티원 이름 목록을 RaidFlow.RequestStart(레이드 시작)에 넘긴다.
/// </para>
/// <para>
/// 사용법 (화면): Request… 함수로 요청하고, RoomsChanged(목록이 바뀜)·RoomChanged(한 방의 파티원이 바뀜)를 받아 다시 그린다.
/// 요청은 결과를 바로 돌려주지 않는다. 성공은 목록으로(FindRoomOf(MyName)), 실패는 RequestFailed로 온다.
/// </para>
/// <para>
/// 사용법 (서버 코드): TryCreate 등 이름을 받는 함수는 서버(RaidRoomsNetwork)가 부른다. 실패하면 false와 함께 이유(화면에 보여 줄 문장)를 돌려준다.
/// </para>
/// </summary>
public static class RaidRooms
{
	/// <summary>파티 최대 인원</summary>
	public const int MaxMembers = 3;

	// 서버에 접속하지 않았을 때 내 이름
	const string OfflineName = "나";

	static readonly List<RaidRoom> rooms = new List<RaidRoom>();
	static int nextId = 1;
	// 서버가 알려 준 내 닉네임. 접속하지 않았으면 null
	static string serverName;

	/// <summary>지금 있는 방 목록 (만든 순서). 클라이언트에서는 서버가 보낸 목록이다</summary>
	public static IReadOnlyList<RaidRoom> Rooms => rooms;

	/// <summary>내 이름 (계정 닉네임). 서버에 접속하지 않았으면 "나"</summary>
	public static string MyName => serverName ?? OfflineName;

	/// <summary>서버에 접속해 요청을 서버로 보내는 중이면 true. false면 요청을 내 컴퓨터 안에서 바로 처리한다 (테스트 씬)</summary>
	public static bool IsOnline => RequestSender != null;

	/// <summary>방이 생기거나 없어지거나 방 인원이 바뀌었을 때. 방 목록 화면을 다시 그린다</summary>
	public static event Action RoomsChanged;

	/// <summary>한 방의 파티원·준비 상태가 바뀌었을 때. 바뀐 방을 넘긴다. 파티방 화면을 다시 그린다</summary>
	public static event Action<RaidRoom> RoomChanged;

	/// <summary>내 요청이 거절되었을 때. 화면에 보여 줄 이유를 넘긴다</summary>
	public static event Action<string> RequestFailed;

	/// <summary>
	/// 요청을 서버에 보내는 함수. 서버에 접속한 클라이언트에서 RaidRoomsNetwork가 넣고, 접속이 끊기면 비운다.<br/>
	/// 비어 있으면 요청을 내 컴퓨터 안에서 바로 처리한다.
	/// </summary>
	// RaidRooms가 Mirror를 직접 부르지 않게 하기 위함이다 (코드 작성 규칙: 기능은 Mirror 없이)
	internal static Action<RaidRoomRequestMessage> RequestSender;

	// Play를 다시 눌렀을 때 이전 방이 남지 않게 비운다 (도메인 리로드를 꺼 둔 에디터 대비)
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		rooms.Clear();
		nextId = 1;
		serverName = null;
		RoomsChanged = null;
		RoomChanged = null;
		RequestFailed = null;
		RequestSender = null;
	}

	// ── 요청 (화면이 부른다) ──

	/// <summary>방 만들기를 요청한다. 비밀번호가 비어 있으면 공개 방이다.</summary>
	public static void RequestCreate(string roomName, string password) =>
		Request(new RaidRoomRequestMessage { kind = RaidRoomRequestKind.Create, roomName = roomName, password = password });

	/// <summary>방 들어가기를 요청한다. 비밀번호 방이면 password가 맞아야 한다.</summary>
	public static void RequestJoin(RaidRoom room, string password) =>
		Request(new RaidRoomRequestMessage { kind = RaidRoomRequestKind.Join, roomId = room != null ? room.Id : 0, password = password });

	/// <summary>내 준비 상태 바꾸기를 요청한다. 파티장은 준비가 없어서 무시된다.</summary>
	public static void RequestSetReady(bool ready) =>
		Request(new RaidRoomRequestMessage { kind = RaidRoomRequestKind.SetReady, ready = ready });

	/// <summary>내가 있는 방에서 나가기를 요청한다. 방에 없으면 무시된다.</summary>
	public static void RequestLeave() => Request(new RaidRoomRequestMessage { kind = RaidRoomRequestKind.Leave });

	/// <summary>레이드 시작을 요청한다. 파티장만, 다른 파티원이 모두 준비했을 때만 된다.</summary>
	public static void RequestStart() => Request(new RaidRoomRequestMessage { kind = RaidRoomRequestKind.Start });

	static void Request(RaidRoomRequestMessage request)
	{
		if (RequestSender != null)
		{
			RequestSender(request);
			return;
		}
		if (!Process(request, MyName, out string reason)) NotifyFailed(reason);
	}

	/// <summary>
	/// 요청 하나를 memberName이 보낸 것으로 처리한다. 서버(RaidRoomsNetwork)와, 서버에 접속하지 않았을 때의 Request가 부른다.<br/>
	/// 방이 바뀌었으면 true. 실패하면 false와 이유(알릴 필요가 없으면 null).
	/// </summary>
	internal static bool Process(RaidRoomRequestMessage request, string memberName, out string reason)
	{
		reason = null;
		RaidRoom myRoom = FindRoomOf(memberName);
		switch (request.kind)
		{
			case RaidRoomRequestKind.Create:
				return TryCreate(request.roomName, request.password, memberName, out _, out reason);
			case RaidRoomRequestKind.Join:
				return TryJoin(FindById(request.roomId), request.password, memberName, out reason);
			case RaidRoomRequestKind.SetReady:
				if (myRoom == null) { reason = "방에 들어가 있지 않습니다."; return false; }
				SetReady(myRoom, memberName, request.ready);
				return true;
			case RaidRoomRequestKind.Leave:
				// 화면은 닫힐 때마다 나가기를 보내므로, 방에 없으면 알리지 않고 무시한다
				if (myRoom == null) return false;
				Leave(myRoom, memberName);
				return true;
			case RaidRoomRequestKind.Start:
				if (myRoom == null) { reason = "방에 들어가 있지 않습니다."; return false; }
				return TryStart(myRoom, memberName, out reason);
			default:
				return false;
		}
	}

	/// <summary>
	/// 요청이 거절되었다고 화면에 알린다. 이유가 null이면 알리지 않는다. 서버의 거절을 받은 RaidRoomsNetwork도 부른다.
	/// </summary>
	internal static void NotifyFailed(string reason)
	{
		if (reason != null) RequestFailed?.Invoke(reason);
	}

	// ── 클라이언트 사본 (RaidRoomsNetwork가 부른다) ──

	/// <summary>
	/// 서버가 알려 준 내 닉네임을 넣는다.
	/// </summary>
	internal static void SetServerName(string name) => serverName = name;

	/// <summary>
	/// 서버가 보낸 방 목록으로 바꾸고 RoomsChanged, RoomChanged(방마다)를 부른다.<br/>
	/// 같은 Id의 방은 같은 RaidRoom 객체를 그대로 쓴다. 화면이 들고 있는 방이 목록을 받을 때마다 다른 객체가 되지 않게 하기 위함이다.
	/// </summary>
	internal static void ApplyServerList(RaidRoomInfo[] infos)
	{
		var previous = new List<RaidRoom>(rooms);
		rooms.Clear();
		if (infos != null)
		{
			foreach (RaidRoomInfo info in infos)
			{
				RaidRoom room = previous.Find(r => r.Id == info.id) ?? new RaidRoom(info.id, info.name, info.hasPassword);
				room.members.Clear();
				for (int i = 0; i < info.memberNames.Length; i++)
					room.members.Add(new RaidMember(info.memberNames[i]) { IsReady = info.memberReady[i] });
				rooms.Add(room);
			}
		}

		RoomsChanged?.Invoke();
		for (int i = 0; i < rooms.Count; i++) RoomChanged?.Invoke(rooms[i]);
	}

	/// <summary>
	/// 방과 내 닉네임을 모두 비우고 RoomsChanged를 부른다. 서버를 끌 때, 클라이언트의 접속이 끊길 때 RaidRoomsNetwork가 부른다.
	/// </summary>
	internal static void Clear()
	{
		rooms.Clear();
		nextId = 1;
		serverName = null;
		RoomsChanged?.Invoke();
	}

	// ── 서버 코드가 부르는 함수 ──

	/// <summary>
	/// 방을 만들고 leaderName을 파티장으로 넣는다. 비밀번호가 비어 있으면 공개 방이다.
	/// </summary>
	public static bool TryCreate(string roomName, string password, string leaderName, out RaidRoom room, out string reason)
	{
		room = null;
		roomName = roomName?.Trim();
		if (string.IsNullOrEmpty(roomName)) { reason = "방 이름을 입력하세요."; return false; }
		if (string.IsNullOrEmpty(leaderName)) { reason = "이름이 비어 있습니다."; return false; }
		if (FindRoomOf(leaderName) != null) { reason = "이미 다른 방에 있습니다."; return false; }

		room = new RaidRoom(nextId++, roomName, password);
		room.members.Add(new RaidMember(leaderName));
		rooms.Add(room);
		reason = null;
		RoomsChanged?.Invoke();
		RoomChanged?.Invoke(room);
		return true;
	}

	/// <summary>
	/// memberName을 방에 넣는다. 비밀번호 방이면 password가 맞아야 한다.
	/// </summary>
	public static bool TryJoin(RaidRoom room, string password, string memberName, out string reason)
	{
		if (room == null || !rooms.Contains(room)) { reason = "없어진 방입니다."; return false; }
		if (string.IsNullOrEmpty(memberName)) { reason = "이름이 비어 있습니다."; return false; }
		if (FindRoomOf(memberName) != null) { reason = "이미 다른 방에 있습니다."; return false; }
		if (room.IsFull) { reason = "인원이 다 찼습니다."; return false; }
		if (!room.CheckPassword(password)) { reason = "비밀번호가 틀렸습니다."; return false; }

		room.members.Add(new RaidMember(memberName));
		reason = null;
		RoomsChanged?.Invoke();
		RoomChanged?.Invoke(room);
		return true;
	}

	/// <summary>
	/// memberName을 방에서 뺀다. 파티장이 나가면 다음 사람이 파티장이 되고(준비 해제), 아무도 없으면 방을 없앤다.
	/// </summary>
	public static void Leave(RaidRoom room, string memberName)
	{
		RaidMember member = room?.Find(memberName);
		if (member == null) return;

		room.members.Remove(member);
		if (room.members.Count == 0) rooms.Remove(room);
		else room.members[0].IsReady = false; // 새 파티장은 준비 상태가 없다

		RoomsChanged?.Invoke();
		RoomChanged?.Invoke(room);
	}

	/// <summary>
	/// 파티원의 준비 상태를 바꾼다. 파티장은 준비가 없어서 무시한다.
	/// </summary>
	public static void SetReady(RaidRoom room, string memberName, bool ready)
	{
		RaidMember member = room?.Find(memberName);
		if (member == null || member == room.Leader || member.IsReady == ready) return;

		member.IsReady = ready;
		RoomChanged?.Invoke(room);
	}

	/// <summary>
	/// 레이드를 시작한다. 파티장만, 다른 파티원이 모두 준비했을 때만 된다.
	/// 방을 목록에서 없애고 파티원 이름 목록(0번 파티장)을 RaidFlow.RequestStart에 넘긴다.
	/// </summary>
	public static bool TryStart(RaidRoom room, string requesterName, out string reason)
	{
		if (room == null || !rooms.Contains(room)) { reason = "없어진 방입니다."; return false; }
		if (room.Leader?.Name != requesterName) { reason = "파티장만 시작할 수 있습니다."; return false; }
		if (!room.AllReady) { reason = "모든 파티원이 준비해야 합니다."; return false; }

		string[] names = new string[room.members.Count];
		for (int i = 0; i < names.Length; i++) names[i] = room.members[i].Name;

		rooms.Remove(room);
		reason = null;
		RoomsChanged?.Invoke();
		RaidFlow.RequestStart(names);
		return true;
	}

	/// <summary>
	/// memberName이 들어가 있는 방. 없으면 null.
	/// </summary>
	public static RaidRoom FindRoomOf(string memberName)
	{
		for (int i = 0; i < rooms.Count; i++)
			if (rooms[i].Find(memberName) != null) return rooms[i];
		return null;
	}

	// 입장 요청은 방 번호로 오므로 번호로 찾는다. 없으면 null
	static RaidRoom FindById(int id)
	{
		for (int i = 0; i < rooms.Count; i++)
			if (rooms[i].Id == id) return rooms[i];
		return null;
	}
}
