using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 블록 함수 하나를 저장·장착 전에 검사한다 (편집 화면의 「컴파일」). 데이터는 바꾸지 않고 오류 목록만 돌려준다.
/// <para>
/// - 편집 화면(BlockEditor)의 컴파일 버튼·오류 창·빨간 테두리, 함수 목록의 빨간 밑줄, 장착 화면(SlotEquipPanel)에서 오류 있는 함수를 막을 때 쓴다.<br/>
/// - 검사: for·while 밖의 break·continue, 값을 돌려줘야 하는데 return이 없는 길(BlockData.AlwaysReturnsValue), 비어 있는 값 칸,
///   선언하지 않은 변수(함수 안의 선언·배열·for의 변수, 파라미터, 전역 변수에 없는 이름), 지운 함수를 부르는 블록 (function-data.md, function-executor.md),
///   같은 { } 안에서 두 번 선언한 변수, 칸 규칙(SlotRules)에 맞지 않는 값 (#504).<br/>
/// - 오류 문장은 C 컴파일러의 오류를 C 용어 그대로 한국어로 옮긴 문장이다. 예: 선언하지 않은 식별자: x (#504).<br/>
/// - 저장하기 전에 FindNewlyBroken으로, 이 저장 때문에 다른 함수에 오류가 새로 생기는지도 본다 (#494).
/// </para>
/// </summary>
public static class FunctionChecker
{
	/// <summary>
	/// 오류 하나. Block은 오류가 난 문장 블록 (함수 전체의 오류면 null), Code는 그 블록의 C 글자.
	/// Slots는 오류가 난 칸 (칸을 가진 블록, 그 블록의 칸 번호). 끼운 값 블록 안의 칸이면 Owner는 그 값 블록이다.
	/// 칸이 아니라 블록 전체의 오류(반복문 밖의 break, 재정의, 모든 경로 return)면 비어 있다 (#504).
	/// </summary>
	public class Error
	{
		public BlockData Block;
		public string Code;
		public string Message;
		public List<(BlockData Owner, int Index)> Slots = new List<(BlockData, int)>();
	}

	/// <summary>
	/// 오류 목록. globals는 전역 변수 선언 (null이면 전역 변수가 없는 것으로 본다).
	/// ownedFunctions는 캐릭터가 가진 함수들로, 사용자 정의 함수 블록의 파라미터·반환 타입을 찾을 때 쓴다 (null이면 그 칸은 타입을 검사하지 않음).
	/// </summary>
	public static List<Error> Find(FunctionData function, GlobalVariablesData globals = null, IEnumerable<FunctionData> ownedFunctions = null)
	{
		var errors = new List<Error>();
		if (function == null || function.Blocks == null) return errors;

		// 칸 규칙(SlotRules)에 쓸 것: 이 함수의 반환 타입(return 칸), 사용자 정의 함수 찾기(편집 중인 함수는 지금 내용으로)
		List<FunctionData> owned = ownedFunctions?.ToList();
		FunctionData FindUserFunction(string guid) =>
			function.Guid == guid ? function : owned?.FirstOrDefault(f => f != null && f.Guid == guid);
		var context = new SlotContext { ReturnType = function.ReturnType, FindUserFunction = owned != null ? (System.Func<string, FunctionData>)FindUserFunction : null };

		// 쓸 수 있는 변수: C처럼 위에서 선언했고 그 자리에서 보이는 것만 (전역 변수, 파라미터, 바깥·같은 { }의 선언, for의 i) (#474 5단계)
		// 실행기도 { } 범위를 지키므로, 순서나 범위를 어긴 변수는 실행 중에 「선언하지 않은 변수」로 실패하기 때문
		Walk(function.Blocks, 0, VariableScopes.Start(function, globals), context, errors);

		bool returnsValue = function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void;
		if (returnsValue && !BlockData.AlwaysReturnsValue(function.Blocks))
			errors.Add(new Error { Code = function.Name, Message = "void가 아닌 함수는 모든 경로에서 return해야 함" });
		return errors;
	}

