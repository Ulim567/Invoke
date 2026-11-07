using UnityEngine;

/// <summary>
/// 사냥터·연습장의 출구. 로컬 플레이어가 가까이 와서 ↑(상호작용)를 누르면 로비로 이동을 요청한다.
/// <para>
/// - 트리거 범위, ↑ 안내(prompt), 로컬 플레이어 판별, 검 타기·사망·경직 중 ↑ 무시는 부모 PlaceInteractable이 한다.<br/>
/// - 이 클래스는 ↑를 눌렀을 때 할 일만 정한다. 요청은 기준 씬의 PlaceTravelClient가 서버에 보낸다.<br/>
/// - 이동하는 동안은 PlaceTravelClient가 Player 액션 맵을 비활성화해 두므로 InteractPressed가 false다. 요청이 두 번 가지 않는다.
/// </para>
/// </summary>
public class PlaceExit : PlaceInteractable
{
	protected override void Interact(PlayerInputReader player)
	{
		if (PlaceTravelClient.Instance == null)
		{
			Debug.LogWarning("[PlaceExit] 기준 씬에 PlaceTravelClient가 없어서 로비로 이동할 수 없습니다.");
			return;
		}
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
	}
}
