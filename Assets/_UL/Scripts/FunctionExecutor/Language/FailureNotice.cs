/// <summary>
/// 시전 거부와 작업이 끝난 이유를 플레이어가 읽을 알림 문장으로 바꾼다. 형식은 두 줄로 「[분류] 이름」, 「설명」이다 (#480).
/// <para>
/// 사용법<br/>
/// - 화면(HUD)이 FunctionExecutorNetwork.CastRejected를 받으면 ForRejection, OwnTaskEnded를 받으면 ForTaskEnd를 부른다.<br/>
/// - null이 오면 알림을 띄우지 않는다.
/// </para>
/// <para>
/// 알리는 것: 시전 거부의 NotEnoughMemory / 실행 중단(Interrupted) / runtime error로 분류된 실패.<br/>
/// 알리지 않는 것: 그 밖의 시전 거부, 정상 종료, 취소(Cancelled), 개발자 책임(InternalError), compile error.
/// </para>
/// </summary>
// compile error를 알리지 않는 이유: compile error는 편집 화면이 저장할 때 잡고, 실행 중에는 runtime error만 나야 한다(이우림 결정 11/1, 11/9)
// 저장 검사가 아직 놓치는 compile error(DuplicateVariable 등)는 별도 이슈에서 저장 검사에 더한다
// 스킬 에러의 설명은 이 표에 두지 않고 SkillError.Message를 그대로 쓴다. 스킬 구현자가 새 에러를 만들어도 이 표를 고치지 않게 하기 위함
public static class FailureNotice
{
	const string RejectedLabel = "[Cast Rejected]";
	const string RuntimeLabel = "[Runtime Error]";

	/// <summary>
	/// 시전 거부 rejection의 알림 문장. NotEnoughMemory만 알리고, 그 밖의 거부면 null.
	/// </summary>
	// 다른 거부를 알리지 않는 이유: 쿨타임은 슬롯 덮개, 공격할 수 없는 장소는 무채색 슬롯처럼 화면이 이미 보여 준다. 메모리 부족만 슬롯에서 원인을 알 수 없다
	public static string ForRejection(CastRejection rejection)
	{
		if (rejection == CastRejection.NotEnoughMemory) return $"{RejectedLabel} NotEnoughMemory\n남은 메모리가 함수 크기보다 적음";
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
				return $"{RuntimeLabel} Interrupted\n실행 중단 공격으로 함수가 멈춤";
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

		string description = Describe(info.Failure);
		return description != null ? $"{RuntimeLabel} {info.Failure}\n{description}" : $"{RuntimeLabel} {info.Failure}";
	}

	// runtime error인 실패의 설명. C 규칙에서 runtime error인 것만 둔다. 다른 언어에서 runtime error가 되는 실패는 이름만 보인다
	static string Describe(FunctionFailure failure)
	{
		switch (failure)
		{
			case FunctionFailure.Overflow: return "stack이 가득 차 프로세스 재시작";
			case FunctionFailure.DivideByZero: return "0으로 나눔";
			case FunctionFailure.InvalidPointer: return "free할 수 없는 포인터를 free함";
			case FunctionFailure.InvalidTarget: return "대상이 없거나 죽음";
			case FunctionFailure.IndexOutOfRange: return "array 범위 밖 index";
		}
		return null;
	}
}
