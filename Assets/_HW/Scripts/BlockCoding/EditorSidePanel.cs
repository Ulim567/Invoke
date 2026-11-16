using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 편집 화면 오른쪽 창(문법·함수·오류·참고)의 너비와 접기. 창 왼쪽 끝의 세로 손잡이에 붙어, 손잡이를 끌면 너비를 바꾼다.
/// <para>
/// 창이 넓어지거나 접히면 편집 영역·함수 틀·아래 줄의 오른쪽 끝도 같이 옮긴다. 어느 창을 보일지(탭)는 BlockEditor가 정하고,
/// 접기는 BlockEditor가 SetCollapsed로 부른다 (열린 탭을 다시 누르거나, 세로 탭 맨 아래 > < 버튼을 누르면 접힘).
/// 너비는 게임이 켜져 있는 동안 기억한다 (편집 화면을 닫았다 열어도 같은 너비).
/// </para>
/// 프리팹은 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기가 만든다 (InvokeUIBuilder, 이름 PanelHandle).
/// </summary>
public class EditorSidePanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
	[Header("너비 (캔버스 단위)")]
	[SerializeField, Tooltip("처음 너비")]
	float defaultWidth = 207f;

	[SerializeField, Tooltip("손잡이로 줄일 수 있는 가장 좁은 너비")]
	float minWidth = 160f;

	[SerializeField, Tooltip("손잡이로 늘릴 수 있는 가장 넓은 너비")]
	float maxWidth = 400f;

	[SerializeField, Tooltip("오른쪽 끝 세로 탭의 너비. 창은 이 탭의 왼쪽에 붙는다")]
	float stripWidth = 33f;

	[Header("같이 움직일 것")]
	[SerializeField, Tooltip("오른쪽 창들 (Palette, ErrorView, ReferenceView, FunctionView). 너비가 창 너비가 된다")]
	RectTransform[] panels;

	[SerializeField, Tooltip("창 왼쪽에 붙은 것들 (Workspace, StatusBar, FrameHeader, FrameFooter). 오른쪽 끝이 창 왼쪽 끝을 따라간다")]
	RectTransform[] followers;

	[SerializeField, Tooltip("블록을 끄는 동안 창 위에 보이는 휴지통 (TrashZone). 창 너비와 세로 탭을 덮는다")]
	RectTransform trashZone;

	[SerializeField, Tooltip("휴지통 글자. 창이 접혀 세로 탭만 남으면 짧은 글자로 바꾼다")]
	TMP_Text trashText;

	[SerializeField, Tooltip("세로 탭 맨 아래 접기·펴기 버튼의 글자 (PanelToggle). 열려 있으면 >, 접혀 있으면 <")]
	TMP_Text toggleText;

	[SerializeField, Tooltip("세로 탭 왼쪽 끝 경계선 (TabStrip/LeftLine). 접혔을 때만 보인다. 열려 있으면 창 바탕이 경계가 된다")]
	GameObject collapsedLine;

	[Header("손잡이 모양")]
	[SerializeField, Tooltip("손잡이 그림. 마우스를 올리거나 끄는 동안만 보인다")]
	Image handleImage;

	[SerializeField, Tooltip("마우스를 올렸을 때 손잡이 색")]
	Color hoverColor = new Color32(0x5E, 0xF2, 0xD6, 0x80);

	// 게임이 켜져 있는 동안 기억하는 너비. 0이면 아직 바꾸지 않은 것 (처음 너비를 쓴다)
	static float rememberedWidth;

	float width;
	bool collapsed;
	bool dragging;
	bool hovering;
	float dragStartWidth;
	Vector2 dragStartPosition;
	string trashOpenText;
	const string TrashCollapsedText = "지\n우\n기";

	/// <summary>오른쪽 창이 접혀 세로 탭만 보이면 true</summary>
	public bool Collapsed => collapsed;

	/// <summary>
	/// 창 너비가 바뀌거나 접히고 펴진 직후에 부른다. 편집 영역 크기에 맞춰 둔 값(블록 자리의 최소 크기)을 그 자리에서 다시 맞추게 하려는 것.
	/// 다음 프레임에 맞추면 그 사이 한 프레임 동안 블록 자리가 보이는 칸보다 넓어져 가로 스크롤바가 깜빡인다
	/// </summary>
	public event System.Action Resized;

	void Awake()
	{
		width = Mathf.Clamp(rememberedWidth > 0f ? rememberedWidth : defaultWidth, minWidth, maxWidth);
		if (trashText != null) trashOpenText = trashText.text;
		Apply();
	}

	/// <summary>
	/// 오른쪽 창을 접거나 편다 (BlockEditor가 탭을 누를 때 부른다). 접으면 편집 영역이 세로 탭까지 넓어진다.
	/// 어느 창을 켜고 끌지는 BlockEditor가 한다.
	/// </summary>
	public void SetCollapsed(bool value)
	{
		if (collapsed == value) return;
		collapsed = value;
		Apply();
	}

	// 지금 너비·접힘에 맞춰 창, 편집 영역, 휴지통, 손잡이 자리를 옮긴다
	void Apply()
	{
		float open = collapsed ? 0f : width;
		foreach (RectTransform panel in panels)
		{
			if (panel == null) continue;
			panel.offsetMin = new Vector2(-stripWidth - width, panel.offsetMin.y);
			panel.offsetMax = new Vector2(-stripWidth, panel.offsetMax.y);
		}
		foreach (RectTransform follower in followers)
			if (follower != null) follower.offsetMax = new Vector2(-(stripWidth + open), follower.offsetMax.y);
		if (trashZone != null) trashZone.offsetMin = new Vector2(-(stripWidth + open), trashZone.offsetMin.y);
		// 접혀 세로 탭 너비만 남으면 두 줄 안내가 들어가지 않으므로 세로로 짧게
		if (trashText != null) trashText.text = collapsed ? TrashCollapsedText : trashOpenText;
		// 버튼 글자는 누르면 창이 움직일 방향: 열려 있으면 오른쪽으로 접기(>), 접혀 있으면 왼쪽으로 펴기(<)
		if (toggleText != null) toggleText.text = collapsed ? "<" : ">";
		// 접으면 편집 영역이 세로 탭에 바로 붙어 경계가 보이지 않으므로 선을 긋는다
		if (collapsedLine != null) collapsedLine.SetActive(collapsed);

		// 손잡이는 창 왼쪽 끝 안쪽에 둔다. 경계 가운데에 두면 편집 영역 오른쪽 스크롤바를 반쯤 덮어 스크롤바를 잡기 어렵기 때문
		var rect = (RectTransform)transform;
		rect.anchoredPosition = new Vector2(-(stripWidth + width) + rect.rect.width / 2f, rect.anchoredPosition.y);
		// 접혀 있으면 손잡이도 숨긴다 (Image만 끄면 끌 수 없음)
		if (handleImage != null) handleImage.enabled = !collapsed;
		UpdateHandleColor();
		Resized?.Invoke();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		if (collapsed) return;
		dragging = true;
		dragStartWidth = width;
		dragStartPosition = eventData.position;
		UpdateHandleColor();
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		// 창은 오른쪽에 붙어 있으므로 손잡이를 왼쪽으로 끌면 넓어진다. 화면 픽셀을 캔버스 단위로 바꿔 마우스와 손잡이가 같이 움직이게
		float moved = (dragStartPosition.x - eventData.position.x) / CanvasScale();
		width = Mathf.Clamp(dragStartWidth + moved, minWidth, maxWidth);
		rememberedWidth = width;
		Apply();
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		dragging = false;
		UpdateHandleColor();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		hovering = true;
		UpdateHandleColor();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		hovering = false;
		UpdateHandleColor();
	}

	// 평소에는 투명하고, 마우스를 올리거나 끄는 동안만 선이 보인다. 투명해도 마우스는 받는다 (Image의 raycast)
	void UpdateHandleColor()
	{
		if (handleImage != null) handleImage.color = hovering || dragging ? hoverColor : new Color(0, 0, 0, 0);
	}

	float CanvasScale()
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
		return scale > 0f ? scale : 1f;
	}
}
