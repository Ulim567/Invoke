using Mirror;

/// <summary>
/// 서버가 레이드 결과(클리어·실패)를 파티원 클라이언트에 알리는 메시지. RaidParties가 보내고 받는다.
/// <para>
/// - 서버의 RaidJudge가 결과 화면 이벤트(ResultEffectPlayed)를 부르는 순간에 보낸다.<br/>
/// - 호스트의 클라이언트에는 보내지 않는다. 서버의 RaidJudge를 같이 써서 이미 결과 화면이 떴기 때문이다.
/// </para>
/// </summary>
// RaidJudge에 [ClientRpc]를 달지 않는 이유: 씬에 놓인 오브젝트라 NetworkIdentity를 붙이면 겹쳐 불러온 던전 씬에서 Mirror가 켜지 않는다 (NetworkServer.SpawnObjects를 부르지 않음)
public struct RaidResultMessage : NetworkMessage
{
	/// <summary>클리어면 true, 실패면 false</summary>
	public bool cleared;
}

/// <summary>
/// 클라이언트가 서버에 "레이드를 포기한다"고 알리는 메시지. RaidParties.RequestGiveUp이 보내고, 서버의 RaidParties가 받는다.
/// <para>
/// - 서버는 보낸 사람이 싸우는 중인 레이드 던전에 있는 파티원일 때만 그 레이드를 실패로 끝낸다. 아니면 무시한다.
/// </para>
/// </summary>
public struct RaidGiveUpMessage : NetworkMessage
{
}
