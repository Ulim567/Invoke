using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 하나의 실행기(스레드 1개). 시전한 함수마다 작업을 하나 만들고, 틱마다 모든 작업을 한 단계씩 진행한다.
/// <para>
/// 사용법<br/>
/// - 캐릭터에 하나만 붙인다.<br/>
/// - 함수를 시전할 때 Cast(함수)를 부른다. 같은 함수든 다른 함수든 여러 번 부를 수 있다.<br/>
/// - 작업이 진행을 시작하거나 끝날 때 TaskStarted, TaskEnded로 알린다. 로그·이펙트 같은 연출은 이 이벤트를 받아서 한다.<br/>
/// - 작업이 입력 함수를 만나면 InputRequested로 알린다. 입력을 받는 쪽이 값을 정해 ProvideInput을 부르면 작업이 이어서 진행한다.
/// </para>
/// <para>
/// 틱 규칙<br/>
/// - 1틱은 FixedUpdate를 fixedUpdatesPerTick번 지난 것이다.<br/>
/// - 같은 틱에서는 먼저 시전한 작업부터 진행한다.<br/>
/// - 시전한 작업은 다음 틱부터 진행한다. 틱 도중에 시전되어도 같다.<br/>
/// - 끝난 작업은 그 틱의 모든 작업을 진행한 뒤에 한꺼번에 뺀다.
/// </para>
/// </summary>
[RequireComponent(typeof(ProcessMemory), typeof(LanguageRule))]
public class FunctionExecutor : MonoBehaviour
{
	[SerializeField, Min(1), Tooltip("1틱이 FixedUpdate 몇 번인지")]
	int fixedUpdatesPerTick = 1;

	// 진행 중인 작업. 틱마다 이 목록의 작업만 진행하며, 시전한 순서대로 쌓인다
	readonly List<FunctionTask> tasks = new List<FunctionTask>();
	// 다음 틱이 시작되기를 기다리는 작업. Cast는 항상 여기에 넣고, 다음 틱이 시작될 때 tasks로 옮긴다
	// 틱 도중에 tasks가 바뀌면 작업을 건너뛰거나 두 번 진행할 수 있고, 새 작업이 끝나는 틱도 생긴 위치에 따라 달라지기 때문
	readonly List<FunctionTask> pendingTasks = new List<FunctionTask>();

	// 시전할 때 차례로 검사하는 공통 조건. 앞의 조건에서 실패하면 뒤의 조건은 검사하지 않는다
	// 새 조건(쿨타임 등)은 Awake에서 이 목록에 추가한다
	readonly List<CastCondition> castConditions = new List<CastCondition>();

	int fixedUpdateCount;
	ProcessMemory memory;
	CooldownTracker cooldowns;
	LanguageRule language;

	/// <summary>
	/// 지금까지 지난 틱 수. 첫 틱이 1이다.
	/// </summary>
	public int CurrentTick { get; private set; }

	/// <summary>
	/// 1틱의 길이(초). 스킬 애니메이션 길이를 TickCount에 맞추거나, 쿨타임을 초로 보여 줄 때 쓴다.
	/// </summary>
	public float SecondsPerTick => Time.fixedDeltaTime * fixedUpdatesPerTick;

	/// <summary>
	/// 작업이 첫 단계를 진행하기 직전에 불린다.
	/// </summary>
	public event Action<FunctionTask> TaskStarted;

	/// <summary>
	/// 작업이 끝나서 목록에서 빠질 때 불린다. 끝난 이유는 task.EndReason으로 확인한다.<br/>
	/// Failed(플레이어 책임)이면 실패 종류는 task.Failure, 분류는 LanguageRule.GetFailureCategory로 확인한다. InternalError(개발자 책임)는 플레이어의 에러로 보여 주지 않는다.
	/// </summary>
	public event Action<FunctionTask> TaskEnded;

	/// <summary>
	/// 시전 전 검사에 걸려 시전하지 않았을 때 불린다. 거부한 함수와 거부 이유를 넘긴다.
	/// </summary>
	public event Action<FunctionBlock, CastRejection> CastRejected;

