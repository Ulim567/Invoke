using System.Collections;
using UnityEngine;

/// <summary>
/// 상태 이상 테스트 (#97). 씬의 아무 오브젝트에 붙이고 Play. 몬스터가 2마리 이상 있어야 한다 (보스가 아닌 몬스터 우선).
/// 몬스터에 StatusEffects가 없으면 테스트 동안 붙인다. 콘솔에 「[StatusTest] … 통과/실패」가 찍힌다 (약 6초).
/// <para>
/// 몬스터 A: 출혈 → 5초 동안 1초마다 5 데미지(총 25), 끝나면 풀림<br/>
/// 몬스터 B: 기절 1초, 이속 감소 3초(배율 0.5), 실명 3초(2초에 다시 걸어 시간 새로 시작), 표식(같은 공격 2배)
/// </para>
/// 자동으로 돌기 때문에 평소에는 꺼 둔다.
/// </summary>
public class StatusEffectsTest : MonoBehaviour
{
	int failures;
	int running;

	IEnumerator Start()
	{
		yield return new WaitForSeconds(0.5f);
		Monster a = null, b = null;
		foreach (Monster m in FindObjectsByType<Monster>())
		{
			if (m.IsDead || m.IsBoss) continue;
			if (a == null) a = m; else if (b == null) b = m;
		}
		if (a == null || b == null)
		{
			Debug.LogError("[StatusTest] 보스가 아닌 몬스터 2마리가 필요함: 실패");
			yield break;
		}

		running = 2;
		StartCoroutine(Run(BleedTest(Ensure(a), a)));
		StartCoroutine(Run(OtherTest(Ensure(b), b)));
		while (running > 0) yield return null;

		if (failures == 0) Debug.Log("[StatusTest] 전체 통과");
		else Debug.LogError($"[StatusTest] 실패 {failures}개");
	}

	IEnumerator Run(IEnumerator test)
	{
		yield return test;
		running--;
	}

	static StatusEffects Ensure(Monster monster)
	{
		StatusEffects effects = monster.GetComponent<StatusEffects>();
		return effects != null ? effects : monster.gameObject.AddComponent<StatusEffects>();
	}

	IEnumerator BleedTest(StatusEffects effects, Monster monster)
	{
		int ticks = 0;
		effects.BleedTicked += _ => ticks++;
		int hp = monster.Hp;
		effects.Apply(StatusType.Bleed);
		Check("출혈 걸림", effects.Has(StatusType.Bleed));
		yield return new WaitForSeconds(0.5f);
		Check("출혈 0.5초: 아직 데미지 없음", monster.Hp == hp);
		yield return new WaitForSeconds(5.2f);
		int expected = StatusDefaults.BleedDamage * Mathf.RoundToInt(StatusDefaults.BleedDuration / StatusDefaults.BleedInterval);
		Check($"출혈 5초: 데미지 {hp - monster.Hp} (기대 {expected}, {ticks}번)", hp - monster.Hp == expected);
		Check("출혈 끝나면 풀림", !effects.Has(StatusType.Bleed));
	}

	IEnumerator OtherTest(StatusEffects effects, Monster monster)
	{
		// 표식: 같은 공격이 2배
		DamageRequest attack = new DamageRequest(10, 1f);
		int normal = DamageCalculator.Calculate(attack, DamageTarget.FromMonster(monster), 1f).Damage;
		effects.Apply(StatusType.Mark);
		int marked = DamageCalculator.Calculate(attack, DamageTarget.FromMonster(monster), 1f).Damage;
		Check($"표식: {normal} → {marked} (2배)", normal == 10 && marked == 20);

		effects.Apply(StatusType.Stun);
		effects.Apply(StatusType.Slow);
		effects.Apply(StatusType.Blind);
		Check("기절·이속 감소·실명 걸림", effects.IsStunned && effects.IsBlinded && Mathf.Approximately(effects.MoveSpeedMultiplier, StatusDefaults.SlowMultiplier));

		yield return new WaitForSeconds(1.2f);
		Check("기절 1초 뒤 풀림", !effects.IsStunned);
		Check("이속 감소는 아직 걸려 있음", Mathf.Approximately(effects.MoveSpeedMultiplier, StatusDefaults.SlowMultiplier));

		yield return new WaitForSeconds(0.8f); // 2초
		effects.Apply(StatusType.Blind);        // 다시 걸면 3초가 새로 시작 → 5초에 풀림
		Check("실명 다시 걸면 남은 시간이 새로 시작", effects.RemainingTime(StatusType.Blind) > StatusDefaults.BlindDuration - 0.1f);

		yield return new WaitForSeconds(1.2f); // 3.2초
		Check("이속 감소 3초 뒤 풀림 (배율 1)", Mathf.Approximately(effects.MoveSpeedMultiplier, 1f));
		Check("다시 건 실명은 3.2초에 아직 걸려 있음", effects.IsBlinded);

		yield return new WaitForSeconds(2f); // 5.2초
		Check("다시 건 실명은 5초 뒤 풀림", !effects.IsBlinded);
		Check("표식 5초 뒤 풀림", !effects.IsMarked);
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[StatusTest] {label}: 통과");
		else { failures++; Debug.LogError($"[StatusTest] {label}: 실패"); }
	}
}