	/// <summary>
	/// 저장하려는 변경 때문에 컴파일 오류가 새로 생기는 함수와 그 새 오류들. 편집 화면이 저장하기 전에 부른다 (#494).
	/// <para>
	/// - before·globalsBefore: 지금 저장된 함수들과 전역 변수. after·globalsAfter: 이 변경을 저장했을 때의 함수들과 전역 변수.<br/>
	/// - 예: 전역 변수 x를 지우면 x를 쓰던 함수, 페이지 함수의 반환 타입을 바꾸면 그 값을 쓰던 함수, 페이지 함수를 지우면 그 함수를 부르던 함수.<br/>
	/// - 이미 있던 오류는 세지 않는다. 이 변경과 상관없는 예전 오류 때문에 저장을 막지 않기 위함.
	/// </para>
	/// </summary>
	public static List<(FunctionData function, List<Error> errors)> FindNewlyBroken(
		IEnumerable<FunctionData> before, GlobalVariablesData globalsBefore,
		IEnumerable<FunctionData> after, GlobalVariablesData globalsAfter)
	{
		List<FunctionData> beforeList = before?.Where(f => f != null).ToList() ?? new List<FunctionData>();
		List<FunctionData> afterList = after?.Where(f => f != null).ToList() ?? new List<FunctionData>();
		var broken = new List<(FunctionData, List<Error>)>();
		foreach (FunctionData function in afterList)
		{
			List<Error> errorsAfter = Find(function, globalsAfter, afterList);
			if (errorsAfter.Count == 0) continue;

			// 저장된 상태에서도 같은 오류가 있었으면 이 변경 때문에 생긴 것이 아니다
			FunctionData old = beforeList.FirstOrDefault(f => f.Guid == function.Guid);
			var known = new HashSet<string>(old != null ? Find(old, globalsBefore, beforeList).Select(KeyOf) : Enumerable.Empty<string>());
			List<Error> added = errorsAfter.Where(e => !known.Contains(KeyOf(e))).ToList();
			if (added.Count > 0) broken.Add((function, added));
		}
		return broken;
	}

	// 같은 오류인지 비교할 때 쓰는 글자: 오류가 난 블록의 C 글자와 오류 문장
	static string KeyOf(Error error) => error.Code + "\n" + error.Message;

	/// <summary>오류 문장만 (예전 쓰임새)</summary>
	public static List<string> Check(FunctionData function, GlobalVariablesData globals = null) =>
		Find(function, globals).Select(e => e.Message).ToList();

	/// <summary>오류가 하나라도 있으면 true. ownedFunctions는 Find와 같다</summary>
	public static bool HasErrors(FunctionData function, GlobalVariablesData globals = null, IEnumerable<FunctionData> ownedFunctions = null) =>
		Find(function, globals, ownedFunctions).Count > 0;

	// 칸 규칙을 정할 때 함께 넘기는 것
	class SlotContext
	{
		public VariableType ReturnType;
		public System.Func<string, FunctionData> FindUserFunction;
	}