	/// <summary>
	/// 작업이 입력 함수(예: GetBug)를 만나 플레이어의 입력을 기다리기 시작할 때 불린다. 작업과 요청한 입력 함수를 넘긴다.<br/>
	/// 입력을 받는 쪽은 입력 함수의 Id로 자기가 맡은 입력인지 확인하고, 값을 정하면 ProvideInput을 부른다.<br/>
	/// 이 event 안에서 바로 ProvideInput을 불러도 된다(예: 슬롯 키가 눌려 있지 않아 기본값으로 진행).
	/// </summary>
	public event Action<FunctionTask, PredefinedFunction> InputRequested;

	/// <summary>
	/// 함수(사전 정의 함수, 사용자 정의 함수)를 시전해서 작업을 하나 만든다. 작업은 다음 틱부터 진행한다.<br/>
	/// 넘긴 함수는 원본으로 남고 복사본이 실행되므로, 같은 함수 객체를 여러 번 넘겨도 된다.<br/>
	/// 시전 전 검사(메모리 등)에 걸리면 작업을 만들지 않고 CastRejected를 알린 뒤 null을 돌려준다.
	/// </summary>
	public FunctionTask Cast(FunctionBlock function)
	{
		// 빈 슬롯은 시전하는 쪽(키 입력 처리)이 걸러 Cast를 부르지 않는다. 그래서 null은 코드 실수로 보고 바로 드러나게 한다
		if (function == null) throw new ArgumentNullException(nameof(function));
		return CastChecked(function, null);
	}

	/// <summary>
	/// 사전 정의 함수에 argument를 넘겨 시전한다. 슬롯에서 바로 시전할 때 플레이어가 고른 대상을 넘기는 데 쓴다.<br/>
	/// 예: Cast(thrust, RuntimeValue.FromBug(몬스터)). 스킬은 함수 안에서 불렸을 때와 같이 TryGetArgument로 읽는다.<br/>
	/// arguments는 Parameters 순서이고, 뒤쪽 파라미터는 비워도 된다(「값 없음」). argument가 없으면 Cast(function)과 같다.<br/>
	/// 넘긴 대상이 쓸 수 없는 대상(대상 없음, 죽음, 없어짐)이면, 작업이 시작할 때 InvalidTarget으로 끝난다.
	/// </summary>
	public FunctionTask Cast(PredefinedFunction function, params RuntimeValue[] arguments)
	{
		if (function == null) throw new ArgumentNullException(nameof(function));
		if (arguments == null || arguments.Length == 0) return CastChecked(function, null);

		// 값을 파라미터 타입으로 맞춘다. 맞출 수 없거나 파라미터보다 많으면 부르는 쪽의 코드 실수이므로 예외를 던진다
		IReadOnlyList<ParameterData> parameters = function.Parameters;
		if (arguments.Length > parameters.Count)
			throw new ArgumentException($"{function.Name}의 파라미터는 {parameters.Count}개인데 argument {arguments.Length}개를 넘겼습니다.", nameof(arguments));

		RuntimeValue?[] converted = new RuntimeValue?[parameters.Count];
		for (int i = 0; i < arguments.Length; i++)
		{
			if (!NumericRules.TryConvertForStore(arguments[i], parameters[i].Type, language.Rule, out RuntimeValue value))
				throw new ArgumentException($"{function.Name}의 파라미터 {parameters[i].Name}({parameters[i].Type.Kind})에 {arguments[i].Type?.Kind} 값을 넘겼습니다.", nameof(arguments));
			converted[i] = value;
		}
		return CastChecked(function, converted);
	}

	// 시전 전 검사를 한 뒤 작업을 만든다. arguments는 사전 정의 함수의 복사본에 넘길 값이고, 없으면 null
	FunctionTask CastChecked(FunctionBlock function, RuntimeValue?[] arguments)
	{
		foreach (CastCondition condition in castConditions)
		{
			if (condition.Check(function, out CastRejection rejection)) continue;

			// 시전 거부는 플레이어 코드의 잘못이 아니므로 언어 규칙 표를 거치지 않고 바로 알린다
			CastRejected?.Invoke(function, rejection);
			return null;
		}

		FunctionTask task = CastUnchecked(function, arguments);
		task.CastSource = function;
		cooldowns.Register(task);
		return task;
	}

