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
/// 거부(false)하는 파일. 콘솔 오류를 남긴다.<br/>
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
		if (data == null) throw new ArgumentNullException(nameof(data));
		if (rule == null) throw new ArgumentNullException(nameof(rule));

		variables = null;
		List<BlockData> declarations = data.Declarations ?? new List<BlockData>();
		var converted = new List<(string, VariableType, RuntimeValue)>();
		var names = new HashSet<string>();
		// 데이터 영역 칸 수 합계. long으로 더하는 이유: 조작한 파일의 아주 큰 길이로 int가 넘쳐 음수가 되면 칸 검사를 지나기 때문
		long cells = 0;
		foreach (BlockData block in declarations)
		{
			// 칸: 타입(배열 만들기는 원소 타입), 이름, 초기값(배열 만들기는 길이). 함수 몸통의 블록과 같음
			if (block == null || (block.Kind != BlockKind.Declare && block.Kind != BlockKind.ArrayDeclare) || block.Slots == null || block.Slots.Count < 3)
				return Reject("전역 변수 파일에는 선언 블록과 배열 만들기 블록만 놓을 수 있습니다.");

			string name = block.Slots[1].Name;
			if (string.IsNullOrEmpty(name)) return Reject($"{(block.Kind == BlockKind.Declare ? "선언" : "배열 만들기")} 블록의 이름 칸이 비어 있습니다.");
			if (!names.Add(name)) return Reject($"전역 변수 {name}이(가) 두 번 선언되었습니다.");

			int length = 1;
			if (block.Kind == BlockKind.ArrayDeclare && !TryGetArrayLength(block, out length, out string lengthReason)) return Reject(lengthReason);
			// 초기화할 때 이 칸 수만큼 데이터 영역을 잡으므로, 칸이 모자란 파일은 저장할 때 막는다
			// Capacity와 비교하는 이유: 초기화가 stack과 heap을 모두 비우므로, 그 순간에는 전역 변수만 칸을 쓰기 때문
			// array를 만들기 전에 검사한다. 칸보다 큰 array의 원소를 먼저 만들지 않기 위함
			cells += length;
			if (cells > capacity) return Reject($"전역 변수가 데이터 영역 {cells}칸 이상을 써서 메모리 {capacity}칸보다 많습니다.");

			if (block.Kind == BlockKind.ArrayDeclare)
			{
				if (!TryCreateArray(block.Slots[0].Type, name, length, rule, out RuntimeValue array, out string arrayReason)) return Reject(arrayReason);
				converted.Add((name, array.Type, array));
				continue;
			}

			VariableType type = block.Slots[0].Type;
			if (!RuntimeValue.IsSupportedType(type)) return Reject($"{type?.Kind} 타입의 전역 변수는 아직 만들 수 없습니다.");
			// 함수 몸통과 같은 규칙. array는 배열 만들기 블록으로만 만든다(길이가 있어야 칸 수를 알 수 있기 때문)
			if (type.Kind == VariableTypeKind.Array) return Reject($"전역 array {name}은(는) 배열 만들기 블록으로 선언해야 합니다.");
			// 대상(Target) 전역 변수는 받는다 (#474). 담아 둔 몬스터가 죽거나 없어지면 플레이어가 is_valid_target(b)로 확인하고,
			// 그대로 스킬에 넘기면 InvalidTarget이 난다. 처음 값은 비워 두는 것만 된다(「대상 없음」). 대상은 직접 적을 수 있는 값이 없기 때문
			if (!TryGetInitialValue(block.Slots[2], type, rule, out RuntimeValue value, out string reason)) return Reject(reason);
			converted.Add((name, type, value));
		}

		variables = converted;
		return true;
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
		reason = $"전역 array {block.Slots[1].Name}의 길이는 직접 입력한 1 이상의 정수여야 합니다.";
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
			reason = $"{elementType?.Kind} 원소의 전역 array {name}은(는) 아직 만들 수 없습니다.";
			return false;
		}
		if (!rule.TryGetDefaultValue(elementType, out RuntimeValue initialValue))
		{
			reason = $"{elementType.Kind} 타입의 기본값이 없습니다.";
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
			reason = $"{type.Kind} 타입의 기본값이 없습니다.";
			return false;
		}

		value = default;
		if (slot.Block != null || slot.VariableName != null)
		{
			reason = "전역 변수의 초기값에는 직접 입력한 값만 넣을 수 있습니다.";
			return false;
		}
		if (!LiteralValue.TryParse(slot.Type, slot.Literal, out RuntimeValue parsed))
		{
			reason = $"초기값 {slot.Literal}을(를) 읽을 수 없습니다.";
			return false;
		}
		// LiteralValue를 함께 넘기는 이유: 포인터 변수에 직접 입력한 0을 NULL로 볼지(ZeroIsNull)를 선언 블록과 같은 규칙으로 판단하기 위함
		// 포인터에 직접 입력할 수 있는 값은 NULL과 0뿐이므로, 포인터 초기값은 여기서 저절로 NULL만 남는다
		if (!NumericRules.TryConvertForStore(new LiteralValue(slot.Type, slot.Literal), parsed, type, rule, out value))
		{
			reason = $"초기값 {slot.Literal}을(를) {type.Kind} 타입으로 바꿀 수 없습니다.";
			return false;
		}
		return true;
	}

	static bool IsEmpty(SlotData slot) => slot.Block == null && slot.VariableName == null && slot.Literal == null;

	static bool Reject(string reason)
	{
		Debug.LogError($"전역 변수 파일을 저장할 수 없습니다: {reason}");
		return false;
	}
}
