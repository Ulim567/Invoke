using System.Collections.Generic;

/// <summary>
/// 블록·함수·type의 플레이어용 설명을 모은 표. 설명은 이 표 한 곳에만 적는다.
/// 참고 카드, 설명창, 스킬 트리 설명이 모두 여기를 읽으므로, 문구를 고치고 Play하면 바로 화면에 보인다.
/// <para>
/// - 이 파일: 문법 블록 20개, type 5개, 기본 함수(get_bug, is_valid_target), 옛 문법 노드 10개, 파라미터 뜻<br/>
/// - ItemDescriptions.Skills.cs: 스킬 24개 (박진우)<br/>
/// - 문구의 원본은 Notion 「블록 표」(11/15 확정). 용어는 영어(type, variable, condition, body ...), 문장은 하십시오체
/// </para>
/// <para>
/// key (Get에 넘기는 값)<br/>
/// - 블록: 블록·함수 개수와 같은 항목 ID (BlockUsageCounter.KeyOf). If, ElseIf, Else, For, Declare, ArrayDeclare, Wait, Malloc ...<br/>
/// - 사전 정의 함수: 고정 ID (c.thrust, input.get_bug)<br/>
/// - type: type. + 이름 (type.float, type.NULL)<br/>
/// - 옛 문법 노드: 노드 ID (syntax.if). 스킬 트리를 하나로 합치면 지운다
/// </para>
/// </summary>
public static partial class ItemDescriptions
{
	/// <summary>
	/// key의 설명. 없으면 null. key는 위 요약의 규칙을 따른다.
	/// </summary>
	public static ItemDescription Get(string key)
	{
		if (string.IsNullOrEmpty(key)) return null;
		// 내 캐릭터 언어의 표를 먼저 본다 (#532, ItemDescriptions.Python.cs)
		Dictionary<string, ItemDescription> languageEntries = EntriesOf(LanguageFamilies.Of(PlayerCharacter.LocalLanguage));
		if (languageEntries != null && languageEntries.TryGetValue(key, out ItemDescription byLanguage)) return byLanguage;
		if (Entries.TryGetValue(key, out ItemDescription found)) return found;
		return SkillEntries.TryGetValue(key, out found) ? found : null;
	}

	/// <summary>
	/// 블록 하나의 설명. 내가 만든 함수를 부르는 블록처럼 설명이 없으면 null.
	/// </summary>
	public static ItemDescription Of(BlockData block) => Get(BlockUsageCounter.KeyOf(block));

	/// <summary>
	/// 파라미터 이름의 뜻 (무엇을 넣는지). 사전 정의 함수가 슬롯(Slots)을 적지 않았을 때 쓴다. 없으면 빈 글자.
	/// 코드에서는 파라미터에 무엇을 넣는지 알 수 없어서 이름마다 적어 둔다.
	/// </summary>
	public static string ParameterMeaningOf(string parameterName) =>
		parameterName != null && ParameterMeanings.TryGetValue(parameterName, out string meaning) ? meaning : "";

	static readonly Dictionary<string, string> ParameterMeanings = new Dictionary<string, string>
	{
		["target"] = "몬스터 한 명입니다. get_bug()를 넣거나, get_bug()를 담아 둔 Target 변수를 넣습니다.",
		["targets"] = "공격할 몬스터 여러 명입니다. 슬롯에서 시전하면 여러 명을 고르게 해 줍니다.",
		["sword"] = "쓸 소환검의 포인터입니다. summon_sword()가 돌려준 값을 넣습니다.",
		["orb"] = "쓸 마나 구슬입니다. orb_create()가 돌려준 값을 담아 둔 variable이나 list의 원소를 넣습니다.",
		["orbs"] = "쓸 마나 구슬들을 담은 list입니다. 다 쓴 구슬은 건너뜁니다.",
	};

