using System.Collections;
using UnityEngine;

/// <summary>
/// 몬스터 체력 바 테스트 (#388). Test_JW 씬의 아무 오브젝트에 붙이고 Play → Host (몬스터가 Host 뒤에 나와서 최대 15초 기다림). 콘솔에 「[MonsterHpBarTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 체력이 가득이면 바가 안 보임<br/>
/// 2. 최대 체력의 약 30%를 깎으면 바가 보이고 빨간 채움 = ceil(20 × 남은 비율)<br/>
/// 3. 맞은 직후 흰 잔상이 빨간 채움보다 길고, 1초 뒤 같아짐<br/>
/// 4. 바가 몸 위쪽에 있음<br/>
/// 5. 죽으면 바가 사라짐
/// </para>
/// 보스·허수아비가 아닌 몬스터 하나를 골라 MonsterHealthBarView가 없으면 붙인다. 마지막에 그 몬스터를 죽이므로 평소에는 꺼 둔다.
/// </summary>
public class MonsterHealthBarSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		Monster monster = null;
		float giveUp = Time.time + 15f;
		while (monster == null && Time.time < giveUp)
		{
			foreach (Monster m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
				if (!m.IsBoss && !m.IsInvulnerable && !m.IsDead && m.Hp == m.MaxHp && m.MaxHp >= 10) { monster = m; break; }
			if (monster == null) yield return new WaitForSeconds(0.5f);
		}
		if (monster == null) { Check("일반 몬스터를 찾음 (Host를 눌렀는지 확인)", false); yield break; }
		Debug.Log($"[MonsterHpBarTest] 대상: {monster.name} (체력 {monster.Hp}/{monster.MaxHp})");

		MonsterHealthBarView view = monster.GetComponent<MonsterHealthBarView>();
		if (view == null) view = monster.gameObject.AddComponent<MonsterHealthBarView>();
		yield return null;
		yield return null;

		// 1. 가득이면 숨김
		Check("체력 가득 → 바 안 보임", !view.IsShown);

		// 2. 30% 깎기
		monster.TakeDamage(Mathf.Max(1, Mathf.RoundToInt(monster.MaxHp * 0.3f)));
		yield return null;
		yield return null;
		int expected = Mathf.Clamp(Mathf.CeilToInt(20f * monster.Hp / monster.MaxHp), 1, 20);
		Check($"맞으면 바가 보임", view.IsShown);
		Check($"빨간 채움 {view.FillPixels}px = 예상 {expected}px", view.FillPixels == expected);

		// 3. 잔상
		Check($"맞은 직후 잔상 {view.TrailPixels}px > 채움 {view.FillPixels}px", view.TrailPixels > view.FillPixels);
		yield return new WaitForSeconds(1f);
		Check($"1초 뒤 잔상 {view.TrailPixels}px = 채움 {view.FillPixels}px", view.TrailPixels == view.FillPixels);

		// 4. 위치
		MonsterHealthBar bar = monster.GetComponentInChildren<MonsterHealthBar>();
		Collider2D body = monster.GetComponentInChildren<Collider2D>();
		Check("바가 몸 위쪽에 있음", bar != null && body != null && bar.transform.position.y > body.bounds.max.y);

		// 5. 죽으면 숨김
		monster.TakeDamage(monster.Hp);
		yield return null;
		Check("죽으면 바가 사라짐", !view.IsShown);

		if (failures == 0) Debug.Log("[MonsterHpBarTest] 전체 통과");
		else Debug.LogError($"[MonsterHpBarTest] 실패 {failures}개");
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[MonsterHpBarTest] {label}: 통과");
		else { failures++; Debug.LogError($"[MonsterHpBarTest] {label}: 실패"); }
	}
}