	/// <summary>
	/// 슬롯의 function의 남은 쿨타임(틱 수). 쿨타임 중이 아니면 0이다. 쿨타임 UI에서 쓴다.<br/>
	/// 스킬이면 그 스킬(고정 ID)의 쿨타임이다. 사용자 정의 함수는 자기 쿨타임이 없으므로 0이다.
	/// </summary>
	// 쿨타임은 CooldownTracker가 가진다. 다른 시스템이 쓰는 접점을 바꾸지 않기 위해 실행기에도 남긴다
	public int GetRemainingCooldownTicks(FunctionBlock function) => cooldowns.GetRemainingTicks(function);

	/// <summary>
	/// 슬롯에서 시전한 function이 아직 실행 중이거나 다음 틱을 기다리고 있으면 true.<br/>
	/// 스킬이면 같은 스킬이 다른 함수 안에서 실행 중일 때도 true다.
	/// </summary>
	public bool IsRunning(FunctionBlock function) => cooldowns.IsRunning(function);

	/// <summary>
	/// 입력을 기다리는 작업에 플레이어가 고른 값을 넣는다. 작업은 다음 틱부터 이어서 진행한다.<br/>
	/// 작업이 이미 끝났거나(예: 실행 중단 공격) 입력을 기다리지 않으면 아무것도 하지 않고 false를 돌려준다.<br/>
	/// 값의 타입이 요청한 타입과 다르면 예외를 던진다. 대상 입력이면 RuntimeValue.FromBug(몬스터), 고를 대상이 없으면 RuntimeValue.FromBug(null)을 넘긴다.
	/// </summary>
	// 값을 바꾸는 함수다. Mirror를 붙일 때 서버에서 실행되도록 [Command]로 받는다
	public bool ProvideInput(FunctionTask task, RuntimeValue value)
	{
		if (task == null) throw new ArgumentNullException(nameof(task));
		return task.ProvideInput(value);
	}

	// 시전 전 검사 없이 작업을 만든다. Cast와, 블록이 요청한 비동기 시전이 쓴다
	// 안쪽 비동기 시전은 슬롯에서 시전한 것이 아니라 이미 실행 중인 코드의 일부이므로, 시전 전 검사를 하지 않는다
	// arguments가 있으면 복사본에 넘긴다. 원본에 넘기면 같은 원본을 쓰는 다른 시전과 값이 섞이기 때문
	FunctionTask CastUnchecked(FunctionBlock function, RuntimeValue?[] arguments = null)
	{
		FunctionBlock copy = (FunctionBlock)function.Clone();
		if (arguments != null) copy.SetArguments(arguments);

		FunctionTask task = new FunctionTask(gameObject, memory, cooldowns, language, copy);
		pendingTasks.Add(task);
		return task;
	}

	/// <summary>
	/// 진행 중인 작업과 다음 틱을 기다리는 작업을 모두 끊는다. 실행 중단 공격에 맞았을 때 부른다.<br/>
	/// 끊긴 작업은 EndReason이 Interrupted가 되고, 실행 중이던 함수들은 OnEnd 대신 OnCancel을 받으며 스택을 돌려준다.<br/>
	/// 끊긴 작업도 다른 작업처럼 틱이 끝날 때 목록에서 빠지며 TaskEnded가 불린다.
	/// </summary>
	public void InterruptAll()
	{
		// foreach 대신 번호로 돈다. OnCancel에서 Cast를 부르면 목록이 늘어나는데, foreach는 그때 오류를 내기 때문
		// 늘어난 작업도 끊어야 하므로 Count를 매번 다시 읽는다
		for (int i = 0; i < tasks.Count; i++)
			tasks[i].Interrupt();
		for (int i = 0; i < pendingTasks.Count; i++)
			pendingTasks[i].Interrupt();
	}

	/// <summary>
	/// 프로세스를 새로 시작한다(초기화). 진행 중인 작업을 모두 끊고, heap(소환검 포함)과 전역 변수를 비운 뒤, 저장한 전역 변수 파일의 초기값으로 전역 변수를 다시 만든다.<br/>
	/// 부르는 곳: 전역 변수 파일 저장(TrySaveGlobalVariables), 사망, 재부팅, 장소 이동(사냥터 ↔ 로비), 연습장 초기화. 접속 때는 이 컴포넌트가 전역 변수만 만든다.<br/>
	/// 끊긴 작업은 InterruptAll과 같이 EndReason이 Interrupted가 되고, 틱이 끝날 때 TaskEnded가 불린다.
	/// </summary>
	// 값을 바꾸는 함수다. Mirror를 붙일 때 서버에서만 실행되도록 [Server]를 붙인다
	public void RestartProcess()
	{
		// 작업을 먼저 끊는다. 끊기 전에 heap을 비우면, 이번 틱에 남은 작업이 사라진 객체의 포인터를 쓰게 되기 때문
		InterruptAll();
		memory.Heap.Clear();
		LoadGlobalVariables();
	}

