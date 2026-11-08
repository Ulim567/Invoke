using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 참고(Reference) 카드의 자세한 내용 한 항목 (예: 형식, 동작, 예시). 카드를 펼치면 위에서부터 차례로 보인다.
/// </summary>
public class ReferenceSection
{
	/// <summary>항목 이름 (예: 형식, 채울 곳, 동작, 예시, 주의)</summary>
	public string Title;

	/// <summary>항목 내용. 여러 줄이면 줄바꿈으로 나눈다</summary>
	public string Body;

	public ReferenceSection(string title, string body)
	{
		Title = title;
		Body = body;
	}
}

/// <summary>
/// 참고(Reference) 창의 카드 한 장. 블록 하나가 무엇이고 어떻게 쓰는지를 담는다. 보여 주기만 하는 데이터이고 게임 규칙에는 쓰지 않는다.
/// </summary>
public class BlockReferenceEntry
{
	/// <summary>카드 제목. 블록 목록(팔레트)의 칩 이름과 같다. 예: If, Else If, thrust</summary>
	public string Name;

	/// <summary>분류 태그. 블록 목록의 분류와 같다 (제어, 변수, 값, 함수, 메모리, 기타)</summary>
	public string Category;

	/// <summary>분류 태그의 색을 정할 때 쓰는 견본 블록 (BlockLabels.ColorOf). 편집 영역에 놓지 않는다</summary>
	public BlockData Sample;

	/// <summary>설명 한 문장 (접힌 카드에 보인다)</summary>
	public string Summary;

	/// <summary>어떻게 쓰는지 한 문장 (C 스킬: 파라미터에 무엇을 넣는지). 없으면 null</summary>
	public string Usage;

	/// <summary>C 표기 예시. 예: if (hp &gt; 5) { … }, thrust(target);</summary>
	public string CodeText;

	/// <summary>놓을 수 있는 곳. 예: 함수 body, 블록 영역</summary>
	public string WhereText;

	/// <summary>카드를 펼치면 보이는 자세한 내용. 위에서부터 차례로 보인다</summary>
	public List<ReferenceSection> Sections = new List<ReferenceSection>();

	/// <summary>사전 정의 함수의 고정 ID (c.thrust). 문법 블록이면 null</summary>
	public string FunctionId;

	/// <summary>스킬의 화면 이름 (검 찌르기). 검색에만 쓴다. 문법 블록이면 null</summary>
	public string DisplayName;
}

/// <summary>
/// 참고(Reference) 창에 보여 줄 블록 설명 표와 검색.
/// <para>
/// - 문법 블록(제어, 변수, 값, 메모리, 기타): 이 파일의 표에 설명을 직접 적는다. 스킬 트리 노드 하나가 블록 여러 개를 함께 열어서
///   (예: if문 노드가 if, compare를 같이 엶) 노드 설명을 그대로 쓰면 블록과 맞지 않기 때문이다.<br/>
/// - 함수 블록(C 스킬, 공통 이동, 입력): 블록 목록에 나오는 사전 정의 함수(c., common., input.)마다 한 장.
///   설명은 스킬 트리 노드 설명(SkillTreeNode.Description)을 그대로 읽고, 호출 모양·파라미터·동작 시간·쿨타임은 사전 정의 함수의 값에서 만든다.
///   파라미터에 무엇을 넣는지는 코드에서 알 수 없어 이 파일의 표에 적는다.<br/>
/// - 블록 목록(BlockEditor.PaletteGroups)과 따로 적은 표이므로, 블록 목록에 블록을 더하면 이 표에도 더한다.
/// </para>
/// </summary>
public static class BlockReference
{
	// 블록 목록에 나오는 사전 정의 함수의 고정 ID 앞부분 (BlockEditor.PaletteFunctionPrefixes와 같다)
	static readonly string[] FunctionPrefixes = { "c.", "common.", "input.", "game." };

