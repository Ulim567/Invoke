/// <summary>
/// 사용자 정의 함수의 파라미터 하나.
/// </summary>
public class ParameterData
{
	public string Name { get; set; }

	public VariableType Type { get; set; }

	// JSON에서 되돌릴 때 Newtonsoft Json이 빈 객체를 먼저 만들고 칸을 채우므로 필요하다
	public ParameterData() { }

	public ParameterData(string name, VariableType type)
	{
		Name = name;
		Type = type;
	}
}
