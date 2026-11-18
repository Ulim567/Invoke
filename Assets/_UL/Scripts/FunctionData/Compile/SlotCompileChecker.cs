using System.Collections.Generic;

/// <summary>
/// 슬롯 하나를 컴파일한다: 슬롯 함수와 그 함수가 호출로 닿는 모든 사용자 정의 함수에 컴파일 오류가 없는지 본다 (#506).
/// <para>
/// - 서버(FunctionExecutorNetwork)가 함수 데이터를 받을 때마다 빌드하며 부른다. 하나라도 실패하면 모든 슬롯의 시전을 CastRejection.CompileError로 거부한다.<br/>
/// - 검사는 편집 화면의 컴파일과 같은 FunctionChecker.Find다. 지운 함수를 부르는 블록도 FunctionChecker가 오류로 찾는다.<br/>
/// - 전역 변수 파일의 검사(FunctionExecutor.CanUseGlobalVariables)는 실행기의 언어 규칙과 메모리 크기가 필요해서 여기서 하지 않는다. 부르는 쪽이 따로 본다.
/// </para>
/// </summary>
// 슬롯에서 닿는 함수만 보는 이유: 슬롯이 부르지 않는 페이지 함수의 오류는 시전에 영향이 없어야 하기 때문 (사용자 결정 11/16)
public static class SlotCompileChecker
{
	/// <summary>
	/// 빌드의 함수 쪽 검사 (#506). 슬롯 함수들(slotFunctionGuids)과 그 함수들이 호출로 닿는 함수 모두에 컴파일 오류가 없으면 true.<br/>
	/// 서버(FunctionExecutorNetwork.Build)와 편집 화면(BlockEditor의 저장 뒤 Build 성공 알림)이 같은 규칙으로 쓴다. 전역 변수 파일 검사는 부르는 쪽이 한다.<br/>
	/// ownedFunctions에 없는 GUID(받지 못한 함수)는 건너뛴다.
	/// </summary>
	// family: 캐릭터의 언어 방식. 그 언어의 규칙으로 검사한다 (#532, FunctionChecker.Find)
	public static bool Compiles(IEnumerable<string> slotFunctionGuids, IReadOnlyList<FunctionData> ownedFunctions, GlobalVariablesData globals, LanguageFamily family = LanguageFamily.C)
	{
		foreach (string guid in slotFunctionGuids)
		{
			FunctionData slotFunction = null;
			foreach (FunctionData function in ownedFunctions)
				if (function != null && function.Guid == guid) { slotFunction = function; break; }
			if (slotFunction != null && !Compiles(slotFunction, ownedFunctions, globals, family)) return false;
		}
		return true;
	}

	/// <summary>
	/// slotFunction과 그 함수가 호출로 닿는 함수 모두에 컴파일 오류가 없으면 true.
	/// </summary>
	/// <param name="slotFunction">슬롯 함수</param>
	/// <param name="ownedFunctions">캐릭터가 가진 모든 함수. 호출된 함수를 GUID로 찾고, 호출 블록의 파라미터 타입을 검사할 때 쓴다</param>
	/// <param name="globals">전역 변수 선언. null이면 전역 변수가 없는 것으로 본다</param>
	public static bool Compiles(FunctionData slotFunction, IReadOnlyList<FunctionData> ownedFunctions, GlobalVariablesData globals, LanguageFamily family = LanguageFamily.C)
	{
		if (slotFunction == null) return false;
		foreach (FunctionData function in Reachable(slotFunction, ownedFunctions))
		{
			if (FunctionChecker.HasErrors(function, globals, ownedFunctions, family)) return false;
		}
		return true;
	}

	/// <summary>
	/// slotFunction에서 호출로 닿는 함수 중 컴파일 오류가 있는 함수마다, slotFunction부터 그 함수까지의 호출 경로 (#506). 오류가 없으면 빈 목록.<br/>
	/// 경로마다 맨 앞이 slotFunction, 맨 끝이 오류가 있는 함수다. 경로는 가장 짧은 하나다.<br/>
	/// 편집 화면이 실패한 슬롯에 ✗를 붙이고, 오류 카드에 「슬롯 Q → aim → find」처럼 보일 때 쓴다.
	/// </summary>
	public static List<List<FunctionData>> FailedPaths(FunctionData slotFunction, IReadOnlyList<FunctionData> ownedFunctions, GlobalVariablesData globals, LanguageFamily family = LanguageFamily.C)
	{
		var paths = new List<List<FunctionData>>();
		if (slotFunction == null) return paths;
		foreach (FunctionData function in Walk(slotFunction, ownedFunctions, out Dictionary<string, FunctionData> callers))
		{
			if (!FunctionChecker.HasErrors(function, globals, ownedFunctions, family)) continue;
			// 부른 함수를 거슬러 올라가 슬롯 함수까지 잇는다
			var path = new List<FunctionData> { function };
			while (callers.TryGetValue(path[0].Guid, out FunctionData caller)) path.Insert(0, caller);
			paths.Add(path);
		}
		return paths;
	}

	/// <summary>
	/// start와, start에서 호출로 닿는 모든 사용자 정의 함수. start가 맨 앞이다.
	/// 같은 함수는 한 번만 들어간다(재귀 호출도 끝난다). 없는 함수(지운 함수의 GUID)는 건너뛴다.
	/// </summary>
	public static List<FunctionData> Reachable(FunctionData start, IEnumerable<FunctionData> ownedFunctions) => Walk(start, ownedFunctions, out _);

	// start에서 호출을 따라 넓게 찾는다. callers에는 찾은 함수마다 처음 그 함수를 부른 함수를 남긴다(GUID → 부른 함수). 가장 짧은 호출 경로를 되짚기 위함
	static List<FunctionData> Walk(FunctionData start, IEnumerable<FunctionData> ownedFunctions, out Dictionary<string, FunctionData> callers)
	{
		callers = new Dictionary<string, FunctionData>();
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
			FunctionData current = toSearch.Dequeue();
			foreach (BlockData block in current.AllBlocks())
			{
				if (block.Kind != BlockKind.Function || !block.IsUserFunction) continue;
				if (block.FunctionId == null || !byGuid.TryGetValue(block.FunctionId, out FunctionData called)) continue;
				if (!visited.Add(called.Guid)) continue;
				callers[called.Guid] = current;
				result.Add(called);
				toSearch.Enqueue(called);
			}
		}
		return result;
	}
}
