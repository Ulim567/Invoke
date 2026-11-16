using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 변수 파일(GlobalVariablesData)을 검사하고, 전역 변수 저장소에 선언할 목록(이름, 타입, 초기값)으로 바꾼다.
/// <para>
/// 사용법<br/>
/// - 전역 변수 파일을 저장하기 전에 TryConvert로 검사한다. false이면 저장하지 않는다(compile error처럼 편집 때 거부).<br/>
/// - 초기화할 때도 TryConvert로 바꾼 목록을 전역 변수 저장소에 선언한다.<br/>
/// - capacity는 캐릭터의 메모리 전체 칸 수(ProcessMemory.Capacity)다. 전역 변수 하나가 데이터 영역 1칸, 전역 array는 길이만큼 칸을 쓴다(CellsOf).
/// </para>
/// <para>
/// 거부(false)하는 파일. 이유는 C 용어로 쓴 한국어 문장이다 (#504). 예: 같은 scope에서 재정의할 수 없음: x. 찾은 오류를 모두 돌려받을 수도 있다 (failures, #506)<br/>
/// - 데이터 영역 칸 수 합계가 capacity보다 많음<br/>
/// - 선언·배열 만들기 블록이 아닌 블록, 지금 실행기가 다룰 수 없는 타입, 선언 블록의 array 타입<br/>
/// - 배열 길이가 직접 입력한 1 이상의 정수가 아님<br/>
/// - 이름이 비었거나 같은 이름이 두 번 있음<br/>
/// - 초기값이 직접 입력한 값이 아님(연산, 변수, malloc 등), 읽을 수 없는 글자열, 변수 타입으로 바꿀 수 없는 값
/// </para>
/// </summary>
public static class GlobalVariablesConverter
{
	public static bool TryConvert(GlobalVariablesData data, LanguageRuleData rule, int capacity, out List<(string Name, VariableType Type, RuntimeValue Value)> variables)
	{
		if (TryConvert(data, rule, capacity, out variables, out string error)) return true;
		// 이유를 받지 않는 쪽(프로세스 초기화 등)은 이유를 콘솔 오류로 남긴다
		Debug.LogError($"전역 변수 파일을 저장할 수 없습니다: {error}");
		return false;
	}

	/// <summary>
	/// TryConvert와 같다. 거부하면 error에 첫 번째 이유를 돌려준다 (#504).<br/>
	/// 이 함수는 콘솔에 남기지 않는다. 편집 화면이 다시 그릴 때마다 부르기 때문이다 (#506).
	/// </summary>
	public static bool TryConvert(GlobalVariablesData data, LanguageRuleData rule, int capacity, out List<(string Name, VariableType Type, RuntimeValue Value)> variables, out string error)
	{
		bool ok = TryConvert(data, rule, capacity, out variables, out List<Failure> failures);
		error = ok ? null : failures[0].Message;
		return ok;
	}

	/// <summary>
	/// 전역 변수 파일의 오류 하나 (#506). Index는 data.Declarations 안의 선언 번호,
	/// Slot은 그 선언의 칸 번호(0 타입, 1 이름, 2 처음 값 또는 array 길이)이고 선언 전체의 오류(선언이 아닌 블록, 데이터 영역 초과)면 -1이다.
	/// </summary>
	public readonly struct Failure
	{
		public readonly int Index;
		public readonly int Slot;
		public readonly string Message;

		public Failure(int index, int slot, string message)
		{
			Index = index;
			Slot = slot;
			Message = message;
		}
	}

	/// <summary>
	/// TryConvert와 같다. 거부하면 찾은 오류를 모두 선언 순서대로 failures에 돌려준다 (#506). 통과하면 failures는 비어 있다.<br/>
	/// 편집 화면이 오류마다 카드를 만들고, 그 선언·칸을 빨갛게 표시하고 깜빡일 때 쓴다. 콘솔에 남기지 않는다.<br/>
	/// 선언 하나에서 오류를 찾으면 그 선언은 더 보지 않고 다음 선언으로 넘어간다. 데이터 영역 초과는 파일 전체에 한 번만 알린다.
	/// </summary>
	// 끝까지 검사하는 이유: C 컴파일러처럼 한 파일의 오류를 모두 보여, 하나 고칠 때마다 다음 오류가 나타나지 않게 하기 위함 (함수의 FunctionChecker와 같음)
	// 이유 문장을 C 컴파일러(gcc, clang)의 오류를 C 용어 그대로 옮긴 한국어로 쓰는 이유: 함수의 컴파일 오류(FunctionChecker)와 같은 형식으로 보이기 위함
	public static bool TryConvert(GlobalVariablesData data, LanguageRuleData rule, int capacity, out List<(string Name, VariableType Type, RuntimeValue Value)> variables,
		out List<Failure> failures)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		if (rule == null) throw new ArgumentNullException(nameof(rule));

