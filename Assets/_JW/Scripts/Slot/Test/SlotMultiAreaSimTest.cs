using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 여러 명 선택과 범위 시전 테스트 (#95). 씬의 아무 오브젝트에 붙이고 Play → Host, Game 창을 한 번 클릭(가짜 키 입력이 들어가게).
/// 콘솔에 「[MultiTest] … 통과/실패」가 찍힌다.
/// <para>
/// 시작할 때 검 소환(c.summon_sword)을 2번 시전해 소환검 2개를 만들고, 끝나면 해제(c.free_sword)로 지운다.<br/>
/// 시작할 때 슬롯 3(E)에 여러 명 선택 스킬(c.battlefield_sweep), 슬롯 4(R)에 범위 스킬(c.wide_slash)을 넣는다 (SlotLoadout). 주변에 몬스터가 2마리 이상 있어야 한다.<br/>
/// 1. 두 명 찍고 떼기 → 찍은 두 명<br/>
/// 2. 아무도 안 찍고 떼기 → 지금 고른 한 명<br/>
/// 3. 찍었다가 다시 찍으면 빠짐<br/>
/// 4. 소환검 1개일 때 두 번째 찍기는 막힘 (찍을 수 있는 최대 인원 = 사용 가능한 소환검 수)<br/>
/// 5. 범위 → 범위 안의 몬스터 모두, 범위 밖은 없음
/// </para>
/// 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class SlotMultiAreaSimTest : MonoBehaviour
{
	[SerializeField, Min(0.05f)] float stepDelay = 0.15f;

	int failures;
	List<GameObject> lastTargets;
	FunctionExecutor executor;

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		SlotCaster caster = NetworkClient.localPlayer.GetComponent<SlotCaster>();
		executor = caster.GetComponent<FunctionExecutor>();
		SlotLoadout loadout = caster.GetComponent<SlotLoadout>();
		loadout.EquipSkill(2, PredefinedFunctionIds.BattlefieldSweep);
		loadout.EquipSkill(3, PredefinedFunctionIds.WideSlash);
		caster.CastRequested += (slot, f, targets) => lastTargets = new List<GameObject>(targets);

		// 여러 명 선택은 사용 가능한 소환검 수만큼 찍을 수 있으므로 2개 소환해 둔다
		yield return Cast(PredefinedFunctionIds.SummonSword);
		yield return Cast(PredefinedFunctionIds.SummonSword);

		// 1. 두 명 찍고 떼기
		lastTargets = null;
		yield return Press(Key.E);
		GameObject first = caster.CurrentTarget;
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		yield return Press(Key.E, Key.RightArrow);
		yield return Press(Key.E);
		GameObject second = caster.CurrentTarget;
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		Check("두 명 찍음", caster.MarkedTargets.Count == 2);
		yield return Press();
		Check("두 명 찍고 떼면 찍은 두 명에게 발동", lastTargets != null && lastTargets.Count == 2
			&& lastTargets.Contains(first) && lastTargets.Contains(second) && first != second);
		Check("끝나면 찍은 목록 비움", caster.MarkedTargets.Count == 0);

		// 2. 아무도 안 찍고 떼기
		lastTargets = null;
		yield return Press(Key.E);
		GameObject current = caster.CurrentTarget;
		yield return Press();
		Check("안 찍고 떼면 지금 고른 한 명", lastTargets != null && lastTargets.Count == 1 && lastTargets[0] == current);

		// 3. 다시 찍으면 빠짐
		yield return Press(Key.E);
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		Check("같은 대상을 다시 찍으면 빠짐", caster.MarkedTargets.Count == 0);
		yield return Press();

		// 4. 소환검 1개면 한 명까지만
		yield return Cast(PredefinedFunctionIds.FreeSword);
		lastTargets = null;
		yield return Press(Key.E);
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		yield return Press(Key.E, Key.RightArrow);
		yield return Press(Key.E);
		yield return Press(Key.E, Key.Space);
		yield return Press(Key.E);
		Check("소환검 1개면 한 명까지만 찍힘", caster.MarkedTargets.Count == 1);
		yield return Press();
		Check("소환검 1개: 한 명에게 발동", lastTargets != null && lastTargets.Count == 1);
		yield return Cast(PredefinedFunctionIds.FreeSword);

		// 5. 범위
		lastTargets = null;
		yield return Press(Key.R);
		yield return Press();
		SkillTable.TryGet(PredefinedFunctionIds.WideSlash, out SkillData wide);
		float radius = wide != null ? wide.AreaRadius : 0f;
		List<GameObject> expected = new List<GameObject>();
		foreach (Monster monster in FindObjectsByType<Monster>())
		{
			if (monster.IsDead) continue;
			Collider2D col = monster.GetComponentInChildren<Collider2D>();
			Vector2 closest = col != null ? col.ClosestPoint(caster.transform.position) : (Vector2)monster.transform.position;
			if (Vector2.Distance(closest, caster.transform.position) <= radius) expected.Add(monster.gameObject);
		}
		bool same = lastTargets != null && lastTargets.Count == expected.Count && expected.TrueForAll(lastTargets.Contains);
		Check($"범위(반경 {radius}): {lastTargets?.Count ?? -1}명 (기대 {expected.Count}명)", same);

		if (failures == 0) Debug.Log("[MultiTest] 전체 통과");
		else Debug.LogError($"[MultiTest] 실패 {failures}개");
	}

	IEnumerator Press(params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(stepDelay);
	}

	// 스킬을 시전하고 끝날 때까지 기다린다
	IEnumerator Cast(string id)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); yield break; }
		FunctionTask task = executor.Cast(function);
		if (task == null) { Check($"{id} 시전", false); yield break; }
		float end = Time.time + 10f;
		while (!task.IsEnded && Time.time < end) yield return null;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[MultiTest] {label}: 통과");
		else { failures++; Debug.LogError($"[MultiTest] {label}: 실패"); }
	}
}
