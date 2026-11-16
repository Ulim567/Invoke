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

	[SerializeField, Tooltip("블록을 끄는 동안 들어갈 자리에 보이는 줄 (InsertMarker 프리팹). #476부터 반투명 미리보기로 바꾸어 쓰지 않는다")]
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

	[SerializeField, Tooltip("고칠 수 있는 함수 이름 칸의 밑줄 (blk_underline). 비워 두면 밑줄 없이 글자만 보인다")]
	Sprite nameFieldSprite;

	[SerializeField, Tooltip("고칠 수 있는 함수 이름 칸의 밑줄 색")]
	Color nameFieldColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);

	[Header("오른쪽 창 너비·접기 (비워 두면 너비를 바꿀 수 없고 접히지 않는다)")]
	[SerializeField, Tooltip("오른쪽 창 왼쪽 끝의 손잡이 (PanelHandle의 EditorSidePanel). 열린 탭을 다시 누르면 이걸로 창을 접는다")]
	EditorSidePanel sidePanel;

	[SerializeField, Tooltip("세로 탭 맨 아래의 > < 버튼 (PanelToggle). 누르면 오른쪽 창을 접거나 편다")]
	Button panelToggleButton;

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

	[Header("함수 탭 (#489, 비워 두면 함수 탭을 쓰지 않는다)")]
	[SerializeField, Tooltip("함수 탭 창 (오른쪽 세로 막대의 「함수」를 누르면 보임. 처음엔 꺼 둔다)")]
	GameObject functionView;

	[SerializeField, Tooltip("세로 막대의 「함수」 버튼")]
	Button functionTabButton;

	[SerializeField, Tooltip("함수 탭의 검색 칸. 한글 이름, 영문 이름, 구역 이름으로 찾는다. 비워 두면 검색 없이 모두 보여 준다")]
	TMP_InputField functionSearch;

	[SerializeField, Tooltip("함수 탭에서 구역 머리글과 함수 항목이 쌓이는 곳 (Vertical Layout Group. 스크롤의 Content)")]
	RectTransform functionTabList;

	[SerializeField, Tooltip("함수 탭의 함수 항목 한 줄 (FunctionItem 프리팹)")]
	FunctionItemView functionTabItemPrefab;

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

	// 블록 목록에 보여 줄 문법 블록 견본들. 분류 → 소분류 → 블록 (#487, 시안 6). 끌 때마다 새 데이터를 만들도록 함수로 둔다.
	// 소분류 이름은 칩 줄 맨 앞에 회색 글씨로 보인다. 이름이 빈 소분류는 글씨 없이 칩만 놓는다
	// 문법 탭에는 문법 블록만 둔다 (#489). 스킬·기본 함수·malloc·free·wait·내가 만든 함수는 함수 탭에서 끈다 (RebuildFunctionTab)
	static readonly (string Title, (string Name, Func<BlockData>[] Blocks)[] Subgroups)[] PaletteGroups =
	{
		("제어", new[]
		{
			("조건", new Func<BlockData>[]
			{
				() => BlockData.Conditional(ConditionalBranch.If),
				() => BlockData.Conditional(ConditionalBranch.ElseIf),
				() => BlockData.Conditional(ConditionalBranch.Else),
			}),
			("반복", new Func<BlockData>[]
			{
				() => BlockData.For("i", Int("0"), Int("10")),
				() => BlockData.While(SlotData.EmptyValue()),
				() => BlockData.Break(),
				() => BlockData.Continue(),
			}),
		}),
		("변수", new[]
		{
			("변수", new Func<BlockData>[]
			{
				() => BlockData.Declare(new VariableType(VariableTypeKind.Int), "x"),
				// 대입·원소 읽기·쓰기의 변수 칸은 비워 두고, 놓은 뒤 드롭다운에서 선언한 변수를 고른다 (#474 5단계)
				() => BlockData.Assign("", SlotData.EmptyValue()),
			}),
			("배열", new Func<BlockData>[]
			{
				() => BlockData.ArrayDeclare(new VariableType(VariableTypeKind.Int), "arr", Int("3")),
				() => BlockData.ArrayGet("", SlotData.EmptyValue()),
				() => BlockData.ArraySet("", SlotData.EmptyValue(), SlotData.EmptyValue()),
			}),
		}),
		// 값 칸에 끼우는 블록
		("값", new[]
		{
			("", new Func<BlockData>[]
			{
				() => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue()),
				() => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue()),
				() => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue()),
				() => BlockData.Not(SlotData.EmptyValue()),
			}),
		}),
		// 함수를 끝내는 Return만. 함수를 부르는 블록은 함수 탭에 있다 (#489)
		("함수", new[]
		{
			("", new Func<BlockData>[]
			{
				() => BlockData.Return(),
			}),
		}),
	};

	// 칩 줄 맨 앞의 소분류 글씨 색 (시안 6의 작은 회색 글씨)
	static readonly Color SubgroupLabelColor = new Color32(0x8A, 0x84, 0xA0, 0xFF);

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

	// 저장하지 않은 작업본 (#494). 다른 페이지로 옮겨도 고친 내용을 버리지 않고 여기에 두었다가, 다시 열면 이어서 고친다.
	// 예: 전역 변수 x를 지운 채로 x를 쓰는 슬롯 Q를 고치러 갈 수 있어야 하기 때문이다.
	// 함수는 GUID로 찾고, 「+」로 만들어 아직 저장하지 않은 새 페이지도 들어 있다. 저장하면 빼고, 나가기 확인창에서 「나가기」를 누르면 모두 버린다
	readonly Dictionary<string, FunctionData> drafts = new Dictionary<string, FunctionData>();
	GlobalVariablesData globalsDraft; // 저장하지 않은 전역 변수 작업본. 없으면 null

	// 서버에 페이지 함수 지우기를 요청하고 답을 기다리는 함수의 GUID와 상태 줄에 보일 이름 (#494). 답이 오면 비운다
	string pendingDeleteGuid;
	string pendingDeleteLabel;

	// 전역 변수 편집 중이면 true. 이때 편집 영역은 globals.Declarations를 보여 준다
	bool editingGlobals;
	GlobalVariablesData globals;
	const string GlobalsKey = "__globals__";

	// 실제 계정이면 내 캐릭터의 FunctionExecutorNetwork (함수의 원본이 서버 DB). 아니면 null이고 이 컴퓨터의 파일을 쓴다
	FunctionExecutorNetwork server;
	static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
	RectTransform openDropdown;

	// 편집 영역 스크롤 (#476): 블록 자리(Content)의 가장 작은 크기를 맞추는 칸과 마지막으로 맞춘 보이는 칸 크기
	LayoutElement workspaceMinSize;
	Vector2 fittedViewportSize;

	// 마우스가 올라가 있는 블록·값 칸들. 블록 안의 값 칸, 값 칸 안의 블록처럼 겹치므로 가장 안쪽의 설명을 보인다
	readonly List<(Component owner, RectTransform anchor, Func<string> text)> hovered = new List<(Component, RectTransform, Func<string>)>();
	float hoverSince;
	Component tooltipOwner;
	BlockTooltipView tooltip;

	// 마우스를 올리고 이만큼 멈춰 있어야 설명창을 띄운다. 블록 위를 지나가기만 할 때 깜빡이지 않게
	const float TooltipDelay = 0.4f;

	// 끄는 동안 들어갈 자리에 보이는 반투명 미리보기 블록 (#476). 끌기 하나에 하나를 만들고, 놓으면 지운다
	RectTransform dropGhost;

	// 반투명 미리보기의 불투명도. 놓인 블록과 구분되면서 모양은 알아볼 수 있게
	const float GhostAlpha = 0.4f;

	BlockValueSlot highlightedSlot;
	// 값 블록을 끌 때 마우스에서 이 거리(캔버스 단위) 안의 칸에 붙인다. 값 칸이 작아 마우스 끝 한 점으로는 맞히기 어렵기 때문
	const float SnapDistance = 16f;
	// 함수 틀 안 이름 칸의 높이. 머리줄(28)에서 위아래 여백을 뺀 정도
	const float NameFieldHeight = 20f;
	// 지금 붙어 있는 칸(highlightedSlot)이 붙기 직전의 화면 사각형. 미리보기를 끼워 칸이 넓어지거나 줄이 바뀌어도
	// 마우스가 이 자리 근처에 있으면 그 칸에 그대로 붙어 있게 한다 (미리보기가 왔다 갔다 하지 않게)
	Rect snapRect;
	// 블록을 끄는 동안 청록·어둡게 표시한 빈 칸들 (#487). 놓으면 표시를 지운다
	readonly List<BlockValueSlot> hintedSlots = new List<BlockValueSlot>();
	Button asyncButton;
	bool paletteBuilt;
	readonly List<BlockValueSlot> errorSlots = new List<BlockValueSlot>();
	readonly List<RectTransform> errorFrames = new List<RectTransform>();
	readonly List<GameObject> errorItems = new List<GameObject>();
	// 오류 창에 지금 보이는 오류들의 글자·자리를 이은 것. 같으면 다시 만들지 않는다 (고칠 때마다 Redraw가 오류 창을 다시 그리기 때문)
	string shownErrorsKey;
	// 컴파일 버튼으로 찾은 지금 함수의 오류. 컴파일은 저장된 전역 변수·함수로 검사해서 작업본 기준 검사와 다를 수 있으므로 오류 창에 합친다. 고치면 비운다
	List<FunctionChecker.Error> compileErrors;
	// 전역 변수를 컴파일했을 때의 오류. 전역 변수 검사는 실행기(FunctionExecutor)가 하므로 고칠 때마다 하지 않고 컴파일 때만 한다
	List<FunctionChecker.Error> globalsErrors;
	// 저장·지우기를 막은 이유: 저장하면(지우면) 다른 함수에 생길 오류 (ShowBroken, #494). 지금 내용에는 없는 오류라 따로 두고 오류 창 맨 위에 보인다. 고치거나 다른 함수를 열면 비운다
	List<ShownError> brokenErrors;
	// 함수 목록 줄을 눌렀을 때 하는 일 (함수 GUID → 열기). 오류 창의 「블록 보기」가 다른 함수의 오류일 때 그 함수를 먼저 열 때 쓴다
	readonly Dictionary<string, Action> openActions = new Dictionary<string, Action>();
	string compileFailedKey; // 마지막 컴파일에서 오류가 난 함수 (목록에서 빨간 밑줄)
	GlobalVariablesData listGlobals; // 함수 목록의 오류 검사에 쓰는 전역 변수
	Coroutine successRoutine;
	readonly List<GameObject> savedButtons = new List<GameObject>();
	// 함수 목록의 줄. 컴파일 오류(빨간 밑줄)는 작업본에 따라 바뀌므로 줄에 두지 않고 Redraw가 계산한다 (#494)
	readonly List<(ListItemView item, string prefix, string guid, string name, string detail, bool dim)> functionItems = new List<(ListItemView, string, string, string, string, bool)>();
	List<FunctionData> savedFunctions = new List<FunctionData>();

	void Start()
	{
		if (trashZone != null) trashZone.SetActive(false);
		if (blockTabButton != null) blockTabButton.onClick.AddListener(() => OnTabClicked(RightTab.Blocks));
		if (functionTabButton != null) functionTabButton.onClick.AddListener(() => OnTabClicked(RightTab.Functions));
		if (errorTabButton != null) errorTabButton.onClick.AddListener(() => OnTabClicked(RightTab.Errors));
		if (referenceTabButton != null) referenceTabButton.onClick.AddListener(() => OnTabClicked(RightTab.Reference));
		if (panelToggleButton != null)
		{
			panelToggleButton.onClick.AddListener(TogglePanel);
			panelToggleButton.gameObject.SetActive(sidePanel != null); // 접을 창(손잡이)이 없으면 버튼도 숨긴다
		}
		// 창 너비가 바뀐 그 자리에서 블록 자리의 최소 크기를 맞춘다. Update에서만 맞추면 손잡이를 끄는 프레임에 가로 스크롤바가 깜빡인다
		if (sidePanel != null) sidePanel.Resized += OnSidePanelResized;
		if (referenceSearch != null) referenceSearch.onValueChanged.AddListener(_ => RebuildReference());
		if (paletteSearch != null) paletteSearch.onValueChanged.AddListener(_ => RebuildPalette());
		if (functionSearch != null) functionSearch.onValueChanged.AddListener(_ => RebuildFunctionTab());
		if (functionFrame != null && functionFrame.ReturnButton != null) functionFrame.ReturnButton.onClick.AddListener(OpenReturnTypeDropdown);
		ShowErrors(new List<ShownError>());
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

	// 오른쪽 창 너비가 바뀐 직후 (EditorSidePanel.Resized). 최소 크기만 바꾸면 블록 자리의 실제 크기는 화면 배치를 다시 계산할 때에야 줄어드는데,
	// 그 전에 ScrollRect가 LateUpdate에서 줄기 전의 블록 자리와 좁아진 보이는 칸을 비교해 가로 스크롤바를 켠다. 그래서 블록 자리 배치를 바로 다시 계산한다
	void OnSidePanelResized()
	{
		FitWorkspaceToViewport();
		if (layoutRoot != null) LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
	}

	// 설명창 (#476) -------------------------------------------------------------

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
		if (!editingGlobals && server != null && (Function == null || savedFunctions.All(f => f.Guid != Function.Guid)))
		{
			StashCurrent(); // 저장하지 않은 새 페이지였으면 작업본으로 둔다
			OpenFirstSlot();
		}
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
		// 가진 개수가 바뀌면(해금, 상점) 「남은/가진」을 다시 센다 (#500)
		if (localPlayer.TryGetComponent(out progressForCounts)) progressForCounts.Changed += RefreshCounts;
	}

	void UnbindServer()
	{
		if (progressForCounts != null) progressForCounts.Changed -= RefreshCounts;
		progressForCounts = null;
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
		if (request == PlayerDataRequest.SaveFunction || request == PlayerDataRequest.SaveGlobals || request == PlayerDataRequest.DeleteFunction) savePending = false;
		if (request == PlayerDataRequest.DeleteFunction)
		{
			// 성공이면 이 답보다 먼저 ReceivedFunctions에서 빠지고 목록을 다시 그렸다 (OnServerDataReceived)
			string guid = pendingDeleteGuid, label = pendingDeleteLabel ?? "함수";
			pendingDeleteGuid = pendingDeleteLabel = null;
			if (result == PlayerDataRequestResult.Saved && guid != null) AfterPageDeleted(guid, label);
			else
			{
				SetMessage(result switch
				{
					PlayerDataRequestResult.SlotFunctionLocked => "슬롯 함수는 지울 수 없음",
					PlayerDataRequestResult.UnknownFunction => $"{label}{Eul(label)} 찾지 못해 지우지 못함",
					_ => $"지우지 못함 ({result})",
				}, true);
				Redraw();
			}
			return;
		}
		if (request == PlayerDataRequest.SaveGlobals)
		{
			bool saved = result == PlayerDataRequestResult.Saved;
			if (saved && editingGlobals) dirty = false;
			if (saved) globalsDraft = null;
			// 저장한 전역 변수로 함수 목록의 오류 표시와 변수 고르기 목록(listGlobals)을 다시 만든다 (#474)
			if (saved) ReloadSavedFunctions();
			SetMessage(saved ? "저장됨"
				: result == PlayerDataRequestResult.CountExceeded ? CountShortageMessage(FindCountShortage(savedFunctions, globals))
				: "전역 변수 저장 실패", !saved);
			Redraw();
			return;
		}
		if (request != PlayerDataRequest.SaveFunction) return;
		if (result == PlayerDataRequestResult.Saved && !editingGlobals)
		{
			dirty = false;
			drafts.Remove(Function.Guid); // 답을 기다리는 동안 다른 페이지를 열 수 없으므로 지금 연 함수가 저장한 함수다
		}
		SetMessage(result switch
		{
			PlayerDataRequestResult.Saved => "저장됨",
			PlayerDataRequestResult.TooManyFunctions => $"함수는 {ProgressRules.MaxPageFunctions}개까지 더할 수 있음",
			PlayerDataRequestResult.SlotFunctionLocked => "슬롯 함수는 이름, 파라미터를 바꿀 수 없음",
			PlayerDataRequestResult.InvalidData => "저장하지 못함 (함수 데이터 오류)",
			PlayerDataRequestResult.BlockLocked => "저장하지 못함 (아직 해금하지 않은 블록이 있음)",
			PlayerDataRequestResult.SkillLocked => "저장하지 못함 (아직 해금하지 않은 스킬이 있음)",
			PlayerDataRequestResult.CodeSizeExceeded => $"저장하지 못함 (슬롯의 코드 영역이 {ProgressRules.MaxCodeSize}을 넘음)",
			PlayerDataRequestResult.CountExceeded => CountShortageMessage(Function == null ? null
				: FindCountShortage(savedFunctions.Where(f => f.Guid != Function.Guid).Append(Function), CurrentGlobals())),
			_ => $"저장하지 못함 ({result})",
		}, result != PlayerDataRequestResult.Saved);
		Redraw();
	}

	/// <summary>
	/// 저장하지 않은 변경이 어느 페이지에든 있으면 true: 지금 페이지를 열고 나서 고쳤거나, 「+」로 만든 새 페이지를 아직 저장하지 않았거나,
	/// 다른 페이지에 저장하지 않은 작업본이 있을 때 (#494). 편집 화면을 처음 열 때 기본으로 열리는 빈 함수는 고치기 전까지 해당하지 않는다.
	/// </summary>
	public bool HasUnsavedChanges => CurrentUnsaved || drafts.Count > 0 || globalsDraft != null;

	// 지금 연 페이지에 저장하지 않은 변경이 있으면 true (이름 뒤 *)
	bool CurrentUnsaved =>
		dirty || (unsavedNewPage && !editingGlobals && Function != null && savedFunctions.All(f => f.Guid != Function.Guid));

	/// <summary>
	/// 다른 함수를 열 때 부른다 (함수 목록의 줄, 「+」, Global Variables). 지금 페이지의 고친 내용은 작업본으로 두고 open을 한다 (#494).
	/// 예전에는 고친 내용을 버릴지 물었지만, 지운 전역 변수를 쓰던 함수를 고치러 가려면 고친 내용을 들고 옮겨야 해서 묻지 않는다.
	/// 서버 저장의 답을 기다리는 중이면 열지 않는다. 답이 오기 전에 페이지가 바뀌면 어느 페이지를 저장했는지 알 수 없기 때문이다.
	/// </summary>
	void SwitchTo(Action open)
	{
		if (savePending)
		{
			ShowNotice("저장 진행 중. 잠시 후 다시 시도", false);
			return;
		}
		StashCurrent();
		open();
	}

	// 지금 페이지에 저장하지 않은 변경이 있으면 작업본으로 둔다. 다른 페이지를 열거나 편집 영역을 비우기 전에 부른다
	void StashCurrent()
	{
		if (editingGlobals)
		{
			if (dirty) globalsDraft = globals;
			return;
		}
		if (Function != null && CurrentUnsaved) drafts[Function.Guid] = Function;
	}

	/// <summary>
	/// 편집 화면을 닫기 전에 부른다 (BlockEditorPanel). 저장하지 않은 변경이 어느 페이지에도 없으면 바로 close를 하고, 있으면 확인창에서
	/// 「나가기」를 눌렀을 때 모든 페이지의 저장하지 않은 내용을 버리고 close를 한다. 「취소」를 누르면 편집 화면에 남는다.
	/// 버리지 않으면 다음에 열 때 고치던 내용이 저장된 것처럼 남기 때문이다.
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
			ShowNotice("저장 진행 중. 잠시 후 다시 시도", false);
			return;
		}
		// 문장마다 줄을 나눈다. 한 줄로 이어 쓰면 확인창 폭에서 「니까?」처럼 글자 중간이 잘려 다음 줄로 넘어가기 때문 (#476)
		confirmDialog.Show("파일이 저장되지 않았습니다.\n저장하지 않고 나가시겠습니까?", () =>
		{
			DiscardUnsaved();
			close();
		}, "나가기 (Enter)", "취소 (Esc)");
	}

	// 모든 페이지의 저장하지 않은 내용(작업본)을 버리고, 지금 페이지는 마지막으로 저장한 상태를 다시 연다.
	// 저장한 적 없는 새 페이지는 사라지고 처음 화면으로 돌아간다
	void DiscardUnsaved()
	{
		drafts.Clear();
		globalsDraft = null;

		if (editingGlobals) OpenGlobals();
		else
		{
			FunctionData saved = Function != null ? savedFunctions.FirstOrDefault(f => f.Guid == Function.Guid) : null;
			if (saved != null) Open(server != null ? Copy(saved) : FunctionStorage.Load(saved.Guid) ?? saved);
			else if (server != null) OpenFirstSlot();
			else Open(FunctionData.Create(UniqueName("새_함수")));
		}
		ReloadSavedFunctions(); // 사라진 새 페이지 줄을 빼고, 작업본의 이름 대신 저장된 이름을 보인다
	}

	// 「을」 「를」을 받침에 맞게 고른다. 한글이 아닌 글자(guard 등)로 끝나면 둘을 같이 적는다
	static string Eul(string word)
	{
		if (string.IsNullOrEmpty(word)) return "을(를)";
		char last = word[word.Length - 1];
		// 영문 대문자 한 글자로 끝나면 그 글자를 읽는 소리로 고른다 (#487, 「슬롯 W를」, 「슬롯 R을」). 엘·엠·엔·알만 받침이 있다
		if (last >= 'A' && last <= 'Z' && (word.Length == 1 || !char.IsLetter(word[word.Length - 2])))
			return "LMNR".IndexOf(last) >= 0 ? "을" : "를";
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
	public void Open(FunctionData function) => OpenFunction(function, false);

	// 함수 목록에서 고른 함수를 연다. 저장하지 않은 작업본이 있으면 그것을, 없으면 loadSaved로 읽은 저장된 함수를 연다 (#494)
	void OpenPage(string guid, Func<FunctionData> loadSaved)
	{
		if (drafts.TryGetValue(guid, out FunctionData draft))
		{
			OpenFunction(draft, true);
			return;
		}
		FunctionData saved = loadSaved();
		if (saved != null) OpenFunction(saved, false);
	}

	// fromDraft: 저장하지 않은 작업본을 다시 여는지. 그러면 연 뒤에도 「저장하지 않은 변경 있음」으로 둔다
	void OpenFunction(FunctionData function, bool fromDraft)
	{
		// 변수 고르기와 칸 규칙은 저장된 전역 변수를 본다. 전역 변수의 작업본이 있어도 저장된 것을 본다 (#474)
		// 저장할 때의 검사가 「이 페이지의 새 내용 + 나머지는 저장된 상태」이기 때문이다 (#494)
		listGlobals = CurrentGlobals();
		workspace.ClearViews();
		editingGlobals = false;
		unsavedNewPage = false;
		Function = function;
		workspace.Bind(function.Blocks, this);
		if (functionNameField != null)
		{
			functionNameField.Text = DisplayNameOf(function);
			SetNameFieldEditable(!IsSlotFunction(function)); // 슬롯 함수 이름(slot1~slot8)은 바꿀 수 없음
		}
		dirty = fromDraft;
		SetMessage("");
		ResetCompileResult();
		RemoveStaleUnsavedEntry();
		Redraw();
	}

	// 저장하지 않은 새 페이지를 작업본으로 두지 않고 다른 곳으로 옮겼으면(고치지 않은 기본 빈 함수 등) 그 페이지의 「저장 안 함」 줄을 목록에서 지운다.
	// 저장한 적이 없어서 다시 열 수 없는데도 줄이 남아 있으면 눌러도 아무 일이 없기 때문이다 (#432). 작업본으로 둔 새 페이지는 줄을 남긴다 (#494)
	void RemoveStaleUnsavedEntry()
	{
		string currentGuid = editingGlobals ? null : Function?.Guid;
		bool stale = functionItems.Any(e => e.guid != null && e.guid != GlobalsKey
			&& e.guid != currentGuid && !drafts.ContainsKey(e.guid) && savedFunctions.All(f => f.Guid != e.guid));
		if (stale) ReloadSavedFunctions();
	}

	// 다른 함수를 열면 지난 컴파일 결과(빨간 표시, 성공 알림)를 지운다.
	// 오류 창과 열린 탭은 그대로 둔다. 오류 창은 모든 함수의 오류를 보이므로 다른 함수를 열어도 목록이 바뀌지 않기 때문
	void ResetCompileResult()
	{
		compileFailedKey = null;
		compileErrors = null;
		brokenErrors = null;
		ClearErrorMarks();
		HideSuccess();
	}

	// 컴파일 성공 알림(초록)을 바로 지운다
	void HideSuccess()
	{
		if (successRoutine != null) StopCoroutine(successRoutine);
		successRoutine = null;
		if (compileOkBadge != null) compileOkBadge.SetActive(false);
	}

	/// <summary>
	/// 전역 변수 선언을 편집 영역에 연다 (함수 목록의 Global Variables). 저장하지 않은 작업본이 있으면 그것을 (#494),
	/// 없으면 실제 계정은 서버가 보낸 것, 임시 계정은 이 컴퓨터의 파일.
	/// </summary>
	public void OpenGlobals()
	{
		bool fromDraft = globalsDraft != null;
		globals = fromDraft ? globalsDraft : CopyGlobals(server != null ? server.ReceivedGlobals : GlobalVariablesStorage.Load());

		workspace.ClearViews();
		editingGlobals = true;
		unsavedNewPage = false;
		workspace.Bind(globals.Declarations, this);
		if (functionNameField != null)
		{
			functionNameField.Text = "Global Variables";
			SetNameFieldEditable(false);
		}
		dirty = fromDraft;
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
			// 전역 변수 파일의 블록도 개수를 쓴다 (#500). 모자라면 서버에 보내지 않고 알린다
			ItemCountRules.Shortage? shortage = FindCountShortage(savedFunctions, globals);
			if (shortage != null)
			{
				SetMessage(CountShortageMessage(shortage), true);
				Redraw();
				return;
			}
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
			globalsDraft = null;
			// 저장한 전역 변수로 함수 목록의 오류 표시와 변수 고르기 목록(listGlobals)을 다시 만든다 (#474)
			ReloadSavedFunctions();
		}
		SetMessage(ok ? "저장됨 (전역 변수를 새로 만듦)" : "전역 변수 저장 실패", !ok);
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
			// 전역 변수를 지우거나 이름·타입을 바꿔서 그 변수를 쓰던 함수에 오류가 생기면 저장하지 않는다 (#494)
			if (RefuseIfBreaksOthers(savedFunctions, globals)) return;
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

		// 반환 타입을 바꿔서 이 함수를 부르던 다른 함수에 오류가 생기면 저장하지 않는다 (#494)
		if (RefuseIfBreaksOthers(savedFunctions.Where(f => f.Guid != Function.Guid).Append(Function), CurrentGlobals())) return;

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
			// 가진 개수보다 많이 쓰면 서버에 보내지 않고 어느 항목이 모자란지 알린다 (#500). 서버도 같은 규칙으로 다시 검사한다
			ItemCountRules.Shortage? shortage = FindCountShortage(savedFunctions.Where(f => f.Guid != Function.Guid).Append(Function), CurrentGlobals());
			if (shortage != null)
			{
				SetMessage(CountShortageMessage(shortage), true);
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
		drafts.Remove(Function.Guid);
		SetMessage("저장됨");
		ReloadSavedFunctions();

		// 슬롯에 넣어 둔 함수를 고쳤을 수 있으므로, 내 캐릭터의 슬롯 원본을 새로 만든다 (slot-caster.md 「ReloadFunctions」)
		// ReloadFunctions가 Changed를 내면 FunctionExecutorNetwork가 서버로 함수를 다시 보낸다 (network.md 「함수 데이터 전송」)
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null && localPlayer.TryGetComponent(out SlotLoadout loadout)) loadout.ReloadFunctions();
		Debug.Log($"[BlockEditor] 「{Function.Name}」 저장함: {FunctionStorage.PathOf(Function)}");
	}

	// 이 저장 뒤의 함수들(after)과 전역 변수(globalsAfter)로 저장된 다른 함수를 다시 컴파일해, 오류가 새로 생기면 저장하지 않고 true (#494).
	// 저장된 상태에는 컴파일 오류가 없어야 하기 때문이다. 오류가 난 채로 저장되면 「저장됨」으로 보여 편집 화면을 그대로 나갈 수 있고,
	// 그 함수는 시전할 때 실패한다 (지운 전역 변수는 UndeclaredVariable, 지운 함수를 부르면 슬롯 전체가 시전되지 않음)
	bool RefuseIfBreaksOthers(IEnumerable<FunctionData> after, GlobalVariablesData globalsAfter)
	{
		List<(FunctionData function, List<FunctionChecker.Error> errors)> broken =
			FunctionChecker.FindNewlyBroken(savedFunctions, CurrentGlobals(), after, globalsAfter);
		// 지금 저장하려는 함수 자신은 저장 버튼의 컴파일이 이미 본다
		if (!editingGlobals) broken.RemoveAll(b => b.function.Guid == Function.Guid);
		if (broken.Count == 0) return false;
		ShowBroken(broken, "에 컴파일 오류가 생겨 저장하지 않았습니다");
		return true;
	}

	// 다른 함수에 새로 생길 오류를 오류 창과 상태 줄에 보인다. 상태 줄은 「슬롯 Q, 페이지 a」 뒤에 what을 붙인 글자 (#494)
	void ShowBroken(List<(FunctionData function, List<FunctionChecker.Error> errors)> broken, string what)
	{
		// 오류 창 맨 위에 어느 함수의 어느 블록인지 보인다. 저장(지우기)했다면 생길 오류라 지금 내용에는 없으므로,
		// 「> 이름」을 누르면 그 함수만 열고 블록은 깜빡이지 않는다 (작업본과 검사한 내용의 블록 순서가 다를 수 있음)
		brokenErrors = new List<ShownError>();
		foreach ((FunctionData function, List<FunctionChecker.Error> list) in broken)
			foreach (FunctionChecker.Error error in list)
				brokenErrors.Add(new ShownError { Message = error.Message, Code = CodeOf(error), Location = LocationOf(function), Guid = function.Guid, BlockIndex = -1 });
		HideSuccess(); // 저장 버튼의 컴파일이 띄운 「컴파일 성공」을 지운다
		ShowTab(RightTab.Errors);
		// 오류가 생기는 함수를 작업본에서 이미 고쳤으면, 목록에는 밑줄이 없어도 저장된 내용 기준으로는 오류다. 그 함수를 먼저 저장하라고 알린다 (#494)
		string hint = broken.Any(b => drafts.ContainsKey(b.function.Guid)) ? ". 고친 함수를 먼저 저장해 주세요" : "";
		SetMessage($"{string.Join(", ", broken.Select(b => LabelOf(b.function)))}{what}{hint}", true);
		Redraw();
	}

	// 페이지 함수 지우기 (#494) ---------------------------------------------------

	// 함수 목록의 페이지 줄 ×를 눌렀을 때. 저장된 다른 함수가 이 함수를 부르면 지우지 않고 그 함수들을 알린다.
	// 부르지 않으면 확인창에서 「지우기」를 눌렀을 때 지운다. 지우는 것은 바로 저장소(서버 DB, 이 컴퓨터 파일)에 반영되므로 「지우기를 저장하는 버튼」이다
	void RequestDeletePage(FunctionData page)
	{
		if (savePending)
		{
			ShowNotice("저장 진행 중. 잠시 후 다시 시도", false);
			return;
		}
		if (IsSlotFunction(page)) return; // 슬롯 함수는 지울 수 없다 (×도 보이지 않음)

		// 저장할 때와 같은 규칙: 이 함수를 뺀 저장된 함수들로 다시 컴파일해 새 오류(error: call to undeclared function)가 생기는 함수를 찾는다
		// 예: 슬롯 Q가 foo를 부르면 foo를 지우는 순간 슬롯 Q 전체가 시전되지 않으므로 막는다
		List<(FunctionData function, List<FunctionChecker.Error> errors)> broken = FunctionChecker.FindNewlyBroken(
			savedFunctions, CurrentGlobals(), savedFunctions.Where(f => f.Guid != page.Guid), CurrentGlobals());
		if (broken.Count > 0)
		{
			ShowBroken(broken, $"에서 {page.Name}{Eul(page.Name)} 불러서 지우지 않았습니다");
			return;
		}

		string label = LabelOf(page);
		if (confirmDialog == null)
		{
			DeletePage(page);
			return;
		}
		// 문장마다 줄을 나눈다. 한 줄로 이어 쓰면 확인창 폭에서 글자 중간이 잘려 다음 줄로 넘어가기 때문 (#476)
		confirmDialog.Show($"{label}{Eul(label)} 지우시겠습니까?\n지운 함수는 되돌릴 수 없습니다.", () => DeletePage(page), "지우기 (Enter)", "취소 (Esc)");
	}

	// 실제 계정은 서버에 지우기를 요청하고(답은 OnServerAnswered), 임시 계정은 이 컴퓨터의 파일을 바로 지운다
	void DeletePage(FunctionData page)
	{
		if (server != null)
		{
			savePending = true; // 답이 오기 전에는 다른 페이지를 열거나 나가지 못하게 한다 (저장과 같음)
			pendingDeleteGuid = page.Guid;
			pendingDeleteLabel = LabelOf(page);
			server.RequestDeleteFunction(page.Guid);
			if (savePending) SetMessage("지우는 중...");
			Redraw();
			return;
		}

		bool deleted = FunctionStorage.Delete(page.Guid);
		if (deleted) AfterPageDeleted(page.Guid, LabelOf(page));
		else
		{
			SetMessage($"{LabelOf(page)}{Eul(LabelOf(page))} 지우지 못했습니다 (파일 없음)", true);
			Redraw();
		}
	}

	// 지운 뒤: 그 페이지의 작업본을 버리고, 지운 페이지를 열어 두었으면 다른 곳을 연다
	void AfterPageDeleted(string guid, string label)
	{
		drafts.Remove(guid);
		if (!editingGlobals && Function != null && Function.Guid == guid)
		{
			if (server != null) OpenFirstSlot();
			else Open(FunctionData.Create(UniqueName("새_함수")));
		}
		ReloadSavedFunctions();
		SetMessage($"{label}{Eul(label)} 지웠습니다");
		Redraw();
	}

	// 상태 글자·오류 창에 보일 함수 이름: 슬롯 함수는 「슬롯 Q」, 나머지는 「페이지 이름」 (확인창의 글자와 같음)
	string LabelOf(FunctionData function)
	{
		int slot = SlotIndexOf(function);
		return slot >= 0 ? $"슬롯 {KeyOf(slot)}" : $"페이지 {function.Name}";
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
			string error = null;
			bool ok = localPlayer != null && localPlayer.TryGetComponent(out FunctionExecutor executor) && executor.CanUseGlobalVariables(globals, out error);
			// 실행기가 찾은 첫 번째 이유를 그대로 보인다 (#504). 예: error: redefinition of 'x'
			// 이유가 없으면 내 캐릭터의 실행기를 찾지 못한 것이다 (접속 전 등)
			if (!ok) errors.Add(new FunctionChecker.Error { Code = "", Message = error ?? "error: function executor not found" });
			globalsErrors = errors.Count > 0 ? errors : null;
		}
		else
		{
			errors = FunctionChecker.Find(Function, CurrentGlobals(), savedFunctions);
			compileErrors = errors;
		}

		// 오류 창(모든 함수의 오류)은 아래 Redraw가 다시 그린다
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

	// 모든 함수(작업본 포함)의 오류. 키는 함수 GUID, 전역 변수는 GlobalsKey. 오류가 없는 함수는 들어 있지 않다.
	// 함수 목록의 빨간 밑줄과 오류 창이 같은 결과를 쓴다
	Dictionary<string, List<FunctionChecker.Error>> FindAllErrors(List<FunctionData> working, GlobalVariablesData workingGlobals)
	{
		var all = new Dictionary<string, List<FunctionChecker.Error>>();
		foreach (FunctionData function in working)
		{
			List<FunctionChecker.Error> errors = FunctionChecker.Find(function, workingGlobals, working);
			// 방금 컴파일한 함수는 컴파일 결과도 합친다. 컴파일은 저장된 전역 변수·함수로 검사해서 작업본 기준 검사에 없는 오류가 있을 수 있기 때문
			if (compileErrors != null && !editingGlobals && Function != null && function.Guid == Function.Guid)
				foreach (FunctionChecker.Error error in compileErrors)
					if (!errors.Any(e => e.Code == error.Code && e.Message == error.Message)) errors.Add(error);
			if (errors.Count > 0) all[function.Guid] = errors;
		}
		if (globalsErrors != null && globalsErrors.Count > 0) all[GlobalsKey] = globalsErrors;
		return all;
	}

	// 오류 창에 모든 함수의 오류를 보인다. 함수 목록 순서(Global Variables, 슬롯, 페이지)대로, 저장·지우기를 막은 이유는 맨 위에
	void ShowAllErrors(Dictionary<string, List<FunctionChecker.Error>> all, List<FunctionData> working)
	{
		var shown = new List<ShownError>();
		if (brokenErrors != null) shown.AddRange(brokenErrors);

		// 함수 목록에 있는 순서대로. 목록이 없는 화면(위쪽 줄 버튼)이면 작업본 순서대로
		var order = new List<string>();
		foreach (var entry in functionItems)
			if (entry.guid != null && !order.Contains(entry.guid)) order.Add(entry.guid);
		foreach (string guid in all.Keys)
			if (!order.Contains(guid)) order.Add(guid);

		foreach (string guid in order)
		{
			if (!all.TryGetValue(guid, out List<FunctionChecker.Error> errors)) continue;
			FunctionData function = guid == GlobalsKey ? null : working.FirstOrDefault(f => f.Guid == guid);
			string location = function != null ? LocationOf(function) : "Global Variables";
			foreach (FunctionChecker.Error error in errors)
			{
				string code = CodeOf(error);
				// 저장·지우기 거부로 이미 맨 위에 보인 오류는 다시 적지 않는다
				// 같은 함수 안의 같은 문장은 거르지 않는다. 빈 칸이 둘인 블록은 error: expected expression이 두 줄이어야 하기 때문 (#504)
				if (brokenErrors != null && brokenErrors.Any(s => s.Location == location && s.Code == code && s.Message == error.Message)) continue;
				shown.Add(new ShownError { Message = error.Message, Code = code, Location = location, Guid = guid,
					BlockIndex = function != null ? IndexOfBlock(function, error.Block) : -1 });
			}
		}
		ShowErrors(shown);
	}

	// 오류 카드 마지막 줄 「> 이름」의 이름: 슬롯 함수는 「슬롯 Q」, 페이지 함수는 이름만 (「페이지」는 붙이지 않음, 사용자 결정)
	string LocationOf(FunctionData function)
	{
		int slot = SlotIndexOf(function);
		return slot >= 0 ? $"슬롯 {KeyOf(slot)}" : function.Name;
	}

	// 오류 카드 가운데 줄의 코드 (예: if ( )). 특정 블록에 붙지 않은 오류(return 없음, 전역 변수 오류)는 빈 글자라 줄이 빠진다
	static string CodeOf(FunctionChecker.Error error) => error.Block != null ? error.Code : "";

	// 함수 안의 모든 블록을 정해진 순서로 (맨 위 블록부터, 칸·블록 영역 안쪽까지). 함수를 다시 열면 블록 데이터가 새로 만들어지므로 순번으로 같은 블록을 찾는다
	static IEnumerable<BlockData> AllBlocksOf(FunctionData function) =>
		function.Blocks == null ? Enumerable.Empty<BlockData>() : function.Blocks.SelectMany(b => b.SelfAndInnerBlocks());

	static int IndexOfBlock(FunctionData function, BlockData block)
	{
		if (block == null) return -1;
		int index = 0;
		foreach (BlockData b in AllBlocksOf(function))
		{
			if (ReferenceEquals(b, block)) return index;
			index++;
		}
		return -1;
	}

	// 오류 카드의 「> 이름」: 그 함수(또는 Global Variables)가 열려 있지 않으면 함수 목록 줄을 누른 것처럼 먼저 연다 (작업본은 그대로 남음).
	// 그다음 오류가 난 블록이 있으면 깜빡인다 (blockIndex가 -1이면 열기만)
	void ShowErrorBlock(string guid, int blockIndex)
	{
		if (!IsOpen(guid))
		{
			if (!openActions.TryGetValue(guid, out Action open)) return;
			open();
			if (!IsOpen(guid)) return; // 저장하는 중이라 열지 못함
		}
		if (blockIndex < 0 || editingGlobals) return;
		BlockData block = AllBlocksOf(Function).ElementAtOrDefault(blockIndex);
		if (block != null) ShowBlock(block);
	}

	// guid의 함수(GlobalsKey면 Global Variables)를 지금 편집 영역에 열어 두었으면 true
	bool IsOpen(string guid) => guid == GlobalsKey ? editingGlobals : !editingGlobals && Function != null && Function.Guid == guid;

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
	/// 블록을 끄는 동안 놓을 자리를 보여 준다 (BlockView가 부른다, #476).
	/// 들어갈 자리에 끄는 블록과 같은 모양의 반투명 블록을 실제로 끼워, 놓은 뒤의 모습(아래 블록이 밀려나는 것, 값 칸이 넓어지는 것)을 미리 보인다.
	/// 값 칸이면 그 칸도 보라색으로 표시한다. 놓을 수 없는 곳이면 아무것도 보이지 않는다.
	/// </summary>
	public void ShowDropPreview(BlockView dragged, Component target, UnityEngine.EventSystems.PointerEventData eventData)
	{
		if (highlightedSlot != null && !ReferenceEquals(target, highlightedSlot))
		{
			highlightedSlot.SetHighlight(false);
			highlightedSlot.HideGhost();
			highlightedSlot = null;
		}

		if (target is BlockValueSlot slot)
		{
			slot.SetHighlight(true);
			highlightedSlot = slot;
			MoveGhost(slot.transform, () => slot.ShowGhost(GhostOf(dragged)));
			return;
		}

		if (target is BlockList list)
		{
			int index = list.GetInsertIndex(eventData.position, eventData.pressEventCamera);
			if (list.CanInsert(dragged.Data, index))
			{
				MoveGhost(list.transform, () => list.PlaceGhost(GhostOf(dragged), index));
				return;
			}
		}

		ParkGhost();
	}

	/// <summary>
	/// 블록을 끌기 시작할 때 부른다 (BlockView, #487). 끄는 블록이 값 칸에 들어가는 블록이면, 편집 영역의 빈 칸마다
	/// 끼울 수 있는 칸은 청록, 끼울 수 없는 칸은 어둡게 표시한다 (시안 6-11). 값 칸에 들어가지 않는 블록(if, void 함수 등)은 표시하지 않는다.
	/// </summary>
	public void ShowDragHints(BlockView dragged)
	{
		HideDragHints();
		if (!CanBeValue(dragged.Data)) return;
		// 끄는 블록은 이미 편집 영역에서 꺼내 DragLayer에 있으므로, 자기 안의 칸은 여기에 들어오지 않는다
		foreach (BlockValueSlot slot in workspace.GetComponentsInChildren<BlockValueSlot>())
		{
			if (!slot.IsEmpty) continue; // 값이 들어 있는 칸은 그대로 둔다
			slot.SetDragHint(slot.CanAccept(dragged.Data) ? BlockValueSlot.DragHint.Accept : BlockValueSlot.DragHint.Reject);
			hintedSlots.Add(slot);
		}
	}

	/// <summary>
	/// 값 칸에 들어갈 블록을 끄는 동안, 마우스에서 SnapDistance 안에 있는 칸 중 끼울 수 있는 가장 가까운 칸 (BlockView가 놓을 자리를 찾을 때 부른다).
	/// 이미 붙어 있는 칸은 붙기 직전의 자리까지 거리로 치고, 거리가 같으면 그 칸을 그대로 고른다.
	/// 마우스가 편집 영역 밖(블록 목록, 휴지통)에 있거나 가까운 칸이 없으면 null.
	/// </summary>
	public BlockValueSlot FindSnapSlot(BlockData dragged, Vector2 screenPosition, Camera eventCamera)
	{
		if (!CanBeValue(dragged)) return null;
		// 편집 영역 밖에서는 붙지 않는다. 블록 목록에 놓아 지우려는 블록이 편집 영역 가장자리의 칸에 붙지 않게
		RectTransform area = workspaceViewport != null ? workspaceViewport : (RectTransform)workspace.transform;
		if (!RectTransformUtility.RectangleContainsScreenPoint(area, screenPosition, eventCamera)) return null;
		Rect areaRect = ScreenRectOf(area, eventCamera);
		float scale = CanvasScale();

		BlockValueSlot best = null;
		float bestDistance = float.MaxValue;
		foreach (BlockValueSlot slot in workspace.GetComponentsInChildren<BlockValueSlot>())
		{
			// 미리보기(DropGhost)는 끄는 블록을 Instantiate한 복사본이라 칸이 Init되지 않았다(editor가 null). 편집 영역에 들어와 있어도 진짜 칸이 아니므로 건너뛴다 (#498)
			if (dropGhost != null && slot.transform.IsChildOf(dropGhost)) continue;
			if (!slot.CanAccept(dragged)) continue;
			Rect rect = ScreenRectOf((RectTransform)slot.transform, eventCamera);
			if (!rect.Overlaps(areaRect)) continue; // 스크롤로 가려진 칸
			bool attached = ReferenceEquals(slot, highlightedSlot);
			float distance = DistanceTo(rect, screenPosition) / scale;
			if (attached) distance = Mathf.Min(distance, DistanceTo(snapRect, screenPosition) / scale);
			if (distance > SnapDistance) continue;
			if (distance < bestDistance || (distance <= bestDistance && attached))
			{
				best = slot;
				bestDistance = distance;
			}
		}

		// 새 칸에 붙을 때 그 칸의 지금 자리(미리보기를 끼우기 전)를 기억한다
		if (best != null && !ReferenceEquals(best, highlightedSlot)) snapRect = ScreenRectOf((RectTransform)best.transform, eventCamera);
		return best;
	}

	// rect의 화면 좌표 사각형
	static Rect ScreenRectOf(RectTransform rect, Camera eventCamera)
	{
		var corners = new Vector3[4];
		rect.GetWorldCorners(corners);
		Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
		Vector2 max = min;
		for (int i = 1; i < 4; i++)
		{
			Vector2 p = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
			min = Vector2.Min(min, p);
			max = Vector2.Max(max, p);
		}
		return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
	}

	// 점에서 사각형까지 거리 (사각형 안이면 0)
	static float DistanceTo(Rect rect, Vector2 point)
	{
		float dx = Mathf.Max(rect.xMin - point.x, 0f, point.x - rect.xMax);
		float dy = Mathf.Max(rect.yMin - point.y, 0f, point.y - rect.yMax);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	// 화면 픽셀 → 캔버스 단위. 해상도가 달라도 붙는 거리가 화면의 UI 크기에 맞게
	float CanvasScale()
	{
		Canvas canvas = dragLayer != null ? dragLayer.GetComponentInParent<Canvas>() : null;
		float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
		return scale > 0f ? scale : 1f;
	}

	/// <summary>끄는 동안의 칸 표시를 모두 지운다. 블록을 놓을 때 BlockView가 부른다 (#487)</summary>
	public void HideDragHints()
	{
		foreach (BlockValueSlot slot in hintedSlots)
			if (slot != null) slot.SetDragHint(BlockValueSlot.DragHint.None);
		hintedSlots.Clear();
	}

	/// <summary>놓을 자리 표시(반투명 미리보기, 놓을 값 칸)를 모두 지운다. 놓을 때 BlockView가 부른다</summary>
	public void HideDropPreview()
	{
		if (highlightedSlot != null)
		{
			highlightedSlot.SetHighlight(false);
			highlightedSlot.HideGhost();
		}
		highlightedSlot = null;
		DestroyGhost();
	}

	// 미리보기를 place로 옮기고, 자리나 순서가 바뀌었을 때만 레이아웃을 다시 계산한다 (끄는 동안 매 프레임 다시 계산하지 않게)
	void MoveGhost(Transform place, Action move)
	{
		Transform oldParent = dropGhost != null ? dropGhost.parent : null;
		int oldIndex = dropGhost != null ? dropGhost.GetSiblingIndex() : -1;
		bool wasActive = dropGhost != null && dropGhost.gameObject.activeSelf;
		move();
		dropGhost.gameObject.SetActive(true);
		if (!wasActive || oldParent != place || oldIndex != dropGhost.GetSiblingIndex()) RefreshLayout();
	}

	// 끄는 블록과 같은 모양의 반투명 블록. 끌기 하나에 한 번만 만든다 (블록 UI를 복사한 것이라 데이터는 없다)
	// 클릭을 통과시켜, 놓을 자리를 찾을 때 미리보기 자신이 걸리지 않게 한다
	RectTransform GhostOf(BlockView dragged)
	{
		if (dropGhost != null) return dropGhost;
		GameObject copy = Instantiate(dragged.gameObject, dragLayer);
		copy.name = "DropGhost";
		dropGhost = (RectTransform)copy.transform;
		dropGhost.localScale = Vector3.one; // 끄는 블록은 작게 보이지만, 미리보기는 놓은 뒤의 실제 크기
		if (copy.TryGetComponent(out CanvasGroup group))
		{
			group.alpha = GhostAlpha;
			group.blocksRaycasts = false;
			group.interactable = false;
		}
		return dropGhost;
	}

	// 놓을 수 없는 곳: 미리보기를 숨겨 둔다 (다시 놓을 수 있는 곳에 오면 다시 쓴다)
	void ParkGhost()
	{
		if (dropGhost == null || !dropGhost.gameObject.activeSelf) return;
		dropGhost.gameObject.SetActive(false);
		dropGhost.SetParent(dragLayer, false);
		RefreshLayout();
	}

	// 미리보기를 지운다. Destroy는 프레임 끝에 지우므로, 그 전에 블록 자리에서 빼고 꺼 둔다
	// (같은 프레임에 블록을 넣고 자리 번호를 셀 때, 편집 영역의 블록 목록에 미리보기가 섞이지 않게)
	void DestroyGhost()
	{
		if (dropGhost == null) return;
		dropGhost.gameObject.SetActive(false);
		dropGhost.SetParent(dragLayer, false);
		Destroy(dropGhost.gameObject);
		dropGhost = null;
		RefreshLayout();
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
	enum RightTab { Blocks, Functions, Errors, Reference }

	RightTab rightTab = RightTab.Blocks;

	// 오른쪽 창이 접혀 세로 탭만 보이면 true
	bool PanelCollapsed => sidePanel != null && sidePanel.Collapsed;

	// 세로 탭을 눌렀을 때: 이미 열린 탭을 다시 누르면 오른쪽 창을 접고, 접혀 있으면 펴서 누른 탭을 연다.
	// 편집 영역을 넓게 쓰고 싶을 때 창을 치울 수 있게
	void OnTabClicked(RightTab tab)
	{
		if (sidePanel != null && !PanelCollapsed && rightTab == tab)
		{
			sidePanel.SetCollapsed(true);
			ShowTab(tab, expand: false);
			return;
		}
		ShowTab(tab);
	}

	// 세로 탭 맨 아래 > < 버튼: 열려 있으면 접고, 접혀 있으면 마지막에 보던 탭으로 편다
	void TogglePanel()
	{
		if (sidePanel == null) return;
		if (PanelCollapsed)
		{
			ShowTab(rightTab);
			return;
		}
		sidePanel.SetCollapsed(true);
		ShowTab(rightTab, expand: false);
	}

	// 오른쪽: 블록 목록, 함수 탭, 오류 창, 참고 창 중 하나만 보인다. 접혀 있으면 하나도 보이지 않는다.
	// expand면 접힌 창을 편다. 컴파일 오류처럼 바로 보여 줘야 할 때도 펴서 연다 (사용자 결정).
	// 다른 함수를 열 때 오류 탭을 문법 탭으로 돌리는 것처럼, 탭만 바꿔 둘 때는 expand를 false로 해 접힌 채 둔다
	void ShowTab(RightTab tab, bool expand = true)
	{
		rightTab = tab;
		if (expand && sidePanel != null) sidePanel.SetCollapsed(false);
		bool open = !PanelCollapsed;
		if (paletteView != null) paletteView.SetActive(open && tab == RightTab.Blocks);
		if (functionView != null) functionView.SetActive(open && tab == RightTab.Functions);
		if (errorView != null) errorView.SetActive(open && tab == RightTab.Errors);
		if (referenceView != null) referenceView.SetActive(open && tab == RightTab.Reference);
		// 접혀 있으면 어느 탭도 고른 모양이 아니다
		SetTabLook(blockTabButton, open && tab == RightTab.Blocks);
		SetTabLook(functionTabButton, open && tab == RightTab.Functions);
		SetTabLook(errorTabButton, open && tab == RightTab.Errors);
		SetTabLook(referenceTabButton, open && tab == RightTab.Reference);
		if (!open) return;
		// 참고 탭을 열 때마다 카드를 다시 만든다. 스킬 트리 설명이나 해금 상태가 바뀌어도 다음에 열 때 맞춰진다
		if (tab == RightTab.Reference) RebuildReference();
		// 함수 탭도 열 때마다 다시 만든다. 그 사이 해금하거나 함수를 저장했어도 맞춰진다
		if (tab == RightTab.Functions) RebuildFunctionTab();
	}

	/// <summary>
	/// 블록 목록의 칩이나 함수 탭의 항목에서 새 블록을 꺼내 끌기 시작한다 (#489). 이후 끌기 이벤트는 새 블록이 받는다.
	/// finished는 블록을 놓았을 때 한 번 불린다 (함수 탭 항목의 점선 표시를 되돌릴 때). 없으면 null.
	/// </summary>
	public void BeginNewBlockDrag(BlockData data, UnityEngine.EventSystems.PointerEventData eventData, Action finished = null)
	{
		ClearHover(); // 끌기 시작하면 설명창을 지운다
		CreateView(data, dragLayer).BeginDragAsNew(eventData, finished);
	}

	// 참고 창: 검색 칸의 글자에 맞는 카드를 처음부터 다시 만든다 (BlockReference.Search)
	void RebuildReference()
	{
		if (referenceList == null || referenceCardPrefab == null) return;
		for (int i = referenceList.childCount - 1; i >= 0; i--) Destroy(referenceList.GetChild(i).gameObject);
		string query = referenceSearch != null ? referenceSearch.text : "";
		List<BlockReferenceEntry> found = BlockReference.Search(query);

		// 문법 탭과 같은 분류 순서로, 분류마다 머리글을 하나 넣고 그 아래에 카드를 놓는다 (#476). 카드가 없는 분류는 머리글도 없다
		// 분류는 카드 태그의 앞 단계(Group): 「제어 > 조건」, 「제어 > 반복」은 「제어」 머리글 아래 (#489)
		// 문법 탭에 없는 분류가 생겨도 카드가 빠지지 않게 그 분류는 맨 뒤에 둔다
		IEnumerable<string> categories = PaletteGroups.Select(group => group.Title).Concat(found.Select(entry => entry.Group)).Distinct();
		foreach (string category in categories)
		{
			List<BlockReferenceEntry> cards = found.Where(entry => entry.Group == category).ToList();
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

	// 오류 창의 오류 하나: 설명, 코드, 「> 이름」 세 줄. Guid·BlockIndex는 「> 이름」을 누를 때 열 함수와 깜빡일 블록 (BlockIndex가 -1이면 함수만 연다)
	struct ShownError
	{
		public string Message;  // 설명 (예: error: expected expression)
		public string Code;     // 오류가 난 블록의 글자 (예: if ( )). 블록이 없으면 빈 글자
		public string Location; // 함수 이름 (예: temp, 슬롯 Q, Global Variables)
		public string Guid;
		public int BlockIndex;
	}

	void ShowErrors(List<ShownError> errors)
	{
		// 고칠 때마다 불리므로, 보이는 내용이 같으면 다시 만들지 않는다
		string key = string.Join("\n", errors.Select(e => $"{e.Message}\t{e.Code}\t{e.Location}\t{e.Guid}\t{e.BlockIndex}"));
		if (key == shownErrorsKey) return;
		shownErrorsKey = key;

		foreach (GameObject item in errorItems) if (item != null) Destroy(item);
		errorItems.Clear();

		bool useItems = errorItemPrefab != null && errorList != null;
		if (errorText != null)
		{
			errorText.gameObject.SetActive(errors.Count == 0 || !useItems);
			errorText.text = errors.Count == 0 ? "오류 없음\n\n모든 함수의 오류가 여기에 나와요."
				: string.Join("\n\n", errors.Select(e => string.IsNullOrEmpty(e.Code) ? $"{e.Message}\n> {e.Location}" : $"{e.Message}\n{e.Code}\n> {e.Location}"));
		}
		if (useItems)
		{
			foreach (ShownError error in errors)
			{
				ErrorItemView item = Instantiate(errorItemPrefab, errorList);
				string guid = error.Guid;
				int index = error.BlockIndex;
				item.Show(error.Message, error.Code, error.Location, guid != null ? () => ShowErrorBlock(guid, index) : null);
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
		// 지난 컴파일·저장 거부의 오류도 지금 내용과 맞지 않으므로 지운다. 오류 창은 아래 Redraw가 지금 내용으로 다시 검사해 그린다
		compileErrors = null;
		brokenErrors = null;
		if (editingGlobals) globalsErrors = null;
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
		// 블록을 놓거나 지운 뒤, 저장 결과를 받은 뒤 모두 Redraw를 거치므로 여기서 「남은/가진」을 다시 센다 (#500)
		RefreshCounts();
		if (Function == null && !editingGlobals) return;

		// 빈 값 칸의 안내 글자 (#476). 선언의 타입이나 대입의 변수를 바꾸면 다른 칸이 받는 타입도 바뀌므로 고칠 때마다 다시 쓴다
		// 칸 테두리(실선·점선·없음, #487)도 내용이 바뀌면 달라지므로 함께 다시 그린다
		foreach (BlockValueSlot slot in workspace.GetComponentsInChildren<BlockValueSlot>())
		{
			// 미리보기(DropGhost)의 칸은 Init되지 않은 복사본이라 건너뛴다. 지금은 끄는 동안 Redraw를 부르지 않지만, 부르게 되어도 오류가 나지 않게 (#498)
			if (dropGhost != null && slot.transform.IsChildOf(dropGhost)) continue;
			slot.RefreshPlaceholder();
			slot.RefreshLook();
		}
		// 블록을 옮기면 겹친 깊이가 바뀌어 칠(밝기 교대)과 줄 여백도 달라지므로 다시 맞춘다 (#487). 옮긴 블록 안쪽 블록까지 모두
		foreach (BlockView view in workspace.GetComponentsInChildren<BlockView>()) view.RefreshLook();

		// 동기·비동기 버튼 (슬롯 함수와 전역 변수는 바꿀 수 없음)
		if (asyncButton != null)
		{
			TMP_Text asyncText = asyncButton.GetComponentInChildren<TMP_Text>();
			if (asyncText != null) asyncText.text = !editingGlobals && Function.IsAsync ? "비동기" : "동기";
			asyncButton.interactable = !editingGlobals && !IsSlotFunction(Function);
		}

		if (functionFrame != null) UpdateFunctionFrame();

		// 함수 목록: 지금 연 것을 고른 모양으로, 저장하지 않은 변경이 있으면 이름 뒤 * (지금 페이지와 작업본이 있는 페이지, #494)
		// 컴파일 오류가 있으면 이름에 빨간 밑줄. 작업본까지 반영해 고칠 때마다 다시 본다 (#494).
		// 예: 전역 변수 x를 지우면 저장하기 전에도 x를 쓰는 슬롯 Q에 밑줄이 생겨 어디를 고쳐야 하는지 보인다
		// 오류 창도 같은 검사 결과로 모든 함수의 오류를 보인다 (지금 연 함수만이 아니라, 사용자 요청)
		string openKey = editingGlobals ? GlobalsKey : Function.Guid;
		List<FunctionData> working = WorkingFunctions();
		GlobalVariablesData workingGlobals = WorkingGlobals();
		Dictionary<string, List<FunctionChecker.Error>> allErrors = FindAllErrors(working, workingGlobals);
		foreach (var entry in functionItems)
		{
			if (entry.item == null) continue;
			bool open = entry.guid != null && entry.guid == openKey;
			bool unsaved = open ? CurrentUnsaved : entry.guid != null && (drafts.ContainsKey(entry.guid) || (entry.guid == GlobalsKey && globalsDraft != null));
			bool error = entry.guid != null && (allErrors.ContainsKey(entry.guid) || entry.guid == compileFailedKey);
			entry.item.Show(entry.prefix, entry.name + (unsaved ? " <color=#8B9CFF>*</color>" : ""), entry.detail, open, entry.dim, error);
		}
		ShowAllErrors(allErrors, working);

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
		// 데이터 영역은 선언 수가 아니라 칸 수. 전역 array는 길이만큼 칸을 쓴다 (#478)
		if (editingGlobals) info = $"전역 변수 {globals.Declarations.Count}개   데이터 영역 {GlobalVariablesConverter.CountCells(globals)}칸";
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
		functionFrame.ShowFunction(ReturnTypeName(Function.ReturnType), DisplayNameOf(Function), parameters, !slot);
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
			where = slot >= 0 ? $"슬롯 {KeyOf(slot)}" : saved ? "페이지 함수" : "새 함수";
		}
		return CurrentUnsaved ? $"{where}, 저장하지 않은 변경 있음" : where;
	}

	// 슬롯 번호(0~7)의 키 글자 Q W E R A S D F
	static string KeyOf(int slot) => slot >= 0 && slot < SlotKeys.Length ? SlotKeys[slot] : (slot + 1).ToString();

	// 화면에 보일 함수 이름 (#487). 실제 계정의 슬롯 함수는 슬롯 키(Q~F)가 이름이다. 데이터의 이름(slot1~slot8)은 서버가 검사하므로 그대로 두고 화면에서만 바꾼다
	string DisplayNameOf(FunctionData function)
	{
		int slot = SlotIndexOf(function);
		return slot >= 0 ? KeyOf(slot) : function.Name;
	}

	// 함수가 들어 있는 슬롯 번호(0~7). 슬롯에 없으면 -1
	// 실제 계정은 서버가 보낸 슬롯, 임시 계정은 내 캐릭터의 장착(SlotLoadout). 임시 계정도 슬롯에 끼운 함수를 슬롯 함수로 다룬다 (#487)
	int SlotIndexOf(FunctionData function)
	{
		if (function == null) return -1;
		if (server != null)
		{
			IReadOnlyList<SlotLoadout.Entry> slots = server.ReceivedSlots;
			if (slots == null) return -1;
			for (int i = 0; i < slots.Count; i++)
				if (slots[i].Id == function.Guid) return i;
			return -1;
		}

		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null || !localPlayer.TryGetComponent(out SlotLoadout loadout)) return -1;
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			SlotLoadout.Entry entry = loadout.GetEntry(i);
			if (!entry.IsEmpty && entry.Kind == SlotLoadout.EntryKind.UserFunction && entry.Id == function.Guid) return i;
		}
		return -1;
	}

	// 「남은/가진」 (#500) ------------------------------------------------------

	// 가진 개수가 바뀌는 것을 받는 내 캐릭터의 진행도. 실제 계정만 있다 (BindServer)
	PlayerProgress progressForCounts;
	// 항목 ID → 지금 쓴 개수 (저장된 다른 함수 + 지금 고치는 함수·전역 변수). null이면 개수를 보지 않는다 (임시 계정)
	Dictionary<string, int> usedCounts;

	/// <summary>
	/// 블록 하나의 「남은/가진」 개수 (#500). 개수가 없는 블록(내가 만든 함수 호출)이나 임시 계정이면 null.<br/>
	/// 남은 개수 = 가진 개수 − 쓴 개수. 쓴 개수에는 저장하지 않은 작업본도 들어가므로, 화면에서 고친 그대로 바뀐다 (RefreshCounts).
	/// 함수 탭 항목(FunctionItemView)과 문법 탭 칩(BlockView)이 부른다.
	/// </summary>
	public (int Remaining, int Owned)? CountOf(BlockData block)
	{
		IReadOnlyDictionary<string, int> owned = CurrentItemCounts();
		string key = BlockUsageCounter.KeyOf(block);
		if (owned == null || usedCounts == null || key == null) return null;
		int have = owned.TryGetValue(key, out int count) ? count : 0;
		int used = usedCounts.TryGetValue(key, out int u) ? u : 0;
		return (have - used, have);
	}

	/// <summary>
	/// 함수 탭·문법 탭에서 새 블록을 끌기 시작해도 되면 true. 남은 개수가 0 이하면 끌지 않고 위쪽 줄에 알린다 (#500, 사용자 결정 (가)).
	/// </summary>
	public bool CanDragNew(BlockData block)
	{
		(int Remaining, int Owned)? count = CountOf(block);
		if (count == null || count.Value.Remaining > 0) return true;
		// 영문 이름(thrust) 뒤에는 「이/가」를 고를 수 없어 조사 없이 적는다
		SetMessage($"{ItemDisplayName(BlockUsageCounter.KeyOf(block))}: 남은 개수 없음", true);
		Redraw();
		return false;
	}

	// 쓴 개수를 다시 세고 함수 탭 항목과 문법 탭 칩의 숫자를 다시 쓴다. 블록을 바꿀 때마다(Redraw), 가진 개수가 바뀔 때 부른다
	void RefreshCounts()
	{
		usedCounts = null;
		if (CurrentItemCounts() != null && server.ReceivedSlots != null)
		{
			var slotGuids = server.ReceivedSlots.Where(e => e.Kind == SlotLoadout.EntryKind.UserFunction).Select(e => e.Id).ToList();
			// 저장하지 않은 작업본(다른 페이지의 drafts, 전역 변수 globalsDraft)과 지금 고치는 내용까지 넣어 센다 (사용자 결정 (나))
			// 화면에서 고친 그대로 숫자가 바뀌게 하기 위함. 서버는 다른 함수를 저장된 내용으로 보므로, 숫자가 괜찮아도
			// 저장 순서에 따라 거부될 수 있다 (예: 다른 함수에서 뺀 것을 저장하기 전에 이 함수에 놓음). 그때는 저장 검사(FindCountShortage)가 안내한다
			var functions = new Dictionary<string, FunctionData>();
			foreach (FunctionData saved in savedFunctions) functions[saved.Guid] = saved;
			foreach (FunctionData draft in drafts.Values) functions[draft.Guid] = draft;
			if (!editingGlobals && Function != null) functions[Function.Guid] = Function;
			GlobalVariablesData countedGlobals = editingGlobals ? globals : globalsDraft ?? CurrentGlobals();
			usedCounts = BlockUsageCounter.Count(slotGuids, functions.Values, countedGlobals);
		}
		if (functionTabList != null)
			foreach (FunctionItemView item in functionTabList.GetComponentsInChildren<FunctionItemView>()) item.RefreshCount();
		if (palette != null)
			foreach (BlockView chip in palette.GetComponentsInChildren<BlockView>()) chip.RefreshCount();
	}

	// 블록·함수의 가진 개수 (#500). 실제 계정만 있고, 임시 계정이면 null이어서 개수를 보지 않는다 (무엇이든 놓을 수 있음)
	IReadOnlyDictionary<string, int> CurrentItemCounts()
	{
		if (server != null && NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PlayerProgress progress))
			return progress.ItemCounts;
		return null;
	}

	// 저장하면 개수가 모자라는 항목 (#500). 서버의 저장 검사(FunctionExecutorNetwork.CheckItemCounts)와 같은 규칙으로 먼저 찾아 안내한다
	// functionsAfter, globalsAfter: 저장한 뒤의 모든 함수와 전역 변수. 저장하기 전은 서버에 저장된 값(savedFunctions, CurrentGlobals)
	ItemCountRules.Shortage? FindCountShortage(IEnumerable<FunctionData> functionsAfter, GlobalVariablesData globalsAfter)
	{
		IReadOnlyDictionary<string, int> owned = CurrentItemCounts();
		if (owned == null || server.ReceivedSlots == null) return null;
		var slotGuids = server.ReceivedSlots.Where(e => e.Kind == SlotLoadout.EntryKind.UserFunction).Select(e => e.Id).ToList();
		Dictionary<string, int> before = BlockUsageCounter.Count(slotGuids, savedFunctions, CurrentGlobals());
		Dictionary<string, int> after = BlockUsageCounter.Count(slotGuids, functionsAfter, globalsAfter);
		return ItemCountRules.FindShortage(before, after, owned);
	}

	// 개수가 모자라 저장하지 않을 때의 안내. shortage가 없으면(서버만 걸림) 항목 없이 알린다
	static string CountShortageMessage(ItemCountRules.Shortage? shortage)
	{
		if (shortage == null) return "개수가 모자라 저장하지 않음 (가진 개수보다 많이 씀)";
		ItemCountRules.Shortage s = shortage.Value;
		return $"개수가 모자라 저장하지 않음 ({ItemDisplayName(s.ItemId)} {s.Used}개 씀, {s.Owned}개 가짐)";
	}

	// 항목 ID를 화면 이름으로: 사전 정의 함수는 영문 이름(c.thrust → thrust), else if는 띄어 쓰고, 나머지 블록은 그대로 (If, Declare)
	static string ItemDisplayName(string itemId)
	{
		if (itemId.Contains('.')) return BlockLabels.PredefinedFunctionName(itemId);
		return itemId == ConditionalBranch.ElseIf.ToString() ? "Else If" : itemId;
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

	// 해금한 스킬 트리 노드. 실제 계정만 있고, 임시 계정이면 null이어서 블록 목록·함수 탭에 모두 보여 준다
	HashSet<string> CurrentUnlocked()
	{
		if (server != null && NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out PlayerProgress progress))
			return new HashSet<string>(progress.Unlocked);
		return null;
	}

	void BuildPalette()
	{
		unlockedForPalette = CurrentUnlocked();

		string query = paletteSearch != null ? paletteSearch.text.Trim() : "";

		foreach ((string title, (string Name, Func<BlockData>[] Blocks)[] subgroups) in PaletteGroups)
		{
			// 소분류마다 해금한 블록 중 검색 글자에 맞는 것만 남긴다 (#476, #487). 남은 블록이 없는 소분류는 줄째, 분류는 머리글째 만들지 않는다 (머리글 색을 첫 블록으로 정하므로)
			var shownGroups = new List<(string Name, Func<BlockData>[] Blocks)>();
			foreach ((string subName, Func<BlockData>[] blocks) in subgroups)
			{
				IEnumerable<Func<BlockData>> candidates = blocks;
				if (unlockedForPalette != null)
					candidates = candidates.Where(template => SkillTree.IsBlockUnlocked(template().Kind, unlockedForPalette));
				Func<BlockData>[] shown = candidates.Where(template => MatchesPaletteSearch(template(), title, subName, query)).ToArray();
				if (shown.Length > 0) shownGroups.Add((subName, shown));
			}
			if (shownGroups.Count == 0) continue;

			if (headerPrefab != null)
			{
				ListHeaderView header = Instantiate(headerPrefab, palette);
				// 분류 색 네모: 그 분류 블록의 색
				header.Show(title, null, BlockLabels.ColorOf(shownGroups[0].Blocks[0]()));
			}
			// 소분류마다 칩을 줄줄이 놓을 칸 (줄이 차면 다음 줄로). 맨 앞에 소분류 이름
			foreach ((string subName, Func<BlockData>[] shown) in shownGroups)
			{
				paletteTarget = paletteGroupPrefab != null ? Instantiate(paletteGroupPrefab, palette) : palette;
				// 소분류 글씨가 있는 줄은 내어쓰기: 칩이 넘쳐 다음 줄로 가도 글씨 아래가 아니라 첫 칩과 같은 자리에서 시작한다 (시안 6)
				if (paletteTarget != palette && paletteTarget.TryGetComponent(out FlowLayoutGroup flow)) flow.HangingIndent = subName.Length > 0;
				if (subName.Length > 0) AddSubgroupLabel(subName);
				foreach (Func<BlockData> template in shown) AddTemplate(template);
			}
		}
		paletteTarget = palette;
	}

	// 칩 줄 맨 앞의 소분류 글씨 (#487). 블록 글자 프리팹을 쓰고 색만 회색으로. 클릭을 받지 않으므로 칩 끌기를 막지 않는다
	// 글자 크기는 블록 글자와 같게 둔다. 픽셀 글꼴(Galmuri11)은 작게 줄이면 글자가 깨지기 때문
	void AddSubgroupLabel(string name)
	{
		TMP_Text label = CreateText(paletteTarget != null ? paletteTarget : palette);
		label.text = name;
		label.color = SubgroupLabelColor;
		label.raycastTarget = false;
	}

	// 문법 탭 검색 (#476): 비어 있거나, 분류·소분류 이름, 칩 이름에 검색 글자가 들어 있으면 true (대소문자 구분 없음)
	// 분류·소분류 이름이 맞으면 그 블록들을 모두 보여 준다. 예: 「제어」 → If, For, While …, 「반복」 → For, While, Break, Continue (#487)
	// 스킬 이름 검색은 함수 탭이 한다 (#489, 문법 탭에는 스킬이 없음)
	bool MatchesPaletteSearch(BlockData block, string groupTitle, string subgroupName, string query) =>
		query.Length == 0 || ContainsText(groupTitle, query) || ContainsText(subgroupName, query) || ContainsText(PaletteNameOf(block), query);

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

	// 편집 화면을 열 때(해금이 바뀌었을 수 있음)와 검색 글자가 바뀔 때 문법 탭을 다시 만든다
	void RebuildPalette()
	{
		if (!paletteBuilt || palette == null) return;
		for (int i = palette.childCount - 1; i >= 0; i--) Destroy(palette.GetChild(i).gameObject);
		BuildPalette();
		// 함수 탭의 「내가 만든 함수」도 저장한 함수로 만들므로 함께 다시 만든다
		if (rightTab == RightTab.Functions) RebuildFunctionTab();
	}

	// 함수 탭 (#489, 시안 6-12) ------------------------------------------------------

	// 함수 탭 항목 하나: 영문 이름, 한글 이름, 원형, 끌면 만들 블록
	readonly struct FunctionTabItem
	{
		public readonly string Name, KoreanName, Prototype;
		public readonly Func<BlockData> Factory;

		public FunctionTabItem(string name, string koreanName, string prototype, Func<BlockData> factory)
		{
			Name = name;
			KoreanName = koreanName;
			Prototype = prototype;
			Factory = factory;
		}
	}

	// 함수 탭: 구역 스킬 / 기본 함수 / 메모리 / 내가 만든 함수. 해금한 것만 보인다 (블록 목록과 같은 규칙, 서버 저장 검사와 같음)
	// - 사전 정의 함수와 내가 만든 함수: 함수 블록(BlockKind.Function)을 해금해야 보인다. 스킬 트리에 노드가 있는 스킬은 그 노드도
	// - malloc, free, wait: 사전 정의 함수가 아니라 각자의 블록이므로 그 블록 종류를 해금해야 보인다
	void RebuildFunctionTab()
	{
		if (functionTabList == null || functionTabItemPrefab == null) return;
		for (int i = functionTabList.childCount - 1; i >= 0; i--) Destroy(functionTabList.GetChild(i).gameObject);

		HashSet<string> unlocked = CurrentUnlocked();
		bool Unlocked(BlockKind kind) => unlocked == null || SkillTree.IsBlockUnlocked(kind, unlocked);
		bool functionUnlocked = Unlocked(BlockKind.Function);

		var skills = new List<FunctionTabItem>();
		var basics = new List<FunctionTabItem>();
		if (functionUnlocked && PredefinedFunctionRegistry.All != null)
		{
			foreach (PredefinedFunction function in PredefinedFunctionRegistry.All)
			{
				string id = function?.Id;
				if (id == null || !PaletteFunctionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))) continue;
				if (unlocked != null && SkillTree.TryGet(id, out _) && !unlocked.Contains(id)) continue;
				int parameterCount = function.Parameters?.Count ?? 0;
				var item = new FunctionTabItem(BlockLabels.PredefinedFunctionName(id), BlockReference.KoreanNameOf(id), BlockReference.PrototypeOf(id),
					() => BlockData.CallPredefinedFunction(id, EmptySlots(parameterCount)));
				// C 스킬과 공통 이동(dash 등)은 스킬, 나머지(get_bug, is_valid_target)는 기본 함수
				bool skill = id.StartsWith("c.", StringComparison.Ordinal) || id.StartsWith("common.", StringComparison.Ordinal);
				(skill ? skills : basics).Add(item);
			}
		}
		// wait 블록은 시간을 비워 둔다 (wait( 초);). 무엇을 적을지는 놓은 뒤 플레이어가 정한다
		AddBuiltin(basics, () => BlockData.Wait(SlotData.EmptyValue()));

		var memory = new List<FunctionTabItem>();
		AddBuiltin(memory, () => BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)));
		AddBuiltin(memory, () => BlockData.Free(SlotData.EmptyValue()));

		// 내가 만든 함수: 페이지 함수만. 슬롯 함수는 슬롯 키로 시전하는 함수라 다른 함수 안에서 부르지 않는다 (#470). 한글 이름은 없다
		var mine = new List<FunctionTabItem>();
		if (functionUnlocked)
		{
			foreach (FunctionData saved in PageFunctions())
			{
				string guid = saved.Guid;
				int parameterCount = saved.Parameters?.Count ?? 0;
				mine.Add(new FunctionTabItem(saved.Name, "", BlockReference.PrototypeOf(saved), () => BlockData.CallUserFunction(guid, EmptySlots(parameterCount))));
			}
		}

		AddFunctionSection("스킬", skills);
		AddFunctionSection("기본 함수", basics);
		AddFunctionSection("메모리", memory);
		AddFunctionSection("내가 만든 함수", mine);

		// 함수처럼 보이는 블록(malloc, free, wait): 그 블록 종류를 해금했을 때만 넣는다
		void AddBuiltin(List<FunctionTabItem> items, Func<BlockData> factory)
		{
			BlockKind kind = factory().Kind;
			var builtin = BlockReference.BuiltinOf(kind);
			if (!Unlocked(kind) || builtin == null) return;
			items.Add(new FunctionTabItem(builtin.Value.Name, builtin.Value.KoreanName, builtin.Value.Prototype, factory));
		}
	}

	// 함수 탭의 구역 하나: 머리글 아래에 항목들. 검색 칸이 비어 있지 않으면 맞는 항목만 남기고, 남은 항목이 없는 구역은 머리글도 만들지 않는다
	// 검색: 구역 이름(메모리)이 맞으면 그 구역 전체, 아니면 한글 이름(휩쓸)이나 영문 이름(sweep)에 검색 글자가 든 항목 (대소문자 구분 없음, 블록 목록 검색과 같은 규칙)
	void AddFunctionSection(string title, List<FunctionTabItem> items)
	{
		string query = functionSearch != null ? functionSearch.text.Trim() : "";
		if (query.Length > 0 && !ContainsText(title, query))
			items = items.Where(item => ContainsText(item.Name, query) || ContainsText(item.KoreanName, query)).ToList();
		if (items.Count == 0) return;
		if (headerPrefab != null) Instantiate(headerPrefab, functionTabList).Show(title, null); // 색 네모 없이 글자만 (시안 6-12)
		// 제목은 한글 이름. 영문 이름은 원형에 들어 있으므로 따로 적지 않는다. 한글 이름이 없는 내가 만든 함수는 함수 이름
		foreach (FunctionTabItem item in items)
			Instantiate(functionTabItemPrefab, functionTabList).Show(this, string.IsNullOrEmpty(item.KoreanName) ? item.Name : item.KoreanName, item.Prototype, item.Factory);
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

		// 함수 이름은 함수 틀(「void 이름( ) {」)에만 보이고 거기서 고친다 (사용자 요청: 위쪽 줄과 두 번 보여 혼잡함).
		// 함수 틀이 없는 화면이면 예전처럼 위쪽 줄 맨 앞에 둔다
		if (functionFrame != null)
		{
			functionNameField = CreateField(topBar);
			functionFrame.PlaceNameField((RectTransform)functionNameField.transform);
			// 칸을 머리줄 높이만큼 키운다. 밑줄은 칸 아래 끝에 그려지고 글자는 칸 가운데에 있으므로, 칸이 높을수록 글자와 밑줄 사이가 벌어진다
			// (칸 기본 높이 14로는 밑줄이 글자에 붙어 보였다)
			if (functionNameField.TryGetComponent(out LayoutElement nameSize)) nameSize.minHeight = NameFieldHeight;
		}
		else
		{
			functionNameField = CreateField(topBar);
			functionNameField.transform.SetSiblingIndex(0); // 상태 글자(Message Label)가 위쪽 줄 안에 있으면 그 앞에
		}
		functionNameField.MinWidth = 30f;
		// 함수 틀의 다른 글자(반환 타입, 괄호)와 같은 크기, 굵게
		if (functionNameField.TryGetComponent(out TMP_InputField nameInput))
		{
			nameInput.pointSize = 12f;
			if (nameInput.textComponent != null) nameInput.textComponent.fontStyle = FontStyles.Bold;
		}
		SetNameFieldEditable(false);

		// 동기·비동기 고르기 (FunctionData.IsAsync, function-data.md #132)
		asyncButton = AddTopButton("동기", ToggleAsync, saveButtonPrefab);
		asyncButton.transform.SetSiblingIndex(0);
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
			functionNameField.Text = DisplayNameOf(Function);
			Redraw();
		};

		if (functionList == null || functionItemPrefab == null) AddTopButton("새 함수", NewFunction); // 함수 목록이 없으면 위쪽 줄에
		if (compileButtonPrefab != null) AddTopButton("컴파일", Compile, compileButtonPrefab);
		AddTopButton("저장", SaveFunction, saveButtonPrefab);

		infoText = infoLabel != null ? infoLabel : CreateText(topBar);
	}

	// 함수 이름 칸을 고칠 수 있게(페이지 함수) 또는 못 고치게(슬롯 함수, Global Variables) 한다.
	// 고칠 수 있는 칸에만 청록 밑줄을 긋는다. 블록 안의 이름 칸(선언 이름)도 밑줄이라, 밑줄이 「여기를 고칠 수 있다」는 표시가 된다
	void SetNameFieldEditable(bool editable)
	{
		if (functionNameField == null) return;
		functionNameField.Interactable = editable;
		functionNameField.SetBackground(editable ? nameFieldSprite : null, nameFieldColor);
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
	// onDelete가 있으면 줄 오른쪽에 지우기 버튼(×)을 보인다 (#494, 저장한 페이지 함수만)
	void AddFunctionEntry(string prefix, string guid, string name, string detail, Action onClick, bool dim = false, Action onDelete = null)
	{
		if (HasFunctionList)
		{
			ListItemView item = Instantiate(functionItemPrefab, functionList);
			item.SetBoxed(guid == GlobalsKey); // Global Variables는 상자로
			item.SetDeleteAction(onDelete);
			if (onClick != null) item.Button.onClick.AddListener(() => onClick());
			item.Button.interactable = onClick != null;
			if (guid != null && onClick != null) openActions[guid] = onClick; // 오류 창의 「블록 보기」가 다른 함수를 열 때
			savedButtons.Add(item.gameObject);
			functionItems.Add((item, prefix, guid, name, detail, dim));
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
		AddFunctionEntry("G", GlobalsKey, "Global Variables", "", () => SwitchTo(OpenGlobals));

		// 슬롯 Q~F: 내 캐릭터의 장착(SlotLoadout.GetEntry). 함수면 눌러서 연다, C 스킬·빈 슬롯은 누를 수 없음
		// 실제 계정과 같게 줄에는 슬롯 키만 보인다 (#487, 사용자 결정). 끼운 함수의 이름은 그 줄을 열면 함수 틀에 보인다
		var equipped = new HashSet<string>();
		AddFunctionHeader("슬롯", null);
		SlotLoadout loadout = null;
		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer != null) localPlayer.TryGetComponent(out loadout);
		for (int slot = 0; slot < SlotLoadout.SlotCount; slot++)
		{
			string key = KeyOf(slot);
			SlotLoadout.Entry entry = loadout != null ? loadout.GetEntry(slot) : default;
			// 빈 슬롯, C 스킬, 지워진 함수는 열 수 있는 함수가 없으므로 키만 보이고 누를 수 없다 (「비어 있음」 글자와 흐리게 하는 표시는 없음)
			FunctionData slotFunction = loadout == null || entry.IsEmpty || entry.Kind == SlotLoadout.EntryKind.Skill
				? null
				: savedFunctions.FirstOrDefault(f => f.Guid == entry.Id);
			if (slotFunction == null)
			{
				AddFunctionEntry("", null, key, key, null);
				continue;
			}
			equipped.Add(slotFunction.Guid);
			string slotGuid = slotFunction.Guid;
			// 작업본이 없으면 누를 때 파일에서 새로 읽어 연다 (오류 밑줄은 Redraw가 작업본으로 계산)
			AddFunctionEntry("", slotGuid, key, key, () => SwitchTo(() => OpenPage(slotGuid, () => FunctionStorage.Load(slotGuid))));
		}

		// 페이지: 슬롯에 넣지 않은 저장한 함수. + 로 새 함수
		List<FunctionData> pages = savedFunctions.Where(f => !equipped.Contains(f.Guid)).ToList();
		AddFunctionHeader($"페이지 {pages.Count}", NewFunction);
		AddUnsavedEntries();
		foreach (FunctionData saved in pages)
		{
			string guid = saved.Guid;
			FunctionData page = saved;
			AddFunctionEntry("-", guid, WorkingNameOf(saved), $"코드 {CodeSizeOf(saved)}", () => SwitchTo(() => OpenPage(guid, () => FunctionStorage.Load(guid))),
				false, () => RequestDeletePage(page));
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
		AddFunctionEntry("G", GlobalsKey, "Global Variables", "", () => SwitchTo(OpenGlobals));
		AddFunctionHeader("슬롯", null);
		for (int slot = 0; slot < SlotLoadout.SlotCount; slot++)
		{
			FunctionData function = SlotFunction(slot);
			string key = slot < SlotKeys.Length ? SlotKeys[slot] : "";
			// 슬롯 함수는 슬롯 키(Q~F)가 이름이다 (#487, 시안 6). 번호와 데이터의 이름(slot1~slot8)은 보이지 않는다
			// 함수를 받지 못한 슬롯도 「비어 있음」 없이 키만 보인다 (누를 수는 없음)
			if (function == null)
			{
				AddFunctionEntry("", null, key, key, null);
				continue;
			}
			FunctionData slotFunction = function; // 반복 변수는 누를 때 이미 바뀌어 있으므로 지금 값을 잡아 둔다
			AddFunctionEntry("", function.Guid, key, key, () => SwitchTo(() => OpenPage(slotFunction.Guid, () => Copy(slotFunction))));
		}
		AddFunctionHeader($"페이지 {PageFunctions().Count()} / {ProgressRules.MaxPageFunctions}", NewFunction);
		AddUnsavedEntries();
		foreach (FunctionData function in PageFunctions())
		{
			FunctionData page = function;
			AddFunctionEntry("-", page.Guid, WorkingNameOf(page), $"코드 {CodeSizeOf(page)}", () => SwitchTo(() => OpenPage(page.Guid, () => Copy(page))),
				false, () => RequestDeletePage(page));
		}
		RebuildPalette();
		Redraw();
	}

	// 다른 함수(GUID가 다른)가 이미 이 이름을 쓰면 true. 저장하지 않은 작업본의 이름도 본다 (#494, 작업본 두 개가 같은 이름이 되지 않게)
	bool NameTaken(string name, string guid) => WorkingFunctions().Any(f => f.Guid != guid && f.Name == name);

	// 함수 목록에 보일 페이지 이름. 작업본에서 이름을 바꿨으면 바꾼 이름 (#494)
	string WorkingNameOf(FunctionData saved) => drafts.TryGetValue(saved.Guid, out FunctionData draft) ? draft.Name : saved.Name;

	// 작업본을 반영한 함수들: 저장된 함수 중 작업본이 있으면 작업본, 지금 연 함수는 지금 내용, 저장하지 않은 새 페이지도 들어 있다 (#494)
	List<FunctionData> WorkingFunctions()
	{
		List<FunctionData> list = savedFunctions.Select(f => drafts.TryGetValue(f.Guid, out FunctionData draft) ? draft : f).ToList();
		foreach (FunctionData draft in drafts.Values)
			if (list.All(f => f.Guid != draft.Guid)) list.Add(draft);
		if (!editingGlobals && Function != null)
		{
			int index = list.FindIndex(f => f.Guid == Function.Guid);
			if (index >= 0) list[index] = Function;
			else list.Add(Function);
		}
		return list;
	}

	// 작업본을 반영한 전역 변수: 고치는 중이면 지금 내용, 작업본이 있으면 작업본, 없으면 저장된 것 (#494)
	GlobalVariablesData WorkingGlobals() => editingGlobals ? globals : globalsDraft ?? listGlobals;

	// 겹치지 않는 이름: 새_함수, 새_함수2, 새_함수3 …
	string UniqueName(string baseName)
	{
		if (!NameTaken(baseName, null)) return baseName;
		for (int n = 2; ; n++)
			if (!NameTaken(baseName + n, null)) return baseName + n;
	}

	// 아직 저장하지 않은 새 함수도 페이지 맨 위에 보인다: 지금 연 새 함수와 작업본으로 둔 새 페이지 (#494). 작업본 줄을 누르면 그 페이지를 다시 연다
	void AddUnsavedEntries()
	{
		foreach (FunctionData page in NewPages())
		{
			string guid = page.Guid;
			bool current = !editingGlobals && Function != null && guid == Function.Guid;
			AddFunctionEntry("-", guid, page.Name, "저장 안 함", current ? () => { } : () => SwitchTo(() => OpenPage(guid, () => null)));
		}
	}

	// 저장하지 않은 새 페이지들: 작업본 중 저장된 함수에 없는 것, 그리고 지금 연 함수가 저장된 함수에 없으면 그것도
	List<FunctionData> NewPages()
	{
		List<FunctionData> pages = drafts.Values.Where(d => savedFunctions.All(f => f.Guid != d.Guid)).ToList();
		if (!editingGlobals && Function != null && savedFunctions.All(f => f.Guid != Function.Guid) && pages.All(p => p.Guid != Function.Guid))
			pages.Add(Function);
		return pages;
	}

	void ClearSavedButtons()
	{
		foreach (GameObject button in savedButtons)
		{
			if (button != null) Destroy(button);
		}
		savedButtons.Clear();
		functionItems.Clear();
		openActions.Clear();
	}

	void NewFunction()
	{
		// 저장하지 않은 새 페이지도 센다. 저장할 때 개수 초과로 거부될 페이지를 미리 만들지 않게 (#494)
		if (server != null && PageFunctions().Count() + NewPages().Count >= ProgressRules.MaxPageFunctions)
		{
			SetMessage($"함수는 {ProgressRules.MaxPageFunctions}개까지 더할 수 있음", true);
			Redraw();
			return;
		}
		SwitchTo(CreateNewPage);
	}

	// 새 페이지를 연다. 저장하기 전에도 저장하지 않은 것으로 쳐서, 다른 곳을 열면 작업본으로 두고 나가려 하면 먼저 묻는다 (사라지기 때문)
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

	// 슬롯 함수면 true: 이름(슬롯 키)과 반환 타입을 바꿀 수 없다. 임시 계정은 슬롯에 끼운 함수 (#487. 이름을 바꾸려면 장착대에서 슬롯을 비운 뒤)
	bool IsSlotFunction(FunctionData function) => SlotIndexOf(function) >= 0;

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