	// 저장한 전역 변수 파일을 읽어 전역 변수를 초기값으로 다시 만든다
	// 실행 중에 메모리 크기가 줄었어도 전역 변수는 모두 만든다. 칸 검사는 저장할 때만 하고, 줄어든 만큼은 Free가 0이 되는 것으로 드러난다(스택 규칙과 같음)
	// 파일이 깨졌으면 GlobalVariablesConverter가 콘솔 오류를 남기고, 전역 변수 없이 시작한다
	void LoadGlobalVariables()
	{
		GlobalVariablesData file = GlobalVariablesStorage.Load();
		if (!GlobalVariablesConverter.TryConvert(file, language.Rule, int.MaxValue, out var variables))
			variables = new List<(string, VariableType, RuntimeValue)>();
		memory.ResetGlobals(variables);
	}

	/// <summary>
	/// 전역 변수 파일을 검사해서 저장하고, 프로세스를 새로 시작한다(RestartProcess).<br/>
	/// 검사에 걸리면(칸 부족, 대상 타입, 직접 입력한 값이 아닌 초기값 등) 저장하지 않고 false를 돌려준다. 거부 이유는 콘솔 오류로 남는다.
	/// </summary>
	// 값을 바꾸는 함수다. Mirror를 붙일 때 서버에서 실행되도록 [Command]로 받는다
	public bool TrySaveGlobalVariables(GlobalVariablesData file)
	{
		if (file == null) throw new ArgumentNullException(nameof(file));
		if (!GlobalVariablesConverter.TryConvert(file, language.Rule, memory.Capacity, out _)) return false;

		GlobalVariablesStorage.Save(file);
		RestartProcess();
		return true;
	}

	void Awake()
	{
		memory = GetComponent<ProcessMemory>();
		language = GetComponent<LanguageRule>();
		// RequireComponent는 이미 만든 프리팹에는 컴포넌트를 붙여 주지 않는다. 붙이지 않은 프리팹도 C 규칙으로 실행을 이어 가게 한다
		if (language == null)
		{
			Debug.LogError($"{name}: LanguageRule 컴포넌트가 없습니다. 언어 규칙 파일 없이 붙여서 실행합니다. 프리팹에 LanguageRule을 붙이고 언어 규칙 파일을 연결합니다.", this);
			language = gameObject.AddComponent<LanguageRule>();
		}
		cooldowns = new CooldownTracker(GetComponent<PlayerStats>(), () => CurrentTick);
		// 쿨타임을 먼저 검사한다. 둘 다 걸리면 「아직 쓸 수 없음」이 플레이어에게 더 정확한 이유이기 때문
		castConditions.Add(new CooldownCondition(cooldowns));
		castConditions.Add(new MemoryCondition(memory));
	}

	// 접속하면 저장한 전역 변수를 만든다. 접속 직후에는 끊을 작업도 비울 heap도 없으므로 RestartProcess의 나머지는 하지 않는다
	// RestartProcess를 부르지 않는 이유: 같은 오브젝트의 다른 컴포넌트가 Start에서 먼저 시전하면, 그 작업까지 끊기 때문(Start 순서는 Unity가 정하지 않음)
	// Awake가 아니라 Start에서 하는 이유: ProcessMemory가 Awake에서 전역 변수 저장소를 만든 뒤여야 하기 때문
	// heap이 누수를 판단할 때 실행 중인 작업들의 지역 변수도 루트로 보게 연결한다. ProcessMemory가 Awake에서 heap을 만든 뒤여야 하므로 Start에서 한다
	void Start()
	{
		memory.Heap.SetRunningLocalPointers(GetRunningLocalPointers);
		LoadGlobalVariables();
	}

	// 실행 중인 작업들의 지역 변수에 담긴 포인터
	// 끝난 작업은 뺀다. 이번 틱에 끝나 아직 목록에 남아 있어도, 끝난 순간 지역 변수는 사라졌기 때문
	IEnumerable<int> GetRunningLocalPointers()
	{
		foreach (FunctionTask task in tasks)
		{
			if (task.IsEnded) continue;
			foreach (int pointer in task.GetLocalPointers())
				yield return pointer;
		}
	}