	static readonly Dictionary<string, ItemDescription> Entries = new Dictionary<string, ItemDescription>
	{
		// 제어 ----------------------------------------------------------------
		["If"] = new ItemDescription
		{
			Format = "if (condition) { body }",
			Summary = "condition이 true일 때만 body를 실행합니다.",
			Purpose = "조건에 따라 실행할지 말지를 정하고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 살아 있을 때만 검 찌르기를 쓰도록 만들 수 있습니다.",
			ExampleCode = "if (is_valid_target(get_bug())) { thrust(get_bug()); }",
			ExampleResult = "고른 몬스터가 살아 있으면 검 찌르기를 쓰고, 그렇지 않으면 아무것도 하지 않고 다음 블록으로 넘어갑니다.",
			Slots = new[]
			{
				"condition : true면 body를 실행 | bool",
				"body : condition이 true일 때 실행할 블록 | statement",
			},
			Caution = "condition이 비어 있으면 compile error가 발생합니다. true, false를 직접 적으려면 bool을 해금해야 합니다.",
		},
		["ElseIf"] = new ItemDescription
		{
			Format = "else if (condition) { body }",
			Summary = "앞의 condition이 모두 false일 때 이 condition을 검사하고, true면 body를 실행합니다.",
			Purpose = "경우를 셋 이상으로 나누어 각각 다른 일을 하고 싶을 때 씁니다.",
			Example = "예를 들면, count 값에 따라 다른 스킬을 쓰도록 만들 수 있습니다.",
			ExampleCode = "int count = 2; if (count > 2) { thrust(get_bug()); } else if (count > 0) { sword_throw(get_bug()); }",
			ExampleResult = "count가 2이므로 첫 condition count > 2는 false이고, 둘째 condition count > 0은 true입니다. 그래서 검 던지기를 씁니다.",
			Slots = new[]
			{
				"condition : 앞의 condition이 모두 false일 때 검사 | bool",
				"body : condition이 true일 때 실행할 블록 | statement",
			},
			Caution = "If 또는 Else If 바로 아래에만 놓을 수 있습니다.",
		},
		["Else"] = new ItemDescription
		{
			Format = "else { body }",
			Summary = "앞의 condition이 모두 false일 때 body를 실행합니다.",
			Purpose = "앞의 조건이 모두 맞지 않을 때 할 일을 정하고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 살아 있으면 찌르고, 그렇지 않으면 기다리도록 만들 수 있습니다.",
			ExampleCode = "if (is_valid_target(get_bug())) { thrust(get_bug()); } else { wait(0.5); }",
			ExampleResult = "고른 몬스터가 살아 있으면 검 찌르기를 쓰고, 그렇지 않으면 0.5초 기다립니다.",
			Slots = new[] { "body : 앞의 condition이 모두 false일 때 실행할 블록 | statement" },
			Caution = "If 또는 Else If 바로 아래에만 놓을 수 있습니다. Else 바로 아래에는 Else If, Else를 붙일 수 없습니다.",
		},
		["For"] = new ItemDescription
		{
			Format = "for (int i = start; i < end; i++) { body }",
			Summary = "start부터 end 직전까지 i를 1씩 늘리며 body를 반복합니다.",
			Purpose = "같은 블록을 정해진 횟수만큼 반복하고 싶을 때 씁니다.",
			Example = "예를 들면, 검 찌르기를 세 번 쓰도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 3; i++) { thrust(get_bug()); }",
			ExampleResult = "i가 0, 1, 2일 때 한 번씩, 검 찌르기를 모두 세 번 씁니다.",
			Slots = new[]
			{
				"i : 반복 횟수를 세는 variable의 이름 | name",
				"start : i의 처음 값 | int",
				"end : i가 이 값에 닿으면 반복을 끝냄 | int",
				"body : 반복할 블록 | statement",
			},
			Caution = "start가 end 이상이면 body를 한 번도 실행하지 않습니다.",
		},
		["While"] = new ItemDescription
		{
			Format = "while (condition) { body }",
			Summary = "condition이 true인 동안 body를 반복합니다.",
			Purpose = "몇 번 반복할지 정해져 있지 않고, 어떤 조건이 맞는 동안 반복하고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 죽을 때까지 계속 찌르도록 만들 수 있습니다.",
			ExampleCode = "while (is_valid_target(get_bug())) { thrust(get_bug()); }",
			ExampleResult = "고른 몬스터가 살아 있는 동안 검 찌르기를 계속 쓰고, 몬스터가 죽으면 반복을 끝냅니다.",
			Slots = new[]
			{
				"condition : true인 동안 body를 반복 | bool",
				"body : 반복할 블록 | statement",
			},
			Caution = "condition이 계속 true이면 반복이 끝나지 않습니다. Break로 빠져나올 수 있습니다.",
		},
		["Break"] = new ItemDescription
		{
			Format = "break;",
			Summary = "가장 안쪽 반복을 바로 끝냅니다.",
			Purpose = "반복을 끝까지 하지 않고 중간에 멈추고 싶을 때 씁니다.",
			Example = "예를 들면, 다섯 번 찌르다가 몬스터가 죽으면 멈추도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 5; i++) { if (!is_valid_target(get_bug())) { break; } thrust(get_bug()); }",
			ExampleResult = "몬스터가 살아 있는 동안 최대 다섯 번 찌릅니다. 중간에 몬스터가 죽으면 남은 반복을 하지 않고 끝냅니다.",
			Caution = "For, While의 body 안에서만 쓸 수 있습니다. 밖에 놓으면 compile error가 발생합니다.",
		},
		["Continue"] = new ItemDescription
		{
			Format = "continue;",
			Summary = "이번 반복의 나머지를 건너뛰고 다음 반복으로 넘어갑니다.",
			Purpose = "반복 가운데 어떤 차례에서만 나머지 블록을 건너뛰고 싶을 때 씁니다.",
			Example = "예를 들면, 다섯 번 가운데 셋째 차례에만 찌르지 않도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 5; i++) { if (i == 2) { continue; } thrust(get_bug()); }",
			ExampleResult = "i가 2일 때만 찌르기를 건너뛰어, 모두 네 번 찌릅니다.",
			Caution = "For, While의 body 안에서만 쓸 수 있습니다. 밖에 놓으면 compile error가 발생합니다.",
		},

		// 변수 ----------------------------------------------------------------
		["Declare"] = new ItemDescription
		{
			Format = "type name = value;",
			Summary = "type과 이름을 골라 variable을 만들고, 처음 값을 담습니다.",
			Purpose = "값에 이름을 붙여 기억해 두었다가 나중에 다시 쓰고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터를 기억해 두고 같은 몬스터에게 스킬 두 개를 쓰도록 만들 수 있습니다.",
			ExampleCode = "Target bug = get_bug(); thrust(bug); sword_throw(bug);",
			ExampleResult = "고른 몬스터를 bug에 담아 두고, 그 몬스터에게 검 찌르기와 검 던지기를 차례로 씁니다.",
			Slots = new[]
			{
				"type : 만들 variable의 type | type",
				"name : 만들 variable의 이름 | name",
				"value : variable에 처음 담을 값 | 고른 type과 같은 type",
			},
			Caution = "처음에는 int, Target만 고를 수 있습니다. 나머지 type은 스킬 트리에서 해금합니다. value를 비워 두면 그 type의 기본값으로 시작합니다. 같은 name을 두 번 선언하면 compile error가 발생합니다.",
		},
		["Assign"] = new ItemDescription
		{
			Format = "variable = value;",
			Summary = "이미 만든 variable에 새 값을 담습니다.",
			Purpose = "variable에 담긴 값을 바꾸고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터를 찌른 뒤 다른 몬스터로 바꿔 다시 찌르도록 만들 수 있습니다.",
			ExampleCode = "Target bug = get_bug(); thrust(bug); bug = get_bug(); thrust(bug);",
			ExampleResult = "처음 고른 몬스터를 찌른 뒤, bug를 새로 고른 몬스터로 바꿔 그 몬스터를 찌릅니다.",
			Slots = new[]
			{
				"variable : 값을 바꿀 variable | variable",
				"value : variable에 담을 값 | variable과 같은 type",
			},
			Caution = "선언하지 않은 variable에 담으면 compile error가 발생합니다.",
		},
		["ArrayDeclare"] = new ItemDescription
		{
			Format = "type name[length];",
			Summary = "같은 type의 값 여러 개를 담는 array를 만듭니다.",
			Purpose = "같은 type의 값 여러 개를 한 이름으로 묶어 기억하고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터 세 마리를 한꺼번에 기억하도록 만들 수 있습니다.",
			ExampleCode = "Target bugs[3]; bugs[0] = get_bug(); bugs[1] = get_bug(); bugs[2] = get_bug();",
			ExampleResult = "몬스터 세 마리를 골라 bugs의 0번, 1번, 2번 칸에 담습니다.",
			Slots = new[]
			{
				"type : array에 담을 값의 type | type",
				"name : 만들 array의 이름 | name",
				"length : 담을 수 있는 값의 개수 | int",
			},
			Caution = "length에는 1 이상의 정수를 직접 적어야 합니다. index는 0부터 length - 1까지입니다.",
		},
		["ArrayGet"] = new ItemDescription
		{
			Format = "array[index]",
			Returns = "array의 원소 type",
			Summary = "array의 index번째 칸에 담긴 값을 꺼냅니다.",
			Purpose = "array에 담긴 값 가운데 하나를 꺼내 쓰고 싶을 때 씁니다.",
			Example = "예를 들면, bugs에 담아 둔 몬스터를 차례로 찌르도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 3; i++) { thrust(bugs[i]); }",
			ExampleResult = "bugs의 0번, 1번, 2번 칸에 담긴 몬스터를 차례로 찌릅니다.",
			Slots = new[]
			{
				"array : 값을 읽을 array | array",
				"index : 읽을 칸의 번호 | int",
			},
			Caution = "index는 0부터 셉니다.",
		},
		["ArraySet"] = new ItemDescription
		{
			Format = "array[index] = value;",
			Summary = "array의 index번째 칸에 값을 담습니다.",
			Purpose = "array의 한 칸에 값을 넣거나 바꾸고 싶을 때 씁니다.",
			Example = "예를 들면, 새로 고른 몬스터를 bugs의 첫 칸에 담을 수 있습니다.",
			ExampleCode = "bugs[0] = get_bug();",
			ExampleResult = "고른 몬스터를 bugs의 0번 칸에 담습니다. 그 칸에 전에 담겨 있던 값은 바뀝니다.",
			Slots = new[]
			{
				"array : 값을 담을 array | array",
				"index : 담을 칸의 번호 | int",
				"value : 그 칸에 담을 값 | array의 원소와 같은 type",
			},
			Caution = "index는 0부터 셉니다.",
		},

		// 값 ------------------------------------------------------------------
		["Arithmetic"] = new ItemDescription
		{
			Format = "left operator right",
			Returns = "int 또는 float",
			Summary = "두 값을 계산합니다.",
			Purpose = "값을 더하거나 빼거나 곱하거나 나누어 새 값을 만들고 싶을 때 씁니다.",
			Example = "예를 들면, 스킬을 쓴 횟수를 셀 수 있습니다.",
			ExampleCode = "int count = 0; thrust(get_bug()); count = count + 1; sword_throw(get_bug()); count = count + 1;",
			ExampleResult = "스킬을 두 번 쓰고, count는 2가 됩니다.",
			Slots = new[]
			{
				"left : 계산할 첫째 값 | int float char",
				"right : 계산할 둘째 값 | int float char",
				"operator : 계산 방법 | + - * / %",
			},
			Caution = "int끼리 나누면 소수점 아래를 버립니다(7 / 2 = 3). 0으로 나누면 runtime error가 발생합니다.",
		},
		["Compare"] = new ItemDescription
		{
			Format = "left operator right",
			Returns = "bool",
			Summary = "두 값을 비교해 true 또는 false를 만듭니다.",
			Purpose = "두 값을 비교한 결과를 condition으로 쓰고 싶을 때 씁니다.",
			Example = "예를 들면, 반복 차례에 따라 다른 스킬을 쓰도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 5; i++) { if (i < 2) { thrust(get_bug()); } else { sword_throw(get_bug()); } }",
			ExampleResult = "처음 두 번은 검 찌르기를, 나머지 세 번은 검 던지기를 씁니다.",
			Slots = new[]
			{
				"left : 비교할 첫째 값 | int float char bool Target Sword*",
				"right : 비교할 둘째 값 | left와 같은 type",
				"operator : 비교 방법 | > < == != >= <=",
			},
			Caution = "==는 같은지 비교합니다. variable에 값을 담는 것은 Assign입니다. string은 비교할 수 없습니다.",
		},
		["Logic"] = new ItemDescription
		{
			Format = "left operator right",
			Returns = "bool",
			Summary = "두 condition을 하나로 합칩니다.",
			Purpose = "두 condition을 함께 따지고 싶을 때 씁니다.",
			Example = "예를 들면, 반복 차례가 어떤 범위 안일 때만 찌르도록 만들 수 있습니다.",
			ExampleCode = "for (int i = 0; i < 5; i++) { if (i > 0 && i < 4) { thrust(get_bug()); } }",
			ExampleResult = "i가 1, 2, 3일 때만, 모두 세 번 찌릅니다.",
			Slots = new[]
			{
				"left : 합칠 첫째 condition | bool",
				"right : 합칠 둘째 condition | bool",
				"operator : 합치는 방법 | && ||",
			},
			Caution = "&&는 둘 다 true일 때만 true, ||는 하나라도 true면 true입니다.",
		},
		["Not"] = new ItemDescription
		{
			Format = "!value",
			Returns = "bool",
			Summary = "true와 false를 뒤집습니다.",
			Purpose = "condition을 반대로 따지고 싶을 때 씁니다.",
			Example = "예를 들면, 고른 몬스터가 없을 때만 기다리도록 만들 수 있습니다.",
			ExampleCode = "if (!is_valid_target(get_bug())) { wait(1); }",
			ExampleResult = "고른 몬스터가 없거나 이미 죽었으면 1초 기다립니다.",
			Slots = new[] { "value : 뒤집을 condition | bool" },
		},

		// 함수 탭의 메모리·기본 함수, return ------------------------------------------
		["Malloc"] = new ItemDescription
		{
			Format = "malloc(type)",
			Returns = "Sword*",
			Summary = "heap에 소환검을 만들고, 그 소환검을 가리키는 pointer를 만듭니다.",
			Purpose = "소환검을 만들고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검을 하나 만들어 소환검 찌르기에 쓸 수 있습니다.",
			ExampleCode = "Sword* s = malloc(Sword); summoned_thrust(get_bug(), s);",
			ExampleResult = "소환검 하나가 생기고, 그 소환검으로 고른 몬스터를 찌릅니다.",
			Slots = new[] { "type : heap에 만들 대상 | Sword" },
			Caution = "소환검은 최대 5개입니다. 더 만들 수 없으면 NULL을 만듭니다. 다 쓴 소환검은 Free로 없애야 heap 자리가 돌아옵니다.",
		},
		["Free"] = new ItemDescription
		{
			Format = "free(pointer);",
			Summary = "pointer가 가리키는 소환검을 없애고 heap 자리를 돌려줍니다.",
			Purpose = "다 쓴 소환검을 없애고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검으로 찌른 뒤 바로 그 소환검을 없앨 수 있습니다.",
			ExampleCode = "Sword* s = malloc(Sword); summoned_thrust(get_bug(), s); free(s);",
			ExampleResult = "소환검으로 찌른 뒤 그 소환검을 없앱니다. 소환검 수가 하나 줄어 새 소환검을 만들 수 있습니다.",
			Slots = new[] { "pointer : 없앨 소환검을 가리키는 pointer | Sword*" },
			Caution = "이미 없앤 pointer는 다시 쓸 수 없습니다.",
		},
		["Return"] = new ItemDescription
		{
			Format = "return value;",
			Summary = "function을 끝내고, 값이 있으면 부른 쪽에 돌려줍니다.",
			Purpose = "내가 만든 function이 값을 돌려주게 하거나, function을 중간에 끝내고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터를 골라 돌려주는 function을 만들 수 있습니다.",
			ExampleCode = "Target pick() { return get_bug(); }를 만든 뒤 thrust(pick());",
			ExampleResult = "pick이 고른 몬스터를 돌려주고, 그 몬스터를 찌릅니다.",
			Slots = new[] { "value : 부른 쪽에 돌려줄 값 | function의 return type과 같은 type" },
			Caution = "return type이 void인 function에서는 value를 비워 둡니다. return type이 void가 아닌 function은 모든 길이 Return으로 끝나야 합니다.",
		},
		["Wait"] = new ItemDescription
		{
			Format = "wait(seconds);",
			Summary = "적은 초만큼 기다린 뒤 다음 블록을 실행합니다.",
			Purpose = "다음 블록을 실행하기 전에 시간을 두고 싶을 때 씁니다.",
			Example = "예를 들면, 스킬과 스킬 사이에 0.5초 틈을 둘 수 있습니다.",
			ExampleCode = "thrust(get_bug()); wait(0.5); sword_throw(get_bug());",
			ExampleResult = "검 찌르기를 쓰고 0.5초 뒤에 검 던지기를 씁니다.",
			Slots = new[] { "seconds : 기다릴 시간(초) | int float" },
			Caution = "float을 해금하기 전에도 seconds에는 소수를 적을 수 있습니다.",
		},

		// 기본 함수 (이우림) ---------------------------------------------------------
		[PredefinedFunctionIds.GetBug] = new ItemDescription
		{
			Summary = "플레이어가 고른 몬스터 한 명을 대상으로 돌려줍니다.",
			Returns = "플레이어가 고른 몬스터입니다. 고를 몬스터가 없으면 \"대상 없음\"입니다.",
			Purpose = "Target을 받는 스킬(thrust, sword_throw 등)에 대상을 넘겨야 할 때 씁니다. 스킬의 target 파라미터에 바로 넣거나, Target variable에 담아 두고 넣습니다.",
			Example = "예를 들면, 고른 몬스터에게 검 찌르기를 쓸 수 있습니다.",
			ExampleCode = "thrust(get_bug());",
			ExampleResult = "슬롯 키를 누른 채 좌우 화살표 키로 몬스터를 고르고 키를 떼면, 그 몬스터를 찌릅니다.",
			Caution = "부를 때마다 새로 고르므로, 같은 몬스터를 여러 번 쓰려면 Target variable에 담아 둡니다. 슬롯 키를 이미 뗀 뒤에 실행되면 가장 가까운 몬스터를 고릅니다. 고를 몬스터가 없으면 \"대상 없음\"이 되고, 그 값을 스킬에 넘기면 runtime error가 발생합니다.",
		},
		[PredefinedFunctionIds.IsValidTarget] = new ItemDescription
		{
			Summary = "대상이 valid한지 알려 줍니다. valid는 대상 몬스터가 존재하고 살아 있어서, 스킬에 넘겨도 된다는 뜻입니다.",
			Returns = "스킬에 넘길 수 있으면 true, \"대상 없음\"이거나 죽었거나 없어진 몬스터면 false입니다.",
			Purpose = "Target variable에 담아 둔 몬스터를 스킬에 넘기기 전에, 그 몬스터가 아직 살아 있는지 확인할 때 씁니다.",
			Example = "예를 들면, 담아 둔 몬스터가 죽었으면 새 몬스터를 고르도록 만들 수 있습니다.",
			ExampleCode = "Target bug = get_bug(); thrust(bug); if (!is_valid_target(bug)) { bug = get_bug(); } thrust(bug);",
			ExampleResult = "처음 고른 몬스터가 찌르기로 죽었으면 새 몬스터를 골라 찌르고, 살아 있으면 같은 몬스터를 한 번 더 찌릅니다.",
			Slots = new[] { "target : 확인할 대상 | Target" },
			Caution = "true이면 그 대상을 스킬에 넘겨도 대상 때문에 runtime error가 발생하지 않습니다. Target은 pointer가 아니므로 NULL과 비교하지 않고 이 함수로 확인합니다.",
		},
		[PredefinedFunctionIds.Printf] = new ItemDescription
		{
			Summary = "값 하나를 화면 왼쪽 아래에 출력합니다.",
			Purpose = "화면에 보이지 않는 값(variable, function이 돌려준 값)을 확인할 때 씁니다.",
			Example = "예를 들면, variable에 담긴 값을 확인할 수 있습니다.",
			ExampleCode = "int n = 3; printf(n);",
			ExampleResult = "Q 슬롯에서 쓰면 화면 왼쪽 아래에 [Q] n = 3이 3초 동안 보입니다.",
			Slots = new[] { "value : 출력할 값 | 아무 값" },
			Caution = "variable이나 expression을 넣으면 n = 3처럼 이름과 값이 함께 보이고, 값을 직접 적으면 값만 보입니다. 출력은 최대 5줄까지 쌓이고, 내 화면에만 보입니다.",
		},
		// 자세 함수 (#538) ---------------------------------------------------------
		[PredefinedFunctionIds.InitStance] = new ItemDescription
		{
			Summary = "자세를 1~100 사이의 무작위 숫자로 다시 정합니다.",
			Returns = "새로 정한 자세 숫자입니다.",
			// 규칙 이름·수치는 적지 않는다. 규칙은 처음부터 모두 적용되고, 내용은 스킬 트리에서 해금해야 알 수 있다 (이우림 결정 11/18, stance.md)
			Purpose = "자세 숫자에 따라 효과가 붙습니다. 효과를 정하는 규칙은 여덟 가지이며, 규칙의 내용은 스킬 트리에서 해금하면 알 수 있습니다. 원하는 자세가 나올 때까지 다시 뽑을 때 씁니다.",
			Example = "예를 들면, 자세를 정하고 그 숫자를 확인할 수 있습니다.",
			ExampleCode = "int n = init_stance(); printf(n);",
			ExampleResult = "2.5초 뒤 자세가 정해지고, 화면 왼쪽 아래에 n = 37처럼 자세 숫자가 보입니다.",
			Caution = "숫자를 받기까지 2.5초가 걸리고, 쿨타임은 3초입니다. 자세 숫자는 화면에 표시되지 않으므로 printf로 출력해서 확인합니다. 전투 장소에 들어가면 자세가 없어집니다.",
		},
		[PredefinedFunctionIds.GetStance] = new ItemDescription
		{
			Summary = "지금 자세 숫자를 돌려줍니다.",
			Returns = "지금 자세 숫자입니다. 자세가 없으면 0입니다.",
			Purpose = "다른 슬롯이나 다른 function에서 지금 자세를 확인하고, 자세에 맞는 스킬을 고를 때 씁니다.",
			Example = "예를 들면, 자세가 홀수일 때만 검 찌르기를 쓸 수 있습니다.",
			ExampleCode = "if (get_stance() % 2 == 1) { thrust(get_bug()); }",
			ExampleResult = "자세가 홀수이면 고른 몬스터를 찌르고, 짝수이거나 자세가 없으면 아무것도 하지 않습니다.",
			Caution = "자세를 바꾸지 않으므로 시간이 더 들지 않습니다. 전투 장소에 막 들어와 init_stance를 부르기 전이면 0입니다.",
		},

		// type ----------------------------------------------------------------
		// 아직 화면에 보이지 않는다. 해금 설명 카드(다음 이슈)가 type 노드를 해금할 때 보여 준다
		["type.float"] = new ItemDescription
		{
			Format = "0.5",
			Summary = "소수가 있는 숫자를 담는 type입니다.",
			Purpose = "0.5처럼 정수가 아닌 숫자를 variable에 담거나 계산하고 싶을 때 씁니다.",
			Example = "예를 들면, 기다릴 시간을 variable에 담아 두고 쓸 수 있습니다.",
			ExampleCode = "float delay = 0.5; thrust(get_bug()); wait(delay); sword_throw(get_bug());",
			ExampleResult = "검 찌르기를 쓰고 0.5초 뒤에 검 던지기를 씁니다.",
			Slots = new[] { "넣을 수 있는 빈칸: Arithmetic의 left와 right, Wait의 seconds, Compare의 left와 right, float variable" },
			Caution = "For의 start와 end, array의 index에는 넣을 수 없습니다. int variable에 float을 담으면 소수점 아래를 버립니다.",
		},
		["type.char"] = new ItemDescription
		{
			Format = "'a'",
			Summary = "글자 하나를 담는 type입니다.",
			Purpose = "글자 하나를 variable에 담거나, 글자를 숫자처럼 계산하고 싶을 때 씁니다.",
			Example = "예를 들면, 글자를 다음 글자로 바꿀 수 있습니다.",
			ExampleCode = "char c = 'a'; c = c + 1;",
			ExampleResult = "c에는 'b'가 담깁니다.",
			Slots = new[] { "넣을 수 있는 빈칸: int와 float을 넣을 수 있는 빈칸 모두, char variable" },
			Caution = "작은따옴표 안에 글자 하나만 적을 수 있습니다. char는 숫자로 계산되어, 예를 들어 'a'는 97, 'z'는 122입니다.",
		},
		["type.bool"] = new ItemDescription
		{
			Format = "true, false",
			Summary = "true 또는 false를 담는 type입니다.",
			Purpose = "조건의 결과를 variable에 담아 두거나, true나 false를 직접 적고 싶을 때 씁니다.",
			Example = "예를 들면, 몬스터가 살아 있는지를 variable에 담아 두고 쓸 수 있습니다.",
			ExampleCode = "bool alive = is_valid_target(get_bug()); if (alive) { thrust(get_bug()); }",
			ExampleResult = "고른 몬스터가 살아 있으면 alive에 true가 담기고, 검 찌르기를 씁니다.",
			Slots = new[] { "넣을 수 있는 빈칸: If, Else If, While의 condition, Logic과 Not의 빈칸, Compare의 left와 right, bool variable" },
			Caution = "bool을 해금하기 전에도 Compare, Logic, Not, is_valid_target()의 결과는 condition에 넣을 수 있습니다. bool을 해금해야 할 수 있는 일은 true와 false를 직접 적기, bool variable 만들기, bool을 받거나 돌려주는 function 만들기입니다.",
		},
		["type.string"] = new ItemDescription
		{
			Format = "\"invoke\"",
			Summary = "여러 글자로 된 글을 담는 type입니다.",
			Purpose = "글을 variable에 담아 두고 싶을 때 씁니다.",
			Example = "예를 들면, 이름을 variable에 담을 수 있습니다.",
			ExampleCode = "string name = \"invoke\";",
			ExampleResult = "name에 \"invoke\"라는 글이 담깁니다.",
			Slots = new[] { "넣을 수 있는 빈칸: string variable, return type이 string인 function의 Return, string parameter" },
			Caution = "큰따옴표 안에 적습니다. string은 Compare로 비교하거나 Arithmetic으로 계산할 수 없습니다.",
		},
		["type.NULL"] = new ItemDescription
		{
			Format = "NULL",
			Summary = "아무것도 가리키지 않는 pointer입니다.",
			Purpose = "pointer가 비어 있는지 확인하거나, pointer를 비워 두고 싶을 때 씁니다.",
			Example = "예를 들면, 소환검을 만들지 못했을 때를 따로 처리할 수 있습니다.",
			ExampleCode = "Sword* s = malloc(Sword); if (s == NULL) { wait(1); } else { summoned_thrust(get_bug(), s); }",
			ExampleResult = "소환검이 이미 5개라서 만들지 못했으면 1초 기다리고, 만들었으면 그 소환검으로 찌릅니다.",
			Slots = new[] { "넣을 수 있는 빈칸: Sword를 받는 빈칸(Free의 pointer, 소환검 스킬의 argument), Compare의 left와 right, Sword variable" },
			Caution = "Target을 받는 빈칸에는 NULL을 넣을 수 없습니다.",
		},

		// 옛 문법 노드 (스킬 트리의 문법 탭) ----------------------------------------------
		// 지금 스킬 트리는 문법 노드 하나가 블록 여러 개를 연다. 스킬 트리를 하나로 합치면 노드가 블록마다 생기므로 이 줄들은 지운다
		["syntax.sequence"] = new ItemDescription { Summary = "블록을 위에서 아래로 하나씩 차례대로 실행합니다." },
		["syntax.variable"] = new ItemDescription { Summary = "값에 이름을 붙여 담아 두고 다시 씁니다." },
		["syntax.arithmetic"] = new ItemDescription { Summary = "+ - * / % 로 값을 계산합니다." },
		["syntax.if"] = new ItemDescription { Summary = "조건이 참일 때만 안쪽 블록을 실행합니다." },
		["syntax.logic"] = new ItemDescription { Summary = "&& || ! 로 여러 조건을 하나로 합칩니다." },
		["syntax.for"] = new ItemDescription { Summary = "정해진 횟수만큼 안쪽 블록을 반복합니다." },
		["syntax.while"] = new ItemDescription { Summary = "조건이 참인 동안 안쪽 블록을 계속 반복합니다." },
		["syntax.function"] = new ItemDescription { Summary = "블록 묶음에 이름을 붙여 두고 필요할 때 부릅니다." },
		["syntax.list"] = new ItemDescription { Summary = "같은 타입의 값 여러 개를 한 줄로 담습니다." },
		["syntax.type"] = new ItemDescription { Summary = "여러 변수를 하나로 묶은 나만의 타입(struct)을 만듭니다." },
	};
}
