using System.Collections.Generic;

/// <summary>
/// 게임이 미리 정한 struct 타입 하나. 이름과 malloc할 때 쓰는 heap 칸 수를 가진다.
/// </summary>
public class StructDefinition
{
	/// <summary>
	/// struct 이름. 함수 데이터에 이 이름으로 저장된다(VariableType.StructName).
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// malloc할 때 heap에서 쓰는 칸 수.
	/// </summary>
	public int HeapSize { get; }

	public StructDefinition(string name, int heapSize)
	{
		Name = name;
		HeapSize = heapSize;
	}
}

/// <summary>
/// 게임이 미리 정한 struct 타입 목록. 실행기(malloc의 heap 칸 수)와 편집 화면(malloc 블록의 드롭다운 목록)이 함께 쓴다.
/// <para>
/// - struct를 더할 때는 이름 상수와 All의 한 줄을 더한다.<br/>
/// - 한 번 정한 이름은 바꾸거나 지우지 않는다. 함수 데이터에 이름으로 저장되므로, 바꾸면 예전에 저장한 함수가 그 struct를 찾지 못한다.<br/>
/// - 플레이어가 만드는 struct(Type 정의)는 이 목록과 따로 다룬다.
/// </para>
/// </summary>
public static class PredefinedStructs
{
	public const string Sword = "Sword";

	// heap 칸 수는 기획 수치다. 바꿀 때는 이 숫자만 고친다
	static readonly StructDefinition[] all =
	{
		new StructDefinition(Sword, 2),
	};

	/// <summary>
	/// 모든 미리 정한 struct. 편집 화면의 malloc 드롭다운에 쓴다.
	/// </summary>
	public static IReadOnlyList<StructDefinition> All => all;

	/// <summary>
	/// 이름으로 struct를 찾는다. 없는 이름이면 false를 돌려준다.
	/// </summary>
	public static bool TryGet(string name, out StructDefinition definition)
	{
		foreach (StructDefinition candidate in all)
		{
			if (candidate.Name != name) continue;

			definition = candidate;
			return true;
		}

		definition = null;
		return false;
	}
}
