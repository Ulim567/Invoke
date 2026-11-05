using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록의 머리글 한 줄 (ListHeader 프리팹). 편집 화면의 「슬롯」, 「페이지 2 / 5 +」와 블록 목록의 「제어」, 「변수」 등에 쓴다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Horizontal Layout Group + Layout Element + 이 컴포넌트,
/// 자식에 Title(Text - TextMeshPro), AddButton(Button, 글자 「+」. 없어도 됨).
/// </para>
/// </summary>
public class ListHeaderView : MonoBehaviour
{
	[SerializeField, Tooltip("머리글 글자")]
	TMP_Text titleText;

	[SerializeField, Tooltip("+ 버튼. 누를 일이 없으면 숨긴다")]
	Button addButton;

	[SerializeField, Tooltip("앞쪽 작은 색 네모 (블록 목록의 분류 색). 비워도 된다")]
	Image iconImage;

	/// <summary>
	/// 글자를 정한다. onAdd가 있으면 + 버튼을 보이고 누르면 부른다.
	/// </summary>
	public void Show(string title, Action onAdd) => Show(title, onAdd, new Color(0, 0, 0, 0));

	/// <summary>
	/// 글자, + 버튼, 앞쪽 색 네모의 색을 정한다. 색의 알파가 0이면 네모를 숨긴다.
	/// </summary>
	public void Show(string title, Action onAdd, Color icon)
	{
		if (iconImage != null)
		{
			iconImage.gameObject.SetActive(icon.a > 0);
			iconImage.color = icon;
		}
		if (titleText != null) titleText.text = title;
		if (addButton == null) return;
		addButton.onClick.RemoveAllListeners();
		addButton.gameObject.SetActive(onAdd != null);
		if (onAdd != null) addButton.onClick.AddListener(() => onAdd());
	}
}
