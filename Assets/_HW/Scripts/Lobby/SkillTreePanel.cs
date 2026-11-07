using Mirror;

/// <summary>
/// 스킬 트리 창. 안의 화면은 SkillTreeView가 그린다.
/// <para>
/// 열린 동안 내 캐릭터의 Player·Attack 액션 맵이 꺼진다.
/// 닫기 버튼의 On Click()에 SkillTreePanel.Close를 연결한다.<br/>
/// 여는 법: 로비의 스킬 트리 단말(SkillTreeStation), HUD의 「스킬 트리」 버튼(HudView.SkillTreeRequested).
/// 이 창은 Lobby 씬에 있으므로 HUD 버튼은 내 캐릭터가 로비에 있을 때만 켜진다.
/// </para>
/// </summary>
public class SkillTreePanel : InputBlockingPanel
{
	void Start() => HudView.SkillTreeRequested += OpenForLocalPlayer;

	void OnDestroy() => HudView.SkillTreeRequested -= OpenForLocalPlayer;

	/// <summary>
	/// 스킬 트리 창을 연다. SkillTreeStation이 부른다.
	/// </summary>
	public void Open(PlayerInputReader player) => Show(player);

	// HUD의 「스킬 트리」 버튼: 내 캐릭터로 연다
	void OpenForLocalPlayer()
	{
		// 호스트는 모든 장소 씬을 띄워 두므로, 내 캐릭터가 이 창과 같은 씬(로비)에 있을 때만 연다
		if (IsOpen || NetworkClient.localPlayer == null || NetworkClient.localPlayer.gameObject.scene != gameObject.scene) return;
		Show(NetworkClient.localPlayer.GetComponentInChildren<PlayerInputReader>());
	}
}
