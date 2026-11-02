using Mirror;

/// <summary>
/// 클라이언트가 서버에 장소 이동을 요청하는 메시지. PlaceTravelClient가 보내고, InvokeNetworkManager가 받는다.
/// <para>
/// - 입구·출구 모두 이 메시지 하나로 요청한다. 레이드 던전 입구는 destination에 PlaceScenes.RaidDungeon을 담는다.<br/>
/// - 지금 있는 장소는 보내지 않는다. 서버가 플레이어 오브젝트의 씬으로 안다.<br/>
/// - 서버의 답은 TravelEndedMessage로 온다.
/// </para>
/// </summary>
// [Command] 대신 메시지를 쓰는 이유: [Command]는 플레이어 프리팹의 NetworkBehaviour 안에 있어야 해서 공용 Player.prefab을 고쳐야 하기 때문
public struct TravelRequestMessage : NetworkMessage
{
	/// <summary>
	/// 갈 씬 이름 (PlaceScenes의 Lobby, HuntingGround, TrainingRoom, RaidDungeon).
	/// </summary>
	public string destination;
}

/// <summary>
/// 서버가 이동을 요청한 클라이언트에 이동이 끝났다고 알리는 메시지. InvokeNetworkManager가 보내고, PlaceTravelClient가 받는다.
/// <para>
/// - 서버가 요청을 거절했을 때와, 플레이어를 옮기고 순간이동(teleport)까지 보낸 뒤에 보낸다.<br/>
/// - 두 경우 모두 클라이언트가 할 일(로딩 화면 비활성화, Player·Attack 액션 맵 활성화)이 같으므로 필드가 없다. 거절한 이유는 서버가 로그로 남긴다.
/// </para>
/// </summary>
// 클라이언트 코드는 RpcTeleport를 받은 때를 알 수 없다(NetworkTransformBase 안에서 처리되고 알리는 이벤트가 없음).
// 같은 연결로 보낸 메시지는 보낸 순서대로 처리되므로, teleport 뒤에 이 메시지를 보내면 클라이언트는 teleport 다음에 받는다
public struct TravelEndedMessage : NetworkMessage
{
}
