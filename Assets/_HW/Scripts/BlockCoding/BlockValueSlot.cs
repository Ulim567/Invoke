using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 블록 안의 값 칸 하나. 키보드로 값·변수를 적거나, 값 블록(연산, 비교, 논리, 값을 돌려주는 함수)을 끌어다 끼운다.
/// <para>
/// 칸 데이터는 블록 데이터의 Slots[칸 번호]를 직접 고친다 (function-data.md 「칸 고치기」).<br/>
/// - 적은 값: Type, Literal / 적은 변수: VariableName (BlockSlotText가 글자를 보고 정한다)<br/>
/// - 끼운 블록: Block. 끼우면 글자 칸은 숨고, 빼면 다시 나온다.
/// </para>
/// <para>
/// 프리팹: UI → Image 에 Horizontal Layout Group, Layout Element와 이 컴포넌트를 붙인다.
/// 블록을 놓을 수 있도록 Image의 Raycast Target을 켠다. 글자 칸(BlockField)은 BlockEditor가 만들어 넣는다.
/// </para>
/// </summary>
public class BlockValueSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	BlockEditor editor;
	BlockView ownerBlock;
	int slotIndex;
	BlockField field;
	// 값 칸 옆의 「변수 고르기」 버튼 (#474 5단계). 직접 적은 값만 받는 칸(전역 변수의 처음 값, 배열 길이)에는 없다
	Button pickButton;

	/// <summary>이 칸에 끼운 블록. 없으면 null</summary>
	public BlockView Held { get; private set; }

	/// <summary>이 칸의 데이터 (블록 데이터의 Slots[칸 번호])</summary>
	public SlotData Slot => ownerBlock.Data.Slots[slotIndex];

	/// <summary>이 칸을 가진 블록의 데이터와 그 블록에서의 칸 번호. BlockEditor가 컴파일 오류가 난 칸(FunctionChecker.Error.Slots)의 UI를 찾을 때 쓴다 (#504)</summary>
	public BlockData OwnerData => ownerBlock.Data;
	public int Index => slotIndex;

	/// <summary>
	/// 꼭 채워야 하는데 비어 있는 칸이면 true (컴파일 오류 표시). 선언의 초기값과 return의 값은 비워도 된다.
	/// </summary>
	public bool IsMissingValue
	{
		get
		{
			if (Held != null || Slot == null || Slot.Kind != SlotKind.Value) return false;
			BlockKind kind = ownerBlock.Data.Kind;
			if (kind == BlockKind.Return || (kind == BlockKind.Declare && slotIndex == 2)) return false;
			return string.IsNullOrEmpty(Slot.Literal) && string.IsNullOrEmpty(Slot.VariableName);
		}
	}

	[Header("모양 (게임 UI 캔버스 시안 6, #487). 비워 두면 프리팹의 그림과 색을 그대로 쓴다")]
	[SerializeField, Tooltip("값이 있는 칸: 1픽셀 테두리 알약 (blk_slot_line, Sliced)")]
	Sprite filledSprite;

	[SerializeField, Tooltip("빈 칸: 1픽셀 점선 테두리 알약 (blk_slot_dashed, Tiled). 점선이 늘어나지 않게 Tiled로 그린다")]
	Sprite emptySprite;

	[SerializeField, Tooltip("값이 있는 칸의 테두리 색")]
	Color filledColor = new Color32(0xF4, 0xF1, 0xFA, 0x4D); // 30%

	[SerializeField, Tooltip("빈 칸의 점선 색")]
	Color emptyColor = new Color32(0xF4, 0xF1, 0xFA, 0x73); // 45%

	[Header("블록을 끄는 동안 (시안 6-11, #487)")]
	[SerializeField, Tooltip("끄는 블록을 받을 수 있는 칸: 2픽셀 테두리 알약 (blk_slot_bold, Sliced). 비워 두면 값이 있는 칸 그림을 쓴다")]
	Sprite acceptSprite;

	[SerializeField, Tooltip("끄는 블록을 받을 수 있는 칸의 테두리와 안내 글자 색")]
	Color acceptColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);

	[SerializeField, Tooltip("끄는 블록을 받을 수 없는 칸의 점선 색")]
	Color rejectColor = new Color32(0xF4, 0xF1, 0xFA, 0x2E); // 18%

	[SerializeField, Tooltip("끄는 블록을 받을 수 없는 칸의 안내 글자 색")]
	Color rejectTextColor = new Color32(0x5A, 0x54, 0x70, 0xFF);

	/// <summary>블록을 끄는 동안 빈 칸에 보이는 표시</summary>
	public enum DragHint
	{
		None,   // 끄는 중이 아니거나, 값 칸에 넣을 수 없는 블록(if, void 함수 등)을 끄는 중
		Accept, // 끄는 블록을 이 칸에 끼울 수 있음: 청록 테두리
		Reject, // 끄는 블록을 이 칸에 끼울 수 없음: 어두운 점선
	}

	// 칸 테두리 색: 컴파일 오류(빨강). 끄는 블록을 놓을 수 있다는 보라색은 그림이 없는 예전 프리팹에서만 쓴다
	static readonly Color HighlightColor = new Color32(0x8B, 0x9C, 0xFF, 0xC0);
	static readonly Color ErrorColor = new Color32(0xE0, 0x50, 0x60, 0xC0);
	Image image;
	Color baseColor;
	bool highlighted;
	bool error;
	DragHint dragHint;
	RectOffset emptyPadding; // 빈 칸일 때의 여백 (프리팹 값). 블록을 끼우면 0으로 했다가 빼면 되돌린다
	Vector2 emptyMinSize;    // 빈 칸일 때의 최소 크기 (프리팹 값)
	bool ghostShown; // 끄는 동안의 반투명 미리보기를 이 칸에 보이는 중인지 (#476)

	/// <summary>끌고 있는 값 블록을 여기에 놓을 수 있다는 표시 (BlockEditor가 부른다)</summary>
	public void SetHighlight(bool on)
	{
		highlighted = on;
		ApplyColor();
	}

	/// <summary>
	/// 끄는 동안의 반투명 미리보기(BlockEditor가 만든다, #476)를 이 칸에 끼운 것처럼 보인다. 적는 칸과 변수 고르기 버튼은 그동안 숨긴다.
	/// 칸 데이터는 바꾸지 않는다.
	/// </summary>
	public void ShowGhost(RectTransform ghost)
	{
		ghost.SetParent(transform, false);
		ghost.SetAsLastSibling();
		ghostShown = true;
		field.gameObject.SetActive(false);
		if (pickButton != null) pickButton.gameObject.SetActive(false);
	}

	/// <summary>미리보기를 거둘 때 적는 칸과 변수 고르기 버튼을 다시 보인다 (BlockEditor가 미리보기를 다른 곳으로 옮긴 뒤 부른다)</summary>
	public void HideGhost()
	{
		if (!ghostShown) return;
		ghostShown = false;
		if (Held != null) return; // 블록을 끼운 칸이면 원래 숨겨 둔 상태
		field.gameObject.SetActive(true);
		if (pickButton != null) pickButton.gameObject.SetActive(true);
	}

	/// <summary>
	/// 블록을 끄는 동안의 표시를 바꾼다 (BlockEditor가 끌기를 시작할 때와 놓을 때 부른다, #487).
	/// 값이 들어 있는 칸은 끼울 수 없으므로 BlockEditor가 빈 칸에만 Accept·Reject를 준다.
	/// </summary>
	public void SetDragHint(DragHint hint)
	{
		dragHint = hint;
		ApplyColor();
	}

	/// <summary>컴파일 오류(비어 있는 칸) 표시 (BlockEditor가 부른다)</summary>
	public void SetError(bool on)
	{
		error = on;
		ApplyColor();
	}

	/// <summary>
	/// 칸 모양을 지금 내용에 맞춘다 (#487): 값이 있으면 실선 테두리, 비었으면 점선 테두리, 블록을 끼웠으면 테두리 없음.
	/// 칸을 고친 뒤 BlockEditor가 화면을 다시 그릴 때 부른다.
	/// </summary>
	public void RefreshLook() => ApplyColor();

	void ApplyColor()
	{
		if (image == null)
		{
			if (!TryGetComponent(out image)) return;
			baseColor = image.color;
		}

		// 그림이 없는 예전 프리팹: 바탕 색만 바꾼다
		if (filledSprite == null || emptySprite == null)
		{
			image.color = highlighted ? HighlightColor : error ? ErrorColor : baseColor;
			return;
		}

		bool empty = Held == null && IsEmptyContent;
		// 마우스 아래의 놓을 칸(highlighted)도 받을 수 있는 칸과 같은 청록으로 보인다. 그 칸에는 반투명 미리보기가 들어가 있다
		bool accept = highlighted || dragHint == DragHint.Accept;
		bool reject = !accept && dragHint == DragHint.Reject;
		if (accept)
		{
			image.sprite = acceptSprite != null ? acceptSprite : filledSprite;
			image.type = Image.Type.Sliced;
			image.color = acceptColor;
		}
		else
		{
			image.sprite = empty ? emptySprite : filledSprite;
			image.type = empty ? Image.Type.Tiled : Image.Type.Sliced;
			// 끼운 블록은 자기 바탕(알약)이 있으므로 칸 테두리를 숨긴다. 투명해도 놓을 자리 찾기(Raycast)는 그대로 받는다
			Color normal = Held != null ? Color.clear : empty ? emptyColor : filledColor;
			image.color = error ? ErrorColor : reject ? rejectColor : normal;
		}
		if (field != null) field.SetPlaceholderColor(accept ? acceptColor : reject ? rejectTextColor : (Color?)null);
	}

	/// <summary>끼운 블록, 적은 값, 변수가 모두 없는 칸이면 true. 블록을 끄는 동안 표시할 칸을 고를 때 BlockEditor가 본다 (#487)</summary>
	public bool IsEmpty => Held == null && IsEmptyContent;

	// 적은 값, 변수, 끼운 블록이 모두 없으면 true. 칸 글자를 잘못 적어 데이터가 비었을 때도 빈 칸으로 본다 (글자는 빨갛게 남음)
	bool IsEmptyContent =>
		Slot == null || (Slot.Block == null && string.IsNullOrEmpty(Slot.Literal) && string.IsNullOrEmpty(Slot.VariableName));

	/// <summary>
	/// 블록의 slotIndex번 칸과 연결한다. 칸에 블록이 이미 있으면 (불러온 함수) 그 블록도 화면에 만든다.
	/// </summary>
	public void Bind(BlockEditor owner, BlockView block, int index)
	{
		editor = owner;
		ownerBlock = block;
		slotIndex = index;

		field = editor.CreateField(transform);
		field.Edited += OnEdited;
		if (!editor.RuleOf(ownerBlock.Data, slotIndex).LiteralOnly) AddPickButton();

		if (Slot.Block != null) Hold(editor.CreateView(Slot.Block, transform));
		else field.Text = BlockSlotText.ToText(Slot);
		RefreshPlaceholder();
		RefreshLook();
	}

	/// <summary>
	/// 빈 칸에 흐리게 보일 안내 글자를 다시 쓴다 (#476). 이 칸이 받는 타입 (예: int, bool, Target, Sword*).
	/// 선언의 타입이나 대입의 변수를 바꾸면 받는 타입이 바뀌므로 BlockEditor가 고칠 때마다 부른다.
	/// </summary>
	public void RefreshPlaceholder()
	{
		if (field == null) return;
		field.Placeholder = SlotRules.ShortLabel(editor.RuleOf(ownerBlock.Data, slotIndex));
	}

	// 마우스를 올리면 잠깐 뒤에 설명창 (#476): 이 칸에 넣을 수 있는 것. 함수의 파라미터면 이름·타입·설명도.
	// 칸에 블록을 끼웠으면 그 블록의 설명이 위에 겹쳐 보인다 (BlockEditor가 가장 안쪽 것을 보인다)
	public void OnPointerEnter(PointerEventData eventData)
	{
		if (editor == null || eventData.dragging) return;
		editor.EnterHover(this, (RectTransform)transform, () => editor.SlotTooltipOf(ownerBlock.Data, slotIndex));
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (editor != null) editor.ExitHover(this);
	}

	// 「변수 고르기」 버튼: 누르면 이 칸에 넣을 수 있는 변수(이 블록 자리에서 보이고, 타입이 칸 규칙에 맞는 것)를 드롭다운으로 보여 준다 (#474 5단계)
	// 키보드로 이름을 적는 것과 같은 결과다. 변수 이름을 잘못 적지 않게 하고, 무엇을 넣을 수 있는지 보이게 하기 위함
	void AddPickButton()
	{
		pickButton = editor.CreateButton(transform);
		// 글자 없이 버튼 프리팹의 ▾만 보인다. 칸이 길어지지 않게 하기 위함
		TMP_Text label = pickButton.GetComponentInChildren<TMP_Text>();
		if (label != null) label.text = "";
		// 버튼 바탕(밝은 네모)은 숨기고 ▾ 삼각형만 보인다 (#487). 칸 안에 네모가 하나 더 있으면 칸이 복잡해 보이기 때문
		// 바탕은 투명하게만 두어 누르는 자리는 그대로 받고, 마우스를 올렸을 때의 색 변화는 삼각형이 받는다
		Transform arrow = pickButton.transform.Find("Arrow");
		if (arrow != null && arrow.TryGetComponent(out Image arrowImage) && pickButton.targetGraphic != null)
		{
			pickButton.targetGraphic.color = Color.clear;
			pickButton.targetGraphic = arrowImage;
		}
		var anchor = (RectTransform)pickButton.transform;
		pickButton.onClick.AddListener(() =>
		{
			var names = new List<string>();
			var labels = new List<string>();
			foreach (KeyValuePair<string, VariableType> variable in editor.VisibleVariables(ownerBlock.Data))
			{
				if (!editor.Fits(ownerBlock.Data, slotIndex, SlotData.FromVariable(variable.Key))) continue;
				names.Add(variable.Key);
				labels.Add($"{BlockLabels.TypeName(variable.Value)} {variable.Key}");
			}
			if (names.Count == 0)
			{
				editor.ShowNotice("이 칸에 넣을 수 있는 변수 없음");
				return;
			}
			editor.ShowDropdown(anchor, labels.ToArray(), names.IndexOf(Slot.VariableName), picked =>
			{
				if (names[picked] == Slot.VariableName) return; // 같은 변수를 다시 고르면 고친 것으로 치지 않는다
				Slot.Type = null;
				Slot.Literal = null;
				Slot.VariableName = names[picked];
				field.Text = names[picked];
				field.SetInvalid(false);
				editor.NotifyChanged();
			});
		});
	}

	/// <summary>
	/// 이 칸에 넣은 값(적은 값, 변수, 끼운 블록)이 칸 규칙(SlotRules)에 맞지 않으면 true (컴파일 오류 표시, #474).
	/// 선언의 타입이나 함수의 반환 타입을 나중에 바꾸면 이미 넣은 값이 맞지 않게 될 수 있다.
	/// </summary>
	public bool HasWrongValue => Slot != null && Slot.Kind == SlotKind.Value && !editor.Fits(ownerBlock.Data, slotIndex, Slot);

	/// <summary>이 칸에 넣을 수 있는 것을 플레이어에게 보일 글자. 예: 「정수」</summary>
	public string AcceptsText => SlotRules.Describe(editor.RuleOf(ownerBlock.Data, slotIndex));

	/// <summary>
	/// block을 이 칸에 끼울 수 있으면 true. 비어 있고, 값 칸에 들어가는 블록이고, 칸 규칙(SlotRules)에 맞아야 한다 (#474).
	/// </summary>
	public bool CanAccept(BlockData block) =>
		Held == null && editor.CanBeValue(block) && editor.FitsBlock(ownerBlock.Data, slotIndex, block);

	/// <summary>
	/// 끌어다 놓은 블록을 이 칸에 끼운다. 적어 둔 값은 지워진다.
	/// </summary>
	public void Accept(BlockView view)
	{
		Slot.Type = null;
		Slot.Literal = null;
		Slot.VariableName = null;
		Slot.Block = view.Data;
		Hold(view);
		editor.NotifyChanged();
	}

	/// <summary>
	/// 끼운 블록을 뺀다 (끌기 시작할 때). 칸 데이터의 Block이 null이 된다.
	/// </summary>
	public void Detach(BlockView view)
	{
		if (Held != view) return;
		Slot.Block = null;
		Held = null;
		view.OwnerSlot = null;
		FitToHeld(false);
		field.gameObject.SetActive(true);
		if (pickButton != null) pickButton.gameObject.SetActive(true);
		field.Text = "";
		field.SetInvalid(false);
		editor.NotifyChanged();
	}

	/// <summary>
	/// 칸을 비운다: 끼운 블록은 지우고, 적은 값과 칸 글자도 지운다. 데이터가 바뀌었을 때만 고친 것으로 친다.
	/// 선언의 처음 값 칸을 접을 때 BlockView가 부른다 (#474).
	/// </summary>
	public void Clear()
	{
		bool changed = Held != null || Slot.Type != null || Slot.Literal != null || Slot.VariableName != null;
		if (Held != null)
		{
			BlockView view = Held;
			Detach(view);
			editor.DestroyView(view);
		}
		Slot.Type = null;
		Slot.Literal = null;
		Slot.VariableName = null;
		field.Text = "";
		field.SetInvalid(false);
		if (changed) editor.NotifyChanged();
	}

	void Hold(BlockView view)
	{
		Held = view;
		view.transform.SetParent(transform, false);
		view.OwnerSlot = this;
		view.OwnerList = null;
		view.RefreshTexts();
		field.gameObject.SetActive(false);
		if (pickButton != null) pickButton.gameObject.SetActive(false);
		ApplyColor(); // 끼운 블록이 보이도록 칸 테두리를 숨긴다
		FitToHeld(true);
		view.RefreshLook(); // 칸에 들어가면 줄 여백이 얇아지고, 겹친 깊이에 따라 칠이 바뀐다
	}

	// 블록을 끼운 칸은 여백과 최소 크기를 없애 블록 크기에 딱 맞춘다 (#487). 테두리를 숨긴 칸이 여백만 남아 겹칠수록 두꺼워지지 않게
	// 빈 칸으로 돌아오면 프리팹의 여백·최소 크기로 되돌린다 (끌어 놓기 쉽게 #476에서 키운 크기)
	void FitToHeld(bool held)
	{
		if (!TryGetComponent(out HorizontalLayoutGroup layout) || !TryGetComponent(out LayoutElement size)) return;
		if (emptyPadding == null)
		{
			emptyPadding = layout.padding;
			emptyMinSize = new Vector2(size.minWidth, size.minHeight);
		}
		layout.padding = held ? new RectOffset(0, 0, 0, 0) : emptyPadding;
		size.minWidth = held ? -1 : emptyMinSize.x;
		size.minHeight = held ? -1 : emptyMinSize.y;
	}

	// 적은 글자를 값으로 읽고, 칸 규칙에 맞는지 본다 (#474). 예: if의 조건 칸에 "abc"
	// 변수 이름이면 이 블록 자리에서 보이는 변수인지, 그 타입이 칸에 맞는지도 본다 (#474 5단계). 예: 선언하지 않은 hp, int x를 thrust에
	// 맞지 않는 값은 칸 글자를 빨갛게 하고, 상태 줄에 컴파일 오류 창과 같은 문장으로 이유를 알린다
	// 데이터는 비우지 않고 남긴다 (#504). 비우면 컴파일 오류 창에 진짜 이유 대신 「expression이 비어 있음」이 나오기 때문
	// 남겨도 저장되지 않는다: 함수는 저장 전에 항상 컴파일하고(#432) FunctionChecker가 같은 값을 오류로 잡으며, 전역 변수는 저장 검사가 거부한다
	// 모양이 틀린 글자(3x)는 값으로 읽을 수 없으므로 지금처럼 비어 있다
	void OnEdited(string text)
	{
		bool valid = BlockSlotText.TryApply(Slot, text);
		string reason = null;
		// 직접 적은 값만 받는 칸(전역 변수의 초기값, array 크기)은 보이는 변수인지와 상관없이 칸 규칙의 문장으로 알린다
		bool literalOnly = editor.RuleOf(ownerBlock.Data, slotIndex).LiteralOnly;
		if (valid && !literalOnly && !string.IsNullOrEmpty(Slot.VariableName) && !editor.VisibleVariables(ownerBlock.Data).ContainsKey(Slot.VariableName))
			reason = FunctionChecker.UndeclaredMessage(Slot.VariableName);
		else if (valid && !editor.Fits(ownerBlock.Data, slotIndex, Slot))
			reason = editor.WrongValueMessage(ownerBlock.Data, slotIndex, Slot);
		field.SetInvalid(!valid || reason != null);
		editor.NotifyChanged();
		if (reason != null) editor.ShowNotice(reason);
	}
}
