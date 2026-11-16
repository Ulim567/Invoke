using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 슬롯 함수와 전역 변수 파일이 블록·함수를 항목마다 몇 개 쓰는지 센다 (#500). 가진 개수와 비교하지 않고 쓴 개수만 돌려준다.
/// 서버의 저장 검사와 편집 화면의 「남은/가진」 표시가 같은 규칙으로 세도록 둘 다 이 클래스를 쓴다.
/// <para>
/// 세는 규칙 (docs/systems/function-data.md 「개수 세기 규칙」)<br/>
/// - 슬롯 함수에서 시작한다. 슬롯에서 닿지 않는 페이지 함수는 세지 않는다.<br/>
/// - 내가 만든 함수를 부르면 그 자리에서 그 함수 안의 블록을 다시 센다. 그래서 부른 횟수만큼 센다. 호출 블록 자체는 개수를 쓰지 않는다.<br/>
/// - 재귀: 지금 세고 있는 함수를 다시 만나면 그 함수 안은 다시 세지 않는다.<br/>
/// - 반복문은 곱하지 않는다. 반복문 안의 블록도 한 번씩 센다.<br/>
/// - 전역 변수 파일의 블록은 한 번씩 센다.<br/>
/// - 없는 함수(지운 함수의 GUID)는 셀 수 없으므로 건너뛴다.
/// </para>
/// <para>
/// CodeSizeCalculator와 다른 점: 코드 영역 크기는 같은 함수를 몇 번 부르든 한 번만 세지만, 개수는 부를 때마다 다시 센다.
/// </para>
/// </summary>
public static class BlockUsageCounter
{
	/// <summary>
	/// 블록 하나가 개수를 쓰는 항목 ID. 가진 개수(DB)와 쓴 개수를 이 ID로 맞춘다.<br/>
	/// - 사전 정의 함수 호출: 고정 ID (예: c.thrust, input.get_bug)<br/>
	/// - 조건문: 갈래 이름 If, ElseIf, Else<br/>
	/// - 나머지 블록: BlockKind 이름 (예: Declare, For, Wait, Malloc)<br/>
	/// - 내가 만든 함수 호출: null (개수가 없다)
	/// </summary>
	public static string KeyOf(BlockData block)
	{
		if (block == null) return null;
		switch (block.Kind)
		{
			case BlockKind.Function:
				return block.IsUserFunction ? null : block.FunctionId;
			case BlockKind.Conditional:
				// BlockKind 하나가 if, else if, else를 모두 맡지만 기획은 셋의 개수를 따로 둔다. 갈래는 첫 칸의 Option에 있다
				// 갈래를 읽을 수 없는 조작 데이터는 셋 중 어느 것도 아닌 이름이 되어, 가진 개수가 0이므로 저장 검사에 걸린다
				if (block.Slots.Count >= 1 && Enum.TryParse(block.Slots[0].Option, out ConditionalBranch branch)) return branch.ToString();
				return block.Kind.ToString();
			default:
				return block.Kind.ToString();
		}
	}

	/// <summary>
	/// 항목 ID → 쓴 개수를 센다. 쓰지 않은 항목은 사전에 없다.
	/// </summary>
	/// <param name="slotFunctionGuids">슬롯 함수 GUID들. 세기 시작하는 곳이다</param>
	/// <param name="ownedFunctions">캐릭터가 가진 모든 함수 (슬롯 함수와 페이지 함수). 편집 중인 함수는 저장 전 내용으로 바꿔 넘긴다</param>
	/// <param name="globals">전역 변수 파일. 없으면 null</param>
	public static Dictionary<string, int> Count(
		IEnumerable<string> slotFunctionGuids,
		IEnumerable<FunctionData> ownedFunctions,
		GlobalVariablesData globals)
	{
		var functionsByGuid = new Dictionary<string, FunctionData>();
		foreach (FunctionData function in ownedFunctions)
		{
			// GUID가 겹치는 조작 데이터는 먼저 들어온 함수만 쓴다 (CodeSizeCalculator와 같음)
			if (function?.Guid != null) functionsByGuid.TryAdd(function.Guid, function);
		}

		var counter = new Counter(functionsByGuid);
		var used = new Dictionary<string, int>();
		foreach (string guid in slotFunctionGuids)
		{
			if (guid == null || !functionsByGuid.TryGetValue(guid, out FunctionData function)) continue;
			AddAll(used, counter.CountFunction(function, new HashSet<string> { guid }));
		}

		if (globals?.Declarations != null)
		{
			foreach (BlockData declaration in globals.Declarations)
			{
				if (declaration == null) continue;
				foreach (BlockData block in declaration.SelfAndInnerBlocks()) Add(used, KeyOf(block), 1);
			}
		}
		return used;
	}

	// 한 번의 Count 동안 함수마다 센 결과를 기억해 둔다
	// 같은 함수를 여러 곳에서 많이 부르면 펼쳐 세는 양이 곱으로 늘어난다(10번 부르는 함수가 5단계면 10만 번). 서버가 그 계산에 묶이지 않도록
	// 「같은 함수 + 같은 세는 중 함수 모음」의 결과는 다시 쓴다. 세는 중 모음이 다르면 재귀를 끊는 곳이 달라 결과도 다를 수 있으므로 열쇠에 넣는다
	class Counter
	{
		readonly Dictionary<string, FunctionData> functionsByGuid;
		readonly Dictionary<string, Dictionary<string, int>> remembered = new Dictionary<string, Dictionary<string, int>>();

		public Counter(Dictionary<string, FunctionData> functionsByGuid)
		{
			this.functionsByGuid = functionsByGuid;
		}

		// function 안의 블록과, function이 부르는 함수 안의 블록을 센다. counting은 function을 포함한 지금 세고 있는 함수들의 GUID
		public Dictionary<string, int> CountFunction(FunctionData function, HashSet<string> counting)
		{
			string memoKey = function.Guid + ">" + string.Join("|", counting.OrderBy(guid => guid, StringComparer.Ordinal));
			if (remembered.TryGetValue(memoKey, out Dictionary<string, int> saved)) return saved;

			var used = new Dictionary<string, int>();
			foreach (BlockData block in function.AllBlocks())
			{
				if (block.Kind == BlockKind.Function && block.IsUserFunction)
				{
					// 재귀: 지금 세고 있는 함수 안은 다시 세지 않는다. 없는 함수는 건너뛴다
					string calleeGuid = block.FunctionId;
					if (calleeGuid == null || counting.Contains(calleeGuid)) continue;
					if (!functionsByGuid.TryGetValue(calleeGuid, out FunctionData callee)) continue;

					var calleeCounting = new HashSet<string>(counting) { calleeGuid };
					AddAll(used, CountFunction(callee, calleeCounting));
					continue;
				}
				Add(used, KeyOf(block), 1);
			}

			remembered[memoKey] = used;
			return used;
		}
	}

	static void AddAll(Dictionary<string, int> into, Dictionary<string, int> from)
	{
		foreach (KeyValuePair<string, int> pair in from) Add(into, pair.Key, pair.Value);
	}

	// 부른 횟수만큼 곱해져 int를 넘을 수 있으므로 int.MaxValue에서 멈춘다. 가진 개수는 99를 넘지 않으므로 그보다 크면 어차피 저장 검사에 걸린다
	static void Add(Dictionary<string, int> into, string key, int amount)
	{
		if (key == null || amount <= 0) return;
		into.TryGetValue(key, out int current);
		into[key] = (int)Math.Min((long)current + amount, int.MaxValue);
	}
}
