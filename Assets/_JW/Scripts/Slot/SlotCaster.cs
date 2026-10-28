using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 슬롯 키로 함수를 시전하는 컴포넌트. 대상이 필요한 함수는 슬롯을 누른 채 ← →로 대상을 고르고, 떼면 시전을 요청한다.
/// <para>
/// 흐름 (노션 「슬롯 누른 채 대상 고르고 떼면 발동하기」)<br/>
/// - 대상 있음(Target 파라미터가 있는 함수): 누르면 주변 몬스터가 후보가 되고 가장 가까운 몬스터가 선택된다 → 누른 채 ← →로 넘긴다 → 떼면 CastRequested.<br/>
/// - 대상 없음(Target 파라미터가 없는 함수): 누르는 순간 FunctionExecutor.Cast로 시전하고 CastRequested를 부른다 (대상은 null).<br/>
/// - 슬롯을 누르고 있는 동안에는 PlayerInputReader.MoveX가 0이라 움직이지 않는다.<br/>
/// - 경직·사망 중(PlayerHealth.CanAct가 false)에는 시전하지 않고, 고르던 대상도 취소한다.
/// </para>
/// <para>
/// 아직 하지 않는 일<br/>
/// - 고른 대상을 함수의 Target 파라미터에 넣는 일: 넘기는 방법을 이우림과 정하는 중이라 CastRequested 이벤트와 로그까지만 한다.<br/>
/// - 슬롯에 함수 장착: 「슬롯 8개에 함수 장착·해제하기」 이슈. 그 전까지는 Inspector의 Slot Function Ids에 고정 ID를 적어 둔다.<br/>
/// - 여러 명 선택(Target list)·범위: 「여러 명 골라 발동하기와 범위로 발동하기」 이슈.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerInputReader))]
public class SlotCaster : NetworkBehaviour
{
	const int SlotCount = 8;

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
	readonly List<GameObject> candidates = new List<GameObject>();
	int selectedIndex = -1;

	/// <summary>지금 대상을 고르고 있는 슬롯 번호 (0~7). 고르는 중이 아니면 -1.</summary>
	public int SelectingSlot => selectingSlot;

	/// <summary>지금 선택된 대상. 고르는 중이 아니거나 후보가 없으면 null. 대상 표시 연출에서 읽는다.</summary>
	public GameObject CurrentTarget => selectedIndex >= 0 && selectedIndex < candidates.Count ? candidates[selectedIndex] : null;

	/// <summary>
	/// [표시] 선택된 대상이 바뀌었을 때 불린다 (고르기 시작, ← →, 취소·시전으로 끝남). 끝나면 null을 넘긴다.
	/// </summary>
	public event Action<GameObject> TargetChanged;

	/// <summary>
	/// [로직] 시전을 요청했을 때 불린다. (슬롯 번호, 함수, 대상). 대상 없는 함수면 대상은 null.<br/>
	/// 대상 있는 함수는 아직 실제로 Cast하지 않는다. 대상을 Target 파라미터에 넣는 방법이 정해지면 이 이벤트를 받는 쪽에서 Cast한다.
	/// </summary>
	public event Action<int, PredefinedFunction, GameObject> CastRequested;

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

		if (!NeedsTarget(function))
		{
			// 대상 없음: 누르는 순간 시전
			executor.Cast(function);
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 바로 시전 (대상 없음)");
			CastRequested?.Invoke(slot, function, null);
			return;
		}

		BeginSelection(slot);
	}

	void BeginSelection(int slot)
	{
		selectingSlot = slot;
		FindCandidates();
		selectedIndex = NearestCandidateIndex();
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: 대상 고르기 시작 (후보 {candidates.Count}명)");
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

	void ConfirmSelection()
	{
		int slot = selectingSlot;
		RemoveDeadCandidates();
		GameObject target = CurrentTarget;
		EndSelection();

		if (!TryGetSlotFunction(slot, out PredefinedFunction function)) return;

		if (target == null)
		{
			Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} 대상이 없어서 시전하지 않음");
			return;
		}

		// TODO(이우림과 정하는 중): 고른 대상을 function의 Target 파라미터에 넣어 Cast한다
		Debug.Log($"[SlotCaster] 슬롯 {slot + 1}: {function.Name} → {target.name} 시전 요청");
		CastRequested?.Invoke(slot, function, target);
	}

	void CancelSelection()
	{
		if (selectingSlot < 0) return;
		Debug.Log($"[SlotCaster] 슬롯 {selectingSlot + 1}: 대상 고르기 취소");
		EndSelection();
	}

	void EndSelection()
	{
		selectingSlot = -1;
		candidates.Clear();
		selectedIndex = -1;
		NotifyTargetChanged();
	}

	void NotifyTargetChanged() => TargetChanged?.Invoke(CurrentTarget);

	// 주변의 살아 있는 몬스터를 왼쪽부터 오른쪽 순서로 모은다 (← → 넘기는 순서와 화면 순서를 맞춤)
	void FindCandidates()
	{
		candidates.Clear();
		Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, targetRange, targetLayers);
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead) continue;
			if (!candidates.Contains(monster.gameObject)) candidates.Add(monster.gameObject);
		}
		candidates.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
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
		candidates.RemoveAll(c => c == null || (c.TryGetComponent(out Monster m) && m.IsDead));

		if (candidates.Count == 0) { selectedIndex = -1; return; }
		int stillThere = selected != null ? candidates.IndexOf(selected) : -1;
		selectedIndex = stillThere >= 0 ? stillThere : Mathf.Clamp(selectedIndex, 0, candidates.Count - 1);
	}

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

	// Target 파라미터가 있으면 대상을 골라야 하는 함수다 (docs/systems/predefined-function.md 「대상 지정 방식」)
	static bool NeedsTarget(PredefinedFunction function)
	{
		foreach (ParameterData parameter in function.Parameters)
		{
			if (parameter.Type != null && parameter.Type.Kind == VariableTypeKind.Target) return true;
		}
		return false;
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.cyan;
		Gizmos.DrawWireSphere(transform.position, targetRange);
	}
}
