using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// 값 칸마다 무엇을 받을 수 있는지 정한 표 (#474). 블록을 끌어다 놓을 때, 키보드로 값을 적을 때, 저장 전 컴파일할 때 함께 쓴다.
/// <para>
/// - 칸 규칙(Rule)은 블록 종류와 칸 번호로 정한다. 예: if의 조건 칸은 참·거짓, for의 시작·끝은 정수, thrust의 target은 대상.<br/>
/// - 변수와 원소 읽기(arr[i])는 그 자리에서 보이는 변수의 타입으로 검사한다(VariableScopes). 타입을 모르면 막지 않는다 (직접 적은 값만 받는 칸은 제외).<br/>
/// - 숫자(int, float, char)끼리는 C처럼 서로 받는다. 예: int x = 0.5; (실행기가 0으로 바꾼다)
/// </para>
/// <para>
/// 실행기의 실행 규칙(function-executor.md)에서 TypeMismatch가 날 값을 편집할 때 미리 막기 위한 표다. 실행 규칙 자체는 바꾸지 않는다.
/// </para>
/// </summary>
public static class SlotRules
{
	/// <summary>칸이 받는 것의 종류</summary>
	public enum Expect
	{
		Any,         // 아무 값 (대입의 값처럼 편집 화면이 타입을 모르는 칸)
		Condition,   // 참·거짓: if·while의 조건, &&·||·!의 양쪽
		Number,      // 숫자: 연산의 양쪽, wait의 초
		Integer,     // 정수: for의 시작·끝, 원소 칸 번호
		ArrayLength, // 직접 적은 1 이상의 정수: 배열 만들기의 길이 (실행기가 직접 적은 값만 받음)
		Comparable,  // 비교할 수 있는 값: 비교의 양쪽 (C는 string 비교를 받지 않음)
		Pointer,     // 포인터: free
		Typed,       // 정해진 타입(Type): 선언의 처음 값, 함수의 파라미터, return
	}

	/// <summary>칸 하나의 규칙</summary>
	public struct Rule
	{
		public Expect Expect;

		/// <summary>Typed일 때 받는 타입. null이면 타입을 몰라 아무 값이나 받는다. Void면 아무것도 받지 않는다 (void 함수의 return)</summary>
		public VariableType Type;

		/// <summary>직접 적은 값만 받는다 (전역 변수의 처음 값: C에서 전역 변수의 처음 값은 상수여야 하기 때문)</summary>
		public bool LiteralOnly;
	}

	/// <summary>
	/// owner 블록의 index번 칸의 규칙.
	/// returnType은 편집 중인 함수의 반환 타입(return 칸에 씀), globals는 전역 변수 파일을 고치는 중인지,
	/// findUserFunction은 GUID로 사용자 정의 함수를 찾는 함수(함수 블록의 파라미터 타입에 씀. null이면 사용자 정의 함수의 파라미터는 아무 값이나 받음),
	/// variableType은 그 자리에서 보이는 변수의 타입을 이름으로 찾는 함수(대입·원소 쓰기의 값에 씀. null이거나 못 찾으면 아무 값이나 받음, #474 5단계).
	/// </summary>
	public static Rule RuleOf(BlockData owner, int index, VariableType returnType, bool globals, Func<string, FunctionData> findUserFunction,
		Func<string, VariableType> variableType = null)
	{
		switch (owner.Kind)
		{
			case BlockKind.Declare when index == 2:
				return new Rule { Expect = Expect.Typed, Type = owner.Slots[0]?.Type, LiteralOnly = globals };
			case BlockKind.Arithmetic:
			case BlockKind.Wait:
				return new Rule { Expect = Expect.Number };
			case BlockKind.Compare:
				return new Rule { Expect = Expect.Comparable };
			case BlockKind.Logic:
			case BlockKind.Not:
			case BlockKind.Conditional:
			case BlockKind.While:
				return new Rule { Expect = Expect.Condition };
			case BlockKind.For:
				return new Rule { Expect = Expect.Integer };
			case BlockKind.ArrayDeclare when index == 2:
				return new Rule { Expect = Expect.ArrayLength, LiteralOnly = true };
			case BlockKind.ArrayGet when index == 1:
			case BlockKind.ArraySet when index == 1:
				return new Rule { Expect = Expect.Integer };
			// 대입의 값은 왼쪽 변수의 타입, 원소 쓰기의 값은 array의 원소 타입 (#474 5단계). 변수를 모르면 아무 값이나
			case BlockKind.Assign when index == 1:
				return new Rule { Expect = Expect.Typed, Type = VariableTypeOf(owner, 0, variableType, false) };
			case BlockKind.ArraySet when index == 2:
				return new Rule { Expect = Expect.Typed, Type = VariableTypeOf(owner, 0, variableType, true) };
			case BlockKind.Free:
				return new Rule { Expect = Expect.Pointer };
			case BlockKind.Function:
				return new Rule { Expect = Expect.Typed, Type = ParameterTypeOf(owner, index, findUserFunction) };
			case BlockKind.Return:
				return new Rule { Expect = Expect.Typed, Type = returnType };
			default:
				return new Rule { Expect = Expect.Any };
		}
	}

