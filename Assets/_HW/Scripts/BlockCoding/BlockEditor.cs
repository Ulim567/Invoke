using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 이어 붙이고, if·for·while 안에 넣는다.
/// <para>
/// - 편집 중인 함수는 FunctionData 하나다. 블록 UI(BlockView)가 BlockData를 직접 들고 있어서, 화면을 고치면 데이터가 바로 바뀐다.
///   (function-data.md 「편집 화면 연결」, 이우림과 확인함)<br/>
/// - 해금은 아직 보지 않고 블록을 모두 보여 준다. 스킬 트리가 생기면 해금된 것만 보여 준다.<br/>
/// - 값 칸 채우기와 함수 저장·불러오기는 「블럭코딩 화면 2」에서 만든다.
/// </para>
/// </summary>
public class BlockEditor : MonoBehaviour
{
	[SerializeField, Tooltip("블록 하나의 프리팹 (BlockView)")]
	BlockView blockPrefab;

	[SerializeField, Tooltip("블록 목록(팔레트). 견본 블록이 이 아래에 만들어진다")]
	RectTransform palette;

	[SerializeField, Tooltip("편집 영역 = 함수 몸통. BlockList가 붙어 있다")]
	BlockList workspace;

	[SerializeField, Tooltip("끌고 있는 블록을 잠시 올려 두는 곳. Canvas 맨 아래(가장 위에 그려지는) 자식으로 둔다")]
	RectTransform dragLayer;

	[SerializeField, Tooltip("레이아웃을 다시 계산할 기준. 보통 이 화면의 최상위 RectTransform")]
	RectTransform layoutRoot;

	/// <summary>지금 편집 중인 함수</summary>
	public FunctionData Function { get; private set; }

	/// <summary>끌고 있는 블록을 올려 두는 곳</summary>
	public RectTransform DragLayer => dragLayer;

	// 블록 목록에 보여 줄 견본들. 끌 때마다 새 데이터를 만들도록 함수로 둔다
	static readonly List<Func<BlockData>> Templates = new List<Func<BlockData>>
	{
		() => BlockData.Declare(new VariableType(VariableTypeKind.Int), "x"),
		() => BlockData.Assign("x", SlotData.EmptyValue()),
		() => BlockData.Conditional(ConditionalBranch.If),
		() => BlockData.Conditional(ConditionalBranch.ElseIf),
		() => BlockData.Conditional(ConditionalBranch.Else),
		() => BlockData.For("i", Int("0"), Int("10")),
		() => BlockData.While(SlotData.EmptyValue()),
		() => BlockData.Break(),
		() => BlockData.Continue(),
		() => BlockData.Free(SlotData.EmptyValue()),
		() => BlockData.Return(),
		() => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5")),
		() => BlockData.CallPredefinedFunction(PredefinedFunctionIds.Thrust),
	};

	static SlotData Int(string value) => SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), value);

	void Start()
	{
		Open(FunctionData.Create("새 함수"));

		foreach (Func<BlockData> template in Templates)
		{
			BlockView view = Instantiate(blockPrefab, palette);
			view.InitTemplate(this, template);
		}
		RefreshLayout();
	}

	/// <summary>
	/// 함수 하나를 편집 영역에 연다. 함수에 블록이 이미 있으면 그대로 화면에 만든다.
	/// </summary>
	public void Open(FunctionData function)
	{
		Function = function;
		workspace.Bind(function.Blocks, this);
	}

	/// <summary>
	/// 블록 UI를 만든다. 블록 영역 안의 블록까지 함께 만든다.
	/// </summary>
	public BlockView CreateView(BlockData data, Transform parent)
	{
		BlockView view = Instantiate(blockPrefab, parent);
		view.Init(this, data);
		return view;
	}

	/// <summary>
	/// 블록을 넣거나 뺀 뒤 크기와 위치를 다시 계산한다. 안쪽 블록이 늘면 바깥 블록도 커져야 하기 때문이다.
	/// </summary>
	public void RefreshLayout()
	{
		if (layoutRoot != null) LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
	}

	// Inspector에서 BlockEditor 컴포넌트 오른쪽 ⋮ 메뉴 → "지금 함수를 JSON으로 보기"
	// 화면에서 만든 블록이 데이터에 제대로 들어갔는지 콘솔에서 확인할 때 쓴다
	[ContextMenu("지금 함수를 JSON으로 보기")]
	void LogFunctionJson()
	{
		if (Function == null) return;
		Debug.Log($"[BlockEditor] 블록 {Function.CountBlocks()}개\n{FunctionDataJson.ToJson(Function)}");
	}
}
