using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 변수 파일(GlobalVariablesData)을 검사하고, 전역 변수 저장소에 선언할 목록(이름, 타입, 초기값)으로 바꾼다.
/// <para>
/// 사용법<br/>
/// - 전역 변수 파일을 저장하기 전에 TryConvert로 검사한다. false이면 저장하지 않는다(compile error처럼 편집 때 거부).<br/>
/// - 초기화할 때도 TryConvert로 바꾼 목록을 전역 변수 저장소에 선언한다.<br/>
/// - capacity는 캐릭터의 메모리 전체 칸 수(ProcessMemory.Capacity)다. 전역 변수 하나가 데이터 영역 1칸을 쓴다.
/// </para>
/// <para>
/// 거부(false)하는 파일. 콘솔 오류를 남긴다.<br/>
/// - 선언 수가 capacity보다 많음<br/>
/// - 선언 블록이 아닌 블록, 지금 실행기가 다룰 수 없는 타입, array, 대상(Target) 타입<br/>
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
		// 초기화할 때 선언 수만큼 데이터 영역을 잡으므로, 칸이 모자란 파일은 저장할 때 막는다
		// Capacity와 비교하는 이유: 초기화가 stack과 heap을 모두 비우므로, 그 순간에는 전역 변수만 칸을 쓰기 때문
		if (declarations.Count > capacity) return Reject($"전역 변수 {declarations.Count}개가 메모리 {capacity}칸보다 많습니다.");

		var converted = new List<(string, VariableType, RuntimeValue)>();
		var names = new HashSet<string>();
		foreach (BlockData block in declarations)
		{
			// 칸: 타입, 이름, 초기값 (함수 몸통의 선언 블록과 같음)
			if (block == null || block.Kind != BlockKind.Declare || block.Slots == null || block.Slots.Count < 3)
				return Reject("전역 변수 파일에는 선언 블록만 놓을 수 있습니다.");

			VariableType type = block.Slots[0].Type;
			string name = block.Slots[1].Name;
			if (!RuntimeValue.IsSupportedType(type)) return Reject($"{type?.Kind} 타입의 전역 변수는 아직 만들 수 없습니다.");
			if (type.Kind == VariableTypeKind.Array) return Reject("전역 array는 아직 만들 수 없습니다.");
			// 전역 변수에 담아 둔 몬스터가 죽으면 전역 변수가 없는 몬스터를 가리키게 되므로, 그 처리를 정하기 전까지 막는다
			if (type.Kind == VariableTypeKind.Target) return Reject("대상 타입은 전역 변수로 선언할 수 없습니다.");
			if (string.IsNullOrEmpty(name)) return Reject("선언 블록의 이름 칸이 비어 있습니다.");
			if (!names.Add(name)) return Reject($"전역 변수 {name}이(가) 두 번 선언되었습니다.");

			if (!TryGetInitialValue(block.Slots[2], type, rule, out RuntimeValue value, out string reason)) return Reject(reason);
			converted.Add((name, type, value));
		}

		variables = converted;
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