	void FixedUpdate()
	{
		fixedUpdateCount++;
		if (fixedUpdateCount < fixedUpdatesPerTick) return;

		fixedUpdateCount = 0;
		RunTick();
	}

	void RunTick()
	{
		CurrentTick++;

		// 이벤트를 받은 쪽이 Cast를 불러도 pendingTasks가 도는 중에 바뀌지 않도록, 먼저 옮기고 나서 알린다
		int firstNew = tasks.Count;
		tasks.AddRange(pendingTasks);
		pendingTasks.Clear();
		// 진행을 시작하기 전에 끊긴 작업은 시작을 알리지 않는다. 이 틱이 끝날 때 TaskEnded만 불린다
		for (int i = firstNew; i < tasks.Count; i++)
			if (!tasks[i].IsEnded) TaskStarted?.Invoke(tasks[i]);

		// 작업은 결과만 남기고, 실행기가 읽어서 처리한다. 작업과 블록이 실행기를 거꾸로 부르지 않게 하기 위함
		foreach (FunctionTask task in GetTickOrder())
		{
			FunctionFailure? failure = task.Step();
			if (failure.HasValue) HandleFailure(task, failure.Value);

			// 상태를 WaitingInput으로 바꾼 뒤에 알린다. 받은 쪽이 이 안에서 바로 ProvideInput을 불러도 다음 틱부터 진행되게 하기 위함
			if (task.TryTakeNewInputRequest(out PredefinedFunction requester))
				InputRequested?.Invoke(task, requester);

			// 작업이 이번 틱에 끝났어도 요청은 시전한다. 함수의 마지막 줄이 비동기 호출이면, 요청한 틱에 원래 작업이 끝나기 때문
			// 새 작업은 pendingTasks에 들어가 다음 틱부터 진행한다. 지금 도는 tasks는 바뀌지 않는다
			foreach (FunctionBlock function in task.TakeAsyncRequests())
				CastUnchecked(function);
		}

		// 끝난 작업은 틱을 마친 뒤에 빼야, 이번 틱에 끝난 작업의 메모리가 진행 순서와 상관없이 항상 다음 틱부터 돌아온다
		foreach (FunctionTask task in tasks)
		{
			if (!task.IsEnded) continue;

			// 쿨타임은 여기서 걸지 않는다. 작업 안의 스킬이 끝날 때 스킬마다 이미 걸렸다
			cooldowns.Unregister(task);
			// TaskEnded보다 먼저 한다. 이벤트를 받은 쪽이 누수 여부까지 정해진 상태를 보게 하기 위함
			memory.Heap.HandleTaskEnded(task);
			TaskEnded?.Invoke(task);
		}
		tasks.RemoveAll(task => task.IsEnded);
	}

	// 작업이 플레이어 책임으로 실패했을 때, 작업의 Step이 돌려준 실패 종류로 실행기가 처리를 찾아 실행한다
	// 작업이 스스로 처리하지 않고 실행기가 맡는다. 재부팅처럼 다른 작업까지 끝내는 처리는 모든 작업을 가진 실행기만 할 수 있기 때문
	void HandleFailure(FunctionTask task, FunctionFailure failure)
	{
		// 표에 줄이 없거나 언어 규칙 파일이 없으면 개발자 책임이다. 플레이어의 에러로 보이지 않게 InternalError로 끝낸다
		if (!language.TryFindHandling(task.Name, failure, out FailureHandling handling))
		{
			task.End(FunctionTaskEndReason.InternalError);
			return;
		}

		switch (handling)
		{
			// 이미 실행 중인 작업은 시전을 되돌릴 수 없으므로, 거부도 그 작업만 끝내는 것으로 처리한다
			case FailureHandling.Reject:
			case FailureHandling.EndTask:
				task.End(FunctionTaskEndReason.Failed);
				break;
		}
	}

	// 같은 틱 안에서 작업을 진행하는 순서. tasks는 시전한 순서대로 쌓이므로 그대로 돌려준다
	// 순서 규칙을 바꿀 때는 이 함수만 고친다
	IReadOnlyList<FunctionTask> GetTickOrder() => tasks;
}
