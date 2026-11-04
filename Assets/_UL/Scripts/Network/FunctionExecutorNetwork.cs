using System;
using System.Collections.Generic;
using System.Text;
using Mirror;
using UnityEngine;

/// <summary>
/// 함수 실행기(FunctionExecutor)의 네트워크 쪽. 플레이어 프리팹에 FunctionExecutor, SlotLoadout과 함께 붙인다.
/// <para>
/// 실행 위치<br/>
/// - 함수는 서버의 실행기만 실행한다. 클라이언트로만 접속한 컴퓨터에서는 모든 플레이어 복사본의 실행기를 멈춘다(FunctionExecutor.TickEnabled).<br/>
/// - 호스트와 네트워크를 켜지 않은 테스트에서는 지금처럼 실행한다.
/// </para>
/// <para>
/// 함수 데이터 - 실제 계정 (서버 DB → 서버, 내 화면)<br/>
/// - 서버가 플레이어를 만들 때 DB에서 불러온 데이터를 넣는다(ApplyLoadedData). 내 화면은 파일을 보내지 않고 서버의 데이터를 받는다(PlayerDataReceived).<br/>
/// - 슬롯 8개는 언제나 그 슬롯의 슬롯 함수(slot1~slot8)다. 장착을 바꾸는 요청은 없고, 슬롯 함수와 페이지 함수의 내용을 RequestSaveFunction으로 저장한다.
/// </para>
/// <para>
/// 함수 데이터 - 임시 계정, 네트워크를 켜지 않은 테스트 (내 화면 → 서버)<br/>
/// - 내 캐릭터가 생기면 슬롯 8칸, 이 컴퓨터에 저장한 함수 전부, 전역 변수 파일을 서버로 보낸다.<br/>
/// - 슬롯 장착 내용이 바뀌면(SlotLoadout.Changed) 슬롯과 함수를 다시 보낸다. 전역 변수는 접속할 때만 보낸다.<br/>
/// - 서버는 받은 데이터로 슬롯마다 원본 함수를 만든다(ApplyPlayerData). 처음 다 만들면 IsDataReady가 true가 되고 DataReady가 불린다. 로딩 화면은 이것을 기다린다.
/// </para>
/// <para>
/// 시전 (내 화면 → 서버 → 화면들)<br/>
/// - 내 화면은 RequestCast(슬롯, 대상) 또는 RequestCast(슬롯, 대상 배열), RequestCancel(슬롯)로 서버에 부탁한다. FunctionExecutor.Cast, Cancel을 직접 부르지 않는다.<br/>
/// - 서버의 답은 시전한 사람에게만 온다: 성공이면 CastAccepted(슬롯, 작업 번호), 거부면 CastRejected(슬롯, 이유).<br/>
/// - 작업이 시작하고 끝나면 모든 화면에 TaskStarted(작업 번호, 슬롯, 함수 이름), TaskEnded(작업 번호, 끝난 이유)가 불린다.<br/>
/// - 네트워크를 켜지 않은 테스트에서도 같은 함수와 event로 동작한다. 이때는 서버로 보내지 않고 그 자리에서 실행기를 부른다.
/// </para>
/// <para>
/// 입력 대기 (서버 → 시전한 사람 → 서버)<br/>
/// - 내 작업이 입력 함수(GetBug)를 만나면 내 화면에 InputRequested(작업 번호, 입력 함수 ID, 슬롯)가 불린다.<br/>
/// - 고른 몬스터는 ProvideTarget(작업 번호, 몬스터)로 서버에 보낸다. 고를 몬스터가 없으면 null(대상 없음). 고르는 방식은 받는 쪽(SlotCaster)이 정한다.
/// </para>
/// <para>
/// 메모리·쿨타임 (서버 → 내 화면)<br/>
/// - 서버의 메모리 값이 내 화면의 ProcessMemory로 온다. 메모리 게이지는 지금처럼 ProcessMemory를 읽는다. 내 메모리는 나만 받는다(Sync Mode: Owner).<br/>
/// - 스킬의 쿨타임이 시작되면 끝나는 시각이 내 화면의 실행기로 온다. 쿨타임 UI는 지금처럼 FunctionExecutor.GetRemainingCooldownTicks를 읽는다.
/// </para>
/// </summary>
// FunctionExecutor와 따로 두는 이유: 실행기는 Mirror 없이 일반 코드로 두고, 네트워크는 이 컴포넌트에만 모으기 위함(CLAUDE.md 코드 작성 규칙)
[RequireComponent(typeof(FunctionExecutor), typeof(SlotLoadout))]
public class FunctionExecutorNetwork : NetworkBehaviour
{
	FunctionExecutor executor;
	SlotLoadout loadout;
	ProcessMemory memory;

	// [서버] 슬롯마다 시전할 원본. ApplyPlayerData만 바꾼다. 빈 슬롯이나 실행할 수 없는 함수는 null
	// 같은 슬롯은 같은 원본을 계속 써야 쿨타임, 실행 중 검사, 취소가 이어진다(function-executor.md)
	readonly FunctionBlock[] slotOriginals = new FunctionBlock[SlotLoadout.SlotCount];
	// [서버] 블록 함수의 원본(GUID → 원본). 함수 데이터가 그대로이면 다시 만들지 않고 이어 쓴다
	// 슬롯 자리만 바꿨을 때 새 원본을 만들면, 실행 중인 함수를 같은 슬롯에서 다시 시전할 수 있게 되기 때문
	readonly Dictionary<string, UserFunction> userFunctionOriginals = new Dictionary<string, UserFunction>();
	// [서버] 슬롯에서 물러났지만 아직 그 원본으로 시전한 작업이 실행 중인 원본 (슬롯 번호, 원본)
	// 함수를 고쳐 원본을 새로 만들어도, 실행 중인 작업을 그 슬롯의 취소로 끊고 끝날 때까지 다시 시전하지 못하게 하기 위함
	// 실행기의 취소와 실행 중 검사는 원본 객체로 작업을 찾으므로, 새 원본만으로는 옛 원본의 작업을 찾지 못한다
	readonly List<(int Slot, FunctionBlock Original)> retiredOriginals = new List<(int Slot, FunctionBlock Original)>();
	// [서버] 원본을 만들 때 쓴 함수 데이터 전체의 JSON. 함수가 하나라도 바뀌었는지 비교한다
	// 함수 하나만 바뀌어도 그 함수를 부르는 함수까지 다시 바꿔야 하므로, 바뀌면 블록 함수 원본을 모두 다시 만든다
	string userFunctionsSnapshot;
	// [서버] 받는 중인 함수 데이터. 슬롯을 받으면 ApplyPlayerData로 넘기고 비운다
	readonly List<FunctionData> receivingFunctions = new List<FunctionData>();
	// [서버] 받는 중인 전역 변수 파일. 접속할 때만 온다. 받지 않았으면 null
	GlobalVariablesData receivingGlobals;
	// [서버] 전역 변수를 한 번이라도 받아 적용했는지. 슬롯·함수와 전역 변수를 모두 받아야 준비된 것으로 본다
	bool globalsApplied;

