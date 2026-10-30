using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함수 데이터(FunctionData)를 실행기가 실행할 수 있는 사용자 정의 함수(UserFunction)로 바꾼다.
/// <para>
/// 사용법<br/>
/// - TryConvert(함수 데이터, out 함수)로 바꾸고, 성공하면 그 함수를 FunctionExecutor.Cast에 넘긴다.<br/>
/// - 바꾼 함수는 원본이다. 슬롯은 같은 객체를 계속 Cast에 넘겨야 쿨타임이 이어진다.
/// </para>
/// <para>
/// 거부(false)하는 함수: 지금 실행기가 아직 실행할 수 없는 기능을 쓴 함수다. 콘솔 오류를 남긴다.<br/>
/// - 예: 조건문·for 블록, 위치 타입 변수, 비어 있는 값 칸(함수 블록의 파라미터 칸은 비워도 됨), 없는 고정 ID<br/>
/// - 플레이어 코드의 잘못(없는 변수, 같은 이름 두 번 선언 등)은 거부하지 않는다. 실행 중에 그 블록이 실패 종류를 기록하고 실패한다.
/// </para>
/// </summary>
// 바꿀 수 있는 블록은 이슈가 진행되며 늘어난다. 새 블록은 ConvertStatement, ConvertValue의 switch에 더한다
public static class FunctionDataConverter
{
	public static bool TryConvert(FunctionData data, out UserFunction function)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));

		function = null;
		List<Block> blocks = new List<Block>();
		foreach (BlockData blockData in data.Blocks)
		{
			Block block = ConvertStatement(data, blockData);
			if (block == null) return false;
			blocks.Add(block);
		}

		// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수
		int frameSize = 1 + data.Parameters.Count + data.LocalVariables().Count;
		function = new UserFunction(data.Name, frameSize, SumCooldownTicks(data), blocks.ToArray());
		return true;
	}

	// 함수 몸통과 값 칸 안의 모든 사전 정의 함수 블록의 쿨타임 합. 같은 스킬을 여러 번 넣으면 그만큼 늘어난다
	// 반복문 안의 함수는 한 번만 센다. 몇 번 반복될지 미리 알 수 없기 때문
	static int SumCooldownTicks(FunctionData data)
	{
		int sum = 0;
		foreach (BlockData block in data.AllBlocks())
		{
			if (block.Kind != BlockKind.Function || block.IsUserFunction) continue;
			if (!string.IsNullOrEmpty(block.FunctionId) && PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction function))
				sum += function.CooldownTicks;
		}
		return sum;
	}

	// 함수 몸통에 놓인 블록 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
	static Block ConvertStatement(FunctionData data, BlockData block)
	{
		switch (block.Kind)
		{
			// 칸: 타입, 이름, 초기값
			case BlockKind.Declare when block.Slots.Count >= 3:
			{
				VariableType type = block.Slots[0].Type;
				string name = block.Slots[1].Name;
				if (!RuntimeValue.IsSupportedType(type)) return Reject(data, $"{type?.Kind} 타입의 변수는 아직 실행할 수 없습니다.");
				if (string.IsNullOrEmpty(name)) return Reject(data, "선언 블록의 이름 칸이 비어 있습니다.");

				// 초기값 칸은 비워도 된다. 그때는 실행할 때 기본값을 넣는다
				SlotData initialSlot = block.Slots[2];
				if (IsEmpty(initialSlot)) return new DeclareBlock(name, type, null);

				ValueBlock initialValue = ConvertValue(data, initialSlot);
				return initialValue == null ? null : new DeclareBlock(name, type, initialValue);
			}

			// 칸: 변수, 값
			case BlockKind.Assign when block.Slots.Count >= 2:
			{
				string variableName = block.Slots[0].VariableName;
				if (string.IsNullOrEmpty(variableName)) return Reject(data, "대입 블록의 변수 칸이 비어 있습니다.");

				ValueBlock value = ConvertValue(data, block.Slots[1]);
				return value == null ? null : new AssignBlock(variableName, value);
			}

			// 칸: 파라미터 수만큼의 값. 반환 값이 있어도 버린다
			case BlockKind.Function:
				return TryConvertCall(data, block, out PredefinedFunction function, out ValueBlock[] arguments)
					? new CallPredefinedBlock(function, arguments)
					: null;
		}

		return Reject(data, $"{block.Kind} 블록은 아직 실행할 수 없습니다.");
	}

	// 함수 블록의 부를 함수와 파라미터 값 칸을 바꾼다. 몸통의 함수 블록과 값 칸의 함수 블록이 함께 쓴다. 바꿀 수 없으면 콘솔 오류를 남기고 false
	static bool TryConvertCall(FunctionData data, BlockData block, out PredefinedFunction function, out ValueBlock[] arguments)
	{
		function = null;
		arguments = null;
		string rejection = null;
		if (block.IsUserFunction) rejection = "사용자 정의 함수 호출은 아직 실행할 수 없습니다.";
		else if (string.IsNullOrEmpty(block.FunctionId) || !PredefinedFunctionRegistry.TryGet(block.FunctionId, out function))
			rejection = $"고정 ID {block.FunctionId}의 함수를 찾을 수 없습니다.";
		else if (block.Slots.Count > function.Parameters.Count)
			rejection = $"{function.Name}의 값 칸이 파라미터 수({function.Parameters.Count})보다 많습니다.";

		if (rejection != null)
		{
			Reject(data, rejection);
			return false;
		}

		// 비운 값 칸은 null로 넘긴다. 스킬은 값이 없을 때 슬롯에서 바로 시전했을 때처럼 동작한다
		arguments = new ValueBlock[block.Slots.Count];
		for (int i = 0; i < arguments.Length; i++)
		{
			if (IsEmpty(block.Slots[i])) continue;
			arguments[i] = ConvertValue(data, block.Slots[i]);
			if (arguments[i] == null) return false;
		}
		return true;
	}

	// 값 칸 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
	static ValueBlock ConvertValue(FunctionData data, SlotData slot)
	{
		if (slot.Block != null) return ConvertValueBlock(data, slot.Block);
		if (slot.VariableName != null) return new VariableValue(slot.VariableName);
		// 글자열이 그 타입의 값으로 바뀌는지는 실행할 때 확인한다(InvalidLiteral). 플레이어 코드의 잘못이기 때문
		if (slot.Literal != null && slot.Type != null) return new LiteralValue(slot.Type, slot.Literal);

		return Reject<ValueBlock>(data, "값 칸이 비어 있습니다.");
	}

	// 값 칸에 끼운 블록 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
	static ValueBlock ConvertValueBlock(FunctionData data, BlockData block)
	{
		switch (block.Kind)
		{
			// 칸: 왼쪽 값, 연산자, 오른쪽 값
			case BlockKind.Arithmetic when block.Slots.Count >= 3:
			{
				// "5"처럼 숫자 글자열도 TryParse를 통과하므로, 정의된 값인지 한 번 더 본다
				if (!System.Enum.TryParse(block.Slots[1].Option, out ArithmeticOperator op) || !System.Enum.IsDefined(typeof(ArithmeticOperator), op))
					return Reject<ValueBlock>(data, $"연산자 {block.Slots[1].Option}을(를) 알 수 없습니다.");

				ValueBlock left = ConvertValue(data, block.Slots[0]);
				if (left == null) return null;
				ValueBlock right = ConvertValue(data, block.Slots[2]);
				return right == null ? null : new ArithmeticBlock(left, op, right);
			}

			// 값을 돌려주는 사전 정의 함수. 칸: 파라미터 수만큼의 값
			case BlockKind.Function:
			{
				if (!TryConvertCall(data, block, out PredefinedFunction function, out ValueBlock[] arguments)) return null;

				VariableType returnType = function.ReturnType;
				if (returnType.Kind == VariableTypeKind.Void)
					return Reject<ValueBlock>(data, $"{function.Name}은(는) 값을 돌려주지 않으므로 값 칸에 끼울 수 없습니다.");
				if (!RuntimeValue.IsSupportedType(returnType))
					return Reject<ValueBlock>(data, $"{function.Name}의 반환 타입 {returnType.Kind}은(는) 아직 실행할 수 없습니다.");
				// 비동기 함수는 끝나기를 기다리지 않으므로 반환 값을 받을 수 없다
				if (function.IsAsync)
					return Reject<ValueBlock>(data, $"{function.Name}은(는) 비동기 함수라 값 칸에 끼울 수 없습니다.");

				return new PredefinedValueBlock(function, arguments);
			}
		}

		return Reject<ValueBlock>(data, $"값 칸의 {block.Kind} 블록은 아직 실행할 수 없습니다.");
	}

	static bool IsEmpty(SlotData slot) => slot.Block == null && slot.VariableName == null && slot.Literal == null;

	// 거부 이유를 남기고 null을 돌려준다. Block과 ValueBlock 어느 쪽 자리에서도 쓰기 위해 제네릭으로 둔다
	static T Reject<T>(FunctionData data, string reason) where T : class
	{
		Debug.LogError($"함수 {data.Name}을(를) 실행할 수 없습니다: {reason}");
		return null;
	}

	static Block Reject(FunctionData data, string reason) => Reject<Block>(data, reason);
}