	// 문장 블록을 위에서부터 차례로 보며 오류를 모은다. loopDepth는 지금 for·while 안에 몇 겹 들어와 있는지, scopes는 지금 보이는 변수
	static void Walk(IEnumerable<BlockData> blocks, int loopDepth, VariableScopes.Stack scopes, SlotContext context, List<Error> errors)
	{
		foreach (BlockData block in blocks)
		{
			if (block == null) continue;
			// 오류 문장은 C 컴파일러(gcc, clang)의 오류를 C 용어(식별자, scope, 상수) 그대로 한국어로 옮긴다 (#504). 플레이어가 나중에 실제 컴파일러의 오류도 같은 용어로 읽을 수 있게 하기 위함
			if (block.Kind == BlockKind.Break && loopDepth == 0) Add(errors, block, "break는 반복문 밖에서 사용할 수 없음");
			if (block.Kind == BlockKind.Continue && loopDepth == 0) Add(errors, block, "continue는 반복문 밖에서 사용할 수 없음");
			if (CallsMissingFunction(block, context)) Add(errors, block, MissingFunctionMessage);

			var empty = new List<(BlockData, int)>();
			var unknown = new List<(string Name, (BlockData, int) Slot)>();
			var wrong = new List<(string Message, (BlockData, int) Slot)>();
			CheckSlots(block, scopes, context, empty, unknown, wrong);
			// 빈 칸 하나마다 한 줄. 실제 컴파일러도 오류 위치마다 한 줄씩 내기 때문
			// 같은 이름·같은 문장은 한 줄로 모으고, 그 칸들을 모두 담는다. 오류 카드를 누르면 그 칸들이 함께 깜빡인다
			foreach ((BlockData, int) slot in empty) Add(errors, block, "expression이 비어 있음", new[] { slot });
			foreach (var group in unknown.GroupBy(u => u.Name)) Add(errors, block, UndeclaredMessage(group.Key), group.Select(u => u.Slot));
			foreach (var group in wrong.GroupBy(w => w.Message)) Add(errors, block, group.Key, group.Select(w => w.Slot));

			// { } 하나가 범위 하나. for는 i의 범위 안에 몸통의 범위가 따로 있다 (실행기의 ForBlock, LoopBlock과 같음)
			// 그래서 for 몸통에서 int i;를 다시 선언해도 C처럼 바깥 i를 가릴 뿐 오류가 아니다
			if (block.InnerBlocks != null)
			{
				bool loop = block.Kind == BlockKind.For || block.Kind == BlockKind.While;
				bool forIndex = block.Kind == BlockKind.For && block.Slots.Count > 0;
				if (forIndex)
				{
					scopes.Push();
					scopes.Declare(block.Slots[0]?.Name, new VariableType(VariableTypeKind.Int));
				}
				scopes.Push();
				Walk(block.InnerBlocks, loopDepth + (loop ? 1 : 0), scopes, context, errors);
				scopes.Pop();
				if (forIndex) scopes.Pop();
			}

			// 칸을 본 뒤에 선언을 더한다. int x = x; 의 오른쪽 x는 아직 선언되지 않은 것으로 보기 위함
			// 같은 { } 안에 같은 이름이 이미 있으면 실행기가 DuplicateVariable로 멈추므로 저장 전에 찾는다 (#504)
			// 바깥 { }나 전역 변수와 같은 이름은 C처럼 가리기만 하므로 오류가 아니다 (LanguageRule_C의 AllowShadowing)
			string declared = VariableScopes.DeclaredName(block);
			if (scopes.IsDeclaredInInnermost(declared)) Add(errors, block, $"같은 scope에서 재정의할 수 없음: {declared}");
			scopes.Declare(declared, VariableScopes.DeclaredType(block));
		}
	}

	// 블록의 칸(끼운 값 블록 안까지)에서 빈 칸, 모르는 변수 이름, 칸 규칙(SlotRules)에 맞지 않는 값을 찾는다. 선언의 초기값과 return의 값은 비워도 된다
	// 맞지 않는 값을 찾는 이유: 선언의 타입이나 반환 타입을 나중에 바꾸면, 이미 넣은 값이 맞지 않게 되기 때문 (#474)
	// 찾은 것마다 그 칸(칸을 가진 블록, 칸 번호)을 함께 담는다. 편집 화면이 오류가 난 칸만 칠하고 깜빡이기 위함 (#504)
	static void CheckSlots(BlockData block, VariableScopes.Stack scopes, SlotContext context,
		List<(BlockData, int)> empty, List<(string Name, (BlockData, int) Slot)> unknown, List<(string Message, (BlockData, int) Slot)> wrong)
	{
		System.Func<string, VariableType> variableType = name => scopes.TryGet(name, out VariableType type) ? type : null;
		for (int i = 0; i < block.Slots.Count; i++)
		{
			SlotData slot = block.Slots[i];
			if (slot == null) continue;
			if (slot.Kind == SlotKind.Value)
			{
				SlotRules.Rule rule = SlotRules.RuleOf(block, i, context.ReturnType, false, context.FindUserFunction, variableType);
				if (!SlotRules.Fits(rule, slot, context.FindUserFunction, variableType))
					wrong.Add((WrongValueMessage(block, slot, rule, context.FindUserFunction, variableType), (block, i)));
			}
			if (slot.Block != null)
			{
				if (CallsMissingFunction(slot.Block, context)) wrong.Add((MissingFunctionMessage, (block, i)));
				CheckSlots(slot.Block, scopes, context, empty, unknown, wrong);
				continue;
			}
			if (!string.IsNullOrEmpty(slot.VariableName))
			{
				if (!scopes.TryGet(slot.VariableName, out _)) unknown.Add((slot.VariableName, (block, i)));
				continue;
			}
			// 변수 칸(대입의 왼쪽, array 이름)을 고르지 않음 (#474 5단계: 블록 목록에서 꺼내면 비어 있다)
			if (slot.Kind == SlotKind.Variable)
			{
				empty.Add((block, i));
				continue;
			}
			bool optional = block.Kind == BlockKind.Return || (block.Kind == BlockKind.Declare && i == 2);
			if (slot.Kind == SlotKind.Value && !optional && string.IsNullOrEmpty(slot.Literal)) empty.Add((block, i));
		}
	}