	// 서버 DB의 데이터를 쓰는 플레이어(실제 계정)면 true. 서버가 플레이어를 연결에 붙이기 전에 정한다(ApplyLoadedData)
	// 실제 계정의 원본은 서버 DB이므로, 소유 클라이언트는 이 컴퓨터의 파일을 보내지 않고 서버가 보낸 데이터를 받는다
	// 「로그인 건너뛰기」 임시 계정과 네트워크를 켜지 않은 테스트는 false이고 지금처럼 이 컴퓨터의 파일을 쓴다
	[SyncVar] bool usesServerData;
	// [서버] DB에서 불러온 데이터. 소유 클라이언트가 달라고 하면(CmdRequestPlayerData) 보낸다
	SlotLoadout.Entry[] serverSlots;
	List<FunctionData> serverFunctions;
	GlobalVariablesData serverGlobals;
#if UNITY_SERVER || UNITY_EDITOR
	// [서버] 소유 클라이언트의 저장 요청을 쓸 DB와 캐릭터 key. ApplyLoadedData가 정한다
	ProgressStore store;
	string characterKey;
#endif
	// [내 화면] 서버에서 받는 중인 함수와 전역 변수. 슬롯을 받으면 ReceivedXxx로 옮긴다
	readonly List<FunctionData> receivingServerFunctions = new List<FunctionData>();
	GlobalVariablesData receivingServerGlobals;

	// [내 화면] 슬롯 장착이 바뀌어 다시 보내야 하는지
	// 장착을 한 번 바꿀 때 Changed가 여러 번 불리므로(자리 바꾸기, 다른 슬롯에서 옮겨 오기), 모아 두었다가 다음 Update에서 한 번만 보낸다
	bool resendRequested;

	// 서버가 이 플레이어의 데이터를 처음 다 받아 슬롯 원본을 만들었는지. 서버만 바꾼다
	[SyncVar(hook = nameof(OnDataReadyChanged))]
	bool dataReady;

	// 서버의 메모리 값. 서버가 매 프레임 ProcessMemory에서 옮기고, 바뀐 값만 Mirror가 보낸다
	// 내 메모리는 나만 보므로, 이 컴포넌트의 Sync Mode는 Owner로 둔다(프리팹 Inspector). Free는 받는 화면이 이 4개로 계산한다
	[SyncVar(hook = nameof(OnMemoryChanged))] int memoryCapacity;
	[SyncVar(hook = nameof(OnMemoryChanged))] int memoryDataUsed;
	[SyncVar(hook = nameof(OnMemoryChanged))] int memoryStackUsed;
	[SyncVar(hook = nameof(OnMemoryChanged))] int memoryHeapUsed;

	/// <summary>
	/// 서버가 이 플레이어의 함수 데이터(슬롯, 함수, 전역 변수)를 처음 다 받아 시전할 준비가 되었으면 true. 로딩 화면이 기다린다.
	/// </summary>
	public bool IsDataReady => dataReady;

	/// <summary>
	/// 이 플레이어의 슬롯·함수·전역 변수 원본이 서버 DB이면 true(실제 계정). false면 이 컴퓨터의 파일이다(임시 계정, 네트워크를 켜지 않은 테스트).
	/// </summary>
	public bool UsesServerData => usesServerData;

	/// <summary>[내 화면] 서버가 보낸 슬롯 8개. 받기 전이면 null.</summary>
	public IReadOnlyList<SlotLoadout.Entry> ReceivedSlots { get; private set; }

	/// <summary>[내 화면] 서버가 보낸 이 캐릭터의 함수 전부. 받기 전이면 null.</summary>
	public IReadOnlyList<FunctionData> ReceivedFunctions { get; private set; }

	/// <summary>[내 화면] 서버가 보낸 전역 변수 선언. 받기 전이면 null.</summary>
	public GlobalVariablesData ReceivedGlobals { get; private set; }

	/// <summary>
	/// [표시] 서버가 보낸 슬롯·함수·전역 변수를 처음 다 받았을 때, 그리고 저장 요청이 성공해 그 값이 바뀌었을 때. 실제 계정의 소유 클라이언트(호스트 포함)에서 불린다.<br/>
	/// 편집 화면은 이 컴퓨터의 파일 대신 ReceivedSlots, ReceivedFunctions, ReceivedGlobals로 그린다. 슬롯 i의 슬롯 함수는 ReceivedSlots[i]의 GUID인 함수다.
	/// </summary>
	public event Action PlayerDataReceived;

	/// <summary>
	/// [로직] 내가 보낸 저장 요청(RequestSaveFunction, RequestSaveGlobals)의 결과 (요청 종류, 결과). 소유 클라이언트에서 불린다.<br/>
	/// 성공(Saved)이면 이 event보다 먼저 ReceivedXxx가 새 값으로 바뀌고 PlayerDataReceived가 불린다. 거부되면 ReceivedXxx는 그대로다.
	/// </summary>
	public event Action<PlayerDataRequest, PlayerDataRequestResult> PlayerDataRequestAnswered;

	/// <summary>
	/// [표시] IsDataReady가 true가 될 때 한 번 불린다. 클라이언트(호스트 포함)에서 불린다. 로딩 화면을 닫을 때 쓴다.
	/// </summary>
	public event Action DataReady;

