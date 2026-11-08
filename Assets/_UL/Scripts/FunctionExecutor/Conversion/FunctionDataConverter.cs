using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함수 데이터(FunctionData)를 실행기가 실행할 수 있는 사용자 정의 함수(UserFunction)로 바꾼다.
/// <para>
/// 사용법<br/>
/// - TryConvert(함수 데이터, 캐릭터가 가진 함수들, out 함수)로 바꾸고, 성공하면 그 함수를 FunctionExecutor.Cast에 넘긴다.<br/>
/// - 캐릭터가 가진 함수들은 함수 블록이 부르는 사용자 정의 함수를 GUID로 찾을 때 쓴다. 다른 사용자 정의 함수를 부르지 않으면 빈 목록을 넘긴다.<br/>
/// - 바꾼 함수는 원본이다. 슬롯은 같은 객체를 계속 Cast에 넘겨야, 실행 중에 같은 함수를 다시 시전하지 못하게 막는 검사가 이어진다.<br/>
/// - 부른 함수를 고치면 그 함수를 부르는 함수도 다시 바꿔야 한다. 바꾼 함수는 바꾼 순간의 함수 데이터로 만들어지기 때문이다.
/// </para>
/// <para>
/// 부르는 함수<br/>
/// - 한 번 바꿀 때 같은 GUID의 함수는 한 번만 바꾼다. 같은 함수를 부르는 함수 블록들이 바꾼 함수 하나를 함께 쓴다.<br/>
/// - 재귀 호출(자기 자신이나 서로를 부르는 함수)도 바꾼다. 끝나지 않는 재귀는 실행 중에 스택이 모자라 Overflow로 끝난다.
/// </para>
/// <para>
/// 거부(false)하는 함수: 지금 실행기가 아직 실행할 수 없는 기능을 쓴 함수다. 콘솔 오류를 남긴다.<br/>
/// - 예: 위치 타입 변수, 앞에 if가 없는 else, 비어 있는 value slot(사전 정의 함수 블록의 파라미터 칸은 비워도 됨), 없는 고정 ID, 없는 GUID<br/>
/// - 플레이어 코드의 잘못(없는 변수, 같은 이름 두 번 선언 등)은 거부하지 않는다. 실행 중에 그 블록이 실패 종류를 기록하고 실패한다.
/// </para>
/// </summary>
// 바꿀 수 있는 블록은 이슈가 진행되며 늘어난다. 새 블록은 ConvertStatement, ConvertValueBlock의 switch에 더한다
// 조건문은 블록 여러 개를 하나로 묶으므로 ConvertStatement가 아니라 TryConvertStatements가 따로 바꾼다
public static class FunctionDataConverter
{
	public static bool TryConvert(FunctionData data, IEnumerable<FunctionData> ownedFunctions, out UserFunction function)
	{
		if (data == null) throw new ArgumentNullException(nameof(data));
		if (ownedFunctions == null) throw new ArgumentNullException(nameof(ownedFunctions));

		function = new Conversion(data, ownedFunctions).ConvertFunction(data);
		return function != null;
	}

	// 배열 길이 칸의 값. 직접 입력한 1 이상의 int만 받는다
	// 변수나 블록을 받지 않는 이유: 함수가 시작할 때 프레임을 한 번에 받으므로, 길이를 실행 전에 알아야 하기 때문(C89와 같음)
	// internal인 이유: 전역 array(GlobalVariablesConverter)도 같은 길이 규칙을 따르게 하기 위함 (#478)
	internal static bool TryGetArrayLength(SlotData slot, out int length)
	{
		length = 0;
		if (slot == null || slot.Block != null || slot.VariableName != null || slot.Literal == null) return false;
		if (slot.Type?.Kind != VariableTypeKind.Int) return false;
		return int.TryParse(slot.Literal, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out length)
			&& length >= 1;
	}