		variables = null;
		failures = new List<Failure>();
		List<BlockData> declarations = data.Declarations ?? new List<BlockData>();
		var converted = new List<(string, VariableType, RuntimeValue)>();
		var names = new HashSet<string>();
		// 데이터 영역 칸 수 합계. long으로 더하는 이유: 조작한 파일의 아주 큰 길이로 int가 넘쳐 음수가 되면 칸 검사를 지나기 때문
		long cells = 0;
		bool overflowed = false;
		for (int i = 0; i < declarations.Count; i++)
		{
			BlockData block = declarations[i];
			// 칸: 타입(배열 만들기는 원소 타입), 이름, 초기값(배열 만들기는 길이). 함수 몸통의 블록과 같음
			if (block == null || (block.Kind != BlockKind.Declare && block.Kind != BlockKind.ArrayDeclare) || block.Slots == null || block.Slots.Count < 3)
			{
				failures.Add(new Failure(i, -1, "전역 scope에는 선언만 둘 수 있음"));
				continue;
			}

			string name = block.Slots[1].Name;
			if (string.IsNullOrEmpty(name))
			{
				failures.Add(new Failure(i, 1, "선언에 식별자가 없음"));
				continue;
			}
			if (!names.Add(name))
			{
				failures.Add(new Failure(i, 1, $"같은 scope에서 재정의할 수 없음: {name}"));
				continue;
			}

			int length = 1;
			if (block.Kind == BlockKind.ArrayDeclare && !TryGetArrayLength(block, out length, out string lengthReason))
			{
				failures.Add(new Failure(i, 2, lengthReason));
				continue;
			}
			// 초기화할 때 이 칸 수만큼 데이터 영역을 잡으므로, 칸이 모자란 파일은 저장할 때 막는다
			// Capacity와 비교하는 이유: 초기화가 stack과 heap을 모두 비우므로, 그 순간에는 전역 변수만 칸을 쓰기 때문
			// array를 만들기 전에 검사한다. 칸보다 큰 array의 원소를 먼저 만들지 않기 위함. 넘친 뒤로는 칸을 쓰는 값을 만들지 않는다
			cells += length;
			if (cells > capacity)
			{
				// 넘친 칸 수는 파일 전체로 센다. 여기의 cells는 넘친 순간까지만 더한 값이라 뒤의 블록이 빠지기 때문
				if (!overflowed) failures.Add(new Failure(i, -1, $"데이터 영역 크기를 {CountCells(data) - capacity}칸 초과함"));
				overflowed = true;
				continue;
			}

			if (block.Kind == BlockKind.ArrayDeclare)
			{
				if (!TryCreateArray(block.Slots[0].Type, name, length, rule, out RuntimeValue array, out string arrayReason))
				{
					failures.Add(new Failure(i, 0, arrayReason));
					continue;
				}
				converted.Add((name, array.Type, array));
				continue;
			}

			VariableType type = block.Slots[0].Type;
			if (!RuntimeValue.IsSupportedType(type))
			{
				failures.Add(new Failure(i, 0, $"전역 변수로 지원하지 않는 타입: {TypeText(type)}"));
				continue;
			}
			// 함수 몸통과 같은 규칙. array는 배열 만들기 블록으로만 만든다(길이가 있어야 칸 수를 알 수 있기 때문)
			if (type.Kind == VariableTypeKind.Array)
			{
				failures.Add(new Failure(i, 0, $"array는 크기를 지정해 선언해야 함: {name}"));
				continue;
			}
			// 대상(Target) 전역 변수는 받는다 (#474). 담아 둔 몬스터가 죽거나 없어지면 플레이어가 is_valid_target(b)로 확인하고,
			// 그대로 스킬에 넘기면 NoTarget이 난다. 처음 값은 비워 두는 것만 된다(「대상 없음」). 대상은 직접 적을 수 있는 값이 없기 때문
			if (!TryGetInitialValue(block.Slots[2], type, rule, out RuntimeValue value, out string reason))
			{
				failures.Add(new Failure(i, 2, reason));
				continue;
			}
			converted.Add((name, type, value));
		}

