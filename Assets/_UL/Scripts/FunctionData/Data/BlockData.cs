using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 블록의 종류.
/// </summary>
public enum BlockKind
{
	Declare,
	Assign,
	Arithmetic,
	Compare,
	// And, Or
	Logic,
	Not,
	// if, else if, else. 셋 중 무엇인지는 첫 칸의 Option에 담는다
	Conditional,
	For,
	While,
	Break,
	Continue,
	ArrayDeclare,
	Malloc,
	Free,
	// 사용자 정의 함수, 사전 정의 함수(C 스킬, 입력 함수)를 부른다
	Function,
	Return,
	Wait,
}

/// <summary>
/// 편집 화면에 놓인 블록 하나. 편집 화면의 블록 UI 하나가 이 객체 하나를 들고, 플레이어가 고치면 값을 바로 바꾼다.
/// <para>
/// 칸 순서는 docs/systems/function-data.md의 「블록마다 가진 칸」을 따른다.<br/>
/// 칸 순서를 틀리지 않도록 직접 new 하지 않고 Declare, Assign 같은 만들기 함수로 만든다.
/// </para>
/// <para>
/// 블록 영역(안에 쌓는 블록들)은 조건문, for, while만 가진다. 다른 블록은 null이다.<br/>
/// if·else if·else를 따로 놓인 블록으로 정했으므로, 블록 하나의 블록 영역은 많아야 하나다.
/// </para>
/// </summary>
public class BlockData
{
	public BlockKind Kind { get; set; }

	/// <summary>
	/// 칸들. 순서는 블록 종류마다 정해져 있다.
	/// </summary>
	public List<SlotData> Slots { get; set; } = new List<SlotData>();

	/// <summary>
	/// 블록 영역에 위에서부터 쌓인 블록들. 조건문, for, while만 가지고 나머지는 null.
	/// </summary>
	public List<BlockData> InnerBlocks { get; set; }

	/// <summary>
	/// 함수 블록이 부를 함수. 사용자 정의 함수는 GUID, 사전 정의 함수는 고정 ID. 함수 블록이 아니면 null.
	/// </summary>
	public string FunctionId { get; set; }

	/// <summary>
	/// 함수 블록이 부를 함수가 사용자 정의 함수이면 true, 사전 정의 함수이면 false.<br/>
	/// FunctionId가 둘 다 글자열이라 모양으로는 구분할 수 없으므로 따로 담는다.
	/// </summary>
	public bool IsUserFunction { get; set; }

	// Newtonsoft Json이 JSON으로 바꿀 때 이 이름의 함수를 찾아 부른다. 함수 블록일 때만 IsUserFunction을 적게 한다
	// bool은 비어 있음(null)이 없어서, 함수 블록이 아닌 블록에도 "IsUserFunction": false가 적히는 것을 막기 위함
	public bool ShouldSerializeIsUserFunction() => Kind == BlockKind.Function;

	/// <summary>
	/// 이 블록과, 칸에 끼운 블록, 블록 영역 안의 블록을 모두 차례로 돌려준다. 안쪽 블록의 안쪽까지 내려간다.<br/>
	/// 블록 수 세기, 지역 변수 모으기, 부르는 함수 찾기처럼 블록을 빠짐없이 훑어야 할 때 쓴다.
	/// </summary>
	public IEnumerable<BlockData> SelfAndInnerBlocks()
	{
		yield return this;

		foreach (SlotData slot in Slots)
		{
			if (slot.Block == null) continue;
			foreach (BlockData block in slot.Block.SelfAndInnerBlocks()) yield return block;
		}

		if (InnerBlocks == null) yield break;
		foreach (BlockData inner in InnerBlocks)
		{
			foreach (BlockData block in inner.SelfAndInnerBlocks()) yield return block;
		}
	}

	/// <summary>
	/// 이 블록의 블록 수. 자기 자신 1개 + 칸에 끼운 블록 + 블록 영역 안의 블록.
	/// </summary>
	public int CountBlocks() => SelfAndInnerBlocks().Count();

	/// <summary>
	/// 이 종류의 블록을 값 칸에 끼울 수 있으면 true. 연산, 비교, 논리, Not, malloc, 함수 블록.<br/>
	/// 함수 블록은 부를 함수의 반환 타입이 void가 아닐 때만 끼울 수 있다. 반환 타입은 이 함수가 알 수 없으므로 부르는 쪽이 따로 확인한다.<br/>
	/// 편집 화면(드래그 앤 드롭으로 놓을 자리 판단)과 서버의 구조 검사가 같은 규칙을 쓰도록 여기에 둔다.
	/// </summary>
	public static bool CanBeValue(BlockKind kind) => kind switch
	{
		BlockKind.Arithmetic or BlockKind.Compare or BlockKind.Logic or BlockKind.Not
			or BlockKind.Malloc or BlockKind.Function => true,
		_ => false,
	};

	/// <summary>
	/// 이 종류의 블록을 함수 몸통이나 블록 영역에 놓을 수 있으면 true. 값 칸에만 들어가는 블록을 뺀 나머지와 함수 블록.
	/// </summary>
	public static bool CanBeStatement(BlockKind kind) => kind == BlockKind.Function || !CanBeValue(kind);

