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
/// - 한 줄(Row)에 글자, 값 칸, 이름 칸, 고르기 버튼을 늘어놓는다. 칸을 고치면 BlockData.Slots를 바로 고친다.<br/>
/// - 조건문·for·while은 안쪽 블록 영역(Inner Blocks)을 가진다. 다른 블록은 그 영역을 숨긴다.
/// </para>
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class BlockView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[SerializeField, Tooltip("블록 배경. 분류별 색이 들어간다")]
	Image background;

	[SerializeField, Tooltip("블록 한 줄. 글자와 칸이 이 아래에 만들어진다 (Horizontal Layout Group)")]
	RectTransform row;

	[SerializeField, Tooltip("조건문·for·while 안쪽에 블록을 쌓는 자리 (자식 오브젝트)")]
	BlockList innerBlocks;

	// 고르기 버튼에서 차례로 바꿔 가며 고르는 타입
	static readonly VariableTypeKind[] DeclareTypes =
	{
		VariableTypeKind.Int, VariableTypeKind.Float, VariableTypeKind.Bool, VariableTypeKind.Char, VariableTypeKind.String,
	};

	/// <summary>이 블록이 들고 있는 블록 데이터</summary>
	public BlockData Data { get; private set; }

	/// <summary>지금 이 블록이 들어 있는 블록 자리. 값 칸에 끼워져 있거나 끌고 있는 중이면 null</summary>
	public BlockList OwnerList { get; set; }

	/// <summary>지금 이 블록이 끼워져 있는 값 칸. 블록 자리에 있거나 끌고 있는 중이면 null</summary>
	public BlockValueSlot OwnerSlot { get; set; }

	BlockEditor editor;
	CanvasGroup canvasGroup;
	Func<BlockData> templateFactory; // 견본이면 새 블록 데이터를 만드는 함수, 아니면 null
	bool dragging;

	// 끌기 전에 있던 자리. 놓을 수 없는 곳에 놓으면 여기로 되돌린다
	BlockList originList;
	int originIndex;
	BlockValueSlot originSlot;

	// 데이터가 바뀌면 다시 써야 하는 글자들 (예: for의 "i <"는 이름 칸을 고치면 바뀜)
	readonly List<(TMP_Text text, Func<string> content)> dynamicTexts = new List<(TMP_Text, Func<string>)>();

	void Awake()
	{
		canvasGroup = GetComponent<CanvasGroup>();
	}

	/// <summary>
	/// 편집 영역에 놓이는 블록으로 준비한다. 칸과 블록 영역 안의 블록까지 화면에 만든다.
	/// </summary>
	public void Init(BlockEditor owner, BlockData data)
	{
		editor = owner;
		Data = data;
		background.color = BlockLabels.ColorOf(Data);

		bool hasInner = data.InnerBlocks != null;
		innerBlocks.gameObject.SetActive(hasInner);
		BuildRow();
		if (hasInner) innerBlocks.Bind(data.InnerBlocks, editor);
	}

	/// <summary>
	/// 블록 목록(팔레트)의 견본으로 준비한다. 칸 없이 글자 한 줄로만 보여 준다. factory는 끌 때마다 새 블록 데이터를 만든다.
	/// </summary>
	public void InitTemplate(BlockEditor owner, Func<BlockData> factory)
	{
		editor = owner;
		templateFactory = factory;
		Data = factory();
		background.color = BlockLabels.ColorOf(Data);
		innerBlocks.gameObject.SetActive(false);
		AddText(() => editor.LabelOf(Data));
		RefreshTexts();
	}

	/// <summary>
	/// 데이터나 놓인 자리가 바뀌었을 때 글자를 다시 쓴다.
	/// </summary>
	public void RefreshTexts()
	{
		foreach ((TMP_Text text, Func<string> content) in dynamicTexts) text.text = content();
	}

	// 블록 종류마다 한 줄을 만든다. 칸 번호는 function-data.md 「블록마다 가진 칸」 순서
	void BuildRow()
	{
		switch (Data.Kind)
		{
			case BlockKind.Declare:
				AddTypeOption(0);
				AddNameField(1);
				AddText("=");
				AddValue(2);
				AddText(";");
				break;
			case BlockKind.Assign:
				AddVariableField(0);
				AddText("=");
				AddValue(1);
				AddText(";");
				break;
			case BlockKind.Arithmetic:
				AddText("(");
				AddValue(0);
				AddOperatorOption(1, Enum.GetNames(typeof(ArithmeticOperator)));
				AddValue(2);
				AddText(")");
				break;
			case BlockKind.Compare:
				AddValue(0);
				AddOperatorOption(1, Enum.GetNames(typeof(CompareOperator)));
				AddValue(2);
				break;
			case BlockKind.Logic:
				AddValue(0);
				AddOperatorOption(1, Enum.GetNames(typeof(LogicOperator)));
				AddValue(2);
				break;
			case BlockKind.Not:
				AddText("!");
				AddValue(0);
				break;
			case BlockKind.Conditional:
				// if·else if·else는 블록 목록에서 따로 고른다. 놓은 뒤에는 바꾸지 않는다
				string branch = Data.Slots[0].Option;
				if (branch == nameof(ConditionalBranch.Else))
				{
					AddText("else");
					break;
				}
				AddText(BlockLabels.BranchKeyword(branch) + " (");
				AddValue(1);
				AddText(")");
				break;
			case BlockKind.For:
				AddText("for (int");
				AddNameField(0);
				AddText("=");
				AddValue(1);
				AddText(() => $"; {Data.Slots[0].Name} <");
				AddValue(2);
				AddText(() => $"; {Data.Slots[0].Name}++)");
				break;
			case BlockKind.While:
				AddText("while (");
				AddValue(0);
				AddText(")");
				break;
			case BlockKind.Break:
				AddText("break;");
				break;
			case BlockKind.Continue:
				AddText("continue;");
				break;
			case BlockKind.ArrayDeclare:
				AddTypeOption(0);
				AddNameField(1);
				AddText("[");
				AddValue(2);
				AddText("];");
				break;
			case BlockKind.Malloc:
				AddText(() => $"malloc({BlockLabels.TypeName(Data.Slots[0].Type)})");
				break;
			case BlockKind.Free:
				AddText("free(");
				AddValue(0);
				AddText(");");
				break;
			case BlockKind.Function:
				AddText(() => editor.FunctionNameOf(Data) + "(");
				for (int i = 0; i < Data.Slots.Count; i++)
				{
					if (i > 0) AddText(",");
					AddValue(i);
				}
				// 값 칸에 끼우면 문장이 아니므로 ;를 붙이지 않는다
				AddText(() => OwnerSlot != null ? ")" : ");");
				break;
			case BlockKind.Return:
				AddText("return");
				AddValue(0);
				AddText(";");
				break;
			case BlockKind.Wait:
				AddText("wait(");
				AddValue(0);
				AddText("초);");
				break;
			default:
				AddText(Data.Kind.ToString());
				break;
		}
		RefreshTexts();
	}

	void AddText(string content) => editor.CreateText(row).text = content;

	void AddText(Func<string> content) => dynamicTexts.Add((editor.CreateText(row), content));

	void AddValue(int index)
	{
		if (index >= Data.Slots.Count) return;
		editor.CreateValueSlot(row).Bind(editor, this, index);
	}

	// 새 이름 칸 (선언의 변수 이름, for의 i). 이름으로 쓸 수 없는 글자면 원래 이름으로 되돌린다
	void AddNameField(int index)
	{
		SlotData slot = Data.Slots[index];
		BlockField field = editor.CreateField(row);
		field.Text = slot.Name;
		field.Edited += text =>
		{
			text = text.Trim();
			if (BlockSlotText.IsIdentifier(text)) slot.Name = text;
			field.Text = slot.Name;
			RefreshTexts();
			editor.NotifyChanged();
		};
	}

	// 변수 칸 (대입의 왼쪽). 이름으로 쓸 수 없는 글자면 원래 이름으로 되돌린다
	void AddVariableField(int index)
	{
		SlotData slot = Data.Slots[index];
		BlockField field = editor.CreateField(row);
		field.Text = slot.VariableName;
		field.Edited += text =>
		{
			text = text.Trim();
			if (BlockSlotText.IsIdentifier(text)) slot.VariableName = text;
			field.Text = slot.VariableName;
			editor.NotifyChanged();
		};
	}

	// 연산자 고르기. 누를 때마다 다음 연산자로 바뀐다. 데이터에는 enum 이름("Add")을 넣는다
	void AddOperatorOption(int index, string[] options)
	{
		SlotData slot = Data.Slots[index];
		TMP_Text label = AddOptionButton(() =>
		{
			int current = Array.IndexOf(options, slot.Option);
			slot.Option = options[(current + 1) % options.Length];
		});
		dynamicTexts.Add((label, () => BlockLabels.OperatorText(slot.Option)));
	}

	// 타입 고르기. 누를 때마다 int → float → bool → char → string 순서로 바뀐다
	void AddTypeOption(int index)
	{
		SlotData slot = Data.Slots[index];
		TMP_Text label = AddOptionButton(() =>
		{
			int current = slot.Type == null ? -1 : Array.IndexOf(DeclareTypes, slot.Type.Kind);
			slot.Type = new VariableType(DeclareTypes[(current + 1) % DeclareTypes.Length]);
		});
		dynamicTexts.Add((label, () => BlockLabels.TypeName(slot.Type)));
	}

	TMP_Text AddOptionButton(Action onClick)
	{
		Button button = editor.CreateButton(row);
		button.onClick.AddListener(() =>
		{
			onClick();
			RefreshTexts();
			editor.NotifyChanged();
		});
		return button.GetComponentInChildren<TMP_Text>();
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
			originList = OwnerList;
			originSlot = OwnerSlot;
			originIndex = transform.GetSiblingIndex();
			if (OwnerList != null) OwnerList.Detach(this);
			if (OwnerSlot != null) OwnerSlot.Detach(this);
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

		Component target = FindDropTarget(eventData);
		if (target == null)
		{
			// 블록 목록이나 빈 곳에 놓으면 지운다
			editor.DestroyView(this);
			return;
		}

		if (target is BlockValueSlot slot)
		{
			slot.Accept(this);
			return;
		}

		BlockList list = (BlockList)target;
		int index = list.GetInsertIndex(eventData.position, eventData.pressEventCamera);
		if (list.CanInsert(Data, index))
		{
			list.Insert(this, index);
			return;
		}

		ReturnToOrigin();
	}

	// 놓을 수 없는 자리 (예: if 없이 else, 블록 자리에 연산 블록): 원래 자리로 되돌리고, 새로 꺼낸 견본이면 지운다
	void ReturnToOrigin()
	{
		if (originList != null) originList.Insert(this, originIndex);
		else if (originSlot != null && originSlot.CanAccept(Data)) originSlot.Accept(this);
		else editor.DestroyView(this);
	}

	void MoveTo(PointerEventData eventData)
	{
		RectTransform layer = editor.DragLayer;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, eventData.position, eventData.pressEventCamera, out Vector2 local);
		transform.localPosition = local;
	}

	// 마우스 아래에서 가장 안쪽에 있는 놓을 자리를 찾는다.
	// 값 칸은 이 블록을 끼울 수 있을 때만 고르고, 아니면 더 바깥의 블록 자리를 찾는다
	Component FindDropTarget(PointerEventData eventData)
	{
		var results = new List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, results);
		foreach (RaycastResult result in results)
		{
			for (Transform t = result.gameObject.transform; t != null; t = t.parent)
			{
				if (t.TryGetComponent(out BlockValueSlot slot))
				{
					if (slot.CanAccept(Data)) return slot;
					continue;
				}
				if (t.TryGetComponent(out BlockList list)) return list;
			}
		}
		return null;
	}
}