		if (failures.Count > 0) return false;
		variables = converted;
		return true;
	}

	/// <summary>
	/// 전역 변수 파일이 데이터 영역에서 쓸 칸 수. 선언 블록은 1칸, 배열 만들기 블록은 길이만큼이다(CellsOf와 같은 규칙).<br/>
	/// 편집 화면이 저장하기 전에 칸 수를 보여 줄 때 쓴다. 검사는 하지 않는다. 길이를 읽을 수 없는 배열 만들기 블록은 1칸으로 센다(저장 검사가 거부함).
	/// </summary>
	// 지역 array의 프레임 크기(FunctionDataConverter.ExtraArrayCells)처럼, 길이가 잘못된 블록은 변수 1칸만 센다
	public static int CountCells(GlobalVariablesData data)
	{
		long cells = 0;
		foreach (BlockData block in data?.Declarations ?? new List<BlockData>())
		{
			if (block == null) continue;
			if (block.Kind == BlockKind.ArrayDeclare && block.Slots != null && block.Slots.Count >= 3
				&& FunctionDataConverter.TryGetArrayLength(block.Slots[2], out int length))
				cells += length;
			else
				cells += 1;
		}
		// 조작한 파일의 아주 큰 길이로 int를 넘으면 가장 큰 값으로 보여 준다
		return (int)Math.Min(cells, int.MaxValue);
	}

	/// <summary>
	/// 전역 변수 하나가 데이터 영역에서 쓰는 칸 수. array는 길이만큼, 그 밖은 1칸이다(지역 array가 스택 프레임에서 쓰는 칸과 같은 규칙).<br/>
	/// TryConvert의 칸 검사와 ProcessMemory.DataUsed가 같은 규칙으로 세게 하려고 함께 쓴다.
	/// </summary>
	internal static int CellsOf(RuntimeValue value) =>
		value.Type?.Kind == VariableTypeKind.Array ? value.AsArray().Length : 1;

	// 배열 만들기 블록의 길이 칸. 함수 몸통의 지역 array와 같은 규칙(FunctionDataConverter.TryGetArrayLength)
	static bool TryGetArrayLength(BlockData block, out int length, out string reason)
	{
		reason = null;
		if (FunctionDataConverter.TryGetArrayLength(block.Slots[2], out length)) return true;
		reason = $"array 크기에는 양의 정수 상수만 쓸 수 있음: {block.Slots[1].Name}";
		return false;
	}

	// 모든 원소를 캐릭터 언어의 기본값으로 채운 array를 만든다. 초기값 목록({1, 2, 3})은 받지 않는다(지역 array와 같음)
	// 호출마다 새 원소 저장소를 만든다. 초기화(RestartProcess)마다 원소가 기본값으로 돌아가게 하기 위함
	static bool TryCreateArray(VariableType elementType, string name, int length, LanguageRuleData rule, out RuntimeValue array, out string reason)
	{
		array = default;
		reason = null;
		VariableType arrayType = VariableType.ArrayOf(elementType);
		if (!RuntimeValue.IsSupportedType(arrayType))
		{
			reason = $"전역 array의 원소로 지원하지 않는 타입: {TypeText(elementType)}";
			return false;
		}
		if (!rule.TryGetDefaultValue(elementType, out RuntimeValue initialValue))
		{
			reason = $"기본값이 없는 타입이라 초기값을 생략할 수 없음: {TypeText(elementType)}";
			return false;
		}

		array = RuntimeValue.FromArray(arrayType, new ArrayStorage(elementType, length, initialValue));
		return true;
	}

	// 초기값 칸을 값으로 바꾼다. 비었으면 캐릭터 언어의 기본값, 직접 입력한 값이면 변수 타입으로 맞춘 값이다
	// 직접 입력한 값만 받는 이유: C에서 전역 변수의 초기값은 컴파일할 때 정해지는 상수여야 하기 때문(malloc, 변수, 함수 호출 불가)
	static bool TryGetInitialValue(SlotData slot, VariableType type, LanguageRuleData rule, out RuntimeValue value, out string reason)
	{
		reason = null;
		if (slot == null || IsEmpty(slot))
		{
			if (rule.TryGetDefaultValue(type, out value)) return true;
			reason = $"기본값이 없는 타입이라 초기값을 생략할 수 없음: {TypeText(type)}";
			return false;
		}

		value = default;
		if (slot.Block != null || slot.VariableName != null)
		{
			reason = "전역 변수의 초기값에는 상수만 쓸 수 있음";
			return false;
		}
		if (!LiteralValue.TryParse(slot.Type, slot.Literal, out RuntimeValue parsed))
		{
			reason = $"{TypeText(type)} 타입에 쓸 수 없는 초기값: {slot.Literal}";
			return false;
		}
		// LiteralValue를 함께 넘기는 이유: 포인터 변수에 직접 입력한 0을 NULL로 볼지(ZeroIsNull)를 선언 블록과 같은 규칙으로 판단하기 위함
		// 포인터에 직접 입력할 수 있는 값은 NULL과 0뿐이므로, 포인터 초기값은 여기서 저절로 NULL만 남는다
		if (!NumericRules.TryConvertForStore(new LiteralValue(slot.Type, slot.Literal), parsed, type, rule, out value))
		{
			reason = $"{TypeText(type)} 타입에 쓸 수 없는 초기값: {slot.Literal}";
			return false;
		}
		return true;
	}

	static bool IsEmpty(SlotData slot) => slot.Block == null && slot.VariableName == null && slot.Literal == null;

	// 오류 문장에 쓸 타입 글자. C처럼 소문자 이름, 포인터는 가리키는 struct 이름 뒤에 *
	static string TypeText(VariableType type)
	{
		if (type == null) return "?";
		switch (type.Kind)
		{
			case VariableTypeKind.Pointer: return (type.ElementType != null ? TypeText(type.ElementType) : "void") + "*";
			case VariableTypeKind.Struct: return type.StructName ?? "struct";
			case VariableTypeKind.Array: return TypeText(type.ElementType) + "[]";
			case VariableTypeKind.Target: return "Target";
			default: return type.Kind.ToString().ToLowerInvariant();
		}
	}
}