	// 한 번의 TryConvert에서 쓰는 기록. 부르는 함수를 GUID로 찾고, 이미 바꾼 함수를 다시 바꾸지 않게 한다
	// static 저장소에 두지 않는 이유: 사용자 정의 함수는 캐릭터마다 다르므로, 서버 한 곳에서 여러 캐릭터의 함수를 바꿀 때 섞이지 않게 하기 위함
	class Conversion
	{
		// 캐릭터가 가진 함수들. 함수 블록이 GUID로 부를 함수를 찾는다
		readonly Dictionary<string, FunctionData> functionsByGuid = new Dictionary<string, FunctionData>();
		// 바꾸기 시작한 함수들. 블록을 바꾸는 중인 함수도 들어 있어서, 재귀 호출이 같은 객체를 가리킨다
		readonly Dictionary<string, UserFunction> convertedByGuid = new Dictionary<string, UserFunction>();

		public Conversion(FunctionData root, IEnumerable<FunctionData> ownedFunctions)
		{
			// 바꾸는 함수가 목록에 없어도 자기 자신을 부를 수 있게 먼저 넣는다
			if (root.Guid != null) functionsByGuid[root.Guid] = root;
			foreach (FunctionData owned in ownedFunctions)
			{
				// GUID가 겹치는 조작 데이터는 구조 검사가 막을 일이므로, 먼저 들어온 함수만 쓴다(CodeSizeCalculator와 같음)
				if (owned?.Guid != null) functionsByGuid.TryAdd(owned.Guid, owned);
			}
		}

		// 함수 하나를 바꾼다. 같은 GUID를 이미 바꾸기 시작했으면 그 객체를 돌려준다. 바꿀 수 없으면 콘솔 오류를 남기고 null
		public UserFunction ConvertFunction(FunctionData data)
		{
			if (data.Guid != null && convertedByGuid.TryGetValue(data.Guid, out UserFunction started)) return started;

			// 예전 데이터에 반환 타입이 빠져 있으면 Void로 본다. FunctionData.Create도 Void로 만든다
			VariableType returnType = data.ReturnType ?? new VariableType(VariableTypeKind.Void);
			if (returnType.Kind != VariableTypeKind.Void && !RuntimeValue.IsSupportedType(returnType))
				return Reject<UserFunction>(data, $"반환 타입 {returnType.Kind}은(는) 아직 실행할 수 없습니다.");
			// C에서도 array는 돌려줄 수 없다
			if (returnType.Kind == VariableTypeKind.Array) return Reject<UserFunction>(data, "array는 반환 타입으로 쓸 수 없습니다.");
			if (!TryCheckParameters(data)) return null;

			// 기획의 프레임 = 1(호출) + 파라미터 수 + 지역 변수 수. array 지역 변수는 1칸이 아니라 길이만큼 센다
			// array 파라미터는 1칸이다. C처럼 원소가 아니라 array를 가리키는 값 하나만 넘어오기 때문
			// long으로 더한다. 조작한 데이터의 아주 큰 길이로 int가 넘쳐 음수가 되면, 프레임이 1칸으로 잡혀 메모리 검사를 지나고 거대한 array를 만들기 때문
			// int 안에 들어오기만 하면 따로 상한을 두지 않는다. 메모리보다 큰 프레임은 시전 전 검사(NotEnoughMemory)나 Overflow가 막아, array를 만들기 전에 끝나기 때문
			long frameSize = 1L + data.Parameters.Count + data.LocalVariables().Count + ExtraArrayCells(data);
			if (frameSize > int.MaxValue) return Reject<UserFunction>(data, "array가 너무 커서 프레임 크기를 셀 수 없습니다.");
			// 블록보다 먼저 만들어 기록한다. 블록 안의 재귀 호출이 이 객체를 가리키게 하기 위함
			// 파라미터와 반환 타입은 복사해서 넘긴다. 편집 화면이 같은 함수 데이터를 고치면, 이미 바꾼 함수의 파라미터만 바뀌고 프레임 크기는 그대로라 서로 어긋나기 때문
			UserFunction function = new UserFunction(data.Name, (int)frameSize, CopyParameters(data.Parameters), CopyType(returnType), data.IsAsync);
			if (data.Guid != null) convertedByGuid[data.Guid] = function;

			if (!TryConvertStatements(data, data.Blocks, out Block[] blocks)) return null;
			function.SetBlocks(blocks);
			return function;
		}

