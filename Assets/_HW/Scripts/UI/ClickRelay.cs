using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 버튼을 누르면 Target 버튼을 누른 것과 같게 한다. 다른 사람 화면의 버튼과 똑같이 동작하는 버튼을 하나 더 둘 때 쓴다
/// (예: 레이드 파티방의 X가 방 목록의 닫기 버튼(RaidLobbyUI, 닫으면서 방에서 나감)과 같게).
/// </summary>
[RequireComponent(typeof(Button))]
public class ClickRelay : MonoBehaviour
{
	[SerializeField] Button target;

	void Awake() => GetComponent<Button>().onClick.AddListener(() => { if (target != null) target.onClick.Invoke(); });
}
