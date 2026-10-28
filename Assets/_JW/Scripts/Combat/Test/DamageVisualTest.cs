using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 데미지 계산을 눈으로 확인하는 테스트용 컴포넌트. 씬의 아무 오브젝트에 붙이고, 몬스터(TestMonster)를 몇 마리 놓은 뒤 Play → Host.
/// <para>
/// 키 (내 캐릭터에서 가장 가까운 몬스터를 때린다. 공격력은 내 PlayerStats.AttackPower)<br/>
/// - Q: 기본 공격 1.0배<br/>
/// - W: 표식 + 집중찌르기(출혈) 2.5배<br/>
/// - E: 표식 + 깊이찌르기 치명타 3.5배 (치명타 100%)<br/>
/// - R: 검 던지기 1.0배 (처형 가능한 공격)<br/>
/// - A: [디버그] 몬스터 체력을 2로 만든다 (처형 테스트용, 데미지 계산을 거치지 않음)
/// </para>
/// <para>
/// 몬스터 머리 위에 체력이 보이고, 맞으면 데미지 숫자가 뜬다. 흰색 일반, 노란색 치명타, 빨간색 처형.<br/>
/// 연출(OnGUI 글자)은 테스트용이다. 실제 데미지 숫자 연출은 임혜원 이슈에서 만든다.
/// </para>
/// </summary>
public class DamageVisualTest : MonoBehaviour
{
	[SerializeField, Min(0f), Tooltip("때릴 수 있는 거리 (유닛)")]
	float range = 8f;

	[SerializeField, Tooltip("몬스터 레이어")]
	LayerMask monsterLayers;

	[SerializeField, Min(0.1f), Tooltip("데미지 숫자가 떠 있는 시간(초)")]
	float popupTime = 1.2f;

	struct Popup
	{
		public Vector3 WorldPos;
		public string Text;
		public Color Color;
		public float StartTime;
	}

	readonly List<Popup> popups = new List<Popup>();
	PlayerInputReader input;
	PlayerStats stats;
	GUIStyle style;

	void Reset()
	{
		monsterLayers = LayerMask.GetMask("Monster");
	}

	void Update()
	{
		if (!FindLocalPlayer()) return;

		if (input.SlotPressed(0)) Attack("기본", new DamageRequest(stats.AttackPower, 1f));
		if (input.SlotPressed(1)) Attack("표식+집중찌르기", new DamageRequest(stats.AttackPower, 1f)
		{
			BonusRate = DamageBonus.Mark + DamageBonus.BleedFocusedThrust,
		});
		if (input.SlotPressed(2)) Attack("표식+깊이찌르기", new DamageRequest(stats.AttackPower, 1f)
		{
			BonusRate = DamageBonus.Mark,
			CritChance = 1f,
			CritBonusRate = DamageBonus.CritDeepThrust,
		});
		if (input.SlotPressed(3)) Attack("검 던지기", new DamageRequest(stats.AttackPower, 1f) { CanExecute = true });
		if (input.SlotPressed(4)) SetNearestHpTo2();
	}

	// 내 캐릭터(Host로 들어온 플레이어)의 입력과 능력치를 찾는다. 입력은 PlayerInputReader로만 읽는다 (코드 작성 규칙 5)
	bool FindLocalPlayer()
	{
		if (input != null && stats != null) return true;
		if (NetworkClient.localPlayer == null) return false;

		input = NetworkClient.localPlayer.GetComponent<PlayerInputReader>();
		stats = NetworkClient.localPlayer.GetComponent<PlayerStats>();
		return input != null && stats != null;
	}

	void Attack(string skillName, DamageRequest request)
	{
		Monster target = FindNearestMonster();
		if (target == null) return;

		// 계산 → TakeDamage → 표시 순서. 표시는 계산 결과만 읽는다 (코드 작성 규칙 1)
		DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(target));
		Vector3 headPos = HeadPosition(target);
		target.TakeDamage(result.Damage);
		ShowPopup(headPos, result);

		Debug.Log($"[DamageVisualTest] {skillName} → {target.name}: {result.Damage}" +
			$"{(result.IsCritical ? " (치명타)" : "")}{(result.IsExecution ? " (처형)" : "")}, 남은 체력 {target.Hp}/{target.MaxHp}");
	}

	void SetNearestHpTo2()
	{
		Monster target = FindNearestMonster();
		if (target == null || target.Hp <= 2) return;
		target.TakeDamage(target.Hp - 2);
		Debug.Log($"[DamageVisualTest] [디버그] {target.name} 체력을 2로 ({target.Hp}/{target.MaxHp})");
	}

	Monster FindNearestMonster()
	{
		Vector2 origin = NetworkClient.localPlayer.transform.position;
		Collider2D[] hits = Physics2D.OverlapCircleAll(origin, range, monsterLayers);

		Monster nearest = null;
		float best = float.MaxValue;
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead) continue;

			float dist = ((Vector2)monster.transform.position - origin).sqrMagnitude;
			if (dist < best) { best = dist; nearest = monster; }
		}
		return nearest;
	}

	void ShowPopup(Vector3 worldPos, DamageResult result)
	{
		popups.Add(new Popup
		{
			WorldPos = worldPos,
			Text = result.IsExecution ? $"처형! {result.Damage}" : result.Damage.ToString(),
			Color = result.IsExecution ? Color.red : result.IsCritical ? Color.yellow : Color.white,
			StartTime = Time.time,
		});
	}

	static Vector3 HeadPosition(Monster monster)
	{
		Collider2D col = monster.GetComponentInChildren<Collider2D>();
		return col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y) : monster.transform.position;
	}

	void OnGUI()
	{
		Camera cam = Camera.main;
		if (cam == null) return;

		style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
		float scale = Screen.height / 360f;

		// 키 안내
		style.fontSize = Mathf.RoundToInt(6 * scale);
		GUI.color = Color.white;
		GUI.Label(new Rect(10, Screen.height - 40 * scale, 600 * scale, 40 * scale),
			"Q 기본 1.0배 | W 표식+집중찌르기 2.5배 | E 표식+깊이찌르기 치명타 3.5배 | R 검 던지기(처형) | A 체력→2");

		// 몬스터 체력
		foreach (Monster monster in FindObjectsByType<Monster>())
		{
			if (monster.IsDead) continue;
			string label = $"{monster.Hp}/{monster.MaxHp}{(monster.IsBoss ? " (보스)" : "")}";
			DrawWorldLabel(cam, HeadPosition(monster) + Vector3.up * 0.4f, label, Color.green, 7 * scale);
		}

		// 데미지 숫자 (위로 떠오르며 사라짐)
		popups.RemoveAll(p => Time.time - p.StartTime > popupTime);
		foreach (Popup popup in popups)
		{
			float t = (Time.time - popup.StartTime) / popupTime;
			Color color = popup.Color;
			color.a = 1f - t;
			DrawWorldLabel(cam, popup.WorldPos + Vector3.up * (1f + t * 1.5f), popup.Text, color, 12 * scale);
		}
		GUI.color = Color.white;
	}

	void DrawWorldLabel(Camera cam, Vector3 worldPos, string text, Color color, float fontSize)
	{
		Vector3 screen = cam.WorldToScreenPoint(worldPos);
		if (screen.z < 0f) return;

		style.fontSize = Mathf.RoundToInt(fontSize);
		GUI.color = color;
		GUI.Label(new Rect(screen.x - 150, Screen.height - screen.y - 30, 300, 60), text, style);
	}
}
