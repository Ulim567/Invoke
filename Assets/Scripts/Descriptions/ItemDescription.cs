using System;
using System.Collections.Generic;

/// <summary>
/// 블록·함수·type 하나의 플레이어용 설명. 해금 설명 카드의 칸과 같은 순서다: 하는 일 → 용도 → 예시 → 슬롯 → 주의할 점.<br/>
/// 값은 ItemDescriptions 표에만 적는다. 다른 코드는 읽기만 한다. 비어 있는 칸은 화면에 보이지 않는다.
/// </summary>
public class ItemDescription
{
	/// <summary>하는 일. 한 문장. 참고 카드·설명창·스킬 트리에서 가장 먼저 보인다</summary>
	public string Summary;

	/// <summary>용도. 「~하고 싶을 때 씁니다.」처럼 일반적인 경우 한 문장</summary>
	public string Purpose;

	/// <summary>예시 상황. 「예를 들면,」으로 시작하는 한 문장. 블록을 쓰는 여러 방법 가운데 하나라는 것을 밝힌다</summary>
	public string Example;

	/// <summary>예시 코드. 한 줄 C 코드 (예: if (is_valid_target(get_bug())) { thrust(get_bug()); })</summary>
	public string ExampleCode;

	/// <summary>예시 결과. 예시 코드를 실행하면 게임에서 무슨 일이 생기는지</summary>
	public string ExampleResult;

	/// <summary>
	/// 슬롯. 한 줄에 슬롯 하나: 「이름 : 역할 | 넣을 수 있는 것」 (예: condition : true면 body를 실행 | bool).
	/// 사전 정의 함수가 비워 두면 파라미터 이름과 ItemDescriptions.ParameterMeaningOf로 만든다
	/// </summary>
	public string[] Slots;

	/// <summary>주의할 점. 실수하기 쉬운 것, 쓸 수 없는 경우</summary>
	public string Caution;

	/// <summary>형식. 블록을 C로 적은 모양 (예: if (condition) { body }). 문법 블록과 type만 적는다. 함수는 원형을 코드가 만든다</summary>
	public string Format;

	/// <summary>return 값. 값을 만드는 블록·함수만 적는다 (예: bool, 플레이어가 고른 몬스터입니다.)</summary>
	public string Returns;

	/// <summary>
	/// 슬롯 줄을 (이름, 역할 | 넣을 수 있는 것)으로 나눈다. 「 : 」가 없는 줄은 이름 없이 글만 돌려준다.
	/// </summary>
	public List<(string Name, string Text)> SlotItems()
	{
		var items = new List<(string, string)>();
		if (Slots == null) return items;
		foreach (string line in Slots)
		{
			if (string.IsNullOrEmpty(line)) continue;
			int split = line.IndexOf(SlotSeparator, StringComparison.Ordinal);
			items.Add(split < 0 ? ("", line) : (line.Substring(0, split), line.Substring(split + SlotSeparator.Length)));
		}
		return items;
	}

	/// <summary>
	/// 이름이 name인 슬롯의 「역할 | 넣을 수 있는 것」. 없으면 null. 값 칸에 마우스를 올렸을 때의 설명창이 쓴다.
	/// </summary>
	public string SlotTextOf(string name)
	{
		foreach ((string slotName, string text) in SlotItems())
			if (slotName == name) return text;
		return null;
	}

	// 슬롯 줄에서 이름과 나머지를 나누는 글자. Notion 「블록 표」의 빈칸 칸과 같은 모양
	const string SlotSeparator = " : ";
}
