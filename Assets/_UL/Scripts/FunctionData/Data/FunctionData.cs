using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 플레이어가 블록 코딩으로 만든 사용자 정의 함수 하나. 저장·전송용이며, 실행은 FunctionExecutor의 UserFunction이 한다.
/// <para>
/// 사용법<br/>
/// - 새 함수는 Create로 만든다. GUID가 이때 한 번 붙고, 이름을 바꾸거나 블록을 고쳐도 바뀌지 않는다.<br/>
/// - 편집 화면은 이 객체를 들고 Blocks에 블록을 넣고 빼며, 저장할 때 FunctionDataJson으로 JSON 글자열로 바꾼다.
/// </para>
/// <para>
/// 블록 수와 지역 변수 목록은 저장하지 않고 부를 때마다 블록을 훑어 계산한다.<br/>
/// 따로 저장해 두면 블록을 고친 뒤 저장한 값과 실제 블록이 어긋날 수 있기 때문이다.<br/>
/// 프로퍼티가 아니라 함수로 둔 이유: public 프로퍼티는 Newtonsoft Json이 JSON에 함께 적기 때문이다.
/// </para>
/// </summary>
public class FunctionData
{
	/// <summary>
	/// 다른 함수의 함수 블록이 이 함수를 부를 때 쓰는 값.
	/// </summary>
	public string Guid { get; set; }

	public string Name { get; set; }

	/// <summary>
	/// 돌려주는 값의 타입. 돌려주는 값이 없으면 Void.
	/// </summary>
	public VariableType ReturnType { get; set; }

	public List<ParameterData> Parameters { get; set; } = new List<ParameterData>();

	/// <summary>
	/// 함수 몸통의 블록들. 위에서부터 순서대로.
	/// </summary>
	public List<BlockData> Blocks { get; set; } = new List<BlockData>();

	/// <summary>
	/// 새 함수를 만든다. GUID를 새로 붙이고, 반환 타입은 Void, 파라미터와 블록은 비운다.
	/// </summary>
	public static FunctionData Create(string name) => new FunctionData
	{
		Guid = System.Guid.NewGuid().ToString(),
		Name = name,
		ReturnType = new VariableType(VariableTypeKind.Void),
	};

	/// <summary>
	/// 함수 몸통의 모든 블록을 칸과 블록 영역 안까지 차례로 돌려준다.
	/// </summary>
	public IEnumerable<BlockData> AllBlocks() => Blocks.SelectMany(block => block.SelfAndInnerBlocks());

	/// <summary>
	/// 이 함수의 블록 수. 부르는 다른 함수의 블록은 세지 않는다. (슬롯 전체의 크기는 CodeSizeCalculator가 계산)
	/// </summary>
	public int CountBlocks() => AllBlocks().Count();

	/// <summary>
	/// 이 함수가 만드는 지역 변수들. 선언 블록, 배열 만들기 블록, for 블록의 변수를 블록 순서대로 모은다.<br/>
	/// 나중에 프레임 크기(1 + 파라미터 수 + 지역 변수 수)를 계산할 때 쓴다.
	/// </summary>
	public List<(string Name, VariableType Type)> LocalVariables()
	{
		List<(string, VariableType)> variables = new List<(string, VariableType)>();

		foreach (BlockData block in AllBlocks())
		{
			// 칸이 모자란 블록은 구조 검사에서 걸러낼 일이므로, 여기서는 오류를 내지 않고 건너뛴다
			switch (block.Kind)
			{
				case BlockKind.Declare when block.Slots.Count >= 2:
					variables.Add((block.Slots[1].Name, block.Slots[0].Type));
					break;
				case BlockKind.ArrayDeclare when block.Slots.Count >= 2:
					variables.Add((block.Slots[1].Name, VariableType.ArrayOf(block.Slots[0].Type)));
					break;
				// for (int i = …)의 i도 실제 C처럼 지역 변수로 센다
				case BlockKind.For when block.Slots.Count >= 1:
					variables.Add((block.Slots[0].Name, new VariableType(VariableTypeKind.Int)));
					break;
			}
		}

		return variables;
	}
}
