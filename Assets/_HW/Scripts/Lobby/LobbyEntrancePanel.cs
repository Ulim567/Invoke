using TMPro;
using UnityEngine;

/// <summary>
/// 로비 입구 창. 「들어가기」와 「닫기」 버튼이 있다. Lobby 씬에 하나만 두고 모든 입구가 같이 쓴다.
/// <para>
/// 버튼 연결 (Inspector의 On Click())<br/>
/// - 들어가기: LobbyEntrancePanel.Enter<br/>
/// - 닫기: LobbyEntrancePanel.Close
/// </para>
/// </summary>
public class LobbyEntrancePanel : InputBlockingPanel
{
	[SerializeField] TMP_Text titleText;
	[SerializeField] TMP_Text descriptionText;

	LobbyEntrance current;

	/// <summary>
	/// 입구 창을 연다. LobbyEntrance가 부른다.
	/// </summary>
	public void Open(LobbyEntrance entrance, PlayerInputReader player)
	{
		if (IsOpen) return;
		current = entrance;
		if (titleText != null) titleText.text = entrance.DisplayName;
		if (descriptionText != null) descriptionText.text = entrance.Description;
		Show(player);
	}

	/// <summary>
	/// 「들어가기」 버튼. 이동 요청을 알리고 창을 닫는다.
	/// </summary>
	public void Enter()
	{
		LobbyEntrance entrance = current;
		Close();
		current = null;
		if (entrance != null) entrance.RequestEnter();
	}
}