	/// <summary>
	/// [로직] 내가 부탁한 시전을 서버가 받아들였을 때 (슬롯 번호, 작업 번호). 시전한 사람의 화면에서만 불린다.
	/// </summary>
	public event Action<int, int> CastAccepted;

	/// <summary>
	/// [로직] 내가 부탁한 시전을 서버가 거부했을 때 (슬롯 번호, 거부 이유). 시전한 사람의 화면에서만 불린다.<br/>
	/// 이유: OnCooldown(실행 중이거나 쿨타임 중), NotEnoughMemory(메모리 부족), NoFunction(서버에 그 슬롯의 함수가 없음).
	/// </summary>
	public event Action<int, CastRejection> CastRejected;

	/// <summary>
	/// [연출] 이 캐릭터의 작업이 진행을 시작할 때 (작업 번호, 슬롯 번호, 함수 이름). 모든 화면에서 불린다. 시전 모션에 쓴다.<br/>
	/// 슬롯 번호는 슬롯에서 시전한 작업만 0~7이고, 함수 안에서 비동기로 시전된 작업은 -1이다.
	/// </summary>
	public event Action<int, int, string> TaskStarted;

	/// <summary>
	/// [로직] 이 캐릭터의 작업이 끝났을 때 (작업 번호, 끝난 이유). 모든 화면에서 불린다. 진행을 시작하기 전에 끊긴 작업은 TaskStarted 없이 이것만 불린다.
	/// </summary>
	public event Action<int, FunctionTaskEndReason> TaskEnded;

	/// <summary>
	/// [로직] 내 작업이 입력 함수를 만나 입력을 기다리기 시작했을 때 (작업 번호, 입력 함수 ID, 슬롯 번호). 시전한 사람의 화면에서만 불린다.<br/>
	/// 입력 함수 ID로 자기가 맡은 입력인지 확인한다(예: input.get_bug). 슬롯 번호는 비동기로 시전된 작업이면 -1이다.<br/>
	/// 값을 정하면 ProvideTarget을 부른다. 이 event 안에서 바로 불러도 된다.
	/// </summary>
	public event Action<int, string, int> InputRequested;

	// 네트워크를 켜지 않은 테스트이면 true. 이때는 서버로 보내지 않고 이 컴퓨터에서 바로 처리한다
	static bool IsOffline => !NetworkServer.active && !NetworkClient.active;

	void Awake()
	{
		executor = GetComponent<FunctionExecutor>();
		loadout = GetComponent<SlotLoadout>();
		memory = GetComponent<ProcessMemory>();
	}

	// 실행기의 event는 실행기가 돌고 있는 곳(서버, 호스트, 네트워크를 켜지 않은 테스트)에서만 불린다. 받아서 화면들에 전한다
	void OnEnable()
	{
		executor.TaskStarted += OnExecutorTaskStarted;
		executor.TaskEnded += OnExecutorTaskEnded;
		executor.InputRequested += OnExecutorInputRequested;
		executor.CooldownStarted += OnExecutorCooldownStarted;
	}

	void OnDisable()
	{
		executor.TaskStarted -= OnExecutorTaskStarted;
		executor.TaskEnded -= OnExecutorTaskEnded;
		executor.InputRequested -= OnExecutorInputRequested;
		executor.CooldownStarted -= OnExecutorCooldownStarted;
	}

	public override void OnStartClient()
	{
		base.OnStartClient();

		// 클라이언트로만 접속했으면 이 컴퓨터의 실행기는 함수를 실행하지 않는다. 값은 서버 한 곳에서만 바꾸기 위함
		// 호스트는 이 오브젝트가 곧 서버의 것이므로 그대로 실행한다
		if (isServer) return;
		executor.TickEnabled = false;
		// 처음 받은 메모리 값도 보여 준다. 그 뒤로는 값이 바뀔 때마다 OnMemoryChanged가 보여 준다
		ApplyDisplayedMemory();
	}

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();

		// 실제 계정은 서버가 DB에서 이미 적용했으므로 이 컴퓨터의 파일을 보내지 않고, 서버의 데이터를 받아 온다
		// 슬롯은 언제나 그 슬롯의 슬롯 함수이므로 장착을 바꾸는 요청이 없다. 슬롯 함수의 내용은 RequestSaveFunction으로 바꾼다
		if (usesServerData)
		{
			CmdRequestPlayerData();
			return;
		}

