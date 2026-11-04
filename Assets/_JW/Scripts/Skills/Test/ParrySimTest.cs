using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 패링 테스트 (#245). Test_JW의 ParryTest. Inspector에 Bullet Prefab(MonsterBullet), Parryable Attack(Loop_Shot), Unparryable Attack(Packet_Drop)을 넣고 켜서 Play → Host (약 4초). 주변에 몬스터가 있어야 한다.
/// <para>
/// 몬스터 투사체가 플레이어에 닿았을 때 김도훈 코드가 부를 ParryGuard.TryParry를 테스트가 직접 부른다. 투사체는 가장 가까운 몬스터 자리에서 플레이어 쪽으로 아주 느리게 날린다(닿지 않음).<br/>
/// 1. 패링 안 했을 때: 잡지 못함<br/>
/// 2. 패링 판정 시간: 패링 불가 공격은 못 잡고, 패링 가능 공격은 잡음. 한 번 잡으면 닫힘<br/>
/// 3. 반격: 투사체가 날아온 쪽(몬스터)으로 검기가 날아가 그 몬스터가 맞음, 끝나면 heap이 원래대로<br/>
/// 4. 판정 시간(0.3초)이 지나면 못 잡음<br/>
/// 「[ParryTest] 전체 통과」면 통과. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class ParrySimTest : MonoBehaviour
{
	const string Tag = "[ParryTest]";

	[SerializeField] MonsterProjectile bulletPrefab;
	[SerializeField] MonsterAttackData parryableAttack;
	[SerializeField] MonsterAttackData unparryableAttack;

	int failed;
	FunctionExecutor executor;
	readonly List<(string id, GameObject target)> hits = new List<(string, GameObject)>();
	readonly List<(string id, Vector2 dir)> fired = new List<(string, Vector2)>();

	IEnumerator Start()
	{
		while (NetworkClient.localPlayer == null) yield return null;
		yield return new WaitForSeconds(1.5f);

		GameObject player = NetworkClient.localPlayer.gameObject;
		executor = player.GetComponent<FunctionExecutor>();
		ParryGuard guard = player.GetComponent<ParryGuard>();
		ProcessMemory memory = player.GetComponent<ProcessMemory>();
		SkillEffectEvents effects = player.GetComponent<SkillEffectEvents>();
		if (executor == null || guard == null || memory == null || effects == null || bulletPrefab == null || parryableAttack == null || unparryableAttack == null)
		{
			Debug.LogError($"{Tag} 플레이어에 FunctionExecutor, ParryGuard, ProcessMemory, SkillEffectEvents가 있어야 하고, Inspector에 Bullet Prefab, Parryable Attack, Unparryable Attack을 넣어야 함");
			yield break;
		}
		effects.Hit += (id, target, point, result) => hits.Add((id, target));
		effects.Fired += (id, origin, dir) => fired.Add((id, dir));
		PhysicalSword physical = player.GetComponent<PhysicalSword>();
		if (physical != null && physical.IsUnarmed) physical.Recall();

		Monster shooter = Nearest(player);
		if (shooter == null) { Debug.LogError($"{Tag} 살아 있는 몬스터가 없음"); yield break; }
		Vector2 from = Center(shooter.gameObject);
		Vector2 toPlayer = (Center(player) - from).normalized;
		MonsterProjectile bullet = Instantiate(bulletPrefab, from, Quaternion.identity);

		// 1. 패링 안 했을 때
		bullet.Launch(parryableAttack, toPlayer * 0.1f);
		Check("패링 안 하면 못 잡음", !guard.TryParry(bullet));

		// 2. 판정 시간 안
		int heapBefore = memory.HeapUsed;
		hits.Clear();
		fired.Clear();
		FunctionTask parry = CastTask();
		// 스킬은 다음 틱에 시작해서 판정 시간을 연다. 열릴 때까지 기다린다
		yield return WaitUntil(() => guard.IsOpen, 1f);
		Check("패링을 쓰면 판정 시간이 열림", guard.IsOpen);
		bullet.Launch(unparryableAttack, toPlayer * 0.1f);
		Check("패링 불가 공격은 못 잡음", !guard.TryParry(bullet));
		bullet.Launch(parryableAttack, toPlayer * 0.1f);
		Check("패링 가능 공격은 잡음", guard.TryParry(bullet));
		Check("한 번 잡으면 닫힘", !guard.IsOpen && !guard.TryParry(bullet));

		// 3. 반격
		yield return WaitUntil(() => hits.Exists(h => h.id == PredefinedFunctionIds.Parry), 1.5f);
		(string id, Vector2 dir) shot = fired.Find(f => f.id == PredefinedFunctionIds.Parry);
		Check("반격: 날아온 쪽(몬스터 쪽)으로 검기", shot.id != null && Vector2.Dot(shot.dir.normalized, -toPlayer) > 0.9f);
		Check($"반격: {shooter.name}이 맞음", hits.Exists(h => h.id == PredefinedFunctionIds.Parry && h.target == shooter.gameObject));
		yield return WaitUntil(() => parry == null || parry.IsEnded, 3f);
		Check($"패링: Completed (실제 {parry?.EndReason})", parry != null && parry.EndReason == FunctionTaskEndReason.Completed);
		Check($"반격: 끝나면 heap이 원래대로 ({heapBefore} → {memory.HeapUsed})", memory.HeapUsed == heapBefore);

		// 4. 판정 시간이 지나면
		parry = CastTask();
		yield return WaitUntil(() => guard.IsOpen, 1f);
		yield return new WaitForSeconds(CParry.WindowSeconds + 0.1f);
		Check("판정 시간이 지나면 못 잡음", !guard.TryParry(bullet));
		yield return WaitUntil(() => parry == null || parry.IsEnded, 3f);

		if (bullet != null) Destroy(bullet.gameObject);
		if (failed == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} {failed}개 실패");
	}

	FunctionTask CastTask()
	{
		if (!PredefinedFunctionRegistry.TryGet(PredefinedFunctionIds.Parry, out PredefinedFunction function)) { Check("패링 등록", false); return null; }
		FunctionTask task = executor.Cast(function);
		if (task == null) Check("패링 시전", false);
		return task;
	}

	static IEnumerator WaitUntil(System.Func<bool> done, float timeout)
	{
		float end = Time.time + timeout;
		while (!done() && Time.time < end) yield return null;
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
