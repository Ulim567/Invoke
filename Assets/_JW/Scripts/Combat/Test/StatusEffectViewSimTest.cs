using System.Collections;
using UnityEngine;

/// <summary>
/// 상태 이상 몸 효과 테스트 (#353). Test_JW의 StatusViewTest. 켜고 Play → Host (몬스터가 나타날 때까지 기다린 뒤 약 4초). 콘솔에 「[StatusViewTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 실명 → 어두운 틴트, 풀리면 처음 색<br/>
/// 2. 이속 감소 → 파란 틴트 / 실명 + 이속 감소 → 둘을 곱한 색<br/>
/// 3. 출혈 → 1초마다 빨갛게 번쩍임 (약 2.3초에 2번 이상), 다시 걸어도 효과가 늘지 않음<br/>
/// 4. 그림 칸이 비어 있으면 그림 효과 없이 색만 (오류 없음)<br/>
/// 5. 모두 풀리면 처음 색
/// </para>
/// 몬스터에 StatusEffects가 미리 붙어 있어야 한다 (#348). StatusEffectView는 없으면 테스트 동안 붙인다. 평소에는 꺼 둔다.
/// </summary>
public class StatusEffectViewSimTest : MonoBehaviour
{
	const string Tag = "[StatusViewTest]";
	int failures;

	IEnumerator Start()
	{
		// 네트워크 몬스터는 Host를 누른 뒤 서버가 켜야 나타나므로 보일 때까지 기다린다 (최대 15초)
		Monster monster = null;
		float until = Time.time + 15f;
		while (monster == null && Time.time < until)
		{
			foreach (Monster m in FindObjectsByType<Monster>())
			{
				if (!m.IsDead && !m.IsBoss) { monster = m; break; }
			}
			yield return null;
		}
		yield return new WaitForSeconds(0.5f);
		if (monster == null) { Check("보스가 아닌 몬스터가 있음", false); yield break; }

		StatusEffects status = monster.GetComponent<StatusEffects>();
		if (status == null) { Check($"{monster.name}에 StatusEffects가 붙어 있음", false); yield break; }
		StatusEffectView view = monster.GetComponent<StatusEffectView>();
		if (view == null) view = monster.gameObject.AddComponent<StatusEffectView>();
		yield return null;
		if (view.TargetRenderer == null) { Check($"{monster.name}에 SpriteRenderer가 있음", false); yield break; }

		status.ClearAll();
		yield return null;
		Color baseColor = view.TargetRenderer.color;

		// 1. 실명
		status.Apply(StatusType.Blind, 10f);
		yield return null;
		Color blind = view.TargetRenderer.color;
		Check($"실명 → 어두워짐 ({Fmt(baseColor)} → {Fmt(blind)})", Brightness(blind) < Brightness(baseColor) - 0.1f);
		Check("실명 → 그림 칸이 비어 있으면 그림 효과 없음 (오류 없음)", !view.HasEffectObject(StatusType.Blind));
		status.Clear(StatusType.Blind);
		yield return null;
		Check($"실명 풀림 → 처음 색 ({Fmt(view.TargetRenderer.color)})", Same(view.TargetRenderer.color, baseColor));

		// 2. 이속 감소, 둘 다
		status.Apply(StatusType.Slow, 10f);
		yield return null;
		Color slow = view.TargetRenderer.color;
		Check($"이속 감소 → 파래짐 ({Fmt(slow)})", (slow.b - slow.r) > (baseColor.b - baseColor.r) + 0.05f);
		status.Apply(StatusType.Blind, 10f);
		yield return null;
		Color both = view.TargetRenderer.color;
		Check($"실명 + 이속 감소 → 둘 다 섞임 ({Fmt(both)})", Brightness(both) < Brightness(slow) - 0.05f && Brightness(both) < Brightness(blind) - 0.02f);
		status.Clear(StatusType.Blind);
		status.Clear(StatusType.Slow);
		yield return null;
		Check("둘 다 풀림 → 처음 색", Same(view.TargetRenderer.color, baseColor));

		// 3. 출혈 번쩍임
		int flashesBefore = view.BleedFlashCount;
		status.Apply(StatusType.Bleed);
		yield return new WaitForSeconds(0.5f);
		status.Apply(StatusType.Bleed);
		Check("출혈 다시 걸어도 효과 하나", view.ShownStatuses.Count == 1);
		bool sawRed = false;
		float watchEnd = Time.time + 1.8f;
		while (Time.time < watchEnd)
		{
			Color c = view.TargetRenderer.color;
			if (view.IsFlashing && c.r > c.g + 0.3f) sawRed = true;
			yield return null;
		}
		int flashes = view.BleedFlashCount - flashesBefore;
		Check($"출혈 → 1초마다 번쩍임 (약 2.3초에 {flashes}번)", flashes >= 2);
		Check("번쩍일 때 빨간색", sawRed);

		// 5. 모두 풀림
		status.ClearAll();
		yield return new WaitForSeconds(0.3f);
		Check($"모두 풀림 → 처음 색 ({Fmt(view.TargetRenderer.color)})", Same(view.TargetRenderer.color, baseColor) && view.ShownStatuses.Count == 0);

		if (failures == 0) Debug.Log($"{Tag} 전체 통과");
		else Debug.LogError($"{Tag} 실패 {failures}개");
	}

	static float Brightness(Color c) => (c.r + c.g + c.b) / 3f;
	static bool Same(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;
	static string Fmt(Color c) => $"{c.r:0.00},{c.g:0.00},{c.b:0.00}";

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"{Tag} {label}: 통과");
		else
		{
			failures++;
			Debug.LogError($"{Tag} {label}: 실패");
		}
	}
}
