using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// 찌르기 스킬 1 테스트 (#152). 씬의 아무 오브젝트에 붙이고 Play → Host, Game 창을 한 번 클릭(가짜 키 입력이 들어가게).
/// 콘솔에 「[ThrustTest] … 통과/실패」가 찍힌다. 주변에 몬스터가 2마리 이상 일렬로 있어야 한다.
/// <para>
/// 1. 검 찌르기를 사거리(2.5칸) 밖의 가장 가까운 몬스터에게 → 앞까지 돌진하고, 그 몬스터가 맞는다 (데미지 = 공격력 × 표 배율). 캐릭터 시작 위치가 몬스터와 사거리보다 멀어야 한다<br/>
/// 2. 검 집중찌르기: 출혈 없을 때와 출혈일 때 데미지 비교 (+50%)<br/>
/// 3. 꼬챙이: 첫 대상 뒤 3칸 안의 몬스터가 모두 맞는다<br/>
/// 4. 맨손이면 실패, 맞는 대상 없음<br/>
/// 5. 슬롯 1(Q)에 검 찌르기: 누른 채 떼면 고른 몬스터가 맞는다 (SlotCaster가 target을 넘김, #133)<br/>
/// 6. 연출 event: Started → Fired → Hit → Ended 순서
/// </para>
/// 몬스터에 StatusEffects가 없으면 테스트 동안 붙였다가 뗀다. 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class ThrustSkillSimTest : MonoBehaviour
{
	int failures;
	FunctionExecutor executor;
	SkillEffectEvents effects;
	readonly List<(GameObject target, int damage)> hits = new List<(GameObject, int)>();
	readonly List<string> order = new List<string>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		effects = player.GetComponent<SkillEffectEvents>();
		PlayerStats stats = player.GetComponent<PlayerStats>();
		if (effects == null) { Check("Player에 SkillEffectEvents가 있음", false); yield break; }
		effects.Started += id => order.Add("Started");
		effects.Fired += (id, o, d) => order.Add("Fired");
		effects.Hit += (id, t, p, r) => { order.Add("Hit"); hits.Add((t, r.Damage)); };
		effects.Ended += (id, done) => order.Add(done ? "Ended" : "EndedCancel");

		// 1. 검 찌르기 + 돌진 (가장 가까운 몬스터. 더 먼 몬스터는 앞의 몬스터 몸에 막혀 닿지 못함)
		Monster far = NearestMonster(player);
		float startX = player.transform.position.x;
		int expected = DamageCalculator.Calculate(new DamageRequest(stats.AttackPower, Multiplier(PredefinedFunctionIds.Thrust)), DamageTarget.FromMonster(far)).Damage;
		hits.Clear(); order.Clear();
		yield return Cast(PredefinedFunctionIds.Thrust, far, FunctionTaskEndReason.Completed);
		Check($"검 찌르기: 고른 몬스터({far.name})가 맞음", hits.Count == 1 && hits[0].target == far.gameObject);
		Check($"검 찌르기 데미지 = 공격력 × 배율 ({expected})", hits.Count == 1 && hits[0].damage == expected);
		Check("사거리 밖이면 앞까지 돌진", Mathf.Abs(player.transform.position.x - startX) > 0.5f);
		Check("연출 event 순서: Started → Fired → Hit → Ended", string.Join(",", order) == "Started,Fired,Hit,Ended");
		yield return new WaitForSeconds(0.3f);

		// 2. 집중찌르기: 출혈 없을 때 / 출혈일 때
		Monster near = NearestMonster(player);
		StatusEffects status = near.GetComponent<StatusEffects>();
		bool added = status == null;
		if (added) status = near.gameObject.AddComponent<StatusEffects>();
		status.ClearAll();
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.FocusedThrust, near, FunctionTaskEndReason.Completed);
		int plain = hits.Count == 1 ? hits[0].damage : -1;
		status.Apply(StatusType.Bleed);
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.FocusedThrust, near, FunctionTaskEndReason.Completed);
		int bleeding = hits.Count == 1 ? hits[0].damage : -1;
		int bleedExpected = DamageCalculator.Calculate(new DamageRequest(stats.AttackPower, Multiplier(PredefinedFunctionIds.FocusedThrust)) { BonusRate = DamageBonus.BleedFocusedThrust }, DamageTarget.FromMonster(near)).Damage;
		Check($"집중찌르기: 출혈 {bleeding} = 1.5배 ({bleedExpected}), 출혈 없음 {plain}", plain > 0 && bleeding == bleedExpected && bleeding > plain);
		status.ClearAll();
		if (added) Destroy(status);
		yield return new WaitForSeconds(0.3f);

		// 3. 꼬챙이: 첫 대상 뒤 3칸
		Monster first = NearestMonster(player);
		int dir = first.transform.position.x >= player.transform.position.x ? 1 : -1;
		List<Monster> line = new List<Monster>();
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead) continue;
			float dx = (m.transform.position.x - first.transform.position.x) * dir;
			if (dx >= -0.01f && dx <= CSkewer.PierceLength) line.Add(m);
		}
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.Skewer, first, FunctionTaskEndReason.Completed);
		bool allHit = hits.Count == line.Count && line.TrueForAll(m => hits.Exists(h => h.target == m.gameObject));
		Check($"꼬챙이: 일직선 {line.Count}명 모두 맞음 (맞은 수 {hits.Count})", allHit && line.Count >= 2);
		yield return new WaitForSeconds(0.3f);

		// 4. 맨손
		PhysicalSword sword = player.GetComponent<PhysicalSword>();
		if (sword != null && sword.Throw(null))
		{
			hits.Clear();
			yield return Cast(PredefinedFunctionIds.Thrust, NearestMonster(player), FunctionTaskEndReason.Failed);
			Check("맨손이면 검 찌르기 실패, 맞는 대상 없음", hits.Count == 0);
			sword.Recall();
		}
		yield return new WaitForSeconds(0.3f);

		// 5. 슬롯 1(Q)으로 시전 → 고른 몬스터가 맞음
		SlotCaster caster = player.GetComponent<SlotCaster>();
		hits.Clear();
		yield return Press(Key.Q);
		GameObject chosen = caster.CurrentTarget;
		yield return Press(Key.Q);
		yield return Press();
		float end = Time.time + 2f;
		while (hits.Count == 0 && Time.time < end) yield return null;
		Check($"슬롯 Q: 고른 대상({(chosen != null ? chosen.name : "없음")})에게 맞음", chosen != null && hits.Count == 1 && hits[0].target == chosen);

		if (failures == 0) Debug.Log("[ThrustTest] 전체 통과");
		else Debug.LogError($"[ThrustTest] 실패 {failures}개");
	}

	IEnumerator Cast(string id, Monster target, FunctionTaskEndReason expected)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); yield break; }
		FunctionTask task = executor.Cast(function, RuntimeValue.FromBug(target));
		if (task == null) { Check($"{id} 시전", false); yield break; }
		float end = Time.time + 5f;
		while (!task.IsEnded && Time.time < end) yield return null;
		if (task.EndReason != expected) Check($"{id}: {expected} (실제 {task.EndReason})", false);
	}

	static float Multiplier(string id) => SkillTable.TryGet(id, out SkillData data) ? data.DamageMultiplier : 1f;

	static Monster NearestMonster(GameObject from) => Pick(from, true);

	static Monster Pick(GameObject from, bool nearest)
	{
		Monster picked = null;
		float best = nearest ? float.MaxValue : -1f;
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead) continue;
			float d = Mathf.Abs(m.transform.position.x - from.transform.position.x);
			if (nearest ? d < best : d > best) { best = d; picked = m; }
		}
		return picked;
	}

	static IEnumerator Press(params Key[] keys)
	{
		InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
		yield return new WaitForSeconds(0.15f);
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[ThrustTest] {label}: 통과");
		else { failures++; Debug.LogError($"[ThrustTest] {label}: 실패"); }
	}
}
