/// <summary>
/// 시전 거부와 작업이 끝난 이유를 플레이어가 읽을 알림 문장으로 바꾼다. 형식은 두 줄로 「[분류] 이름」, 「설명」이다 (#480).
/// <para>
/// 사용법<br/>
/// - 화면(HUD)이 FunctionExecutorNetwork.CastRejected를 받으면 ForRejection, OwnTaskEnded를 받으면 ForTaskEnd를 부른다.<br/>
/// - null이 오면 알림을 띄우지 않는다.
/// </para>
/// <para>
/// 알리는 것: 시전 거부의 NotEnoughMemory, CompileError / 몬스터 공격으로 끊긴 실행 중단(Interrupted, InterruptCause.MonsterAttack) / runtime error로 분류된 실패.<br/>
/// 알리지 않는 것: 그 밖의 시전 거부, 사망·장소 이동·재시작으로 끊긴 작업(InterruptCause.System), 정상 종료, 취소(Cancelled), 개발자 책임(InternalError), 실행 중에 난 compile error.
/// </para>
/// </summary>
// compile error는 시전하기 전에 서버가 슬롯을 컴파일해 CompileError로 거부하고 알린다 (#506). 실행 중에는 runtime error만 나야 하므로(이우림 결정 11/1, 11/9)
// 실행 중에 compile error로 분류된 실패가 나면 컴파일 검사가 놓친 것이라 플레이어에게 알리지 않는다
// 스킬 에러의 설명은 이 표에 두지 않고 SkillError.Message를 그대로 쓴다. 스킬 구현자가 새 에러를 만들어도 이 표를 고치지 않게 하기 위함
public static class FailureNotice
{
	const string RejectedLabel = "[Cast Rejected]";
	const string RuntimeLabel = "[Runtime Error]";
	// 실행 중단은 플레이어 코드의 에러가 아니므로 [Runtime Error]와 다른 분류로 보인다 (#504)
	const string InterruptedLabel = "[Interrupted]";

	/// <summary>
	/// 시전 거부 rejection의 알림 문장. NotEnoughMemory, CompileError만 알리고, 그 밖의 거부면 null.
	/// </summary>
	// 다른 거부를 알리지 않는 이유: 쿨타임은 슬롯 덮개, 공격할 수 없는 장소는 무채색 슬롯처럼 화면이 이미 보여 준다. 메모리 부족과 컴파일 오류는 슬롯에서 원인을 알 수 없다
	public static string ForRejection(CastRejection rejection) => ForRejection(rejection, false);

	/// <summary>
	/// ForRejection과 같고, python이 true이면 컴파일 오류 거부를 Python 이름(SyntaxError)으로 보인다 (#532).
	/// </summary>
	public static string ForRejection(CastRejection rejection, bool python)
	{
		if (rejection == CastRejection.NotEnoughMemory) return $"{RejectedLabel} NotEnoughMemory\n남은 메모리가 함수 크기보다 적음";
		if (rejection == CastRejection.CompileError)
			return python ? $"{RejectedLabel} SyntaxError\n문법 오류가 있는 함수" : $"{RejectedLabel} CompileError\n컴파일 오류가 있는 함수";
		return null;
	}

	/// <summary>
	/// 끝난 작업 info의 알림 문장. 정상 종료, 취소, InternalError, compile error면 null.
	/// </summary>
	public static string ForTaskEnd(TaskEndInfo info)
	{
		switch (info.EndReason)
		{
			case FunctionTaskEndReason.Interrupted:
				// 사망·장소 이동·재시작은 플레이어가 화면으로 이미 알고 있으므로 알리지 않는다
				if (info.InterruptCause != InterruptCause.MonsterAttack) return null;
				return $"{InterruptedLabel} 실행 중단 공격\n함수가 모두 멈춤";
			case FunctionTaskEndReason.Failed:
				if (info.Category != FailureCategory.RuntimeError) return null;
				return ForRuntimeFailure(info);
		}
		return null;
	}

	static string ForRuntimeFailure(TaskEndInfo info)
	{
		// 스킬 에러는 에러 클래스 이름과 그 에러의 설명을 쓴다. 어떤 에러인지 SkillFailed보다 바로 보이기 때문
		if (info.Failure == FunctionFailure.SkillFailed && !string.IsNullOrEmpty(info.ErrorName))
			return $"{RuntimeLabel} {info.ErrorName}\n{info.ErrorMessage}";

		string name = info.IsPython ? PythonName(info.Failure) : info.Failure.ToString();
		string description = info.IsPython ? DescribePython(info.Failure) : Describe(info.Failure);
		return description != null ? $"{RuntimeLabel} {name}\n{description}" : $"{RuntimeLabel} {name}";
	}

	// Python 캐릭터에게 보이는 실패 이름 (#532, 이우림 결정 11/18). FunctionFailure는 그대로 두고 이름만 Python 예외 이름으로 바꾼다
	// NoTarget, InvalidTarget은 Python에 같은 예외가 없어 그대로 둔다 (이우림 결정 11/18 (가))
	static string PythonName(FunctionFailure failure)
	{
		switch (failure)
		{
			case FunctionFailure.UndeclaredVariable: return "NameError";
			case FunctionFailure.TypeMismatch: return "TypeError";
			case FunctionFailure.DivideByZero: return "ZeroDivisionError";
			case FunctionFailure.IndexOutOfRange: return "IndexError";
			case FunctionFailure.OutOfMemory: return "MemoryError";
			case FunctionFailure.Overflow: return "RecursionError";
			case FunctionFailure.InvalidLiteral:
			case FunctionFailure.NotInLoop:
			case FunctionFailure.DuplicateVariable:
			case FunctionFailure.MissingReturn:
				return "SyntaxError";
		}
		return failure.ToString();
	}

	// Python 실패의 설명 (#532, 이우림 결정 11/18). 대상 실패는 C와 같다
	static string DescribePython(FunctionFailure failure)
	{
		switch (failure)
		{
			case FunctionFailure.UndeclaredVariable: return "정의되지 않은 이름";
			case FunctionFailure.TypeMismatch: return "값의 타입이 맞지 않음";
			case FunctionFailure.DivideByZero: return "0으로 나눔";
			case FunctionFailure.IndexOutOfRange: return "리스트 범위 밖 index";
			case FunctionFailure.OutOfMemory: return "heap 메모리 부족";
			case FunctionFailure.Overflow: return "stack이 가득 참";
			case FunctionFailure.NoTarget:
			case FunctionFailure.InvalidTarget:
				return Describe(failure);
		}
		return null;
	}

	// runtime error인 실패의 설명. C 규칙에서 runtime error인 것만 둔다. 다른 언어에서 runtime error가 되는 실패는 이름만 보인다
	static string Describe(FunctionFailure failure)
	{
		switch (failure)
		{
			case FunctionFailure.Overflow: return "stack이 가득 차 프로세스 재시작";
			case FunctionFailure.DivideByZero: return "0으로 나눔";
			case FunctionFailure.InvalidPointer: return "free할 수 없는 포인터를 free";
			case FunctionFailure.DoubleFree: return "이미 free한 포인터를 다시 free";
			case FunctionFailure.NoTarget: return "스킬에 전달한 대상이 비어 있음";
			case FunctionFailure.InvalidTarget: return "스킬에 전달한 대상이 죽었거나 사라짐";
			case FunctionFailure.IndexOutOfRange: return "array 범위 밖 index";
		}
		return null;
	}
}
