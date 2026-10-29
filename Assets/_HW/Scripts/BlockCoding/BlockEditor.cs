using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 이어 붙이고, if·for·while 안에 넣고,
/// 값 칸을 채운 뒤 함수를 저장·불러온다.
/// <para>
/// - 편집 중인 함수는 FunctionData 하나다. 블록 UI(BlockView)가 BlockData를 직접 들고 있어서, 화면을 고치면 데이터가 바로 바뀐다.
///   (function-data.md 「편집 화면 연결」, 이우림과 확인함)<br/>
/// - 해금은 아직 보지 않고 블록을 모두 보여 준다. 스킬 트리가 생기면 해금된 것만 보여 준다.<br/>
/// - 저장은 지금은 이 컴퓨터의 파일에 한다 (FunctionStorage). 서버 저장은 「만든 함수를 서버로 보내 검사·저장하기」(이우림)에서 바꾼다.
/// </para>
/// <para>
/// 블록 UI의 부품(글자, 글자 칸, 값 칸, 버튼)은 모두 프리팹으로 받아 이 컴포넌트가 만든다.
/// </para>
/// </summary>
public class BlockEditor : MonoBehaviour
{
	[Header("프리팹")]
	[SerializeField, Tooltip("블록 하나 (BlockView)")]
	BlockView blockPrefab;

	[SerializeField, Tooltip("블록 안의 글자 (Text - TextMeshPro, Raycast Target 끔)")]
	TMP_Text textPrefab;

	[SerializeField, Tooltip("키보드로 적는 칸 (Input Field - TextMeshPro + Layout Element + BlockField)")]
	BlockField fieldPrefab;

	[SerializeField, Tooltip("값 칸 (Image + Horizontal Layout Group + Layout Element + BlockValueSlot)")]
	BlockValueSlot valueSlotPrefab;

	[SerializeField, Tooltip("작은 버튼 (Button - TextMeshPro). 연산자·타입 고르기, 새 함수, 저장, 불러올 함수에 쓴다")]
	Button buttonPrefab;

	[Header("화면")]
	[SerializeField, Tooltip("블록 목록(팔레트). 견본 블록이 이 아래에 만들어진다. 스크롤을 쓰면 Scroll Rect의 Content")]
	RectTransform palette;

	[SerializeField, Tooltip("편집 영역 = 함수 몸통. BlockList가 붙어 있다")]
	BlockList workspace;

	[SerializeField, Tooltip("끌고 있는 블록을 잠시 올려 두는 곳. Canvas 맨 아래(가장 위에 그려지는) 자식으로 둔다")]
	RectTransform dragLayer;

	[SerializeField, Tooltip("레이아웃을 다시 계산할 기준. 편집 영역(Workspace)을 넣는다")]
	RectTransform layoutRoot;

	[SerializeField, Tooltip("함수 이름 칸, 새 함수·저장 버튼, 블록 수, 저장한 함수 목록이 만들어지는 줄 (Horizontal Layout Group). 비워 두면 만들지 않는다")]
	RectTransform topBar;

	/// <summary>지금 편집 중인 함수</summary>
	public FunctionData Function { get; private set; }

	/// <summary>끌고 있는 블록을 올려 두는 곳</summary>
	public RectTransform DragLayer => dragLayer;

