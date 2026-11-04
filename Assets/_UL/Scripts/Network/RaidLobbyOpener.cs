using UnityEngine;

/// <summary>
/// 로비의 레이드 입구에서 ↑를 누르면 레이드 방 화면(RaidLobbyUI)을 연다.
/// <para>
/// - LobbyEntrance.RaidEntryRequested를 받아 RaidLobbyUI.OpenFor(↑를 누른 내 캐릭터)를 부른다. 화면이 열린 동안 그 캐릭터의 액션 맵이 비활성화된다.<br/>
/// - 레이드 던전으로 옮기는 일은 하지 않는다. 파티장이 시작하면 서버의 RaidFlow.StartRequested를 받는 쪽(다음 이슈)이 한다.
/// </para>
/// <para>
/// 사용법: Lobby 씬에 두고, Raid Lobby 칸에 같은 씬의 RaidLobbyUI를 연결한다.
/// </para>
/// </summary>
// PlaceTravelClient(Base 씬)가 받지 않는 이유: Inspector에서 다른 씬(Lobby)의 RaidLobbyUI를 연결할 수 없기 때문
public class RaidLobbyOpener : MonoBehaviour
{
	[SerializeField, Tooltip("같은 Lobby 씬에 놓은 레이드 방 화면")]
	RaidLobbyUI raidLobby;

	void OnEnable() => LobbyEntrance.RaidEntryRequested += Open;

	void OnDisable() => LobbyEntrance.RaidEntryRequested -= Open;

	void Open(PlayerInputReader player)
	{
		if (raidLobby == null)
		{
			Debug.LogError($"[RaidLobbyOpener] {name}: Raid Lobby가 비어 있음", this);
			return;
		}
		raidLobby.OpenFor(player);
	}
}