		// 배열 만들기 블록마다 길이 - 1칸. LocalVariables가 array 하나를 이미 1칸으로 세기 때문
		// 길이가 잘못된 블록은 블록을 바꿀 때 거부되므로 여기서는 세지 않는다
		static long ExtraArrayCells(FunctionData data)
		{
			long cells = 0;
			foreach (BlockData block in data.AllBlocks())
			{
				if (block.Kind == BlockKind.ArrayDeclare && block.Slots.Count >= 3 && TryGetArrayLength(block.Slots[2], out int length))
					cells += length - 1;
			}
			return cells;
		}

		static ParameterData[] CopyParameters(IReadOnlyList<ParameterData> parameters)
		{
			ParameterData[] copied = new ParameterData[parameters.Count];
			for (int i = 0; i < copied.Length; i++)
				copied[i] = new ParameterData(parameters[i].Name, CopyType(parameters[i].Type));
			return copied;
		}

		// VariableType은 칸을 바꿀 수 있는 클래스라서, 원소 타입까지 새로 만든다
		static VariableType CopyType(VariableType type) => type == null ? null : new VariableType(type.Kind)
		{
			ElementType = CopyType(type.ElementType),
			StructName = type.StructName,
			TargetKind = type.TargetKind,
		};

		// 파라미터마다 이름이 있고, 이름이 겹치지 않고, 실행할 수 있는 타입인지 본다
		// 편집 화면이 막을 일이지만 저장된 데이터를 한 번 더 확인한다. 실행 중에는 파라미터 선언이 실패하지 않게 하기 위함
		bool TryCheckParameters(FunctionData data)
		{
			HashSet<string> names = new HashSet<string>();
			foreach (ParameterData parameter in data.Parameters)
			{
				string rejection = null;
				if (string.IsNullOrEmpty(parameter?.Name)) rejection = "파라미터 이름이 비어 있습니다.";
				else if (!names.Add(parameter.Name)) rejection = $"파라미터 {parameter.Name}이(가) 두 번 있습니다.";
				else if (!RuntimeValue.IsSupportedType(parameter.Type))
					rejection = $"파라미터 {parameter.Name}의 {parameter.Type?.Kind} 타입은 아직 실행할 수 없습니다.";

				if (rejection == null) continue;
				Reject(data, rejection);
				return false;
			}
			return true;
		}


		// 블록 목록 하나(function body, 또는 조건문·반복문의 { } 안)를 바꾼다. 연달아 놓인 if, else if, else는 조건문 블록 하나로 묶는다
		bool TryConvertStatements(FunctionData data, IReadOnlyList<BlockData> blockDatas, out Block[] blocks)
		{
			blocks = null;
			List<Block> converted = new List<Block>();
			for (int i = 0; i < blockDatas.Count; i++)
			{
				Block block = blockDatas[i].Kind == BlockKind.Conditional
					? ConvertConditional(data, blockDatas, ref i)
					: ConvertStatement(data, blockDatas[i]);
				if (block == null) return false;
				converted.Add(block);
			}

			blocks = converted.ToArray();
			return true;
		}

