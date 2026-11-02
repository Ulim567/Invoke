using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 찌르기 2 테스트 (#165). Test_JW의 Thrust2Test. 켜고 Play → Host (약 10초). 주변에 몬스터가 있어야 한다.
/// <para>
/// 1. 상처 공략: 출혈 없을 때 기본, 출혈일 때 2.0배<br/>
/// 2. 검 깊이찌르기 6번: 치명타가 난 것은 2.5배, 안 난 것은 기본 (치명타가 한 번도 안 나면 경고)<br/>
/// 3. 소환검 찌르기: 캐릭터는 안 움직이고 고른 몬스터가 기본 데미지로 맞음, 소환검이 돌아와 다시 사용 가능<br/>
/// 4. 소환검을 모두 해제한 뒤 소환검 찌르기: NoSummonedSwordError로 실패<br/>
/// 몬스터에 StatusEffects가 없으면 붙인다. 「[Thrust2Test] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class Thrust2SimTest : MonoBehaviour
{
	const string Tag = "[Thrust2Test]";

	int failed;
	FunctionExecutor executor;
	PlayerStats stats;
	readonly List<(string id, GameObject target, DamageResult result)> hits = new List<(string, GameObject, DamageResult)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		stats = player.GetComponent<PlayerStats>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		SummonedSwords swords = player.GetComponent<SummonedSwords>();
		if (executor == null || stats == null || effects == null || swords == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, PlayerStats, SkillEffectEvents, SummonedSwords가 있어야 함");
			yield break;
		}
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.GetComponent<StatusEffects>() == null) m.gameObject.AddComponent<StatusEffects>();
			m.GetComponent<StatusEffects>().ClearAll();
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target, result));

		// 1. 상처 공략
		Monster target = Nearest(player);
		StatusEffects status = target.GetComponent<StatusEffects>();
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.WoundExploit, target);
		int plain = hits.Count == 1 ? hits[0].result.Damage : -1;
		status.Apply(StatusType.Bleed);
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.WoundExploit, target);
		int bleeding = hits.Count == 1 ? hits[0].result.Damage : -1;
		status.ClearAll();
		int plainExpected = Expected(PredefinedFunctionIds.WoundExploit, target, 0f);
		int bleedExpected = Expected(PredefinedFunctionIds.WoundExploit, target, DamageBonus.BleedWoundExploit);
		Check($"상처 공략: 출혈 없음 {plain} = 기본 ({plainExpected})", plain == plainExpected);
		Check($"상처 공략: 출혈 {bleeding} = 2.0배 ({bleedExpected})", bleeding == bleedExpected && bleeding > plain);

		// 2. 검 깊이찌르기
		int crits = 0, normals = 0;
		bool consistent = true;
		for (int i = 0; i < 6; i++)
		{
			target = Nearest(player);
			hits.Clear();
			yield return Cast(PredefinedFunctionIds.DeepThrust, target);
			if (hits.Count != 1) { consistent = false; continue; }
			DamageResult r = hits[0].result;
			int baseDamage = Expected(PredefinedFunctionIds.DeepThrust, target, 0f);
			int critDamage = DamageCalculator.Calculate(
				new DamageRequest(stats.AttackPower, Multiplier(PredefinedFunctionIds.DeepThrust)) { CritChance = 1f, CritBonusRate = DamageBonus.CritDeepThrust },
				DamageTarget.FromMonster(target), 0f).Damage;
			if (r.IsCritical) { crits++; consistent &= r.Damage == critDamage; }
			else { normals++; consistent &= r.Damage == baseDamage; }
		}
		Check($"깊이찌르기: 치명타 {crits}번은 2.5배, 아닌 {normals}번은 기본", consistent);
		if (crits == 0) Debug.LogWarning($"{Tag} 깊이찌르기 6번 중 치명타가 한 번도 안 났음 (확률 {CDeepThrust.DeepCritChance}). 다시 돌려 보면 됨");

		// 3. 소환검 찌르기 (사용 가능한 소환검이 없으면 하나 소환)
		if (swords.ReadyCount == 0)
		{
			yield return Cast(PredefinedFunctionIds.SummonSword, null);
			yield return new WaitForSeconds(0.3f);
		}
		int readyBefore = swords.ReadyCount;
		target = Nearest(player);
		float startX = player.transform.position.x;
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.SummonedThrust, target);
		Check("소환검 찌르기: 캐릭터는 안 움직임", Mathf.Abs(player.transform.position.x - startX) < 0.05f);
		int summonedExpected = Expected(PredefinedFunctionIds.SummonedThrust, target, 0f);
		Check($"소환검 찌르기: 고른 몬스터({target.name})가 기본 데미지({summonedExpected})로 맞음",
			hits.Count == 1 && hits[0].target == target.gameObject && hits[0].result.Damage == summonedExpected, $"맞은 수 {hits.Count}");
		float until = Time.time + 2f;
		while (swords.ReadyCount < readyBefore && Time.time < until) yield return null;
		Check("소환검 찌르기: 소환검이 돌아와 다시 사용 가능", swords.ReadyCount == readyBefore, $"{readyBefore} → {swords.ReadyCount}");

		// 4. 소환검이 없을 때
		int guard = 10;
		while (swords.ReadyCount > 0 && guard-- > 0) yield return Cast(PredefinedFunctionIds.FreeSword, null);
		FunctionTask failTask = null;
		yield return Cast(PredefinedFunctionIds.SummonedThrust, Nearest(player), t => failTask = t);
		Check("소환검이 없으면 소환검 찌르기가 NoSummonedSwordError로 실패", failTask != null && failTask.IsFailed && failTask.Error is NoSummonedSwordError,
			failTask != null ? $"실제 {failTask.EndReason} {failTask.Error?.GetType().Name}" : "시전 안 됨");

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Cast(string id, Monster target, System.Action<FunctionTask> onEnd = null)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); yield break; }
		FunctionTask task = target != null ? executor.Cast(function, RuntimeValue.FromBug(target)) : executor.Cast(function);
		if (task == null) { Check($"{id} 시전", false); yield break; }
		float end = Time.time + 3f;
		while (!task.IsEnded && Time.time < end) yield return null;
		onEnd?.Invoke(task);
		yield return new WaitForSeconds(0.15f);
	}

	int Expected(string id, Monster target, float bonus) =>
		DamageCalculator.Calculate(new DamageRequest(stats.AttackPower, Multiplier(id)) { BonusRate = bonus }, DamageTarget.FromMonster(target)).Damage;

	static float Multiplier(string id) => SkillTable.TryGet(id, out SkillData data) ? data.DamageMultiplier : 1f;

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

	void Check(string label, bool ok, string detail = "")
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failed++;
			Debug.LogError($"{Tag} {label}: 실패 {detail}");
		}
	}
}
