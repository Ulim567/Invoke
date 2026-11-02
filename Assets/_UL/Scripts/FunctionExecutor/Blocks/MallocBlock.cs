/// <summary>
/// malloc 블록. 예: Sword* s = malloc(Sword). expression으로 쓴다. 1틱을 쓴다.
/// <para>
/// - 미리 정한 struct의 객체를 시전한 캐릭터의 heap에 올리고, 그 포인터를 내놓는다.<br/>
/// - 그 struct가 ProcessMemory.StructObjects에 등록되어 있으면 등록한 함수로 실제 객체(예: 소환검)를 만든다. 등록이 없으면 StructInstance를 올린다.<br/>
/// - heap이 모자라거나 등록한 함수가 객체를 만들지 못하면(예: 소환검 5개) NullPointer(0)를 내놓는다. C의 malloc처럼 실패로 끝내지 않는다.<br/>
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

	protected override BlockResult Step(FunctionContext context)
	{
		Result = RuntimeValue.FromPointer(pointerType, Malloc(context));
		return BlockResult.Done;
	}

	int Malloc(FunctionContext context)
	{
		if (!context.Memory.StructObjects.TryGet(definition.Name, out StructObjectRegistry.Entry entry))
			return context.Malloc(new StructInstance(definition), definition.HeapSize);

		// 만들기 전에 heap 남은 칸을 본다. 만든 뒤에 모자란 것을 알면, 서버가 소환검을 만들었다가 같은 틱에 지워 모든 화면에 생성·삭제를 보내게 되기 때문
		// 아래 Malloc도 같은 Free로 검사하고 그 사이에 heap이 바뀌지 않으므로, 여기를 지나면 Malloc은 성공한다
		if (context.Memory.Free < definition.HeapSize) return HeapStorage.NullPointer;

		object obj = entry.Create();
		if (obj == null) return HeapStorage.NullPointer;

		int pointer = context.Malloc(obj, definition.HeapSize);
		// 만들기 함수가 heap을 써서 칸이 모자라진 경우. 만든 객체가 포인터 없이 화면에만 남으므로 등록한 코드의 실수로 드러낸다
		if (pointer == HeapStorage.NullPointer)
		{
			UnityEngine.Debug.LogError($"malloc({definition.Name}): 만들기 함수가 만든 객체를 heap에 올리지 못했습니다. 만들기 함수 안에서 heap을 쓰지 않습니다.");
			return pointer;
		}

		entry.NotifyAllocated(obj, pointer);
		return pointer;
	}
}
