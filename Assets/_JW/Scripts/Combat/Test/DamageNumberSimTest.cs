using System.Collections;
using UnityEngine;

/// <summary>
/// 픽셀 데미지 숫자 테스트 (#442). Test_JW 씬의 아무 오브젝트에 붙이고 Play → Host (몬스터가 Host 뒤에 나와서 최대 15초 기다림). 콘솔에 「[DmgNumTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. Spawn(123) → 숫자 123, 세 자리, 일반 그림<br/>
/// 2. 처음에 크게 시작해서(팝) 줄어듦<br/>
/// 3. 위로 튀었다가 1초 안에 사라짐<br/>
/// 4. 표식을 걸고 Spawn → 표식 맞힘(치명타) 그림, 일반보다 큼<br/>
/// 5. 큰 데미지는 작은 데미지보다 큼
/// </para>
/// 보스가 아닌 몬스터 하나를 골라 DamageNumberView가 없으면 붙이고 Spawn을 직접 부른다 (몬스터를 때리지 않음). 표식은 서버에서 걸었다가 푼다.
/// </summary>
public class DamageNumberSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		Monster monster = null;
		float giveUp = Time.time + 15f;
		while (monster == null && Time.time < giveUp)
		{
			foreach (Monster m in FindObjectsByType<Monster>(FindObjectsSortMode.None))
				if (!m.IsBoss && !m.IsDead && m.GetComponent<StatusEffects>() != null) { monster = m; break; }
			if (monster == null) yield return new WaitForSeconds(0.5f);
		}
		if (monster == null) { Check("StatusEffects가 있는 일반 몬스터를 찾음 (Host를 눌렀는지 확인)", false); yield break; }

		DamageNumberView view = monster.GetComponent<DamageNumberView>();
		if (view == null) view = monster.gameObject.AddComponent<DamageNumberView>();
		StatusEffects status = monster.GetComponent<StatusEffects>();
		status.Clear(StatusType.Mark);
		yield return null;

		// 1. 일반
		DamageNumber n = SpawnAndFind(view, 123);
		Check("숫자 123이 뜸", n != null && n.Value == 123);
		if (n == null) { Finish(); yield break; }
		Check($"세 자리 (지금 {n.DigitCount})", n.DigitCount == 3);
		Check("일반 그림", !n.IsCritical);
		float startScale = n.transform.localScale.x;
		float startY = n.transform.position.y;

		// 2·3. 팝, 튀어 오름, 사라짐
		yield return new WaitForSeconds(0.2f);
		Check("처음에 크게 시작해서 줄어듦 (팝)", n != null && n.transform.localScale.x < startScale);
		Check("위로 튀어 오름", n != null && n.transform.position.y > startY);
		yield return new WaitForSeconds(1f);
		Check("1초 안에 사라짐", n == null);

		// 5. 큰 데미지
		DamageNumber small = SpawnAndFind(view, 5);
		DamageNumber big = SpawnAndFind(view, 500);
		yield return new WaitForSeconds(0.2f);
		Check("큰 데미지 숫자가 더 큼", small != null && big != null && big.transform.localScale.x > small.transform.localScale.x);

		// 4. 표식
		status.Apply(StatusType.Mark, 3f);
		yield return null;
		DamageNumber normal = SpawnAndFind(view, 50);
		Check("표식이면 치명타 그림", normal != null && normal.IsCritical);
		status.Clear(StatusType.Mark);
		yield return null;
		DamageNumber plain = SpawnAndFind(view, 50);
		yield return new WaitForSeconds(0.2f);
		Check("표식 숫자가 일반보다 큼", normal != null && plain != null && !plain.IsCritical && normal.transform.localScale.x > plain.transform.localScale.x);

		Finish();
	}

	// 방금 만든 숫자: Spawn 전에 없던 것
	static DamageNumber SpawnAndFind(DamageNumberView view, int damage)
	{
		var before = new System.Collections.Generic.HashSet<DamageNumber>(FindObjectsByType<DamageNumber>(FindObjectsSortMode.None));
		view.Spawn(damage);
		foreach (DamageNumber d in FindObjectsByType<DamageNumber>(FindObjectsSortMode.None))
			if (!before.Contains(d)) return d;
		return null;
	}

	void Finish()
	{
		if (failures == 0) Debug.Log("[DmgNumTest] 전체 통과");
		else Debug.LogError($"[DmgNumTest] 실패 {failures}개");
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[DmgNumTest] {label}: 통과");
		else { failures++; Debug.LogError($"[DmgNumTest] {label}: 실패"); }
	}
}
