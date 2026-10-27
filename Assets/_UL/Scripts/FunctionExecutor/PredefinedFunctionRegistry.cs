using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 고정 ID로 사전 정의 함수를 찾는다.
/// <para>
/// 사용법<br/>
/// - TryGet(고정 ID, out 함수)로 찾는다. 받은 함수는 원본이므로, 실행할 때는 FunctionExecutor.Cast에 넘긴다. (복사본이 실행된다)<br/>
/// - All로 모든 사전 정의 함수를 받는다. 편집 화면의 블록 목록에 쓴다.<br/>
/// - PredefinedFunction을 상속한 클래스는 자동으로 등록된다. 따로 등록하지 않는다.
/// </para>
/// <para>
/// 고정 ID가 유일하고 형식(docs/systems/predefined-function.md 「고정 ID」)에 맞는 함수만 등록한다. 규칙을 어긴 함수는 콘솔 오류를 내고 등록하지 않는다.<br/>
/// 에디터에서는 컴파일이 끝날 때마다 등록하므로, Play를 누르지 않아도 실수가 바로 드러난다. 빌드한 게임에서는 처음 쓸 때 등록한다.
/// </para>
/// </summary>
public static class PredefinedFunctionRegistry
{
	// 분류.영어_이름 (소문자, 밑줄). 분류를 더하면 PredefinedFunctionIds의 설명도 함께 고친다
	static readonly Regex IdFormat = new Regex(@"^(c|common|input|test)\.[a-z]+(_[a-z]+)*$");

	static Dictionary<string, PredefinedFunction> functionsById;
	static List<PredefinedFunction> allFunctions;

	/// <summary>
	/// 등록된 모든 사전 정의 함수(원본).
	/// </summary>
	public static IReadOnlyList<PredefinedFunction> All
	{
		get
		{
			EnsureRegistered();
			return allFunctions;
		}
	}

	/// <summary>
	/// 고정 ID로 사전 정의 함수(원본)를 찾는다. 없는 ID면 false를 돌려주고 function은 null이다.
	/// </summary>
	public static bool TryGet(string id, out PredefinedFunction function)
	{
		EnsureRegistered();
		function = null;
		return id != null && functionsById.TryGetValue(id, out function);
	}

	// 아직 등록하지 않았으면 그 자리에서 등록한다. 빌드한 게임은 여기서 처음 등록된다
	static void EnsureRegistered()
	{
		if (functionsById == null) Register();
	}

	// 에디터에서는 컴파일이 끝나 스크립트를 다시 불러올 때마다 Unity가 이 함수를 부른다
	// 컴파일하면 static 값이 비워지므로, 매번 처음부터 다시 등록하고 검사한다
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoadMethod]
#endif
	static void Register()
	{
		functionsById = new Dictionary<string, PredefinedFunction>();
		allFunctions = new List<PredefinedFunction>();

		// 프로젝트에 어셈블리 정의(asmdef)가 없어 모든 스크립트가 한 어셈블리에 있으므로, 이 어셈블리만 훑는다
		IEnumerable<Type> types = typeof(PredefinedFunction).Assembly.GetTypes()
			.Where(type => type.IsSubclassOf(typeof(PredefinedFunction)) && !type.IsAbstract);

		// 형식에 맞는 함수만 모은다. 같은 ID인지는 다 모은 뒤에 따진다
		List<PredefinedFunction> candidates = new List<PredefinedFunction>();
		foreach (Type type in types)
		{
			// 원본 객체는 생성자에 값을 넘기지 않고 만든다. 값을 받는 생성자만 있으면 만들 수 없다
			if (type.GetConstructor(Type.EmptyTypes) == null)
			{
				Debug.LogError($"[PredefinedFunctionRegistry] {type.Name}: 값을 받지 않는 생성자가 없어 등록하지 않음");
				continue;
			}

			PredefinedFunction function = (PredefinedFunction)Activator.CreateInstance(type);
			if (function.Id == null || !IdFormat.IsMatch(function.Id))
			{
				Debug.LogError($"[PredefinedFunctionRegistry] {type.Name}: 고정 ID \"{function.Id}\"가 형식(분류.영어_이름)에 맞지 않아 등록하지 않음");
				continue;
			}

			candidates.Add(function);
		}

		// 같은 ID는 어느 쪽이 맞는지 알 수 없으므로 모두 등록하지 않는다
		// 먼저 찾은 쪽만 남기면, C#이 클래스를 돌려주는 순서에 따라 남는 쪽이 달라지기 때문
		foreach (IGrouping<string, PredefinedFunction> group in candidates.GroupBy(function => function.Id))
		{
			if (group.Count() > 1)
			{
				string classNames = string.Join(", ", group.Select(function => function.GetType().Name));
				Debug.LogError($"[PredefinedFunctionRegistry] 고정 ID \"{group.Key}\"를 여러 클래스({classNames})가 써서 모두 등록하지 않음");
				continue;
			}

			PredefinedFunction registered = group.First();
			functionsById.Add(group.Key, registered);
			allFunctions.Add(registered);
		}
	}
}
