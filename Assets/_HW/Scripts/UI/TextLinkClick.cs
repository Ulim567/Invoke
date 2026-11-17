using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 글자 안의 TMP 링크(&lt;link="id"&gt;이름&lt;/link&gt;)를 누르면 그 링크의 id를 알린다 (#506).
/// <para>
/// - 누른 곳을 받는 오브젝트(Button 등 IPointerClickHandler가 있는 오브젝트)에 붙이고, 링크가 든 글자를 text에 연결한다.<br/>
/// - 링크를 누르면 LinkClicked(id), 링크가 아닌 곳을 누르면 LinkClicked(null)을 부른다. 마우스를 올린 링크가 바뀌면 LinkHovered(id)를 부른다.<br/>
/// - 오류 창의 오류 카드(ErrorItemView)가 「> 슬롯 Q &gt; aim &gt; test」의 이름마다 다른 일을 할 때 쓴다.
/// </para>
/// </summary>
// 누른 위치는 UI 이벤트(PointerEventData)로 받는다. 키·마우스를 직접 읽지 않기 위함 (CLAUDE.md 코드 작성 규칙 5)
public class TextLinkClick : MonoBehaviour, IPointerClickHandler, IPointerMoveHandler, IPointerExitHandler
{
	[SerializeField, Tooltip("링크가 든 글자")]
	TMP_Text text;

	/// <summary>누른 링크의 id. 링크가 아닌 곳이면 null.</summary>
	public event Action<string> LinkClicked;

	/// <summary>마우스를 올린 링크가 바뀌었을 때 그 링크의 id. 링크 밖으로 나가면 null. 오류 카드가 올린 이름만 다른 색으로 칠할 때 쓴다.</summary>
	public event Action<string> LinkHovered;

	string hovered;

	public void OnPointerClick(PointerEventData eventData) => LinkClicked?.Invoke(LinkAt(eventData.position, eventData.pressEventCamera));

	public void OnPointerMove(PointerEventData eventData) => SetHovered(LinkAt(eventData.position, eventData.enterEventCamera));

	public void OnPointerExit(PointerEventData eventData) => SetHovered(null);

	void SetHovered(string id)
	{
		if (id == hovered) return;
		hovered = id;
		LinkHovered?.Invoke(id);
	}

	// 화면 위치 position에 있는 링크의 id. 없으면 null
	string LinkAt(Vector2 position, Camera eventCamera)
	{
		if (text == null) return null;
		int index = TMP_TextUtilities.FindIntersectingLink(text, position, eventCamera);
		return index >= 0 ? text.textInfo.linkInfo[index].GetLinkID() : null;
	}
}
