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
/// 캐릭터 언어 하나의 실패 처리 표. 실패 종류마다 실행기가 무엇을 할지 정한다.
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
	}

	[SerializeField, Tooltip("실패 종류마다 할 처리. 모든 실패 종류가 한 줄씩 있어야 한다")]
	List<Rule> rules = new List<Rule>();

	/// <summary>
	/// failure의 처리를 찾는다. 표에 없으면 false를 돌려준다.
	/// </summary>
	public bool TryGetHandling(FunctionFailure failure, out FailureHandling handling)
	{
		foreach (Rule rule in rules)
		{
			if (rule.failure != failure) continue;

			handling = rule.handling;
			return true;
		}

		handling = default;
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
}