	/// <summary>
	/// 칸 데이터(적은 값, 변수, 끼운 블록)가 규칙에 맞으면 true. 빈 칸은 true (빈 칸은 컴파일의 「비어 있는 값 칸」 검사가 따로 본다).
	/// </summary>
	public static bool Fits(Rule rule, SlotData content, Func<string, FunctionData> findUserFunction, Func<string, VariableType> variableType = null)
	{
		if (content == null) return true;
		if (content.Block != null) return FitsBlock(rule, content.Block, findUserFunction, variableType);
		if (!string.IsNullOrEmpty(content.VariableName)) return FitsVariable(rule, variableType?.Invoke(content.VariableName));
		if (string.IsNullOrEmpty(content.Literal)) return true;

		if (rule.Expect == Expect.ArrayLength)
			return content.Type?.Kind == VariableTypeKind.Int
				&& int.TryParse(content.Literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int length) && length >= 1;

		bool isNull = content.Literal == SlotData.NullLiteral && content.Type?.Kind == VariableTypeKind.Pointer;
		return FitsKind(rule, isNull ? Kind.Null : KindOf(content.Type), content.Type);
	}

	/// <summary>
	/// 값 블록(연산, 비교, malloc, 값을 돌려주는 함수 등)을 이 칸에 끼울 수 있으면 true. 직접 적은 값만 받는 칸은 false.
	/// </summary>
	public static bool FitsBlock(Rule rule, BlockData block, Func<string, FunctionData> findUserFunction, Func<string, VariableType> variableType = null)
	{
		if (rule.LiteralOnly) return false;
		switch (block.Kind)
		{
			case BlockKind.Arithmetic: return FitsKind(rule, Kind.NumberExpression, null);
			case BlockKind.Compare:
			case BlockKind.Logic:
			case BlockKind.Not: return FitsKind(rule, Kind.Bool, null);
			// 원소 읽기는 array 변수의 원소 타입. 변수를 모르면 막지 않는다
			case BlockKind.ArrayGet:
				VariableType element = VariableTypeOf(block, 0, variableType, true);
				return element == null ? !IsVoid(rule) : FitsKind(rule, KindOf(element), element);
			case BlockKind.Malloc:
				VariableType pointer = VariableType.PointerTo(block.Slots.Count > 0 ? block.Slots[0]?.Type : null);
				return FitsKind(rule, Kind.Pointer, pointer);
			case BlockKind.Function:
				VariableType returned = ReturnTypeOf(block, findUserFunction);
				// 반환 타입을 모르는 함수(지워진 함수 등)는 막지 않는다. 없는 함수는 실행기가 따로 거부한다
				if (returned == null) return !IsVoid(rule);
				return FitsKind(rule, KindOf(returned), returned);
			default:
				return false;
		}
	}

	/// <summary>
	/// 타입이 type인 변수를 이 칸에 넣을 수 있으면 true. type이 null(모르는 변수)이면 막지 않는다. 직접 적은 값만 받는 칸은 false.
	/// 값 칸의 「변수 고르기」 목록을 거를 때도 쓴다 (#474 5단계).
	/// </summary>
	public static bool FitsVariable(Rule rule, VariableType type)
	{
		if (rule.LiteralOnly || IsVoid(rule)) return false;
		return type == null || FitsKind(rule, KindOf(type), type);
	}

	// block의 index번 변수 칸에 적힌 변수의 타입. element면 array의 원소 타입. 모르면 null
	static VariableType VariableTypeOf(BlockData block, int index, Func<string, VariableType> variableType, bool element)
	{
		if (variableType == null || index >= block.Slots.Count) return null;
		VariableType type = variableType(block.Slots[index]?.VariableName);
		if (!element) return type;
		return type?.Kind == VariableTypeKind.Array ? type.ElementType : null;
	}

	/// <summary>
	/// 플레이어에게 보일 「이 칸에 넣을 수 있는 것」. 예: 「참·거짓(true, false, 비교, 논리)」
	/// </summary>
	public static string Describe(Rule rule)
	{
		switch (rule.Expect)
		{
			case Expect.Condition: return "참·거짓(true, false, 비교, 논리)";
			case Expect.Number: return "숫자";
			case Expect.Integer: return "정수";
			case Expect.ArrayLength: return "직접 적은 1 이상의 정수";
			case Expect.Comparable: return "숫자, 문자, 참·거짓, 포인터 (string은 비교할 수 없음)";
			case Expect.Pointer: return "포인터(NULL, malloc, summon_sword() 등)";
			case Expect.Typed:
				if (rule.Type == null) return "값";
				if (rule.Type.Kind == VariableTypeKind.Void) return "아무것도 (void 함수의 return은 비워 둠)";
				string type = rule.Type.Kind switch
				{
					VariableTypeKind.Int or VariableTypeKind.Float or VariableTypeKind.Char => $"{BlockLabels.TypeName(rule.Type)}(숫자)",
					VariableTypeKind.Bool => "bool(true, false, 비교, 논리)",
					VariableTypeKind.Pointer => $"{BlockLabels.TypeName(rule.Type)}(NULL, malloc, summon_sword() 등)",
					VariableTypeKind.Target => "대상(get_bug(), Target 변수. NULL은 안 됨)",
					_ => BlockLabels.TypeName(rule.Type),
				};
				return rule.LiteralOnly ? $"직접 적은 {type}" : type;
			default:
				return "값";
		}
	}

