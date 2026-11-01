using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 작업 하나의 실행 문맥. 블록과 사전 정의 함수가 실행 중에 닿을 수 있는 것들의 입구다.
/// <para>
/// 사용법<br/>
/// - 블록과 사전 정의 함수는 Step, OnStart, OnTick, OnEnd, OnCancel에서 이 객체를 받는다.<br/>
/// - 사전 정의 함수는 Caster로 시전한 캐릭터의 위치, 방향, 컴포넌트를 찾는다.<br/>
/// - 실행 문맥은 작업마다 하나다. 작업(FunctionTask)이 만들어 가진다.
/// </para>
/// <para>
/// 가지는 것<br/>
/// - 작업마다 다른 것(지역 변수 목록, 실패 기록, malloc한 포인터의 주인)은 실행 문맥이 직접 가진다.<br/>
/// - 캐릭터에 하나인 것(메모리, heap, 쿨타임 기록, 언어 규칙)은 원래 주인이 가지고, 실행 문맥은 그 주인에게 닿는 길만 준다. heap 규칙은 HeapStorage에 있다.
/// </para>
/// </summary>
// 작업 상태(FunctionTask)와 나눈 이유: 멀티플레이에서 서버만 블록을 실행하고 화면에는 작업 상태만 보낼 때 나누기 쉽고, 블록만 따로 테스트하기 쉽기 때문
// 실행기를 모른다. 비동기 시전도 요청만 기록하고, 실행기가 꺼내 간다. 블록과 실행 문맥이 실행기를 거꾸로 부르지 않게 하기 위함
public class FunctionContext
{
	// 이 문맥으로 실행하는 작업. malloc한 포인터의 주인으로 HeapStorage에 넘긴다
	readonly FunctionTask task;
	// 블록이 요청한 비동기 시전. 실행기가 Step 다음에 꺼내 가서 새 작업으로 만든다
	readonly List<FunctionBlock> asyncRequests = new List<FunctionBlock>();
	readonly LanguageRule language;
	// 실행 중인 사용자 정의 함수들의 지역 변수. 맨 뒤가 지금 실행 중인(가장 안쪽) 함수의 것이다
	// 목록인 이유: 함수 안에서 다른 함수를 부르면 불린 함수의 것이 위에 쌓이고, 끝나면 내려가 부른 함수의 것이 다시 맨 위가 된다(C의 호출 스택)
	readonly List<VariableStore> variableStack = new List<VariableStore>();

	/// <summary>
	/// 이 작업을 시전한 캐릭터.
	/// </summary>
	public GameObject Caster { get; }

	// 시전한 캐릭터의 메모리. 함수 블록이 프레임 칸을 쓰고 돌려주고, heap 함수가 HeapStorage를 찾을 때 쓴다
	internal ProcessMemory Memory { get; }

	// 시전한 캐릭터의 쿨타임 기록. 사전 정의 함수가 실행을 시작하고 끝낼 때 알린다
	internal CooldownTracker Cooldowns { get; }

	// 블록이 따를 언어 규칙(기본값, 숫자 타입 변환). 언어 규칙 파일이 없으면 C 규칙만 가진 임시 규칙이다
	internal LanguageRuleData Rule => language.Rule;

	// 지금 실행 중인 사용자 정의 함수의 지역 변수. 사용자 정의 함수 밖(사전 정의 함수만 시전한 작업)이면 null이다
	internal VariableStore CurrentVariables => variableStack.Count > 0 ? variableStack[variableStack.Count - 1] : null;

	// 변수 name을 가진 저장소. 지금 함수의 지역 변수를 먼저 찾고, 없으면 캐릭터의 전역 변수를 찾는다. 어디에도 없으면 null이다
	// 지역을 먼저 보는 이유: C처럼 같은 이름이면 지역 변수가 전역 변수를 가리기 때문
	// 선언은 이 함수를 쓰지 않는다. 함수 안의 선언은 언제나 지역 변수(CurrentVariables)에 만든다
	internal VariableStore FindVariables(string name)
	{
		VariableStore locals = CurrentVariables;
		if (locals != null && locals.Contains(name)) return locals;

		VariableStore globals = Memory != null ? Memory.Globals : null;
		if (globals != null && globals.Contains(name)) return globals;
		return null;
	}

	// 실패한 블록이 기록한 실패 종류. 작업이 Failure로 바깥에 보여 준다
	// null을 쓰는 이유: enum 기본값은 첫 값인 Overflow라서, 기록하지 않았는데도 Overflow로 보이기 때문
	internal FunctionFailure? RecordedFailure { get; private set; }
	// 스킬이 Fail로 낸 에러. 실패 종류가 SkillFailed일 때만 있다
	internal SkillError RecordedError { get; private set; }
	// 개발자 책임인 잘못을 블록이 기록했는지. true이면 작업이 언어 규칙 표를 거치지 않고 InternalError로 끝난다
	internal bool InternalErrorRecorded { get; private set; }

	internal FunctionContext(FunctionTask task, GameObject caster, ProcessMemory memory, CooldownTracker cooldowns, LanguageRule language)
	{
		this.task = task;
		this.language = language;
		Memory = memory;
		Cooldowns = cooldowns;
		Caster = caster;
	}

	// return 블록이 계산한 값(value slot의 복사본). 값을 비운 return;이거나 return을 만나지 않았으면 null이다
	// 작업마다 칸 하나로 충분하다. return 블록이 돌려준 Returned는 같은 틱 안에서 가장 안쪽 사용자 정의 함수까지 바로 올라가고, 그 사이에 다른 블록이 실행되지 않기 때문
	ValueBlock returnValue;

