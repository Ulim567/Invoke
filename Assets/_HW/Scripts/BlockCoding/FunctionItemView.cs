using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 블록 편집 화면 함수 탭(#489, 시안 6-12)의 함수 항목 한 줄. 위에 제목(한글 이름), 아래에 함수 원형을 보여 준다.
/// 영문 이름은 원형(void thrust(Target target);)에 들어 있으므로 따로 적지 않는다.
/// <para>
/// - 끌면 그 함수의 호출 블록을 새로 만들어 끈다. 블록 목록의 칩과 같은 흐름이다 (BlockEditor.BeginNewBlockDrag).<br/>
/// - 끄는 동안 이 항목은 흐린 바탕과 점선 테두리로 비워 둔다 (시안 6-8). 블록을 놓으면 원래 모양으로 돌아온다.<br/>
/// - 마우스를 올리고 멈추면 설명창을 띄운다. 편집 영역의 같은 블록과 같은 설명이다 (원형, 스킬 설명, 파라미터, 돌려주는 값).
///   항목에는 짧게 제목과 원형만 두고, 설명은 여기서 보이기로 했다 (사용자가 정함).<br/>
/// - BlockEditor가 Show로 내용을 채운다. 프리팹은 InvokeUIBuilder가 만든다 (FunctionItem).
/// </para>
/// </summary>
public class FunctionItemView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField, Tooltip("항목 바탕 (둥근 네모). 함수 색을 칠한다")]
	Image background;

	[SerializeField, Tooltip("끄는 동안만 보이는 점선 테두리")]
	Image dashedOutline;

	[SerializeField, Tooltip("제목: 한글 이름 (예: 검 찌르기). 한글 이름이 없는 내가 만든 함수는 함수 이름")]
	TMP_Text titleText;

	[SerializeField, Tooltip("함수 원형 (예: void thrust(Target target);). 길면 여러 줄로 감싼다")]
	TMP_Text prototypeText;

	// 시안 6-12의 칠: 함수 색(#9E7BFF)을 편집 영역 바탕(#141823)에 26% 섞은 색. 끄는 동안은 8%로 비워 보이고, 점선은 70%
	[SerializeField, Tooltip("바탕 색: 보통")]
	Color fillColor = new Color32(0x38, 0x32, 0x5C, 0xFF);

	[SerializeField, Tooltip("바탕 색: 끄는 동안 (비워 둔 자리)")]
	Color emptyFillColor = new Color32(0x1F, 0x20, 0x35, 0xFF);

	[SerializeField, Tooltip("점선 테두리 색")]
	Color outlineColor = new Color32(0x75, 0x5D, 0xBD, 0xFF);

	BlockEditor editor;
	Func<BlockData> factory;
	BlockData tooltipSample; // 설명창 글자를 만들 견본 블록. 처음 마우스를 올릴 때 한 번 만들고 편집 영역에는 놓지 않는다

	/// <summary>
	/// 항목 내용을 채운다. title은 한글 이름(없으면 함수 이름), prototype은 함수 원형.
	/// blockFactory는 끌 때마다 새 블록 데이터를 만든다 (같은 항목을 여러 번 끌어도 블록이 따로 생기게).
	/// </summary>
	public void Show(BlockEditor owner, string title, string prototype, Func<BlockData> blockFactory)
	{
		editor = owner;
		factory = blockFactory;
		tooltipSample = null;
		if (titleText != null) titleText.text = title ?? "";
		// 원형은 부분마다 색을 나눈다 (반환 타입, 함수 이름, 파라미터, #489). 참고 카드·설명창과 같은 색
		if (prototypeText != null) prototypeText.text = BlockReference.ColorCode(prototype);
		SetEmpty(false);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (editor == null || factory == null) return;
		SetEmpty(true);
		// 놓으면 원래 모양으로. 그 사이 목록을 다시 만들어 이 항목이 지워졌으면 되돌릴 것이 없다
		editor.BeginNewBlockDrag(factory(), eventData, () => { if (this != null) SetEmpty(false); });
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (editor == null || factory == null) return;
		editor.EnterHover(this, (RectTransform)transform, () => editor.TooltipOf(tooltipSample ??= factory()));
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (editor != null) editor.ExitHover(this);
	}

	// 끌기 이벤트는 새로 만든 블록이 받는다. 이 칸들이 있어야 Unity가 이 항목에서 끌기를 시작한다
	public void OnDrag(PointerEventData eventData) { }
	public void OnEndDrag(PointerEventData eventData) { }

	// 끄는 동안: 흐린 바탕 + 점선 테두리 (시안 6-8)
	void SetEmpty(bool empty)
	{
		if (background != null) background.color = empty ? emptyFillColor : fillColor;
		if (dashedOutline != null)
		{
			dashedOutline.color = outlineColor;
			dashedOutline.enabled = empty;
		}
	}
}
