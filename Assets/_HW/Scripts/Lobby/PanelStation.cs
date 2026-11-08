using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 가까이 가서 ↑를 누르면 창 하나를 여는 장소. 연습장의 편집 터미널(블럭코딩 편집 화면)과 장착대(슬롯 장착 화면)에 쓴다.
/// <para>
/// 사용법: 장소 오브젝트에 Is Trigger를 켠 Collider 2D와 이 컴포넌트를 붙이고, Panel 칸에 같은 씬의 창(InputBlockingPanel)을 넣는다.<br/>
/// - 창이 열린 동안 Player·Attack 액션 맵은 꺼진다 (InputBlockingPanel).<br/>
/// - 내 캐릭터가 다른 장소로 가면 놓아준다 (PlaceInteractable).<br/>
/// - 내 캐릭터가 그 창을 쓸 수 없으면(InputBlockingPanel.IsAvailableFor가 false) 내 화면에서 이 장소를 숨긴다. 예: 실제 계정의 장착대 (#470).
/// </para>
/// </summary>
public class PanelStation : PlaceInteractable
{
	[SerializeField, Tooltip("열 창. 같은 씬에 놓은 BlockEditorPanel, SlotEquipPanel 등")]
	InputBlockingPanel panel;

	// 장소 씬은 보통 내 캐릭터가 생긴 뒤에 열리지만, 순서가 바뀌어도 맞도록 내 캐릭터가 생길 때까지 기다린 뒤 한 번 정한다.
	// 네트워크를 켜지 않은 테스트와 서버(화면 없음)는 내 캐릭터가 없으므로 그대로 둔다
	IEnumerator Start()
	{
		if (panel == null) yield break;
		while (NetworkClient.localPlayer == null)
		{
			if (Application.isBatchMode || !NetworkClient.active) yield break;
			yield return null;
		}
		if (!NetworkClient.localPlayer.TryGetComponent(out PlayerInputReader player)) yield break;
		if (!panel.IsAvailableFor(player)) gameObject.SetActive(false);
	}

	protected override void Interact(PlayerInputReader player)
	{
		if (panel == null)
		{
			Debug.LogError($"[PanelStation] {name}: Panel이 비어 있음", this);
			return;
		}
		if (!panel.IsAvailableFor(player)) return;
		panel.OpenFor(player);
	}
}
