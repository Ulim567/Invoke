using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 던지기 테스트 (#167). Test_JW의 ThrowTest. 켜고 Play → Host (약 10초). 주변에 몬스터가 2마리 이상 있어야 한다. 몬스터 하나를 처형한다.
/// <para>
/// 1. 소환검 던지기: 사거리 안 몬스터가 맞고 소환검이 몬스터에 꽂혀 따라다님 <br/>
/// 2. 검 던지기: 가장 가까운 몬스터가 사거리 안이면 맞고 데미지, 검이 떨어짐 (사거리 밖이면 빗나가 안 맞음)<br/>
/// 3. 검 돌아오기 두 번: 먼저 던진 소환검이 먼저 곁으로 오며 오는 길(꽂혀 있던 몬스터)에서 데미지, 그다음 물리 검이 손으로<br/>
/// 4. 처형: 체력 2% 이하로 만든 몬스터에게 검 던지기가 맞으면 처형되어 죽음<br/>
/// 「[ThrowTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class ThrowSkillSimTest : MonoBehaviour
{
	const string Tag = "[ThrowTest]";

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
		PhysicalSword physical = player.GetComponent<PhysicalSword>();
		SummonedSwords swords = player.GetComponent<SummonedSwords>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (executor == null || physical == null || swords == null || effects == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, PhysicalSword, SummonedSwords, SkillEffectEvents가 있어야 함");
			yield break;
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target, result));
		if (physical.State != SwordState.InHand) physical.Recall();

		// 1. 소환검 던지기
		if (swords.ReadyCount == 0) { yield return Cast(PredefinedFunctionIds.SummonSword, null); yield return new WaitForSeconds(0.3f); }
		swords.TryGetFirstReady(out SummonedSword first);
		Monster summonTarget = Nearest(player);
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.SummonedThrow, summonTarget);
		yield return WaitUntil(() => first == null || first.State == SummonedSwordState.Dropped, 2f);
		Check($"소환검 던지기: {summonTarget.name}이 맞음", hits.Count == 1 && hits[0].target == summonTarget.gameObject && hits[0].result.Damage == Expected(PredefinedFunctionIds.SummonedThrow, summonTarget));
		Check("소환검 던지기: 몬스터에 꽂힘", first != null && first.StuckTo == summonTarget);
		if (first != null && first.StuckTo != summonTarget)
			Debug.LogWarning($"{Tag} 소환검이 멈춘 곳 {(Vector2)first.transform.position} (상태 {first.State}), 대상 {summonTarget.name} 가운데 {Center(summonTarget.gameObject)}, 캐릭터 {(Vector2)player.transform.position}");

		// 2. 검 던지기
		Monster near = Nearest(player);
		bool inRange = Vector2.Distance(Center(player), Center(near.gameObject)) <= CSwordThrow.HitRange;
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.SwordThrow, near);
		yield return WaitUntil(() => physical.State == SwordState.OnGround, 2f);
		if (inRange)
		{
			Check($"검 던지기: 사거리 안 {near.name}이 맞음, 데미지 {Expected(PredefinedFunctionIds.SwordThrow, near)}",
				hits.Count == 1 && hits[0].target == near.gameObject && hits[0].result.Damage == Expected(PredefinedFunctionIds.SwordThrow, near));
			Check("검 던지기: 검이 맞은 자리 근처에 떨어짐", physical.Dropped != null && Mathf.Abs(physical.Dropped.transform.position.x - near.transform.position.x) < 1.5f);
		}
		else
		{
			Check("검 던지기: 사거리 밖이면 빗나감", hits.Count == 0);
			Debug.LogWarning($"{Tag} 가장 가까운 몬스터가 사거리 {CSwordThrow.HitRange} 밖이라 명중은 확인 못 함");
		}

		// 3. 검 돌아오기 두 번 (소환검을 먼저 던졌으므로 소환검부터)
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.SwordReturn, null);
		yield return WaitUntil(() => first.State == SummonedSwordState.Ready, 2f);
		Check("검 돌아오기 1: 먼저 던진 소환검이 곁으로", first.State == SummonedSwordState.Ready && physical.State == SwordState.OnGround);
		Check($"검 돌아오기 1: 오는 길의 몬스터가 데미지 (맞은 수 {hits.Count})", hits.Exists(h => h.target == summonTarget.gameObject));
		yield return Cast(PredefinedFunctionIds.SwordReturn, null);
		yield return WaitUntil(() => physical.State == SwordState.InHand, 2f);
		Check("검 돌아오기 2: 물리 검이 손으로", physical.State == SwordState.InHand);

		// 4. 처형
		Monster victim = Nearest(player);
		if (Vector2.Distance(Center(player), Center(victim.gameObject)) <= CSwordThrow.HitRange)
		{
			victim.GetComponent<IDamageable>().TakeDamage(victim.Hp - 1);
			hits.Clear();
			yield return Cast(PredefinedFunctionIds.SwordThrow, victim);
			yield return WaitUntil(() => hits.Count > 0, 2f);
			Check($"처형: 체력 {victim.MaxHp}의 2% 이하 몬스터가 처형되어 죽음", hits.Count == 1 && hits[0].result.IsExecution && victim.IsDead);
		}
		else Debug.LogWarning($"{Tag} 사거리 안에 몬스터가 없어 처형은 확인 못 함");

		physical.Recall();
		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	IEnumerator Cast(string id, Monster target)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); yield break; }
		FunctionTask task = target != null ? executor.Cast(function, RuntimeValue.FromBug(target)) : executor.Cast(function);
		if (task == null) { Check($"{id} 시전", false); yield break; }
		float end = Time.time + 3f;
		while (!task.IsEnded && Time.time < end) yield return null;
		if (task.EndReason != FunctionTaskEndReason.Completed) Check($"{id}: Completed (실제 {task.EndReason})", false);
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
	}

	int Expected(string id, Monster target)
	{
		float multiplier = SkillTable.TryGet(id, out SkillData data) ? data.DamageMultiplier : 1f;
		return DamageCalculator.Calculate(new DamageRequest(stats.AttackPower, multiplier), DamageTarget.FromMonster(target)).Damage;
	}

	static Vector2 Center(GameObject obj)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		return col != null ? (Vector2)col.bounds.center : (Vector2)obj.transform.position;
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
