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
	public bool HasPassword => !string.IsNullOrEmpty(password);

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
/// - 지금은 멀티 없이 내 컴퓨터 안에서만 도는 임시 버전이다. 나중에 이우림이 이 안쪽을 네트워크(서버)로 바꾸고, UI(RaidLobbyUI)는 그대로 쓴다.<br/>
/// - 파티원은 이름으로 구분한다. 인원은 최대 MaxMembers(3)명이다.<br/>
/// - 시작하면 방을 목록에서 없애고, 파티원 이름 목록을 RaidFlow.RequestStart(레이드 시작)에 넘긴다.
/// </para>
/// <para>
/// 사용법: 함수를 부르고, 화면은 RoomsChanged(목록이 바뀜)·RoomChanged(한 방의 파티원이 바뀜)를 받아 다시 그린다.
/// 실패한 함수는 false와 함께 이유(화면에 보여 줄 문장)를 돌려준다.
/// </para>
/// </summary>
public static class RaidRooms
{
	/// <summary>파티 최대 인원</summary>
	public const int MaxMembers = 3;

	static readonly List<RaidRoom> rooms = new List<RaidRoom>();
	static int nextId = 1;

	/// <summary>지금 있는 방 목록 (만든 순서)</summary>
	public static IReadOnlyList<RaidRoom> Rooms => rooms;

	/// <summary>방이 생기거나 없어지거나 방 인원이 바뀌었을 때. 방 목록 화면을 다시 그린다</summary>
	public static event Action RoomsChanged;

	/// <summary>한 방의 파티원·준비 상태가 바뀌었을 때. 바뀐 방을 넘긴다. 파티방 화면을 다시 그린다</summary>
	public static event Action<RaidRoom> RoomChanged;

	// Play를 다시 눌렀을 때 이전 방이 남지 않게 비운다 (도메인 리로드를 꺼 둔 에디터 대비)
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		rooms.Clear();
		nextId = 1;
		RoomsChanged = null;
		RoomChanged = null;
	}

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
}
