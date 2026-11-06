using UnityEngine;

/// <summary>
/// 로비의 스킬 트리 해금 장소. 가까이 가서 ↑를 누르면 스킬 트리 창을 연다.
/// <para>
/// 창이 열린 동안 Player·Attack 액션 맵은 꺼진다 (SkillTreePanel → InputBlockingPanel).
/// 재료를 써서 해금하는 일은 「재료 써서 스킬 트리 해금하기」에서 한다.
/// </para>
/// </summary>
public class SkillTreeStation : PlaceInteractable
{
	[SerializeField, Tooltip("Lobby 씬에 놓은 스킬 트리 창")]
	SkillTreePanel panel;

	protected override void Interact(PlayerInputReader player)
	{
		if (panel == null)
		{
			Debug.LogError($"[SkillTreeStation] {name}: Panel이 비어 있음", this);
			return;
		}
		Debug.Log("[SkillTreeStation] 스킬 트리 창 열기");
		panel.Open(player);
	}
}