	// 값의 종류. NumberExpression은 연산 블록(int인지 float인지 실행해야 앎)
	enum Kind { Other, Int, Float, Char, Bool, String, Null, Pointer, Target, Array, NumberExpression }

	static Kind KindOf(VariableType type) => type?.Kind switch
	{
		VariableTypeKind.Int => Kind.Int,
		VariableTypeKind.Float => Kind.Float,
		VariableTypeKind.Char => Kind.Char,
		VariableTypeKind.Bool => Kind.Bool,
		VariableTypeKind.String => Kind.String,
		VariableTypeKind.Pointer => Kind.Pointer,
		VariableTypeKind.Target => Kind.Target,
		VariableTypeKind.Array => Kind.Array,
		_ => Kind.Other,
	};

	static bool IsNumeric(Kind kind) => kind is Kind.Int or Kind.Float or Kind.Char or Kind.NumberExpression;

	static bool IsVoid(Rule rule) => rule.Expect == Expect.Typed && rule.Type?.Kind == VariableTypeKind.Void;

	// 값의 종류(kind, 타입을 알면 type)가 규칙에 맞는지
	static bool FitsKind(Rule rule, Kind kind, VariableType type)
	{
		switch (rule.Expect)
		{
			case Expect.Any: return true;
			case Expect.Condition: return kind == Kind.Bool;
			case Expect.Number: return IsNumeric(kind);
			// 연산 블록은 정수일 수도 있으므로 받는다 (i + 1). float이 되면 실행기가 TypeMismatch로 알린다
			case Expect.Integer: return kind is Kind.Int or Kind.Char or Kind.NumberExpression;
			case Expect.ArrayLength: return false;
			case Expect.Comparable: return kind != Kind.String && kind != Kind.Array && kind != Kind.Other;
			case Expect.Pointer: return kind is Kind.Null or Kind.Pointer;
			case Expect.Typed: return FitsType(rule.Type, kind, type);
			default: return true;
		}
	}

	// 정해진 타입 target에 값을 넣을 수 있는지. 숫자끼리는 C처럼 받는다
	static bool FitsType(VariableType target, Kind kind, VariableType type)
	{
		if (target == null) return true;
		switch (target.Kind)
		{
			case VariableTypeKind.Void: return false;
			case VariableTypeKind.Int:
			case VariableTypeKind.Float:
			case VariableTypeKind.Char: return IsNumeric(kind);
			case VariableTypeKind.Bool: return kind == Kind.Bool;
			case VariableTypeKind.String: return kind == Kind.String;
			case VariableTypeKind.Pointer:
				if (kind == Kind.Null) return true;
				if (kind != Kind.Pointer) return false;
				// 가리키는 struct가 다르면 다른 포인터 (Sword*에 다른 struct의 포인터를 넣지 않음). 한쪽을 모르면 막지 않는다
				string want = target.ElementType?.StructName;
				string have = type?.ElementType?.StructName;
				return want == null || have == null || want == have;
			case VariableTypeKind.Target: return kind == Kind.Target;
			case VariableTypeKind.Array: return kind == Kind.Array;
			default: return true;
		}
	}

	// 함수 블록의 index번 파라미터 타입. 모르면 null
	static VariableType ParameterTypeOf(BlockData block, int index, Func<string, FunctionData> findUserFunction)
	{
		if (string.IsNullOrEmpty(block.FunctionId)) return null;
		if (block.IsUserFunction)
		{
			List<ParameterData> parameters = findUserFunction?.Invoke(block.FunctionId)?.Parameters;
			return parameters != null && index < parameters.Count ? parameters[index]?.Type : null;
		}
		if (!PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction function) || function.Parameters == null) return null;
		return index < function.Parameters.Count ? function.Parameters[index]?.Type : null;
	}

	// 함수 블록이 돌려주는 타입. 모르면 null
	static VariableType ReturnTypeOf(BlockData block, Func<string, FunctionData> findUserFunction)
	{
		if (string.IsNullOrEmpty(block.FunctionId)) return null;
		if (block.IsUserFunction) return findUserFunction?.Invoke(block.FunctionId)?.ReturnType;
		return PredefinedFunctionRegistry.TryGet(block.FunctionId, out PredefinedFunction function) ? function.ReturnType : null;
	}
}
