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
		// 이동하는 동안은 PlaceTravelClient가 Player 액션 맵을 비활성화해 두므로 InteractPressed가 false다. 요청이 두 번 가지 않는다
		if (localPlayer == null || !localPlayer.InteractPressed) return;

		if (PlaceTravelClient.Instance == null)
		{
			Debug.LogWarning("[PlaceExit] 기준 씬에 PlaceTravelClient가 없어서 로비로 이동할 수 없습니다.");
			return;
		}
		PlaceTravelClient.Instance.RequestTravel(PlaceScenes.Lobby);
	}
}
