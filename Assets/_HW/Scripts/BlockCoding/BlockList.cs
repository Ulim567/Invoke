using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 블록을 위아래로 쌓는 자리 하나. 함수 몸통(편집 영역)과 조건문·for·while의 블록 영역이 각각 BlockList 하나다.
/// <para>
/// 블록 데이터 목록(List&lt;BlockData&gt;)과 화면의 블록 UI 순서를 항상 같게 유지한다.<br/>
/// - 함수 몸통: FunctionData.Blocks<br/>
/// - 블록 영역: BlockData.InnerBlocks
/// </para>
/// <para>
/// 이 오브젝트의 자식은 BlockView만 둔다. 자식 순서가 곧 데이터 순서다.
/// 블록을 놓을 수 있도록 같은 오브젝트에 Image(Raycast Target 켬)를 둔다.
/// </para>
/// </summary>
public class BlockList : MonoBehaviour
{
	List<BlockData> blocks;
	BlockEditor editor;

	/// <summary>
	/// 이 자리가 들고 있는 블록 데이터 목록과 연결하고, 이미 들어 있는 블록이 있으면 화면에 만든다.
	/// </summary>
	public void Bind(List<BlockData> list, BlockEditor owner)
	{
		blocks = list;
		editor = owner;

		// 불러온 함수처럼 데이터가 이미 있으면 그대로 블록 UI를 만든다
		foreach (BlockData block in blocks)
		{
			BlockView view = editor.CreateView(block, transform);
			view.OwnerList = this;
			view.RefreshTexts();
		}
	}

	/// <summary>
	/// 화면의 블록 UI를 모두 지운다. 데이터는 건드리지 않는다. 다른 함수를 열기 전에 부른다.
	/// </summary>
	public void ClearViews()
	{
		for (int i = transform.childCount - 1; i >= 0; i--)
		{
			if (transform.GetChild(i).TryGetComponent(out BlockView view)) editor.DestroyView(view);
		}
	}

	/// <summary>
	/// 끌어서 놓은 블록을 index 자리에 넣는다. 데이터와 화면에 같은 자리로 들어간다.
	/// </summary>
	public void Insert(BlockView view, int index)
	{
		index = Mathf.Clamp(index, 0, blocks.Count);
		blocks.Insert(index, view.Data);
		view.transform.SetParent(transform, false);
		view.transform.SetSiblingIndex(index);
		view.OwnerList = this;
		view.OwnerSlot = null;
		view.RefreshTexts();
		editor.NotifyChanged();
	}

	/// <summary>
	/// block을 index 자리에 놓을 수 있으면 true.<br/>
	/// - 값 칸에만 들어가는 블록(연산, 비교, 논리)은 놓을 수 없다 (BlockData.CanBeStatement).<br/>
	/// - else if, else는 바로 위 블록이 if 또는 else if일 때만 놓을 수 있다 (function-data.md 「조건문 드롭다운」).
	/// </summary>
	public bool CanInsert(BlockData block, int index)
	{
		if (!BlockData.CanBeStatement(block.Kind)) return false;
		if (!IsElseBranch(block)) return true;
		index = Mathf.Clamp(index, 0, blocks.Count);
		if (index == 0) return false;
		BlockData above = blocks[index - 1];
		return above.Kind == BlockKind.Conditional && !IsElseBranch(above, elseOnly: true);
	}

	// else if 또는 else 블록이면 true. elseOnly면 else만 true
	static bool IsElseBranch(BlockData block, bool elseOnly = false)
	{
		if (block.Kind != BlockKind.Conditional || block.Slots.Count == 0) return false;
		string branch = block.Slots[0].Option;
		if (branch == nameof(ConditionalBranch.Else)) return true;
		return !elseOnly && branch == nameof(ConditionalBranch.ElseIf);
	}

	/// <summary>
	/// 블록을 이 자리에서 꺼낸다 (끌기 시작할 때). 데이터에서도 빠진다.
	/// </summary>
	public void Detach(BlockView view)
	{
		blocks.Remove(view.Data);
		view.OwnerList = null;
		editor.NotifyChanged();
	}

	/// <summary>
	/// 화면 좌표 screenPosition에 놓으면 몇 번째 자리에 들어가는지. 블록 가운데보다 위면 그 블록 앞에 들어간다.
	/// </summary>
	public int GetInsertIndex(Vector2 screenPosition, Camera eventCamera)
	{
		for (int i = 0; i < transform.childCount; i++)
		{
			RectTransform child = (RectTransform)transform.GetChild(i);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(child, screenPosition, eventCamera, out Vector2 local);
			if (local.y > child.rect.center.y) return i;
		}
		return transform.childCount;
	}
}
