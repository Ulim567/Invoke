using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 표식 남기기·검무 테스트 (#243). Test_JW의 MarkDanceTest. 켜고 Play → Host (약 8초). 주변에 몬스터가 있어야 한다.
/// <para>
/// 시작할 때 소환검을 모두 해제하고 하나만 소환한다. 몬스터에 StatusEffects가 없으면 붙인다.<br/>
/// 1. 소환검 1개로 표식 남기기 → NoSummonedSwordError<br/>
/// 2. 소환검 2개로 표식 남기기 → 검 2개가 각각 데미지(공격력 × 표 배율), 대상에 표식 + 기절, 검이 모두 돌아옴<br/>
/// 3. 검무 → 대상이 소환검 수 이상 맞고 출혈, 도는 동안 검이 검무 중 상태, 끝나면 검이 모두 돌아옴<br/>
/// 「[MarkDanceTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class MarkDanceSimTest : MonoBehaviour
{
	const string Tag = "[MarkDanceTest]";

	int failed;
	FunctionExecutor executor;
	PlayerStats stats;
	readonly List<(string id, GameObject target, int damage)> hits = new List<(string, GameObject, int)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		stats = player.GetComponent<PlayerStats>();
		SummonedSwords swords = player.GetComponent<SummonedSwords>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (executor == null || swords == null || effects == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, SummonedSwords, SkillEffectEvents가 있어야 함");
			yield break;
		}
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.GetComponent<StatusEffects>() == null) m.gameObject.AddComponent<StatusEffects>();
			m.GetComponent<StatusEffects>().ClearAll();
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target, result.Damage));

		// 소환검을 하나만 남긴다
		int guard = 8;
		while (swords.Swords.Count > 0 && guard-- > 0) yield return Cast(PredefinedFunctionIds.FreeSword, null, FunctionTaskEndReason.Completed);
		yield return Cast(PredefinedFunctionIds.SummonSword, null, FunctionTaskEndReason.Completed);
		Monster target = Nearest(player);
		if (target == null) { Debug.LogError($"{Tag} 살아 있는 몬스터가 없음"); yield break; }
		StatusEffects status = target.GetComponent<StatusEffects>();

		// 1. 소환검 1개
		FunctionTask one = CastTask(PredefinedFunctionIds.Mark, target);
		yield return WaitUntil(() => one == null || one.IsEnded, 3f);
		Check($"표식 남기기: 소환검 1개면 NoSummonedSwordError (실제 {one?.EndReason} {one?.Error?.GetType().Name})",
			one != null && one.IsFailed && one.Error is NoSummonedSwordError);

		// 2. 소환검 2개
		yield return Cast(PredefinedFunctionIds.SummonSword, null, FunctionTaskEndReason.Completed);
		yield return new WaitForSeconds(0.3f);
		int expected = Expected(PredefinedFunctionIds.Mark, target);
		hits.Clear();
		FunctionTask mark = CastTask(PredefinedFunctionIds.Mark, target);
		// 기절은 1초라 스킬이 끝나기 전에 풀릴 수 있어서, 두 번 맞은 직후에 본다
		yield return WaitUntil(() => hits.FindAll(h => h.id == PredefinedFunctionIds.Mark).Count >= 2, 2f);
		yield return null;
		List<(string id, GameObject target, int damage)> markHits = hits.FindAll(h => h.id == PredefinedFunctionIds.Mark && h.target == target.gameObject);
		Check($"표식 남기기: 검 2개가 각각 데미지 {expected} (맞은 수 {markHits.Count})", markHits.Count == 2 && markHits.TrueForAll(h => h.damage == expected));
		Check("표식 남기기: 대상에 표식", status.IsMarked);
		Check("표식 남기기: 대상에 기절", status.IsStunned);
		yield return WaitUntil(() => mark == null || mark.IsEnded, 3f);
		Check($"표식 남기기: Completed (실제 {mark?.EndReason})", mark != null && mark.EndReason == FunctionTaskEndReason.Completed);
		yield return WaitUntil(() => swords.ReadyCount == 2, 2f);
		Check("표식 남기기: 검이 모두 돌아옴", swords.ReadyCount == 2);
		status.ClearAll();

		// 3. 검무
		int ready = swords.ReadyCount;
		hits.Clear();
		bool sawDancing = false;
		FunctionTask dance = CastTask(PredefinedFunctionIds.SwordDance, target);
		float end = Time.time + 3f;
		while (dance != null && !dance.IsEnded && Time.time < end)
		{
			foreach (SummonedSword s in swords.Swords) if (s != null && s.State == SummonedSwordState.Dancing) sawDancing = true;
			yield return null;
		}
		Check($"검무: Completed (실제 {dance?.EndReason})", dance != null && dance.EndReason == FunctionTaskEndReason.Completed);
		int danceHits = hits.FindAll(h => h.id == PredefinedFunctionIds.SwordDance && h.target == target.gameObject).Count;
		Check("검무: 도는 동안 검이 검무 중 상태", sawDancing);
		Check($"검무: 소환검 {ready}개 이상 맞음 (맞은 수 {danceHits})", danceHits >= ready);
		Check("검무: 대상이 출혈", status.Has(StatusType.Bleed));
		yield return WaitUntil(() => swords.ReadyCount == ready, 2f);
		Check("검무: 끝나면 검이 모두 돌아옴", swords.ReadyCount == ready);
		status.ClearAll();

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	FunctionTask CastTask(string id, Monster target)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = target != null ? executor.Cast(function, RuntimeValue.FromBug(target)) : executor.Cast(function);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	IEnumerator Cast(string id, Monster target, FunctionTaskEndReason expected)
	{
		FunctionTask task = CastTask(id, target);
		if (task == null) yield break;
		yield return WaitUntil(() => task.IsEnded, 3f);
		if (task.EndReason != expected) Check($"{id}: {expected} (실제 {task.EndReason} {task.Error?.GetType().Name})", false);
	}

	int Expected(string id, Monster target)
	{
		float multiplier = SkillTable.TryGet(id, out SkillData data) ? data.DamageMultiplier : 1f;
		return DamageCalculator.Calculate(new DamageRequest(stats != null ? stats.AttackPower : 0, multiplier), DamageTarget.FromMonster(target)).Damage;
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
	}

	static Monster Nearest(GameObject from)
	{
		Monster picked = null;
		float best = float.MaxValue;
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead) continue;
			float d = Mathf.Abs(m.transform.position.x - from.transform.position.x);
			if (d < best) { best = d; picked = m; }
		}
		return picked;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패");
		}
	}
}
