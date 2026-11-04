using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 장착 화면의 슬롯 카드 하나 (SlotCard 프리팹). 키 배지(Q W E R A S D F)와 넣은 것의 이름을 보여 준다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_slot, Sliced) + Button + 이 컴포넌트,
/// 자식에 Key(Image ui_key + 자식 Text - TextMeshPro), Name(Text - TextMeshPro, 가운데 정렬).
/// </para>
/// </summary>
public class SlotCardView : MonoBehaviour
{
	[SerializeField, Tooltip("누르면 이 칸을 고른다 (최상위 Button)")]
	Button button;

	[SerializeField, Tooltip("바탕 (최상위 Image)")]
	Image background;

	[SerializeField, Tooltip("키 배지 안의 글자")]
	TMP_Text keyText;

	[SerializeField, Tooltip("넣은 함수·스킬 이름")]
	TMP_Text nameText;

	[SerializeField, Tooltip("보통 바탕 (ui_slot)")]
	Sprite normalSprite;

	[SerializeField, Tooltip("고른 칸 바탕 (ui_slot_selected)")]
	Sprite selectedSprite;

	[SerializeField, Tooltip("이름 글자 색")]
	Color nameColor = Color.white;

	[SerializeField, Tooltip("빈 칸 글자 색 (「비어 있음」)")]
	Color emptyColor = new Color32(0x5A, 0x62, 0x90, 0xFF);

	public Button Button => button;

	/// <summary>
	/// 키, 이름, 빈 칸인지, 고른 칸인지를 정한다.
	/// </summary>
	public void Show(string key, string name, bool empty, bool selected)
	{
		if (keyText != null) keyText.text = key;
		if (nameText != null)
		{
			nameText.text = empty ? "비어 있음" : name;
			nameText.color = empty ? emptyColor : nameColor;
		}
		if (background != null && normalSprite != null) background.sprite = selected && selectedSprite != null ? selectedSprite : normalSprite;
	}
}
