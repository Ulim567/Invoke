using UnityEngine;

/// <summary>
/// 로비에서 내 캐릭터가 가까이 와서 ↑(상호작용)를 누르면 반응하는 오브젝트의 공통 부분. 입구, 스킬 트리 해금 장소가 물려받는다.
/// <para>
/// - 이 오브젝트에 Is Trigger를 켠 Collider 2D를 붙인다. 그 범위 안에 내 캐릭터가 들어오면 ↑ 안내(prompt)를 보여 준다.<br/>
/// - 내 캐릭터(isLocalPlayer)만 반응한다. 다른 플레이어가 들어와도 내 화면의 안내는 켜지지 않는다.<br/>
/// - 키는 PlayerInputReader.InteractPressed로만 읽는다 (CLAUDE.md 코드 작성 규칙 5).
/// </para>
/// </summary>
public abstract class LobbyInteractable : MonoBehaviour
{
	[SerializeField, Tooltip("내 캐릭터가 범위 안에 있을 때 켜지는 ↑ 안내. 비워 두면 안내 없음")]
	GameObject prompt;

	PlayerInputReader localPlayer;

	void Awake()
	{
		if (prompt != null) prompt.SetActive(false);
	}

	void OnTriggerEnter2D(Collider2D other)
	{
		PlayerInputReader reader = other.GetComponentInParent<PlayerInputReader>();
		if (reader == null || !reader.isLocalPlayer) return;
		localPlayer = reader;
		if (prompt != null) prompt.SetActive(true);
	}

	void OnTriggerExit2D(Collider2D other)
	{
		PlayerInputReader reader = other.GetComponentInParent<PlayerInputReader>();
		if (reader == null || reader != localPlayer) return;
		localPlayer = null;
		if (prompt != null) prompt.SetActive(false);
	}

	void Update()
	{
		// 창이 열려 있는 동안은 Player 액션 맵이 꺼져 있어서 InteractPressed가 false이므로, 같은 창이 또 열리지 않는다
		if (localPlayer != null && localPlayer.InteractPressed) Interact(localPlayer);
	}

	/// <summary>
	/// 범위 안에서 ↑를 눌렀을 때 불린다. 물려받는 쪽이 창을 연다.
	/// </summary>
	protected abstract void Interact(PlayerInputReader player);
}