		loadout.Changed += OnLoadoutChanged;
		SendPlayerData(true);
	}

	public override void OnStopLocalPlayer()
	{
		base.OnStopLocalPlayer();

		loadout.Changed -= OnLoadoutChanged;
		resendRequested = false;
	}

	void OnLoadoutChanged(int slot) => resendRequested = true;

	void Update()
	{
		if (!isLocalPlayer) return;

		if (!resendRequested) return;
		resendRequested = false;
		SendPlayerData(false);
	}

	// [서버] 메모리 값을 SyncVar로 옮긴다. 같은 값을 넣으면 Mirror가 보내지 않으므로 매 프레임 옮겨도 된다
	void LateUpdate()
	{
		if (!isServer) return;

		memoryCapacity = memory.Capacity;
		memoryDataUsed = memory.DataUsed;
		memoryStackUsed = memory.StackUsed;
		memoryHeapUsed = memory.HeapUsed;
	}

	// [클라이언트] 서버의 메모리 값이 바뀌었다. 네 값 중 무엇이 바뀌었든 네 값을 모두 다시 넣는다
	void OnMemoryChanged(int oldValue, int newValue) => ApplyDisplayedMemory();

	// 호스트는 서버와 같은 ProcessMemory를 쓰므로 넣지 않는다. 넣으면 자기 값 대신 한 프레임 늦은 값으로 답하게 되기 때문
	void ApplyDisplayedMemory()
	{
		if (isServer) return;
		memory.SetDisplayedValues(memoryCapacity, memoryDataUsed, memoryStackUsed, memoryHeapUsed);
	}

	// [내 화면] 이 컴퓨터에 저장한 데이터를 서버로 보낸다. 서버는 마지막에 오는 슬롯을 받을 때 앞서 받은 것과 함께 적용한다
	// 함수는 하나씩 따로 보낸다. 한 메시지의 크기 한도(KCP ReliableMaxMessageSize, Base 씬 설정 약 290KB)를 함수 수와 상관없게 하기 위함
	// Command는 보낸 순서대로 도착하므로(신뢰 채널), 슬롯이 언제나 마지막에 도착한다
	void SendPlayerData(bool includeGlobals)
	{
		if (includeGlobals) CmdReceiveGlobalVariables(FunctionDataJson.ToJson(GlobalVariablesStorage.Load()));

		foreach (FunctionData function in FunctionStorage.LoadAll())
			CmdReceiveFunction(FunctionDataJson.ToJson(function));

		SlotLoadout.Entry[] slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++)
			slots[i] = loadout.GetEntry(i);
		CmdReceiveSlots(slots);
	}

	[Command]
	void CmdReceiveGlobalVariables(string json)
	{
		// 읽을 수 없으면 선언이 없는 빈 파일로 본다(GlobalVariablesStorage.Load와 같은 처리)
		// 버리면 전역 변수를 받은 적이 없어 「데이터 준비됨」이 켜지지 않고, 로딩 화면이 끝나지 않기 때문
		if (!FunctionDataJson.TryGlobalVariablesFromJson(json, out GlobalVariablesData globals))
		{
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 읽을 수 없는 전역 변수 파일을 받아, 전역 변수 없이 시작합니다.", this);
			globals = new GlobalVariablesData();
		}
		receivingGlobals = globals;
	}

	[Command]
	void CmdReceiveFunction(string json)
	{
		if (!FunctionDataJson.TryFromJson(json, out FunctionData function))
		{
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 읽을 수 없는 함수 데이터를 받아 버립니다.", this);
			return;
		}
		receivingFunctions.Add(function);
	}

	[Command]
	void CmdReceiveSlots(SlotLoadout.Entry[] slots)
	{
		// 실제 계정은 서버 DB가 원본이다. 클라이언트가 보낸 파일로 덮어쓰지 않는다
		if (usesServerData)
		{
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 서버 DB의 데이터를 쓰는 플레이어라 클라이언트가 보낸 슬롯·함수·전역 변수를 버립니다.", this);
			receivingFunctions.Clear();
			receivingGlobals = null;
			return;
		}
		ApplyPlayerData(slots, receivingFunctions, receivingGlobals);
		receivingFunctions.Clear();
		receivingGlobals = null;
	}

#if UNITY_SERVER || UNITY_EDITOR
	/// <summary>
	/// [서버] DB에서 불러온 슬롯·함수·전역 변수를 넣는다. 플레이어를 만든 직후, 연결에 붙이기 전에 AccountServer가 부른다.<br/>
	/// 이 플레이어는 서버 DB의 데이터를 쓰는 플레이어가 되어(UsesServerData), 클라이언트가 보낸 파일 데이터를 받지 않는다.<br/>
	/// 이후 소유 클라이언트의 저장 요청은 progressStore의 characterKey 캐릭터에 쓴다.
	/// </summary>
	[Server]
	public void ApplyLoadedData(CharacterProgressData data, ProgressStore progressStore, string characterKey)
	{
		// 연결에 붙이기 전에 켜야, 소유 클라이언트가 처음 받을 때부터 이 값이 true라서 파일을 보내지 않는다
		usesServerData = true;
		store = progressStore;
		this.characterKey = characterKey;
		serverSlots = data.Slots;
		serverFunctions = data.Functions;
		serverGlobals = data.Globals;
		ApplyPlayerData(serverSlots, serverFunctions, serverGlobals);
	}
