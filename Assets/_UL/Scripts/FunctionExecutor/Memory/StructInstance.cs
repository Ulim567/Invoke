/// <summary>
/// 등록이 없는 struct를 malloc할 때 malloc 블록이 heap에 올리는 객체 하나. 어떤 struct인지만 가진다.
/// <para>
/// 화면의 오브젝트는 만들지 않는다. 실제 오브젝트(예: Sword의 소환검)를 올리려면 그 오브젝트 담당자가 ProcessMemory.StructObjects에 생성 함수를 등록한다.
/// </para>
/// </summary>
public class StructInstance
{
	public StructDefinition Definition { get; }

	public StructInstance(StructDefinition definition)
	{
		Definition = definition;
	}

	public override string ToString() => Definition.Name;
}
