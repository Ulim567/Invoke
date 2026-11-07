using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// HUD 슬롯 칸에 마우스를 올린 동안만 칸 위 말풍선(장착된 스킬 이름)을 보여 준다. 보여 주기만 한다.
/// 글자는 HudView가 채운다. 빈 슬롯처럼 글자가 없으면 말풍선을 띄우지 않는다.
/// </summary>
public class SlotHoverTip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField, Tooltip("말풍선 (처음엔 꺼 둠)")]
	GameObject tip;

	[SerializeField, Tooltip("말풍선 글자 (HudView가 채움)")]
	TMP_Text label;

	bool hovered;

	public void OnPointerEnter(PointerEventData eventData) => hovered = true;

	public void OnPointerExit(PointerEventData eventData) => hovered = false;

	void OnDisable() => hovered = false;

	void LateUpdate()
	{
		if (tip == null) return;
		bool show = hovered && label != null && !string.IsNullOrEmpty(label.text);
		if (tip.activeSelf != show) tip.SetActive(show);
	}
}