		// i번째부터 연달아 놓인 If, ElseIf들, Else를 조건문 블록 하나로 바꾼다. i는 묶은 마지막 블록으로 옮긴다
		// 칸: 갈래(드롭다운), 조건(Else는 없음). 블록 영역: { } 안
		Block ConvertConditional(FunctionData data, IReadOnlyList<BlockData> blockDatas, ref int i)
		{
			List<ConditionalBlock.Branch> branches = new List<ConditionalBlock.Branch>();
			for (int j = i; j < blockDatas.Count && blockDatas[j].Kind == BlockKind.Conditional; j++)
			{
				BlockData block = blockDatas[j];
				// 연산 블록과 같은 이유로 정의된 값인지 한 번 더 본다
				string option = block.Slots.Count >= 1 ? block.Slots[0].Option : null;
				if (!System.Enum.TryParse(option, out ConditionalBranch branch) || !System.Enum.IsDefined(typeof(ConditionalBranch), branch))
					return Reject(data, $"조건문 갈래 {option}을(를) 알 수 없습니다.");

				// If는 새 조건문을 시작한다. 앞의 조건문은 바로 앞 블록에서 끝난다
				if (j > i && branch == ConditionalBranch.If) break;
				// 편집 화면도 바로 위가 if, else if가 아니면 else if, else를 고르지 못하게 하지만, 저장된 데이터를 한 번 더 확인한다
				if (j == i && branch != ConditionalBranch.If) return Reject(data, $"{branch} 앞에 if가 없습니다.");

				ValueBlock condition = null;
				if (branch != ConditionalBranch.Else)
				{
					if (block.Slots.Count < 2) return Reject(data, $"{branch} 블록에 조건 칸이 없습니다.");
					condition = ConvertValue(data, block.Slots[1]);
					if (condition == null) return null;
				}

				if (!TryConvertStatements(data, InnerBlocksOf(block), out Block[] body)) return null;

				branches.Add(new ConditionalBlock.Branch(condition, body));
				i = j;
				// Else 뒤에는 갈래가 이어지지 않는다. 바로 뒤의 ElseIf, Else는 다음 블록으로 바꿀 때 앞에 if가 없는 것으로 거부된다
				if (branch == ConditionalBranch.Else) break;
			}

			return new ConditionalBlock(branches.ToArray());
		}

		// 블록 영역이 null이어도 빈 { }로 본다. JSON에서 되돌린 데이터에 블록 영역이 빠져 있을 수 있기 때문
		IReadOnlyList<BlockData> InnerBlocksOf(BlockData block) =>
			block.InnerBlocks ?? (IReadOnlyList<BlockData>)Array.Empty<BlockData>();

