using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 참고(Reference) 카드의 자세한 내용 한 항목 (예: 슬롯, 예시). 카드를 펼치면 위에서부터 차례로 보인다.
/// <para>
/// Items가 있으면 「- 이름: 설명」 줄들(슬롯, return 값). 없으면 Body 글자, Code(코드 색), Result(「결과: 」 뒤 글자)를 차례로 보인다. 비어 있는 것은 건너뛴다.
/// </para>
/// </summary>
public class ReferenceSection
{
	/// <summary>항목 이름 (예: 용도, 예시, 슬롯, return 값, 주의할 점, 실행)</summary>
	public string Title;

	/// <summary>항목 글. 예시에서는 「예를 들면」으로 시작하는 상황. Items가 있으면 쓰지 않는다</summary>
	public string Body;

	/// <summary>코드 (예시 코드). 카드가 코드 색으로 보인다</summary>
	public string Code;

	/// <summary>예시 코드를 실행한 결과. 카드가 「결과: 」를 붙여 보인다</summary>
	public string Result;

	/// <summary>이름 붙은 줄들. 카드가 이름을 코드 색으로 보인다. 예: (target, 몬스터 한 명입니다.)</summary>
	public List<(string Name, string Text)> Items;

	public ReferenceSection(string title, string body)
	{
		Title = title;
		Body = body;
	}

	public ReferenceSection(string title, List<(string Name, string Text)> items)
	{
		Title = title;
		Items = items;
	}

	/// <summary>예시 항목: 상황, 코드, 결과. 셋 중 빈 것은 보이지 않는다</summary>
	public static ReferenceSection Example(string situation, string code, string result) =>
		new ReferenceSection("예시", situation) { Code = code, Result = result };
}

/// <summary>
/// 참고(Reference) 창의 카드 한 장. 블록 하나가 무엇이고 어떻게 쓰는지를 담는다. 보여 주기만 하는 데이터이고 게임 규칙에는 쓰지 않는다.
/// </summary>
public class BlockReferenceEntry
{
	/// <summary>카드 제목. 블록 목록(팔레트)의 칩 이름과 같다. 예: If, Else If, thrust</summary>
	public string Name;

	/// <summary>분류 태그 (#489). 「분류 > 소분류」로 두 단계: 제어 > 조건, 변수 > 배열, 함수 > 스킬. 소분류가 없으면 분류만 (값, 함수)</summary>
	public string Category;

	/// <summary>분류 태그의 앞 단계 (제어, 변수, 값, 함수). 참고 탭이 이 분류마다 머리글을 하나 둔다. 문법 탭의 분류와 같다</summary>
	public string Group;

	/// <summary>분류 태그의 색을 정할 때 쓰는 견본 블록 (BlockLabels.ColorOf). 편집 영역에 놓지 않는다</summary>
	public BlockData Sample;

	/// <summary>하는 일 한 문장 (펼친 카드의 맨 위). 설명 표(ItemDescriptions)의 Summary</summary>
	public string Summary;

	/// <summary>형식 (접힌 카드에 코드로 보인다). 문법 블록은 if (condition) { body }, 함수는 원형 void thrust(Target target);</summary>
	public string CodeText;

	/// <summary>카드를 펼치면 보이는 자세한 내용. 위에서부터 차례로 보인다</summary>
	public List<ReferenceSection> Sections = new List<ReferenceSection>();

	/// <summary>사전 정의 함수의 고정 ID (c.thrust). 문법 블록이면 null</summary>
	public string FunctionId;

	/// <summary>한글 이름 (꼬챙이, 버그 찾기, 메모리 할당). 있으면 카드 제목은 이것만 보이고, 검색에도 쓴다. 문법 블록이면 null (#489)</summary>
	public string KoreanName;

	/// <summary>설명 표·가진 개수의 key (#544). 문법 블록은 If·For 같은 항목 ID, 사전 정의 함수는 고정 ID(c.thrust), type은 type.float</summary>
	public string Key;

	/// <summary>설명 카드의 「블록 사용 예」 그림으로 그릴 블록 (#544). 대상(Target)을 받는 칸에는 get_bug()를 끼워 둔다. type이면 null</summary>
	public BlockData Picture;

	/// <summary>type 카드면 그 값의 모양 (0.5, 'a', true). 설명 카드의 그림 칸에 값 알약으로 보인다 (#544). 블록이면 null</summary>
	public string TypeLiteral;

	/// <summary>어느 탭에서 꺼내는지 (#544 설명 카드의 「사용 방법」): 「함수 탭」, 「블록 탭」</summary>
	public string UsageTab;

	/// <summary>사용 방법의 나머지 글 (#544). 예: 「에서 코드로 끌어다 놓기」</summary>
	public string UsageText;
}

/// <summary>
/// 참고(Reference) 창의 카드와 설명창을 설명 표(ItemDescriptions)에서 만든다. 설명 문장은 이 파일에 적지 않는다.
/// <para>
/// - 문법 블록(제어, 변수, 값, return)과 malloc·free·wait: 이 파일의 표에는 이름, 분류, 견본 블록만 적고, 형식과 설명은 ItemDescriptions.Of(견본 블록)에서 읽는다.
///   malloc·free·wait 카드는 함수 탭과 같이 함수 카드 모양(소문자 이름, 한글 이름, 원형)으로 보인다 (#489).<br/>
/// - 분류 태그는 「분류 > 소분류」 두 단계 (제어 > 조건, 함수 > 스킬). 참고 탭은 앞 단계(Group)마다 머리글을 둔다 (#489).<br/>
/// - 함수 블록(C 스킬, 공통 이동, 입력): 함수 탭에 나오는 사전 정의 함수(c., common., input., game.)마다 한 장.
///   설명은 ItemDescriptions.Get(고정 ID)에서 읽고, 원형·파라미터·동작 시간·쿨타임은 사전 정의 함수의 값에서 만든다.<br/>
/// - 카드를 펼친 내용의 순서는 해금 설명 카드와 같다: 하는 일 → 용도 → 예시 → 슬롯 → (return 값) → 주의할 점 → (실행).<br/>
/// - 블록 목록(BlockEditor.PaletteGroups)과 따로 적은 표이므로, 블록 목록에 블록을 더하면 이 표와 ItemDescriptions에도 더한다.
/// </para>
/// </summary>
public static class BlockReference
{
	// 블록 목록에 나오는 사전 정의 함수의 고정 ID 앞부분 (BlockEditor.PaletteFunctionPrefixes와 같다)
	static readonly string[] FunctionPrefixes = { "c.", "py.", "common.", "input.", "game." };