	// 블록 목록에 보여 줄 문법 블록 견본들. 끌 때마다 새 데이터를 만들도록 함수로 둔다
	static readonly List<Func<BlockData>> SyntaxTemplates = new List<Func<BlockData>>
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
		() => BlockData.Return(),
		() => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5")),
		// 값 칸에 끼우는 블록
		() => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue()),
		() => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue()),
		() => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue()),
		() => BlockData.Not(SlotData.EmptyValue()),
		() => BlockData.Free(SlotData.EmptyValue()),
	};

	// 블록 목록에 보여 줄 사전 정의 함수의 분류 (고정 ID 앞부분). test.는 빼고 보여 준다
	static readonly string[] PaletteFunctionPrefixes = { "c.", "common.", "input." };

	static SlotData Int(string value) => SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), value);

	BlockField functionNameField;
	TMP_Text infoText;
	readonly List<Button> savedButtons = new List<Button>();
	List<FunctionData> savedFunctions = new List<FunctionData>();

	void Start()
	{
		// 편집 영역부터 연다. 블록 목록을 만들다 문제가 생겨도 편집은 되도록
		BuildTopBar();
		Open(FunctionData.Create("새_함수"));
		ReloadSavedFunctions();
		BuildPalette();
	}

	/// <summary>
	/// 함수 하나를 편집 영역에 연다. 함수에 블록이 이미 있으면 (불러온 함수) 그대로 화면에 만든다.
	/// </summary>
	public void Open(FunctionData function)
	{
		workspace.ClearViews();
		Function = function;
		workspace.Bind(function.Blocks, this);
		if (functionNameField != null) functionNameField.Text = function.Name;
		NotifyChanged();
	}

	/// <summary>
	/// 지금 함수를 저장하고 저장한 함수 목록을 다시 읽는다.
	/// </summary>
	public void SaveFunction()
	{
		FunctionStorage.Save(Function);
		ReloadSavedFunctions();
		Debug.Log($"[BlockEditor] 「{Function.Name}」 저장함: {FunctionStorage.PathOf(Function)}");
	}

	// 블록 UI 부품 만들기 ---------------------------------------------------
	// 게임 중에 만드는 UI는 모두 프리팹에서 만들고, 지우는 일은 DestroyView 하나로 모은다 (CLAUDE.md 코드 작성 규칙 3, 4)

	/// <summary>
	/// 블록 UI를 만든다. 칸과 블록 영역 안의 블록까지 함께 만든다.
	/// </summary>
	public BlockView CreateView(BlockData data, Transform parent)
	{
		BlockView view = Instantiate(blockPrefab, parent);
		view.Init(this, data);
		return view;
	}

	/// <summary>
	/// 블록 UI를 지운다. 데이터는 이미 자리에서 뺀 뒤에 부른다.
	/// </summary>
	public void DestroyView(BlockView view)
	{
		Destroy(view.gameObject);
		NotifyChanged();
	}

	/// <summary>블록 안의 글자 하나를 만든다</summary>
	public TMP_Text CreateText(Transform parent) => Instantiate(textPrefab, parent);

	/// <summary>키보드로 적는 칸 하나를 만든다</summary>
	public BlockField CreateField(Transform parent) => Instantiate(fieldPrefab, parent);

	/// <summary>값 칸 하나를 만든다. 만든 뒤 Bind로 블록의 칸과 연결한다</summary>
	public BlockValueSlot CreateValueSlot(Transform parent) => Instantiate(valueSlotPrefab, parent);

	/// <summary>작은 버튼 하나를 만든다</summary>
	public Button CreateButton(Transform parent) => Instantiate(buttonPrefab, parent);

	// 규칙 ------------------------------------------------------------------

	/// <summary>
	/// block을 값 칸에 끼울 수 있으면 true. 함수 블록은 부를 함수의 반환 타입이 void가 아닐 때만 (function-data.md 「놓을 수 있는지 판단」).
	/// </summary>
	public bool CanBeValue(BlockData block)
	{
		if (!BlockData.CanBeValue(block.Kind)) return false;
		if (block.Kind != BlockKind.Function) return true;
		VariableType returnType = ReturnTypeOf(block);
		return returnType != null && returnType.Kind != VariableTypeKind.Void;
	}

	/// <summary>
	/// 함수 블록에 보여 줄 함수 이름. 사용자 정의 함수는 저장한 함수에서 이름을 찾는다.
	/// </summary>
	public string FunctionNameOf(BlockData block)
	{
		if (!block.IsUserFunction) return BlockLabels.FunctionName(block);
		if (Function != null && Function.Guid == block.FunctionId) return Function.Name;
		FunctionData found = savedFunctions.FirstOrDefault(f => f.Guid == block.FunctionId);
		return found != null ? found.Name : "없는_함수";
	}

	/// <summary>
	/// 블록 목록의 견본에 보여 줄 한 줄 글자.
	/// </summary>
	public string LabelOf(BlockData block)
	{
		if (block.Kind != BlockKind.Function) return BlockLabels.TextOf(block);
		string args = string.Join(", ", block.Slots.Select(_ => " "));
		return $"{FunctionNameOf(block)}({args});";
	}

	VariableType ReturnTypeOf(BlockData block)
	{
		if (block.IsUserFunction)
			return savedFunctions.FirstOrDefault(f => f.Guid == block.FunctionId)?.ReturnType;
		return PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction function) ? function.ReturnType : null;
	}

	// 화면 ------------------------------------------------------------------

	/// <summary>
	/// 블록을 넣거나 빼거나 칸을 고친 뒤 부른다. 레이아웃을 다시 계산하고 블록 수를 다시 쓴다.
	/// </summary>
	public void NotifyChanged()
	{
		if (layoutRoot != null) LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
		if (infoText == null || Function == null) return;

		// 코드 영역 크기: 이 함수 + 이 함수가 부르는 함수들 (function-data.md 「코드 영역 크기 표시」)
		IEnumerable<FunctionData> owned = savedFunctions.Where(f => f.Guid != Function.Guid).Append(Function);
		int codeSize = CodeSizeCalculator.Calculate(new[] { Function.Guid }, new string[0], owned);
		infoText.text = $"블록 {Function.CountBlocks()}개 / 코드 크기 {codeSize}";
	}

	void BuildPalette()
	{
		foreach (Func<BlockData> template in SyntaxTemplates) AddTemplate(template);

		// C 스킬 등 사전 정의 함수: 파라미터 수만큼 빈 값 칸을 가진 함수 블록
		IReadOnlyList<PredefinedFunction> functions = PredefinedFunctionRegistry.All;
		if (functions == null)
		{
			Debug.LogWarning("[BlockEditor] 사전 정의 함수 목록이 비어 있어서 C 스킬 블록을 만들지 못함");
			return;
		}

		foreach (PredefinedFunction function in functions)
		{
			// 함수 하나에서 문제가 나도 나머지 블록은 만든다. 어느 함수인지 콘솔에 남긴다
			try
			{
				if (function == null) continue;
				string id = function.Id;
				if (id == null || !PaletteFunctionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))) continue;
				int parameterCount = function.Parameters?.Count ?? 0;
				AddTemplate(() => BlockData.CallPredefinedFunction(id, EmptySlots(parameterCount)));
			}
			catch (Exception e)
			{
				Debug.LogError($"[BlockEditor] {function?.GetType().Name} 블록을 만들지 못함: {e}");
			}
		}
	}

	void AddTemplate(Func<BlockData> template)
	{
		BlockView view = Instantiate(blockPrefab, palette);
		view.InitTemplate(this, template);
	}

	static SlotData[] EmptySlots(int count)
	{
		var slots = new SlotData[count];
		for (int i = 0; i < count; i++) slots[i] = SlotData.EmptyValue();
		return slots;
	}

	void BuildTopBar()
	{
		if (topBar == null) return;

		functionNameField = CreateField(topBar);
		functionNameField.MinWidth = 80f;
		functionNameField.Edited += text =>
		{
			text = text.Trim();
			if (text.Length > 0) Function.Name = text;
			functionNameField.Text = Function.Name;
		};

		AddTopButton("새 함수", () => Open(FunctionData.Create("새_함수")));
		AddTopButton("저장", SaveFunction);

		infoText = CreateText(topBar);
	}

	Button AddTopButton(string label, Action onClick)
	{
		Button button = CreateButton(topBar);
		button.GetComponentInChildren<TMP_Text>().text = label;
		button.onClick.AddListener(() => onClick());
		return button;
	}

	// 저장한 함수를 다시 읽고, 위 줄 맨 뒤에 함수마다 불러오기 버튼을 만든다
	void ReloadSavedFunctions()
	{
		savedFunctions = FunctionStorage.LoadAll();
		if (topBar != null)
		{
			foreach (Button button in savedButtons) Destroy(button.gameObject);
			savedButtons.Clear();
			foreach (FunctionData saved in savedFunctions)
			{
				string guid = saved.Guid;
				// 버튼을 누를 때 파일에서 새로 읽어 연다. 저장하지 않고 고친 내용이 섞이지 않게
				savedButtons.Add(AddTopButton(saved.Name, () =>
				{
					FunctionData loaded = FunctionStorage.Load(guid);
					if (loaded != null) Open(loaded);
				}));
			}
		}
		NotifyChanged();
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
