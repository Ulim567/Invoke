using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 대상 선택 조준점 테스트 (#438). Test_JW 씬의 아무 오브젝트에 붙이고 Play → Host (몬스터가 Host 뒤에 나와서 최대 15초 기다림). 콘솔에 「[ReticleTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 대상 고르면 빨간 조준점이 대상 몸 가운데에 보임<br/>
/// 2. 대상 둘 찍으면 하늘색 조준점 2개, 고른 대상과 겹치는 하늘색은 크게<br/>
/// 3. 고르기를 끝내면(null, 빈 목록) 모두 숨김<br/>
/// 4. 대상이 사라지면(꺼지면) 조준점도 숨김
/// </para>
/// 내 플레이어의 SlotTargetMarker에 SetTarget/SetMarked를 직접 부른다 (슬롯 키 입력 없음). 몬스터는 잠깐 껐다 다시 켠다.
/// </summary>
public class SlotTargetMarkerSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		SlotTargetMarker marker = null;
		var monsters = new List<Monster>();
		float giveUp = Time.time + 15f;
		while ((marker == null || monsters.Count < 2) && Time.time < giveUp)
		{
			marker = FindFirstObjectByType<SlotTargetMarker>();
			monsters.Clear();
			foreach (Monster m in FindObjectsByType<Monster>(FindObjectsSortMode.None)) if (!m.IsDead) monsters.Add(m);
			if (marker == null || monsters.Count < 2) yield return new WaitForSeconds(0.5f);
		}
		if (marker == null || monsters.Count < 2) { Check("플레이어(SlotTargetMarker)와 몬스터 2마리를 찾음 (Host를 눌렀는지 확인)", false); yield break; }
		GameObject a = monsters[0].gameObject, b = monsters[1].gameObject;

		// 1. 고른 대상
		marker.SetTarget(a);
		yield return null;
		Vector2 center = a.GetComponentInChildren<Collider2D>().bounds.center;
		Check("대상 고르면 빨간 조준점이 보임", marker.IsTargetShown);
		Check("조준점이 대상 몸 가운데", Vector2.Distance(marker.TargetReticlePosition, center) < 0.05f);

		// 2. 찍은 대상 둘 (하나는 고른 대상)
		marker.SetMarked(new List<GameObject> { a, b });
		yield return null;
		Check($"찍은 대상 조준점 2개 (지금 {marker.ShownMarkedCount})", marker.ShownMarkedCount == 2);
		Check("고른 대상과 겹치는 하늘색은 크게", marker.LargestMarkedScale > marker.Scale * 1.01f);

		// 3. 끝내기
		marker.SetTarget(null);
		marker.SetMarked(new List<GameObject>());
		yield return null;
		Check("고르기 끝나면 모두 숨김", !marker.IsTargetShown && marker.ShownMarkedCount == 0);

		// 4. 대상이 사라짐
		marker.SetTarget(b);
		yield return null;
		b.SetActive(false);
		yield return null;
		Check("대상이 꺼지면 조준점도 숨김", !marker.IsTargetShown);
		b.SetActive(true);
		marker.SetTarget(null);

		if (failures == 0) Debug.Log("[ReticleTest] 전체 통과");
		else Debug.LogError($"[ReticleTest] 실패 {failures}개");
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[ReticleTest] {label}: 통과");
		else { failures++; Debug.LogError($"[ReticleTest] {label}: 실패"); }
	}
}