	// 지운 함수는 이름이 남아 있지 않아(호출 블록은 GUID만 가짐) 이름 없이 적는다
	const string MissingFunctionMessage = "선언하지 않은 함수";

	/// <summary>
	/// 선언하지 않은 식별자의 오류 문장. 편집 화면의 값 칸(BlockValueSlot)이 적는 순간 상태 줄에 같은 문장을 띄울 때도 쓴다 (#504).
	/// </summary>
	public static string UndeclaredMessage(string name) => $"선언하지 않은 식별자: {name}";

	/// <summary>
	/// block의 칸에 넣은 slot이 칸 규칙(rule)에 맞지 않을 때의 오류 문장. 넣은 값의 타입과 칸이 받는 타입을 보인다.
	/// 편집 화면의 값 칸(BlockValueSlot)이 적는 순간 상태 줄에 컴파일 오류 창과 같은 문장을 띄울 때도 쓴다 (#504).
	/// </summary>
	public static string WrongValueMessage(BlockData block, SlotData slot, SlotRules.Rule rule, System.Func<string, FunctionData> findUserFunction, System.Func<string, VariableType> variableType)
	{
		if (rule.Expect == SlotRules.Expect.ArrayLength)
			return $"array 크기에는 양의 정수 상수만 쓸 수 있음: {(block.Slots.Count > 1 ? block.Slots[1]?.Name : "")}";
		// 직접 적은 값만 받는 칸은 array 크기 말고는 전역 변수의 초기값뿐이다. 문장은 GlobalVariablesConverter와 같게
		if (rule.LiteralOnly) return "전역 변수의 초기값에는 상수만 쓸 수 있음";
		if (SlotRules.IsVoid(rule)) return "void 함수는 값을 return할 수 없음";

		VariableType found = SlotRules.TypeOf(slot, findUserFunction, variableType);
		string expected = ExpectedName(rule);
		return found != null
			? $"호환되지 않는 타입. 필요한 타입: {expected}, 넣은 타입: {TypeText(found)}"
			: $"호환되지 않는 타입. 필요한 타입: {expected}";
	}

	// 칸이 받는 것을 오류 문장에 쓸 영어 이름으로
	static string ExpectedName(SlotRules.Rule rule)
	{
		switch (rule.Expect)
		{
			case SlotRules.Expect.Condition: return "bool";
			case SlotRules.Expect.Number: return "number";
			case SlotRules.Expect.Integer: return "int";
			case SlotRules.Expect.Comparable: return "number, char, bool, pointer";
			case SlotRules.Expect.Pointer: return "pointer";
			case SlotRules.Expect.Typed: return rule.Type != null ? TypeText(rule.Type) : "value";
			default: return "value";
		}
	}

	// 타입을 C 글자로. NULL(가리키는 타입이 없는 포인터)은 C처럼 void*
	static string TypeText(VariableType type)
	{
		if (type.Kind == VariableTypeKind.Pointer && type.ElementType == null) return "void*";
		if (type.Kind == VariableTypeKind.Array) return (type.ElementType != null ? BlockLabels.TypeName(type.ElementType) : "?") + "[]";
		return BlockLabels.TypeName(type);
	}

	// 사용자 정의 함수를 부르는 블록인데 캐릭터가 가진 함수 중에 그 GUID가 없으면 true (#494).
	// 실행기도 이 함수를 바꾸지 못해 그 슬롯 전체가 시전되지 않으므로 저장 전에 찾는다. 함수 목록을 받지 않았으면(FindUserFunction null) 보지 않는다
	static bool CallsMissingFunction(BlockData block, SlotContext context) =>
		block.Kind == BlockKind.Function && block.IsUserFunction && context.FindUserFunction != null
		&& context.FindUserFunction(block.FunctionId) == null;

	static void Add(List<Error> errors, BlockData block, string message, IEnumerable<(BlockData, int)> slots = null) =>
		errors.Add(new Error { Block = block, Code = BlockLabels.TextOf(block), Message = message,
			Slots = slots?.ToList() ?? new List<(BlockData, int)>() });
}
