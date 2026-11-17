using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오류 창의 오류 하나 (ErrorItem 프리팹): 설명, 오류가 난 블록의 C 글자, 「> 함수 이름」 세 줄. 보여 주기만 한다.
/// 「> 함수 이름」을 누르면 BlockEditor가 그 함수를 열고 오류가 난 블록을 깜빡인다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_card) + Vertical Layout Group + 이 컴포넌트, 자식에 Bar(빨간 세로줄), Message, Code(Text - TextMeshPro),
/// LocationButton(Button, 바탕 없이 청록 글자, TextLinkClick: 호출 경로의 이름마다 누르기 #506).
/// </para>
/// </summary>
public class ErrorItemView : MonoBehaviour
{
	[SerializeField, Tooltip("설명 (예: 선언하지 않은 식별자: hpp)")]
	TMP_Text messageText;

	[SerializeField, Tooltip("오류가 난 블록의 글자 (예: hp = hpp - 1;). 블록이 없는 오류면 숨긴다")]
	TMP_Text codeText;

	[SerializeField, Tooltip("「> 함수 이름」 버튼. 누르면 그 함수를 연다")]
	Button locationButton;

	[SerializeField, Tooltip("「> 함수 이름」 버튼의 글자")]
	TMP_Text locationText;

	[SerializeField, Tooltip("「> 함수 이름」 글자 안의 링크를 누른 것을 알려 주는 컴포넌트 (LocationButton에 붙임). 비워도 된다 (#506)")]
	TextLinkClick locationLinks;

	[SerializeField, Tooltip("누를 수 있는 이름의 색 (링크가 있는 카드)")]
	Color linkColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);

	[SerializeField, Tooltip("마우스를 올린 이름의 색 (링크가 있는 카드)")]
	Color hoverColor = Color.white;

	[SerializeField, Tooltip("누를 수 없는 「> 이름」의 색")]
	Color disabledColor = new Color32(0x5A, 0x62, 0x90, 0xFF);

	// 링크마다 할 일 (「슬롯 Q > aim > test」의 이름마다, 경로가 없으면 이름 하나). 링크를 쓰지 않으면 null
	List<Action> links;
	Action onLocationClick;
	// 링크가 든 「> 」 뒤의 글자와, 마우스를 올린 링크의 id (없으면 null)
	string shownLocation;
	string hoveredLink;

	void Awake()
	{
		if (locationLinks == null) return;
		locationLinks.LinkClicked += OnLinkClicked;
		locationLinks.LinkHovered += OnLinkHovered;
	}

	/// <summary>
	/// 글자를 정한다. code가 비었으면 코드 줄을 숨긴다. onLocation이 있으면 「> 함수 이름」을 누를 때 부른다 (없으면 누를 수 없음).
	/// </summary>
	public void Show(string message, string code, string location, Action onLocation) => Show(message, code, location, onLocation, null);

	/// <summary>
	/// Show와 같고, location에 TMP 링크(&lt;link="번호"&gt;이름&lt;/link&gt;)를 넣었으면 links[번호]를 그 이름을 누를 때 부른다 (#506).
	/// 오류 카드의 「> 슬롯 Q &gt; aim &gt; test」에서 이름마다 다른 함수를 열 때 쓴다. 링크가 아닌 곳을 누르면 onLocation을 부른다.
	/// </summary>
	public void Show(string message, string code, string location, Action onLocation, List<Action> links)
	{
		if (messageText != null) messageText.text = message ?? "";
		if (codeText != null)
		{
			codeText.text = code ?? "";
			codeText.gameObject.SetActive(!string.IsNullOrEmpty(code));
		}
		if (locationButton == null || locationText == null) return;
		locationButton.gameObject.SetActive(!string.IsNullOrEmpty(location));
		locationButton.onClick.RemoveAllListeners();
		locationButton.interactable = onLocation != null;
		onLocationClick = onLocation;
		// 밑줄은 누를 수 있는 이름에만 긋는다 (#506). 앞의 「> 」와 호출 경로 사이의 「 > 」에는 긋지 않는다
		// 호출 경로(links)는 부르는 쪽이 이름마다 <u>를 달아 넘긴다. 예전 프리팹의 글자 전체 밑줄은 여기서 끈다
		locationText.fontStyle &= ~FontStyles.Underline;

		if (locationLinks == null)
		{
			// 링크를 알려 줄 컴포넌트가 없는 예전 프리팹: 줄 전체가 버튼 하나다
			this.links = null;
			locationText.text = links != null ? $"> {location}" : $"> <u>{location}</u>";
			if (onLocation != null) locationButton.onClick.AddListener(() => onLocation());
			return;
		}

		// 링크로 나눈다 (#506). 경로가 없는 카드도 이름 하나를 링크 0으로 둔다. 모든 카드가 이름마다 hover 색을 같은 방식으로 보이기 위함
		// 누른 곳은 OnLinkClicked가 나눈다. 버튼의 onClick도 같이 불리므로 onClick에는 넣지 않는다
		if (links == null)
		{
			location = $"<link=\"0\"><u>{location}</u></link>";
			links = onLocation != null ? new List<Action> { onLocation } : null;
		}
		this.links = links;
		// 이름 하나만 hover 색으로 칠하려면 글자 색으로 칠해야 한다. 버튼의 Color Tint는 글자 전체에 곱해지므로 끈다
		locationButton.transition = Selectable.Transition.None;
		locationText.CrossFadeColor(Color.white, 0f, true, true);
		locationText.color = onLocation != null ? linkColor : disabledColor;
		shownLocation = location;
		hoveredLink = null;
		RenderLocation();
	}

	// 「> 」 뒤 글자를 그린다. 마우스를 올린 링크의 이름만 hoverColor로 칠한다
	void RenderLocation()
	{
		string text = shownLocation ?? "";
		if (hoveredLink != null)
		{
			string open = $"<link=\"{hoveredLink}\">";
			int start = text.IndexOf(open, StringComparison.Ordinal);
			int end = start >= 0 ? text.IndexOf("</link>", start, StringComparison.Ordinal) : -1;
			if (end > start)
				text = text.Substring(0, start + open.Length) + $"<color=#{ColorUtility.ToHtmlStringRGB(hoverColor)}>"
					+ text.Substring(start + open.Length, end - start - open.Length) + "</color>" + text.Substring(end);
		}
		locationText.text = $"> {text}";
	}

	// 「> …」 줄을 눌렀을 때. id는 링크 번호, 링크가 아닌 곳이면 null
	void OnLinkClicked(string id)
	{
		if (links == null || !locationButton.interactable) return;
		Action action = int.TryParse(id, out int index) && index >= 0 && index < links.Count ? links[index] : onLocationClick;
		action?.Invoke();
	}

	// 마우스를 올린 링크가 바뀌었을 때. 누를 수 있을 때만 칠한다
	void OnLinkHovered(string id)
	{
		string next = links != null && locationButton.interactable ? id : null;
		if (next == hoveredLink) return;
		hoveredLink = next;
		RenderLocation();
	}
}
