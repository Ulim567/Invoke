/// <summary>
/// malloc 블록. 예: Sword* s = malloc(Sword). 값 칸에 끼워 쓴다. 1틱을 쓴다.
/// <para>
/// - 미리 정한 struct의 객체(StructInstance)를 시전한 캐릭터의 heap에 올리고, 그 포인터를 내놓는다.<br/>
/// - heap이 모자라면 NullPointer(0)를 내놓는다. C의 malloc처럼 실패로 끝내지 않는다.<br/>
/// - 포인터는 이 작업의 포인터 목록에 들어간다. 이후 규칙(정상 종료 시 전역 포인터 목록, 끊기면 누수)은 HeapStorage를 따른다.
/// </para>
/// </summary>
public class MallocBlock : ValueBlock
{
	readonly StructDefinition definition;
	// 내놓을 포인터의 타입. 예: Sword*
	readonly VariableType pointerType;

	public MallocBlock(StructDefinition definition)
	{
		this.definition = definition;
		pointerType = VariableType.PointerTo(VariableType.StructOf(definition.Name));
	}

	protected override BlockResult Step(FunctionTask task)
	{
		int pointer = task.Memory.Heap.Malloc(task, new StructInstance(definition), definition.HeapSize);
		Result = RuntimeValue.FromPointer(pointerType, pointer);
		return BlockResult.Done;
	}
}
