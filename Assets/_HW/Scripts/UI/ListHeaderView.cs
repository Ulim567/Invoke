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

	/// <summary>머리글 글자. 고정 머리글(StickyListHeader)이 지금 구역 이름을 옮겨 적을 때 읽는다</summary>
	public string Title => titleText != null ? titleText.text : "";

	// 아래 모양 함수들 (#578, 함수 탭): 프리팹은 그대로 두고 이 머리글만 바꾼다. 같은 프리팹을 왼쪽 함수 목록·문법 탭·참고 탭도 쓰기 때문

	/// <summary>머리글 뒤에 불투명한 바탕을 깐다. 고정 머리글(StickyListHeader)이 아래로 지나가는 항목을 가릴 때 쓴다</summary>
	public void SetBackground(Color color)
	{
		Image background = GetComponent<Image>();
		if (background == null) background = gameObject.AddComponent<Image>();
		background.color = color;
		background.raycastTarget = false;
	}

	/// <summary>
	/// 「스킬 ────」 모양: 글자 뒤에 남은 너비만큼 가는 가로선을 긋고, 위쪽을 비워 앞 구역과 떨어뜨린다. 함수 탭의 대구분이 쓴다.
	/// 선은 앞쪽 색 네모(iconImage)를 글자 뒤로 옮겨 늘린 것이다. 새 오브젝트를 만들지 않기 위함 (게임 중 오브젝트는 프리팹으로만 만든다).
	/// </summary>
	public void SetDividerStyle(Color text, Color line, float fontSize, float height)
	{
		if (titleText != null)
		{
			titleText.color = text;
			titleText.fontSize = fontSize;
			// 글자는 글자 너비만큼만 차지하고, 남은 너비는 선이 가진다
			if (titleText.TryGetComponent(out LayoutElement titleSize))
			{
				titleSize.preferredWidth = -1;
				titleSize.flexibleWidth = 0;
			}
		}
		if (iconImage != null)
		{
			iconImage.gameObject.SetActive(true);
			iconImage.color = line;
			if (titleText != null) iconImage.transform.SetSiblingIndex(titleText.transform.GetSiblingIndex() + 1);
			if (iconImage.TryGetComponent(out LayoutElement lineSize))
			{
				lineSize.preferredWidth = 0;
				lineSize.flexibleWidth = 1;
				lineSize.preferredHeight = 1;
			}
		}
		// 위쪽 여백을 늘려 글자와 선이 아래쪽 칸의 가운데에 오게 한다 (선이 글자 가운데 높이를 지나게)
		if (TryGetComponent(out HorizontalLayoutGroup row))
			row.padding = new RectOffset(row.padding.left, row.padding.right, Mathf.Max(0, Mathf.RoundToInt(height - DividerRowHeight)), row.padding.bottom);
		if (TryGetComponent(out LayoutElement size)) size.preferredHeight = height;
	}

	const float DividerRowHeight = 14f; // 글자 줄의 높이. height에서 이만큼을 뺀 나머지가 위쪽 여백

	/// <summary>
	/// 「▪ 찌르기」 모양: 글자 앞에 작은 네모 점을 찍고, 대구분보다 안쪽으로 들인다. 함수 탭의 소구분이 쓴다.
	/// 점은 앞쪽 색 네모(iconImage)를 작게 줄인 것이다 (Galmuri11 글꼴에 「•」가 없다).
	/// </summary>
	public void SetBulletStyle(Color text, Color bullet, float bulletSize, float indent, float height)
	{
		if (titleText != null) titleText.color = text;
		if (iconImage != null)
		{
			iconImage.gameObject.SetActive(true);
			iconImage.color = bullet;
			if (iconImage.TryGetComponent(out LayoutElement dot))
			{
				dot.preferredWidth = bulletSize;
				dot.preferredHeight = bulletSize;
			}
		}
		if (TryGetComponent(out HorizontalLayoutGroup row))
			row.padding = new RectOffset(row.padding.left + Mathf.RoundToInt(indent), row.padding.right,
				Mathf.Max(0, Mathf.RoundToInt(height - DividerRowHeight)), row.padding.bottom);
		if (TryGetComponent(out LayoutElement size)) size.preferredHeight = height;
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
