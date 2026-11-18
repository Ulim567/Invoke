using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 상점 카드에 보여 줄 항목의 이름, 한글 이름, 표기, 색 (#534). 블록 편집 화면과 같은 글자·색을 쓴다.
/// <para>
/// - 문법 블록: 이름(If, Array Get 등)과 형식(설명 표의 Format, 예: if (condition) { body })<br/>
/// - 함수: 함수 탭과 같은 영문 이름, 한글 이름, 원형 (BlockReference)<br/>
/// - 내 캐릭터 언어(Python이면 Python 표기)로 보인다.
/// </para>
/// </summary>
public static class ShopItemInfo
{
	// 문법 블록의 카드 이름 (Notion 「블록 표」의 이름). Python 블록은 Python 낱말
	static readonly Dictionary<string, string> SyntaxTitles = new Dictionary<string, string>
	{
		["If"] = "If",
		["ElseIf"] = "Else If",
		["Else"] = "Else",
		["For"] = "For",
		["While"] = "While",
		["Break"] = "Break",
		["Continue"] = "Continue",
		["Declare"] = "Declare",
		["Assign"] = "Assign",
		["ArrayDeclare"] = "Array",
		["ArrayGet"] = "Array Get",
		["ArraySet"] = "Array Set",
		["Arithmetic"] = "Arithmetic",
		["Compare"] = "Compare",
		["Logic"] = "Logic",
		["Not"] = "Not",
		["Return"] = "Return",
		["Global"] = "global",
		["Del"] = "del",
		["ListNew"] = "list",
		["ListAppend"] = "append",
		["ListPop"] = "pop",
		["ListLen"] = "len",
	};

	/// <summary>카드 제목. 문법 블록은 블록 이름, 함수는 영문 이름(thrust, malloc).</summary>
	public static string TitleOf(string itemId)
	{
		if (string.IsNullOrEmpty(itemId)) return "";
		if (SyntaxTitles.TryGetValue(itemId, out string title)) return title;
		if (TryBuiltin(itemId, out var builtin)) return builtin.Name;
		return BlockLabels.PredefinedFunctionName(itemId);
	}

	/// <summary>함수의 한글 이름 (검 찌르기, 메모리 할당). 문법 블록과 한글 이름이 없는 함수는 빈 글자.</summary>
	public static string KoreanNameOf(string itemId)
	{
		if (string.IsNullOrEmpty(itemId) || SyntaxTitles.ContainsKey(itemId)) return "";
		if (TryBuiltin(itemId, out var builtin)) return builtin.KoreanName;
		return BlockReference.KoreanNameOf(itemId);
	}

	/// <summary>
	/// 카드 제목·구매 확인 창·알림에 쓰는 이름. 한글 이름이 있는 함수는 한글 이름(상처 가르기), 나머지는 TitleOf(If, malloc).
	/// 함수의 영문 이름은 카드 둘째 줄의 원형에 들어 있으므로 따로 적지 않는다.
	/// </summary>
	public static string DisplayNameOf(string itemId)
	{
		string korean = KoreanNameOf(itemId);
		return string.IsNullOrEmpty(korean) ? TitleOf(itemId) : korean;
	}

	/// <summary>코드 색을 칠한 표기 (BlockReference.ColorCode). 문법 블록은 형식, 함수는 원형. 없으면 빈 글자.</summary>
	public static string NotationOf(string itemId)
	{
		if (string.IsNullOrEmpty(itemId)) return "";
		string code;
		if (SyntaxTitles.ContainsKey(itemId)) code = ItemDescriptions.Get(itemId)?.Format;
		else if (TryBuiltin(itemId, out var builtin)) code = builtin.Prototype;
		else code = BlockReference.PrototypeOf(itemId);
		return BlockReference.ColorCode(code ?? "");
	}

	/// <summary>블록 분류 색 (BlockLabels). 함수는 함수 색.</summary>
	public static Color ColorOf(string itemId) => BlockLabels.ColorOf(new BlockData { Kind = KindOf(itemId) });

	/// <summary>문법 블록(If, While 등)이면 true, 함수(검 찌르기, malloc 등)면 false. 획득 표시가 「블록」, 「함수」 낱말을 고를 때 쓴다.</summary>
	public static bool IsSyntaxItem(string itemId) => !string.IsNullOrEmpty(itemId) && SyntaxTitles.ContainsKey(itemId);

	/// <summary>구매 확인 창의 「~ 블록」, 「~ 함수」 낱말.</summary>
	public static string KindWordOf(int slot) => ShopRules.IsSyntaxSlot(slot) ? "블록" : "함수";

	/// <summary>재료의 화면 이름. 모르는 재료는 ID 그대로.</summary>
	public static string MaterialNameOf(string materialId) => materialId switch
	{
		ShopRules.BugFragment => "버그 조각",
		ShopRules.BossCore => "보스 코어",
		_ => materialId,
	};

	/// <summary>
	/// 가격 한 줄의 글자 (「버그 조각 <b>8</b>」). 카드·구매 확인 창·새로고침 버튼이 함께 쓴다.
	/// 게임 글꼴 Galmuri11에는 ×, → 글자가 없어 빈칸으로 보이므로 기호 없이 숫자만 적는다.
	/// </summary>
	public static string PriceLineOf(ShopPrice price) => $"{MaterialNameOf(price.MaterialId)} <b>{price.Count}</b>";

	// 항목 ID의 블록 종류. If·ElseIf·Else는 조건문, 사전 정의 함수는 함수 블록
	static BlockKind KindOf(string itemId)
	{
		if (itemId == "If" || itemId == "ElseIf" || itemId == "Else") return BlockKind.Conditional;
		return Enum.TryParse(itemId, out BlockKind kind) ? kind : BlockKind.Function;
	}

	// 함수 탭의 wait, malloc, free (각자의 블록 종류라서 이름과 원형이 BlockReference.BuiltinOf에 있다)
	static bool TryBuiltin(string itemId, out (string Name, string KoreanName, string Prototype) builtin)
	{
		builtin = default;
		if (!Enum.TryParse(itemId, out BlockKind kind)) return false;
		var found = BlockReference.BuiltinOf(kind);
		if (found == null) return false;
		builtin = found.Value;
		return true;
	}
}
