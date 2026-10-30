using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함수 실행이 실패한 이유. 언어 규칙 표가 이 종류마다 처리를 정한다.
/// </summary>
// 언어 규칙 파일은 이 값을 숫자로 저장한다. 중간에 끼워 넣거나 순서를 바꾸면 이미 만든 파일의 값이 다른 종류로 바뀌므로, 새 종류는 맨 끝에만 추가한다
public enum FunctionFailure
{
	// 실행 중에 스택이 모자라 다음 함수를 시작하지 못했다
	Overflow,
	// 시전하는 순간 남은 메모리가 그 함수의 프레임 크기보다 적었다
	NotEnoughMemory,
	// 스킬 구현자가 PredefinedFunction.Fail로 에러를 냈다. 어떤 에러인지는 FunctionTask.Error로 확인한다
	// 에러 종류마다 값을 두지 않는다. 스킬 구현자가 에러 클래스를 새로 만들어도 이 목록과 언어 규칙 표는 고치지 않게 하기 위함
	SkillFailed,
	// 시전한 함수가 아직 실행 중이거나 쿨타임 중이었다
	OnCooldown,
	// 값 칸의 변수가 지금 함수의 지역 변수에 없었다
	UndeclaredVariable,
	// 값 칸에 직접 입력한 글자열을 그 타입의 값으로 바꿀 수 없었다 (예: int 칸의 "abc")
	InvalidLiteral,
	// 같은 이름의 변수를 두 번 선언했다
	DuplicateVariable,
	// 변수의 타입과 다른 타입의 값을 넣었거나, 그 타입에 쓸 수 없는 연산을 했다 (예: int x = 0.5를 허용하지 않는 언어, float의 %)
	TypeMismatch,
	// 0으로 나누거나 0으로 나머지를 구했다 (int, float 모두)
	DivideByZero,
	// 반환 타입이 있는 사전 정의 함수가 Return을 부르지 않고 끝났다. 플레이어가 아니라 스킬 작성자의 실수다
	MissingReturnValue,
}

/// <summary>
/// 숫자 타입(int, float, char, bool)이 섞일 때 값을 바꾸는 규칙. 언어마다 다르므로 언어 규칙 파일이 고른다.
/// </summary>
// FunctionFailure와 같은 이유로, 새 규칙은 맨 끝에만 추가한다
public enum NumericConversion
{
	// 실제 C처럼 모두 자동으로 바꾼다. 연산에서 char, bool은 int로 올리고, int와 float이 섞이면 float으로 계산한다.
	// 대입할 때 float을 int에 넣으면 소수점을 버린다 (int x = 0.5 → 0)
	ImplicitAll,
	// 같은 타입끼리만 계산하고 대입한다 (float f = 3도 TypeMismatch)
	Strict,
	// 값이 잘리지 않는 쪽으로만 바꾼다 (Java, C#). int → float, char → int, char → float만 된다
	WideningOnly,
}

/// <summary>
/// 실패가 플레이어 코드의 어떤 잘못인지. 화면이 compile error와 runtime error를 나누어 보여 줄 때 쓴다.
/// </summary>
// 같은 실패라도 언어마다 분류가 다르므로(예: 선언하지 않은 변수는 C에서 compile error, Python에서 runtime error) 언어 규칙 표가 정한다
// FunctionFailure와 같은 이유로, 새 분류는 맨 끝에만 추가한다
public enum FailureCategory
{
	// 플레이어 코드의 잘못이 아니다 (쿨타임, 시전 전 메모리 부족 등)
	NotCodeError,
	// 그 언어에서 실행하기 전에 찾아내는 잘못
	CompileError,
	// 그 언어에서 실행 중에 찾아내는 잘못
	RuntimeError,
}

/// <summary>
/// 실패했을 때 실행기가 하는 처리.
/// </summary>
// FunctionFailure와 같은 이유로, 새 처리는 맨 끝에만 추가한다
public enum FailureHandling
{
	// 실패한 작업만 끝낸다. 다른 작업은 계속 진행한다
	EndTask,
	// 시전하지 않는다. 이유만 알리고 페널티는 없다
	Reject,
}

/// <summary>
/// 캐릭터 언어 하나의 규칙 파일. 실패 종류마다 실행기가 무엇을 할지와 그 실패의 분류(실패 처리 표), 초기값을 비운 변수의 기본값, 숫자 타입 변환 규칙을 정한다.
/// <para>
/// 사용법<br/>
/// - Project 창에서 Create → Invoke → Language Rule로 만들고, 모든 실패 종류마다 처리를 한 줄씩 넣는다.<br/>
/// - 캐릭터의 FunctionExecutor에 연결한다.<br/>
/// - 표에 빠진 실패 종류가 있으면 Play를 시작할 때 FunctionExecutor가 콘솔 오류로 알린다.
/// </para>
/// <para>
/// 언어마다 다른 처리(C의 재부팅, Java의 try 등)는 처리 종류를 추가하고 이 표의 내용만 다르게 채운다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "LanguageRule", menuName = "Invoke/Language Rule")]
public class LanguageRule : ScriptableObject
{
	[Serializable]
	struct Rule
	{
		public FunctionFailure failure;
		public FailureHandling handling;
		public FailureCategory category;
	}

	[SerializeField, Tooltip("실패 종류마다 할 처리. 모든 실패 종류가 한 줄씩 있어야 한다")]
	List<Rule> rules = new List<Rule>();

