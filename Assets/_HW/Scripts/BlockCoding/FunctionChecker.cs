using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 블록 함수 하나를 저장·장착 전에 검사한다 (편집 화면의 「컴파일」). 데이터는 바꾸지 않고 오류 목록만 돌려준다.
/// <para>
/// - 편집 화면(BlockEditor)의 컴파일 버튼·오류 창·빨간 테두리, 함수 목록의 빨간 밑줄, 장착 화면(SlotEquipPanel)에서 오류 있는 함수를 막을 때 쓴다.<br/>
/// - 검사: for·while 밖의 break·continue, 값을 돌려줘야 하는데 return이 없는 길(BlockData.AlwaysReturnsValue), 비어 있는 값 칸,
///   선언하지 않은 변수(함수 안의 선언·배열·for의 변수, 파라미터, 전역 변수에 없는 이름) (function-data.md, function-executor.md).
/// </para>
/// </summary>
public static class FunctionChecker
{
	/// <summary>오류 하나. Block은 오류가 난 문장 블록 (함수 전체의 오류면 null), Code는 그 블록의 C 글자</summary>
	public class Error
	{
		public BlockData Block;
		public string Code;
		public string Message;
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
			errors.Add(new Error { Code = function.Name, Message = "값을 돌려주지 않고 끝나는 길이 있어요 (return 필요)" });
		return errors;
	}

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
			if ((block.Kind == BlockKind.Break || block.Kind == BlockKind.Continue) && loopDepth == 0)
				Add(errors, block, "break, continue는 for, while 안에서만 쓸 수 있어요");

			int empty = 0;
			var unknown = new List<string>();
			var wrong = new List<string>();
			CheckSlots(block, scopes, context, ref empty, unknown, wrong);
			if (empty > 0) Add(errors, block, $"비어 있는 값 칸 {empty}개");
			foreach (string name in unknown.Distinct()) Add(errors, block, $"선언하지 않은 변수 {name} (이 블록보다 위에서, 보이는 범위에 선언해야 해요)");
			foreach (string message in wrong.Distinct()) Add(errors, block, message);

			// 블록 영역은 { } 하나. for의 i는 그 안에서만 보인다
			if (block.InnerBlocks != null)
			{
				bool loop = block.Kind == BlockKind.For || block.Kind == BlockKind.While;
				scopes.Push();
				if (block.Kind == BlockKind.For && block.Slots.Count > 0) scopes.Declare(block.Slots[0]?.Name, new VariableType(VariableTypeKind.Int));
				Walk(block.InnerBlocks, loopDepth + (loop ? 1 : 0), scopes, context, errors);
				scopes.Pop();
			}

			// 칸을 본 뒤에 선언을 더한다. int x = x; 의 오른쪽 x는 아직 선언되지 않은 것으로 보기 위함
			scopes.Declare(VariableScopes.DeclaredName(block), VariableScopes.DeclaredType(block));
		}
	}

	// 블록의 칸(끼운 값 블록 안까지)에서 빈 칸, 모르는 변수 이름, 칸 규칙(SlotRules)에 맞지 않는 값을 찾는다. 선언의 초기값과 return의 값은 비워도 된다
	// 맞지 않는 값을 찾는 이유: 선언의 타입이나 반환 타입을 나중에 바꾸면, 이미 넣은 값이 맞지 않게 되기 때문 (#474)
	static void CheckSlots(BlockData block, VariableScopes.Stack scopes, SlotContext context, ref int empty, List<string> unknown, List<string> wrong)
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
					wrong.Add($"{SlotText(slot)}은(는) 이 칸에 넣을 수 없어요 ({SlotRules.Describe(rule)}만)");
			}
			if (slot.Block != null)
			{
				CheckSlots(slot.Block, scopes, context, ref empty, unknown, wrong);
				continue;
			}
			if (!string.IsNullOrEmpty(slot.VariableName))
			{
				if (!scopes.TryGet(slot.VariableName, out _)) unknown.Add(slot.VariableName);
				continue;
			}
			// 변수 칸(대입의 왼쪽, array 이름)을 고르지 않음 (#474 5단계: 블록 목록에서 꺼내면 비어 있다)
			if (slot.Kind == SlotKind.Variable)
			{
				empty++;
				continue;
			}
			bool optional = block.Kind == BlockKind.Return || (block.Kind == BlockKind.Declare && i == 2);
			if (slot.Kind == SlotKind.Value && !optional && string.IsNullOrEmpty(slot.Literal)) empty++;
		}
	}

	// 오류 문장에 보일 칸 글자. 끼운 블록은 그 블록의 C 글자
	static string SlotText(SlotData slot) =>
		slot.Block != null ? BlockLabels.TextOf(slot.Block).TrimEnd(';') : BlockSlotText.ToText(slot);

	static void Add(List<Error> errors, BlockData block, string message) =>
		errors.Add(new Error { Block = block, Code = BlockLabels.TextOf(block), Message = message });
}
