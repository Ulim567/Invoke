using UnityEngine;

/// <summary>
/// 화면 전체를 덮어, 장소 씬을 언로드하고 로드하는 모습을 가리는 로딩 화면. 네트워크는 쓰지 않는다.
/// <para>
/// - PlaceTravelClient만 Show, Hide를 부른다.<br/>
/// - 화면을 덮는 Image의 Raycast Target을 켜 두면, 로딩 화면이 떠 있는 동안 뒤의 UI 버튼이 눌리지 않는다.
/// </para>
/// </summary>
public class LoadingScreen : MonoBehaviour
{
	[SerializeField, Tooltip("화면 전체를 덮는 UI의 부모. 로딩 화면을 보일 때만 활성화한다")]
	GameObject panelRoot;

	void Awake()
	{
		// 게임을 시작할 때는 보이지 않게 한다
		Hide();
	}

	/// <summary>
	/// 로딩 화면을 보인다.
	/// </summary>
	public void Show() => panelRoot.SetActive(true);

	/// <summary>
	/// 로딩 화면을 숨긴다.
	/// </summary>
	public void Hide() => panelRoot.SetActive(false);
}
