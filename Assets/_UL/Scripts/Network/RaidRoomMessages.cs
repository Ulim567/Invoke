using Mirror;

/// <summary>
/// 레이드 방 요청의 종류. RaidRoomRequestMessage.kind에 담는다.
/// </summary>
public enum RaidRoomRequestKind : byte
{
	Create,
	Join,
	SetReady,
	Leave,
	Start,
}

/// <summary>
/// 클라이언트가 서버에 레이드 방 요청을 보내는 메시지. RaidRooms의 Request… 함수가 만들고, RaidRoomsNetwork가 보내고 받는다.
/// <para>
/// - 요청한 사람의 이름은 담지 않는다. 서버가 요청이 온 연결의 닉네임(AccountSession.Nickname)으로 안다.<br/>
/// - 종류마다 쓰는 필드만 채운다. Create: roomName, password / Join: roomId, password / SetReady: ready / Leave, Start: 없음<br/>
/// - 서버의 답: 성공하면 모두에게 RaidRoomListMessage, 실패하면 요청한 클라이언트에만 RaidRoomFailedMessage.
/// </para>
/// </summary>
// 요청마다 메시지를 따로 두지 않는 이유: 다섯 요청 모두 서버가 하는 일(검사 → 성공이면 목록 보내기, 실패면 이유 보내기)이 같기 때문
public struct RaidRoomRequestMessage : NetworkMessage
{
	public RaidRoomRequestKind kind;
	public int roomId;
	public string roomName;
	public string password;
	public bool ready;
}

/// <summary>
/// 서버가 레이드 방 요청을 거절했을 때 요청한 클라이언트에 이유를 알리는 메시지. RaidRoomsNetwork가 보내고 받는다.
/// </summary>
public struct RaidRoomFailedMessage : NetworkMessage
{
	/// <summary>화면에 보여 줄 이유. 예: "비밀번호가 틀렸습니다."</summary>
	public string reason;
}

/// <summary>
/// 서버가 클라이언트에 보내는 레이드 방 목록 전체. RaidRoomsNetwork가 보내고 받는다.
/// <para>
/// - 방이 바뀔 때마다 게임에 들어온 모든 클라이언트에, 게임에 들어올 때 그 클라이언트에 한 번 보낸다.<br/>
/// - 받는 사람마다 myName에 그 사람의 닉네임을 담는다. 로그인 건너뛰기 임시 계정의 닉네임은 서버만 정하기 때문이다.
/// </para>
/// </summary>
public struct RaidRoomListMessage : NetworkMessage
{
	public string myName;
	public RaidRoomInfo[] rooms;
}

/// <summary>
/// RaidRoomListMessage에 담는 방 하나. 비밀번호는 담지 않고 걸려 있는지만 담는다.
/// </summary>
public struct RaidRoomInfo
{
	public int id;
	public string name;
	public bool hasPassword;
	/// <summary>파티원 이름. 0번이 파티장</summary>
	public string[] memberNames;
	/// <summary>memberNames와 같은 순서의 준비 상태</summary>
	public bool[] memberReady;
}
