using UnityEngine;

/// <summary>
/// 로비에서 내 캐릭터가 가까이 와서 ↑(상호작용)를 누르면 반응하는 오브젝트의 공통 부분. 입구, 스킬 트리 해금 장소가 물려받는다.
/// <para>
/// - 이 오브젝트에 Is Trigger를 켠 Collider 2D를 붙인다. 그 범위 안에 내 캐릭터가 들어오면 ↑ 안내(prompt)를 보여 준다.<br/>
/// - 내 캐릭터(isLocalPlayer)만 반응한다. 다른 플레이어가 들어와도 내 화면의 안내는 켜지지 않는다.<br/>
/// - 키는 PlayerInputReader.InteractPressed로만 읽는다 (CLAUDE.md 코드 작성 규칙 5).<br/>
/// - 내 캐릭터가 다른 장소로 가면 놓아준다. 호스트에서는 로비 씬이 남아 있어도 OnTriggerExit2D가 불리지 않기 때문이다 (이우림 PlaceExit와 같은 방식).
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
		if (localPlayer == null) return;

		// 호스트에서는 내 캐릭터가 다른 장소로 가도 로비 씬이 서버에 남아 있고, 다른 물리 세계로 옮겨진 캐릭터에는 OnTriggerExit2D가 불리지 않는다
		// 놓아주지 않으면 다른 장소에서 ↑를 누를 때 로비 창이 열리므로, 내 캐릭터가 이 물리 세계를 떠났으면 여기서 놓아준다
		// 씬이 아니라 물리 세계로 비교하는 이유: 클라이언트에서는 플레이어가 기준 씬(Base)에 있어서 씬이 늘 다르지만, 로비와 Base는 같은 물리 세계라 트리거는 정상으로 동작한다
		if (localPlayer.gameObject.scene.GetPhysicsScene2D() != gameObject.scene.GetPhysicsScene2D())
		{
			localPlayer = null;
			if (prompt != null) prompt.SetActive(false);
			return;
		}

		// 창이 열려 있는 동안은 Player 액션 맵이 꺼져 있어서 InteractPressed가 false이므로, 같은 창이 또 열리지 않는다
		if (localPlayer.InteractPressed) Interact(localPlayer);
	}

	/// <summary>
	/// 범위 안에서 ↑를 눌렀을 때 불린다. 물려받는 쪽이 창을 연다.
	/// </summary>
	protected abstract void Interact(PlayerInputReader player);
}