	static List<BlockReferenceEntry> entries;
	// entries를 만든 때의 언어 방식 (#532). 내 캐릭터 언어가 바뀌면(로그인한 캐릭터가 바뀜) 다시 만든다
	static LanguageFamily entriesFamily;

	// Play 모드에 들어갈 때 도메인 다시 불러오기를 꺼 두었으므로, 스킬 트리 설명을 고친 뒤에도 지난 값이 남지 않게 static 값을 직접 비운다
	[UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => entries = null;

	/// <summary>
	/// 카드 전체. 문법 블록 다음에 함수 블록 순서. 처음 부를 때 한 번 만든다.
	/// </summary>
	public static IReadOnlyList<BlockReferenceEntry> All
	{
		get
		{
			if (entries == null || entriesFamily != BlockLabels.Family)
			{
				entriesFamily = BlockLabels.Family;
				entries = Build();
			}
			return entries;
		}
	}

	/// <summary>
	/// 검색한 글자가 이름, 분류 태그(제어 > 조건), 한글 이름, 고정 ID에 들어 있는 카드를 돌려준다 (대소문자 구분 없음).
	/// 비어 있으면 전체. 예: "if" → If, Else If, "조건" → If, Else If, Else, "꼬챙" → skewer
	/// </summary>
	public static List<BlockReferenceEntry> Search(string query)
	{
		query = query?.Trim() ?? "";
		if (query.Length == 0) return All.ToList();
		return All.Where(e => Contains(e.Name, query) || Contains(e.Category, query)
			|| Contains(e.KoreanName, query) || Contains(e.FunctionId, query)).ToList();
	}

	// 분류 태그의 두 단계 사이 글자 (#489). 「제어 > 조건」: 조건이 제어 안에 속한다는 뜻. 「·」는 Galmuri11 글꼴에 없어 빈칸으로 보이므로 쓰지 않는다
	const string CategorySeparator = " > ";

	static string Category(string group, string subgroup) => group + CategorySeparator + subgroup;

	static bool Contains(string text, string query) =>
		text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

	// 문법 블록 ---------------------------------------------------------------
	// (이름, 분류, 견본 블록을 만드는 함수). 형식·설명은 견본 블록의 key로 ItemDescriptions에서 읽는다
	static readonly (string Name, string Category, Func<BlockData> Sample)[] SyntaxRows =
	{
		("If", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.If)),
		("Else If", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.ElseIf)),
		("Else", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.Else)),
		("For", Category("제어", "반복"), () => BlockData.For("i", Int("0"), Int("10"))),
		("While", Category("제어", "반복"), () => BlockData.While(SlotData.EmptyValue())),
		("Break", Category("제어", "반복"), () => BlockData.Break()),
		("Continue", Category("제어", "반복"), () => BlockData.Continue()),

		("Declare", Category("변수", "변수"), () => BlockData.Declare(new VariableType(VariableTypeKind.Int), "x")),
		("Assign", Category("변수", "변수"), () => BlockData.Assign("x", SlotData.EmptyValue())),
		("Array", Category("변수", "배열"), () => BlockData.ArrayDeclare(new VariableType(VariableTypeKind.Int), "arr", Int("3"))),
		("Array Get", Category("변수", "배열"), () => BlockData.ArrayGet("arr", SlotData.EmptyValue())),
		("Array Set", Category("변수", "배열"), () => BlockData.ArraySet("arr", SlotData.EmptyValue(), SlotData.EmptyValue())),

		("Arithmetic", "값", () => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue())),
		("Compare", "값", () => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue())),
		("Logic", "값", () => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue())),
		("Not", "값", () => BlockData.Not(SlotData.EmptyValue())),

		("Malloc", Category("함수", "메모리"), () => BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword))),
		("Free", Category("함수", "메모리"), () => BlockData.Free(SlotData.EmptyValue())),

		("Return", "함수", () => BlockData.Return()),
		("Wait", Category("함수", "기본 함수"), () => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5"))),
	};

	// Python 캐릭터의 문법 카드 (#532). 블록 목록(BlockEditor.PythonPaletteGroups)과 같은 블록
	static readonly (string Name, string Category, Func<BlockData> Sample)[] PythonSyntaxRows =
	{
		("If", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.If)),
		("Elif", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.ElseIf)),
		("Else", Category("제어", "조건"), () => BlockData.Conditional(ConditionalBranch.Else)),
		("For", Category("제어", "반복"), () => BlockData.For("i", Int("0"), Int("10"))),
		("While", Category("제어", "반복"), () => BlockData.While(SlotData.EmptyValue())),
		("Break", Category("제어", "반복"), () => BlockData.Break()),
		("Continue", Category("제어", "반복"), () => BlockData.Continue()),

		("Assign", Category("변수", "변수"), () => BlockData.Assign("x", SlotData.EmptyValue())),
		("Global", Category("변수", "변수"), () => BlockData.Global("x")),
		("Del", Category("변수", "변수"), () => BlockData.Del("x")),
		("List", Category("변수", "리스트"), () => BlockData.ListNew("items")),
		("Append", Category("변수", "리스트"), () => BlockData.ListAppend("items", SlotData.EmptyValue())),
		("Pop", Category("변수", "리스트"), () => BlockData.ListPop("items")),
		("Len", Category("변수", "리스트"), () => BlockData.ListLen(SlotData.EmptyValue())),
		("List Get", Category("변수", "리스트"), () => BlockData.ArrayGet("items", SlotData.EmptyValue())),
		("List Set", Category("변수", "리스트"), () => BlockData.ArraySet("items", SlotData.EmptyValue(), SlotData.EmptyValue())),

		("Arithmetic", "값", () => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue())),
		("Compare", "값", () => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue())),
		("Logic", "값", () => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue())),
		("Not", "값", () => BlockData.Not(SlotData.EmptyValue())),

		("Return", "함수", () => BlockData.Return()),
		("Wait", Category("함수", "기본 함수"), () => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5"))),
	};

	// 언어 방식의 문법 카드 표 (#532)
	static (string Name, string Category, Func<BlockData> Sample)[] SyntaxRowsOf(LanguageFamily family) => family switch
	{
		LanguageFamily.Python => PythonSyntaxRows,
		_ => SyntaxRows,
	};

	// 카드 순서: 문법 블록(제어, 변수, 값, return) → 함수 탭과 같은 순서의 함수 카드(스킬, 기본 함수, 메모리) (#489)
	static List<BlockReferenceEntry> Build()
	{
		var list = new List<BlockReferenceEntry>();
		var skills = new List<BlockReferenceEntry>();
		var basics = new List<BlockReferenceEntry>();
		var builtinBasics = new List<BlockReferenceEntry>(); // wait: 기본 함수 중 get_bug, is_valid_target 뒤 (함수 탭과 같은 순서)
		var memory = new List<BlockReferenceEntry>();

		foreach ((string name, string category, Func<BlockData> sample) in SyntaxRowsOf(BlockLabels.Family))
		{
			BlockData block = sample();
			// 접힌 카드는 형식, 펼치면 해금 설명 카드와 같은 순서의 칸 (설명 표 ItemDescriptions)
			ItemDescription description = ItemDescriptions.Of(block);
			var entry = new BlockReferenceEntry
			{
				Name = name,
				Category = category,
				Sample = block,
				Summary = description?.Summary,
				CodeText = string.IsNullOrEmpty(description?.Format) ? name : description.Format,
				Key = BlockUsageCounter.KeyOf(block),
				Picture = block,
				UsageTab = "블록 탭",
				UsageText = "에서 코드로 끌어다 놓기",
			};
			List<(string, string)> returned = string.IsNullOrEmpty(description?.Returns) ? null
				: new List<(string, string)> { (description.Returns, "") };
			// malloc, free, wait는 함수 탭에서 함수처럼 꺼내므로 카드도 함수 카드처럼: 소문자 이름, 한글 이름, 원형 (#489)
			// 나머지 칸은 문법 블록과 같이 설명 표의 것을 그대로 쓰고, 슬롯 칸 제목만 함수 카드처럼 「파라미터」
			var builtin = BuiltinOf(block.Kind);
			AddDescriptionSections(entry, description, builtin != null ? ParameterTitle : SlotTitle, description?.SlotItems(), returned, null);

			if (builtin != null)
			{
				entry.Name = builtin.Value.Name;
				entry.KoreanName = builtin.Value.KoreanName;
				entry.CodeText = builtin.Value.Prototype;
				entry.UsageTab = "함수 탭"; // malloc·free·wait는 함수 탭에서 꺼낸다 (#489)
				(block.Kind == BlockKind.Wait ? builtinBasics : memory).Add(entry);
				continue;
			}
			list.Add(entry);
		}

		IReadOnlyList<PredefinedFunction> functions = PredefinedFunctionRegistry.All;
		if (functions != null)
		{
			foreach (PredefinedFunction function in functions)
			{
				string id = function?.Id;
				if (id == null || !FunctionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))) continue;
				// 내 캐릭터 언어가 쓸 수 없는 스킬(C 캐릭터의 py.*, 파이썬 캐릭터의 c.*)은 빼고 보여 준다
				if (!PlayerCharacter.CanUse(PlayerCharacter.LocalLanguage, id)) continue;
				BlockReferenceEntry entry = FunctionEntry(function);
				(IsSkill(id) ? skills : basics).Add(entry);
			}
		}

		list.AddRange(skills);
		list.AddRange(basics);
		list.AddRange(builtinBasics);
		list.AddRange(memory);
		list.AddRange(TypeEntries());
		// 머리글을 나눌 분류: 분류 태그의 앞 단계 (「함수 > 스킬」 → 함수)
		foreach (BlockReferenceEntry entry in list)
		{
			int split = entry.Category.IndexOf(CategorySeparator, StringComparison.Ordinal);
			entry.Group = split < 0 ? entry.Category : entry.Category.Substring(0, split);
		}
		return list;
	}

	// 설명 카드 (#544) ---------------------------------------------------------------

	/// <summary>
	/// key(설명 표·가진 개수의 key: If, c.thrust, type.float)의 카드. 내 캐릭터 언어에 없으면 null.
	/// </summary>
	public static BlockReferenceEntry EntryOf(string key)
	{
		if (string.IsNullOrEmpty(key)) return null;
		foreach (BlockReferenceEntry entry in All)
			if (entry.Key == key) return entry;
		return null;
	}

	/// <summary>
	/// 블록 하나의 카드 (블록 목록의 칩, 함수 탭 항목을 눌렀을 때). 내가 만든 함수를 부르는 블록은 null.
	/// </summary>
	public static BlockReferenceEntry EntryOf(BlockData block) => block == null ? null : EntryOf(BlockUsageCounter.KeyOf(block));

	/// <summary>
	/// 스킬 트리 노드를 해금하면 쓸 수 있게 되는 것들의 카드 (#544). 해금 직후 카드와 스킬 트리 설명 칸이 보인다.
	/// <para>
	/// - 스킬 노드: 그 스킬 함수. 검 소환처럼 블록도 여는 노드는 그 블록(malloc, free)도 뒤에 붙인다.<br/>
	/// - 문법 노드: 여는 블록 종류(UnlocksBlocks)의 카드. 조건문은 If·Else If·Else 셋. 함수 블록 종류(Function)는 스킬마다 따로 열리므로 빼고, return만.<br/>
	/// - 순서는 참고 탭과 같다. 내 캐릭터 언어에 없는 블록은 빠진다 (Python의 선언 등).
	/// </para>
	/// </summary>
	public static List<BlockReferenceEntry> EntriesUnlockedBy(SkillTreeNode node)
	{
		var result = new List<BlockReferenceEntry>();
		if (node == null) return result;
		if (node.Kind == SkillTreeNodeKind.Skill)
		{
			BlockReferenceEntry skill = EntryOf(node.Id);
			if (skill != null) result.Add(skill);
		}
		foreach (BlockReferenceEntry entry in All)
		{
			if (entry.FunctionId != null || entry.Picture == null || entry.Picture.Kind == BlockKind.Function) continue;
			if (node.UnlocksBlocks.Contains(entry.Picture.Kind) && !result.Contains(entry)) result.Add(entry);
		}
		return result;
	}

	// type 카드 (#544): 설명 표의 type 다섯 개. C 캐릭터만 (Python은 type을 적지 않는다)
	static readonly string[] TypeKeys = { "type.float", "type.char", "type.bool", "type.string", "type.NULL" };

	static IEnumerable<BlockReferenceEntry> TypeEntries()
	{
		if (BlockLabels.Family == LanguageFamily.Python) yield break;
		foreach (string key in TypeKeys)
		{
			ItemDescription description = ItemDescriptions.Get(key);
			if (description == null) continue;
			string name = key.Substring("type.".Length);
			var entry = new BlockReferenceEntry
			{
				Name = name,
				Category = "type",
				Summary = description.Summary,
				CodeText = name,
				Key = key,
				TypeLiteral = description.Format,
				UsageTab = "블록 탭",
				UsageText = name == "NULL" ? "의 값 칸에 NULL을 적어 넣기" : $"의 선언 블록에서 {name}을 고르거나, 값 칸에 {description.Format}처럼 적기",
			};
			AddDescriptionSections(entry, description, "쓰는 곳", description.SlotItems(), null, null);
			yield return entry;
		}
	}

	// 함수 카드 그림 (#544): 대상(target)을 받는 칸에는 get_bug()를 끼운다. 시안 9-3 「skewer ( get_bug ( ) );」
	static BlockData PictureOf(string id, IReadOnlyList<ParameterData> parameters)
	{
		var slots = new SlotData[parameters.Count];
		for (int i = 0; i < slots.Length; i++)
		{
			bool target = parameters[i].Name == "target";
			slots[i] = target ? SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug, new SlotData[0])) : SlotData.EmptyValue();
		}
		return BlockData.CallPredefinedFunction(id, slots);
	}

	// 함수 탭의 「스킬」 구역에 들어가는 함수: C 스킬, Python 스킬(#486), 공통 이동(dash 등). 나머지(get_bug, is_valid_target)는 기본 함수
	// BlockEditor.RebuildFunctionTab도 이 함수로 나눈다 (두 곳이 어긋나지 않게)
	// py.is_valid_orb는 py.로 시작하지만 스킬이 아니라 기본 함수다 (#532, 이우림). is_valid_target과 같은 구역에 둔다
	// 출력 함수(c.printf, py.print, #536)도 c.·py.로 시작하지만 기본 함수다
	// 자세 함수(c.init_stance, c.get_stance #538, py.init_stance, py.get_stance #546)도 기본 함수다
	internal static bool IsSkill(string id) =>
		id != PredefinedFunctionIds.PyIsValidOrb
		&& id != PredefinedFunctionIds.Printf && id != PredefinedFunctionIds.PyPrint
		&& id != PredefinedFunctionIds.InitStance && id != PredefinedFunctionIds.GetStance
		&& id != PredefinedFunctionIds.PyInitStance && id != PredefinedFunctionIds.PyGetStance
		&& (id.StartsWith("c.", StringComparison.Ordinal) || id.StartsWith("py.", StringComparison.Ordinal) || id.StartsWith("common.", StringComparison.Ordinal));

	static BlockReferenceEntry FunctionEntry(PredefinedFunction function)
	{
		string id = function.Id;
		string name = BlockLabels.PredefinedFunctionName(id);
		IReadOnlyList<ParameterData> parameters = function.Parameters ?? (IReadOnlyList<ParameterData>)new List<ParameterData>();
		bool returnsValue = function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void;

		// 설명은 설명 표에서 읽는다. 한글 이름(검 찌르기)은 카드 제목에 보이므로 설명 앞에 붙이지 않는다 (#489)
		ItemDescription description = ItemDescriptions.Get(id);

		// 견본 블록: 편집 화면의 함수 블록 견본과 같이 파라미터 수만큼 빈 값 칸을 둔다
		SlotData[] slots = new SlotData[parameters.Count];
		for (int i = 0; i < slots.Length; i++) slots[i] = SlotData.EmptyValue();
		BlockData sample = BlockData.CallPredefinedFunction(id, slots);

		// 접힌 카드는 원형, 펼치면 해금 설명 카드와 같은 순서의 칸 뒤에 실행(동작·쿨타임 틱, 코드 크기)
		var entry = new BlockReferenceEntry
		{
			Name = name,
			Category = Category("함수", IsSkill(id) ? "스킬" : "기본 함수"),
			Sample = sample,
			Summary = description?.Summary,
			CodeText = Prototype(function.ReturnType, name, parameters),
			FunctionId = id,
			KoreanName = KoreanNameOf(id),
			Key = id,
			Picture = PictureOf(id, parameters),
			UsageTab = "함수 탭",
			UsageText = "에서 코드로 끌어다 놓기",
		};

		List<(string, string)> returned = returnsValue
			? new List<(string, string)> { (TypeText(function.ReturnType), description?.Returns ?? "") }
			: null;
		AddDescriptionSections(entry, description, ParameterTitle, SlotItemsOf(description, parameters), returned, ExampleOf(name, parameters));
		entry.Sections.Add(new ReferenceSection("실행", CostOf(function)));
		return entry;
	}

	// 설명 표의 칸을 카드의 자세한 내용으로 넣는다. 순서는 해금 설명 카드와 같다: 용도 → 예시 → 슬롯 → return 값 → 주의할 점.
	// 하는 일(Summary)은 카드가 맨 위에 따로 보인다. 비어 있는 칸은 넣지 않는다.
	// autoExample: 설명 표에 예시가 없을 때 대신 보일 호출 한 줄 (스킬 thrust(get_bug());). 박진우가 예시를 채우기 전까지 지금처럼 보이게
	// slotTitle: 슬롯 칸의 제목. 함수 카드(사전 정의 함수, malloc·free·wait)는 C 용어 그대로 「파라미터」, 문법 블록은 「슬롯」
	// (If의 condition·body는 C에서 parameter가 아니므로 이름을 나눈다. 11/17 이우림)
	static void AddDescriptionSections(BlockReferenceEntry entry, ItemDescription description, string slotTitle,
		List<(string, string)> slots, List<(string, string)> returned, string autoExample)
	{
		if (!string.IsNullOrEmpty(description?.Purpose)) entry.Sections.Add(new ReferenceSection("용도", description.Purpose));

		bool hasExample = !string.IsNullOrEmpty(description?.Example) || !string.IsNullOrEmpty(description?.ExampleCode);
		if (hasExample) entry.Sections.Add(ReferenceSection.Example(description.Example, description.ExampleCode, description.ExampleResult));
		else if (autoExample != null) entry.Sections.Add(ReferenceSection.Example(null, autoExample, null));

		if (slots != null && slots.Count > 0) entry.Sections.Add(new ReferenceSection(slotTitle, slots));
		if (returned != null) entry.Sections.Add(new ReferenceSection("return 값", returned));
		if (!string.IsNullOrEmpty(description?.Caution)) entry.Sections.Add(new ReferenceSection("주의할 점", description.Caution));
	}

	// 카드의 슬롯 칸 제목 (AddDescriptionSections)
	const string SlotTitle = "슬롯";
	const string ParameterTitle = "파라미터";

	// 사전 정의 함수의 슬롯 줄: 설명 표에 적었으면 그것, 비어 있으면 파라미터마다 (이름, 이름의 뜻)
	static List<(string, string)> SlotItemsOf(ItemDescription description, IReadOnlyList<ParameterData> parameters)
	{
		if (description?.Slots != null && description.Slots.Length > 0) return description.SlotItems();
		return parameters.Select(p => (p.Name, ItemDescriptions.ParameterMeaningOf(p.Name))).ToList();
	}

	// 값 칸 하나(파라미터 name)에 무엇을 넣는지: 설명 표의 슬롯 줄, 없으면 이름의 뜻
	static string SlotMeaningOf(ItemDescription description, string name)
	{
		string text = description?.SlotTextOf(name);
		return string.IsNullOrEmpty(text) ? ItemDescriptions.ParameterMeaningOf(name) : text;
	}

	// 동작 시간, 쿨타임, 코드 크기, 비동기 여부. 사전 정의 함수의 값을 그대로 읽는다 (틱 하나의 시간은 캐릭터마다 달라 틱 수로 적는다)
	static string CostOf(PredefinedFunction function)
	{
		var parts = new List<string> { $"동작 {function.TickCount}틱" };
		if (function.CooldownTicks > 0) parts.Add($"쿨타임 {function.CooldownTicks}틱");
		parts.Add($"코드 크기 {function.CodeSize}");
		if (function.IsAsync) parts.Add("비동기(불러도 끝나기를 기다리지 않음)");
		return string.Join(", ", parts);
	}

	// 호출 예. 대상은 get_bug(), 소환검은 summon_sword()를 바로 끼워 쓰는 모양으로 적는다 (변수에 담지 않아도 되는 형태).
	// 대상 array(targets)를 받는 함수는 블록 하나로 만들 수 없어 예시를 적지 않는다
	static string ExampleOf(string name, IReadOnlyList<ParameterData> parameters)
	{
		var args = new List<string>();
		foreach (ParameterData parameter in parameters)
		{
			switch (parameter.Name)
			{
				case "target": args.Add("get_bug()"); break;
				// Python 구슬 스킬(#532): 구슬을 담아 둔 variable
				case "orb": args.Add("orb"); break;
				case "sword": args.Add("summon_sword()"); break;
				default: return null;
			}
		}
		return BlockLabels.IsPython ? $"{name}({string.Join(", ", args)})" : $"{name}({string.Join(", ", args)});";
	}

	// 설명창 (#476) ------------------------------------------------------------
	// 블록·값 칸에 마우스를 올리면 보이는 짧은 설명. 참고 카드와 같은 설명 표(ItemDescriptions)를 읽는다

	// 설명창 글자 색: 문법 블록 형식(주황, 참고 카드의 형식 줄과 같음), 스킬 이름(참고 탭의 강조색), return 값(민트), 줄 이름(어두운 회색 파랑)
	// 함수 원형과 문법 형식은 낱말마다 색을 나눈다 (ColorCode, #489, VS Code Dark+). 파라미터 이름은 원형과 같은 하늘색(ParameterColor)
	const string TooltipAccent = "#8B9CFF";
	const string TooltipParameter = "#7ED6C0";
	const string TooltipLabel = "#7C84A8";

	// 설명과 파라미터 줄 사이의 작은 빈 줄. 한 줄을 통째로 비우면 너무 벌어져서 글자 크기의 반만 띄운다
	const string TooltipGap = "<size=50%> </size>";

	/// <summary>
	/// 블록에 마우스를 올렸을 때 보일 설명 (리치 텍스트). 없으면 null.
	/// <para>
	/// - 사전 정의 함수(C 스킬 등): 함수 원형(void thrust(Target target);), 스킬의 한국어 이름과 설명, 파라미터마다 한 줄, return 값<br/>
	/// - 내가 만든 함수(userFunction): 함수 원형<br/>
	/// - 문법 블록: 형식, 설명 한 문장. paletteName은 블록 목록의 칩 이름(If, Array Get ...)이고 참고 카드의 이름과 같다
	/// </para>
	/// </summary>
	public static string TooltipOf(BlockData block, string paletteName, FunctionData userFunction)
	{
		if (block == null) return null;
		if (block.Kind == BlockKind.Function)
		{
			if (block.IsUserFunction)
				return userFunction == null ? null : string.Join("\n", ColorCode(Prototype(userFunction.ReturnType, userFunction.Name, userFunction.Parameters)), "내가 만든 함수입니다.");
			return PredefinedTooltip(block.FunctionId);
		}

		// 문법 블록은 형식과 하는 일 한 문장만 짧게 보인다. 슬롯까지 보이면 설명창이 길어져 편집 영역을 가리므로, 자세한 것은 참고 탭에 맡긴다
		ItemDescription description = ItemDescriptions.Of(block);
		if (description == null) return null;
		var lines = new List<string>();
		// 형식(if (condition) { body }, malloc(type))은 VS Code처럼 낱말마다 색을 나눈다 (#489). 형식이 없으면 칩 이름
		lines.Add(ColorCode(string.IsNullOrEmpty(description.Format) ? paletteName : description.Format));
		if (!string.IsNullOrEmpty(description.Summary)) lines.Add($"<noparse>{description.Summary}</noparse>");
		return string.Join("\n", lines);
	}

	/// <summary>
	/// 값 칸에 마우스를 올렸을 때 보일 설명 (리치 텍스트). 함수 블록의 파라미터면 「타입 이름」과 무엇을 넣는지, 그리고 이 칸에 넣을 수 있는 것(accepts, SlotRules.Describe).
	/// userFunction은 owner가 내가 만든 함수를 부르는 블록일 때 그 함수 (파라미터 이름·타입을 읽는다).
	/// </summary>
	public static string SlotTooltipOf(BlockData owner, int index, string accepts, FunctionData userFunction)
	{
		var lines = new List<string>();
		if (owner != null && owner.Kind == BlockKind.Function)
		{
			ParameterData parameter = null;
			ItemDescription description = null; // 사전 정의 함수의 설명 (슬롯 줄). 내가 만든 함수는 설명이 없다
			if (owner.IsUserFunction)
			{
				List<ParameterData> parameters = userFunction?.Parameters;
				if (parameters != null && index < parameters.Count) parameter = parameters[index];
			}
			else if (PredefinedFunctionRegistry.TryGet(owner.FunctionId, out PredefinedFunction function) && function.Parameters != null && index < function.Parameters.Count)
			{
				parameter = function.Parameters[index];
				description = ItemDescriptions.Get(owner.FunctionId);
			}

			if (parameter != null)
			{
				lines.Add(ColorCode($"{TypeText(parameter.Type)} {parameter.Name}")); // 원형과 같은 색 (#489)
				string meaning = SlotMeaningOf(description, parameter.Name);
				if (!string.IsNullOrEmpty(meaning)) lines.Add($"<noparse>{meaning}</noparse>");
			}
		}
		lines.Add($"<color={TooltipLabel}>넣을 수 있는 것</color>  <noparse>{accepts}</noparse>");
		return string.Join("\n", lines);
	}

	// 사전 정의 함수의 설명창: 원형, 스킬 이름·설명, 파라미터, return 값
	// 줄 앞 표시는 「-」: Galmuri11 글꼴(정적 아틀라스)에 「·」「•」「—」가 없어 빈칸으로 보이기 때문
	static string PredefinedTooltip(string id)
	{
		if (string.IsNullOrEmpty(id) || !PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) return null;
		string name = BlockLabels.PredefinedFunctionName(id);
		IReadOnlyList<ParameterData> parameters = function.Parameters ?? (IReadOnlyList<ParameterData>)new List<ParameterData>();
		var lines = new List<string> { ColorCode(Prototype(function.ReturnType, name, parameters)) };

		// 한글 이름 (검 찌르기, 버그 찾기). 함수 탭·참고 카드와 같은 이름 (#489). 없으면 적지 않는다
		string koreanName = KoreanNameOf(id);
		if (!string.IsNullOrEmpty(koreanName)) lines.Add($"<color={TooltipAccent}>{koreanName}</color>");

		// 하는 일: 설명 표에서 읽는다 (참고 카드와 같다)
		ItemDescription description = ItemDescriptions.Get(id);
		if (!string.IsNullOrEmpty(description?.Summary)) lines.Add($"<noparse>{description.Summary}</noparse>");

		var details = new List<string>();
		foreach (ParameterData parameter in parameters)
			details.Add($"- {Paint(parameter.Name, ParameterColor)}  <noparse>{SlotMeaningOf(description, parameter.Name)}</noparse>");
		if (function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void && !string.IsNullOrEmpty(description?.Returns))
			details.Add($"- <color={TooltipParameter}>return 값</color>  <noparse>{description.Returns}</noparse>");
		if (details.Count > 0)
		{
			lines.Add(TooltipGap);
			lines.AddRange(details);
		}
		return string.Join("\n", lines);
	}

	// 함수 탭 (#489) ---------------------------------------------------------
	// 기본 함수의 한글 이름 (11/15 정함). 스킬은 스킬 표의 이름(PredefinedFunction.Name)을 쓰므로 적지 않는다.
	// get_bug, is_valid_target의 Name은 클래스 이름(GetBug)이라 화면에 그대로 보일 수 없기 때문
	static readonly Dictionary<string, string> KoreanNames = new Dictionary<string, string>
	{
		[PredefinedFunctionIds.GetBug] = "버그 찾기",
		[PredefinedFunctionIds.IsValidTarget] = "대상 확인",
		[PredefinedFunctionIds.PyIsValidOrb] = "구슬 확인",
		[PredefinedFunctionIds.Printf] = "출력",
		[PredefinedFunctionIds.PyPrint] = "출력",
		[PredefinedFunctionIds.InitStance] = "자세 정하기",
		[PredefinedFunctionIds.GetStance] = "자세 확인",
		[PredefinedFunctionIds.PyInitStance] = "자세 정하기",
		[PredefinedFunctionIds.PyGetStance] = "자세 확인",
	};

	/// <summary>
	/// 사전 정의 함수의 한글 이름. 함수 탭 항목과 참고 카드에 쓴다. 예: c.skewer → 꼬챙이, input.get_bug → 버그 찾기. 없으면 빈 글자.
	/// </summary>
	public static string KoreanNameOf(string functionId)
	{
		if (string.IsNullOrEmpty(functionId)) return "";
		if (KoreanNames.TryGetValue(functionId, out string fixedName)) return fixedName;
		if (!PredefinedFunctionRegistry.TryGet(functionId, out PredefinedFunction function)) return "";
		// 화면 이름이 따로 없으면(고정 ID나 블록에 보이는 이름과 같으면) 한글 이름이 없는 것으로 본다
		string name = function.Name;
		return string.IsNullOrEmpty(name) || name == functionId || name == BlockLabels.PredefinedFunctionName(functionId) ? "" : name;
	}

	/// <summary>
	/// 사전 정의 함수의 C 원형. 예: void thrust(Target target); 없는 함수면 빈 글자.
	/// </summary>
	public static string PrototypeOf(string functionId)
	{
		if (string.IsNullOrEmpty(functionId) || !PredefinedFunctionRegistry.TryGet(functionId, out PredefinedFunction function)) return "";
		return Prototype(function.ReturnType, BlockLabels.PredefinedFunctionName(functionId), function.Parameters);
	}

	/// <summary>
	/// 내가 만든 함수의 C 원형. 예: int count_bugs();
	/// </summary>
	public static string PrototypeOf(FunctionData function) =>
		function == null ? "" : Prototype(function.ReturnType, function.Name, function.Parameters);

	/// <summary>
	/// 함수 탭에서 함수처럼 보이는 블록(malloc, free, wait)의 이름, 한글 이름, 원형 (#489, 시안 6-12). 그 밖의 블록이면 null.
	/// 사전 정의 함수가 아니라 각자의 블록(BlockKind.Malloc 등)이어서 이름과 원형을 여기에 적는다.
	/// </summary>
	public static (string Name, string KoreanName, string Prototype)? BuiltinOf(BlockKind kind) => kind switch
	{
		BlockKind.Malloc => ("malloc", "메모리 할당", "Sword* malloc(Sword);"),
		// 파라미터 이름은 설명 표의 슬롯 이름(pointer, seconds)과 같게 둔다. 카드의 원형과 슬롯 줄이 같은 이름을 가리키게
		BlockKind.Free => ("free", "메모리 해제", "void free(Sword* pointer);"),
		BlockKind.Wait => ("wait", "기다리기", BlockLabels.IsPython ? "def wait(seconds)" : "void wait(float seconds);"),
		_ => null,
	};

	// C 선언 모양의 함수 원형. 예: void thrust(Target target);
	// Python 캐릭터(#532)는 「def orb_shot(target, orb)」처럼 타입 없이 보인다
	static string Prototype(VariableType returnType, string name, IReadOnlyList<ParameterData> parameters)
	{
		if (BlockLabels.IsPython) return $"def {name}({(parameters == null ? "" : string.Join(", ", parameters.Select(p => p.Name)))})";
		string returned = returnType == null || returnType.Kind == VariableTypeKind.Void ? "void" : TypeText(returnType);
		string args = parameters == null ? "" : string.Join(", ", parameters.Select(p => $"{TypeText(p.Type)} {p.Name}"));
		return $"{returned} {name}({args});";
	}

	// C 코드 글자의 색 (#489, VS Code 어두운 테마 Dark+ 기준). 낱말마다 색을 나눠 키워드·타입·함수 이름·변수를 구분하기 쉽게 한다
	const string KeywordColor = "#C586C0";       // if, else, for, while, break, continue, return
	const string PrimitiveTypeColor = "#569CD6"; // void, int, float, bool, char, string, array, true, false, NULL
	const string GameTypeColor = "#4EC9B0";      // Target, Sword (대문자로 시작하는 게임 타입)
	const string FunctionNameColor = "#DCDCAA";  // skewer, get_bug (뒤에 괄호가 오는 이름)
	const string NumberColor = "#B5CEA8";        // 0, 0.5
	const string StringColor = "#CE9178";        // "abc", 'a'
	const string PunctuationColor = "#D4D4D4";   // ( ) { } , ; = + < && 와 포인터의 *
	// 형식 속의 한글 자리 글자(조건, 블록, 처음 값): 코드가 아니라 채울 곳이라 코드 색과 다른 연보라.
	// 참고 카드 「채울 곳」의 줄 이름(타입, 처음 값)도 ColorCode로 칠해 같은 색이 된다.
	// 흐린 보라(#B8B3C7)는 설명 글자(회색 파랑)와 구분되지 않았고, 주황은 진하다고 해서(사용자) 채도가 있는 연보라로
	const string PlaceholderColor = "#B9A2FF";

	// 변수·파라미터 이름 색 (하늘색, VS Code와 같음). 핑크로 했다가 사용자가 바꿈
	const string ParameterColor = "#9CDCFE";

	// Python 낱말(#532)도 함께 둔다. def·elif·in·global·del·and·or·not은 키워드, True·False·None·list는 기본 값·타입 색
	static readonly HashSet<string> Keywords = new HashSet<string> { "if", "else", "for", "while", "break", "continue", "return",
		"def", "elif", "in", "global", "del", "and", "or", "not", "pass" };
	static readonly HashSet<string> PrimitiveWords = new HashSet<string> { "void", "int", "float", "bool", "char", "string", "array", "true", "false", "NULL",
		"True", "False", "None", "list" };

	/// <summary>
	/// C 코드 글자(함수 원형 void skewer(Target target);, 문법 형식 if (조건) { 블록 })를 낱말마다 색을 나눈 리치 텍스트로 바꾼다 (#489, VS Code Dark+ 기준).
	/// 키워드는 보라, 기본 타입은 파랑, 게임 타입(Target, Sword)은 청록, 함수 이름은 노랑, 변수·파라미터 이름은 하늘색, 숫자는 연두,
	/// 괄호·연산자·포인터의 *는 밝은 회색, 한글 자리 글자(조건, 처음 값)는 흐린 보라. 함수 탭, 참고 카드, 설명창이 같이 쓴다.
	/// </summary>
	public static string ColorCode(string code)
	{
		if (string.IsNullOrEmpty(code)) return "";
		var text = new System.Text.StringBuilder();
		int i = 0;
		while (i < code.Length)
		{
			char c = code[i];
			int start = i;
			if (char.IsWhiteSpace(c))
			{
				// 빈칸·줄바꿈은 칠하지 않고 그대로 (Declare 형식은 두 줄)
				while (i < code.Length && char.IsWhiteSpace(code[i])) i++;
				text.Append(code, start, i - start);
			}
			else if (IsHangul(c))
			{
				// 한글 자리 글자. 「처음 값」, 「칸 수」처럼 빈칸을 사이에 둔 한글 낱말은 한 덩어리로
				while (i < code.Length && (IsHangul(code[i]) || (code[i] == ' ' && i + 1 < code.Length && IsHangul(code[i + 1])))) i++;
				text.Append(Paint(code.Substring(start, i - start), PlaceholderColor));
			}
			else if (char.IsLetter(c) || c == '_')
			{
				while (i < code.Length && (char.IsLetterOrDigit(code[i]) && !IsHangul(code[i]) || code[i] == '_')) i++;
				string word = code.Substring(start, i - start);
				text.Append(Paint(word, ColorOfWord(word, NextNonSpace(code, i) == '(')));
			}
			else if (char.IsDigit(c))
			{
				while (i < code.Length && (char.IsDigit(code[i]) || code[i] == '.')) i++;
				text.Append(Paint(code.Substring(start, i - start), NumberColor));
			}
			else if (c == '"' || c == '\'')
			{
				// 문자열·문자: 닫는 따옴표까지
				i++;
				while (i < code.Length && code[i] != c) i++;
				if (i < code.Length) i++;
				text.Append(Paint(code.Substring(start, i - start), StringColor));
			}
			else
			{
				// 괄호·연산자: 이어진 기호를 한 번에 (&&, ++, ==)
				while (i < code.Length && !char.IsWhiteSpace(code[i]) && !char.IsLetterOrDigit(code[i]) && code[i] != '_' && code[i] != '"' && code[i] != '\'') i++;
				text.Append(Paint(code.Substring(start, i - start), PunctuationColor));
			}
		}
		return text.ToString();
	}

	// 영문 낱말의 색: 키워드 → 기본 타입·상수 → 뒤에 괄호가 오면 함수 이름 → 대문자로 시작하면 게임 타입 → 나머지는 변수·파라미터
	static string ColorOfWord(string word, bool beforeParenthesis)
	{
		if (Keywords.Contains(word)) return KeywordColor;
		if (PrimitiveWords.Contains(word)) return PrimitiveTypeColor;
		if (beforeParenthesis) return FunctionNameColor;
		if (char.IsUpper(word[0])) return GameTypeColor;
		return ParameterColor;
	}

	static char NextNonSpace(string code, int index)
	{
		while (index < code.Length && code[index] == ' ') index++;
		return index < code.Length ? code[index] : '\0';
	}

	static bool IsHangul(char c) => c >= '가' && c <= '힣';

	// 글자 하나를 색으로 칠한다. 태그로 읽지 않음 (< 같은 글자가 그대로 보이게)
	static string Paint(string text, string color) => $"<color={color}><noparse>{text}</noparse></color>";

	// 타입을 C 글자로. 배열은 「int array」처럼 원소 타입 뒤에 array
	static string TypeText(VariableType type)
	{
		if (type == null) return "?";
		if (type.Kind == VariableTypeKind.Array) return TypeText(type.ElementType) + " array";
		return BlockLabels.TypeName(type);
	}

	static SlotData Int(string value) => SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), value);
}
