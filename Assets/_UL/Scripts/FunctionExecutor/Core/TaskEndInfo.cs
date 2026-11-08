/// <summary>
/// 작업이 끝난 이유를 화면에 알릴 때 쓰는 값. 서버가 시전한 사람에게 보낸다 (FunctionExecutorNetwork.OwnTaskEnded).
/// <para>
/// - EndReason이 Failed일 때만 Failure, Category가 뜻이 있다.<br/>
/// - Failure가 SkillFailed일 때만 ErrorName(에러 클래스 이름, 예: UnarmedError), ErrorMessage(SkillError.Message)가 있다. 그 밖에는 빈 글자열이다.
/// </para>
/// </summary>
// FunctionTask를 그대로 보내지 않는 이유: 작업은 블록, 메모리 같은 서버의 실행 상태를 가지고 있어 네트워크로 보낼 수 없다
// SkillError도 객체가 아니라 이름과 문장만 보낸다. 스킬 구현자가 새 에러 클래스를 만들어도 네트워크 코드를 고치지 않게 하기 위함
// 필드를 public으로 두는 이유: Mirror가 public 필드만 가진 struct는 보내는 코드를 알아서 만들어 준다
public struct TaskEndInfo
{
	public FunctionTaskEndReason EndReason;
	public FunctionFailure Failure;
	public FailureCategory Category;
	public string ErrorName;
	public string ErrorMessage;

	/// <summary>
	/// 끝난 작업 task에서 알릴 값을 모은다. 분류는 시전자 캐릭터의 언어 language가 정한다(없으면 RuntimeError).
	/// </summary>
	public static TaskEndInfo From(FunctionTask task, LanguageRule language)
	{
		var info = new TaskEndInfo { EndReason = task.EndReason, ErrorName = "", ErrorMessage = "" };
		if (task.Failure is FunctionFailure failure)
		{
			info.Failure = failure;
			info.Category = language != null ? language.GetFailureCategory(failure) : FailureCategory.RuntimeError;
		}
		if (task.Error != null)
		{
			info.ErrorName = task.Error.GetType().Name;
			info.ErrorMessage = task.Error.Message ?? "";
		}
		return info;
	}
}
