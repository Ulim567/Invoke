using Mirror;

/// <summary>
/// 스킬 트리 창. 안의 화면은 SkillTreeView가 그린다.
/// <para>
/// 열린 동안 내 캐릭터의 Player·Attack 액션 맵이 꺼진다.
/// 닫기 버튼의 On Click()에 SkillTreePanel.Close를 연결한다.<br/>
/// 여는 법: HUD의 「스킬 트리」 버튼(HudView.SkillTreeRequested). 로비의 스킬 트리 장소는 없앴다 (#378).
/// 이 창은 Lobby 씬에 있으므로 HUD 버튼은 내 캐릭터가 로비에 있을 때만 켜진다.
/// </para>
/// </summary>
public class SkillTreePanel : InputBlockingPanel
{
	void Start() => HudView.SkillTreeRequested += OpenForLocalPlayer;

	void OnDestroy() => HudView.SkillTreeRequested -= OpenForLocalPlayer;

	/// <summary>
	/// 플레이어를 정해서 스킬 트리 창을 연다.
	/// </summary>
	public void Open(PlayerInputReader player) => Show(player);

	// HUD의 「스킬 트리」 버튼: 내 캐릭터로 연다
	void OpenForLocalPlayer()
	{
		// 내 캐릭터가 로비에 있을 때만 연다 (호스트는 모든 장소 씬을 띄워 둠. 장소는 PlayerPublicInfo.Place, #362)
		if (IsOpen || NetworkClient.localPlayer == null || PlayerPlace.Mine != PlaceScenes.Lobby) return;
		Show(NetworkClient.localPlayer.GetComponentInChildren<PlayerInputReader>());
	}
}
