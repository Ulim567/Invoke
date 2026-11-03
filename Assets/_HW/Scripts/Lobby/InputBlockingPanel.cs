using UnityEngine;

/// <summary>
/// 열려 있는 동안 내 캐릭터의 Player·Attack 액션 맵을 끄는 창의 공통 부분. 로비 입구 창, 스킬 트리 창이 물려받는다.
/// <para>
/// - 두 맵을 다 끄는 이유 (이우림과 정함): 슬롯 키가 눌리지 않아야 SlotCaster가 대상 고르기를 끝내면서 Player 맵을 다시 켜는 일이 생기지 않는다.<br/>
/// - 닫을 때 다시 켠다. 창이 꺼지거나 사라질 때도 켠다 (꺼진 채로 남지 않게).<br/>
/// - 창 그림은 panelRoot 아래에 둔다. 이 컴포넌트가 붙은 오브젝트는 늘 켜 둔다.
/// </para>
/// </summary>
public abstract class InputBlockingPanel : MonoBehaviour
{
	[SerializeField, Tooltip("열고 닫을 창. 이 오브젝트의 자식으로 두고, 처음에는 꺼 둔다")]
	GameObject panelRoot;

	PlayerInputReader blockedPlayer;

	/// <summary>창이 열려 있으면 true.</summary>
	public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

	void Awake()
	{
		if (panelRoot != null) panelRoot.SetActive(false);
	}

	/// <summary>
	/// 창을 열고, 그 플레이어의 Player·Attack 액션 맵을 끈다.
	/// </summary>
	protected void Show(PlayerInputReader player)
	{
		if (IsOpen) return;
		blockedPlayer = player;
		if (blockedPlayer != null)
		{
			blockedPlayer.SetPlayerMapEnabled(false);
			blockedPlayer.SetAttackMapEnabled(false);
		}
		panelRoot.SetActive(true);
	}

	/// <summary>
	/// 창을 연다. PanelStation(연습장의 편집 터미널·장착대 등)이 부른다. 열 때 할 일이 있는 창은 덮어쓴다.
	/// </summary>
	public virtual void OpenFor(PlayerInputReader player) => Show(player);

	/// <summary>
	/// 창을 닫고 꺼 둔 액션 맵을 다시 켠다. 닫기 버튼의 On Click()에 연결한다.
	/// </summary>
	public void Close()
	{
		if (panelRoot != null) panelRoot.SetActive(false);
		Unblock();
	}

	void Unblock()
	{
		if (blockedPlayer == null) return;
		blockedPlayer.SetPlayerMapEnabled(true);
		blockedPlayer.SetAttackMapEnabled(true);
		blockedPlayer = null;
	}

	void OnDisable() => Unblock();
}
