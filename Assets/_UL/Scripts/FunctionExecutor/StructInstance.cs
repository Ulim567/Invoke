/// <summary>
/// malloc 블록이 heap에 올리는 struct 객체 하나. 지금은 어떤 struct인지만 가진다.
/// <para>
/// 화면의 오브젝트(예: 소환검)는 만들지 않는다. 오브젝트 생성은 그 오브젝트 담당자의 생성 함수가 맡는다(CLAUDE.md 코드 작성 규칙 3).<br/>
/// 이 객체와 실제 오브젝트를 잇는 방법은 담당자와 정한다(function-executor.md 「맞춘 결과」).
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