	// 만들기 함수 ----------------------------------------------------------

	/// <summary>
	/// 선언 블록. 예: int x = 3. 초기값을 넣지 않으면 initialValue를 비운다.
	/// </summary>
	public static BlockData Declare(VariableType type, string name, SlotData initialValue = null) =>
		Create(BlockKind.Declare, SlotData.FromTypeOption(type), SlotData.FromName(name), initialValue ?? SlotData.EmptyValue());

	/// <summary>
	/// 대입 블록. 예: x = 5
	/// </summary>
	public static BlockData Assign(string variableName, SlotData value) =>
		Create(BlockKind.Assign, SlotData.VariableOnly(variableName), value);

	/// <summary>
	/// 연산 블록. 예: x + 1
	/// </summary>
	public static BlockData Arithmetic(SlotData left, ArithmeticOperator op, SlotData right) =>
		Create(BlockKind.Arithmetic, left, SlotData.FromOption(op.ToString()), right);

	/// <summary>
	/// 비교 블록. 예: x &gt; 3
	/// </summary>
	public static BlockData Compare(SlotData left, CompareOperator op, SlotData right) =>
		Create(BlockKind.Compare, left, SlotData.FromOption(op.ToString()), right);

	/// <summary>
	/// 논리 블록. 예: a And b
	/// </summary>
	public static BlockData Logic(SlotData left, LogicOperator op, SlotData right) =>
		Create(BlockKind.Logic, left, SlotData.FromOption(op.ToString()), right);

	public static BlockData Not(SlotData value) =>
		Create(BlockKind.Not, value);

	/// <summary>
	/// 조건문 블록. Else는 조건 칸이 없으므로 condition을 넣지 않는다.
	/// </summary>
	public static BlockData Conditional(ConditionalBranch branch, SlotData condition = null)
	{
		SlotData branchSlot = SlotData.FromOption(branch.ToString());
		BlockData block = branch == ConditionalBranch.Else
			? Create(BlockKind.Conditional, branchSlot)
			: Create(BlockKind.Conditional, branchSlot, condition ?? SlotData.EmptyValue());
		block.InnerBlocks = new List<BlockData>();
		return block;
	}

	/// <summary>
	/// for 블록. for (int indexName = start; indexName &lt; end; indexName++)
	/// </summary>
	public static BlockData For(string indexName, SlotData start, SlotData end)
	{
		BlockData block = Create(BlockKind.For, SlotData.FromName(indexName), start, end);
		block.InnerBlocks = new List<BlockData>();
		return block;
	}

	public static BlockData While(SlotData condition)
	{
		BlockData block = Create(BlockKind.While, condition);
		block.InnerBlocks = new List<BlockData>();
		return block;
	}

	public static BlockData Break() => Create(BlockKind.Break);

	public static BlockData Continue() => Create(BlockKind.Continue);

	/// <summary>
	/// 배열 만들기 블록. 예: int arr[5]. elementType은 원소 타입(int)이다.
	/// </summary>
	public static BlockData ArrayDeclare(VariableType elementType, string name, SlotData length) =>
		Create(BlockKind.ArrayDeclare, SlotData.FromTypeOption(elementType), SlotData.FromName(name), length);

	/// <summary>
	/// malloc 블록. 값 칸에 끼워 쓴다. 예: Sword* s = malloc(Sword)
	/// </summary>
	public static BlockData Malloc(VariableType structType) =>
		Create(BlockKind.Malloc, SlotData.FromTypeOption(structType));

	public static BlockData Free(SlotData pointer) =>
		Create(BlockKind.Free, pointer);

	/// <summary>
	/// 사용자 정의 함수를 부르는 함수 블록. arguments는 파라미터 순서대로 넣는다.
	/// </summary>
	public static BlockData CallUserFunction(string functionGuid, params SlotData[] arguments)
	{
		BlockData block = Create(BlockKind.Function, arguments);
		block.FunctionId = functionGuid;
		block.IsUserFunction = true;
		return block;
	}

	/// <summary>
	/// 사전 정의 함수(C 스킬, 입력 함수)를 부르는 함수 블록. arguments는 파라미터 순서대로 넣는다.
	/// </summary>
	public static BlockData CallPredefinedFunction(string functionId, params SlotData[] arguments)
	{
		BlockData block = Create(BlockKind.Function, arguments);
		block.FunctionId = functionId;
		block.IsUserFunction = false;
		return block;
	}

	/// <summary>
	/// return 블록. 돌려줄 값이 없으면 value를 넣지 않는다.
	/// </summary>
	public static BlockData Return(SlotData value = null) =>
		Create(BlockKind.Return, value ?? SlotData.EmptyValue());

	/// <summary>
	/// 기다리기 블록. seconds는 기다릴 시간(초).
	/// </summary>
	public static BlockData Wait(SlotData seconds) =>
		Create(BlockKind.Wait, seconds);

	static BlockData Create(BlockKind kind, params SlotData[] slots) =>
		new BlockData { Kind = kind, Slots = new List<SlotData>(slots) };
}
