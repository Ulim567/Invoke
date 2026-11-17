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
/// - 마우스를 올리고 멈추면 설명창을 띄운다. 편집 영역의 같은 블록과 같은 설명이다 (원형, 스킬 설명, 파라미터, return 값).
///   항목에는 짧게 제목과 원형만 두고, 설명은 여기서 보이기로 했다 (사용자가 정함).<br/>
/// - 제목 줄 오른쪽 끝에 「남은/가진」 개수를 보인다 (#500, BlockEditor.CountOf). 남은 개수가 0이면 흐리게 보이고 끌리지 않는다.<br/>
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

	[SerializeField, Tooltip("제목 줄 오른쪽 끝의 「남은/가진」 개수 (#500). 개수가 없는 항목은 숨긴다")]
	TMP_Text countText;

	// 시안 6-12의 칠: 함수 색(#9E7BFF)을 편집 영역 바탕(#141823)에 26% 섞은 색. 끄는 동안은 8%로 비워 보이고, 점선은 70%
	[SerializeField, Tooltip("바탕 색: 보통")]
	Color fillColor = new Color32(0x38, 0x32, 0x5C, 0xFF);

	[SerializeField, Tooltip("바탕 색: 끄는 동안 (비워 둔 자리)")]
	Color emptyFillColor = new Color32(0x1F, 0x20, 0x35, 0xFF);

	[SerializeField, Tooltip("점선 테두리 색")]
	Color outlineColor = new Color32(0x75, 0x5D, 0xBD, 0xFF);

	// 시안 6-12의 「남은/가진」 색: 남은 개수는 제목과 같은 밝은 색, /가진 개수는 작고 흐리게. 남은 개수가 0이면 항목 전체를 흐리게 한다
	static readonly Color TitleColor = new Color32(0xF4, 0xF1, 0xFA, 0xFF);
	static readonly Color DimTitleColor = new Color32(0x8A, 0x84, 0xA0, 0xFF);
	const string OwnedColor = "#8A84A0";
	const string DimOwnedColor = "#6E688A";
	// 가진 것보다 많이 놓았을 때(저장하면 거부됨)의 남은 개수. 편집 화면의 오류 글자와 같은 색
	const string OverColor = "#FF9B8A";

	BlockEditor editor;
	Func<BlockData> factory;
	BlockData tooltipSample; // 설명창 글자를 만들 견본 블록. 처음 마우스를 올릴 때 한 번 만들고 편집 영역에는 놓지 않는다
	BlockData countSample;   // 「남은/가진」을 물을 견본 블록. 편집 영역에는 놓지 않는다
	bool dimmed;             // 남은 개수가 0 이하라 흐리게 보이는 중 (끌 수 없음)
	bool dragging;

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
		countSample = factory();
		dragging = false;
		RefreshCount();
	}

	/// <summary>
	/// 「남은/가진」 개수를 다시 쓴다 (#500). BlockEditor가 블록을 바꿀 때마다, 가진 개수가 바뀔 때 부른다.
	/// 개수가 없는 항목(내가 만든 함수, 임시 계정)은 숫자를 숨긴다. 남은 개수가 0 이하면 흐리게, 0보다 작으면 남은 개수를 빨갛게 보인다.
	/// </summary>
	public void RefreshCount()
	{
		(int Remaining, int Owned)? count = editor != null && countSample != null ? editor.CountOf(countSample) : null;
		dimmed = count != null && count.Value.Remaining <= 0;
		if (countText != null)
		{
			countText.gameObject.SetActive(count != null);
			if (count != null)
			{
				(int remaining, int owned) = count.Value;
				string left = remaining < 0 ? $"<color={OverColor}>{remaining}</color>" : remaining.ToString();
				countText.text = $"{left}<size=9><color={(dimmed ? DimOwnedColor : OwnedColor)}>/{owned}</color></size>";
				countText.color = dimmed ? DimTitleColor : TitleColor;
			}
		}
		if (titleText != null) titleText.color = dimmed ? DimTitleColor : TitleColor;
		// 원형은 부분마다 리치 텍스트 색이 있어 글자 색으로는 흐리게 할 수 없으므로 투명도로 흐리게 한다
		if (prototypeText != null) prototypeText.alpha = dimmed ? 0.5f : 1f;
		SetEmpty(dragging);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (editor == null || factory == null) return;
		// 남은 개수가 0이면 끌지 않고 편집 화면이 안내한다 (#500, 사용자 결정 (가))
		if (!editor.CanDragNew(countSample ?? factory())) return;
		dragging = true;
		SetEmpty(true);
		// 놓으면 원래 모양으로. 그 사이 목록을 다시 만들어 이 항목이 지워졌으면 되돌릴 것이 없다
		editor.BeginNewBlockDrag(factory(), eventData, () =>
		{
			if (this == null) return;
			dragging = false;
			SetEmpty(false);
		});
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

	// 끄는 동안: 흐린 바탕 + 점선 테두리 (시안 6-8). 남은 개수가 0이라 흐린 항목도 같은 흐린 바탕(시안 6-12, 8%)이고 점선은 없다
	void SetEmpty(bool empty)
	{
		if (background != null) background.color = empty || dimmed ? emptyFillColor : fillColor;
		if (dashedOutline != null)
		{
			dashedOutline.color = outlineColor;
			dashedOutline.enabled = empty;
		}
	}
}
