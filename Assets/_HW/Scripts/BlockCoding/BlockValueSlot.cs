using UnityEngine;

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

	/// <summary>이 칸에 끼운 블록. 없으면 null</summary>
	public BlockView Held { get; private set; }

	/// <summary>이 칸의 데이터 (블록 데이터의 Slots[칸 번호])</summary>
	public SlotData Slot => ownerBlock.Data.Slots[slotIndex];

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

		if (Slot.Block != null) Hold(editor.CreateView(Slot.Block, transform));
		else field.Text = BlockSlotText.ToText(Slot);
	}

	/// <summary>
	/// block을 이 칸에 끼울 수 있으면 true. 비어 있고, 값 칸에 들어가는 블록이어야 한다.
	/// </summary>
	public bool CanAccept(BlockData block) => Held == null && editor.CanBeValue(block);

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
		field.Text = "";
		field.SetInvalid(false);
		editor.NotifyChanged();
	}

	void Hold(BlockView view)
	{
		Held = view;
		view.transform.SetParent(transform, false);
		view.OwnerSlot = this;
		view.OwnerList = null;
		view.RefreshTexts();
		field.gameObject.SetActive(false);
	}

	void OnEdited(string text)
	{
		bool valid = BlockSlotText.TryApply(Slot, text);
		field.SetInvalid(!valid);
		editor.NotifyChanged();
	}
}