#endif

	#region 저장 요청 (소유 클라이언트 → 서버 → DB)

	/// <summary>
	/// [소유 클라이언트] 함수 하나를 서버에 저장하라고 요청한다. 같은 GUID의 함수가 있으면 덮어쓴다. 결과는 PlayerDataRequestAnswered.<br/>
	/// 슬롯 함수(ReceivedSlots의 GUID)와 페이지 함수 모두 이 요청으로 저장한다. 슬롯 함수를 저장하면 그 슬롯은 새 내용으로 시전된다.<br/>
	/// 새 GUID는 페이지 함수로 세고, 이미 ProgressRules.MaxPageFunctions개면 TooManyFunctions.<br/>
	/// 슬롯 함수의 이름(slot1~slot8)·파라미터(없음)·반환 타입(Void)·비동기 여부(아님) 중 하나라도 바뀌어 있으면 SlotFunctionLocked.
	/// </summary>
	public void RequestSaveFunction(FunctionData function)
	{
		if (!CanRequest(PlayerDataRequest.SaveFunction)) return;
		if (function == null)
		{
			PlayerDataRequestAnswered?.Invoke(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.InvalidData);
			return;
		}
		CmdSaveFunction(FunctionDataJson.ToJson(function));
	}

	/// <summary>
	/// [소유 클라이언트] 전역 변수 선언을 서버에 저장하라고 요청한다. 결과는 PlayerDataRequestAnswered.<br/>
	/// 서버는 FunctionExecutor.TrySaveGlobalVariables와 같은 검사를 하고, 통과하면 DB에 쓰고 프로세스를 새로 시작한다(RestartProcess).
	/// </summary>
	public void RequestSaveGlobals(GlobalVariablesData globals)
	{
		if (!CanRequest(PlayerDataRequest.SaveGlobals)) return;
		if (globals == null)
		{
			PlayerDataRequestAnswered?.Invoke(PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.InvalidData);
			return;
		}
		CmdSaveGlobals(FunctionDataJson.ToJson(globals));
	}

	// 서버 DB를 쓰는 플레이어의 소유 클라이언트만 요청할 수 있다. 임시 계정과 네트워크를 켜지 않은 테스트는 지금처럼 이 컴퓨터의 파일에 저장한다
	bool CanRequest(PlayerDataRequest request)
	{
		if (usesServerData && isLocalPlayer) return true;
		PlayerDataRequestAnswered?.Invoke(request, PlayerDataRequestResult.NotAllowed);
		return false;
	}

	[Command]
	void CmdSaveFunction(string json)
	{
		if (!usesServerData) { TargetAnswer(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.NotAllowed); return; }
		if (!FunctionDataJson.TryFromJson(json, out FunctionData function) || function == null || string.IsNullOrEmpty(function.Guid))
		{
			TargetAnswer(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.InvalidData);
			return;
		}

		// 슬롯 함수는 function body만 바꿀 수 있다. 이름(slot1~slot8)·파라미터(없음)·반환 타입(Void)·비동기 여부(아님) 중 하나라도 다르면 거부한다
		int slot = Array.FindIndex(serverSlots, e => e.Kind == SlotLoadout.EntryKind.UserFunction && e.Id == function.Guid);
		if (slot >= 0 && !KeepsSlotFrame(function, slot))
		{
			TargetAnswer(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.SlotFunctionLocked);
			return;
		}

		int index = serverFunctions.FindIndex(f => f.Guid == function.Guid);
#if UNITY_SERVER || UNITY_EDITOR
		// 새 GUID는 페이지 함수다. 슬롯 함수 8개는 캐릭터를 만들 때 서버가 만들고, 그 GUID는 바뀌지 않기 때문
		bool isNew = !store.HasFunction(characterKey, function.Guid);
		if (isNew && store.CountPageFunctions(characterKey) >= ProgressRules.MaxPageFunctions)
		{
			TargetAnswer(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.TooManyFunctions);
			return;
		}
		store.SaveFunction(characterKey, function.Guid, json);
#endif
		if (index < 0) serverFunctions.Add(function);
		else serverFunctions[index] = function;
		// 바뀐 함수를 쓰는 슬롯 원본을 다시 만든다. 전역 변수는 그대로 둔다(null)
		ApplyPlayerData(serverSlots, serverFunctions, null);

		TargetFunctionSaved(json);
		TargetAnswer(PlayerDataRequest.SaveFunction, PlayerDataRequestResult.Saved);
	}

	// 슬롯 함수의 틀(이름·파라미터·반환 타입·비동기 여부)이 서버가 만든 그대로인지 본다.
	// 반환 타입이 비어 있으면 FunctionDataConverter처럼 Void로 본다
	static bool KeepsSlotFrame(FunctionData function, int slot)
	{
		if (function.Name != ProgressRules.SlotFunctionName(slot)) return false;
		if (function.Parameters != null && function.Parameters.Count > 0) return false;
		if (function.ReturnType != null && function.ReturnType.Kind != VariableTypeKind.Void) return false;
		return !function.IsAsync;
	}

	[Command]
	void CmdSaveGlobals(string json)
	{
		if (!usesServerData) { TargetAnswer(PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.NotAllowed); return; }
		if (!FunctionDataJson.TryGlobalVariablesFromJson(json, out GlobalVariablesData globals) || globals == null)
		{
			TargetAnswer(PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.InvalidData);
			return;
		}
		if (!executor.CanUseGlobalVariables(globals))
		{
			TargetAnswer(PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.GlobalsRejected);
			return;
		}

#if UNITY_SERVER || UNITY_EDITOR
		store.SaveGlobals(characterKey, json);
#endif
		serverGlobals = globals;
		// 전역 변수를 저장하면 프로세스를 새로 시작한다(TrySaveGlobalVariables와 같음)
		ApplyPlayerData(serverSlots, serverFunctions, serverGlobals);

		TargetGlobalsSaved(json);
		TargetAnswer(PlayerDataRequest.SaveGlobals, PlayerDataRequestResult.Saved);
	}

	[TargetRpc]
	void TargetAnswer(PlayerDataRequest request, PlayerDataRequestResult result)
	{
		if (result != PlayerDataRequestResult.Saved)
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 서버가 {request} 요청을 거부했습니다: {result}", this);
		PlayerDataRequestAnswered?.Invoke(request, result);
	}

	// [소유 클라이언트] 저장이 성공한 값으로 ReceivedXxx를 바꾼다. 결과(TargetAnswer)보다 먼저 온다
	[TargetRpc]
	void TargetFunctionSaved(string json)
	{
		if (!FunctionDataJson.TryFromJson(json, out FunctionData function)) return;
		var functions = ReceivedFunctions != null ? new List<FunctionData>(ReceivedFunctions) : new List<FunctionData>();
		int index = functions.FindIndex(f => f.Guid == function.Guid);
		if (index < 0) functions.Add(function);
		else functions[index] = function;
		ReceivedFunctions = functions;
		PlayerDataReceived?.Invoke();
	}

	[TargetRpc]
	void TargetGlobalsSaved(string json)
	{
		if (!FunctionDataJson.TryGlobalVariablesFromJson(json, out GlobalVariablesData globals) || globals == null) return;
		ReceivedGlobals = globals;
		PlayerDataReceived?.Invoke();
	}

	#endregion

	// [서버] 소유 클라이언트가 서버의 슬롯·함수·전역 변수를 달라고 한다. 소유 클라이언트에게만 보낸다
	// 함수는 하나씩 따로, 슬롯은 마지막에 보낸다(SendPlayerData와 같은 이유)
	[Command]
	void CmdRequestPlayerData()
	{
		if (!usesServerData) return;

		TargetReceiveServerGlobals(FunctionDataJson.ToJson(serverGlobals ?? new GlobalVariablesData()));
		if (serverFunctions != null)
		{
			foreach (FunctionData function in serverFunctions)
				TargetReceiveServerFunction(FunctionDataJson.ToJson(function));
		}
		TargetReceiveServerSlots(serverSlots ?? CharacterProgressData.NewEmptySlots());
	}

	[TargetRpc]
	void TargetReceiveServerGlobals(string json)
	{
		receivingServerGlobals = FunctionDataJson.TryGlobalVariablesFromJson(json, out GlobalVariablesData globals) && globals != null
			? globals
			: new GlobalVariablesData();
		receivingServerFunctions.Clear();
	}

	[TargetRpc]
	void TargetReceiveServerFunction(string json)
	{
		if (FunctionDataJson.TryFromJson(json, out FunctionData function)) receivingServerFunctions.Add(function);
		else Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 서버가 보낸 함수 데이터를 읽을 수 없어 버립니다.", this);
	}

	[TargetRpc]
	void TargetReceiveServerSlots(SlotLoadout.Entry[] slots)
	{
		ReceivedSlots = slots;
		ReceivedFunctions = new List<FunctionData>(receivingServerFunctions);
		ReceivedGlobals = receivingServerGlobals ?? new GlobalVariablesData();
		receivingServerFunctions.Clear();
		receivingServerGlobals = null;
		Debug.Log($"[FunctionExecutorNetwork] {name}: 서버의 데이터를 받음 (함수 {ReceivedFunctions.Count}개, 전역 변수 {ReceivedGlobals.Declarations.Count}개)", this);
		PlayerDataReceived?.Invoke();
	}

	/// <summary>
	/// [서버] 플레이어의 슬롯 장착 내용, 가진 함수 전부, 전역 변수 파일로 슬롯마다 시전할 원본을 만든다.<br/>
	/// globals가 null이 아니면 그 파일로 전역 변수를 정하고 프로세스를 새로 시작한다(RestartProcess). null이면 전역 변수는 그대로 둔다.<br/>
	/// 실제 계정은 DB에서 불러온 데이터(ApplyLoadedData), 임시 계정은 클라이언트가 보낸 이 컴퓨터의 파일 데이터가 들어온다.
	/// </summary>
	// 플레이어 데이터가 들어오는 곳을 이 함수 하나로 모은다. 나중에 받은 데이터를 검사(보안)할 때 이 함수에만 넣으면 되게 하기 위함
	[Server]
	public void ApplyPlayerData(IReadOnlyList<SlotLoadout.Entry> slots, IReadOnlyList<FunctionData> functions, GlobalVariablesData globals)
	{
		RefreshUserFunctionOriginals(functions);
		FunctionBlock[] previous = (FunctionBlock[])slotOriginals.Clone();
		for (int i = 0; i < slotOriginals.Length; i++)
			slotOriginals[i] = slots != null && i < slots.Count ? BuildOriginal(i, slots[i], functions) : null;
		RetireReplacedOriginals(previous);

		if (globals != null)
		{
			executor.SetGlobalVariablesFile(globals);
			executor.RestartProcess();
			globalsApplied = true;
		}

		Debug.Log($"[FunctionExecutorNetwork] {name}: 함수 {functions.Count}개, 슬롯 [{DescribeSlots()}]{(globals != null ? $", 전역 변수 {globals.Declarations.Count}개" : "")} 받음", this);
		if (!dataReady && globalsApplied) dataReady = true;
	}

	// 슬롯에서 빠진 옛 원본 중 아직 작업이 실행 중인 것을 기억한다. 다른 슬롯으로 옮겨 간 원본(자리 바꾸기)은 그 슬롯이 맡으므로 기억하지 않는다
	void RetireReplacedOriginals(FunctionBlock[] previous)
	{
		for (int i = 0; i < previous.Length; i++)
		{
			FunctionBlock old = previous[i];
			if (old == null || Array.IndexOf(slotOriginals, old) >= 0 || !executor.IsRunning(old)) continue;
			retiredOriginals.Add((i, old));
		}
	}

	// slot번 슬롯에서 물러났고 아직 작업이 실행 중인 옛 원본. 작업이 끝난 원본은 이때 지운다
	List<FunctionBlock> RunningRetiredOriginals(int slot)
	{
		retiredOriginals.RemoveAll(r => !executor.IsRunning(r.Original));
		List<FunctionBlock> running = new List<FunctionBlock>();
		foreach ((int retiredSlot, FunctionBlock original) in retiredOriginals)
			if (retiredSlot == slot) running.Add(original);
		return running;
	}

	// 함수 데이터가 하나라도 바뀌었으면 블록 함수 원본을 모두 버린다. 바뀌지 않았으면 그대로 이어 쓴다
	void RefreshUserFunctionOriginals(IReadOnlyList<FunctionData> functions)
	{
		StringBuilder snapshot = new StringBuilder();
		foreach (FunctionData function in functions)
			snapshot.Append(FunctionDataJson.ToJson(function)).Append('\n');

		string current = snapshot.ToString();
		if (current == userFunctionsSnapshot) return;

		userFunctionsSnapshot = current;
		userFunctionOriginals.Clear();
	}

	// 슬롯 하나의 원본. 빈 슬롯, 없는 스킬, 없는 함수, 실행할 수 없는 함수면 null
	// 실행할 수 없는 함수는 FunctionDataConverter가 콘솔 오류를 남긴다
	FunctionBlock BuildOriginal(int slot, SlotLoadout.Entry entry, IReadOnlyList<FunctionData> functions)
	{
		if (entry.IsEmpty) return null;

		if (entry.Kind == SlotLoadout.EntryKind.Skill)
		{
			if (SlotLoadout.IsEquippableSkill(entry.Id) && PredefinedFunctionRegistry.TryGet(entry.Id, out PredefinedFunction skill)) return skill;
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 슬롯 {slot + 1}에 넣을 수 없는 스킬 '{entry.Id}'", this);
			return null;
		}

		if (userFunctionOriginals.TryGetValue(entry.Id, out UserFunction cached)) return cached;

		FunctionData data = null;
		foreach (FunctionData function in functions)
			if (function.Guid == entry.Id) { data = function; break; }
		if (data == null)
		{
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 슬롯 {slot + 1}의 함수({entry.Id})를 받지 못했습니다.", this);
			return null;
		}

		if (!FunctionDataConverter.TryConvert(data, functions, out UserFunction converted)) return null;
		userFunctionOriginals[entry.Id] = converted;
		return converted;
	}

	// 로그용: 슬롯마다 원본 이름. 비었거나 만들지 못했으면 -
	string DescribeSlots()
	{
		string[] names = new string[slotOriginals.Length];
		for (int i = 0; i < slotOriginals.Length; i++)
			names[i] = slotOriginals[i] != null ? slotOriginals[i].Name : "-";
		return string.Join(", ", names);
	}

	void OnDataReadyChanged(bool oldValue, bool newValue)
	{
		if (newValue) DataReady?.Invoke();
	}

	/// <summary>
	/// [내 화면] 서버에 slot번(0~7) 슬롯의 시전을 부탁한다. 대상(Target) 파라미터가 있는 스킬이면 target에 고른 몬스터를 넘기고, 없으면 null.<br/>
	/// 답은 CastAccepted 또는 CastRejected로 온다. 슬롯 키 입력(SlotCaster)이 부른다.
	/// </summary>
	public void RequestCast(int slot, GameObject target = null)
	{
		if (IsOffline) ServerCast(slot, target, null);
		else CmdCast(slot, target);
	}

	/// <summary>
	/// [내 화면] 서버에 slot번(0~7) 슬롯의 시전을 부탁하며 고른 몬스터 여러 마리를 넘긴다. 대상 array(Target array) 파라미터가 있는 스킬(전장 휩쓸기)에 쓴다.<br/>
	/// 서버는 targets를 순서대로 RuntimeValue.FromBugs로 바꿔 첫 파라미터에 넣는다. 그사이 사라진 몬스터는 「대상 없음」 원소가 된다.<br/>
	/// 답은 CastAccepted 또는 CastRejected로 온다. 대상 없이 시전하려면 RequestCast(slot)을 부른다. RequestCast(slot, null)은 어느 함수인지 정할 수 없어 컴파일 오류가 난다.
	/// </summary>
	public void RequestCast(int slot, GameObject[] targets)
	{
		if (IsOffline) ServerCast(slot, null, targets);
		else CmdCastTargets(slot, targets);
	}

	/// <summary>
	/// [내 화면] 서버에 slot번 슬롯으로 시전한 작업을 끊어 달라고 부탁한다(FunctionExecutor.Cancel). 끊긴 작업은 TaskEnded(Cancelled)로 알 수 있다.<br/>
	/// 취소 키(SlotCaster)가 부른다.
	/// </summary>
	public void RequestCancel(int slot)
	{
		if (IsOffline) ServerCancel(slot);
		else CmdCancel(slot);
	}

	/// <summary>
	/// [내 화면] 입력을 기다리는 내 작업(InputRequested의 작업 번호)에 고른 몬스터를 넣어 달라고 서버에 부탁한다. 고를 몬스터가 없으면 null(대상 없음).<br/>
	/// 작업이 그사이 끝났으면(취소, 실행 중단 공격) 서버가 버린다. 대상 입력(input.get_bug)에 쓴다.
	/// </summary>
	public void ProvideTarget(int taskId, GameObject target)
	{
		if (IsOffline) ServerProvideTarget(taskId, target);
		else CmdProvideTarget(taskId, target);
	}

	[Command]
	void CmdProvideTarget(int taskId, GameObject target) => ServerProvideTarget(taskId, target);

	// 작업 번호로 서버의 작업을 찾아 몬스터를 넣는다. 이미 끝났거나 입력을 기다리지 않는 작업이면 실행기가 false를 돌려주고 버린다
	// 나중에 보안 검사(그 몬스터를 정말 고를 수 있었는지)를 넣을 곳이다
	void ServerProvideTarget(int taskId, GameObject target)
	{
		if (!executor.TryGetTask(taskId, out FunctionTask task))
		{
			Debug.Log($"[FunctionExecutorNetwork] {name}: 작업 {taskId}이(가) 없어 입력을 버립니다 (이미 끝남)", this);
			return;
		}

		Monster monster = target != null ? target.GetComponent<Monster>() : null;
		try
		{
			if (!executor.ProvideInput(task, RuntimeValue.FromBug(monster)))
				Debug.Log($"[FunctionExecutorNetwork] {name}: 작업 {taskId}이(가) 입력을 기다리지 않아 버립니다", this);
		}
		catch (ArgumentException e)
		{
			// Bug가 아닌 값을 기다리는 작업에 몬스터가 들어왔다. 클라이언트가 보낸 값 하나 때문에 서버의 Command 처리에서 처리되지 않은 오류가 나지 않게 예외를 받아 버린다
			Debug.LogWarning($"[FunctionExecutorNetwork] {name}: 작업 {taskId}에 맞지 않는 입력을 버립니다. {e.Message}", this);
		}
	}

	[Command]
	void CmdCast(int slot, GameObject target) => ServerCast(slot, target, null);

	// Mirror의 Command는 이름으로 구분되므로 CmdCast를 오버로드하지 않고 이름을 따로 둔다
	[Command]
	void CmdCastTargets(int slot, GameObject[] targets) => ServerCast(slot, null, targets);

	[Command]
	void CmdCancel(int slot) => ServerCancel(slot);

	// 서버(또는 네트워크를 켜지 않은 테스트)에서 슬롯의 원본을 시전하고, 결과를 시전한 사람에게 알린다
	// [Server]를 붙이지 않는다. 네트워크를 켜지 않은 테스트에서도 같은 함수로 시전하기 위함
	// 대상 하나(target)와 여러 대상(targets)을 한 함수에서 받는다. 슬롯 확인과 거부 규칙을 한 곳에만 두기 위함
	// 나중에 보안 검사(그 몬스터들을 정말 고를 수 있었는지)를 넣을 곳이다
	void ServerCast(int slot, GameObject target, GameObject[] targets)
	{
		FunctionBlock original = GetSlotOriginal(slot);
		if (original == null)
		{
			ReplyCastRejected(slot, CastRejection.NoFunction);
			return;
		}
		// 함수를 고치기 전에 이 슬롯으로 시전한 작업이 아직 실행 중이면 거부한다. 한 슬롯의 작업은 하나뿐이라는 규칙을 지키기 위함
		if (!IsOffline && RunningRetiredOriginals(slot).Count > 0)
		{
			ReplyCastRejected(slot, CastRejection.OnCooldown);
			return;
		}

		CastRejection? rejection;
		FunctionTask task;
		if (TakesTargetArray(original) && targets != null)
			task = executor.Cast((PredefinedFunction)original, new[] { RuntimeValue.FromBugs(ToMonsters(targets)) }, out rejection);
		else if (TakesTarget(original) && target != null)
			task = executor.Cast((PredefinedFunction)original, new[] { RuntimeValue.FromBug(target.GetComponent<Monster>()) }, out rejection);
		else
			task = executor.Cast(original, out rejection);

		if (task == null) ReplyCastRejected(slot, rejection ?? CastRejection.NoFunction);
		else ReplyCastAccepted(slot, task.Id);
	}

	void ServerCancel(int slot)
	{
		FunctionBlock original = GetSlotOriginal(slot);
		bool cancelled = original != null && executor.Cancel(original);
		// 함수를 고치기 전에 이 슬롯으로 시전한 작업도 끊는다
		if (!IsOffline)
			foreach (FunctionBlock retired in RunningRetiredOriginals(slot))
				cancelled |= executor.Cancel(retired);
		Debug.Log($"[FunctionExecutorNetwork] {name}: 슬롯 {slot + 1} 취소 {(cancelled ? "끊음" : "끊을 작업 없음")}", this);
	}

	// 대상(Target) 파라미터 하나를 받는 스킬인지. 그때만 고른 몬스터를 argument로 넘긴다
	// 파라미터가 없는 스킬에 argument를 넘기면 실행기가 예외를 던지기 때문
	static bool TakesTarget(FunctionBlock function) =>
		function is PredefinedFunction skill && skill.Parameters.Count > 0 && skill.Parameters[0].Type?.Kind == VariableTypeKind.Target;

	// 첫 파라미터가 대상 array(Target array)인 스킬인지. 그때만 고른 몬스터들을 argument로 넘긴다 (전장 휩쓸기)
	static bool TakesTargetArray(FunctionBlock function) =>
		function is PredefinedFunction skill && skill.Parameters.Count > 0
		&& skill.Parameters[0].Type?.Kind == VariableTypeKind.Array
		&& skill.Parameters[0].Type.ElementType?.Kind == VariableTypeKind.Target;

	// 받은 오브젝트들을 순서 그대로 몬스터로 바꾼다. 서버에서 찾지 못한 오브젝트(null)나 몬스터가 아닌 오브젝트는 null(대상 없음)로 둔다
	static List<Monster> ToMonsters(GameObject[] targets)
	{
		List<Monster> monsters = new List<Monster>(targets.Length);
		foreach (GameObject target in targets)
			monsters.Add(target != null ? target.GetComponent<Monster>() : null);
		return monsters;
	}

	// 슬롯의 원본. 네트워크를 켜지 않은 테스트에서는 서버로 받은 데이터가 없으므로 이 컴퓨터의 SlotLoadout에서 꺼낸다
	FunctionBlock GetSlotOriginal(int slot)
	{
		if (slot < 0 || slot >= SlotLoadout.SlotCount) return null;
		if (IsOffline) return loadout.TryGetFunction(slot, out FunctionBlock function) ? function : null;
		return slotOriginals[slot];
	}

	// 작업을 시전한 슬롯 번호. 함수 안에서 비동기로 시전된 작업이나 다른 곳에서 시전한 작업은 -1
	int SlotOf(FunctionTask task)
	{
		if (task.CastSource == null) return -1;
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
			if (GetSlotOriginal(i) == task.CastSource) return i;
		foreach ((int retiredSlot, FunctionBlock original) in retiredOriginals)
			if (original == task.CastSource) return retiredSlot;
		return -1;
	}

	void OnExecutorTaskStarted(FunctionTask task)
	{
		if (IsOffline) TaskStarted?.Invoke(task.Id, SlotOf(task), task.Name);
		else if (isServer) RpcTaskStarted(task.Id, SlotOf(task), task.Name);
	}

	void OnExecutorTaskEnded(FunctionTask task)
	{
		if (IsOffline) TaskEnded?.Invoke(task.Id, task.EndReason);
		else if (isServer) RpcTaskEnded(task.Id, task.EndReason);
	}

	// 입력 요청은 그 작업을 시전한 사람에게만 보낸다. 고르는 일은 그 사람의 화면에서 하기 때문
	void OnExecutorInputRequested(FunctionTask task, PredefinedFunction inputFunction)
	{
		if (IsOffline) InputRequested?.Invoke(task.Id, inputFunction.Id, SlotOf(task));
		else if (isServer) TargetInputRequested(task.Id, inputFunction.Id, SlotOf(task));
	}

	void ReplyCastAccepted(int slot, int taskId)
	{
		if (IsOffline) CastAccepted?.Invoke(slot, taskId);
		else TargetCastAccepted(slot, taskId);
	}

	void ReplyCastRejected(int slot, CastRejection rejection)
	{
		if (IsOffline) CastRejected?.Invoke(slot, rejection);
		else TargetCastRejected(slot, rejection);
	}

	// 시전 결과는 시전한 사람에게만 보낸다. 다른 플레이어의 시전이 거부된 것은 내 화면에 보일 필요가 없기 때문
	[TargetRpc]
	void TargetCastAccepted(int slot, int taskId) => CastAccepted?.Invoke(slot, taskId);

	[TargetRpc]
	void TargetCastRejected(int slot, CastRejection rejection) => CastRejected?.Invoke(slot, rejection);

	// 작업의 시작·끝은 모든 화면에 보낸다. 다른 플레이어의 시전 모션도 보여야 하기 때문
	[ClientRpc]
	void RpcTaskStarted(int taskId, int slot, string functionName) => TaskStarted?.Invoke(taskId, slot, functionName);

	[ClientRpc]
	void RpcTaskEnded(int taskId, FunctionTaskEndReason reason) => TaskEnded?.Invoke(taskId, reason);

	[TargetRpc]
	void TargetInputRequested(int taskId, string inputFunctionId, int slot) => InputRequested?.Invoke(taskId, inputFunctionId, slot);

	// [서버] 스킬의 쿨타임이 시작되면, 끝나는 시각을 시전한 사람에게 알린다
	// 틱 수가 아니라 네트워크 시계(NetworkTime) 기준 시각으로 보낸다. 메시지가 늦게 도착해도 남은 시간이 서버와 맞게 하기 위함
	void OnExecutorCooldownStarted(string skillId, int ticks)
	{
		if (IsOffline || !isServer) return;
		TargetCooldownStarted(skillId, NetworkTime.time + ticks * executor.SecondsPerTick);
	}

	// [클라이언트] 네트워크 시계 기준 끝나는 시각을 이 컴퓨터의 Time.time 기준으로 바꿔 실행기에 넣는다
	// 호스트는 실행기가 직접 쿨타임을 세므로 넣지 않는다
	[TargetRpc]
	void TargetCooldownStarted(string skillId, double endNetworkTime)
	{
		if (isServer) return;
		executor.SetDisplayedCooldown(skillId, Time.time + (float)(endNetworkTime - NetworkTime.time));
	}
}
