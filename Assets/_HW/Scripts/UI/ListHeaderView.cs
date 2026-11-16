using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 목록의 머리글 한 줄 (ListHeader 프리팹). 편집 화면의 「슬롯」, 「페이지 2 / 5 +」와 블록 목록의 「제어」, 「변수」 등에 쓴다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Horizontal Layout Group + Layout Element + 이 컴포넌트,
/// 자식에 Title(Text - TextMeshPro), Status(Image, 빌드 결과 ✓·✗. 없어도 됨, #506), StatusText(Text - TextMeshPro, 「시전 가능」 / 「시전 불가능」. 없어도 됨), AddButton(Button, 글자 「+」. 없어도 됨).
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

	[SerializeField, Tooltip("머리글 오른쪽 빌드 결과 그림 (자식 Status). 비워도 된다. 편집 화면 함수 목록의 「슬롯」 머리글에만 보인다 (#506)")]
	Image statusImage;

	[SerializeField, Tooltip("빌드 결과 그림 오른쪽 글자 (자식 StatusText, 「시전 가능」 / 「시전 불가능」). 비워도 된다 (#506)")]
	TMP_Text statusText;

	[SerializeField, Tooltip("빌드 성공 글자")]
	string okLabel = "시전 가능";

	[SerializeField, Tooltip("빌드 실패 글자")]
	string failLabel = "시전 불가능";

	[SerializeField, Tooltip("빌드 성공 그림 (ui_check)")]
	Sprite okSprite;

	[SerializeField, Tooltip("빌드 실패 그림 (ui_cross)")]
	Sprite failSprite;

	[SerializeField, Tooltip("빌드 성공 색")]
	Color okColor = new Color32(0x7E, 0xD6, 0xA0, 0xFF);

	[SerializeField, Tooltip("빌드 실패 색")]
	Color failColor = new Color32(0xE0, 0x56, 0x6A, 0xFF);

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

	/// <summary>
	/// 머리글 오른쪽에 빌드 결과를 보인다 (#506). true면 초록 ✓와 「시전 가능」, false면 빨간 ✗와 「시전 불가능」.
	/// 부르지 않은 머리글은 그림과 글자가 꺼진 채로 있다 (프리팹에서 꺼 둠).
	/// </summary>
	public void SetBuildStatus(bool succeeded)
	{
		Color color = succeeded ? okColor : failColor;
		if (statusImage != null)
		{
			statusImage.gameObject.SetActive(true);
			statusImage.sprite = succeeded ? okSprite : failSprite;
			statusImage.color = color;
		}
		if (statusText != null)
		{
			// 그림만으로는 ✓·✗가 무엇의 결과인지 알기 어려워, 플레이어가 아는 말(시전)로 함께 적는다 (사용자 요청)
			statusText.gameObject.SetActive(true);
			statusText.text = succeeded ? okLabel : failLabel;
			statusText.color = color;
		}
	}
}
