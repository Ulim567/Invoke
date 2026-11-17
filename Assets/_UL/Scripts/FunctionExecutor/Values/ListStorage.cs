using System.Collections.Generic;
using System.Numerics;

/// <summary>
/// Python 리스트 하나의 원소들 (#532). heap 객체이고, 리스트 값(RuntimeValue.IsList)은 이 객체를 올린 heap 포인터를 가리킨다.
/// <para>
/// - heap 칸 수 = 원소 수. 빈 리스트는 0칸이고, append 한 번에 1칸 늘고 pop 한 번에 1칸 준다.<br/>
/// - 원소는 어떤 타입이든 섞어 담는다. 원소에 담은 구슬·리스트는 가비지 컬렉션의 루트다(리스트가 살아 있는 동안).<br/>
/// - 스킬은 Count, this[i], Elements로 읽기만 한다. 바꾸는 일은 리스트 블록만 한다.
/// </para>
/// </summary>
// 원소를 바꾸는 함수를 internal로 두는 이유: heap 칸 수와 원소 수가 늘 같아야 하는데, 칸 수는 리스트 블록이 HeapStorage와 함께 바꾸기 때문
public class ListStorage
{
	readonly List<RuntimeValue> elements = new List<RuntimeValue>();

	public int Count => elements.Count;

	public RuntimeValue this[int index] => elements[index];

	public IReadOnlyList<RuntimeValue> Elements => elements;

	internal void Add(RuntimeValue value) => elements.Add(value);

	internal void Set(int index, RuntimeValue value) => elements[index] = value;

	internal RuntimeValue RemoveAt(int index)
	{
		RuntimeValue value = elements[index];
		elements.RemoveAt(index);
		return value;
	}

	/// <summary>
	/// Python의 index(음수면 뒤에서부터)를 원소 번호로 바꾼다. 범위 밖이면 false(IndexError).
	/// </summary>
	internal bool TryNormalize(BigInteger index, out int number)
	{
		number = -1;
		if (index < 0) index += Count;
		if (index < 0 || index >= Count) return false;
		number = (int)index;
		return true;
	}
}

/// <summary>
/// Python 리스트 블록이 함께 쓰는 도우미 (#532).
/// </summary>
internal static class PythonLists
{
	// 리스트 값의 heap 칸 수 단위. 원소 하나에 1칸
	internal const int CellsPerElement = 1;

	/// <summary>
	/// 변수 name에 담긴 리스트를 찾는다. 이름이 없으면 NameError(UndeclaredVariable), 리스트가 아니면 TypeError(TypeMismatch)를 기록하고 false.
	/// </summary>
	public static bool TryFind(FunctionContext context, string name, out ListStorage list, out int pointer)
	{
		list = null;
		pointer = HeapStorage.NullPointer;
		VariableStore variables = context.FindVariables(name);
		if (variables == null || !variables.TryGet(name, out RuntimeValue value))
		{
			context.RecordFailure(FunctionFailure.UndeclaredVariable);
			return false;
		}
		return TryGet(context, value, out list, out pointer);
	}

	/// <summary>
	/// 값 value가 가리키는 리스트를 꺼낸다. 리스트가 아니면 TypeError(TypeMismatch)를 기록하고 false.
	/// </summary>
	public static bool TryGet(FunctionContext context, RuntimeValue value, out ListStorage list, out int pointer)
	{
		list = null;
		pointer = HeapStorage.NullPointer;
		if (!value.IsList || context.Memory == null || !context.Memory.Heap.TryGet(value.AsObjectPointer(), out list))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}
		pointer = value.AsObjectPointer();
		return true;
	}

	/// <summary>
	/// index 값을 원소 번호로 바꾼다. 정수(bool 포함)가 아니면 TypeError, 범위 밖이면 IndexError를 기록하고 false.
	/// </summary>
	public static bool TryGetIndex(FunctionContext context, ListStorage list, RuntimeValue index, out int number)
	{
		number = -1;
		if (!PythonRules.TryGetInteger(index, out BigInteger value))
		{
			context.RecordFailure(FunctionFailure.TypeMismatch);
			return false;
		}
		if (!list.TryNormalize(value, out number))
		{
			context.RecordFailure(FunctionFailure.IndexOutOfRange);
			return false;
		}
		return true;
	}
}