		// function body에 놓인 블록 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
		Block ConvertStatement(FunctionData data, BlockData block)
		{
			switch (block.Kind)
			{
				// 칸: 타입, 이름, 초기값
				case BlockKind.Declare when block.Slots.Count >= 3:
				{
					VariableType type = block.Slots[0].Type;
					string name = block.Slots[1].Name;
					if (!RuntimeValue.IsSupportedType(type)) return Reject(data, $"{type?.Kind} 타입의 변수는 아직 실행할 수 없습니다.");
					// array는 길이가 있어야 프레임 크기를 정할 수 있으므로 배열 만들기 블록으로만 만든다
					if (type.Kind == VariableTypeKind.Array) return Reject(data, "array는 선언 블록이 아니라 배열 만들기 블록으로 만듭니다.");
					if (string.IsNullOrEmpty(name)) return Reject(data, "선언 블록의 이름 칸이 비어 있습니다.");

					// initializer는 비워도 된다. 그때는 실행할 때 기본값을 넣는다
					SlotData initialSlot = block.Slots[2];
					if (IsEmpty(initialSlot)) return new DeclareBlock(name, type, null);

					ValueBlock initialValue = ConvertValue(data, initialSlot);
					return initialValue == null ? null : new DeclareBlock(name, type, initialValue);
				}

				// 칸: 원소 타입, 이름, 길이
				case BlockKind.ArrayDeclare when block.Slots.Count >= 3:
				{
					VariableType elementType = block.Slots[0].Type;
					string name = block.Slots[1].Name;
					if (!RuntimeValue.IsSupportedType(VariableType.ArrayOf(elementType)))
						return Reject(data, $"{elementType?.Kind} 원소의 array는 아직 실행할 수 없습니다.");
					if (string.IsNullOrEmpty(name)) return Reject(data, "배열 만들기 블록의 이름 칸이 비어 있습니다.");
					// 편집 화면도 길이 칸에 1 이상의 숫자만 직접 입력하게 막지만, 저장된 데이터를 한 번 더 확인한다
					if (!TryGetArrayLength(block.Slots[2], out int length))
						return Reject(data, $"array {name}의 길이는 직접 입력한 1 이상의 정수여야 합니다.");

					return new ArrayDeclareBlock(name, elementType, length);
				}

				// 칸: array 변수, index, 쓸 값
				case BlockKind.ArraySet when block.Slots.Count >= 3:
				{
					string arrayName = block.Slots[0].VariableName;
					if (string.IsNullOrEmpty(arrayName)) return Reject(data, "원소 쓰기 블록의 array 칸이 비어 있습니다.");

					ValueBlock index = ConvertValue(data, block.Slots[1]);
					if (index == null) return null;
					ValueBlock value = ConvertValue(data, block.Slots[2]);
					return value == null ? null : new ArraySetBlock(arrayName, index, value);
				}

				// 칸: 변수, 값
				case BlockKind.Assign when block.Slots.Count >= 2:
				{
					string variableName = block.Slots[0].VariableName;
					if (string.IsNullOrEmpty(variableName)) return Reject(data, "대입 블록의 변수 칸이 비어 있습니다.");

					ValueBlock value = ConvertValue(data, block.Slots[1]);
					return value == null ? null : new AssignBlock(variableName, value);
				}

				// 칸: 포인터 값
				case BlockKind.Free when block.Slots.Count >= 1:
				{
					ValueBlock pointer = ConvertValue(data, block.Slots[0]);
					return pointer == null ? null : new FreeBlock(pointer);
				}

				// 칸: i 이름, start 값, end 값. 블록 영역: { } 안
				case BlockKind.For when block.Slots.Count >= 3:
				{
					string indexName = block.Slots[0].Name;
					if (string.IsNullOrEmpty(indexName)) return Reject(data, "for 블록의 변수 이름 칸이 비어 있습니다.");

					ValueBlock start = ConvertValue(data, block.Slots[1]);
					if (start == null) return null;
					ValueBlock end = ConvertValue(data, block.Slots[2]);
					if (end == null) return null;
					return TryConvertStatements(data, InnerBlocksOf(block), out Block[] body) ? new ForBlock(indexName, start, end, body) : null;
				}

				// 칸: 조건. 블록 영역: { } 안
				case BlockKind.While when block.Slots.Count >= 1:
				{
					ValueBlock condition = ConvertValue(data, block.Slots[0]);
					if (condition == null) return null;
					return TryConvertStatements(data, InnerBlocksOf(block), out Block[] body) ? new WhileBlock(condition, body) : null;
				}

				// 반복문 밖에 놓여도 거부하지 않는다. 플레이어 코드의 잘못이므로 실행 중에 NotInLoop로 실패한다
				case BlockKind.Break:
					return new BreakBlock();
				case BlockKind.Continue:
					return new ContinueBlock();

				// 칸: 파라미터 수만큼의 값. 반환 값은 버리지만, Return 누락과 반환 타입 불일치는 expression에서처럼 실패한다
				case BlockKind.Function:
					return TryConvertCall(data, block, out FunctionBlock function, out ValueBlock[] arguments)
						? new CallBlock(function, arguments)
						: null;

				// 칸: 돌려줄 값(비워도 됨). 반환 타입에 맞는지와 return 누락은 실행 중에 함수가 끝날 때 본다
				case BlockKind.Return:
				{
					if (block.Slots.Count < 1 || IsEmpty(block.Slots[0])) return new ReturnBlock();

					ValueBlock value = ConvertValue(data, block.Slots[0]);
					return value == null ? null : new ReturnBlock(value);
				}
			}

			return Reject(data, $"{block.Kind} 블록은 아직 실행할 수 없습니다.");
		}

