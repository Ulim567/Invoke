using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 열려 있는 동안 내 캐릭터의 Player·Attack 액션 맵을 끄는 창의 공통 부분. 로비 입구 창, 스킬 트리 창이 물려받는다.
/// <para>
/// - 두 맵을 다 끄는 이유 (이우림과 정함): 슬롯 키가 눌리지 않아야 SlotCaster가 대상 고르기를 끝내면서 Player 맵을 다시 켜는 일이 생기지 않는다.<br/>
/// - 닫을 때 다시 켠다. 창이 꺼지거나 사라질 때도 켠다 (꺼진 채로 남지 않게).<br/>
/// - 창 그림은 panelRoot 아래에 둔다. 이 컴포넌트가 붙은 오브젝트는 늘 켜 둔다.<br/>
/// - 열린 창이 하나라도 있으면 AnyOpen이 true. 게임 HUD는 HudHidden을 보고 창이 열린 동안 숨는다 (#324). 설정 창, 로비 창(입구, 스킬 트리, #431)처럼 HUD를 남기는 창은 HidesHud를 false로 덮어쓴다. 그동안 HUD는 누를 수 없다.<br/>
/// - ESC·Enter (#324): panelRoot에 UIShortcut이 없으면 시작할 때 붙인다. ESC는 OnCancelKey(기본: 닫기), Enter는 OnSubmitKey(기본: 아무것도 안 함).
///   창마다 다르게 하려면 덮어쓴다 (예: 로비 입구 창의 Enter = 들어가기). 맨 위 창 하나만 반응한다 (UIShortcut 규칙).
/// </para>
/// </summary>
public abstract class InputBlockingPanel : MonoBehaviour
{
	[SerializeField, Tooltip("열고 닫을 창. 이 오브젝트의 자식으로 두고, 처음에는 꺼 둔다")]
	GameObject panelRoot;

	PlayerInputReader blockedPlayer;

	// 지금 열린 창들. 창을 다른 방법으로 닫아도 맞도록 AnyOpen을 읽을 때 IsOpen으로 다시 거른다
	static readonly HashSet<InputBlockingPanel> openPanels = new HashSet<InputBlockingPanel>();

	// Play 모드에 들어갈 때 도메인 다시 불러오기를 꺼 두었으므로 static 값을 직접 비운다
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => openPanels.Clear();

	/// <summary>[표시] 이 컴포넌트를 쓰는 창(로비 입구, 스킬 트리, 편집, 장착, 레이드 방 등)이 하나라도 열려 있으면 true.</summary>
	public static bool AnyOpen
	{
		get
		{
			openPanels.RemoveWhere(panel => panel == null || !panel.IsOpen);
			return openPanels.Count > 0;
		}
	}

	/// <summary>[표시] HUD를 숨기는 창(HidesHud)이 하나라도 열려 있으면 true. 게임 HUD가 읽는다.</summary>
	public static bool HudHidden
	{
		get
		{
			if (!AnyOpen) return false;
			foreach (InputBlockingPanel panel in openPanels)
				if (panel.HidesHud) return true;
			return false;
		}
	}

	/// <summary>열려 있는 동안 게임 HUD를 숨기는지. 기본 true (창 뒤로 HUD가 비쳐 보이지 않게). 설정 창은 false</summary>
	protected virtual bool HidesHud => true;

	/// <summary>창이 열려 있으면 true.</summary>
	public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

	void Awake()
	{
		if (panelRoot == null) return;
		panelRoot.SetActive(false);

		if (!panelRoot.TryGetComponent(out UIShortcut shortcut)) shortcut = panelRoot.AddComponent<UIShortcut>();
		shortcut.OnCancel.AddListener(OnCancelKey);
		shortcut.OnSubmit.AddListener(OnSubmitKey);
	}

	/// <summary>창이 맨 위에 열려 있을 때 ESC. 기본은 닫기. 닫을 때 할 일이 더 있는 창은 덮어쓴다.</summary>
	protected virtual void OnCancelKey() => Close();

	/// <summary>창이 맨 위에 열려 있을 때 Enter. 기본은 아무것도 안 함 (버튼이 선택되어 있으면 그 버튼이 먼저 받는다).</summary>
	protected virtual void OnSubmitKey() { }

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
		openPanels.Add(this);
	}

	/// <summary>
	/// 창을 연다. PanelStation(연습장의 편집 터미널·장착대 등)이 부른다. 열 때 할 일이 있는 창은 덮어쓴다.
	/// </summary>
	public virtual void OpenFor(PlayerInputReader player) => Show(player);

	/// <summary>
	/// 창을 닫고 꺼 둔 액션 맵을 다시 켠다. 닫기 버튼의 On Click()에 연결한다.
	/// 닫기 전에 물어야 하는 창(편집 화면: 저장하지 않은 변경)은 덮어쓰고, 닫을 때 base.Close()를 부른다.
	/// </summary>
	public virtual void Close()
	{
		if (panelRoot != null) panelRoot.SetActive(false);
		openPanels.Remove(this);
		Unblock();
	}

	void Unblock()
	{
		if (blockedPlayer == null) return;
		blockedPlayer.SetPlayerMapEnabled(true);
		blockedPlayer.SetAttackMapEnabled(true);
		blockedPlayer = null;
	}

	void OnDisable()
	{
		openPanels.Remove(this);
		Unblock();
	}
}
