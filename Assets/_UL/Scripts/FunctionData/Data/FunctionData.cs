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
	/// 다른 함수 안에서 불렸을 때 기다리지 않으면 true. 이때 부른 쪽은 이 함수를 새 작업으로 시전하고 바로 다음 블록으로 넘어간다.<br/>
	/// 비동기 함수는 반환 값을 받을 수 없으므로 값 칸에 끼울 수 없다.
	/// </summary>
	// 형식을 넓힐 때 규칙에 따라 맨 뒤에 더한 칸이다. 이 칸이 없는 예전 데이터는 false(동기)로 읽힌다
	public bool IsAsync { get; set; }

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

	/// <summary>
	/// global 블록에 적은 이름들 (Python, #532). 이 함수 안에서 이 이름에 대입하면 전역 변수를 바꾼다.
	/// </summary>
	public HashSet<string> GlobalNames()
	{
		HashSet<string> names = new HashSet<string>();
		foreach (BlockData block in AllBlocks())
			if (block.Kind == BlockKind.Global && block.Slots.Count >= 1 && !string.IsNullOrEmpty(block.Slots[0].Name))
				names.Add(block.Slots[0].Name);
		return names;
	}

	/// <summary>
	/// Python 함수의 지역 이름들 (#532). 파라미터, 대입 블록의 왼쪽, for의 변수, del의 변수 가운데 global 블록에 적지 않은 이름이다.<br/>
	/// 블록 위치와 상관없이 함수 전체에서 모은다. Python은 함수 안 어디에서든 대입하는 이름을 그 함수 전체의 지역 변수로 보기 때문이다.<br/>
	/// 프레임 크기(1 + 지역 이름 수)를 계산할 때와, 변수를 읽을 때 지역·전역 중 어디서 찾을지 정할 때 쓴다.
	/// </summary>
	// if 안에서 대입한 이름도 if 밖에서 같은 지역 변수다. Python에는 { } 범위가 없기 때문
	public HashSet<string> PythonLocalNames()
	{
		HashSet<string> globals = GlobalNames();
		HashSet<string> names = new HashSet<string>();
		foreach (ParameterData parameter in Parameters)
			if (!string.IsNullOrEmpty(parameter?.Name)) names.Add(parameter.Name);

		foreach (BlockData block in AllBlocks())
		{
			string name = block.Kind switch
			{
				BlockKind.Assign when block.Slots.Count >= 1 => block.Slots[0].VariableName,
				// 리스트 만들기(name = [])도 대입이다
				BlockKind.ListNew when block.Slots.Count >= 1 => block.Slots[0].VariableName,
				BlockKind.For when block.Slots.Count >= 1 => block.Slots[0].Name,
				// Python에서 del x도 x를 그 함수의 지역 이름으로 만든다
				BlockKind.Del when block.Slots.Count >= 1 => block.Slots[0].VariableName,
				_ => null,
			};
			if (!string.IsNullOrEmpty(name) && !globals.Contains(name)) names.Add(name);
		}
		return names;
	}
}