		// 함수 블록의 부를 함수와 argument를 바꾼다. statement로 부른 호출과 expression으로 부른 호출이 함께 쓴다. 바꿀 수 없으면 콘솔 오류를 남기고 false
		bool TryConvertCall(FunctionData data, BlockData block, out FunctionBlock function, out ValueBlock[] arguments)
		{
			function = null;
			arguments = null;
			if (block.IsUserFunction)
			{
				if (!TryFindUserFunction(data, block, out function)) return false;
			}
			else if (string.IsNullOrEmpty(block.FunctionId) || !PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction predefined))
			{
				Reject(data, $"고정 ID {block.FunctionId}의 함수를 찾을 수 없습니다.");
				return false;
			}
			else function = predefined;

			if (block.Slots.Count > function.Parameters.Count)
			{
				Reject(data, $"{function.Name}의 argument가 파라미터 수({function.Parameters.Count})보다 많습니다.");
				return false;
			}
			// 비동기 함수는 부른 함수보다 늦게 끝날 수 있다. 부른 함수가 끝나 프레임과 함께 array의 칸이 돌아가도, 비동기 작업이 메모리 없이 그 array를 계속 쓰게 되므로 막는다
			// C에서는 끝난 함수의 array를 가리키는 포인터(dangling pointer)라서 정의되지 않은 동작이다
			if (function.IsAsync)
			{
				foreach (ParameterData parameter in function.Parameters)
				{
					if (parameter.Type?.Kind != VariableTypeKind.Array) continue;
					Reject(data, $"{function.Name}은(는) 비동기 함수라 array 파라미터 {parameter.Name}에 값을 넘길 수 없습니다.");
					return false;
				}
			}
			// 사용자 정의 함수는 argument를 빠뜨릴 수 없다. C와 같다. 비운 칸을 「값 없음」으로 받는 것은 사전 정의 함수(스킬)만이다
			if (block.IsUserFunction && block.Slots.Count < function.Parameters.Count)
			{
				Reject(data, $"{function.Name}의 argument가 파라미터 수({function.Parameters.Count})보다 적습니다.");
				return false;
			}

			// 비운 value slot은 null로 넘긴다. 스킬은 값이 없을 때 슬롯에서 바로 시전했을 때처럼 동작한다
			arguments = new ValueBlock[block.Slots.Count];
			for (int i = 0; i < arguments.Length; i++)
			{
				if (IsEmpty(block.Slots[i]))
				{
					if (!block.IsUserFunction) continue;
					Reject(data, $"{function.Name}의 {i + 1}번째 argument가 비어 있습니다.");
					return false;
				}
				arguments[i] = ConvertValue(data, block.Slots[i]);
				if (arguments[i] == null) return false;
			}
			return true;
		}

		// 함수 블록이 GUID로 부르는 사용자 정의 함수를 찾아 바꾼다. 부른 함수를 바꿀 수 없으면 그 함수 이름으로 콘솔 오류가 남는다
		bool TryFindUserFunction(FunctionData data, BlockData block, out FunctionBlock function)
		{
			function = null;
			if (string.IsNullOrEmpty(block.FunctionId) || !functionsByGuid.TryGetValue(block.FunctionId, out FunctionData callee))
			{
				Reject(data, $"GUID {block.FunctionId}의 함수를 찾을 수 없습니다. 지운 함수일 수 있습니다.");
				return false;
			}

			function = ConvertFunction(callee);
			return function != null;
		}

		// value slot 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
		ValueBlock ConvertValue(FunctionData data, SlotData slot)
		{
			if (slot.Block != null) return ConvertValueBlock(data, slot.Block);
			if (slot.VariableName != null) return new VariableValue(slot.VariableName);
			// 글자열이 그 타입의 값으로 바뀌는지는 실행할 때 확인한다(InvalidLiteral). 플레이어 코드의 잘못이기 때문
			if (slot.Literal != null && slot.Type != null) return new LiteralValue(slot.Type, slot.Literal);

			return Reject<ValueBlock>(data, "value slot이 비어 있습니다.");
		}

		// expression으로 쓴 블록 하나를 바꾼다. 바꿀 수 없으면 콘솔 오류를 남기고 null
		ValueBlock ConvertValueBlock(FunctionData data, BlockData block)
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

