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
public class BlockView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
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
	int containerBottom = 9;

	[SerializeField, Tooltip("컴파일 오류가 난 블록 색 (반복문 밖의 break 등)")]
	Color errorColor = new Color32(0xC8, 0x48, 0x58, 0xFF);

	[SerializeField, Range(0.3f, 1f), Tooltip("끌고 있는 블록의 불투명도")]
	float dragAlpha = 0.75f;

	[SerializeField, Range(0.3f, 1f), Tooltip("끌고 있는 블록의 크기 (원래 크기의 배수, #476)")]
	float dragScale = 0.6f;

	// 선언 블록에서 고를 수 있는 타입. 고를 때마다 새 VariableType을 만들도록 함수로 둔다 (블록끼리 같은 타입 객체를 나눠 쓰지 않게)
	// Sword*: 소환검 포인터를 담아 두었다가 free_sword(s)처럼 해제하기 위함. 스킬 파라미터는 반드시 채워야 하기 때문 (#474)
	// Target: get_bug()로 고른 몬스터를 담아 두고 여러 스킬에 넘기기 위함. 몬스터가 죽었는지는 is_valid_target(b)로 확인한다 (#474)
	static readonly Func<VariableType>[] DeclareTypes =
	{
		() => new VariableType(VariableTypeKind.Int),
		() => new VariableType(VariableTypeKind.Float),
		() => new VariableType(VariableTypeKind.Bool),
		() => new VariableType(VariableTypeKind.Char),
		() => new VariableType(VariableTypeKind.String),
		() => VariableType.PointerTo(VariableType.StructOf(PredefinedStructs.Sword)),
		() => VariableType.TargetOf(TargetKind.Bug),
	};

	// 배열 만들기 블록의 원소 타입. 선언과 같다 (#474). 실행기가 포인터 array를 받고, 원소의 처음 값은 NULL이다
	// 예: Sword* swords[3]; 에 소환검 여러 개를 담아 두었다가 차례로 해제한다. Target targets[3]; 은 battlefield_sweep의 targets에 넘긴다
	static readonly Func<VariableType>[] ArrayElementTypes = DeclareTypes;

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

	// 선언 블록의 처음 값: 「=」 글자, 값 칸, 펼치기·접기 버튼 글자, 지금 펼쳤는지 (#474). 선언 블록이 아니면 null
	GameObject initialEquals;
	BlockValueSlot initialValueSlot;
	TMP_Text initialToggleLabel;
	bool initialValueOpen;

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
			// 끌어 놓기 쉽게 예전(칩 5·2·3, 값 6·2, 문장 5·5·3)보다 여백을 키운다 (#476)
			rowLayout.padding = chip ? new RectOffset(7, 7, 4, 4)
				: value ? new RectOffset(8, 8, 3, 3)
				: new RectOffset(7, 8, 7, 4);
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
				AddTypeOption(0, DeclareTypes);
				AddNameField(1);
				AddInitialValue(2);
				break;
			case BlockKind.Assign:
				AddVariableOption(0, false);
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
				AddTypeOption(0, ArrayElementTypes);
				AddNameField(1);
				AddText("[");
				AddValue(2);
				AddText("];");
				break;
			case BlockKind.ArrayGet:
				AddVariableOption(0, true);
				AddText("[");
				AddValue(1);
				AddText("]");
				break;
			case BlockKind.ArraySet:
				AddVariableOption(0, true);
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

	// 선언의 처음 값 (#474): 비어 있으면 「int x; [>]」로 접어 두고, > 버튼을 누르면 「int x = [값] ; [<]」로 펼친다. < 버튼으로 접으면 넣은 값을 지운다
	// 접을 때 값을 지우는 이유: 값을 숨겨 두기만 하면 화면은 int x;인데 그 값으로 실행되어, 화면과 동작이 달라지기 때문
	// 펼쳤는지는 데이터에 저장하지 않는다. 처음 값 칸이 비었으면 접힌 것으로 보고, 함수를 다시 열면 접힌 모양으로 만든다
	void AddInitialValue(int index)
	{
		if (index >= Data.Slots.Count) return;

		TMP_Text equals = editor.CreateText(row);
		equals.text = "=";
		initialEquals = equals.gameObject;
		initialValueSlot = editor.CreateValueSlot(row);
		initialValueSlot.Bind(editor, this, index);
		AddText(";");

		Button toggle = editor.CreateButton(row);
		// 버튼 프리팹의 ▾(드롭다운 표시)는 숨긴다. 누르면 목록이 아니라 칸이 펼쳐지는 버튼이기 때문
		Transform arrow = toggle.transform.Find("Arrow");
		if (arrow != null) arrow.gameObject.SetActive(false);
		initialToggleLabel = toggle.GetComponentInChildren<TMP_Text>();
		// <를 태그의 시작으로 읽지 않게 한다
		if (initialToggleLabel != null) initialToggleLabel.richText = false;
		toggle.onClick.AddListener(() => SetInitialValueOpen(!initialValueOpen));

		SetInitialValueOpen(!IsEmpty(Data.Slots[index]));
	}

	// 처음 값 칸을 펼치거나 접는다. 접으면 칸을 비운다 (값이 있었으면 고친 것으로 친다)
	void SetInitialValueOpen(bool open)
	{
		initialValueOpen = open;
		initialEquals.SetActive(open);
		initialValueSlot.gameObject.SetActive(open);
		if (initialToggleLabel != null) initialToggleLabel.text = open ? "<" : ">";
		if (!open) initialValueSlot.Clear();
		editor.RefreshLayout();
	}

	/// <summary>
	/// 선언 블록의 처음 값 칸을 펼쳐 두고 비워 두었으면 접는다. 저장할 때 BlockEditor가 부른다 (#474).
	/// 빈 처음 값은 실행기가 기본값으로 채우므로, 저장한 함수가 화면에서도 int x;로 보이게 하기 위함이다.
	/// </summary>
	public void CollapseEmptyInitialValue()
	{
		if (initialValueSlot == null || !initialValueOpen || !IsEmpty(initialValueSlot.Slot)) return;
		SetInitialValueOpen(false);
	}

	// 값 칸에 아무것도 없으면 true (적은 값, 변수, 끼운 블록 모두 없음)
	static bool IsEmpty(SlotData slot) =>
		slot == null || (slot.Block == null && string.IsNullOrEmpty(slot.Literal) && string.IsNullOrEmpty(slot.VariableName));

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
			if (!BlockSlotText.IsIdentifier(text))
			{
				RejectText(field, slot.Name, "이름에는 영문자, 숫자, 밑줄만 쓸 수 있고 숫자로 시작할 수 없어요");
				return;
			}
			if (text == slot.Name) return; // 바꾸지 않았으면 고친 것으로 치지 않는다 (저장하지 않은 변경 표시)
			slot.Name = text;
			field.Text = slot.Name;
			RefreshTexts();
			editor.NotifyChanged();
		};
	}

	// 잘못 적은 글자는 원래 값으로 되돌리고, 글자를 잠깐 빨갛게 보이며 상태 줄에 이유를 알린다 (데이터는 바꾸지 않는다)
	void RejectText(BlockField field, string original, string reason)
	{
		field.Text = original;
		editor.ShowNotice(reason);
		StartCoroutine(FlashInvalid(field));
	}

	System.Collections.IEnumerator FlashInvalid(BlockField field)
	{
		field.SetInvalid(true);
		yield return new WaitForSeconds(1f);
		if (field != null) field.SetInvalid(false);
	}

	// 변수 칸 (대입의 왼쪽, 원소 읽기·쓰기의 array 이름): 이름을 적지 않고, 이 블록 자리에서 보이는 변수 중에서 드롭다운으로 고른다 (#474 5단계)
	// 대입은 array가 아닌 변수만, 원소 읽기·쓰기는 array 변수만 보인다. array를 통째로 대입하면 실행기가 TypeMismatch를 내기 때문
	// 목록은 누를 때마다 다시 만든다. 위에 선언을 더하거나 블록을 옮기면 보이는 변수가 바뀌기 때문
	void AddVariableOption(int index, bool arrays)
	{
		SlotData slot = Data.Slots[index];
		Button button = editor.CreateButton(row);
		var anchor = (RectTransform)button.transform;
		TMP_Text label = button.GetComponentInChildren<TMP_Text>();
		button.onClick.AddListener(() =>
		{
			var names = new List<string>();
			var labels = new List<string>();
			foreach (KeyValuePair<string, VariableType> variable in editor.VisibleVariables(Data))
			{
				bool isArray = variable.Value?.Kind == VariableTypeKind.Array;
				if (isArray != arrays) continue;
				names.Add(variable.Key);
				VariableType shown = isArray ? variable.Value.ElementType : variable.Value;
				labels.Add($"{BlockLabels.TypeName(shown)} {variable.Key}{(isArray ? "[]" : "")}");
			}
			if (names.Count == 0)
			{
				editor.ShowNotice(arrays ? "고를 수 있는 array가 없어요. 이 블록보다 위에서 Array로 먼저 만드세요"
					: "고를 수 있는 변수가 없어요. 이 블록보다 위에서 Declare로 먼저 선언하세요");
				return;
			}
			editor.ShowDropdown(anchor, labels.ToArray(), names.IndexOf(slot.VariableName), picked =>
			{
				if (names[picked] == slot.VariableName) return; // 같은 변수를 다시 고르면 고친 것으로 치지 않는다
				slot.VariableName = names[picked];
				RefreshTexts();
				editor.NotifyChanged();
			});
		});
		// 아직 고르지 않았으면 「변수」라고 보인다
		if (label != null) dynamicTexts.Add((label, () => string.IsNullOrEmpty(slot.VariableName) ? "변수" : slot.VariableName));
	}

	// 연산자 고르기 (드롭다운). 데이터에는 enum 이름("Add")을 넣는다
	void AddOperatorOption(int index, string[] options)
	{
		SlotData slot = Data.Slots[index];
		string[] labels = Array.ConvertAll(options, BlockLabels.OperatorText);
		TMP_Text label = AddDropdown(labels, () => Array.IndexOf(options, slot.Option), picked => slot.Option = options[picked]);
		dynamicTexts.Add((label, () => BlockLabels.OperatorText(slot.Option)));
	}

	// 타입 고르기 (드롭다운): types 목록 (선언은 int, float, bool, char, string, Sword*)
	// 지금 타입은 화면 글자로 찾는다. Sword*처럼 Kind만으로는 구분되지 않는 타입(가리키는 struct)도 글자는 서로 다르기 때문
	void AddTypeOption(int index, Func<VariableType>[] types)
	{
		SlotData slot = Data.Slots[index];
		string[] labels = Array.ConvertAll(types, type => BlockLabels.TypeName(type()));
		TMP_Text label = AddDropdown(labels,
			() => slot.Type == null ? -1 : Array.IndexOf(labels, BlockLabels.TypeName(slot.Type)),
			picked => slot.Type = types[picked](),
			WarnIfInitialValueDoesNotFit);
		dynamicTexts.Add((label, () => BlockLabels.TypeName(slot.Type)));
	}

	// 선언의 타입을 바꾼 뒤, 이미 넣은 처음 값이 새 타입에 맞지 않으면 칸을 빨갛게 하고 상태 줄에 알린다 (#474)
	// 값을 지우지 않는 이유: 타입을 잘못 골랐다가 되돌릴 때 플레이어가 넣은 값(끼운 블록 포함)을 잃지 않게 하기 위함. 맞지 않는 값은 저장할 때 컴파일이 막는다
	void WarnIfInitialValueDoesNotFit()
	{
		if (initialValueSlot == null) return;
		if (!initialValueSlot.HasWrongValue) return;
		editor.MarkSlotError(initialValueSlot);
		editor.ShowNotice($"처음 값이 {BlockLabels.TypeName(Data.Slots[0].Type)}에 맞지 않아요. 이 칸에는 {initialValueSlot.AcceptsText}만 넣을 수 있어요");
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

	// 누르면 고를 수 있는 것 목록이 아래에 열리는 칸. 고르면 pick을 부르고 글자를 다시 쓴다.
	// afterChange는 고친 것으로 친 뒤에 부른다 (NotifyChanged가 지우는 빨간 표시·상태 줄 안내를 그 뒤에 다시 보이기 위함)
	TMP_Text AddDropdown(string[] labels, Func<int> current, Action<int> pick, Action afterChange = null)
	{
		Button button = editor.CreateButton(row);
		var anchor = (RectTransform)button.transform;
		button.onClick.AddListener(() => editor.ShowDropdown(anchor, labels, current(), picked =>
		{
			pick(picked);
			RefreshTexts();
			editor.NotifyChanged();
			afterChange?.Invoke();
		}));
		return button.GetComponentInChildren<TMP_Text>();
	}

	// 마우스를 올리면 잠깐 뒤에 설명창 (#476): 함수 원형, 스킬 설명, 파라미터. 끄는 중에는 띄우지 않는다
	public void OnPointerEnter(PointerEventData eventData)
	{
		if (editor == null || eventData.dragging || dragging) return;
		editor.EnterHover(this, (RectTransform)transform, () => editor.TooltipOf(Data));
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		if (editor != null) editor.ExitHover(this);
	}

	public void OnBeginDrag(PointerEventData eventData)
	{
		BlockView dragged = this;
		editor.ClearHover(); // 끌기 시작하면 설명창을 지운다

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
		// 마우스를 따라오는 블록은 작게 (#476). 놓은 뒤의 실제 크기는 들어갈 자리의 반투명 미리보기가 보여 주므로, 큰 블록이 미리보기를 가리지 않게
		transform.localScale = Vector3.one * dragScale;
		// 끄는 블록이 마우스를 가리면 아래에 있는 놓을 자리를 찾을 수 없으므로 클릭을 통과시킨다
		canvasGroup.blocksRaycasts = false;
		MoveTo(eventData);
	}

	public void OnDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		MoveTo(eventData);
		editor.ShowDropPreview(this, FindDropTarget(eventData), eventData); // 들어갈 자리의 반투명 미리보기, 끼울 수 있는 값 칸 표시
	}

	public void OnEndDrag(PointerEventData eventData)
	{
		if (!dragging) return;
		dragging = false;
		// 놓을 자리는 클릭 통과를 되돌리기 전에 찾는다. 되돌린 뒤에 찾으면 마우스 아래의 이 블록 자신이 걸려,
		// 미리보기와 다른 자리(자기 자신의 블록 영역)에 들어가거나 지워지기 때문 (#474)
		Component target = FindDropTarget(eventData);
		// 들어갈 자리 번호도 미리보기를 지우기 전에 센다. 지운 뒤에는 블록들이 아직 미리보기만큼 밀려난 위치에 있어서,
		// 미리보기에 보인 자리와 다른 자리가 나올 수 있기 때문 (#476)
		int index = target is BlockList targetList ? targetList.GetInsertIndex(eventData.position, eventData.pressEventCamera) : -1;
		canvasGroup.blocksRaycasts = true;
		canvasGroup.alpha = 1f;
		transform.localScale = Vector3.one; // 끄는 동안 작게 보이던 것을 원래 크기로
		editor.ShowTrash(false);
		editor.HideDropPreview();

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
		// 원래 있던 칸이면 칸 규칙(SlotRules)과 상관없이 되돌린다. 규칙에 맞지 않는 예전 값이 끌다가 지워지지 않게
		else if (originSlot != null && originSlot.Held == null) originSlot.Accept(this);
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
			// 끄는 블록 자신과 그 안쪽(칸, 블록 영역)은 놓을 자리가 아니다. 자기 안에 들어가면 데이터가 자기 자신을 담게 되기 때문 (#474)
			if (result.gameObject.transform.IsChildOf(transform)) continue;
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
