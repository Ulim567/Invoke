using UnityEngine;

/// <summary>
/// 캐릭터 하나의 언어. 연결한 언어 규칙 파일(LanguageRuleData)로 실패 처리, 실패 분류, 기본값, 숫자 타입 변환 규칙을 알려 준다.
/// <para>
/// 사용법<br/>
/// - 캐릭터에 FunctionExecutor와 함께 붙이고, Inspector의 Rule Data 칸에 언어 규칙 파일(예: LanguageRule_C)을 연결한다.<br/>
/// - 화면은 GetFailureCategory로 실패가 compile error인지 runtime error인지 나누어 보여 준다.
/// </para>
/// </summary>
// 실행기와 따로 두는 이유: 편집 화면의 compile error 검사가 작업이나 실행기 없이 캐릭터의 언어 규칙을 보기 위함
public class LanguageRule : MonoBehaviour
{
	[SerializeField, Tooltip("이 캐릭터 언어의 규칙 파일")]
	LanguageRuleData ruleData;

	// 언어 규칙 파일이 연결되지 않았을 때 기본값을 읽는 임시 규칙. 처음 값(C 기본값)만 가진다
	LanguageRuleData fallbackRule;

	/// <summary>
	/// 블록이 따를 언어 규칙. 언어 규칙 파일이 없으면 처음 값(C 규칙)만 가진 임시 규칙이다.
	/// </summary>
	internal LanguageRuleData Rule
	{
		get
		{
			if (ruleData != null) return ruleData;

			if (fallbackRule == null) fallbackRule = ScriptableObject.CreateInstance<LanguageRuleData>();
			return fallbackRule;
		}
	}

	/// <summary>
	/// 이 캐릭터 언어에서 failure가 compile error인지 runtime error인지.<br/>
	/// 화면이 FunctionExecutor.TaskEnded에서 EndReason이 Failed인 작업의 task.Failure를 받아 보여 줄 때 쓴다.
	/// </summary>
	// Failed로 끝난 작업은 언제나 표에 줄이 있다. 줄이 없으면 실행기가 InternalError로 끝내기 때문
	// 그래도 줄이 없는 실패 종류를 넘기면 RuntimeError로 본다. 그 실패는 실행 중에 났고, 줄이 없는 것은 Play를 시작할 때 이미 콘솔 오류로 알렸다
	public FailureCategory GetFailureCategory(FunctionFailure failure)
	{
		if (ruleData != null && ruleData.TryGetCategory(failure, out FailureCategory category)) return category;
		return FailureCategory.RuntimeError;
	}

	/// <summary>
	/// 이 캐릭터 언어에서 type 타입 변수의 기본값. 선언 블록의 initializer가 비어 있을 때 쓴다.
	/// </summary>
	// 언어 규칙 파일이 없으면 C 기본값으로 실행을 이어 간다. 연결하지 않은 것은 Awake에서 이미 알렸다
	// 임시 규칙을 만들어 쓰는 이유: C 기본값을 이 파일에 한 번 더 적으면, 언어 규칙 파일의 처음 값과 어긋날 수 있기 때문
	internal bool TryGetDefaultValue(VariableType type, out RuntimeValue value) => Rule.TryGetDefaultValue(type, out value);

	// 실행기가 플레이어 책임인 실패를 처리할 때 부른다. 처리를 찾지 못하면 false. 실행기는 그 작업을 InternalError(개발자 책임)로 끝낸다
	// functionName은 어느 함수에서 났는지 콘솔 오류에 적기 위함
	// 예외를 던지지 않는다. 예외를 던지면 이번 틱의 나머지 작업이 진행되지 않고, 끝난 작업도 목록에서 빠지지 않기 때문
	internal bool TryFindHandling(string functionName, FunctionFailure failure, out FailureHandling handling)
	{
		if (ruleData != null && ruleData.TryGetHandling(failure, out handling)) return true;

		// Play를 시작할 때도 알렸지만, 어느 함수에서 났는지 알 수 있도록 실제로 났을 때 한 번 더 알린다
		if (ruleData == null)
			Debug.LogError($"{name}: LanguageRule에 언어 규칙 파일이 연결되지 않아 {functionName}의 {failure}를 처리할 수 없습니다. 작업을 InternalError로 끝냅니다.", this);
		else
			Debug.LogError($"{ruleData.name}: 실패 종류 {failure}의 처리가 표에 없습니다. {functionName}을 InternalError로 끝냅니다.", ruleData);
		handling = default;
		return false;
	}

	void Awake()
	{
		CheckRuleData();
	}

	// 코드로 만든 ScriptableObject는 Unity가 저절로 지우지 않으므로 직접 지운다
	void OnDestroy()
	{
		if (fallbackRule != null) Destroy(fallbackRule);
	}

	// 그 실패가 실제로 나기 전에, Play를 시작하자마자 표의 문제를 알리기 위함
	void CheckRuleData()
	{
		if (ruleData == null)
		{
			Debug.LogError($"{name}: LanguageRule에 언어 규칙 파일이 연결되지 않았습니다. 플레이어 함수가 실패하면 작업을 InternalError로 끝냅니다.", this);
			return;
		}

		foreach (FunctionFailure failure in ruleData.FindMissingFailures())
			Debug.LogError($"{ruleData.name}: 실패 종류 {failure}의 처리가 표에 없습니다.", ruleData);
	}
}
