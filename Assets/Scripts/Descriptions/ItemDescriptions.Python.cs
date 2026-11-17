using System.Collections.Generic;

/// <summary>
/// Python 캐릭터에게 보이는 블록 설명 (#532, 이우림). key는 C 표(ItemDescriptions.cs)와 같고, Python 캐릭터가 Get을 부르면 이 표를 먼저 본다.
/// 이 표에 없는 key(스킬, get_bug 등)는 C 표와 스킬 표의 설명을 그대로 쓴다.
/// </summary>
// C 표를 고치지 않고 따로 두는 이유: 같은 블록이라도 Python에서는 모양(;, { }, 선언 없음)과 규칙(대입이 곧 변수 만들기)이 달라 문장이 달라지기 때문
public static partial class ItemDescriptions
{
	// 내 캐릭터가 Python이면 true. 설명 표는 화면(내 캐릭터)에서만 읽는다
	static bool IsPythonCharacter => PlayerCharacter.LocalLanguage == CharacterLanguages.Python;

	static readonly Dictionary<string, ItemDescription> PythonEntries = new Dictionary<string, ItemDescription>
	{
		// 제어 ----------------------------------------------------------------
		["If"] = new ItemDescription
		{
			Format = "if condition: body",
			Summary = "condition이 참일 때만 body를 실행합니다.",
			Purpose = "조건에 따라 실행할지 말지를 정하고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 살아 있을 때만 매직 미사일을 쓰도록 만들 수 있습니다.",
			ExampleCode = "b = get_bug()\nif is_valid_target(b):\n    magic_missile(b)",
			ExampleResult = "고른 몬스터가 살아 있으면 매직 미사일을 쓰고, 그렇지 않으면 다음 블록으로 넘어갑니다.",
			Slots = new[]
			{
				"condition : 참이면 body를 실행 | 아무 값 (0, 0.0, \"\", 빈 list, None은 거짓)",
				"body : condition이 참일 때 실행할 블록 | statement",
			},
			Caution = "condition이 비어 있으면 SyntaxError가 발생합니다.",
		},
		["ElseIf"] = new ItemDescription
		{
			Format = "elif condition: body",
			Summary = "앞의 condition이 모두 거짓일 때 이 condition을 검사하고, 참이면 body를 실행합니다.",
			Purpose = "경우를 셋 이상으로 나누어 각각 다른 일을 하고 싶을 때 씁니다.",
			Slots = new[]
			{
				"condition : 참이면 body를 실행 | 아무 값",
				"body : condition이 참일 때 실행할 블록 | statement",
			},
			Caution = "if나 elif 바로 아래에만 놓을 수 있습니다.",
		},
		["Else"] = new ItemDescription
		{
			Format = "else: body",
			Summary = "앞의 condition이 모두 거짓일 때 body를 실행합니다.",
			Slots = new[] { "body : 앞의 condition이 모두 거짓일 때 실행할 블록 | statement" },
			Caution = "if나 elif 바로 아래에만 놓을 수 있습니다.",
		},
		["For"] = new ItemDescription
		{
			Format = "for i in range(start, end): body",
			Summary = "i를 start부터 end 바로 앞까지 1씩 늘리며 body를 반복합니다.",
			Purpose = "정해진 횟수만큼 같은 일을 하고 싶을 때 씁니다.",
			Example = "예를 들면, 마나 구슬 세 개를 만들어 list에 담을 수 있습니다.",
			ExampleCode = "orbs = []\nfor i in range(0, 3):\n    orbs.append(orb_create())",
			ExampleResult = "orbs에 구슬 세 개가 담깁니다.",
			Slots = new[]
			{
				"i : 반복마다 바뀌는 variable 이름 | 이름",
				"start : 처음 i 값 | int",
				"end : 이 값이 되기 전까지 반복 | int",
				"body : 반복할 블록 | statement",
			},
			Caution = "body에서 i를 바꿔도 다음 반복의 i는 range가 정합니다. 반복이 끝난 뒤에도 i는 남아 있습니다.",
		},
		["While"] = new ItemDescription
		{
			Format = "while condition: body",
			Summary = "condition이 참인 동안 body를 계속 반복합니다.",
			Slots = new[]
			{
				"condition : 참인 동안 반복 | 아무 값",
				"body : 반복할 블록 | statement",
			},
			Caution = "condition이 계속 참이면 끝나지 않습니다.",
		},
		["Break"] = new ItemDescription
		{
			Format = "break",
			Summary = "가장 안쪽 반복문을 바로 끝냅니다.",
			Caution = "반복문 밖에 놓으면 SyntaxError가 발생합니다.",
		},
		["Continue"] = new ItemDescription
		{
			Format = "continue",
			Summary = "이번 반복의 남은 블록을 건너뛰고 다음 반복으로 갑니다.",
			Caution = "반복문 밖에 놓으면 SyntaxError가 발생합니다.",
		},

		// 변수 ----------------------------------------------------------------
		["Assign"] = new ItemDescription
		{
			Format = "variable = value",
			Summary = "variable에 값을 담습니다. 처음 담는 이름이면 그 variable이 새로 생깁니다.",
			Purpose = "값에 이름을 붙여 담아 두고 다시 쓰고 싶을 때 씁니다.",
			Example = "예를 들면, 만든 마나 구슬을 variable에 담아 두었다가 쏠 수 있습니다.",
			ExampleCode = "orb = orb_create()\norb_shot(get_bug(), orb)",
			ExampleResult = "만든 구슬을 orb에 담아 두었다가 고른 몬스터에게 쏩니다.",
			Slots = new[]
			{
				"variable : 값을 담을 이름 | 이름",
				"value : 담을 값 | 아무 값",
			},
			Caution = "type은 담은 값이 정합니다. 다른 type의 값을 다시 담아도 됩니다. function 안에서 담는 이름은 그 function의 지역 variable입니다(global 제외).",
		},
		["Global"] = new ItemDescription
		{
			Format = "global name",
			Summary = "이 function 안에서 name에 담으면 Global Variables의 name을 바꾸게 합니다.",
			Purpose = "구슬이나 list를 function이 끝나도 남도록 전역 variable에 담고 싶을 때 씁니다.",
			Example = "예를 들면, 만든 구슬을 전역 list에 모을 수 있습니다.",
			ExampleCode = "global orb\norb = orb_create()",
			ExampleResult = "Global Variables의 orb에 구슬이 담기고, function이 끝나도 남습니다.",
			Slots = new[] { "name : Global Variables에 있는 이름 | 이름" },
			Caution = "Global Variables에 없는 이름에 담으면 NameError가 발생합니다. 읽기만 하는 이름에는 global을 적지 않아도 됩니다.",
		},
		["Del"] = new ItemDescription
		{
			Format = "del variable",
			Summary = "variable을 지웁니다.",
			Purpose = "더 쓰지 않는 구슬이나 list를 놓아주고 싶을 때 씁니다.",
			Slots = new[] { "variable : 지울 이름 | 이름" },
			Caution = "지운 뒤에 읽으면 NameError가 발생합니다. 그 이름만 가리키던 구슬과 list는 heap에서 사라집니다.",
		},
		["ListNew"] = new ItemDescription
		{
			Format = "[]",
			Summary = "빈 list를 만듭니다.",
			Purpose = "구슬이나 몬스터 여러 개를 한 variable에 모으고 싶을 때 씁니다.",
			ExampleCode = "orbs = []",
			Caution = "list는 heap에 만들어지고, 원소 하나마다 heap 1칸을 씁니다.",
		},
		["ListAppend"] = new ItemDescription
		{
			Format = "list.append(value)",
			Summary = "list의 끝에 value를 넣습니다.",
			Slots = new[]
			{
				"list : 값을 넣을 list | list",
				"value : 넣을 값 | 아무 값",
			},
			Caution = "heap 1칸을 더 씁니다. heap이 모자라면 MemoryError가 발생합니다.",
		},
		["ListPop"] = new ItemDescription
		{
			Format = "list.pop(index)",
			Summary = "list에서 index번 원소를 빼고 그 값을 돌려줍니다.",
			Slots = new[]
			{
				"list : 원소를 뺄 list | list",
				"index : 뺄 원소의 번호. 비우면 맨 끝 | int",
			},
			Returns = "뺀 원소",
			Caution = "빈 list이거나 범위 밖이면 IndexError가 발생합니다. -1은 맨 끝 원소입니다.",
		},
		["ListLen"] = new ItemDescription
		{
			Format = "len(list)",
			Summary = "list의 원소 수를 돌려줍니다.",
			Slots = new[] { "list : 원소 수를 셀 list | list" },
			Returns = "원소 수 (int)",
		},
		["ArrayGet"] = new ItemDescription
		{
			Format = "list[index]",
			Summary = "list의 index번 원소를 읽습니다.",
			Slots = new[]
			{
				"list : 읽을 list | list",
				"index : 원소 번호. 0부터, 음수는 뒤에서부터 | int",
			},
			Returns = "그 원소",
			Caution = "범위 밖이면 IndexError가 발생합니다.",
		},
		["ArraySet"] = new ItemDescription
		{
			Format = "list[index] = value",
			Summary = "list의 index번 원소를 value로 바꿉니다.",
			Slots = new[]
			{
				"list : 바꿀 list | list",
				"index : 원소 번호. 0부터, 음수는 뒤에서부터 | int",
				"value : 넣을 값 | 아무 값",
			},
			Caution = "범위 밖이면 IndexError가 발생합니다.",
		},

		// 값 ------------------------------------------------------------------
		["Arithmetic"] = new ItemDescription
		{
			Format = "left + right",
			Summary = "두 값을 + - * / // % 로 계산합니다.",
			Example = "예를 들면, 7 / 2는 3.5, 7 // 2는 3, -7 % 2는 1입니다.",
			Slots = new[]
			{
				"left : 계산할 값 | 숫자, + 는 문자열도",
				"right : 계산할 값 | 숫자, + 는 문자열도",
			},
			Returns = "계산 결과",
			Caution = "/는 언제나 float입니다. //와 %는 아래로 내림합니다. 0으로 나누면 ZeroDivisionError가 발생합니다.",
		},
		["Compare"] = new ItemDescription
		{
			Format = "left > right",
			Summary = "두 값을 비교해 True나 False를 돌려줍니다.",
			Slots = new[]
			{
				"left : 비교할 값 | 아무 값",
				"right : 비교할 값 | 아무 값",
			},
			Returns = "True 또는 False",
			Caution = "== 와 != 는 어떤 값이든 비교합니다(None 포함). 크기 비교는 숫자끼리, 문자열끼리만 되고 그 밖은 TypeError가 발생합니다.",
		},
		["Logic"] = new ItemDescription
		{
			Format = "left and right",
			Summary = "and는 둘 다 참일 때, or는 하나라도 참일 때 True를 돌려줍니다.",
			Slots = new[]
			{
				"left : 조건 | 아무 값",
				"right : 조건 | 아무 값",
			},
			Returns = "True 또는 False",
		},
		["Not"] = new ItemDescription
		{
			Format = "not value",
			Summary = "참이면 False, 거짓이면 True를 돌려줍니다.",
			Slots = new[] { "value : 뒤집을 조건 | 아무 값" },
			Returns = "True 또는 False",
		},

		// 함수 ----------------------------------------------------------------
		["Return"] = new ItemDescription
		{
			Format = "return value",
			Summary = "function을 끝내고 value를 돌려줍니다.",
			Slots = new[] { "value : 돌려줄 값. 비우면 None | 아무 값" },
			Caution = "return 없이 끝난 function은 None을 돌려줍니다.",
		},
		["Wait"] = new ItemDescription
		{
			Format = "wait(seconds)",
			Summary = "적은 초만큼 기다립니다.",
			Slots = new[] { "seconds : 기다릴 시간(초) | 숫자" },
		},
		[PredefinedFunctionIds.PyIsValidOrb] = new ItemDescription
		{
			Format = "is_valid_orb(orb)",
			Summary = "orb가 아직 쓰지 않은 마나 구슬이면 True를 돌려줍니다.",
			Purpose = "구슬 스킬에 넘기기 전에 다 쓴 구슬인지 확인하고 싶을 때 씁니다.",
			ExampleCode = "if is_valid_orb(orb):\n    orb_shot(get_bug(), orb)",
			Slots = new[] { "orb : 확인할 값 | 아무 값" },
			Returns = "True 또는 False",
			Caution = "다 쓴 구슬도 variable에는 그대로 남으므로 None과 비교해서는 알 수 없습니다. 다 쓴 구슬, None, 구슬이 아닌 값은 False입니다.",
		},

		// 문법 노드 (스킬 트리의 문법 탭) ----------------------------------------------
		["syntax.variable"] = new ItemDescription { Summary = "값에 이름을 붙여 담아 두고 다시 씁니다. global, del도 씁니다." },
		["syntax.arithmetic"] = new ItemDescription { Summary = "+ - * / // % 로 값을 계산합니다." },
		["syntax.logic"] = new ItemDescription { Summary = "and or not 으로 여러 조건을 하나로 합칩니다." },
		["syntax.list"] = new ItemDescription { Summary = "여러 값을 list 하나에 모아 담습니다." },
	};
}
