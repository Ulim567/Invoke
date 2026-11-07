using System.Collections;
using System.Linq;
using UnityEngine;

/// <summary>
/// 상태 이상 표시 테스트 (#156). 씬의 아무 오브젝트에 붙이고 Play → Host (몬스터가 나타날 때까지 기다린 뒤 약 3초). 콘솔에 「[StatusIconTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 출혈을 걸면 아이콘 1개, 머리 위<br/>
/// 2. 출혈·표식·기절 → 아이콘 3개가 걸린 순서대로 왼쪽부터 나란히<br/>
/// 3. 기절(1초)이 먼저 풀리면 2개만 남고 다시 가운데 정렬<br/>
/// 4. 다시 걸어도 아이콘이 늘지 않음<br/>
/// 5. 몬스터가 죽으면 모두 사라짐
/// </para>
/// 몬스터에 StatusEffects가 미리 붙어 있어야 한다 (네트워크 컴포넌트라 실행 중에 붙이지 않음, #325). StatusIconView는 없으면 테스트 동안 붙인다. 마지막에 몬스터 하나를 죽이므로 평소에는 꺼 둔다.
/// </summary>
public class StatusIconSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		// 네트워크 몬스터는 Host를 누른 뒤 서버가 켜야 나타나므로 보일 때까지 기다린다 (최대 15초)
		Monster monster = null;
		float until = Time.time + 15f;
		while (monster == null && Time.time < until)
		{
			monster = FindFirstObjectByType<Monster>();
			yield return null;
		}
		yield return new WaitForSeconds(0.5f);
		if (monster == null) { Check("씬에 몬스터가 있음", false); yield break; }

		StatusEffects status = monster.GetComponent<StatusEffects>();
		if (status == null) { Check($"{monster.name}에 StatusEffects가 붙어 있음", false); yield break; }
		StatusIconView view = monster.GetComponent<StatusIconView>();
		if (view == null) view = monster.gameObject.AddComponent<StatusIconView>();
		status.ClearAll();
		yield return null;

		// 1. 출혈 하나
		status.Apply(StatusType.Bleed);
		Collider2D body = monster.GetComponentInChildren<Collider2D>();
		bool above = view.TryGetIconPosition(StatusType.Bleed, out Vector3 bleedPos) && body != null && bleedPos.y > body.bounds.max.y;
		Check("출혈 → 아이콘 1개, 머리 위", view.ShownStatuses.Count == 1 && above);
		Check("아이콘 그림 = 출혈", IconSprite(monster, StatusType.Bleed));

		// 2. 세 개
		status.Apply(StatusType.Mark);
		status.Apply(StatusType.Stun);
		bool ordered = view.ShownStatuses.Count == 3 && view.ShownStatuses[0] == StatusType.Bleed && view.ShownStatuses[1] == StatusType.Mark && view.ShownStatuses[2] == StatusType.Stun;
		view.TryGetIconPosition(StatusType.Bleed, out Vector3 a);
		view.TryGetIconPosition(StatusType.Mark, out Vector3 b);
		view.TryGetIconPosition(StatusType.Stun, out Vector3 c);
		Check("출혈·표식·기절 → 3개, 걸린 순서대로 왼쪽부터", ordered && a.x < b.x && b.x < c.x && Mathf.Abs(a.y - c.y) < 0.001f);
		Check("가운데 맞춤 (가운데 아이콘 = 몸 가운데)", Mathf.Abs(b.x - body.bounds.center.x) < 0.01f);

		// 4. 다시 걸어도 그대로
		status.Apply(StatusType.Bleed);
		Check("다시 걸어도 3개 그대로", view.ShownStatuses.Count == 3);

		// 3. 기절(1초) 풀림
		yield return new WaitForSeconds(StatusDefaults.DurationOf(StatusType.Stun) + 0.2f);
		view.TryGetIconPosition(StatusType.Bleed, out a);
		view.TryGetIconPosition(StatusType.Mark, out b);
		float center = (a.x + b.x) * 0.5f;
		Check("기절이 풀리면 2개 남고 다시 가운데 정렬", view.ShownStatuses.Count == 2 && !view.ShownStatuses.Contains(StatusType.Stun) && Mathf.Abs(center - body.bounds.center.x) < 0.01f);

		// 5. 죽으면 모두 사라짐
		monster.TakeDamage(monster.Hp);
		yield return null;
		Check("몬스터가 죽으면 아이콘 모두 사라짐", view.ShownStatuses.Count == 0 && monster.GetComponentsInChildren<StatusIcon>().Length == 0);

		if (failures == 0) Debug.Log("[StatusIconTest] 전체 통과");
		else Debug.LogError($"[StatusIconTest] 실패 {failures}개");
	}

	static bool IconSprite(Monster monster, StatusType type)
	{
		foreach (StatusIcon icon in monster.GetComponentsInChildren<StatusIcon>())
			if (icon.Type == type) return icon.GetComponent<SpriteRenderer>().sprite != null;
		return false;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[StatusIconTest] {label}: 통과");
		else { failures++; Debug.LogError($"[StatusIconTest] {label}: 실패"); }
	}
}
