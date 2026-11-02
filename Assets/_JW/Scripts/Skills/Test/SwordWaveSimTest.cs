using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 검 휘두르기·검 크게베기 테스트 (#163). Test_JW의 WaveTest. 켜고 Play → Host (약 4초). 주변에 몬스터가 2마리 이상 한 줄로 있어야 한다.
/// <para>
/// 1. 검 휘두르기를 가장 가까운 몬스터에게: 그 몬스터 한 명만 맞음, 데미지 = 공격력 × 표 배율, 날아가는 동안 heap 사용, 끝나면 heap이 원래대로, 검기가 사라짐<br/>
/// 2. 검 크게베기: 바라보는 방향 사거리 안의 몬스터가 모두 맞음(밖은 안 맞음), 맞은 몬스터마다 출혈, 데미지 = 공격력 × 0.5, 끝나면 heap이 원래대로<br/>
/// 몬스터에 StatusEffects가 없으면 붙인다. 「[WaveTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class SwordWaveSimTest : MonoBehaviour
{
	const string Tag = "[WaveTest]";

	int failed;
	readonly List<(GameObject target, int damage)> hits = new List<(GameObject, int)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		FunctionExecutor executor = player.GetComponent<FunctionExecutor>();
		ProcessMemory memory = player.GetComponent<ProcessMemory>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		PlayerStats stats = player.GetComponent<PlayerStats>();
		PlayerMovement movement = player.GetComponent<PlayerMovement>();
		if (executor == null || memory == null || effects == null || player.GetComponent<SwordWaveLauncher>() == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, ProcessMemory, SkillEffectEvents, SwordWaveLauncher가 있어야 함");
			yield break;
		}
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.GetComponent<StatusEffects>() == null) m.gameObject.AddComponent<StatusEffects>();
		}
		effects.Hit += (id, target, point, result) => hits.Add((target, result.Damage));
		// 검기를 날린 순간(Malloc 뒤)의 heap 사용량
		int heapAtFire = -1;
		effects.Fired += (id, origin, dir) => heapAtFire = memory.HeapUsed;

		// 1. 검 휘두르기
		Monster nearest = Nearest(player);
		int heapBefore = memory.HeapUsed;
		hits.Clear();
		FunctionTask task = Cast(executor, PredefinedFunctionIds.SwordSlash, RuntimeValue.FromBug(nearest));
		yield return WaitEnd(task);
		Check("휘두르기: 날아가는 동안 heap 사용", heapAtFire > heapBefore, $"{heapBefore} → {heapAtFire}");
		int expected = Expected(stats, PredefinedFunctionIds.SwordSlash, nearest);
		Check($"휘두르기: 고른 몬스터({nearest.name}) 한 명만 맞음", hits.Count == 1 && hits[0].target == nearest.gameObject, $"맞은 수 {hits.Count}");
		Check($"휘두르기: 데미지 = 공격력 × 배율 ({expected})", hits.Count == 1 && hits[0].damage == expected, hits.Count > 0 ? $"실제 {hits[0].damage}" : "");
		Check("휘두르기: 끝나면 heap이 원래대로", memory.HeapUsed == heapBefore, $"{heapBefore} → {memory.HeapUsed}");
		Check("휘두르기: 검기가 사라짐", FindObjectsByType<SwordWave>().Length == 0);
		yield return new WaitForSeconds(0.3f);

		// 2. 검 크게베기 (몬스터 쪽을 보고)
		foreach (StatusEffects s in FindObjectsByType<StatusEffects>()) s.ClearAll();
		Monster first = Nearest(player);
		if (movement != null) movement.FaceTowards(first.transform.position.x);
		heapBefore = memory.HeapUsed;
		// 사거리 안(몸 가운데부터 사거리 + 검기 앞쪽 반 칸)에 있는 몬스터. 이만큼 맞아야 한다
		int facing = movement != null ? movement.Facing : 1;
		float originX = player.GetComponent<Collider2D>() != null ? player.GetComponent<Collider2D>().bounds.center.x : player.transform.position.x;
		List<GameObject> inRange = new List<GameObject>();
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead) continue;
			Collider2D col = m.GetComponentInChildren<Collider2D>();
			float near = (facing > 0 ? col.bounds.min.x : -col.bounds.max.x) - originX * facing;
			if (near >= -0.5f && near <= CWideSlash.WaveRange + 0.5f) inRange.Add(m.gameObject);
		}
		hits.Clear();
		task = Cast(executor, PredefinedFunctionIds.WideSlash);
		yield return WaitEnd(task);
		bool allBleeding = hits.Count > 0 && hits.TrueForAll(h => h.target.GetComponent<StatusEffects>().Has(StatusType.Bleed));
		bool damageOk = hits.Count > 0 && hits.TrueForAll(h => h.damage == Expected(stats, PredefinedFunctionIds.WideSlash, h.target.GetComponent<Monster>()));
		bool sameSet = hits.Count == inRange.Count && inRange.TrueForAll(t => hits.Exists(h => h.target == t));
		Check($"크게베기: 사거리 안 몬스터 {inRange.Count}명 모두 관통해서 맞음", inRange.Count >= 1 && sameSet, $"맞은 수 {hits.Count}");
		if (inRange.Count < 2) Debug.LogWarning($"{Tag} 사거리 {CWideSlash.WaveRange} 안에 몬스터가 {inRange.Count}명뿐이라 관통은 확인 못 함. 몬스터를 캐릭터 가까이 일렬로 두면 확인됨");
		Check("크게베기: 맞은 몬스터 모두 출혈", allBleeding);
		Check("크게베기: 데미지 = 공격력 × 0.5", damageOk);
		Check("크게베기: 끝나면 heap이 원래대로", memory.HeapUsed == heapBefore, $"{heapBefore} → {memory.HeapUsed}");
		Check("크게베기: 검기가 사라짐", FindObjectsByType<SwordWave>().Length == 0);

		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	FunctionTask Cast(FunctionExecutor executor, string id, params RuntimeValue[] args)
	{
		if (!PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction function)) { Check($"{id} 등록", false); return null; }
		FunctionTask task = executor.Cast(function, args);
		if (task == null) Check($"{id} 시전", false);
		return task;
	}

	static IEnumerator WaitEnd(FunctionTask task)
	{
		float end = Time.time + 3f;
		while (task != null && !task.IsEnded && Time.time < end) yield return null;
		yield return new WaitForFixedUpdate();
	}

	static int Expected(PlayerStats stats, string id, Monster monster)
	{
		float multiplier = SkillTable.TryGet(id, out SkillData data) ? data.DamageMultiplier : 1f;
		return DamageCalculator.Calculate(new DamageRequest(stats.AttackPower, multiplier), DamageTarget.FromMonster(monster)).Damage;
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
