using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 슬롯 키로 함수를 시전하는 컴포넌트. 함수의 대상 지정 방식에 따라 입력이 달라진다.
/// <para>
/// 대상 지정 방식 (노션 「C 캐릭터 - 1차」 7. 입력 방식, c-skills.md 「대상 지정 방식」)<br/>
/// - 단일(Target 파라미터 1개): 누르면 주변 몬스터가 후보가 되고 가장 가까운 몬스터가 선택된다 → 누른 채 ← →로 넘긴다 → 떼면 CastRequested (대상 1명).<br/>
/// - 여러 명(Target list 파라미터): 단일처럼 고르면서 Space(MarkPressed)로 지금 고른 대상을 찍거나 뺀다 → 떼면 찍은 대상 모두로 CastRequested.
///   아무도 찍지 않고 떼면 지금 고른 대상 한 명. 찍을 수 있는 최대 인원은 쓸 수 있는 소환검 개수다.<br/>
/// - 범위(스킬 표의 Area): 누르는 순간 시전하고, 캐릭터 주변 Area Radius 안의 살아 있는 몬스터로 CastRequested.<br/>
/// - 대상 없음: 누르는 순간 FunctionExecutor.Cast로 시전하고 CastRequested (대상 0명).<br/>
/// - 대상을 고르는 동안에는 Player 액션 맵(이동·점프·대시·내려가기)을 비활성화한다. 그래야 ← →가 Move가 아니라 대상 넘기기로, Space가 점프가 아니라 찍기로 들어간다 (#59).<br/>
/// - 고를 대상이 필요한 함수는 주변에 후보가 없으면 시전하지 않는다.<br/>
/// - 경직·사망 중(PlayerHealth.CanAct가 false)에는 시전하지 않고, 고르던 대상도 취소한다.
/// </para>
/// <para>
/// 아직 하지 않는 일<br/>
/// - 고른 대상을 함수의 Target 파라미터에 넣는 일: 넘기는 방법을 이우림과 정하는 중이라 CastRequested 이벤트와 로그까지만 한다.<br/>
/// - 슬롯에 함수 장착: 「슬롯 8개에 함수 장착·해제하기」 이슈. 그 전까지는 Inspector의 Slot Function Ids에 고정 ID를 적어 둔다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class SlotCaster : NetworkBehaviour
{
	const int SlotCount = 8;

	/// <summary>슬롯을 눌렀을 때의 입력 방식</summary>
	public enum CastMode
	{
		None,        // 대상 없음: 누르는 순간 시전
		Single,      // 한 명 고르기
		MultiSelect, // 여러 명 찍기
		Area,        // 범위: 누르는 순간 주변 몬스터 모두
	}

	[Header("슬롯 (임시: 장착 이슈 전까지 고정 ID를 직접 적는다)")]
	[Tooltip("슬롯 1~8(Q W E R A S D F)에 넣을 사전 정의 함수의 고정 ID. 예: c.thrust, test.function_a. 비우면 빈 슬롯")]
	[SerializeField] string[] slotFunctionIds = new string[SlotCount];

	[Header("대상 찾기")]
	[Tooltip("대상 후보를 찾는 거리 (유닛)")]
	[SerializeField, Min(0f)] float targetRange = 8f;
	[Tooltip("대상 후보가 될 레이어 (Monster)")]
	[SerializeField] LayerMask targetLayers;

	[Header("연결")]
	[SerializeField] PlayerInputReader input;
	[SerializeField] FunctionExecutor executor;
	[Tooltip("비워 두면 경직·사망 검사를 하지 않는다")]
	[SerializeField] PlayerHealth health;

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
	/// 대상을 고르는 함수(단일, 여러 명)는 아직 실제로 Cast하지 않는다. 대상을 Target 파라미터에 넣는 방법이 정해지면 이 이벤트를 받는 쪽에서 Cast한다.
	/// </summary>
	public event Action<int, PredefinedFunction, IReadOnlyList<GameObject>> CastRequested;

	void Reset()
	{
		input = GetComponent<PlayerInputReader>();
		executor = GetComponent<FunctionExecutor>();
		health = GetComponent<PlayerHealth>();
		targetLayers = LayerMask.GetMask("Monster");
	}

	void Update()
	{
		if (!isLocalPlayer) return;

		if (health != null && !health.CanAct)
		{
			CancelSelection();
			return;
		}

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
	}

	void OnSlotPressed(int slot)
	{
		// 다른 슬롯으로 고르는 중이면 그 슬롯이 끝날 때까지 무시한다 (PlayerInputReader도 먼저 누른 슬롯 하나만 기록)
		if (selectingSlot >= 0) return;

		if (!TryGetSlotFunction(slot, out PredefinedFunction function))
		{
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: 빈 슬롯");
			return;
		}

		CastMode mode = GetCastMode(function, out float areaRadius);
		switch (mode)
		{
			case CastMode.None:
				executor.Cast(function);
				Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 바로 시전 (대상 없음)");
				CastRequested?.Invoke(slot, function, Array.Empty<GameObject>());
				break;

			case CastMode.Area:
				List<GameObject> inArea = FindMonsters(areaRadius);
				executor.Cast(function);
				Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 범위 시전 (반경 {areaRadius}, {inArea.Count}명)");
				CastRequested?.Invoke(slot, function, inArea);
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
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: 대상 고르기 시작 ({(mode == CastMode.MultiSelect ? "여러 명" : "단일")}, 후보 {candidates.Count}명)");
		NotifyTargetChanged();
	}

	void MoveSelection(int step)
	{
		RemoveDeadCandidates();
		if (candidates.Count == 0) return;

		// 후보는 왼쪽부터 오른쪽 순서. 끝에서 넘기면 반대쪽 끝으로 돌아간다
		selectedIndex = ((selectedIndex + step) % candidates.Count + candidates.Count) % candidates.Count;
		NotifyTargetChanged();
	}

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

		EndSelection();

		if (!TryGetSlotFunction(slot, out PredefinedFunction function)) return;

		if (targets.Count == 0)
		{
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 대상이 없어서 시전하지 않음");
			return;
		}

		// TODO(이우림과 정하는 중): 고른 대상을 function의 Target 파라미터에 넣어 Cast한다
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} → {string.Join(", ", targets.ConvertAll(t => t.name))} 시전 요청");
		CastRequested?.Invoke(slot, function, targets);
	}

	void CancelSelection()
	{
		if (selectingSlot < 0) return;
		Debug.Log($"[SlotCaster] 슬롯 {selectingSlot + 1}: 대상 고르기 취소");
		EndSelection();
	}

	void EndSelection()
	{
		SetPlayerMapBlocked(false);
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
		if (selectingSlot >= 0) EndSelection();
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
		Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, targetLayers);
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

	bool TryGetSlotFunction(int slot, out PredefinedFunction function)
	{
		function = null;
		if (slot < 0 || slot >= slotFunctionIds.Length) return false;

		string id = slotFunctionIds[slot];
		if (string.IsNullOrWhiteSpace(id)) return false;

		if (!PredefinedFunctionRegistry.TryGet(id, out function))
		{
			Debug.LogWarning($"[SlotCaster] 슬롯 {slot + 1}: 없는 함수 ID '{id}'");
			return false;
		}
		return true;
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(transform.position, targetRange);
	}
}