	// return 블록이 Returned를 돌려주기 직전에 값을 기록한다. 값을 비운 return;이면 null을 넘긴다
	// 값만이 아니라 value slot을 넘기는 이유: 직접 입력한 0을 포인터 반환 타입에서 NULL로 볼지(ZeroIsNull) 받는 쪽이 판단하기 때문
	internal void RecordReturnValue(ValueBlock value) => returnValue = value;

	// return 블록이 기록한 값을 꺼내고 지운다. Returned를 받은 사용자 정의 함수가 부른다
	internal ValueBlock TakeReturnValue()
	{
		ValueBlock taken = returnValue;
		returnValue = null;
		return taken;
	}

	// 사용자 정의 함수가 실행을 시작할 때 자기 지역 변수를 올리고, 끝나거나 끊길 때 내린다. UserFunction만 부른다
	internal void PushVariables(VariableStore variables) => variableStack.Add(variables);
	internal void PopVariables() => variableStack.RemoveAt(variableStack.Count - 1);

	/// <summary>
	/// 실패한 블록이 그 자리에서 실패 종류(플레이어 책임)를 기록한다. 기록한 뒤 그 블록은 BlockResult.Failed를 돌려준다.<br/>
	/// 스킬이 Fail로 낸 에러이면 error도 함께 넘긴다.
	/// </summary>
	internal void RecordFailure(FunctionFailure failure, SkillError error = null)
	{
		RecordedFailure = failure;
		RecordedError = error;
	}

	/// <summary>
	/// 개발자 책임인 잘못(예: 스킬이 Return을 빠뜨림)을 블록이 그 자리에서 기록한다. 기록한 뒤 그 블록은 BlockResult.Failed를 돌려준다.<br/>
	/// 콘솔 오류는 기록하는 블록이 낸다. 작업은 언어 규칙 표를 거치지 않고 InternalError로 끝난다.
	/// </summary>
	internal void RecordInternalError()
	{
		InternalErrorRecorded = true;
	}

	// 비동기 함수를 새 작업으로 시전해 달라고 요청한다. 실행기가 이번 틱의 Step 다음에 시전하므로, 새 작업은 다음 틱부터 진행한다
	internal void RequestAsyncCast(FunctionBlock function) => asyncRequests.Add(function);

	// 쌓인 요청을 꺼내고 비운다. 요청이 없는 틱이 대부분이므로, 없으면 빈 배열을 돌려주어 새 목록을 만들지 않는다
	internal IReadOnlyList<FunctionBlock> TakeAsyncRequests()
	{
		if (asyncRequests.Count == 0) return System.Array.Empty<FunctionBlock>();

		FunctionBlock[] taken = asyncRequests.ToArray();
		asyncRequests.Clear();
		return taken;
	}

	// 입력을 요청한 사전 정의 함수(입력 함수). 요청이 없거나 값을 꺼내 갔으면 null이다
	// 동기 함수는 한 작업 안에서 하나씩만 실행되므로, 한 작업에 요청은 언제나 하나뿐이다
	internal PredefinedFunction InputRequester { get; private set; }
	// 요청한 값의 타입. ProvideInput이 받은 값의 타입을 검사할 때 쓴다
	internal VariableType RequestedInputType { get; private set; }
	// 입력을 받는 쪽이 넣은 값. 아직 넣지 않았으면 null이다
	RuntimeValue? providedInput;

	// 입력을 요청했고 아직 값이 들어오지 않았으면 true. 작업은 이 동안 WaitingInput으로 멈추고, 사전 정의 함수는 틱을 세지 않는다
	internal bool IsWaitingInput => InputRequester != null && !providedInput.HasValue;

	// 입력 함수가 type 타입의 값을 입력받아야 한다고 기록한다. 실행기가 Step 다음에 읽어 InputRequested로 알린다
	internal void RequestInput(PredefinedFunction requester, VariableType type)
	{
		InputRequester = requester;
		RequestedInputType = type;
		providedInput = null;
	}

	// 입력을 받는 쪽이 넣은 값을 기록한다. 실행기의 ProvideInput만 부른다
	internal void ProvideInput(RuntimeValue value) => providedInput = value;

	// 들어온 값을 꺼내고 요청을 지운다. 값이 아직 없으면 false
	internal bool TryTakeInput(out RuntimeValue value)
	{
		value = providedInput ?? default;
		if (!providedInput.HasValue) return false;

		ClearInputRequest(InputRequester);
		return true;
	}

	// requester가 한 요청이 남아 있으면 지운다. 사전 정의 함수가 끝날 때 부른다
	internal void ClearInputRequest(PredefinedFunction requester)
	{
		if (InputRequester != requester) return;

		InputRequester = null;
		RequestedInputType = null;
		providedInput = null;
	}

	// obj를 heap에 올린다. 포인터의 주인은 이 작업이다. 주인을 채우는 것만 여기서 하고, 루트 집합과 누수 규칙은 HeapStorage가 맡는다
	internal int Malloc(object obj, int size) => Memory.Heap.Malloc(task, obj, size);

	// 사전 정의 함수가 정상으로 끝날 때, 그 함수가 malloc한 포인터를 이 작업의 포인터 목록에서 숨은 시스템 전역으로 옮긴다. PredefinedFunction만 부른다
	internal void MoveToSystemGlobals(IEnumerable<int> pointers) => Memory.Heap.MoveToSystemGlobals(task, pointers);
}
