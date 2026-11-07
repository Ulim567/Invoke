using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 파티방 파티원 줄의 모양 (시안 13). 김도훈의 RaidMemberRowUI가 쓴 글자를 읽어서 모양만 바꾼다.
/// <para>
/// RaidMemberRowUI는 내 줄이면 이름 뒤에 「 (나)」를 붙인다. 그 글자를 떼고 이름 옆의 민트 「나」 표시(Me Badge)를 켜고, 줄 바탕을 민트 테두리(Mine Sprite)로 바꾼다.
/// RaidMemberRowUI는 그대로 쓴다 (코드를 고치지 않음). RaidMemberRowUI는 줄을 다시 쓸 때마다 이름을 새로 넣으므로 매 프레임 확인한다.
/// </para>
/// </summary>
public class RaidMemberRowStyle : MonoBehaviour
{
	const string MeSuffix = " (나)";

	[SerializeField, Tooltip("RaidMemberRowUI의 이름 글자")]
	TMP_Text nameText;

	[SerializeField, Tooltip("이름 옆 「나」 표시")]
	GameObject meBadge;

	[SerializeField, Tooltip("줄 바탕")]
	Image background;

	[SerializeField] Sprite normalSprite;
	[SerializeField, Tooltip("내 줄 바탕 (민트 테두리)")]
	Sprite mineSprite;

	string lastName;
	bool mine;

	void LateUpdate()
	{
		if (nameText == null) return;
		string value = nameText.text;
		if (value == lastName) return;
		// RaidMemberRowUI가 새로 쓴 이름: 「 (나)」가 붙어 있으면 내 줄
		mine = value.EndsWith(MeSuffix);
		if (mine)
		{
			value = value.Substring(0, value.Length - MeSuffix.Length);
			nameText.text = value;
		}
		lastName = value;
		if (meBadge != null) meBadge.SetActive(mine);
		Sprite sprite = mine ? mineSprite : normalSprite;
		if (background != null && sprite != null) background.sprite = sprite;
	}
}
