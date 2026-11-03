using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 슬롯 키로 함수를 시전하는 컴포넌트. 함수의 대상 지정 방식에 따라 입력이 달라진다.
/// <para>
/// 대상 지정 방식 (노션 「C 캐릭터 - 1차」 7. 입력 방식, c-skills.md 「대상 지정 방식」)<br/>
/// - 단일(Target 파라미터 1개): 누르면 주변 몬스터가 후보가 되고 가장 가까운 몬스터가 선택된다 → 누른 채 ← →로 넘긴다 → 떼면 고른 몬스터를 target으로 넘겨 시전하고 CastRequested (대상 1명).<br/>
/// - 여러 명(Target list 파라미터): 단일처럼 고르면서 Space(MarkPressed)로 지금 고른 대상을 찍거나 뺀다 → 떼면 찍은 대상 모두로 CastRequested.
///   아무도 찍지 않고 떼면 지금 고른 대상 한 명. 찍을 수 있는 최대 인원은 쓸 수 있는 소환검 개수다.<br/>
/// - 범위(스킬 표의 Area): 누르는 순간 시전하고, 캐릭터 주변 Area Radius 안의 살아 있는 몬스터로 CastRequested.<br/>
/// - 대상 없음: 누르는 순간 시전하고 CastRequested (대상 0명).<br/>
/// - 시전은 직접 하지 않고 FunctionExecutorNetwork로 서버에 부탁한다 (#212, network.md). RequestCast(슬롯, 대상) → 답 CastAccepted / CastRejected. 로그·CastRequested·취소용 기록은 CastAccepted 뒤에.<br/>
/// - 대상을 고르는 동안에는 Player 액션 맵(이동·점프·대시·내려가기)을 비활성화한다. 그래야 ← →가 Move가 아니라 대상 넘기기로, Space가 점프가 아니라 찍기로 들어간다 (#59).<br/>
/// - 고를 대상이 필요한 함수는 주변에 후보가 없으면 시전하지 않는다.<br/>
/// - 경직·사망 중(PlayerHealth.CanAct가 false)에는 시전하지 않고, 고르던 대상도 취소한다.<br/>
/// - 슬롯의 함수는 SlotLoadout에서 꺼낸다 (#159). 블록코딩 함수는 누르는 순간 시전하고, 함수 안의 GetBug가 대상을 기다리면 그때 고른다.<br/>
/// - GetBug 입력: 그 함수를 시전한 슬롯 키를 누르고 있으면 ← →로 고르고 떼면 넣는다. 누르고 있지 않으면 가장 가까운 몬스터(없으면 대상 없음)를 바로 넣는다.<br/>
/// - 취소 키(C): 슬롯으로 시전해 아직 끝나지 않은 작업 중 가장 최근 것의 슬롯을 RequestCancel로 끊어 달라고 부탁한다.<br/>
/// - 급소공략 미니게임 (#161): 급소공략 슬롯(또는 GetBug 입력)으로 대상을 고르는 중 Space(MarkPressed)를 누르면 미니게임 시작, 한 번 더 누르면 판정. 시작하면 대상이 고정된다. 결과는 급소공략이 시작할 때 가져간다.
/// </para>
/// <para>
/// 아직 하지 않는 일<br/>
/// - 여러 명 선택의 대상 목록을 넘기는 일: RequestCast가 대상 하나만 받아 CastRequested 이벤트와 로그까지만 한다 (소환검·전장 휩쓸기 이슈).
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class SlotCaster : NetworkBehaviour
{
	const int SlotCount = SlotLoadout.SlotCount;

	/// <summary>슬롯을 눌렀을 때의 입력 방식</summary>
	public enum CastMode
	{
		None,        // 대상 없음: 누르는 순간 시전
		Single,      // 한 명 고르기
		MultiSelect, // 여러 명 찍기
		Area,        // 범위: 누르는 순간 주변 몬스터 모두
	}

	[Header("대상 찾기")]
	[Tooltip("대상 후보를 찾는 거리 (유닛)")]
	[SerializeField, Min(0f)] float targetRange = 8f;
	[Tooltip("대상 후보가 될 레이어 (Monster)")]
	[SerializeField] LayerMask targetLayers;

	[Header("연결")]
	[SerializeField] PlayerInputReader input;
	[Tooltip("서버에 시전·취소·입력을 부탁하는 곳 (#212). 비워 두면 같은 오브젝트에서 찾는다")]
	[SerializeField] FunctionExecutorNetwork net;
	[Tooltip("슬롯 장착 내용. 비워 두면 같은 오브젝트에서 찾는다")]
	[SerializeField] SlotLoadout loadout;
	[Tooltip("급소공략 미니게임. 비워 두면 같은 오브젝트에서 찾는다. 없으면 미니게임 없이 시전")]
	[SerializeField] VitalStrikeMinigame minigame;
	[Tooltip("비워 두면 경직·사망 검사를 하지 않는다")]
	[SerializeField] PlayerHealth health;

	// 서버가 받아 준 슬롯 작업 (시전한 순서). 작업 번호로 기억한다 (#212). 끝나면(TaskEnded) 뺀다. 취소 키가 가장 최근 것을 고른다
	struct SlotCast
	{
		public int Slot;
		public int TaskId;
	}
	readonly List<SlotCast> slotCasts = new List<SlotCast>();

	// 서버에 시전을 부탁하고 답을 기다리는 중인 슬롯. 답(CastAccepted)이 오면 로그와 CastRequested를 낸다
	struct PendingCast
	{
		public FunctionBlock Function;
		public List<GameObject> Targets;
		public string Note;
	}
	readonly Dictionary<int, PendingCast> pending = new Dictionary<int, PendingCast>();

	// GetBug 입력을 위해 대상을 고르는 중이면 그 작업 번호. 아니면 -1
	int inputTaskId = -1;

	// 이번 대상 고르기에서 Space로 급소공략 미니게임을 할 수 있는지, 이미 시작했는지 (시작하면 대상 고정)
	bool minigameAllowed;
	bool minigameStarted;

	// 대상을 고르는 중인 슬롯과 후보. 고르는 중이 아니면 selectingSlot = -1
	int selectingSlot = -1;
	CastMode selectingMode;
	readonly List<GameObject> candidates = new List<GameObject>();
	int selectedIndex = -1;
	// 여러 명 선택에서 찍은 대상 (찍은 순서)
	readonly List<GameObject> marked = new List<GameObject>();

	// 이 컴포넌트가 Player 액션 맵을 비활성화했는지. 내가 끈 것만 다시 켠다 (다른 이유로 꺼진 맵을 켜지 않기 위해)
	bool playerMapDisabledByMe;

	/// <summary>지금 대상을 고르고 있는 슬롯 번호 (0~7). 고르는 중이 아니면 -1.</summary>
	public int SelectingSlot => selectingSlot;

	/// <summary>지금 선택된 대상. 고르는 중이 아니거나 후보가 없으면 null. 대상 표시 연출에서 읽는다.</summary>
	public GameObject CurrentTarget => selectedIndex >= 0 && selectedIndex < candidates.Count ? candidates[selectedIndex] : null;

	/// <summary>여러 명 선택에서 지금까지 찍은 대상 (찍은 순서). 찍은 대상 표시 연출에서 읽는다.</summary>
	public IReadOnlyList<GameObject> MarkedTargets => marked;

	/// <summary>
	/// [표시] 선택된 대상이 바뀌었을 때 불린다 (고르기 시작, ← →, 취소·시전으로 끝남). 끝나면 null을 넘긴다.
	/// </summary>
	public event Action<GameObject> TargetChanged;

	/// <summary>
	/// [표시] 여러 명 선택에서 찍은 대상 목록이 바뀌었을 때 불린다 (찍기, 빼기, 끝남). 끝나면 빈 목록을 넘긴다.
	/// </summary>
	public event Action<IReadOnlyList<GameObject>> MarkedChanged;

	/// <summary>
	/// [로직] 시전을 요청했을 때 불린다. (슬롯 번호, 함수, 대상 목록).<br/>
	/// 단일은 1명, 여러 명은 찍은 대상(없으면 고른 1명), 범위는 범위 안의 몬스터 모두, 대상 없음은 빈 목록.<br/>
	/// 단일과 대상 없음·범위는 실제로 시전한 뒤 불린다. 여러 명은 list 값이 아직 없어 시전하지 않고 이 이벤트까지만 한다.<br/>
	/// 블록코딩 함수는 누르는 순간 시전하고 대상 목록은 빈 목록이다 (대상은 함수 안의 GetBug가 받는다).
	/// </summary>
	public event Action<int, FunctionBlock, IReadOnlyList<GameObject>> CastRequested;

	void Reset()
	{
		input = GetComponent<PlayerInputReader>();
		net = GetComponent<FunctionExecutorNetwork>();
		loadout = GetComponent<SlotLoadout>();
		minigame = GetComponent<VitalStrikeMinigame>();
		health = GetComponent<PlayerHealth>();
		targetLayers = LayerMask.GetMask("Monster");
	}

	void Awake()
	{
		if (net == null) net = GetComponent<FunctionExecutorNetwork>();
		if (loadout == null) loadout = GetComponent<SlotLoadout>();
		if (minigame == null) minigame = GetComponent<VitalStrikeMinigame>();
	}

	void OnEnable()
	{
		if (net == null) return;
		net.CastAccepted += OnCastAccepted;
		net.CastRejected += OnCastRejected;
		net.TaskEnded += OnTaskEnded;
		net.InputRequested += OnInputRequested;
	}

	void Update()
	{
		if (!isLocalPlayer) return;

		if (health != null && !health.CanAct)
		{
			CancelSelection();
			return;
		}

		if (input.CancelPressed) CancelLatestCast();

		for (int i = 0; i < SlotCount; i++)
		{
			if (input.SlotPressed(i)) OnSlotPressed(i);
		}

		if (selectingSlot < 0) return;

		if (input.SlotReleased(selectingSlot))
		{
			ConfirmSelection();
			return;
		}

		// 묶음이 꺼지는 등으로 떼는 순간을 놓쳤으면 취소한다
		if (input.HeldSlot != selectingSlot)
		{
			CancelSelection();
			return;
		}

		int step = input.TargetStep;
		if (step != 0) MoveSelection(step);

		if (selectingMode == CastMode.MultiSelect && input.MarkPressed) ToggleMark();
		else if (minigameAllowed && input.MarkPressed) OnMinigameKey();
	}

	void OnSlotPressed(int slot)
	{
		// 다른 슬롯으로 고르는 중이면 그 슬롯이 끝날 때까지 무시한다 (PlayerInputReader도 먼저 누른 슬롯 하나만 기록)
		if (selectingSlot >= 0) return;

		if (!TryGetSlotFunction(slot, out FunctionBlock slotFunction))
		{
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: 빈 슬롯");
			return;
		}

		// 블록코딩 함수: 파라미터로 대상을 넘길 수 없으므로 누르는 순간 시전한다. 대상은 함수 안의 GetBug가 받는다
		if (!(slotFunction is PredefinedFunction function))
		{
			RequestCast(slot, slotFunction, null, new List<GameObject>(), "시전 (블록 함수)");
			return;
		}

		CastMode mode = GetCastMode(function, out float areaRadius);
		switch (mode)
		{
			case CastMode.None:
				RequestCast(slot, function, null, new List<GameObject>(), "바로 시전 (대상 없음)");
				break;

			case CastMode.Area:
				List<GameObject> inArea = FindMonsters(areaRadius);
				RequestCast(slot, function, null, inArea, $"범위 시전 (반경 {areaRadius}, {inArea.Count}명)");
				break;

			default:
				BeginSelection(slot, mode);
				break;
		}
	}

	void BeginSelection(int slot, CastMode mode)
	{
		selectingSlot = slot;
		selectingMode = mode;
		SetPlayerMapBlocked(true);
		candidates.Clear();
		candidates.AddRange(FindMonsters(targetRange));
		// ← → 넘기는 순서와 화면 순서를 맞춘다
		candidates.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
		selectedIndex = NearestCandidateIndex();

		// 급소공략 슬롯이거나 GetBug 입력이면 Space로 미니게임을 할 수 있다. 이전에 남은 결과는 지운다
		minigameAllowed = minigame != null && mode == CastMode.Single && (inputTaskId >= 0 || IsVitalStrikeSlot(slot));
		minigameStarted = false;
		if (minigame != null) minigame.ClearResult();

		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: 대상 고르기 시작 ({(mode == CastMode.MultiSelect ? "여러 명" : "단일")}, 후보 {candidates.Count}명)");
		NotifyTargetChanged();
	}

	void MoveSelection(int step)
	{
		// 미니게임을 시작하면 대상이 고정된다
		if (minigameStarted) return;
		RemoveDeadCandidates();
		if (candidates.Count == 0) return;

		// 후보는 왼쪽부터 오른쪽 순서. 끝에서 넘기면 반대쪽 끝으로 돌아간다
		selectedIndex = ((selectedIndex + step) % candidates.Count + candidates.Count) % candidates.Count;
		NotifyTargetChanged();
	}

	// 급소공략 미니게임: 처음 누르면 시작, 다음에 누르면 판정. 판정이 끝난 뒤에는 다시 시작하지 않는다
	void OnMinigameKey()
	{
		if (!minigameStarted)
		{
			if (CurrentTarget == null) return;
			minigameStarted = true;
			minigame.Begin();
			Debug.Log($"[SlotCaster] 슬롯 {selectingSlot + 1}: 급소공략 미니게임 시작 (대상 {CurrentTarget.name} 고정)");
		}
		else if (minigame.IsRunning)
		{
			VitalStrikeResult result = minigame.Stop();
			Debug.Log($"[SlotCaster] 슬롯 {selectingSlot + 1}: 급소공략 미니게임 판정 {result}");
		}
	}

	bool IsVitalStrikeSlot(int slot) =>
		TryGetSlotFunction(slot, out FunctionBlock function) && function is PredefinedFunction predefined && predefined.Id == PredefinedFunctionIds.VitalStrike;

	// 지금 고른 대상을 찍는다. 이미 찍었으면 뺀다
	void ToggleMark()
	{
		RemoveDeadCandidates();
		GameObject target = CurrentTarget;
		if (target == null) return;

		if (marked.Remove(target))
		{
			Debug.Log($"[SlotCaster] {target.name} 찍기 취소 ({marked.Count}명)");
		}
		else
		{
			int max = MaxMarkCount();
			if (marked.Count >= max)
			{
				Debug.Log($"[SlotCaster] 더 찍을 수 없음 (최대 {max}명 = 쓸 수 있는 소환검 개수)");
				return;
			}
			marked.Add(target);
			Debug.Log($"[SlotCaster] {target.name} 찍음 ({marked.Count}명)");
		}
		MarkedChanged?.Invoke(marked);
	}

	void ConfirmSelection()
	{
		int slot = selectingSlot;
		CastMode mode = selectingMode;
		RemoveDeadCandidates();

		List<GameObject> targets = new List<GameObject>();
		if (mode == CastMode.MultiSelect && marked.Count > 0) targets.AddRange(marked);
		else if (CurrentTarget != null) targets.Add(CurrentTarget); // 단일, 또는 아무도 찍지 않은 여러 명 선택

		// 바늘이 도는 중에 떼면 Miss로 끝내고 그 결과를 서버에 먼저 보낸다 (#223). 서버의 급소공략은 서버에 받아 둔 결과를 읽는다
		if (minigameStarted && minigame != null) minigame.FinishAsMiss();
		EndSelection();

		// GetBug 입력: 고른 몬스터를 기다리는 작업에 넣는다. 후보가 없었으면 대상 없음을 넣는다
		if (inputTaskId >= 0)
		{
			int taskId = inputTaskId;
			inputTaskId = -1;
			ProvideBug(taskId, targets.Count > 0 ? targets[0] : null);
			return;
		}

		if (!TryGetSlotFunction(slot, out FunctionBlock slotFunction) || !(slotFunction is PredefinedFunction function)) return;

		if (targets.Count == 0)
		{
			if (minigame != null) minigame.Cancel();
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 대상이 없어서 시전하지 않음");
			return;
		}

		// 단일: 고른 몬스터를 대상으로 서버에 시전을 부탁한다. 죽었거나 없어진 대상은 서버의 실행기가 InvalidTarget으로 막는다
		// 여러 명: 대상 배열을 넘기는 방법이 아직 없어 부탁하지 않고 CastRequested까지만 한다 (소환검·전장 휩쓸기 이슈)
		string names = string.Join(", ", targets.ConvertAll(t => t.name));
		if (mode == CastMode.Single)
		{
			RequestCast(slot, function, targets[0], targets, $"→ {names} 시전");
			return;
		}
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} → {names} 시전 요청 (여러 명, 아직 시전 안 함)");
		CastRequested?.Invoke(slot, function, targets);
	}

	// 서버에 시전을 부탁한다 (#212). 로그, CastRequested, 취소용 기록은 답(CastAccepted)이 온 뒤에 한다
	void RequestCast(int slot, FunctionBlock function, GameObject target, List<GameObject> targets, string note)
	{
		if (net == null)
		{
			Debug.LogError($"[SlotCaster] {name}에 FunctionExecutorNetwork가 없어 시전을 부탁하지 못함", this);
			return;
		}
		pending[slot] = new PendingCast { Function = function, Targets = targets, Note = note };
		net.RequestCast(slot, target);
	}

	// [내 화면] 서버가 시전을 받아 줌
	void OnCastAccepted(int slot, int taskId)
	{
		slotCasts.Add(new SlotCast { Slot = slot, TaskId = taskId });
		if (!pending.TryGetValue(slot, out PendingCast cast)) return;
		pending.Remove(slot);
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {cast.Function.Name} {cast.Note} (작업 {taskId})");
		CastRequested?.Invoke(slot, cast.Function, cast.Targets);
	}

	// [내 화면] 서버가 시전을 거부함 (쿨타임, 메모리 부족, 서버에 그 슬롯의 함수 없음)
	void OnCastRejected(int slot, CastRejection reason)
	{
		string fn = pending.TryGetValue(slot, out PendingCast cast) ? cast.Function.Name : "";
		pending.Remove(slot);
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {fn} 시전 거부됨 ({reason})");
		// 급소공략을 거부당하면 남은 미니게임 결과를 다음 시전에 쓰지 않게 지운다
		if (minigame != null) minigame.ClearResult();
	}

	// [모든 화면] 작업이 끝남. 내 작업 기록에서 빼고, 그 작업의 GetBug 입력을 고르던 중이면 고르기도 끝낸다
	void OnTaskEnded(int taskId, FunctionTaskEndReason reason)
	{
		slotCasts.RemoveAll(c => c.TaskId == taskId);
		if (inputTaskId != taskId) return;
		Debug.Log($"[SlotCaster] 작업 {taskId}: 입력을 기다리던 작업이 끝나서 대상 고르기 끝 ({reason})");
		inputTaskId = -1;
		if (selectingSlot >= 0) EndSelection();
	}

	/// <summary>
	/// 취소 키(C): 슬롯으로 시전해 아직 끝나지 않은 작업 중 가장 최근 것의 슬롯을 끊어 달라고 서버에 부탁한다 (RequestCancel).<br/>
	/// 서버의 Cancel은 원본 단위지만, 같은 원본은 실행 중이면 다시 시전되지 않으므로 한 슬롯의 작업은 늘 하나다.
	/// </summary>
	void CancelLatestCast()
	{
		if (slotCasts.Count == 0)
		{
			Debug.Log("[SlotCaster] 취소: 진행 중인 슬롯 작업 없음");
			return;
		}
		SlotCast latest = slotCasts[slotCasts.Count - 1];
		Debug.Log($"[SlotCaster] 취소: 슬롯 {latest.Slot + 1} (작업 {latest.TaskId}) 끊기 요청");
		net.RequestCancel(latest.Slot);
	}

	// [내 화면] 내 작업이 입력 함수를 만나 입력을 기다린다. 대상 입력(GetBug)만 맡는다. slot은 그 작업을 시전한 슬롯(비동기 작업이면 -1)
	void OnInputRequested(int taskId, string inputFunctionId, int slot)
	{
		if (inputFunctionId != PredefinedFunctionIds.GetBug) return;

		// 그 작업을 시전한 슬롯 키를 아직 누르고 있고 다른 대상을 고르는 중이 아니면, 그 슬롯으로 고른다
		if (isLocalPlayer && slot >= 0 && selectingSlot < 0 && input.HeldSlot == slot && (health == null || health.CanAct))
		{
			inputTaskId = taskId;
			BeginSelection(slot, CastMode.Single);
			return;
		}

		// 누르고 있지 않으면(이미 뗐음, 함수 안에서 따로 시전된 작업 등) 가장 가까운 몬스터를 바로 넣는다
		List<GameObject> around = FindMonsters(targetRange);
		GameObject nearest = null;
		float best = float.MaxValue;
		foreach (GameObject monster in around)
		{
			float dist = (monster.transform.position - transform.position).sqrMagnitude;
			if (dist < best) { best = dist; nearest = monster; }
		}
		ProvideBug(taskId, nearest);
	}

	// 고른 몬스터를 서버에 보낸다. 작업이 그사이 끝났으면 서버가 버린다
	void ProvideBug(int taskId, GameObject target)
	{
		net.ProvideTarget(taskId, target);
		Debug.Log($"[SlotCaster] 작업 {taskId}: GetBug ← {(target != null ? target.name : "대상 없음")}");
	}

	void CancelSelection()
	{
		if (selectingSlot < 0) return;
		Debug.Log($"[SlotCaster] 슬롯 {selectingSlot + 1}: 대상 고르기 취소");
		if (minigameStarted) minigame.Cancel();
		EndSelection();

		// GetBug 입력을 고르다 취소되면(경직·사망 등) 작업이 계속 기다리지 않게 대상 없음을 넣는다
		if (inputTaskId >= 0)
		{
			int taskId = inputTaskId;
			inputTaskId = -1;
			ProvideBug(taskId, null);
		}
	}

	// 미니게임 결과는 지우지 않는다. 떼서 시전하면 급소공략이 시작할 때 가져간다 (진행 중이었으면 그때 Miss)
	void EndSelection()
	{
		SetPlayerMapBlocked(false);
		minigameAllowed = false;
		minigameStarted = false;
		selectingSlot = -1;
		candidates.Clear();
		selectedIndex = -1;
		bool hadMarks = marked.Count > 0;
		marked.Clear();
		NotifyTargetChanged();
		if (hadMarks) MarkedChanged?.Invoke(marked);
	}

	// 고르는 동안 Player 액션 맵을 비활성화한다. 다시 켤 때는 내가 끈 경우에만 켠다.
	// 지금은 Player 맵을 끄는 곳이 여기뿐이다. UI·공격 불가 지역 등 다른 이유가 생기면, 모든 이유가 풀렸을 때만 켜도록 함께 정해야 한다 (player-input.md)
	void SetPlayerMapBlocked(bool blocked)
	{
		if (blocked == playerMapDisabledByMe) return;
		input.SetPlayerMapEnabled(!blocked);
		playerMapDisabledByMe = blocked;
	}

	// 고르는 중에 꺼지거나 사라지면 꺼 둔 Player 맵을 남기지 않는다
	void OnDisable()
	{
		if (net != null)
		{
			net.CastAccepted -= OnCastAccepted;
			net.CastRejected -= OnCastRejected;
			net.TaskEnded -= OnTaskEnded;
			net.InputRequested -= OnInputRequested;
		}
		if (selectingSlot >= 0) CancelSelection();
		else SetPlayerMapBlocked(false);
	}

	void NotifyTargetChanged() => TargetChanged?.Invoke(CurrentTarget);

	// 찍을 수 있는 최대 인원 = 쓸 수 있는 소환검 개수
	// SummonedSwords(#99)의 사용 가능한 개수를 읽는다. 없으면 TempSwordState(이우림, 테스트용), 그것도 없으면 제한 없음
	int MaxMarkCount()
	{
		SummonedSwords summoned = GetComponent<SummonedSwords>();
		if (summoned != null) return summoned.ReadyCount;
		TempSwordState temp = GetComponent<TempSwordState>();
		return temp != null ? temp.SummonedSwordCount : int.MaxValue;
	}

	// 함수의 대상 지정 방식으로 입력 방식을 정한다. 범위는 스킬 표에만 있으므로 표를 먼저 보고, 표에 없는 함수(테스트 함수 등)는 파라미터로 정한다
	static CastMode GetCastMode(PredefinedFunction function, out float areaRadius)
	{
		areaRadius = 0f;
		if (function is DataDrivenSkill && SkillTable.TryGet(function.Id, out SkillData data) && data.Targeting == SkillTargeting.Area)
		{
			areaRadius = data.AreaRadius;
			return CastMode.Area;
		}

		foreach (ParameterData parameter in function.Parameters)
		{
			VariableType type = parameter.Type;
			if (type == null) continue;
			if (type.Kind == VariableTypeKind.Target) return CastMode.Single;
			if (type.ElementType != null && type.ElementType.Kind == VariableTypeKind.Target) return CastMode.MultiSelect;
		}
		return CastMode.None;
	}

	// 플레이어 주변 radius 안의 살아 있는 몬스터
	List<GameObject> FindMonsters(float radius)
	{
		List<GameObject> found = new List<GameObject>();
		// 내가 속한 장소 씬의 물리 세계에 묻는다. 서버는 씬마다 물리 세계가 따로 있어서 Physics2D로는 못 찾는다 (place-scenes.md). 씬이 바뀔 수 있어 저장하지 않고 매번 가져온다
		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(targetLayers);
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		gameObject.scene.GetPhysicsScene2D().OverlapCircle(transform.position, radius, filter, hits);
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead) continue;
			if (!found.Contains(monster.gameObject)) found.Add(monster.gameObject);
		}
		return found;
	}

	int NearestCandidateIndex()
	{
		int nearest = -1;
		float best = float.MaxValue;
		for (int i = 0; i < candidates.Count; i++)
		{
			float dist = (candidates[i].transform.position - transform.position).sqrMagnitude;
			if (dist < best) { best = dist; nearest = i; }
		}
		return nearest;
	}

	// 고르는 사이 죽었거나 사라진 후보를 뺀다. 선택된 대상이 빠지면 같은 자리(다음 후보)를 고른다
	void RemoveDeadCandidates()
	{
		GameObject selected = CurrentTarget;
		candidates.RemoveAll(IsGone);
		if (marked.RemoveAll(IsGone) > 0) MarkedChanged?.Invoke(marked);

		if (candidates.Count == 0) { selectedIndex = -1; return; }
		int stillThere = selected != null ? candidates.IndexOf(selected) : -1;
		selectedIndex = stillThere >= 0 ? stillThere : Mathf.Clamp(selectedIndex, 0, candidates.Count - 1);
	}

	static bool IsGone(GameObject target) => target == null || (target.TryGetComponent(out Monster monster) && monster.IsDead);

	// 슬롯에 장착한 함수(원본). 빈 슬롯이거나 실행할 수 없으면 false (SlotLoadout)
	bool TryGetSlotFunction(int slot, out FunctionBlock function)
	{
		function = null;
		return loadout != null && loadout.TryGetFunction(slot, out function);
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(transform.position, targetRange);
	}
}
