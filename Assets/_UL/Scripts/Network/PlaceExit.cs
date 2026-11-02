using UnityEngine;

/// <summary>
/// 사냥터·연습장·레이드 던전의 출구. 로컬 플레이어가 가까이 와서 ↑(상호작용)를 누르면 로비로 이동을 요청한다.
/// <para>
/// - 이 오브젝트에 Is Trigger를 체크한 Collider 2D를 붙인다. 그 범위 안에 로컬 플레이어가 들어오면 ↑ 안내(prompt)를 보여 준다.<br/>
/// - 로컬 플레이어(isLocalPlayer)에만 반응한다. 서버의 플레이어 오브젝트와 원격 플레이어에는 반응하지 않는다.<br/>
/// - 키는 PlayerInputReader.InteractPressed로만 읽는다 (CLAUDE.md 코드 작성 규칙 5).<br/>
/// - 요청은 기준 씬의 PlaceTravelClient가 서버에 보낸다.
/// </para>
/// </summary>
public class PlaceExit : MonoBehaviour
{
	[SerializeField, Tooltip("로컬 플레이어가 범위 안에 있을 때 활성화되는 ↑ 안내. 비워 두면 안내 없음")]
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

		// 호스트에서는 플레이어가 다른 장소로 가도 이 씬이 서버에 남아 있고, 다른 씬의 물리 세계로 옮겨진 플레이어에는 OnTriggerExit2D가 불리지 않는다
		// 놓아주지 않으면 다른 장소에서 ↑를 누를 때마다 이 출구도 로비 이동을 요청하므로, 로컬 플레이어가 이 물리 세계를 떠났으면 여기서 놓아준다
		// 씬이 아니라 물리 세계로 비교하는 이유: 클라이언트에서는 Mirror가 플레이어를 기준 씬(Base)에 만들어서 씬이 늘 다르지만,
		// 장소 씬과 Base가 같은 기본 물리 세계에 있어 트리거는 정상으로 동작한다
		if (localPlayer.gameObject.scene.GetPhysicsScene2D() != gameObject.scene.GetPhysicsScene2D())
		{
			localPlayer = null;
			if (prompt != null) prompt.SetActive(false);
			return;
		}

		// 이동하는 동안은 PlaceTravelClient가 Player 액션 맵을 비활성화해 두므로 InteractPressed가 false다. 요청이 두 번 가지 않는다
		if (!localPlayer.InteractPressed) return;

		if (PlaceTravelClient.Instance == null)
		{
			Debug.LogWarning("[PlaceExit] 기준 씬에 PlaceTravelClient가 없어서 로비로 이동할 수 없습니다.");
			return;
		}
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
	}
}
