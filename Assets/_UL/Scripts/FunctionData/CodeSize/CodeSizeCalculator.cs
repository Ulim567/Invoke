using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 슬롯에 넣은 함수들의 코드 영역 크기를 계산한다. 한도와 비교하지 않고 크기만 돌려준다. (비교는 슬롯에 넣는 쪽이 한다)
/// <para>
/// 계산 규칙 (docs/systems/function-data.md 「코드 영역 크기 규칙」)<br/>
/// - 슬롯의 사용자 정의 함수와, 그 함수들이 호출로 닿는 모든 사용자 정의 함수의 블록 수를 더한다.<br/>
/// - 같은 함수는 몇 번 불리든, 여러 함수가 부르든 한 번만 센다. 실제 프로그램에서도 함수 코드는 코드 영역에 한 번만 올라가기 때문이다.<br/>
/// - 반복 횟수는 곱하지 않는다.<br/>
/// - C 스킬 블록은 1이 아니라 그 스킬의 CodeSize로 센다.<br/>
/// - 슬롯에 바로 넣은 C 스킬은 C 스킬 블록 하나만 든 함수와 같게 센다.
/// </para>
/// <para>
/// 재귀와 없는 함수 호출은 서버의 구조 검사가 막는다. 여기서는 그런 함수를 만나도 멈추지 않고 계산을 끝낸다.<br/>
/// - 재귀: 이미 센 함수는 다시 세지 않으므로 끝없이 돌지 않는다.<br/>
/// - 없는 함수(지운 함수의 GUID): 셀 수 없으므로 건너뛴다.
/// </para>
/// </summary>
public static class CodeSizeCalculator
{
	/// <summary>
	/// 코드 영역 크기를 계산한다.
	/// </summary>
	/// <param name="slotUserFunctionGuids">슬롯에 넣은 사용자 정의 함수들의 GUID</param>
	/// <param name="slotPredefinedFunctionIds">슬롯에 바로 넣은 C 스킬들의 고정 ID</param>
	/// <param name="ownedFunctions">캐릭터가 가진 모든 사용자 정의 함수. 호출된 함수를 GUID로 찾을 때 쓴다</param>
	public static int Calculate(
		IEnumerable<string> slotUserFunctionGuids,
		IEnumerable<string> slotPredefinedFunctionIds,
		IEnumerable<FunctionData> ownedFunctions)
	{
		Dictionary<string, FunctionData> functionsByGuid = new Dictionary<string, FunctionData>();
		foreach (FunctionData function in ownedFunctions)
		{
			// GUID가 겹치는 조작 데이터는 구조 검사가 막을 일이므로, 먼저 들어온 함수만 쓴다
			if (function?.Guid != null) functionsByGuid.TryAdd(function.Guid, function);
		}

		// 셀 함수 모음. 슬롯의 함수에서 시작해, 그 함수들이 부르는 함수를 찾을 때마다 더한다
		HashSet<string> countedGuids = new HashSet<string>();
		Queue<FunctionData> toSearch = new Queue<FunctionData>();

		foreach (string guid in slotUserFunctionGuids)
		{
			AddToCount(guid, functionsByGuid, countedGuids, toSearch);
		}

		while (toSearch.Count > 0)
		{
			FunctionData function = toSearch.Dequeue();
			foreach (BlockData block in function.AllBlocks())
			{
				if (block.Kind == BlockKind.Function && block.IsUserFunction)
				{
					AddToCount(block.FunctionId, functionsByGuid, countedGuids, toSearch);
				}
			}
		}

		int size = countedGuids.Sum(guid => FunctionSize(functionsByGuid[guid]));
		size += slotPredefinedFunctionIds.Sum(PredefinedFunctionSize);
		return size;
	}

	// 아직 세지 않은 함수만 모음에 넣는다. 없는 GUID는 건너뛴다
	static void AddToCount(string guid, Dictionary<string, FunctionData> functionsByGuid,
		HashSet<string> countedGuids, Queue<FunctionData> toSearch)
	{
		if (guid == null || !functionsByGuid.TryGetValue(guid, out FunctionData function)) return;
		if (!countedGuids.Add(guid)) return;

		toSearch.Enqueue(function);
	}

	// 함수 하나의 크기. 블록마다 크기를 더한다
	static int FunctionSize(FunctionData function) => function.AllBlocks().Sum(BlockSize);

	static int BlockSize(BlockData block)
	{
		if (block.Kind == BlockKind.Function && !block.IsUserFunction) return PredefinedFunctionSize(block.FunctionId);
		return 1;
	}

	// C 스킬 하나의 크기. 함수 안의 C 스킬 블록과 슬롯에 바로 넣은 C 스킬이 모두 여기서 크기를 가져온다
	// 크기는 스킬마다 PredefinedFunction.CodeSize에 정한다
	// 없는 ID는 구조 검사가 막을 일이므로 멈추지 않고, 블록 하나로 보아 1로 센다
	static int PredefinedFunctionSize(string functionId) =>
		PredefinedFunctionRegistry.TryGet(functionId, out PredefinedFunction function) ? function.CodeSize : 1;
}
