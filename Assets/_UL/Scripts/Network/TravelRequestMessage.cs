using Mirror;

/// <summary>
/// 클라이언트가 서버에 장소 이동을 요청하는 메시지. PlaceTravelClient가 보내고, InvokeNetworkManager가 받는다.
/// <para>
/// - 입구·출구 모두 이 메시지 하나로 요청한다. 레이드 던전은 서버가 거절한다. 파티장이 시작할 때 서버가 옮겨 준다(TravelStartedMessage).<br/>
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

/// <summary>
/// 서버가 클라이언트에 "서버가 너를 옮기기 시작한다"고 알리는 메시지. RaidParties가 보내고, PlaceTravelClient가 받는다.
/// <para>
/// - 레이드 시작(로비 → 던전)과 레이드 끝(던전 → 로비)처럼 클라이언트가 요청하지 않은 이동에 쓴다.<br/>
/// - 받은 클라이언트는 RequestTravel과 같이 화면을 막고, 로딩 화면이 다 덮으면 TravelReadyMessage로 답한다.<br/>
/// - 끝은 다른 이동과 같이 TravelEndedMessage로 온다. 서버가 이동을 취소할 때도 TravelEndedMessage만 온다.
/// </para>
/// </summary>
public struct TravelStartedMessage : NetworkMessage
{
}

/// <summary>
/// TravelStartedMessage를 받은 클라이언트가 로딩 화면이 화면을 다 덮었다고 서버에 답하는 메시지. PlaceTravelClient가 보내고, RaidParties가 받는다.
/// </summary>
// 서버가 이 답을 기다렸다가 옮기는 이유: 먼저 옮기면 이전 장소가 사라지는 모습이 페이드 사이로 보인다 (RequestTravel이 덮은 뒤 요청하는 것과 같은 이유)
public struct TravelReadyMessage : NetworkMessage
{
}
