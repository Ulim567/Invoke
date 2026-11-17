/// <summary>
/// 빈 리스트 블록 (Python, #532). 예: orbs = []. heap에 빈 리스트를 올리고 리스트 값을 내놓는다. 1틱을 쓴다.<br/>
/// 빈 리스트는 heap 0칸이다. 원소를 넣을 때마다 1칸씩 는다.
/// </summary>
public class ListNewBlock : ValueBlock
{
	protected override BlockResult Step(FunctionContext context)
	{
		// 0칸이라 heap이 모자라 실패하는 일은 없다. 이 작업의 임시 객체로 올라가고, 변수에 담지 않으면 statement가 끝날 때 해제된다
		int pointer = context.Malloc(new ListStorage(), 0);
		Result = RuntimeValue.FromList(pointer);
		return BlockResult.Done;
	}
}

/// <summary>
/// append 블록 (Python, #532). 예: orbs.append(o). 리스트 끝에 값을 넣는다. 1틱을 쓴다.
/// <para>
/// - heap 1칸을 더 쓴다. 모자라면 MemoryError(OutOfMemory)이고 리스트는 그대로다.<br/>
/// - 이름이 없으면 NameError, 리스트가 아니면 TypeError.
/// </para>
/// </summary>
public class ListAppendBlock : Block
{
	readonly string listName;
	readonly ValueBlock valueOriginal;
	ValueSlots slots;

	public ListAppendBlock(string listName, ValueBlock value)
	{
		this.listName = listName;
		valueOriginal = value;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(valueOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		if (!PythonLists.TryFind(context, listName, out ListStorage list, out int pointer)) return BlockResult.Failed;
		if (!context.Memory.Heap.TryResize(pointer, (list.Count + 1) * PythonLists.CellsPerElement))
		{
			context.RecordFailure(FunctionFailure.OutOfMemory);
			return BlockResult.Failed;
		}
		list.Add(slots[0]);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}

/// <summary>
/// pop 블록 (Python, #532). 예: o = orbs.pop(0), orbs.pop(). 원소 하나를 리스트에서 빼고 그 값을 내놓는다. 1틱을 쓴다.
/// <para>
/// - index를 비우면 맨 끝 원소. 음수 index는 뒤에서부터 센다(-1이 맨 끝).<br/>
/// - heap 1칸을 돌려준다. 빈 리스트이거나 범위 밖이면 IndexError, 정수가 아닌 index는 TypeError.<br/>
/// - statement로 놓으면 꺼낸 값은 버린다. 버린 값이 구슬이면 statement가 끝날 때 가비지 컬렉션이 해제한다.
/// </para>
/// </summary>
public class ListPopBlock : ValueBlock
{
	readonly string listName;
	// 비운 index 칸이면 null
	readonly ValueBlock indexOriginal;
	ValueSlots slots;

	public ListPopBlock(string listName, ValueBlock index)
	{
		this.listName = listName;
		indexOriginal = index;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(indexOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		if (!PythonLists.TryFind(context, listName, out ListStorage list, out int pointer)) return BlockResult.Failed;
		RuntimeValue index = indexOriginal == null ? RuntimeValue.FromInt(-1) : slots[0];
		if (!PythonLists.TryGetIndex(context, list, index, out int number)) return BlockResult.Failed;

		// 원소를 뺀 뒤에 칸을 돌려준다. 꺼낸 값은 이 statement가 끝날 때까지 작업이 쥐므로, 담기 전에 해제되지 않는다
		Result = list.RemoveAt(number);
		context.Memory.Heap.TryResize(pointer, list.Count * PythonLists.CellsPerElement);
		context.HoldTemporary(Result);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}

/// <summary>
/// len 블록 (Python, #532). 예: len(orbs). 리스트의 원소 수를 내놓는다. 1틱을 쓴다. 리스트가 아니면 TypeError.
/// </summary>
public class ListLenBlock : ValueBlock
{
	readonly ValueBlock listOriginal;
	ValueSlots slots;

	public ListLenBlock(ValueBlock list)
	{
		listOriginal = list;
	}

	protected override void OnStart(FunctionContext context)
	{
		slots = new ValueSlots(listOriginal);
	}

	protected override BlockResult Step(FunctionContext context)
	{
		BlockResult result = slots.Step(context);
		if (result != BlockResult.Done) return result;

		if (!PythonLists.TryGet(context, slots[0], out ListStorage list, out _)) return BlockResult.Failed;
		Result = RuntimeValue.FromInt(list.Count);
		return BlockResult.Done;
	}

	protected override void OnCancel(FunctionContext context)
	{
		slots?.Cancel(context);
	}
}
