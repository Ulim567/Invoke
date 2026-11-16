using System.Collections.Generic;

/// <summary>
/// 슬롯 하나를 컴파일한다: 슬롯 함수와 그 함수가 호출로 닿는 모든 사용자 정의 함수에 컴파일 오류가 없는지 본다 (#506).
/// <para>
/// - 서버(FunctionExecutorNetwork)가 함수 데이터를 받을 때마다 슬롯마다 부르고, 실패한 슬롯의 시전을 CastRejection.CompileError로 거부한다.<br/>
/// - 검사는 편집 화면의 컴파일과 같은 FunctionChecker.Find다. 지운 함수를 부르는 블록도 FunctionChecker가 오류로 찾는다.<br/>
/// - 전역 변수 파일의 검사(FunctionExecutor.CanUseGlobalVariables)는 실행기의 언어 규칙과 메모리 크기가 필요해서 여기서 하지 않는다. 부르는 쪽이 따로 본다.
/// </para>
/// </summary>
// 슬롯에서 닿는 함수만 보는 이유: 슬롯이 부르지 않는 페이지 함수의 오류는 시전에 영향이 없어야 하기 때문 (사용자 결정 11/16)
public static class SlotCompileChecker
{
	/// <summary>
	/// slotFunction과 그 함수가 호출로 닿는 함수 모두에 컴파일 오류가 없으면 true.
	/// </summary>
	/// <param name="slotFunction">슬롯 함수</param>
	/// <param name="ownedFunctions">캐릭터가 가진 모든 함수. 호출된 함수를 GUID로 찾고, 호출 블록의 파라미터 타입을 검사할 때 쓴다</param>
	/// <param name="globals">전역 변수 선언. null이면 전역 변수가 없는 것으로 본다</param>
	public static bool Compiles(FunctionData slotFunction, IReadOnlyList<FunctionData> ownedFunctions, GlobalVariablesData globals)
	{
		if (slotFunction == null) return false;
		foreach (FunctionData function in Reachable(slotFunction, ownedFunctions))
		{
			if (FunctionChecker.HasErrors(function, globals, ownedFunctions)) return false;
		}
		return true;
	}

	/// <summary>
	/// start와, start에서 호출로 닿는 모든 사용자 정의 함수. start가 맨 앞이다.
	/// 같은 함수는 한 번만 들어간다(재귀 호출도 끝난다). 없는 함수(지운 함수의 GUID)는 건너뛴다.
	/// </summary>
	public static List<FunctionData> Reachable(FunctionData start, IEnumerable<FunctionData> ownedFunctions)
	{
		var byGuid = new Dictionary<string, FunctionData>();
		if (ownedFunctions != null)
		{
			foreach (FunctionData function in ownedFunctions)
				if (function?.Guid != null) byGuid.TryAdd(function.Guid, function);
		}

		var result = new List<FunctionData>();
		if (start == null) return result;

		var visited = new HashSet<string> { start.Guid };
		var toSearch = new Queue<FunctionData>();
		result.Add(start);
		toSearch.Enqueue(start);
		while (toSearch.Count > 0)
		{
			foreach (BlockData block in toSearch.Dequeue().AllBlocks())
			{
				if (block.Kind != BlockKind.Function || !block.IsUserFunction) continue;
				if (block.FunctionId == null || !byGuid.TryGetValue(block.FunctionId, out FunctionData called)) continue;
				if (!visited.Add(called.Guid)) continue;
				result.Add(called);
				toSearch.Enqueue(called);
			}
		}
		return result;
	}
}
