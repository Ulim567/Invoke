using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 화면에 놓인 블록 하나. BlockData 하나를 들고 있고, 끌어서 옮길 수 있다.
/// <para>
/// - 블록 목록(팔레트)에 있는 블록은 견본이다. 끌면 견본은 그대로 두고 새 블록을 만들어 끈다.<br/>
/// - 편집 영역에 놓인 블록을 끌면 그 블록을 꺼내서 옮긴다. 블록 목록이나 빈 곳에 놓으면 지워진다.<br/>
/// - 조건문·for·while은 안쪽 블록 영역(Inner Blocks)을 가진다. 다른 블록은 그 영역을 숨긴다.
/// </para>
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BlockView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[SerializeField, Tooltip("블록 배경. 분류별 색이 들어간다")]
	Image background;

	[SerializeField, Tooltip("블록 글자 (Text - TextMeshPro)")]
	TMP_Text label;

	[SerializeField, Tooltip("조건문·for·while 안쪽에 블록을 쌓는 자리 (자식 오브젝트)")]
	BlockList innerBlocks;

	/// <summary>이 블록이 들고 있는 블록 데이터</summary>
	public BlockData Data { get; private set; }

	/// <summary>지금 이 블록이 들어 있는 자리. 끌고 있는 중이면 null</summary>
	public BlockList Owner { get; set; }

	BlockEditor editor;
	CanvasGroup canvasGroup;
	Func<BlockData> templateFactory; // 견본이면 새 블록 데이터를 만드는 함수, 아니면 null
	bool dragging;
	BlockList originOwner; // 끌기 전에 있던 자리. 놓을 수 없는 곳에 놓으면 여기로 되돌린다
	int originIndex;

	void Awake()
	{
		canvasGroup = GetComponent<CanvasGroup>();
	}

	/// <summary>
	/// 편집 영역에 놓이는 블록으로 준비한다. 블록 영역이 있으면 안쪽 블록까지 화면에 만든다.
	/// </summary>
	public void Init(BlockEditor owner, BlockData data)
	{
		editor = owner;
		Data = data;
		Refresh();

		bool hasInner = data.InnerBlocks != null;
		innerBlocks.gameObject.SetActive(hasInner);
		if (hasInner) innerBlocks.Bind(data.InnerBlocks, editor);
	}

	/// <summary>
	/// 블록 목록(팔레트)의 견본으로 준비한다. factory는 끌 때마다 새 블록 데이터를 만든다.
	/// </summary>
	public void InitTemplate(BlockEditor owner, Func<BlockData> factory)
	{
		editor = owner;
		templateFactory = factory;
		Data = factory();
		Refresh();
		innerBlocks.gameObject.SetActive(false); // 견본은 한 줄로만 보여 준다
	}

	/// <summary>
	/// 데이터가 바뀌었을 때 글자와 색을 다시 그린다.
	/// </summary>
	public void Refresh()
	{
		label.text = BlockLabels.TextOf(Data);
		background.color = BlockLabels.ColorOf(Data);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		BlockView dragged = this;

		if (templateFactory != null)
		{
			// 견본: 새 블록을 만들어서 그걸 끈다. 이후 끌기 이벤트는 새 블록이 받는다
			dragged = editor.CreateView(templateFactory(), editor.DragLayer);
			((RectTransform)dragged.transform).sizeDelta = ((RectTransform)transform).rect.size;
			eventData.pointerDrag = dragged.gameObject;
		}
		else
		{
			// 편집 영역의 블록: 지금 자리를 기억해 두고 꺼낸다
			originOwner = Owner;
			originIndex = transform.GetSiblingIndex();
			if (Owner != null) Owner.Detach(this);
			transform.SetParent(editor.DragLayer, true);
		}

		dragged.StartDrag(eventData);
	}

	void StartDrag(PointerEventData eventData)
	{
		dragging = true;
		// 끄는 블록이 마우스를 가리면 아래에 있는 놓을 자리를 찾을 수 없으므로 클릭을 통과시킨다
		canvasGroup.blocksRaycasts = false;
		MoveTo(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (dragging) MoveTo(eventData);
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		dragging = false;
		canvasGroup.blocksRaycasts = true;

		BlockList target = FindDropTarget(eventData);
		if (target == null)
		{
			// 블록 목록이나 빈 곳에 놓으면 지운다
			Destroy(gameObject);
			editor.RefreshLayout();
			return;
		}

		int index = target.GetInsertIndex(eventData.position, eventData.pressEventCamera);
		if (target.CanInsert(Data, index))
		{
			target.Insert(this, index);
			return;
		}

		// 놓을 수 없는 자리 (예: if 없이 else): 원래 자리로 되돌리고, 새로 꺼낸 견본이면 지운다
		if (originOwner != null) originOwner.Insert(this, originIndex);
		else
		{
			Destroy(gameObject);
			editor.RefreshLayout();
		}
	}

	void MoveTo(PointerEventData eventData)
	{
		RectTransform layer = editor.DragLayer;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, eventData.position, eventData.pressEventCamera, out Vector2 local);
		transform.localPosition = local;
	}

	// 마우스 아래에서 가장 안쪽에 있는 블록 자리를 찾는다. if 안쪽 영역 위면 그 안쪽 자리가 먼저 잡힌다
	static BlockList FindDropTarget(PointerEventData eventData)
	{
		var results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, results);
		foreach (RaycastResult result in results)
		{
			BlockList list = result.gameObject.GetComponentInParent<BlockList>();
			if (list != null) return list;
		}
		return null;
	}
}