	// 초기값 칸을 비운 선언 블록이 변수에 넣는 값. 언어마다 기본값이 다르므로(예: Java의 String은 null) 언어 규칙 파일이 정한다
	// 처음 값은 C의 전역 변수 기본값이다. 이미 만든 C 파일도 Unity가 새 칸을 읽을 때 이 값을 넣는다
	[Header("기본값 (초기값을 비운 변수)")]
	[SerializeField] int defaultInt = 0;
	[SerializeField] float defaultFloat = 0f;
	[SerializeField] bool defaultBool = false;
	// 글자가 아니라 글자 번호로 받는다. Inspector의 글자 칸에는 글자 번호 0('\0')을 넣을 수 없기 때문
	[SerializeField, Tooltip("char 기본값의 글자 번호. 0은 '\\0'")] int defaultCharCode = 0;
	[SerializeField] string defaultString = "";

	// 처음 값은 C의 규칙이다. 이미 만든 C 파일도 Unity가 새 칸을 읽을 때 이 값을 넣는다
	[Header("숫자 타입 변환")]
	[SerializeField, Tooltip("int, float, char, bool이 섞일 때 값을 바꾸는 규칙")]
	NumericConversion numericConversion = NumericConversion.ImplicitAll;
	[SerializeField, Tooltip("float에 %를 쓸 수 있는지. C는 쓸 수 없다(TypeMismatch)")]
	bool allowFloatModulo = false;

	/// <summary>
	/// 숫자 타입이 섞일 때 값을 바꾸는 규칙. 연산 블록, 선언 블록, 대입 블록이 따른다.
	/// </summary>
	public NumericConversion NumericConversion => numericConversion;

	/// <summary>
	/// float에 %를 쓸 수 있으면 true. false이면 TypeMismatch로 실패한다.
	/// </summary>
	public bool AllowFloatModulo => allowFloatModulo;

	// 테스트 코드가 C가 아닌 숫자 타입 변환 규칙을 확인할 때 쓴다. 게임 코드는 Project 창에서 만든 언어 규칙 파일을 쓴다
	internal static LanguageRule CreateForTest(NumericConversion conversion, bool allowFloatModulo)
	{
		LanguageRule rule = CreateInstance<LanguageRule>();
		rule.numericConversion = conversion;
		rule.allowFloatModulo = allowFloatModulo;
		return rule;
	}

	/// <summary>
	/// failure의 처리를 찾는다. 표에 없으면 false를 돌려준다.
	/// </summary>
	public bool TryGetHandling(FunctionFailure failure, out FailureHandling handling)
	{
		bool found = TryGetRule(failure, out Rule rule);
		handling = rule.handling;
		return found;
	}

	/// <summary>
	/// failure의 분류(compile error, runtime error 등)를 찾는다. 표에 없으면 false를 돌려준다.
	/// </summary>
	public bool TryGetCategory(FunctionFailure failure, out FailureCategory category)
	{
		bool found = TryGetRule(failure, out Rule rule);
		category = rule.category;
		return found;
	}

	bool TryGetRule(FunctionFailure failure, out Rule found)
	{
		foreach (Rule rule in rules)
		{
			if (rule.failure != failure) continue;

			found = rule;
			return true;
		}

		found = default;
		return false;
	}

	/// <summary>
	/// 표에 처리가 없는 실패 종류들을 돌려준다.
	/// </summary>
	public IEnumerable<FunctionFailure> FindMissingFailures()
	{
		foreach (FunctionFailure failure in Enum.GetValues(typeof(FunctionFailure)))
			if (!TryGetHandling(failure, out _)) yield return failure;
	}

	/// <summary>
	/// type 타입 변수의 기본값을 돌려준다. 선언 블록의 초기값 칸이 비어 있을 때 쓴다.<br/>
	/// 포인터는 언어와 상관없이 언제나 HeapStorage.NullPointer다. 아직 다루지 않는 타입이면 콘솔 오류를 내고 false를 돌려준다.
	/// </summary>
	public bool TryGetDefaultValue(VariableType type, out RuntimeValue value)
	{
		value = default;
		if (type == null) throw new ArgumentNullException(nameof(type));

		switch (type.Kind)
		{
			case VariableTypeKind.Int: value = RuntimeValue.FromInt(defaultInt); return true;
			case VariableTypeKind.Float: value = RuntimeValue.FromFloat(defaultFloat); return true;
			case VariableTypeKind.Bool: value = RuntimeValue.FromBool(defaultBool); return true;
			case VariableTypeKind.Char: value = RuntimeValue.FromChar((char)defaultCharCode); return true;
			// Unity는 비운 글자 칸을 null로 둘 수 있으므로 빈 글자열로 바꾼다
			case VariableTypeKind.String: value = RuntimeValue.FromString(defaultString ?? ""); return true;
			// 표에서 정하지 않는다. HeapStorage가 0을 null 포인터로 쓰므로 다른 값이면 malloc 실패를 알아볼 수 없기 때문
			case VariableTypeKind.Pointer: value = RuntimeValue.FromPointer(type, HeapStorage.NullPointer); return true;
		}

		Debug.LogError($"{name}: {type.Kind} 타입의 기본값은 아직 없습니다. 이 타입은 이후 이슈에서 추가합니다.", this);
		return false;
	}
}
