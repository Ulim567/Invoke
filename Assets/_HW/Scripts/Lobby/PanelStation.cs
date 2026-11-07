using UnityEngine;

/// <summary>
/// 가까이 가서 ↑를 누르면 창 하나를 여는 장소. 연습장의 편집 터미널(블럭코딩 편집 화면)과 장착대(슬롯 장착 화면)에 쓴다.
/// <para>
/// 사용법: 장소 오브젝트에 Is Trigger를 켠 Collider 2D와 이 컴포넌트를 붙이고, Panel 칸에 같은 씬의 창(InputBlockingPanel)을 넣는다.<br/>
/// - 창이 열린 동안 Player·Attack 액션 맵은 꺼진다 (InputBlockingPanel).<br/>
/// - 내 캐릭터가 다른 장소로 가면 놓아준다 (PlaceInteractable).
/// </para>
/// </summary>
public class PanelStation : PlaceInteractable
{
	[SerializeField, Tooltip("열 창. 같은 씬에 놓은 BlockEditorPanel, SlotEquipPanel 등")]
	InputBlockingPanel panel;

	protected override void Interact(PlayerInputReader player)
	{
		if (panel == null)
		{
			Debug.LogError($"[PanelStation] {name}: Panel이 비어 있음", this);
			return;
		}
		panel.OpenFor(player);
	}
}
