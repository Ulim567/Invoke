using System.Collections.Generic;

/// <summary>
/// 블록 하나의 자리에서 쓸 수 있는 변수(이름과 타입)를 찾는다 (#474). 실행기처럼 C의 범위 규칙을 따른다 (function-executor.md 「블록 범위」).
/// <para>
/// - 늘 보임: 전역 변수, 파라미터.<br/>
/// - 함수 안의 선언·배열 만들기: 그 블록보다 위에 있고, 같은 { } 이거나 바깥 { } 에 있을 때만. 예: if 안에서 선언한 변수는 if 밖에서 보이지 않는다.<br/>
/// - for의 i: 그 for의 안쪽에서만.<br/>
/// - 이름이 같으면 안쪽 변수가 보인다 (C처럼 바깥 변수를 가린다).
/// </para>
/// <para>
/// 편집 화면(변수 고르기 드롭다운, 적은 변수 검사)과 컴파일(FunctionChecker)이 함께 쓴다.
/// </para>
/// </summary>
public static class VariableScopes
{
	/// <summary>
	/// { } 범위를 쌓아 두는 곳. 컴파일처럼 블록을 위에서부터 차례로 보며 선언을 더할 때 쓴다.
	/// </summary>
	public class Stack
	{
		readonly List<Dictionary<string, VariableType>> scopes = new List<Dictionary<string, VariableType>>();

		public Stack() => Push();

		/// <summary>{ 를 연다</summary>
		public void Push() => scopes.Add(new Dictionary<string, VariableType>());

		/// <summary>} 로 닫는다. 그 안에서 선언한 변수는 사라진다</summary>
		public void Pop()
		{
			if (scopes.Count > 1) scopes.RemoveAt(scopes.Count - 1);
		}

		/// <summary>가장 안쪽 범위에 변수를 더한다. 이름이 비었으면 무시한다</summary>
		public void Declare(string name, VariableType type)
		{
			if (!string.IsNullOrEmpty(name)) scopes[scopes.Count - 1][name] = type;
		}

		/// <summary>안쪽 범위부터 찾는다. 없으면 false</summary>
		public bool TryGet(string name, out VariableType type)
		{
			type = null;
			if (string.IsNullOrEmpty(name)) return false;
			for (int i = scopes.Count - 1; i >= 0; i--)
				if (scopes[i].TryGetValue(name, out type)) return true;
			return false;
		}

		/// <summary>지금 보이는 변수 모두 (이름 → 타입). 안쪽 변수가 바깥 변수를 가린다</summary>
		public Dictionary<string, VariableType> Snapshot()
		{
			var visible = new Dictionary<string, VariableType>();
			foreach (Dictionary<string, VariableType> scope in scopes)
				foreach (KeyValuePair<string, VariableType> variable in scope) visible[variable.Key] = variable.Value;
			return visible;
		}
	}

	/// <summary>
	/// 함수 하나의 맨 바깥 범위를 만든다: 전역 변수(globals, null이면 없음) 위에 파라미터.
	/// 함수 몸통의 맨 바깥 선언은 파라미터와 같은 범위에 더한다 (실행기와 같음).
	/// </summary>
	public static Stack Start(FunctionData function, GlobalVariablesData globals)
	{
		var stack = new Stack();
		foreach (BlockData declaration in globals?.Declarations ?? new List<BlockData>())
			if (declaration != null) stack.Declare(DeclaredName(declaration), DeclaredType(declaration));
		stack.Push();
		foreach (ParameterData parameter in function?.Parameters ?? new List<ParameterData>())
			if (parameter != null) stack.Declare(parameter.Name, parameter.Type);
		return stack;
	}

	/// <summary>
	/// 선언·배열 만들기 블록이 만드는 변수 이름. 다른 블록이면 null
	/// </summary>
	public static string DeclaredName(BlockData block) =>
		(block.Kind == BlockKind.Declare || block.Kind == BlockKind.ArrayDeclare) && block.Slots.Count > 1 ? block.Slots[1]?.Name : null;

	/// <summary>
	/// 선언·배열 만들기 블록이 만드는 변수의 타입. 배열 만들기는 원소 타입의 array. 다른 블록이면 null
	/// </summary>
	public static VariableType DeclaredType(BlockData block)
	{
		if (block.Slots.Count == 0) return null;
		if (block.Kind == BlockKind.Declare) return block.Slots[0]?.Type;
		if (block.Kind == BlockKind.ArrayDeclare) return VariableType.ArrayOf(block.Slots[0]?.Type);
		return null;
	}

	/// <summary>
	/// blocks(함수 몸통이나 전역 변수 선언) 안의 target 블록 자리에서 보이는 변수. target은 문장 블록이거나 그 칸에 끼운 값 블록이다.
	/// stack은 Start로 만든 범위. target을 찾지 못하면 null (화면에 떠 있는 블록 등).
	/// </summary>
	public static Dictionary<string, VariableType> VisibleAt(List<BlockData> blocks, Stack stack, BlockData target)
	{
		if (blocks == null || target == null) return null;
		foreach (BlockData block in blocks)
		{
			if (block == null) continue;
			// 이 문장 블록의 칸에 있으면, 이 블록이 선언하는 변수는 아직 보이지 않는다 (int x = x; 에서 오른쪽 x)
			if (ContainsInSlots(block, target)) return stack.Snapshot();

			if (block.InnerBlocks != null)
			{
				stack.Push();
				if (block.Kind == BlockKind.For && block.Slots.Count > 0) stack.Declare(block.Slots[0]?.Name, new VariableType(VariableTypeKind.Int));
				Dictionary<string, VariableType> found = VisibleAt(block.InnerBlocks, stack, target);
				stack.Pop();
				if (found != null) return found;
			}

			stack.Declare(DeclaredName(block), DeclaredType(block));
		}
		return null;
	}

	// block 자신이거나, block의 칸에 끼운 값 블록(그 안의 칸까지)이 target이면 true. 블록 영역(InnerBlocks)은 보지 않는다
	static bool ContainsInSlots(BlockData block, BlockData target)
	{
		if (block == target) return true;
		foreach (SlotData slot in block.Slots)
			if (slot?.Block != null && ContainsInSlots(slot.Block, target)) return true;
		return false;
	}
}
