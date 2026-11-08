/// <summary>
/// 시전 거부와 작업이 끝난 이유를 플레이어가 읽을 알림 문장으로 바꾼다. 형식은 「[분류] 이름: 설명」이다 (#480).
/// <para>
/// 사용법<br/>
/// - 화면(HUD)이 FunctionExecutorNetwork.CastRejected를 받으면 ForRejection, OwnTaskEnded를 받으면 ForTaskEnd를 부른다.<br/>
/// - null이 오면 알림을 띄우지 않는다.
/// </para>
/// <para>
/// 알리는 것: 시전 거부의 OnCooldown, NotEnoughMemory / 실행 중단(Interrupted) / runtime error로 분류된 실패.<br/>
/// 알리지 않는 것: 그 밖의 시전 거부, 취소(Cancelled), 개발자 책임(InternalError), compile error.
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
	/// 시전 거부 rejection의 알림 문장. 알리지 않는 거부면 null.
	/// </summary>
	public static string ForRejection(CastRejection rejection)
	{
		switch (rejection)
		{
			case CastRejection.OnCooldown:
				return $"{RejectedLabel} OnCooldown: 이 함수가 아직 실행 중이거나 쿨타임이 남아 있습니다. 끝날 때까지 기다리세요";
			case CastRejection.NotEnoughMemory:
				return $"{RejectedLabel} NotEnoughMemory: 남은 메모리가 이 함수의 크기보다 적습니다. 실행 중인 함수가 끝나거나 heap을 free하면 자리가 생깁니다";
		}
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
				return $"{RuntimeLabel} Interrupted: 몬스터의 실행 중단 공격을 맞아 실행 중이던 함수가 멈췄습니다";
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
			return $"{RuntimeLabel} {info.ErrorName}: {info.ErrorMessage}";

		string description = Describe(info.Failure);
		return description != null ? $"{RuntimeLabel} {info.Failure}: {description}" : $"{RuntimeLabel} {info.Failure}";
	}

	// runtime error인 실패의 설명. C 규칙에서 runtime error인 것만 둔다. 다른 언어에서 runtime error가 되는 실패는 이름만 보인다
	static string Describe(FunctionFailure failure)
	{
		switch (failure)
		{
			case FunctionFailure.Overflow: return "stack이 가득 차 함수를 더 부를 수 없습니다. 프로세스가 재시작되어 실행 중인 함수가 모두 멈추고 heap과 전역 변수가 초기화됐습니다";
			case FunctionFailure.DivideByZero: return "0으로 나누거나 0으로 나머지(%)를 구했습니다";
			case FunctionFailure.InvalidPointer: return "이미 free했거나 잃어버린 포인터를 free에 넣었습니다";
			case FunctionFailure.InvalidTarget: return "스킬에 넘긴 대상이 없거나 이미 죽었습니다";
			case FunctionFailure.IndexOutOfRange: return "array의 길이를 벗어난 index(음수 포함)로 원소를 읽거나 썼습니다";
		}
		return null;
	}
}
