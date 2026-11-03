using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 휘두르기 2 테스트 (#172). Test_JW의 Slash2Test. 켜고 Play → Host (약 12초). 주변에 몬스터가 있어야 한다.
/// <para>
/// 시작할 때 사용 가능한 소환검이 3개가 되도록 소환한다.<br/>
/// 1. 소환검 휘두르기: 날린 검기 수(소환검 수, heap이 모자라면 그보다 적게)만큼 대상이 맞음, 끝나면 heap이 원래대로<br/>
/// 2. 상처 가르기: 출혈 중인 몬스터를 맞히면 다음 출혈 틱 데미지가 2배, 출혈이 아닌 몬스터는 그대로<br/>
/// 3. 검 다중베기: 소환검 수만큼 대상이 베이고, 검이 모두 돌아옴<br/>
/// 몬스터에 StatusEffects가 없으면 붙인다. 「[Slash2Test] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class Slash2SimTest : MonoBehaviour
{
	const string Tag = "[Slash2Test]";

	int failed;
	FunctionExecutor executor;
	readonly List<(string id, GameObject target, int damage)> hits = new List<(string, GameObject, int)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		ProcessMemory memory = player.GetComponent<ProcessMemory>();
		SummonedSwords swords = player.GetComponent<SummonedSwords>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (executor == null || memory == null || swords == null || effects == null || player.GetComponent<SwordWaveLauncher>() == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, ProcessMemory, SummonedSwords, SkillEffectEvents, SwordWaveLauncher가 있어야 함");
			yield break;
		}
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.GetComponent<StatusEffects>() == null) m.gameObject.AddComponent<StatusEffects>();
			m.GetComponent<StatusEffects>().ClearAll();
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target, result.Damage));

		int guard = 6;
		while (swords.ReadyCount < 3 && swords.CanSummon && guard-- > 0) yield return Cast(PredefinedFunctionIds.SummonSword, null);
		yield return new WaitForSeconds(0.3f);
		int ready = swords.ReadyCount;
		Check($"소환검 준비 ({ready}개)", ready >= 1);

		// 1. 소환검 휘두르기
		Monster target = Nearest(player);
		int heapBefore = memory.HeapUsed;
		hits.Clear();
		// 검기도 하나당 heap 1칸이라, heap이 모자라면 소환검 수보다 적게 날린다. 실제로 날린 수(Fired)와 비교한다 (#220)
		int fired = 0;
		System.Action<string, Vector2, Vector2> onFired = (id, from, dir) => { if (id == PredefinedFunctionIds.SummonedSlash) fired++; };
		effects.Fired += onFired;
		yield return Cast(PredefinedFunctionIds.SummonedSlash, target);
		effects.Fired -= onFired;
		yield return new WaitForFixedUpdate();
		int slashHits = hits.FindAll(h => h.id == PredefinedFunctionIds.SummonedSlash && h.target == target.gameObject).Count;
		Check($"소환검 휘두르기: 검기 {fired}개(소환검 {ready}개)가 {target.name}을 맞힘 (맞은 수 {slashHits})", fired >= 1 && fired <= ready && slashHits == fired);
		Check($"소환검 휘두르기: 끝나면 heap이 원래대로 ({heapBefore} → {memory.HeapUsed})", memory.HeapUsed == heapBefore);
		Check("소환검 휘두르기: 소환검은 제자리 (사용 가능 그대로)", swords.ReadyCount == ready);

		// 2. 상처 가르기
		target = Nearest(player);
		StatusEffects status = target.GetComponent<StatusEffects>();
		int lastTick = -1;
		status.BleedTicked += d => lastTick = d;
		status.Apply(StatusType.Bleed);
		yield return WaitUntil(() => lastTick > 0, 1.5f);
		int baseTick = lastTick;
		yield return Cast(PredefinedFunctionIds.WoundSlash, target);
		Check("상처 가르기: 출혈 중인 대상의 출혈 배율 +100%", Mathf.Approximately(status.BleedBonusRate, DamageBonus.BleedWoundSlash));
		lastTick = -1;
		yield return WaitUntil(() => lastTick > 0, 1.5f);
		Check($"상처 가르기: 다음 출혈 틱 {lastTick} = 2배 ({baseTick * 2})", baseTick > 0 && lastTick == baseTick * 2);
		status.ClearAll();
		Check("상처 가르기: 출혈이 풀리면 배율도 없어짐", status.BleedBonusRate == 0f);
		yield return Cast(PredefinedFunctionIds.WoundSlash, target);
		Check("상처 가르기: 출혈이 아닌 대상은 배율 없음", status.BleedBonusRate == 0f);

		// 3. 검 다중베기
		ready = swords.ReadyCount;
		target = Nearest(player);
		hits.Clear();
		yield return Cast(PredefinedFunctionIds.MultiSlash, target);
		yield return WaitUntil(() => swords.ReadyCount == ready, 2f);
		int multiHits = hits.FindAll(h => h.id == PredefinedFunctionIds.MultiSlash && h.target == target.gameObject).Count;
		Check($"검 다중베기: 소환검 {ready}개가 {target.name}을 한 번씩 벰 (맞은 수 {multiHits})", multiHits == ready);
		Check("검 다중베기: 검이 모두 돌아옴", swords.ReadyCount == ready);

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
		if (task.EndReason != FunctionTaskEndReason.Completed) Check($"{id}: Completed (실제 {task.EndReason} {task.Error?.GetType().Name})", false);
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
