using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블럭코딩 편집 화면. 블록 목록(팔레트)에서 블록을 끌어 편집 영역에 놓고, 이어 붙이고, if·for·while 안에 넣고,
/// 값 칸을 채운 뒤 함수를 저장·불러온다.
/// <para>
/// - 편집 중인 함수는 FunctionData 하나다. 블록 UI(BlockView)가 BlockData를 직접 들고 있어서, 화면을 고치면 데이터가 바로 바뀐다.
///   (function-data.md 「편집 화면 연결」, 이우림과 확인함)<br/>
/// - 블록 목록 해금 (#396, 이우림 #309): 실제 계정(server != null)이면 해금한 문법 블록(SkillTree.IsBlockUnlocked)과 해금한 C 스킬만 보여 준다.
///   서버가 저장 때 하는 검사(FunctionExecutorNetwork.CheckFunctionContent)와 같은 규칙이다. 스킬 트리에 노드가 없는 함수(input.* 등)는 늘 보인다.
///   임시 계정은 서버가 검사하지 않고 해금 목록도 비어 있으므로 모두 보여 준다. 편집 화면을 열 때마다 목록을 다시 만들어서, 스킬 트리에서 해금하면 다음에 열 때 보인다.<br/>
/// - 저장: 실제 계정(내 캐릭터의 FunctionExecutorNetwork.UsesServerData)이면 서버 DB(RequestSaveFunction), 아니면 이 컴퓨터의 파일(FunctionStorage). (이우림 #226, #239)<br/>
///   실제 계정은 슬롯 함수 slot1~slot8이 늘 있고(이름 고정), 페이지 함수는 ProgressRules.MaxPageFunctions개까지 더할 수 있다.
/// </para>
/// <para>
/// 블록 UI의 부품(글자, 글자 칸, 값 칸, 버튼)은 모두 프리팹으로 받아 이 컴포넌트가 만든다.
/// </para>
/// <para>
/// 화면 배치 (이우림의 「블록 견본」): 왼쪽 함수 목록(Global Variables, 슬롯 1~8, 페이지 함수), 위쪽 함수 이름·저장 상태·저장 버튼,
/// 가운데 편집 영역, 오른쪽 분류별 블록 목록. 목록 줄과 머리글은 ListItemView, ListHeaderView 프리팹으로 만든다.<br/>
/// - Global Variables를 누르면 전역 변수 선언(GlobalVariablesData.Declarations)을 같은 편집 영역에서 고친다.
///   저장: 실제 계정이면 RequestSaveGlobals, 아니면 내 캐릭터의 FunctionExecutor.TrySaveGlobalVariables (function-executor.md 「전역 변수」).
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

	[SerializeField, Tooltip("블록 목록 위의 검색 칸 (#476). 비워 두면 검색 없이 모두 보여 준다")]
	TMP_InputField paletteSearch;

	[SerializeField, Tooltip("편집 영역 = 함수 몸통. BlockList가 붙어 있다")]
	BlockList workspace;

	[SerializeField, Tooltip("끌고 있는 블록을 잠시 올려 두는 곳. Canvas 맨 아래(가장 위에 그려지는) 자식으로 둔다")]
	RectTransform dragLayer;

	[SerializeField, Tooltip("레이아웃을 다시 계산할 기준. 편집 영역의 블록 자리(Workspace/Viewport/Content)를 넣는다")]
	RectTransform layoutRoot;

	[SerializeField, Tooltip("편집 영역 스크롤의 보이는 칸 (Workspace/Viewport, #476). 블록 자리가 이보다 작아지지 않게 맞춘다. 비워 두면 맞추지 않는다")]
	RectTransform workspaceViewport;

	[SerializeField, Tooltip("블록·값 칸에 마우스를 올리면 보이는 설명창 (BlockTooltip 프리팹, #476). 비워 두면 설명창을 쓰지 않는다")]
	BlockTooltipView tooltipPrefab;

	[SerializeField, Tooltip("함수 이름 칸과 저장 버튼이 만들어지는 줄 (Horizontal Layout Group). 아래 칸들을 비워 두면 새 함수 버튼, 블록 수, 저장한 함수도 여기에 만든다")]
	RectTransform topBar;

	[Header("함수 목록·안내 (비워 두면 위쪽 줄에 버튼으로 만든다)")]
	[SerializeField, Tooltip("함수 목록 한 줄 (ListItem 프리팹)")]
	ListItemView functionItemPrefab;

	[SerializeField, Tooltip("함수 목록·블록 목록의 머리글 (ListHeader 프리팹. 「슬롯」, 「페이지 2 / 5 +」, 「제어」 등)")]
	ListHeaderView headerPrefab;

	[SerializeField, Tooltip("왼쪽 함수 목록. 줄과 머리글이 위에서부터 만들어진다 (Vertical Layout Group. 스크롤을 쓰면 Scroll Rect의 Content)")]
	RectTransform functionList;

	[SerializeField, Tooltip("저장 버튼 프리팹. 비워 두면 Button Prefab")]
	Button saveButtonPrefab;

	[SerializeField, Tooltip("컴파일 버튼 프리팹 (보라색). 비워 두면 컴파일 버튼을 만들지 않는다")]
	Button compileButtonPrefab;

	[SerializeField, Tooltip("블록 목록에서 분류 하나의 칩들을 담는 칸 (PaletteGroup 프리팹: Flow Layout Group). 비워 두면 칩을 한 줄씩 쌓는다")]
	RectTransform paletteGroupPrefab;

	[Header("드롭다운·놓을 자리")]
	[SerializeField, Tooltip("타입·연산자 드롭다운 (DropdownPopup 프리팹: 화면 전체를 덮는 투명 Button + 자식 List)")]
	RectTransform dropdownPrefab;

	[SerializeField, Tooltip("블록을 끄는 동안 들어갈 자리에 보이는 줄 (InsertMarker 프리팹)")]
	RectTransform insertMarkerPrefab;

	[Header("휴지통·오류 창")]
	[SerializeField, Tooltip("편집 영역의 블록을 끄는 동안 블록 목록 위에 보이는 「여기에 놓으면 지워요」 (처음엔 꺼 둔다)")]
	GameObject trashZone;

	[SerializeField, Tooltip("블록 목록 (오른쪽 세로 막대의 「블록」을 누르면 보임)")]
	GameObject paletteView;

	[SerializeField, Tooltip("오류 창 (오른쪽 세로 막대의 「오류」를 누르면 보임. 처음엔 꺼 둔다)")]
	GameObject errorView;

	[SerializeField, Tooltip("오류 창 안의 글자")]
	TMP_Text errorText;

	[SerializeField, Tooltip("세로 막대의 「블록」 버튼")]
	Button blockTabButton;

	[SerializeField, Tooltip("세로 막대의 「오류」 버튼")]
	Button errorTabButton;

	[SerializeField, Tooltip("「오류」 버튼 글자")]
	TMP_Text errorTabLabel;

	[SerializeField, Tooltip("「오류」 버튼의 빨간 동그라미 (오류 수). 오류가 없으면 숨긴다")]
	GameObject errorTabBadge;

	[SerializeField, Tooltip("빨간 동그라미 안의 숫자")]
	TMP_Text errorTabBadgeText;

	[SerializeField, Tooltip("오류 창의 오류 하나 (ErrorItem 프리팹)")]
	ErrorItemView errorItemPrefab;

	[SerializeField, Tooltip("오류 창에서 오류가 쌓이는 곳 (Vertical Layout Group)")]
	RectTransform errorList;

	[SerializeField, Tooltip("오류가 난 블록에 씌우는 빨간 테두리와 ! (ErrorFrame 프리팹)")]
	RectTransform errorFramePrefab;

	[SerializeField, Tooltip("컴파일 성공 알림 (편집 영역 위의 초록 알약. 처음엔 꺼 둔다)")]
	GameObject compileOkBadge;

	[Header("함수 틀 (비워 두면 편집 영역 위아래에 틀을 두지 않는다)")]
	[SerializeField, Tooltip("편집 영역 위의 「반환 타입 이름(파라미터) {」와 아래의 「}」 (#432)")]
	FunctionFrameView functionFrame;

	[Header("확인창 (비워 두면 묻지 않고 바로 연다)")]
	[SerializeField, Tooltip("저장하지 않은 변경이 있을 때 다른 함수를 열거나 나가기 전에 묻는 확인창 (#432)")]
	ConfirmDialog confirmDialog;

	[Header("참고 창 (비워 두면 참고 탭을 쓰지 않는다)")]
	[SerializeField, Tooltip("참고 창 (오른쪽 세로 막대의 「참고」를 누르면 보임. 처음엔 꺼 둔다)")]
	GameObject referenceView;

	[SerializeField, Tooltip("세로 막대의 「참고」 버튼")]
	Button referenceTabButton;

	[SerializeField, Tooltip("참고 창의 검색 칸")]
	TMP_InputField referenceSearch;

	[SerializeField, Tooltip("참고 창에서 카드가 쌓이는 곳 (Vertical Layout Group. 스크롤을 쓰면 Scroll Rect의 Content)")]
	RectTransform referenceList;

	[SerializeField, Tooltip("참고 창의 카드 하나 (ReferenceCard 프리팹)")]
	ReferenceCardView referenceCardPrefab;

	[SerializeField, Tooltip("블록 수·코드 크기 글자 (위쪽 줄 오른쪽). 비워 두면 위쪽 줄에 만든다")]
	TMP_Text infoLabel;

	[SerializeField, Tooltip("저장 상태 글자 (함수 이름 옆: 「슬롯 1 · 저장하지 않은 변경 있음」, 「저장됨」)")]
	TMP_Text messageLabel;

	[SerializeField, Tooltip("상태 글자 색: 보통")]
	Color normalColor = new Color32(0x9A, 0xA4, 0xD6, 0xFF);

	[SerializeField, Tooltip("상태 글자 색: 잘 됐을 때")]
	Color okColor = new Color32(0x8A, 0xD0, 0xA0, 0xFF);

	[SerializeField, Tooltip("상태 글자 색: 안 됐을 때")]
	Color errorColor = new Color32(0xFF, 0x8A, 0x96, 0xFF);

	/// <summary>지금 편집 중인 함수</summary>
	public FunctionData Function { get; private set; }

	/// <summary>끌고 있는 블록을 올려 두는 곳</summary>
	public RectTransform DragLayer => dragLayer;

	// 블록 목록에 보여 줄 문법 블록 견본들 (분류별). 끌 때마다 새 데이터를 만들도록 함수로 둔다.
	// 「함수」 분류는 사전 정의 함수(C 스킬 등)로 채운다 (Blocks가 null)
	static readonly (string Title, Func<BlockData>[] Blocks)[] PaletteGroups =
	{
		("제어", new Func<BlockData>[]
		{
			() => BlockData.Conditional(ConditionalBranch.If),
			() => BlockData.Conditional(ConditionalBranch.ElseIf),
			() => BlockData.Conditional(ConditionalBranch.Else),
			() => BlockData.For("i", Int("0"), Int("10")),
			() => BlockData.While(SlotData.EmptyValue()),
			() => BlockData.Break(),
			() => BlockData.Continue(),
		}),
		("변수", new Func<BlockData>[]
		{
			() => BlockData.Declare(new VariableType(VariableTypeKind.Int), "x"),
			// 대입·원소 읽기·쓰기의 변수 칸은 비워 두고, 놓은 뒤 드롭다운에서 선언한 변수를 고른다 (#474 5단계)
			() => BlockData.Assign("", SlotData.EmptyValue()),
			() => BlockData.ArrayDeclare(new VariableType(VariableTypeKind.Int), "arr", Int("3")),
			() => BlockData.ArrayGet("", SlotData.EmptyValue()),
			() => BlockData.ArraySet("", SlotData.EmptyValue(), SlotData.EmptyValue()),
		}),
		// 값 칸에 끼우는 블록
		("값", new Func<BlockData>[]
		{
			() => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue()),
			() => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue()),
			() => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue()),
			() => BlockData.Not(SlotData.EmptyValue()),
		}),
		("함수", null),
		("메모리", new Func<BlockData>[]
		{
			() => BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)),
			() => BlockData.Free(SlotData.EmptyValue()),
		}),
		("기타", new Func<BlockData>[]
		{
			() => BlockData.Return(),
			() => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5")),
		}),
	};

	// 블록 목록에 보여 줄 사전 정의 함수의 분류 (고정 ID 앞부분). test.는 빼고 보여 준다
	// game.은 게임 상태를 묻는 함수 (is_valid_target, #474)
	static readonly string[] PaletteFunctionPrefixes = { "c.", "common.", "input.", "game." };

	static SlotData Int(string value) => SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), value);

	BlockField functionNameField;
	TMP_Text infoText;
	string message = "";
	bool messageIsError;
	bool dirty; // 열고 나서 고쳤는지 (이름 뒤 *)
	bool unsavedNewPage; // 「+」로 만든 새 페이지를 아직 저장하지 않았는지. 비어 있어도 저장하지 않은 것으로 친다 (HasUnsavedChanges)
	bool savePending; // 서버에 저장을 요청하고 답을 기다리는 중인지. 답이 오기 전에는 저장했는지 알 수 없어 다른 함수를 열지 못하게 한다

	// 전역 변수 편집 중이면 true. 이때 편집 영역은 globals.Declarations를 보여 준다
	bool editingGlobals;
	GlobalVariablesData globals;
	const string GlobalsKey = "__globals__";

	// 실제 계정이면 내 캐릭터의 FunctionExecutorNetwork (함수의 원본이 서버 DB). 아니면 null이고 이 컴퓨터의 파일을 쓴다
	FunctionExecutorNetwork server;
	static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
	RectTransform openDropdown;
	RectTransform insertMarker;
	BlockValueSlot highlightedSlot;
	Button asyncButton;
	bool paletteBuilt;
	readonly List<BlockValueSlot> errorSlots = new List<BlockValueSlot>();
	readonly List<RectTransform> errorFrames = new List<RectTransform>();
	readonly List<GameObject> errorItems = new List<GameObject>();
	string compileFailedKey; // 마지막 컴파일에서 오류가 난 함수 (목록에서 빨간 밑줄)
	GlobalVariablesData listGlobals; // 함수 목록의 오류 검사에 쓰는 전역 변수
	Coroutine successRoutine;
	readonly List<GameObject> savedButtons = new List<GameObject>();
	readonly List<(ListItemView item, string prefix, string guid, string name, string detail, bool dim, bool error)> functionItems = new List<(ListItemView, string, string, string, string, bool, bool)>();
	List<FunctionData> savedFunctions = new List<FunctionData>();

	void Start()
	{
		if (trashZone != null) trashZone.SetActive(false);
		if (blockTabButton != null) blockTabButton.onClick.AddListener(() => ShowTab(RightTab.Blocks));
		if (errorTabButton != null) errorTabButton.onClick.AddListener(() => ShowTab(RightTab.Errors));
		if (referenceTabButton != null) referenceTabButton.onClick.AddListener(() => ShowTab(RightTab.Reference));
		if (referenceSearch != null) referenceSearch.onValueChanged.AddListener(_ => RebuildReference());
		if (paletteSearch != null) paletteSearch.onValueChanged.AddListener(_ => RebuildPalette());
		if (functionFrame != null && functionFrame.ReturnButton != null) functionFrame.ReturnButton.onClick.AddListener(OpenReturnTypeDropdown);
		ShowErrors(new List<FunctionChecker.Error>());
		if (compileOkBadge != null) compileOkBadge.SetActive(false);
		ShowTab(RightTab.Blocks);

		// 편집 영역부터 연다. 블록 목록을 만들다 문제가 생겨도 편집은 되도록
		BuildTopBar();
		BindServer();
		Open(FunctionData.Create("새_함수"));
		ReloadSavedFunctions();
		if (server != null) OpenFirstSlot();
		BuildPalette();
		paletteBuilt = true;
	}

	void OnDestroy() => UnbindServer();

	void OnDisable() => ClearHover(); // 편집 화면을 닫으면 설명창을 지운다

	void Update()
	{
		FitWorkspaceToViewport();
		UpdateTooltip();
	}

	// 편집 영역 스크롤 (#476) ----------------------------------------------------

	LayoutElement workspaceMinSize;
	Vector2 fittedViewportSize;

	// 블록 자리(Content)의 가장 작은 크기를 보이는 칸(Viewport) 크기로 맞춘다. 블록이 적어도 편집 영역 전체가 블록 자리여서,
	// 블록 아래 빈 곳에 놓아도 지워지지 않고 맨 끝에 들어가게 하기 위함 (예전에는 편집 영역 자체가 블록 자리였다)
	void FitWorkspaceToViewport()
	{
		if (workspaceViewport == null) return;
		if (workspaceMinSize == null && !workspace.TryGetComponent(out workspaceMinSize)) return;
		Vector2 size = workspaceViewport.rect.size;
		if (size == fittedViewportSize) return;
		fittedViewportSize = size;
		workspaceMinSize.minWidth = size.x;
		workspaceMinSize.minHeight = size.y;
	}

	// 설명창 (#476) -------------------------------------------------------------

	// 마우스가 올라가 있는 블록·값 칸들. 블록 안의 값 칸, 값 칸 안의 블록처럼 겹치므로 가장 안쪽의 설명을 보인다
	readonly List<(Component owner, RectTransform anchor, Func<string> text)> hovered = new List<(Component, RectTransform, Func<string>)>();
	float hoverSince;
	Component tooltipOwner;
	BlockTooltipView tooltip;

	// 마우스를 올리고 이만큼 멈춰 있어야 설명창을 띄운다. 블록 위를 지나가기만 할 때 깜빡이지 않게
	const float TooltipDelay = 0.4f;

	/// <summary>
	/// 블록·값 칸에 마우스가 들어왔을 때 부른다 (BlockView, BlockValueSlot). 잠깐 멈추면 text()의 글자로 설명창을 띄운다.
	/// </summary>
	public void EnterHover(Component owner, RectTransform anchor, Func<string> text)
	{
		hovered.RemoveAll(h => h.owner == owner);
		hovered.Add((owner, anchor, text));
		hoverSince = Time.unscaledTime;
	}

	/// <summary>블록·값 칸에서 마우스가 나갔을 때 부른다</summary>
	public void ExitHover(Component owner)
	{
		hovered.RemoveAll(h => h.owner == owner);
		hoverSince = Time.unscaledTime;
	}

	/// <summary>설명창을 지우고 마우스가 올라간 것을 모두 잊는다. 블록을 끌기 시작할 때, 편집 화면을 닫을 때 부른다</summary>
	public void ClearHover()
	{
		hovered.Clear();
		HideTooltip();
	}

	void UpdateTooltip()
	{
		// 지워진 블록(검색으로 블록 목록을 다시 만든 경우 등)은 뺀다
		hovered.RemoveAll(h => h.owner == null);
		if (hovered.Count == 0)
		{
			HideTooltip();
			return;
		}
		// 가장 안쪽(부모가 가장 많은) 것을 고른다. Unity가 마우스가 들어왔다고 알리는 순서는 안쪽·바깥쪽이 섞이기 때문
		(Component owner, RectTransform anchor, Func<string> text) top = hovered[0];
		int topDepth = -1;
		foreach (var entry in hovered)
		{
			int depth = 0;
			for (Transform t = entry.owner.transform; t != null; t = t.parent) depth++;
			if (depth <= topDepth) continue;
			topDepth = depth;
			top = entry;
		}
		if (top.owner == tooltipOwner) return;
		HideTooltip();
		if (Time.unscaledTime - hoverSince < TooltipDelay) return;
		ShowTooltip(top.anchor, top.text());
		tooltipOwner = top.owner;
	}

	// 설명창은 처음 한 번만 프리팹에서 만들고, 숨겼다 보였다 한다. 끄는 블록과 같은 층(DragLayer)에 두어 다른 것 위에 그린다
	void ShowTooltip(RectTransform anchor, string text)
	{
		if (tooltipPrefab == null || string.IsNullOrEmpty(text)) return;
		if (tooltip == null) tooltip = Instantiate(tooltipPrefab, dragLayer);
		tooltip.Show(text, anchor, dragLayer);
	}

	void HideTooltip()
	{
		tooltipOwner = null;
		if (tooltip != null) tooltip.Hide();
	}

	/// <summary>
	/// 블록에 마우스를 올렸을 때 보일 설명. 함수 원형, 스킬의 한국어 이름·설명, 파라미터 설명 (BlockReference.TooltipOf).
	/// </summary>
	public string TooltipOf(BlockData block) =>
		BlockReference.TooltipOf(block, PaletteNameOf(block), block.IsUserFunction ? FindUserFunction(block.FunctionId) : null);

	/// <summary>
	/// 값 칸에 마우스를 올렸을 때 보일 설명. 함수의 파라미터면 이름·타입·설명, 그리고 이 칸에 넣을 수 있는 것 (SlotRules.Describe).
	/// </summary>
	public string SlotTooltipOf(BlockData owner, int index) =>
		BlockReference.SlotTooltipOf(owner, index, SlotRules.Describe(RuleOf(owner, index)),
			owner.IsUserFunction ? FindUserFunction(owner.FunctionId) : null);

	/// <summary>
	/// 편집 화면을 열 때 부른다 (BlockEditorPanel). 내 캐릭터가 실제 계정인지 다시 보고 함수 목록을 새로 그린다.
	/// 실제 계정이고 지금 연 함수가 서버 목록에 없으면 slot1을 연다.
	/// </summary>
	public void Refresh()
	{
		if (topBar == null || functionNameField == null) return; // Start 전 (처음 여는 경우는 Start가 한다)
		BindServer();
		SetMessage("");
		savePending = false; // 편집 화면을 닫은 동안 답이 오지 못했어도 막힌 채로 남지 않게
		if (confirmDialog != null) confirmDialog.Hide();
		ReloadSavedFunctions();
		if (!editingGlobals && server != null && (Function == null || savedFunctions.All(f => f.Guid != Function.Guid))) OpenFirstSlot();
	}

	// 실제 계정이면 내 캐릭터의 FunctionExecutorNetwork에 연결한다
	void BindServer()
	{
		FunctionExecutorNetwork found = null;
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null && localPlayer.TryGetComponent(out FunctionExecutorNetwork network) && network.UsesServerData) found = network;
		if (found == server) return;

		UnbindServer();
		server = found;
		if (server == null) return;
		server.PlayerDataReceived += OnServerDataReceived;
		server.PlayerDataRequestAnswered += OnServerAnswered;
	}

	void UnbindServer()
	{
		if (server == null) return;
		server.PlayerDataReceived -= OnServerDataReceived;
		server.PlayerDataRequestAnswered -= OnServerAnswered;
		server = null;
	}

	// 서버가 처음 데이터를 보냈거나 저장이 성공해서 목록이 바뀌었을 때
	void OnServerDataReceived()
	{
		bool hadNothing = savedFunctions.Count == 0;
		ReloadSavedFunctions();
		if (hadNothing) OpenFirstSlot();
	}

	void OnServerAnswered(PlayerDataRequest request, PlayerDataRequestResult result)
	{
		if (request == PlayerDataRequest.SaveFunction || request == PlayerDataRequest.SaveGlobals) savePending = false;
		if (request == PlayerDataRequest.SaveGlobals)
		{
			bool saved = result == PlayerDataRequestResult.Saved;
			if (saved && editingGlobals) dirty = false;
			// 저장한 전역 변수로 함수 목록의 오류 표시와 변수 고르기 목록(listGlobals)을 다시 만든다 (#474)
			if (saved) ReloadSavedFunctions();
			SetMessage(saved ? "저장됨" : "전역 변수를 저장하지 못함 (int x = 3; 처럼 선언만, 칸 수 확인)", !saved);
			Redraw();
			return;
		}
		if (request != PlayerDataRequest.SaveFunction) return;
		if (result == PlayerDataRequestResult.Saved && !editingGlobals) dirty = false;
		SetMessage(result switch
		{
			PlayerDataRequestResult.Saved => "저장됨",
			PlayerDataRequestResult.TooManyFunctions => $"함수는 {ProgressRules.MaxPageFunctions}개까지 더할 수 있음",
			PlayerDataRequestResult.SlotFunctionLocked => "슬롯 함수는 이름, 파라미터를 바꿀 수 없음",
			PlayerDataRequestResult.InvalidData => "저장하지 못함 (함수 데이터 오류)",
			PlayerDataRequestResult.BlockLocked => "저장하지 못함 (아직 해금하지 않은 블록이 있음)",
			PlayerDataRequestResult.SkillLocked => "저장하지 못함 (아직 해금하지 않은 스킬이 있음)",
			PlayerDataRequestResult.CodeSizeExceeded => $"저장하지 못함 (슬롯의 코드 영역이 {ProgressRules.MaxCodeSize}을 넘음)",
			_ => $"저장하지 못함 ({result})",
		}, result != PlayerDataRequestResult.Saved);
		Redraw();
	}

	/// <summary>
	/// 저장하지 않은 변경이 있으면 true: 열고 나서 고쳤거나(이름 뒤 *), 「+」로 만든 새 페이지를 아직 저장하지 않았을 때.
	/// 편집 화면을 처음 열 때 기본으로 열리는 빈 함수는 고치기 전까지 해당하지 않는다.
	/// </summary>
	public bool HasUnsavedChanges
	{
		get
		{
			if (dirty) return true;
			return unsavedNewPage && !editingGlobals && Function != null && savedFunctions.All(f => f.Guid != Function.Guid);
		}
	}

	/// <summary>
	/// 다른 함수를 열기 전에 부른다 (함수 목록의 줄, 「+」, Global Variables). 저장하지 않은 변경이 없으면 바로 open을 하고,
	/// 있으면 확인창에서 「예」를 눌렀을 때 open을 한다. 서버 저장의 답을 기다리는 중이면 열지 않는다.
	/// </summary>
	void RequestOpen(string label, Action open, string verb = "여시겠습니까")
	{
		if (!HasUnsavedChanges || confirmDialog == null)
		{
			open();
			return;
		}
		if (savePending)
		{
			ShowNotice("저장하는 중입니다. 잠시 뒤에 다시 눌러 주세요", false);
			return;
		}
		// 문장마다 줄을 나눈다. 한 줄로 이어 쓰면 확인창 폭에서 「니까?」처럼 글자 중간이 잘려 다음 줄로 넘어가기 때문 (#476)
		confirmDialog.Show($"파일이 저장되지 않았습니다.\n{label}{Eul(label)} {verb}?", open);
	}

	/// <summary>
	/// 편집 화면을 닫기 전에 부른다 (BlockEditorPanel). 저장하지 않은 변경이 없으면 바로 close를 하고, 있으면 확인창에서 「예」를 눌렀을 때
	/// 저장하지 않은 내용을 버리고 close를 한다. 버리지 않으면 다음에 열 때 고치던 내용이 저장된 것처럼 남기 때문이다.
	/// </summary>
	public void RequestClose(Action close)
	{
		if (!HasUnsavedChanges || confirmDialog == null)
		{
			close();
			return;
		}
		if (savePending)
		{
			ShowNotice("저장하는 중입니다. 잠시 뒤에 다시 눌러 주세요", false);
			return;
		}
		confirmDialog.Show("파일이 저장되지 않았습니다.\n저장하지 않고 나가시겠습니까?", () =>
		{
			DiscardUnsaved();
			close();
		});
	}

	// 저장하지 않은 내용을 버리고 마지막으로 저장한 상태를 다시 연다. 저장한 적 없는 새 페이지는 사라지고 처음 화면으로 돌아간다
	void DiscardUnsaved()
	{
		if (editingGlobals)
		{
			OpenGlobals();
			return;
		}

		FunctionData saved = Function != null ? savedFunctions.FirstOrDefault(f => f.Guid == Function.Guid) : null;
		if (saved != null)
		{
			Open(server != null ? Copy(saved) : FunctionStorage.Load(saved.Guid) ?? saved);
			return;
		}

		if (server != null) OpenFirstSlot();
		else Open(FunctionData.Create(UniqueName("새_함수")));
		ReloadSavedFunctions(); // 사라진 새 페이지 줄을 목록에서 뺀다
	}

	// 「을」 「를」을 받침에 맞게 고른다. 한글이 아닌 글자(guard 등)로 끝나면 둘을 같이 적는다
	static string Eul(string word)
	{
		if (string.IsNullOrEmpty(word)) return "을(를)";
		char last = word[word.Length - 1];
		if (last < 0xAC00 || last > 0xD7A3) return "을(를)";
		return (last - 0xAC00) % 28 == 0 ? "를" : "을";
	}

	void OpenFirstSlot()
	{
		FunctionData slot1 = SlotFunction(0);
		if (slot1 != null) Open(Copy(slot1));
	}

	/// <summary>
	/// 함수 하나를 편집 영역에 연다. 함수에 블록이 이미 있으면 (불러온 함수) 그대로 화면에 만든다.
	/// </summary>
	public void Open(FunctionData function)
	{
		// 변수 고르기와 칸 규칙은 저장된 전역 변수를 본다. 전역 변수를 고치다 저장하지 않고 왔으면 저장된 것으로 되돌려 둔다 (#474)
		listGlobals = CurrentGlobals();
		workspace.ClearViews();
		editingGlobals = false;
		unsavedNewPage = false;
		Function = function;
		workspace.Bind(function.Blocks, this);
		if (functionNameField != null)
		{
			functionNameField.Text = function.Name;
			functionNameField.Interactable = !IsSlotFunction(function); // 슬롯 함수 이름(slot1~slot8)은 바꿀 수 없음
		}
		dirty = false;
		SetMessage("");
		ResetCompileResult();
		RemoveStaleUnsavedEntry();
		Redraw();
	}

	// 저장하지 않은 새 페이지에서 다른 곳으로 옮겼으면 그 페이지의 「저장 안 함」 줄을 목록에서 지운다.
	// 저장한 적이 없어서 다시 열 수 없는데도 줄이 남아 있으면 눌러도 아무 일이 없기 때문이다 (#432)
	void RemoveStaleUnsavedEntry()
	{
		string currentGuid = editingGlobals ? null : Function?.Guid;
		bool stale = functionItems.Any(e => e.guid != null && e.guid != GlobalsKey
			&& e.guid != currentGuid && savedFunctions.All(f => f.Guid != e.guid));
		if (stale) ReloadSavedFunctions();
	}

	// 다른 함수를 열면 지난 컴파일 결과(오류 창, 빨간 표시, 성공 알림)를 지우고 블록 목록으로 돌아간다
	void ResetCompileResult()
	{
		compileFailedKey = null;
		ClearErrorMarks();
		ShowErrors(new List<FunctionChecker.Error>());
		// 지난 오류 창만 블록 목록으로 돌린다. 참고 창을 보던 중이면 그대로 둔다
		if (rightTab == RightTab.Errors) ShowTab(RightTab.Blocks);
		if (successRoutine != null) StopCoroutine(successRoutine);
		successRoutine = null;
		if (compileOkBadge != null) compileOkBadge.SetActive(false);
	}

	/// <summary>
	/// 전역 변수 선언을 편집 영역에 연다 (함수 목록의 Global Variables). 실제 계정이면 서버가 보낸 것, 아니면 이 컴퓨터의 파일.
	/// </summary>
	public void OpenGlobals()
	{
		GlobalVariablesData source = server != null ? server.ReceivedGlobals : GlobalVariablesStorage.Load();
		globals = CopyGlobals(source);

		workspace.ClearViews();
		editingGlobals = true;
		unsavedNewPage = false;
		workspace.Bind(globals.Declarations, this);
		if (functionNameField != null)
		{
			functionNameField.Text = "Global Variables";
			functionNameField.Interactable = false;
		}
		dirty = false;
		SetMessage("");
		ResetCompileResult();
		RemoveStaleUnsavedEntry();
		Redraw();
	}

	static GlobalVariablesData CopyGlobals(GlobalVariablesData source)
	{
		if (source == null) return new GlobalVariablesData();
		return FunctionDataJson.TryGlobalVariablesFromJson(FunctionDataJson.ToJson(source), out GlobalVariablesData copy) && copy != null
			? copy
			: new GlobalVariablesData();
	}

	// 전역 변수 저장. 검사에 걸리면 저장하지 않는다 (function-executor.md 「전역 변수」 저장 검사)
	void SaveGlobals()
	{
		if (server != null)
		{
			savePending = true; // 요청이 바로 거부되면 답이 이 줄 안에서 와서 곧바로 풀린다
			server.RequestSaveGlobals(globals);
			SetMessage("저장하는 중...");
			Redraw();
			return;
		}

		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null || !localPlayer.TryGetComponent(out FunctionExecutor executor))
		{
			SetMessage("내 캐릭터의 함수 실행기를 찾지 못해 저장하지 못함", true);
			Redraw();
			return;
		}
		bool ok = executor.TrySaveGlobalVariables(globals);
		if (ok)
		{
			dirty = false;
			// 저장한 전역 변수로 함수 목록의 오류 표시와 변수 고르기 목록(listGlobals)을 다시 만든다 (#474)
			ReloadSavedFunctions();
		}
		SetMessage(ok ? "저장됨 (전역 변수를 새로 만듦)" : "전역 변수를 저장하지 못함 (int x = 3; 처럼 선언만, 칸 수 확인. 콘솔 참고)", !ok);
		Redraw();
	}

	/// <summary>
	/// 지금 함수를 저장하고 저장한 함수 목록을 다시 읽는다. 저장하기 전에 컴파일(Compile과 같은 검사)을 하고, 오류가 있으면 저장하지 않는다.
	/// 전역 변수는 컴파일 대신 저장 검사(FunctionExecutor.CanUseGlobalVariables)가 막는다.
	/// </summary>
	public void SaveFunction()
	{
		// 펼쳐 두고 비운 선언의 처음 값 칸을 접는다. 빈 처음 값은 기본값으로 실행되므로 저장한 모양을 int x;로 맞춘다 (#474)
		foreach (BlockView view in workspace.GetComponentsInChildren<BlockView>()) view.CollapseEmptyInitialValue();

		if (editingGlobals)
		{
			SaveGlobals();
			return;
		}

		// 저장하기 전에 항상 컴파일한다. 오류가 있으면 서버에도 파일에도 저장하지 않고, 오류 창과 빨간 표시를 보여 준다 (#432)
		// 저장하지 않은 내용은 편집 화면에 그대로 남으므로, 고친 뒤 다시 저장하면 된다
		int compileErrors = RunCompile();
		if (compileErrors > 0)
		{
			SetMessage($"컴파일 오류 {compileErrors}개가 있어 저장하지 않았습니다", true);
			Redraw();
			return;
		}

		if (NameTaken(Function.Name, Function.Guid))
		{
			SetMessage($"이미 「{Function.Name}」 함수가 있어서 저장하지 못했어요. 이름을 바꿔 주세요", true);
			Redraw();
			return;
		}

		// 실제 계정: 서버 DB에 저장 요청. 결과는 OnServerAnswered, 목록은 OnServerDataReceived에서 다시 그린다
		if (server != null)
		{
			bool isNewPage = savedFunctions.All(f => f.Guid != Function.Guid);
			if (isNewPage && PageFunctions().Count() >= ProgressRules.MaxPageFunctions)
			{
				SetMessage($"함수는 {ProgressRules.MaxPageFunctions}개까지 더할 수 있음", true);
				Redraw();
				return;
			}
			savePending = true; // 요청이 바로 거부되면 답이 이 줄 안에서 와서 곧바로 풀린다
			server.RequestSaveFunction(Function);
			SetMessage("저장하는 중...");
			Redraw();
			return;
		}

		FunctionStorage.Save(Function);
		dirty = false;
		SetMessage("저장됨");
		ReloadSavedFunctions();

		// 슬롯에 넣어 둔 함수를 고쳤을 수 있으므로, 내 캐릭터의 슬롯 원본을 새로 만든다 (slot-caster.md 「ReloadFunctions」)
		// ReloadFunctions가 Changed를 내면 FunctionExecutorNetwork가 서버로 함수를 다시 보낸다 (network.md 「함수 데이터 전송」)
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null && localPlayer.TryGetComponent(out SlotLoadout loadout)) loadout.ReloadFunctions();
		Debug.Log($"[BlockEditor] 「{Function.Name}」 저장함: {FunctionStorage.PathOf(Function)}");
	}

	/// <summary>
	/// 저장하기 전에 지금 함수(또는 전역 변수)를 검사한다. 결과는 위쪽 상태 글자에 보인다. 저장은 하지 않는다.
	/// <para>
	/// 검사: 반복문 밖의 break·continue(BlockData.HasJumpOutsideLoop), 값을 돌려줘야 하는데 return이 없는 길(BlockData.AlwaysReturnsValue),
	/// 비어 있는 값 칸. 전역 변수는 FunctionExecutor.CanUseGlobalVariables (function-data.md, function-executor.md).
	/// </para>
	/// </summary>
	public void Compile() => RunCompile();

	// 컴파일 버튼과 저장이 함께 쓰는 검사. 오류 수를 돌려준다 (0이면 성공)
	int RunCompile()
	{
		List<FunctionChecker.Error> errors;
		if (editingGlobals)
		{
			errors = new List<FunctionChecker.Error>();
			NetworkIdentity localPlayer = NetworkClient.localPlayer;
			bool ok = localPlayer != null && localPlayer.TryGetComponent(out FunctionExecutor executor) && executor.CanUseGlobalVariables(globals);
			if (!ok) errors.Add(new FunctionChecker.Error { Code = "Global Variables", Message = "int x = 3; 처럼 선언 블록만 넣을 수 있어요. 칸 수도 확인하세요 (자세한 이유는 콘솔)" });
		}
		else errors = FunctionChecker.Find(Function, CurrentGlobals(), savedFunctions);

		ShowErrors(errors);
		MarkErrors(errors);
		compileFailedKey = errors.Count > 0 ? OpenKey : null;
		if (errors.Count > 0) ShowTab(RightTab.Errors);
		else ShowSuccess();
		SetMessage(errors.Count == 0 ? "컴파일 성공" : $"컴파일 오류 {errors.Count}개", errors.Count > 0);
		Redraw();
		return errors.Count;
	}

	string OpenKey => editingGlobals ? GlobalsKey : Function?.Guid;

	// 컴파일 검사에 쓸 전역 변수 선언: 실제 계정은 서버가 보낸 것, 아니면 이 컴퓨터의 파일
	GlobalVariablesData CurrentGlobals() => server != null ? server.ReceivedGlobals : GlobalVariablesStorage.Load();

	// 컴파일 성공: 편집 영역 위에 초록 알림을 잠깐 띄운다
	void ShowSuccess()
	{
		if (compileOkBadge == null) return;
		if (successRoutine != null) StopCoroutine(successRoutine);
		successRoutine = StartCoroutine(SuccessRoutine());
	}

	System.Collections.IEnumerator SuccessRoutine()
	{
		compileOkBadge.SetActive(true);
		yield return new WaitForSeconds(2.5f);
		compileOkBadge.SetActive(false);
		successRoutine = null;
	}

	// 오류가 난 자리를 화면에 표시: 오류가 난 블록에 빨간 테두리와 !, 비어 있는 값 칸은 빨강
	void MarkErrors(List<FunctionChecker.Error> errors)
	{
		ClearErrorMarks();
		if (editingGlobals) return;
		foreach (BlockValueSlot slot in workspace.GetComponentsInChildren<BlockValueSlot>())
		{
			if (!slot.IsMissingValue && !slot.HasWrongValue) continue;
			slot.SetError(true);
			errorSlots.Add(slot);
		}
		if (errorFramePrefab == null) return;
		foreach (FunctionChecker.Error error in errors)
		{
			BlockView view = ViewOf(error.Block);
			if (view == null || errorFrames.Exists(f => f != null && f.parent == view.transform)) continue;
			RectTransform frame = Instantiate(errorFramePrefab, view.transform);
			frame.SetAsLastSibling();
			errorFrames.Add(frame);
		}
	}

	// 편집 영역에서 그 블록 데이터의 블록 UI
	BlockView ViewOf(BlockData block)
	{
		if (block == null) return null;
		foreach (BlockView view in workspace.GetComponentsInChildren<BlockView>())
			if (view.Data == block) return view;
		return null;
	}

	/// <summary>
	/// 값 칸 하나를 컴파일 오류처럼 빨갛게 표시한다 (예: 선언의 타입을 바꿨더니 처음 값이 맞지 않음, #474). 다음에 고치면 다른 표시와 함께 지워진다.
	/// </summary>
	public void MarkSlotError(BlockValueSlot slot)
	{
		slot.SetError(true);
		errorSlots.Add(slot);
	}

	void ClearErrorMarks()
	{
		foreach (BlockValueSlot slot in errorSlots) if (slot != null) slot.SetError(false);
		foreach (RectTransform frame in errorFrames) if (frame != null) Destroy(frame.gameObject);
		errorSlots.Clear();
		errorFrames.Clear();
	}

	// 「블록 보기」: 그 블록을 세 번 깜빡인다
	void ShowBlock(BlockData block)
	{
		BlockView view = ViewOf(block);
		if (view != null) StartCoroutine(Blink(view));
	}

	System.Collections.IEnumerator Blink(BlockView view)
	{
		CanvasGroup group = view.GetComponent<CanvasGroup>();
		for (int i = 0; i < 3 && group != null; i++)
		{
			group.alpha = 0.3f;
			yield return new WaitForSeconds(0.15f);
			group.alpha = 1f;
			yield return new WaitForSeconds(0.15f);
		}
	}

	/// <summary>
	/// 타입·연산자 드롭다운을 anchor 바로 아래에 연다 (BlockView가 부른다). 하나를 고르거나 바깥을 누르면 닫힌다.
	/// </summary>
	public void ShowDropdown(RectTransform anchor, string[] labels, int current, Action<int> pick)
	{
		CloseDropdown();
		if (dropdownPrefab == null)
		{
			// 프리팹이 없으면 예전처럼 다음 것으로 바꾼다
			pick((current + 1 + labels.Length) % labels.Length);
			return;
		}

		openDropdown = Instantiate(dropdownPrefab, dragLayer);
		if (openDropdown.TryGetComponent(out Button blocker)) blocker.onClick.AddListener(CloseDropdown);
		var list = (RectTransform)openDropdown.Find("List");
		if (list == null) list = openDropdown;
		for (int i = 0; i < labels.Length; i++)
		{
			int picked = i;
			Button item = CreateButton(list);
			TMP_Text text = item.GetComponentInChildren<TMP_Text>();
			if (text != null) text.text = labels[i];
			Transform arrow = item.transform.Find("Arrow");
			if (arrow != null) arrow.gameObject.SetActive(false);
			if (item.targetGraphic != null && i == current) item.targetGraphic.color = new Color32(0x8B, 0x9C, 0xFF, 0xFF);
			item.onClick.AddListener(() =>
			{
				CloseDropdown();
				pick(picked);
			});
		}

		var corners = new Vector3[4];
		anchor.GetWorldCorners(corners);
		list.position = corners[0]; // 칸 왼쪽 아래
	}

	/// <summary>열린 드롭다운을 닫는다</summary>
	public void CloseDropdown()
	{
		if (openDropdown != null) Destroy(openDropdown.gameObject);
		openDropdown = null;
	}

	/// <summary>
	/// 블록을 끄는 동안 놓을 자리를 보여 준다 (BlockView가 부른다): 블록 자리면 들어갈 곳에 줄, 값 칸이면 그 칸을 보라색으로.
	/// </summary>
	public void ShowDropPreview(BlockView dragged, Component target, UnityEngine.EventSystems.PointerEventData eventData)
	{
		if (highlightedSlot != null && !ReferenceEquals(target, highlightedSlot))
		{
			highlightedSlot.SetHighlight(false);
			highlightedSlot = null;
		}

		if (target is BlockValueSlot slot)
		{
			slot.SetHighlight(true);
			highlightedSlot = slot;
			SetMarker(false);
			return;
		}

		if (target is BlockList list && insertMarkerPrefab != null)
		{
			int index = list.GetInsertIndex(eventData.position, eventData.pressEventCamera);
			if (!list.CanInsert(dragged.Data, index))
			{
				SetMarker(false);
				return;
			}
			SetMarker(true);
			var corners = new Vector3[4];
			Transform t = list.transform;
			if (t.childCount == 0) ((RectTransform)t).GetWorldCorners(corners);
			else ((RectTransform)t.GetChild(Mathf.Min(index, t.childCount - 1))).GetWorldCorners(corners);
			// 맨 끝이면 마지막 블록 아래(왼쪽 아래 모서리), 아니면 그 자리 블록 위(왼쪽 위 모서리)
			insertMarker.position = t.childCount > 0 && index >= t.childCount ? corners[0] : corners[1];

			// 줄을 시작 위치에서 이 블록 자리(편집 영역 또는 블록 영역)의 오른쪽 끝까지 늘려서, 어디에 들어가는지 멀리서도 보이게 한다
			var listCorners = new Vector3[4];
			((RectTransform)t).GetWorldCorners(listCorners);
			float width = (listCorners[2].x - insertMarker.position.x) / insertMarker.lossyScale.x;
			insertMarker.sizeDelta = new Vector2(Mathf.Max(MarkerMinWidth, width), MarkerThickness);
			insertMarker.SetAsFirstSibling(); // 끄는 블록보다 아래에 그린다
			return;
		}

		SetMarker(false);
	}

	/// <summary>놓을 자리 표시를 모두 끈다</summary>
	public void HideDropPreview()
	{
		if (highlightedSlot != null) highlightedSlot.SetHighlight(false);
		highlightedSlot = null;
		SetMarker(false);
	}

	// 들어갈 자리 줄의 가장 짧은 길이와 두께 (캔버스 기준 960×540 픽셀)
	const float MarkerMinWidth = 40f;
	const float MarkerThickness = 3f;

	// 들어갈 자리 줄. 처음 쓸 때 한 번만 프리팹에서 만든다
	void SetMarker(bool show)
	{
		if (insertMarker == null)
		{
			if (!show || insertMarkerPrefab == null) return;
			insertMarker = Instantiate(insertMarkerPrefab, dragLayer);
		}
		insertMarker.gameObject.SetActive(show);
	}

	void ToggleAsync()
	{
		if (editingGlobals || Function == null || IsSlotFunction(Function)) return;
		Function.IsAsync = !Function.IsAsync;
		NotifyChanged();
	}

	/// <summary>
	/// 편집 영역의 블록을 끄는 동안 블록 목록 위에 휴지통을 보인다 (BlockView가 부른다). 그 위에 놓으면 블록이 지워진다.
	/// </summary>
	public void ShowTrash(bool show)
	{
		if (trashZone != null) trashZone.SetActive(show);
	}

	// 오른쪽 세로 막대가 고르는 창 세 가지
	enum RightTab { Blocks, Errors, Reference }

	RightTab rightTab = RightTab.Blocks;

	// 오른쪽: 블록 목록, 오류 창, 참고 창 중 하나만 보인다
	void ShowTab(RightTab tab)
	{
		rightTab = tab;
		if (paletteView != null) paletteView.SetActive(tab == RightTab.Blocks);
		if (errorView != null) errorView.SetActive(tab == RightTab.Errors);
		if (referenceView != null) referenceView.SetActive(tab == RightTab.Reference);
		SetTabLook(blockTabButton, tab == RightTab.Blocks);
		SetTabLook(errorTabButton, tab == RightTab.Errors);
		SetTabLook(referenceTabButton, tab == RightTab.Reference);
		// 참고 탭을 열 때마다 카드를 다시 만든다. 스킬 트리 설명이나 해금 상태가 바뀌어도 다음에 열 때 맞춰진다
		if (tab == RightTab.Reference) RebuildReference();
	}

	// 참고 창: 검색 칸의 글자에 맞는 카드를 처음부터 다시 만든다 (BlockReference.Search)
	void RebuildReference()
	{
		if (referenceList == null || referenceCardPrefab == null) return;
		for (int i = referenceList.childCount - 1; i >= 0; i--) Destroy(referenceList.GetChild(i).gameObject);
		string query = referenceSearch != null ? referenceSearch.text : "";
		List<BlockReferenceEntry> found = BlockReference.Search(query);

		// 블록 목록과 같은 분류 순서로, 분류마다 머리글을 하나 넣고 그 아래에 카드를 놓는다 (#476). 카드가 없는 분류는 머리글도 없다
		// 블록 목록에 없는 분류가 생겨도 카드가 빠지지 않게 그 분류는 맨 뒤에 둔다
		IEnumerable<string> categories = PaletteGroups.Select(group => group.Title).Concat(found.Select(entry => entry.Category)).Distinct();
		foreach (string category in categories)
		{
			List<BlockReferenceEntry> cards = found.Where(entry => entry.Category == category).ToList();
			if (cards.Count == 0) continue;
			if (headerPrefab != null)
			{
				// 분류 색 네모: 그 분류 블록의 색 (블록 목록의 머리글과 같다)
				Color icon = cards[0].Sample != null ? BlockLabels.ColorOf(cards[0].Sample) : new Color(0, 0, 0, 0);
				Instantiate(headerPrefab, referenceList).Show(category, null, icon);
			}
			foreach (BlockReferenceEntry entry in cards) Instantiate(referenceCardPrefab, referenceList).Show(entry);
		}
	}

	static void SetTabLook(Button tab, bool active)
	{
		if (tab == null) return;
		if (tab.targetGraphic != null) tab.targetGraphic.color = active ? Color.white : new Color(1, 1, 1, 0.35f);
	}

	void ShowErrors(List<FunctionChecker.Error> errors)
	{
		foreach (GameObject item in errorItems) if (item != null) Destroy(item);
		errorItems.Clear();

		bool useItems = errorItemPrefab != null && errorList != null;
		if (errorText != null)
		{
			errorText.gameObject.SetActive(errors.Count == 0 || !useItems);
			errorText.text = errors.Count == 0 ? "오류 없음\n\n컴파일을 누르면 여기에 결과가 나와요."
				: "- " + string.Join("\n\n- ", errors.ConvertAll(e => $"{e.Code}  {e.Message}"));
		}
		if (useItems)
		{
			foreach (FunctionChecker.Error error in errors)
			{
				ErrorItemView item = Instantiate(errorItemPrefab, errorList);
				BlockData block = error.Block;
				item.Show(error.Code, error.Message, block != null ? () => ShowBlock(block) : null);
				errorItems.Add(item.gameObject);
			}
		}

		if (errorTabLabel != null) errorTabLabel.text = "오\n류";
		if (errorTabBadge != null) errorTabBadge.SetActive(errors.Count > 0);
		if (errorTabBadgeText != null) errorTabBadgeText.text = errors.Count.ToString();
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
	/// owner 블록의 index번 값 칸이 받을 수 있는 것 (SlotRules, #474). 값 칸(BlockValueSlot)이 끼우기·적기를 판단할 때 부른다.
	/// </summary>
	public SlotRules.Rule RuleOf(BlockData owner, int index) =>
		SlotRules.RuleOf(owner, index, editingGlobals ? null : Function?.ReturnType, editingGlobals, FindUserFunction, VariableTypeLookup(owner));

	/// <summary>
	/// owner 블록의 index번 값 칸에 content(적은 값, 변수, 끼운 블록)를 넣을 수 있으면 true (칸 규칙 + 그 자리에서 보이는 변수의 타입).
	/// </summary>
	public bool Fits(BlockData owner, int index, SlotData content) =>
		SlotRules.Fits(RuleOf(owner, index), content, FindUserFunction, VariableTypeLookup(owner));

	/// <summary>
	/// owner 블록의 index번 값 칸에 값 블록 block을 끼울 수 있으면 true.
	/// </summary>
	public bool FitsBlock(BlockData owner, int index, BlockData block) =>
		SlotRules.FitsBlock(RuleOf(owner, index), block, FindUserFunction, VariableTypeLookup(owner));

	/// <summary>
	/// owner 블록 자리에서 쓸 수 있는 변수 (이름 → 타입). C처럼 위에서 선언했고 그 자리에서 보이는 것만 (VariableScopes, #474 5단계).
	/// 함수에 들어 있지 않은 블록(끄는 중인 블록 등)이면 비어 있다.
	/// </summary>
	public Dictionary<string, VariableType> VisibleVariables(BlockData owner)
	{
		Dictionary<string, VariableType> found;
		if (editingGlobals) found = VariableScopes.VisibleAt(globals?.Declarations, new VariableScopes.Stack(), owner);
		else found = VariableScopes.VisibleAt(Function?.Blocks, VariableScopes.Start(Function, listGlobals), owner);
		return found ?? new Dictionary<string, VariableType>();
	}

	// owner 자리에서 변수 이름으로 타입을 찾는 함수. 칸 규칙(SlotRules)에 넘긴다. 못 찾으면 null(막지 않음)
	Func<string, VariableType> VariableTypeLookup(BlockData owner)
	{
		Dictionary<string, VariableType> visible = null;
		return name =>
		{
			if (string.IsNullOrEmpty(name)) return null;
			visible ??= VisibleVariables(owner); // 필요할 때 한 번만 찾는다
			return visible.TryGetValue(name, out VariableType type) ? type : null;
		};
	}

	/// <summary>
	/// GUID로 사용자 정의 함수를 찾는다. 편집 중인 함수는 지금 내용으로. 없으면 null
	/// </summary>
	public FunctionData FindUserFunction(string guid)
	{
		if (Function != null && Function.Guid == guid) return Function;
		return savedFunctions.FirstOrDefault(f => f.Guid == guid);
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
		dirty = true;
		ClearErrorMarks(); // 고쳤으면 지난 컴파일의 빨간 표시를 지운다
		SetMessage(""); // 「저장됨」을 지우고 「저장하지 않은 변경 있음」을 보인다
		Redraw();
	}

	/// <summary>
	/// 데이터는 그대로이고 블록 모양만 바뀌었을 때 레이아웃만 다시 계산한다 (예: 선언의 처음 값 칸 펼치기). 고친 것으로 치지 않는다.
	/// </summary>
	public void RefreshLayout()
	{
		if (layoutRoot != null) LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
	}

	// 화면 글자만 다시 그린다 (고친 것으로 치지 않음)
	void Redraw()
	{
		if (layoutRoot != null) LayoutRebuilder.MarkLayoutForRebuild(layoutRoot);
		if (Function == null && !editingGlobals) return;

		// 빈 값 칸의 안내 글자 (#476). 선언의 타입이나 대입의 변수를 바꾸면 다른 칸이 받는 타입도 바뀌므로 고칠 때마다 다시 쓴다
		foreach (BlockValueSlot slot in workspace.GetComponentsInChildren<BlockValueSlot>()) slot.RefreshPlaceholder();

		// 동기·비동기 버튼 (슬롯 함수와 전역 변수는 바꿀 수 없음)
		if (asyncButton != null)
		{
			TMP_Text asyncText = asyncButton.GetComponentInChildren<TMP_Text>();
			if (asyncText != null) asyncText.text = !editingGlobals && Function.IsAsync ? "비동기" : "동기";
			asyncButton.interactable = !editingGlobals && !IsSlotFunction(Function);
		}

		if (functionFrame != null) UpdateFunctionFrame();

		// 함수 목록: 지금 연 것을 고른 모양으로, 고쳤으면 이름 뒤 *
		string openKey = editingGlobals ? GlobalsKey : Function.Guid;
		foreach (var entry in functionItems)
		{
			if (entry.item == null) continue;
			bool open = entry.guid != null && entry.guid == openKey;
			// 저장한 함수에 컴파일 오류가 있으면 이름에 빨간 밑줄 (견본)
			bool error = entry.error || (entry.guid != null && entry.guid == compileFailedKey);
			entry.item.Show(entry.prefix, entry.name + (open && HasUnsavedChanges ?" <color=#8B9CFF>*</color>" : ""), entry.detail, open, entry.dim, error);
		}

		if (messageLabel != null)
		{
			string text = message;
			bool error = messageIsError;
			if (string.IsNullOrEmpty(text)) text = StatusText();
			messageLabel.text = text;
			messageLabel.color = string.IsNullOrEmpty(message) ? normalColor : error ? errorColor : okColor;
		}

		if (infoText == null) return;
		string info;
		if (editingGlobals) info = $"전역 변수 {globals.Declarations.Count}개   데이터 영역 {globals.Declarations.Count}칸";
		else info = $"블록 {Function.CountBlocks()}개   코드 크기 {CodeSizeOf(Function)}";
		// 상태 글자 칸이 없으면 예전처럼 뒤에 붙인다
		if (messageLabel == null && !string.IsNullOrEmpty(message)) info += $"  {message}";
		infoText.text = info;
	}

	// 고를 수 있는 반환 타입 (페이지 함수). 슬롯 함수는 서버가 void로 고정한다
	static readonly VariableTypeKind[] ReturnKinds =
	{
		VariableTypeKind.Void, VariableTypeKind.Int, VariableTypeKind.Float, VariableTypeKind.Bool, VariableTypeKind.Char, VariableTypeKind.String,
	};

	static string ReturnTypeName(VariableType type) =>
		type == null || type.Kind == VariableTypeKind.Void ? "void" : BlockLabels.TypeName(type);

	// 편집 영역 위아래의 함수 틀을 지금 함수에 맞게 쓴다. 파라미터는 아직 편집할 수 없어서 있으면 보여 주기만 한다
	void UpdateFunctionFrame()
	{
		if (editingGlobals || Function == null)
		{
			functionFrame.ShowGlobals();
			return;
		}

		bool slot = IsSlotFunction(Function);
		string parameters = string.Join(", ", (Function.Parameters ?? new List<ParameterData>()).Select(p => $"{BlockLabels.TypeName(p.Type)} {p.Name}"));
		functionFrame.ShowFunction(ReturnTypeName(Function.ReturnType), Function.Name, parameters,
			slot ? "슬롯 함수 · 이름과 반환 타입은 고정" : "페이지 함수 · 반환 타입을 고를 수 있음", !slot);
	}

	// 반환 타입을 누르면 드롭다운으로 고른다. 고르면 고친 것으로 치고, 틀린 곳(return이 없는 길 등)은 저장할 때 컴파일이 알려 준다
	void OpenReturnTypeDropdown()
	{
		if (editingGlobals || Function == null || IsSlotFunction(Function)) return;
		string[] labels = ReturnKinds.Select(kind => ReturnTypeName(new VariableType(kind))).ToArray();
		int current = Array.IndexOf(ReturnKinds, Function.ReturnType?.Kind ?? VariableTypeKind.Void);
		ShowDropdown((RectTransform)functionFrame.ReturnButton.transform, labels, current, picked =>
		{
			Function.ReturnType = new VariableType(ReturnKinds[picked]);
			NotifyChanged();
		});
	}

	// 안내가 없을 때의 상태 글자: 「슬롯 1 · 저장하지 않은 변경 있음」
	string StatusText()
	{
		string where;
		if (editingGlobals) where = "전역 변수";
		else
		{
			int slot = SlotIndexOf(Function);
			bool saved = savedFunctions.Any(f => f.Guid == Function.Guid);
			where = slot >= 0 ? $"슬롯 {slot + 1}" : saved ? "페이지 함수" : "새 함수";
		}
		return HasUnsavedChanges ? $"{where}, 저장하지 않은 변경 있음" : where;
	}

	int SlotIndexOf(FunctionData function)
	{
		IReadOnlyList<SlotLoadout.Entry> slots = server?.ReceivedSlots;
		if (slots == null || function == null) return -1;
		for (int i = 0; i < slots.Count; i++)
			if (slots[i].Id == function.Guid) return i;
		return -1;
	}

	// 함수 하나의 코드 영역 크기 (그 함수 + 그 함수가 부르는 함수들). 편집 중인 함수는 지금 내용으로 센다
	int CodeSizeOf(FunctionData function)
	{
		IEnumerable<FunctionData> owned = Function == null ? savedFunctions : savedFunctions.Where(f => f.Guid != Function.Guid).Append(Function);
		return CodeSizeCalculator.Calculate(new[] { function.Guid }, new string[0], owned);
	}

	/// <summary>
	/// 위쪽 줄의 상태 글자에 안내를 보인다. 칸에 잘못 적었을 때처럼 데이터를 바꾸지 않고 알리기만 할 때 쓴다 (BlockView가 부른다).
	/// 고친 것으로 치지 않으므로 「저장하지 않은 변경」 표시는 바뀌지 않는다.
	/// </summary>
	public void ShowNotice(string text, bool error = true)
	{
		SetMessage(text, error);
		Redraw();
	}

	void SetMessage(string text, bool error = false)
	{
		message = text;
		messageIsError = error;
	}

	// 블록 목록에서 쓸 해금 목록. null이면 걸러 내지 않고 모두 보여 준다 (임시 계정)
	HashSet<string> unlockedForPalette;

	void BuildPalette()
	{
		unlockedForPalette = null;
		if (server != null && NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PlayerProgress progress))
			unlockedForPalette = new HashSet<string>(progress.Unlocked);

		string query = paletteSearch != null ? paletteSearch.text.Trim() : "";

		foreach ((string title, Func<BlockData>[] blocks) in PaletteGroups)
		{
			// 함수 분류: 함수 블록 자체를 해금하지 않았으면 분류째 뺀다
			if (blocks == null && unlockedForPalette != null && !SkillTree.IsBlockUnlocked(BlockKind.Function, unlockedForPalette)) continue;

			// 해금한 블록 중 검색 글자에 맞는 것만 남긴다 (#476). 남은 블록이 없는 분류는 머리글도 만들지 않는다 (머리글 색을 첫 블록으로 정하므로)
			IEnumerable<Func<BlockData>> candidates = (IEnumerable<Func<BlockData>>)blocks ?? FunctionTemplates();
			if (blocks != null && unlockedForPalette != null)
				candidates = candidates.Where(template => SkillTree.IsBlockUnlocked(template().Kind, unlockedForPalette));
			Func<BlockData>[] shown = candidates.Where(template => MatchesPaletteSearch(template(), title, query)).ToArray();
			if (shown.Length == 0) continue;

			if (headerPrefab != null)
			{
				ListHeaderView header = Instantiate(headerPrefab, palette);
				// 분류 색 네모: 그 분류 블록의 색
				header.Show(title, null, BlockLabels.ColorOf(shown[0]()));
			}
			// 분류마다 칩을 줄줄이 놓을 칸 (줄이 차면 다음 줄로)
			paletteTarget = paletteGroupPrefab != null ? Instantiate(paletteGroupPrefab, palette) : palette;
			foreach (Func<BlockData> template in shown) AddTemplate(template);
		}
		paletteTarget = palette;
	}

	// 블록 목록 검색 (#476): 비어 있거나, 분류 이름·칩 이름·스킬의 화면 이름(검 찌르기)·고정 ID(c.thrust)에 검색 글자가 들어 있으면 true (대소문자 구분 없음)
	// 분류 이름이 맞으면 그 분류의 블록을 모두 보여 준다. 예: 「제어」 → If, For, While …
	bool MatchesPaletteSearch(BlockData block, string groupTitle, string query)
	{
		if (query.Length == 0 || ContainsText(groupTitle, query) || ContainsText(PaletteNameOf(block), query)) return true;
		if (block.Kind != BlockKind.Function || block.IsUserFunction) return false;
		return PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction function)
			&& (ContainsText(function.Name, query) || ContainsText(function.Id, query));
	}

	static bool ContainsText(string text, string query) =>
		text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

	RectTransform paletteTarget; // 견본 칩을 만들 곳 (BuildPalette 중의 분류 칸)

	/// <summary>
	/// 블록 목록 칩에 보일 짧은 이름. 예: If, Else If, For, Declare, Arithmetic, thrust
	/// </summary>
	public string PaletteNameOf(BlockData block)
	{
		switch (block.Kind)
		{
			case BlockKind.Conditional:
				string branch = block.Slots[0].Option;
				return branch == nameof(ConditionalBranch.ElseIf) ? "Else If" : branch == nameof(ConditionalBranch.Else) ? "Else" : "If";
			case BlockKind.Function: return FunctionNameOf(block);
			case BlockKind.ArrayDeclare: return "Array";
			case BlockKind.ArrayGet: return "Array Get";
			case BlockKind.ArraySet: return "Array Set";
			default: return block.Kind.ToString();
		}
	}

	// 「함수」 분류의 견본들: C 스킬 등 사전 정의 함수와 내가 만든 함수 부르기. 둘 다 파라미터 수만큼 빈 값 칸을 가진 함수 블록
	// 칩을 바로 만들지 않고 목록으로 돌려준다. 검색에 맞는 블록이 없으면 분류 머리글도 만들지 않기 위함 (#476)
	List<Func<BlockData>> FunctionTemplates()
	{
		var templates = new List<Func<BlockData>>();
		IReadOnlyList<PredefinedFunction> functions = PredefinedFunctionRegistry.All;
		if (functions == null) Debug.LogWarning("[BlockEditor] 사전 정의 함수 목록이 비어 있어서 C 스킬 블록을 만들지 못함");
		else
		{
			foreach (PredefinedFunction function in functions)
			{
				// 함수 하나에서 문제가 나도 나머지 블록은 만든다. 어느 함수인지 콘솔에 남긴다
				try
				{
					if (function == null) continue;
					string id = function.Id;
					if (id == null || !PaletteFunctionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))) continue;
					// 스킬 트리에 노드가 있는 스킬은 해금한 것만 (노드가 없는 input.* 등은 늘 보임)
					if (unlockedForPalette != null && SkillTree.TryGet(id, out _) && !unlockedForPalette.Contains(id)) continue;
					int parameterCount = function.Parameters?.Count ?? 0;
					templates.Add(() => BlockData.CallPredefinedFunction(id, EmptySlots(parameterCount)));
				}
				catch (Exception e)
				{
					Debug.LogError($"[BlockEditor] {function?.GetType().Name} 블록을 만들지 못함: {e}");
				}
			}
		}

		// 내가 만든 함수 부르기 (BlockData.CallUserFunction)
		// 슬롯 함수는 슬롯 키로 시전하는 함수라 다른 함수 안에서 부르지 않으므로 페이지 함수만 붙인다
		foreach (FunctionData saved in PageFunctions())
		{
			string guid = saved.Guid;
			int parameterCount = saved.Parameters?.Count ?? 0;
			templates.Add(() => BlockData.CallUserFunction(guid, EmptySlots(parameterCount)));
		}
		return templates;
	}

	// 저장한 함수가 바뀌면 블록 목록의 「함수」 분류도 바뀌므로 다시 만든다
	void RebuildPalette()
	{
		if (!paletteBuilt || palette == null) return;
		for (int i = palette.childCount - 1; i >= 0; i--) Destroy(palette.GetChild(i).gameObject);
		BuildPalette();
	}

	void AddTemplate(Func<BlockData> template)
	{
		BlockView view = Instantiate(blockPrefab, paletteTarget != null ? paletteTarget : palette);
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
		functionNameField.MinWidth = 60f;
		functionNameField.SetBackground(null, Color.clear); // 견본처럼 이름만 보이게
		functionNameField.transform.SetSiblingIndex(0); // 상태 글자(Message Label)가 위쪽 줄 안에 있으면 그 앞에

		// 동기·비동기 고르기 (FunctionData.IsAsync, function-data.md #132). 함수 이름 바로 뒤
		asyncButton = AddTopButton("동기", ToggleAsync, saveButtonPrefab);
		asyncButton.transform.SetSiblingIndex(1);
		// 일단 항상 숨긴다 (#470). 버튼과 ToggleAsync는 남겨 두어, 다시 보일 때 이 줄만 지우면 된다
		asyncButton.gameObject.SetActive(false);
		functionNameField.Edited += text =>
		{
			text = text.Trim();
			if (editingGlobals) return;
			if (text.Length > 0 && !IsSlotFunction(Function) && text != Function.Name)
			{
				// 같은 이름의 함수는 만들 수 없다 (함수 부르기 블록이 이름으로 보이므로 헷갈림)
				if (!BlockSlotText.IsFunctionName(text)) SetMessage("함수 이름에는 글자(한글 포함), 숫자, 밑줄만 쓸 수 있고 숫자로 시작할 수 없어요", true);
				else if (NameTaken(text, Function.Guid)) SetMessage($"이미 「{text}」 함수가 있어요. 다른 이름을 적어 주세요", true);
				else
				{
					Function.Name = text;
					dirty = true;
				}
			}
			functionNameField.Text = Function.Name;
			Redraw();
		};

		if (functionList == null || functionItemPrefab == null) AddTopButton("새 함수", NewFunction); // 함수 목록이 없으면 위쪽 줄에
		if (compileButtonPrefab != null) AddTopButton("컴파일", Compile, compileButtonPrefab);
		AddTopButton("저장", SaveFunction, saveButtonPrefab);

		infoText = infoLabel != null ? infoLabel : CreateText(topBar);
	}

	Button AddTopButton(string label, Action onClick, Button prefab = null)
	{
		Button button = prefab != null ? Instantiate(prefab, topBar) : CreateButton(topBar);
		button.GetComponentInChildren<TMP_Text>().text = label;
		button.onClick.AddListener(() => onClick());
		return button;
	}

	bool HasFunctionList => functionList != null && functionItemPrefab != null;

	// 함수 목록에 한 줄을 더한다. 함수 목록 칸이 있으면 그곳에, 없으면 위쪽 줄에 버튼으로 (onClick이 null이면 누를 수 없는 줄)
	void AddFunctionEntry(string prefix, string guid, string name, string detail, Action onClick, bool dim = false, bool error = false)
	{
		if (HasFunctionList)
		{
			ListItemView item = Instantiate(functionItemPrefab, functionList);
			item.SetBoxed(guid == GlobalsKey); // Global Variables는 상자로
			if (onClick != null) item.Button.onClick.AddListener(() => onClick());
			item.Button.interactable = onClick != null;
			savedButtons.Add(item.gameObject);
			functionItems.Add((item, prefix, guid, name, detail, dim, error));
			return;
		}
		if (topBar != null && onClick != null) savedButtons.Add(AddTopButton(name, onClick).gameObject);
	}

	// 함수 목록의 머리글 (「슬롯」, 「페이지 2 / 5 +」). onAdd가 있으면 + 버튼을 보인다
	void AddFunctionHeader(string title, Action onAdd)
	{
		if (!HasFunctionList || headerPrefab == null) return;
		ListHeaderView header = Instantiate(headerPrefab, functionList);
		header.Show(title, onAdd);
		savedButtons.Add(header.gameObject);
	}

	// 저장한 함수를 다시 읽고, 위 줄 맨 뒤에 함수마다 불러오기 버튼을 만든다
	void ReloadSavedFunctions()
	{
		if (server != null)
		{
			ReloadServerFunctions();
			return;
		}

		savedFunctions = FunctionStorage.LoadAll();
		listGlobals = CurrentGlobals();
		ClearSavedButtons();
		AddFunctionEntry("G", GlobalsKey, "Global Variables", "", () => RequestOpen("Global Variables", OpenGlobals));

		// 슬롯 1~8: 내 캐릭터의 장착(SlotLoadout.GetEntry). 함수면 눌러서 연다, C 스킬은 이름만
		var equipped = new HashSet<string>();
		AddFunctionHeader("슬롯", null);
		SlotLoadout loadout = null;
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null) localPlayer.TryGetComponent(out loadout);
		for (int slot = 0; slot < SlotLoadout.SlotCount; slot++)
		{
			string number = (slot + 1).ToString();
			string key = slot < SlotKeys.Length ? SlotKeys[slot] : "";
			SlotLoadout.Entry entry = loadout != null ? loadout.GetEntry(slot) : default;
			if (loadout == null || entry.IsEmpty)
			{
				AddFunctionEntry(number, null, "비어 있음", key, null, true);
				continue;
			}
			if (entry.Kind == SlotLoadout.EntryKind.Skill)
			{
				string skillName = SkillTable.TryGet(entry.Id, out SkillData skill) ? skill.DisplayName : entry.Id;
				AddFunctionEntry(number, null, skillName, $"{key} 스킬", null, true);
				continue;
			}
			FunctionData slotFunction = savedFunctions.FirstOrDefault(f => f.Guid == entry.Id);
			if (slotFunction == null)
			{
				AddFunctionEntry(number, null, "(지워진 함수)", key, null, true);
				continue;
			}
			equipped.Add(slotFunction.Guid);
			string slotGuid = slotFunction.Guid;
			string slotLabel = $"슬롯 {number} {slotFunction.Name}";
			AddFunctionEntry(number, slotGuid, slotFunction.Name, key, () => RequestOpen(slotLabel, () =>
			{
				FunctionData loaded = FunctionStorage.Load(slotGuid);
				if (loaded != null) Open(loaded);
			}), false, FunctionChecker.HasErrors(slotFunction, listGlobals, savedFunctions));
		}

		// 페이지: 슬롯에 넣지 않은 저장한 함수. + 로 새 함수
		List<FunctionData> pages = savedFunctions.Where(f => !equipped.Contains(f.Guid)).ToList();
		AddFunctionHeader($"페이지 {pages.Count}", NewFunction);
		AddUnsavedEntry();
		foreach (FunctionData saved in pages)
		{
			string guid = saved.Guid;
			// 누를 때 파일에서 새로 읽어 연다. 저장하지 않고 고친 내용이 섞이지 않게
			string pageLabel = $"페이지 {saved.Name}";
			AddFunctionEntry("-", guid, saved.Name, $"코드 {CodeSizeOf(saved)}", () => RequestOpen(pageLabel, () =>
			{
				FunctionData loaded = FunctionStorage.Load(guid);
				if (loaded != null) Open(loaded);
			}), false, FunctionChecker.HasErrors(saved, listGlobals, savedFunctions));
		}
		RebuildPalette();
		Redraw();
	}

	// 실제 계정: 위 줄에 슬롯 함수 slot1~slot8, 그 뒤에 페이지 함수 버튼
	void ReloadServerFunctions()
	{
		savedFunctions = server.ReceivedFunctions?.ToList() ?? new List<FunctionData>();
		listGlobals = CurrentGlobals();
		ClearSavedButtons();
		AddFunctionEntry("G", GlobalsKey, "Global Variables", "", () => RequestOpen("Global Variables", OpenGlobals));
		AddFunctionHeader("슬롯", null);
		for (int slot = 0; slot < SlotLoadout.SlotCount; slot++)
		{
			FunctionData function = SlotFunction(slot);
			string key = slot < SlotKeys.Length ? SlotKeys[slot] : "";
			if (function == null)
			{
				AddFunctionEntry((slot + 1).ToString(), null, "비어 있음", key, null, true);
				continue;
			}
			string slotLabel = $"슬롯 {slot + 1} {function.Name}"; // 반복 변수 slot은 누를 때 이미 바뀌어 있으므로 지금 글자로 만들어 둔다
			AddFunctionEntry((slot + 1).ToString(), function.Guid, function.Name, key, () => RequestOpen(slotLabel, () => Open(Copy(function))), false, FunctionChecker.HasErrors(function, listGlobals, savedFunctions));
		}
		AddFunctionHeader($"페이지 {PageFunctions().Count()} / {ProgressRules.MaxPageFunctions}", NewFunction);
		AddUnsavedEntry();
		foreach (FunctionData function in PageFunctions())
		{
			FunctionData page = function;
			string pageLabel = $"페이지 {page.Name}";
			AddFunctionEntry("-", page.Guid, page.Name, $"코드 {CodeSizeOf(page)}", () => RequestOpen(pageLabel, () => Open(Copy(page))), false, FunctionChecker.HasErrors(page, listGlobals, savedFunctions));
		}
		RebuildPalette();
		Redraw();
	}

	// 다른 함수(GUID가 다른)가 이미 이 이름을 쓰면 true
	bool NameTaken(string name, string guid) => savedFunctions.Any(f => f.Guid != guid && f.Name == name);

	// 겹치지 않는 이름: 새_함수, 새_함수2, 새_함수3 …
	string UniqueName(string baseName)
	{
		if (!NameTaken(baseName, null)) return baseName;
		for (int n = 2; ; n++)
			if (!NameTaken(baseName + n, null)) return baseName + n;
	}

	// 아직 저장하지 않은 새 함수도 페이지 맨 위에 보인다 (지금 연 함수)
	void AddUnsavedEntry()
	{
		if (editingGlobals || Function == null || savedFunctions.Any(f => f.Guid == Function.Guid)) return;
		AddFunctionEntry("-", Function.Guid, Function.Name, "저장 안 함", () => { });
	}

	void ClearSavedButtons()
	{
		foreach (GameObject button in savedButtons)
		{
			if (button != null) Destroy(button);
		}
		savedButtons.Clear();
		functionItems.Clear();
	}

	void NewFunction()
	{
		if (server != null && PageFunctions().Count() >= ProgressRules.MaxPageFunctions)
		{
			SetMessage($"함수는 {ProgressRules.MaxPageFunctions}개까지 더할 수 있음", true);
			Redraw();
			return;
		}
		RequestOpen("새 페이지", CreateNewPage, "만드시겠습니까");
	}

	// 새 페이지를 연다. 저장하기 전에도 저장하지 않은 것으로 쳐서, 다른 곳을 열거나 나가려 하면 먼저 묻는다 (사라지기 때문)
	void CreateNewPage()
	{
		SetMessage("");
		Open(FunctionData.Create(UniqueName("새_함수")));
		unsavedNewPage = true;
		ReloadSavedFunctions(); // 새 함수를 목록(페이지)에 보인다
	}

	// 슬롯 번호의 슬롯 함수 (실제 계정). ReceivedSlots[slot].Id가 그 함수의 GUID
	FunctionData SlotFunction(int slot)
	{
		IReadOnlyList<SlotLoadout.Entry> slots = server?.ReceivedSlots;
		if (slots == null || slot >= slots.Count) return null;
		string guid = slots[slot].Id;
		return savedFunctions.FirstOrDefault(f => f.Guid == guid);
	}

	// 슬롯 함수가 아닌 함수들 (실제 계정)
	IEnumerable<FunctionData> PageFunctions()
	{
		IReadOnlyList<SlotLoadout.Entry> slots = server?.ReceivedSlots;
		var slotGuids = new HashSet<string>(slots?.Select(e => e.Id) ?? Enumerable.Empty<string>());
		return savedFunctions.Where(f => !slotGuids.Contains(f.Guid));
	}

	bool IsSlotFunction(FunctionData function)
	{
		if (server == null || function == null || server.ReceivedSlots == null) return false;
		return server.ReceivedSlots.Any(e => e.Id == function.Guid);
	}

	// 서버가 보낸 함수를 그대로 고치면 저장하지 않은 내용이 목록에 섞이므로, 열 때마다 복사본을 연다
	static FunctionData Copy(FunctionData function) =>
		FunctionDataJson.TryFromJson(FunctionDataJson.ToJson(function), out FunctionData copy) ? copy : function;

	// Inspector에서 BlockEditor 컴포넌트 오른쪽 ⋮ 메뉴 → "지금 함수를 JSON으로 보기"
	// 화면에서 만든 블록이 데이터에 제대로 들어갔는지 콘솔에서 확인할 때 쓴다
	[ContextMenu("지금 함수를 JSON으로 보기")]
	void LogFunctionJson()
	{
		if (Function == null) return;
		Debug.Log($"[BlockEditor] 블록 {Function.CountBlocks()}개\n{FunctionDataJson.ToJson(Function)}");
	}
}
