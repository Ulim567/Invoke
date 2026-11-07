using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어가 지금 어느 장소에 있는지 읽는 도구. 이우림의 PlayerPublicInfo.Place를 읽기만 한다 (#362).
/// <para>
/// 클라이언트에서는 Mirror가 플레이어를 늘 기준 씬(Base)에 만들어서 player.scene.name으로는 장소를 알 수 없다.
/// 그래서 HUD·설정·레벨업 창 등 화면은 장소를 이것으로 본다. 서버가 아직 장소를 정하지 않았으면 빈 문자열.
/// </para>
/// </summary>
public static class PlayerPlace
{
	/// <summary>그 플레이어의 장소 씬 이름 (PlaceScenes.Lobby 등). 모르면 ""</summary>
	public static string Of(GameObject player) =>
		player != null && player.TryGetComponent(out PlayerPublicInfo info) ? info.Place : "";

	/// <summary>내 캐릭터의 장소 씬 이름. 내 캐릭터가 없으면 ""</summary>
	public static string Mine => Of(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null);
}
