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

	[Header("모양 (이우림 「블록 견본」)")]
	[SerializeField, Tooltip("문장 블록: 위 왼쪽에 홈이 있는 네모 (blk_stmt)")]
	Sprite statementSprite;

	[SerializeField, Tooltip("값 블록: 양 끝이 둥근 알약 (blk_value)")]
	Sprite valueSprite;

	[SerializeField, Tooltip("블록 목록의 칩: 홈 없는 네모 (blk_chip)")]
	Sprite chipSprite;

	[SerializeField, Tooltip("조건문·반복문의 안쪽: 왼쪽 기둥만 블록 색이 보이는 그림 (blk_inner)")]
	Sprite innerSprite;

	[SerializeField, Tooltip("안쪽 바탕 색 (편집 영역 바탕과 같게)")]
	Color innerColor = new Color32(0x14, 0x18, 0x23, 0xFF);

	[SerializeField, Tooltip("이름 칸 밑줄 (blk_underline)")]
	Sprite nameFieldSprite;

	[SerializeField, Tooltip("변수 칸 점선 상자 (blk_dashed)")]
	Sprite variableFieldSprite;

	[SerializeField, Tooltip("이름 칸·변수 칸 선 색")]
	Color fieldLineColor = new Color(1, 1, 1, 0.75f);

	[SerializeField, Tooltip("조건문·반복문 아래 막대 높이")]
	int containerBottom = 7;

	[SerializeField, Tooltip("컴파일 오류가 난 블록 색 (반복문 밖의 break 등)")]
	Color errorColor = new Color32(0xC8, 0x48, 0x58, 0xFF);

	[SerializeField, Range(0.3f, 1f), Tooltip("끌고 있는 블록의 불투명도")]
	float dragAlpha = 0.75f;

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
		ApplyShape(IsValueBlock(data) ? valueSprite : statementSprite, IsValueBlock(data), hasInner);
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
		bool value = IsValueBlock(Data);
		ApplyShape(value ? valueSprite : chipSprite, value, false, true);
		AddText(() => editor.PaletteNameOf(Data));
		RefreshTexts();
	}

	// 값 칸에 끼우는 블록 (알약 모양)
	static bool IsValueBlock(BlockData data) =>
		data.Kind is BlockKind.Arithmetic or BlockKind.Compare or BlockKind.Logic or BlockKind.Not or BlockKind.ArrayGet or BlockKind.Malloc;

	/// <summary>
	/// 컴파일 오류 표시 (블록을 빨갛게). BlockEditor가 컴파일할 때 부르고, 고치면 끈다.
	/// </summary>
	public void SetError(bool on) => background.color = on ? errorColor : BlockLabels.ColorOf(Data);

	// 블록 모양: 바탕 그림, 한 줄의 여백, 조건문·반복문의 안쪽과 아래 막대
	void ApplyShape(Sprite sprite, bool value, bool hasInner, bool chip = false)
	{
		if (sprite != null)
		{
			background.sprite = sprite;
			background.type = Image.Type.Sliced;
		}

		if (row.TryGetComponent(out HorizontalLayoutGroup rowLayout))
		{
			// 문장 블록은 위의 홈(3픽셀)만큼 위 여백을 더 둔다
			rowLayout.padding = chip ? new RectOffset(5, 5, 2, 3)
				: value ? new RectOffset(6, 6, 2, 2)
				: new RectOffset(5, 6, 5, 3);
			rowLayout.childAlignment = chip ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
		}

		if (TryGetComponent(out VerticalLayoutGroup layout))
			layout.padding = new RectOffset(0, 0, 0, hasInner ? containerBottom : 0);

		if (hasInner && innerBlocks.TryGetComponent(out Image innerImage) && innerSprite != null)
		{
			innerImage.sprite = innerSprite;
			innerImage.type = Image.Type.Sliced;
			innerImage.color = innerColor;
		}
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
			case BlockKind.ArrayGet:
				AddVariableField(0);
				AddText("[");
				AddValue(1);
				AddText("]");
				break;
			case BlockKind.ArraySet:
				AddVariableField(0);
				AddText("[");
				AddValue(1);
				AddText("] =");
				AddValue(2);
				AddText(";");
				break;
			case BlockKind.Malloc:
				AddText("malloc(");
				AddStructOption(0);
				AddText(")");
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
		field.SetBackground(nameFieldSprite, fieldLineColor);
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
		field.SetBackground(variableFieldSprite, fieldLineColor);
		field.Text = slot.VariableName;
		field.Edited += text =>
		{
			text = text.Trim();
			if (BlockSlotText.IsIdentifier(text)) slot.VariableName = text;
			field.Text = slot.VariableName;
			editor.NotifyChanged();
		};
	}

	// 연산자 고르기 (드롭다운). 데이터에는 enum 이름("Add")을 넣는다
	void AddOperatorOption(int index, string[] options)
	{
		SlotData slot = Data.Slots[index];
		string[] labels = Array.ConvertAll(options, BlockLabels.OperatorText);
		TMP_Text label = AddDropdown(labels, () => Array.IndexOf(options, slot.Option), picked => slot.Option = options[picked]);
		dynamicTexts.Add((label, () => BlockLabels.OperatorText(slot.Option)));
	}

	// 타입 고르기 (드롭다운): int, float, bool, char, string
	void AddTypeOption(int index)
	{
		SlotData slot = Data.Slots[index];
		string[] labels = Array.ConvertAll(DeclareTypes, kind => BlockLabels.TypeName(new VariableType(kind)));
		TMP_Text label = AddDropdown(labels,
			() => slot.Type == null ? -1 : Array.IndexOf(DeclareTypes, slot.Type.Kind),
			picked => slot.Type = new VariableType(DeclareTypes[picked]));
		dynamicTexts.Add((label, () => BlockLabels.TypeName(slot.Type)));
	}

	// malloc의 struct 고르기 (드롭다운): 게임이 정한 struct 목록 (PredefinedStructs.All)
	void AddStructOption(int index)
	{
		SlotData slot = Data.Slots[index];
		string[] names = new string[PredefinedStructs.All.Count];
		for (int i = 0; i < names.Length; i++) names[i] = PredefinedStructs.All[i].Name;
		TMP_Text label = AddDropdown(names,
			() => Array.IndexOf(names, slot.Type?.StructName),
			picked => slot.Type = VariableType.StructOf(names[picked]));
		dynamicTexts.Add((label, () => slot.Type?.StructName ?? "?"));
	}

	// 누르면 고를 수 있는 것 목록이 아래에 열리는 칸. 고르면 pick을 부르고 글자를 다시 쓴다
	TMP_Text AddDropdown(string[] labels, Func<int> current, Action<int> pick)
	{
		Button button = editor.CreateButton(row);
		var anchor = (RectTransform)button.transform;
		button.onClick.AddListener(() => editor.ShowDropdown(anchor, labels, current(), picked =>
		{
			pick(picked);
			RefreshTexts();
			editor.NotifyChanged();
		}));
		return button.GetComponentInChildren<TMP_Text>();
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		BlockView dragged = this;

		if (templateFactory != null)
		{
			// 견본: 새 블록을 만들어서 그걸 끈다. 이후 끌기 이벤트는 새 블록이 받는다
			dragged = editor.CreateView(templateFactory(), editor.DragLayer);
			// 끄는 블록은 칩이 아니라 칸이 다 있는 블록이므로, 그 블록이 원하는 크기로 맞춘다
			var draggedRect = (RectTransform)dragged.transform;
			LayoutRebuilder.ForceRebuildLayoutImmediate(draggedRect);
			draggedRect.sizeDelta = new Vector2(LayoutUtility.GetPreferredWidth(draggedRect), LayoutUtility.GetPreferredHeight(draggedRect));
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
			editor.ShowTrash(true); // 놓인 블록을 끌 때만: 블록 목록 위에 「여기에 놓으면 지워요」
		}

		dragged.StartDrag(eventData);
	}

	void StartDrag(PointerEventData eventData)
	{
		dragging = true;
		canvasGroup.alpha = dragAlpha; // 아직 놓이지 않았다는 표시
		// 끄는 블록이 마우스를 가리면 아래에 있는 놓을 자리를 찾을 수 없으므로 클릭을 통과시킨다
		canvasGroup.blocksRaycasts = false;
		MoveTo(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		MoveTo(eventData);
		editor.ShowDropPreview(this, FindDropTarget(eventData), eventData); // 들어갈 자리 줄, 끼울 수 있는 값 칸 표시
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		dragging = false;
		canvasGroup.blocksRaycasts = true;
		canvasGroup.alpha = 1f;
		editor.ShowTrash(false);
		editor.HideDropPreview();

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