				// 칸: 왼쪽 값, 연산자, 오른쪽 값
				case BlockKind.Compare when block.Slots.Count >= 3:
				{
					// 연산 블록과 같은 이유로 정의된 값인지 한 번 더 본다
					if (!System.Enum.TryParse(block.Slots[1].Option, out CompareOperator op) || !System.Enum.IsDefined(typeof(CompareOperator), op))
						return Reject<ValueBlock>(data, $"비교 연산자 {block.Slots[1].Option}을(를) 알 수 없습니다.");

					ValueBlock left = ConvertValue(data, block.Slots[0]);
					if (left == null) return null;
					ValueBlock right = ConvertValue(data, block.Slots[2]);
					return right == null ? null : new CompareBlock(left, op, right);
				}

				// 칸: 왼쪽 값, 연산자, 오른쪽 값
				case BlockKind.Logic when block.Slots.Count >= 3:
				{
					// 연산 블록과 같은 이유로 정의된 값인지 한 번 더 본다
					if (!System.Enum.TryParse(block.Slots[1].Option, out LogicOperator op) || !System.Enum.IsDefined(typeof(LogicOperator), op))
						return Reject<ValueBlock>(data, $"논리 연산자 {block.Slots[1].Option}을(를) 알 수 없습니다.");

					ValueBlock left = ConvertValue(data, block.Slots[0]);
					if (left == null) return null;
					ValueBlock right = ConvertValue(data, block.Slots[2]);
					return right == null ? null : new LogicBlock(left, op, right);
				}

				// 칸: 값
				case BlockKind.Not when block.Slots.Count >= 1:
				{
					ValueBlock value = ConvertValue(data, block.Slots[0]);
					return value == null ? null : new NotBlock(value);
				}

				// 칸: struct 타입 (드롭다운)
				case BlockKind.Malloc when block.Slots.Count >= 1:
				{
					VariableType structType = block.Slots[0].Type;
					if (structType?.Kind != VariableTypeKind.Struct || !PredefinedStructs.TryGet(structType.StructName, out StructDefinition definition))
						return Reject<ValueBlock>(data, $"malloc할 struct {structType?.StructName}이(가) 미리 정한 struct 목록에 없습니다.");

					return new MallocBlock(definition);
				}

				// 칸: array 변수, index
				case BlockKind.ArrayGet when block.Slots.Count >= 2:
				{
					string arrayName = block.Slots[0].VariableName;
					if (string.IsNullOrEmpty(arrayName)) return Reject<ValueBlock>(data, "원소 읽기 블록의 array 칸이 비어 있습니다.");

					ValueBlock index = ConvertValue(data, block.Slots[1]);
					return index == null ? null : new ArrayGetBlock(arrayName, index);
				}

				// 값을 돌려주는 함수(사전 정의 함수, 사용자 정의 함수). 칸: 파라미터 수만큼의 값
				case BlockKind.Function:
				{
					if (!TryConvertCall(data, block, out FunctionBlock function, out ValueBlock[] arguments)) return null;

					VariableType returnType = function.ReturnType;
					if (returnType.Kind == VariableTypeKind.Void)
						return Reject<ValueBlock>(data, $"{function.Name}은(는) 값을 돌려주지 않으므로 expression으로 쓸 수 없습니다.");
					if (!RuntimeValue.IsSupportedType(returnType) || returnType.Kind == VariableTypeKind.Array)
						return Reject<ValueBlock>(data, $"{function.Name}의 반환 타입 {returnType.Kind}은(는) 아직 실행할 수 없습니다.");
					// 비동기 함수는 끝나기를 기다리지 않으므로 반환 값을 받을 수 없다
					if (function.IsAsync)
						return Reject<ValueBlock>(data, $"{function.Name}은(는) 비동기 함수라 expression으로 쓸 수 없습니다.");

					return new CallBlock(function, arguments);
				}
			}

			return Reject<ValueBlock>(data, $"expression으로 쓴 {block.Kind} 블록은 아직 실행할 수 없습니다.");
		}
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