	static List<BlockReferenceEntry> entries;

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
			if (entries == null) entries = Build();
			return entries;
		}
	}

	/// <summary>
	/// 검색한 글자가 이름, 분류, 스킬 화면 이름, 고정 ID에 들어 있는 카드를 돌려준다 (대소문자 구분 없음).
	/// 비어 있으면 전체. 예: "if" → If, Else If
	/// </summary>
	public static List<BlockReferenceEntry> Search(string query)
	{
		query = query?.Trim() ?? "";
		if (query.Length == 0) return All.ToList();
		return All.Where(e => Contains(e.Name, query) || Contains(e.Category, query)
			|| Contains(e.DisplayName, query) || Contains(e.FunctionId, query)).ToList();
	}

	static bool Contains(string text, string query) =>
		text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

	// 문법 블록 ---------------------------------------------------------------
	// (이름, 분류, 견본 블록을 만드는 함수, C 표기 예시, 설명 한 문장)
	static readonly (string Name, string Category, Func<BlockData> Sample, string Code, string Summary)[] SyntaxRows =
	{
		("If", "제어", () => BlockData.Conditional(ConditionalBranch.If), "if (hp > 5) { … }",
			"조건 칸이 참일 때만 안쪽 블록을 실행합니다."),
		("Else If", "제어", () => BlockData.Conditional(ConditionalBranch.ElseIf), "else if (hp > 0) { … }",
			"앞의 if나 else if가 거짓일 때 이 조건을 검사해 참이면 안쪽을 실행합니다."),
		("Else", "제어", () => BlockData.Conditional(ConditionalBranch.Else), "else { … }",
			"앞의 조건이 모두 거짓일 때 안쪽 블록을 실행합니다."),
		("For", "제어", () => BlockData.For("i", Int("0"), Int("10")), "for (int i = 0; i < 10; i++) { … }",
			"시작 값부터 끝 값 직전까지 i를 1씩 늘리며 안쪽 블록을 반복합니다."),
		("While", "제어", () => BlockData.While(SlotData.EmptyValue()), "while (hp > 0) { … }",
			"조건 칸이 참인 동안 안쪽 블록을 계속 반복합니다."),
		("Break", "제어", () => BlockData.Break(), "break;",
			"가장 안쪽 반복문을 바로 끝냅니다."),
		("Continue", "제어", () => BlockData.Continue(), "continue;",
			"이번 반복을 건너뛰고 다음 반복으로 넘어갑니다."),

		("Declare", "변수", () => BlockData.Declare(new VariableType(VariableTypeKind.Int), "x"), "int x = 3;",
			"타입과 이름을 골라 변수를 만들고, 원하면 처음 값을 넣습니다."),
		("Assign", "변수", () => BlockData.Assign("x", SlotData.EmptyValue()), "x = 5;",
			"이미 만든 변수에 새 값을 담습니다. 왼쪽 칸을 눌러 위에서 선언한 변수를 고릅니다."),
		("Array", "변수", () => BlockData.ArrayDeclare(new VariableType(VariableTypeKind.Int), "arr", Int("3")), "int arr[3];",
			"타입과 칸 수를 정해 같은 타입의 값 여러 개를 담는 배열을 만듭니다."),
		("Array Get", "변수", () => BlockData.ArrayGet("arr", SlotData.EmptyValue()), "arr[i]",
			"배열의 i번째 칸 값을 읽습니다. 값 칸에 끼워 쓰고, 칸 번호는 0부터 셉니다."),
		("Array Set", "변수", () => BlockData.ArraySet("arr", SlotData.EmptyValue(), SlotData.EmptyValue()), "arr[i] = 7;",
			"배열의 i번째 칸에 값을 담습니다. 칸 번호는 0부터 셉니다."),

		("Arithmetic", "값", () => BlockData.Arithmetic(SlotData.EmptyValue(), ArithmeticOperator.Add, SlotData.EmptyValue()), "x + 1",
			"두 값을 + - * / % 중 하나로 계산합니다. 값 칸에 끼워 씁니다."),
		("Compare", "값", () => BlockData.Compare(SlotData.EmptyValue(), CompareOperator.Greater, SlotData.EmptyValue()), "hp > 5",
			"두 값을 > < == != >= <= 중 하나로 비교해 참 또는 거짓을 만듭니다. 값 칸에 끼워 씁니다."),
		("Logic", "값", () => BlockData.Logic(SlotData.EmptyValue(), LogicOperator.And, SlotData.EmptyValue()), "a && b",
			"두 조건을 &&(모두 참) 또는 ||(하나라도 참)로 합칩니다. 값 칸에 끼워 씁니다."),
		("Not", "값", () => BlockData.Not(SlotData.EmptyValue()), "!done",
			"참과 거짓을 뒤집습니다. 값 칸에 끼워 씁니다."),

		("Malloc", "메모리", () => BlockData.Malloc(VariableType.StructOf(PredefinedStructs.Sword)), "malloc(Sword)",
			"heap에 Sword를 만들고 그 포인터를 돌려줍니다. 소환검은 최대 5개이고, 만들지 못하면 NULL을 돌려줍니다."),
		("Free", "메모리", () => BlockData.Free(SlotData.EmptyValue()), "free(s);",
			"포인터가 가리키는 heap 메모리를 해제합니다."),

		("Return", "기타", () => BlockData.Return(), "return n;",
			"함수를 끝냅니다. 값을 적으면 부른 쪽에 그 값을 돌려줍니다."),
		("Wait", "기타", () => BlockData.Wait(SlotData.FromLiteral(new VariableType(VariableTypeKind.Float), "0.5")), "wait(0.5초);",
			"적은 초만큼 기다린 뒤 다음 블록으로 넘어갑니다."),
	};

	// 문법 블록의 자세한 내용: 이름 → (형식, 채울 곳 줄들, 동작, 예시, 주의). 주의가 없으면 null
	static readonly Dictionary<string, (string Format, string[] Fills, string Behavior, string Example, string Note)> SyntaxDetails =
		new Dictionary<string, (string, string[], string, string, string)>
	{
		["If"] = ("if (조건) { 블록 }",
			new[]
			{
				"조건 — 참 또는 거짓이 되는 값입니다. Compare, Logic 블록을 끼우거나 true, false를 적습니다.",
				"안쪽 블록 — 조건이 참일 때 실행할 블록을 놓는 곳입니다.",
			},
			"조건이 참이면 안쪽 블록을 위에서부터 차례로 실행하고, 거짓이면 건너뜁니다. 바로 아래에 Else If나 Else를 붙이면 거짓일 때 다른 길을 고를 수 있습니다.",
			"if (hp > 5) {\n  thrust(get_bug());\n}",
			"조건 칸이 비어 있으면 컴파일 오류입니다."),
		["Else If"] = ("else if (조건) { 블록 }",
			new[]
			{
				"조건 — 참 또는 거짓이 되는 값입니다.",
				"안쪽 블록 — 조건이 참일 때 실행할 블록을 놓는 곳입니다.",
			},
			"바로 위의 if와 else if의 조건이 모두 거짓일 때만 이 조건을 검사하고, 참이면 안쪽을 실행합니다. 앞에서 이미 한 곳을 실행했으면 건너뜁니다.",
			"if (hp > 5) {\n  thrust(get_bug());\n} else if (hp > 0) {\n  dash();\n}",
			"if 또는 else if 바로 아래에만 놓을 수 있습니다."),
		["Else"] = ("else { 블록 }",
			new[] { "안쪽 블록 — 앞의 조건이 모두 거짓일 때 실행할 블록을 놓는 곳입니다. 조건 칸은 없습니다." },
			"바로 위의 if와 else if의 조건이 모두 거짓일 때만 안쪽 블록을 실행합니다.",
			"if (hp > 5) {\n  thrust(get_bug());\n} else {\n  dash();\n}",
			"if 또는 else if 바로 아래에만 놓을 수 있습니다. else 바로 아래에는 else if, else를 붙일 수 없습니다."),
		["For"] = ("for (int i = 시작; i < 끝; i++) { 블록 }",
			new[]
			{
				"i — 몇 번째 반복인지 세는 변수의 이름입니다. 이름 칸에서 바꿀 수 있습니다.",
				"시작 — i의 처음 값입니다.",
				"끝 — i가 이 값에 닿으면 반복을 끝냅니다. 끝 값은 포함하지 않습니다.",
				"안쪽 블록 — 반복할 블록을 놓는 곳입니다.",
			},
			"i가 시작 값에서 출발해 1씩 늘어나며, i < 끝인 동안 안쪽 블록을 반복합니다. 시작 0, 끝 3이면 i는 0, 1, 2로 세 번 실행합니다.",
			"for (int i = 0; i < 3; i++) {\n  thrust(get_bug());\n}",
			"시작 값이 끝 값 이상이면 한 번도 실행하지 않습니다."),
		["While"] = ("while (조건) { 블록 }",
			new[]
			{
				"조건 — 참 또는 거짓이 되는 값입니다.",
				"안쪽 블록 — 조건이 참인 동안 반복할 블록을 놓는 곳입니다.",
			},
			"조건을 먼저 검사하고, 참이면 안쪽 블록을 실행한 뒤 다시 조건을 검사합니다. 거짓이 되면 반복을 끝냅니다.",
			"int n = 3;\nwhile (n > 0) {\n  thrust(get_bug());\n  n = n - 1;\n}",
			"안쪽에서 조건이 거짓이 되게 만들지 않으면 끝나지 않습니다. Break로 빠져나올 수 있습니다."),
		["Break"] = ("break;",
			new[] { "채울 곳이 없습니다." },
			"가장 안쪽의 for 또는 while을 바로 끝내고, 그 반복문 다음 블록으로 넘어갑니다.",
			"for (int i = 0; i < 10; i++) {\n  if (i == 3) {\n    break;\n  }\n}",
			"for, while 안쪽에서만 쓸 수 있습니다. 밖에 놓으면 컴파일 오류입니다."),
		["Continue"] = ("continue;",
			new[] { "채울 곳이 없습니다." },
			"이번 반복의 남은 블록을 건너뛰고 다음 반복으로 넘어갑니다. for는 i를 1 늘린 뒤 조건을 다시 검사합니다.",
			"for (int i = 0; i < 5; i++) {\n  if (i == 2) {\n    continue;\n  }\n  thrust(get_bug());\n}",
			"for, while 안쪽에서만 쓸 수 있습니다. 밖에 놓으면 컴파일 오류입니다."),

		["Declare"] = ("타입 이름;\n타입 이름 = 처음 값;",
			new[]
			{
				"타입 — int, float, bool, char, string, Sword*, Target 중에서 고릅니다. Sword*에는 summon_sword()가 돌려준 소환검을, Target에는 get_bug()가 돌려준 몬스터를 담습니다.",
				"이름 — 새 변수의 이름입니다. 영문자, 숫자, 밑줄만 쓰고 숫자로 시작할 수 없습니다.",
				"처음 값 — 변수에 처음 담을 값입니다. 블록 끝의 > 버튼으로 칸을 펼치고, < 버튼으로 접습니다(넣은 값은 지워집니다). 비워 두면 그 타입의 기본값으로 시작합니다.",
			},
			"변수를 만들고 처음 값을 담습니다. 이후 그 이름으로 값을 읽고, Assign으로 값을 바꿉니다.",
			"int hp = 10;",
			"같은 이름의 변수를 두 번 선언하면 오류입니다."),
		["Assign"] = ("변수 = 값;",
			new[]
			{
				"변수 — 눌러서 이 블록보다 위에서 선언한 변수 중에 고릅니다.",
				"값 — 담을 값입니다. 숫자, 변수, 값 블록을 넣습니다.",
			},
			"오른쪽 값을 계산한 뒤 변수에 담아, 이전에 담겨 있던 값을 바꿉니다.",
			"hp = hp - 1;",
			"선언하지 않은 변수에 담으면 컴파일 오류입니다."),
		["Array"] = ("타입 이름[칸 수];",
			new[]
			{
				"타입 — 원소의 타입입니다.",
				"이름 — 배열의 이름입니다.",
				"칸 수 — 담을 수 있는 원소의 개수입니다.",
			},
			"같은 타입의 값을 칸 수만큼 담을 수 있는 배열을 만듭니다. 원소의 번호는 0부터 칸 수 - 1까지입니다.",
			"int arr[3];",
			"범위를 벗어난 번호의 칸은 쓸 수 없습니다."),
		["Array Get"] = ("배열[번호]",
			new[]
			{
				"배열 — 눌러서 위에서 만든 배열 중에 고릅니다.",
				"번호 — 읽을 칸의 번호입니다. 0부터 셉니다.",
			},
			"배열의 그 번호 칸에 담긴 값을 돌려줍니다. 값 칸에 끼워 씁니다.",
			"x = arr[1];",
			"배열을 먼저 Array 블록으로 만들어야 합니다."),
		["Array Set"] = ("배열[번호] = 값;",
			new[]
			{
				"배열 — 눌러서 위에서 만든 배열 중에 고릅니다.",
				"번호 — 값을 담을 칸의 번호입니다. 0부터 셉니다.",
				"값 — 그 칸에 담을 값입니다.",
			},
			"배열의 그 번호 칸에 값을 담습니다.",
			"arr[1] = 7;",
			"배열을 먼저 Array 블록으로 만들어야 합니다."),

		["Arithmetic"] = ("(왼쪽 연산자 오른쪽)",
			new[]
			{
				"왼쪽, 오른쪽 — 계산할 두 값입니다. 숫자, 변수, 다른 값 블록을 넣습니다.",
				"연산자 — + - * / % 중에서 고릅니다.",
			},
			"두 값을 계산한 결과를 돌려줍니다. %는 나눈 나머지입니다.",
			"hp = hp - 1;",
			"값 칸에만 끼울 수 있고, 함수 body에 바로 놓을 수 없습니다."),
		["Compare"] = ("왼쪽 비교자 오른쪽",
			new[]
			{
				"왼쪽, 오른쪽 — 비교할 두 값입니다.",
				"비교자 — > < == != >= <= 중에서 고릅니다.",
			},
			"두 값을 비교해 맞으면 참(true), 아니면 거짓(false)을 돌려줍니다. if, while의 조건 칸에 끼웁니다.",
			"if (hp > 5) { … }",
			"==는 같은지 비교하는 것이고, 값을 담는 것은 Assign입니다."),
		["Logic"] = ("왼쪽 && 오른쪽",
			new[]
			{
				"왼쪽, 오른쪽 — 참 또는 거짓이 되는 두 값입니다.",
				"연산자 — && 또는 || 중에서 고릅니다.",
			},
			"&&는 두 값이 모두 참일 때만 참, ||는 하나라도 참이면 참을 돌려줍니다.",
			"if (hp > 0 && hp < 5) { … }",
			null),
		["Not"] = ("!값",
			new[] { "값 — 뒤집을 참 또는 거짓입니다." },
			"참이면 거짓, 거짓이면 참을 돌려줍니다.",
			"if (!(hp > 5)) { … }",
			null),

		["Malloc"] = ("malloc(타입)",
			new[] { "타입 — 만들 대상입니다. 지금은 Sword만 있습니다." },
			"heap에 그 타입의 오브젝트를 만들고, 그것을 가리키는 포인터를 돌려줍니다. Sword는 실제 소환검입니다. 소환검은 최대 5개이고, 만들지 못하면 NULL을 돌려줍니다.",
			"summoned_thrust(get_bug(), malloc(Sword));",
			"쓰고 나면 Free로 해제해야 합니다. 해제하지 않으면 heap을 계속 차지합니다."),
		["Free"] = ("free(포인터);",
			new[] { "포인터 — 해제할 오브젝트를 가리키는 값입니다. malloc이나 summon_sword가 돌려준 값을 넣습니다." },
			"포인터가 가리키는 heap 오브젝트를 없애고 자리를 돌려줍니다.",
			"free(summon_sword());",
			"이미 해제한 포인터는 다시 쓸 수 없습니다."),

		["Return"] = ("return 값;",
			new[] { "값 — 부른 쪽에 돌려줄 값입니다. 돌려줄 값이 없는 함수는 비워 둡니다." },
			"함수를 그 자리에서 끝내고 값을 부른 쪽에 돌려줍니다. 아래에 블록이 남아 있어도 실행하지 않습니다.",
			"return n;",
			"반환 타입이 void가 아닌 함수는 모든 길이 return으로 끝나야 합니다. 아니면 컴파일 오류입니다."),
		["Wait"] = ("wait(초);",
			new[] { "초 — 기다릴 시간입니다. 소수로 적을 수 있습니다." },
			"적은 초만큼 기다린 뒤 다음 블록을 실행합니다. 기다리는 동안 이 함수는 멈춰 있습니다.",
			"thrust(get_bug());\nwait(0.5초);\nthrust(get_bug());",
			null),
	};

	// 함수 블록 ---------------------------------------------------------------
	// 스킬 트리 노드에 설명이 없는 함수의 짧은 설명 (input.* 는 스킬 트리에 노드가 없다)
	static readonly Dictionary<string, string> FunctionSummaries = new Dictionary<string, string>
	{
		["input.get_bug"] = "플레이어가 고른 몬스터 한 명을 대상으로 돌려줍니다.",
		["game.is_valid_target"] = "대상이 스킬에 넘길 수 있는 몬스터인지(있고, 살아 있는지) 알려 줍니다.",
	};

	// 파라미터 이름 → 무엇을 넣는지 (코드에서 알 수 없는 사용 지식)
	static readonly Dictionary<string, string> ParameterMeanings = new Dictionary<string, string>
	{
		["target"] = "몬스터 한 명입니다. get_bug()를 넣거나, get_bug()를 담아 둔 Target 변수를 넣습니다.",
		["targets"] = "공격할 몬스터 여러 명입니다. 슬롯에서 시전하면 여러 명을 고르게 해 줍니다.",
		["sword"] = "쓸 소환검의 포인터입니다. summon_sword()가 돌려준 값을 넣습니다.",
	};

	// 함수 고정 ID → 돌려주는 값이 무엇인지
	static readonly Dictionary<string, string> ReturnMeanings = new Dictionary<string, string>
	{
		[PredefinedFunctionIds.SummonSword] = "만든 소환검의 포인터입니다. 만들지 못하면 NULL입니다.",
		[PredefinedFunctionIds.GetBug] = "플레이어가 고른 몬스터입니다. 고를 몬스터가 없으면 「대상 없음」입니다.",
		[PredefinedFunctionIds.IsValidTarget] = "스킬에 넘길 수 있으면 true, 「대상 없음」이거나 죽었거나 없어진 몬스터면 false입니다.",
	};

	// 파라미터에 무엇을 넣는지 한 문장 (접힌 카드에 보인다). 파라미터가 없으면 호출 모양만으로 충분해 한 줄 안내만 둔다
	const string SingleTarget = "target에는 get_bug()가 돌려준 대상이나 Target 변수를 넣습니다.";
	const string NoParameter = "값을 넣지 않고 이름();으로 부릅니다.";
	const string NeedsSword = " 소환검이 있어야 씁니다.";

	static readonly Dictionary<string, string> FunctionUsages = new Dictionary<string, string>
	{
		[PredefinedFunctionIds.Thrust] = SingleTarget,
		[PredefinedFunctionIds.FocusedThrust] = SingleTarget,
		[PredefinedFunctionIds.VitalStrike] = SingleTarget,
		[PredefinedFunctionIds.Skewer] = SingleTarget + " 넘긴 대상부터 뒤쪽으로 관통합니다.",
		[PredefinedFunctionIds.WoundExploit] = SingleTarget,
		[PredefinedFunctionIds.DeepThrust] = SingleTarget,
		[PredefinedFunctionIds.SwordThrow] = SingleTarget,
		[PredefinedFunctionIds.SwordSlash] = SingleTarget,
		[PredefinedFunctionIds.SummonedSlash] = SingleTarget,
		[PredefinedFunctionIds.WoundSlash] = SingleTarget,
		[PredefinedFunctionIds.MultiSlash] = SingleTarget + NeedsSword,
		[PredefinedFunctionIds.Mark] = SingleTarget + NeedsSword,
		[PredefinedFunctionIds.SwordDance] = SingleTarget + NeedsSword,
		[PredefinedFunctionIds.SummonedThrust] = "target에는 get_bug()가 돌려준 대상을, sword에는 summon_sword()가 돌려준 검을 넣습니다." + NeedsSword,
		[PredefinedFunctionIds.SummonedThrow] = "target에는 get_bug()가 돌려준 대상을, sword에는 summon_sword()가 돌려준 검을 넣습니다." + NeedsSword,
		[PredefinedFunctionIds.BattlefieldSweep] = "targets에는 대상 array를 넣습니다. 슬롯에서 쓰면 여러 대상을 고르게 해 줍니다." + NeedsSword,
		[PredefinedFunctionIds.SummonSword] = "돌려받은 포인터(Sword*)를 summoned_thrust, summoned_throw, free_sword의 sword에 넘길 수 있습니다. 소환검이 이미 최대 개수면 NULL을 돌려줍니다.",
		[PredefinedFunctionIds.FreeSword] = "sword에는 summon_sword()가 돌려준 검을 넣습니다." + NeedsSword,
		[PredefinedFunctionIds.SwordReturn] = NoParameter,
		[PredefinedFunctionIds.WideSlash] = NoParameter,
		[PredefinedFunctionIds.Parry] = NoParameter,
		[PredefinedFunctionIds.Dash] = NoParameter,
		[PredefinedFunctionIds.DoubleJump] = NoParameter,
		[PredefinedFunctionIds.SwordRide] = NoParameter + NeedsSword,
		[PredefinedFunctionIds.GetBug] = "Target b = get_bug(); 처럼 쓰면 플레이어가 대상을 고를 때까지 기다린 뒤 그 대상을 돌려줍니다. 고를 몬스터가 없으면 「대상 없음」을 돌려주고, 그 값을 스킬에 넘기면 실패합니다.",
		[PredefinedFunctionIds.IsValidTarget] = "if (!is_valid_target(b)) b = get_bug(); 처럼 담아 둔 몬스터가 죽었으면 새로 고릅니다. Target은 포인터가 아니라 NULL과 비교할 수 없습니다.",
	};

	static List<BlockReferenceEntry> Build()
	{
		var list = new List<BlockReferenceEntry>();

		foreach ((string name, string category, Func<BlockData> sample, string code, string summary) in SyntaxRows)
		{
			BlockData block = sample();
			var entry = new BlockReferenceEntry
			{
				Name = name,
				Category = category,
				Sample = block,
				Summary = summary,
				CodeText = code,
				WhereText = WhereOf(block),
			};
			if (SyntaxDetails.TryGetValue(name, out var detail))
			{
				entry.Sections.Add(new ReferenceSection("형식", detail.Format));
				entry.Sections.Add(new ReferenceSection("채울 곳", string.Join("\n", detail.Fills)));
				entry.Sections.Add(new ReferenceSection("동작", detail.Behavior));
				entry.Sections.Add(new ReferenceSection("예시", detail.Example));
				if (!string.IsNullOrEmpty(detail.Note)) entry.Sections.Add(new ReferenceSection("주의", detail.Note));
			}
			list.Add(entry);
		}

		IReadOnlyList<PredefinedFunction> functions = PredefinedFunctionRegistry.All;
		if (functions == null) return list;
		foreach (PredefinedFunction function in functions)
		{
			string id = function?.Id;
			if (id == null || !FunctionPrefixes.Any(prefix => id.StartsWith(prefix, StringComparison.Ordinal))) continue;
			list.Add(FunctionEntry(function));
		}
		return list;
	}

	static BlockReferenceEntry FunctionEntry(PredefinedFunction function)
	{
		string id = function.Id;
		string name = BlockLabels.PredefinedFunctionName(id);
		IReadOnlyList<ParameterData> parameters = function.Parameters ?? (IReadOnlyList<ParameterData>)new List<ParameterData>();
		bool returnsValue = function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void;

		// 스킬 트리 노드의 설명을 그대로 읽는다 (읽기만 한다). 노드가 없으면 이 파일의 짧은 설명
		string summary = null, detail = null;
		if (SkillTree.TryGet(id, out SkillTreeNode node) && node.Description != null)
		{
			summary = node.Description.Summary;
			detail = node.Description.Detail;
		}
		if (string.IsNullOrEmpty(summary)) FunctionSummaries.TryGetValue(id, out summary);
		FunctionUsages.TryGetValue(id, out string usage);

		string args = string.Join(", ", parameters.Select(p => p.Name));
		string call = $"{name}({args});";
		if (returnsValue) call = $"{TypeText(function.ReturnType)} result = {name}({args});";

		// 견본 블록: 편집 화면의 함수 블록 견본과 같이 파라미터 수만큼 빈 값 칸을 둔다
		SlotData[] slots = new SlotData[parameters.Count];
		for (int i = 0; i < slots.Length; i++) slots[i] = SlotData.EmptyValue();
		BlockData sample = BlockData.CallPredefinedFunction(id, slots);

		var entry = new BlockReferenceEntry
		{
			Name = name,
			Category = "함수",
			Sample = sample,
			Summary = summary,
			Usage = usage,
			CodeText = call,
			WhereText = WhereOf(sample, returnsValue),
			FunctionId = id,
			DisplayName = function.Name,
		};

		if (!string.IsNullOrEmpty(detail)) entry.Sections.Add(new ReferenceSection("설명", detail));
		entry.Sections.Add(new ReferenceSection("형식", call));
		if (parameters.Count > 0)
		{
			entry.Sections.Add(new ReferenceSection("파라미터", string.Join("\n", parameters.Select(p =>
				ParameterMeanings.TryGetValue(p.Name, out string meaning)
					? $"{p.Name} ({TypeText(p.Type)}) — {meaning}"
					: $"{p.Name} ({TypeText(p.Type)})"))));
		}
		if (returnsValue)
		{
			string returned = ReturnMeanings.TryGetValue(id, out string returnMeaning) ? " — " + returnMeaning : "";
			entry.Sections.Add(new ReferenceSection("돌려주는 값", TypeText(function.ReturnType) + returned));
		}
		entry.Sections.Add(new ReferenceSection("실행", CostOf(function)));
		string example = ExampleOf(name, parameters);
		if (example != null) entry.Sections.Add(new ReferenceSection("예시", example));
		return entry;
	}

	// 동작 시간, 쿨타임, 코드 크기, 비동기 여부. 사전 정의 함수의 값을 그대로 읽는다 (틱 하나의 시간은 캐릭터마다 달라 틱 수로 적는다)
	static string CostOf(PredefinedFunction function)
	{
		var lines = new List<string> { $"동작 시간 {function.TickCount}틱" };
		if (function.CooldownTicks > 0) lines.Add($"쿨타임 {function.CooldownTicks}틱");
		lines.Add($"코드 크기 {function.CodeSize}");
		if (function.IsAsync) lines.Add("비동기 — 다른 함수 안에서 불러도 끝나기를 기다리지 않습니다.");
		return string.Join("\n", lines);
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
				case "sword": args.Add("summon_sword()"); break;
				default: return null;
			}
		}
		return $"{name}({string.Join(", ", args)});";
	}

	// 놓을 수 있는 곳. 함수 블록은 돌려주는 값이 있을 때만 값 칸에도 끼울 수 있다 (BlockEditor.CanBeValue와 같은 규칙)
	static string WhereOf(BlockData block, bool functionReturnsValue = false)
	{
		BlockKind kind = block.Kind;
		if (kind == BlockKind.Conditional)
		{
			string branch = block.Slots.Count > 0 ? block.Slots[0].Option : null;
			if (branch == nameof(ConditionalBranch.ElseIf) || branch == nameof(ConditionalBranch.Else))
				return "함수 body, 블록 영역 (if 또는 else if 바로 아래만)";
		}
		if (kind == BlockKind.Break || kind == BlockKind.Continue) return "for, while의 블록 영역";
		if (kind == BlockKind.Function) return functionReturnsValue ? "함수 body, 블록 영역, 값 칸" : "함수 body, 블록 영역";
		return BlockData.CanBeValue(kind) ? "값 칸" : "함수 body, 블록 영역";
	}

	// 타입을 C 글자로. 배열은 「int array」처럼 원소 타입 뒤에 array
	static string TypeText(VariableType type)
	{
		if (type == null) return "?";
		if (type.Kind == VariableTypeKind.Array) return TypeText(type.ElementType) + " array";
		return BlockLabels.TypeName(type);
	}

	static SlotData Int(string value) => SlotData.FromLiteral(new VariableType(VariableTypeKind.Int), value);
}
