using System.Collections.Generic;
using TMPro;
using UnityEngine;
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
public class BlockValueSlot : MonoBehaviour
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

	// 칸 바탕 색: 보통 / 끌고 있는 값 블록을 놓을 수 있음(보라) / 컴파일 오류(빨강)
	static readonly Color HighlightColor = new Color32(0x8B, 0x9C, 0xFF, 0xC0);
	static readonly Color ErrorColor = new Color32(0xE0, 0x50, 0x60, 0xC0);
	Image image;
	Color baseColor;
	bool highlighted;
	bool error;

	/// <summary>끌고 있는 값 블록을 여기에 놓을 수 있다는 표시 (BlockEditor가 부른다)</summary>
	public void SetHighlight(bool on)
	{
		highlighted = on;
		ApplyColor();
	}

	/// <summary>컴파일 오류(비어 있는 칸) 표시 (BlockEditor가 부른다)</summary>
	public void SetError(bool on)
	{
		error = on;
		ApplyColor();
	}

	void ApplyColor()
	{
		if (image == null)
		{
			if (!TryGetComponent(out image)) return;
			baseColor = image.color;
		}
		image.color = highlighted ? HighlightColor : error ? ErrorColor : baseColor;
	}

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
	}

	// 「변수 고르기」 버튼: 누르면 이 칸에 넣을 수 있는 변수(이 블록 자리에서 보이고, 타입이 칸 규칙에 맞는 것)를 드롭다운으로 보여 준다 (#474 5단계)
	// 키보드로 이름을 적는 것과 같은 결과다. 변수 이름을 잘못 적지 않게 하고, 무엇을 넣을 수 있는지 보이게 하기 위함
	void AddPickButton()
	{
		pickButton = editor.CreateButton(transform);
		// 글자 없이 버튼 프리팹의 ▾만 보인다. 칸이 길어지지 않게 하기 위함
		TMP_Text label = pickButton.GetComponentInChildren<TMP_Text>();
		if (label != null) label.text = "";
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
				editor.ShowNotice($"이 칸에 넣을 수 있는 변수가 없어요 ({AcceptsText})");
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
	}

	// 적은 글자를 값으로 읽고, 칸 규칙에 맞지 않으면 넣지 않는다 (#474). 예: if의 조건 칸에 "abc"
	// 변수 이름이면 이 블록 자리에서 보이는 변수인지, 그 타입이 칸에 맞는지도 본다 (#474 5단계). 예: 선언하지 않은 hp, int x를 thrust에
	// 맞지 않는 값도 모양이 틀린 글자(3x)처럼 칸 글자를 빨갛게 남기고 데이터는 비운다. 상태 줄에 이유를 알린다
	void OnEdited(string text)
	{
		bool valid = BlockSlotText.TryApply(Slot, text);
		string reason = null;
		if (valid && !string.IsNullOrEmpty(Slot.VariableName) && !editor.VisibleVariables(ownerBlock.Data).ContainsKey(Slot.VariableName))
			reason = $"{Slot.VariableName}은(는) 여기서 쓸 수 있는 변수가 아니에요. 이 블록보다 위에서 선언해야 해요";
		else if (valid && !editor.Fits(ownerBlock.Data, slotIndex, Slot))
			reason = $"이 칸에는 {AcceptsText}만 넣을 수 있어요";
		if (reason != null)
		{
			Slot.Type = null;
			Slot.Literal = null;
			Slot.VariableName = null;
		}
		field.SetInvalid(!valid || reason != null);
		editor.NotifyChanged();
		if (reason != null) editor.ShowNotice(reason);
	}
}
