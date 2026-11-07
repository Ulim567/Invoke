using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 재료 드롭 효과 테스트 (#404). Test_JW 씬의 아무 오브젝트에 붙이고 Play → Host (플레이어·몬스터가 Host 뒤에 나와서 최대 15초 기다림). 콘솔에 「[MaterialDropTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 버그 조각 3개 → 아이콘 3개<br/>
/// 2. 처음에는 위로 튀어 오름<br/>
/// 3. 잠시 뒤 플레이어에게 날아가는 중<br/>
/// 4. 2.5초 안에 모두 플레이어에 닿아 사라짐<br/>
/// 5. 없는 재료 ID는 아무것도 안 띄움<br/>
/// 6. 보스 코어 프리팹이 있으면 보스 코어 1개 → 아이콘 1개 (없으면 건너뜀)
/// </para>
/// MonsterReward가 붙은 몬스터(없으면 아무 일반 몬스터에 MonsterReward를 붙임)를 골라 MaterialDropView가 없으면 붙이고 Play를 직접 부른다 (확률을 굴리지 않음). 몬스터를 죽이지 않는다. MonsterReward를 붙이면 Player Layers 경고가 한 번 뜰 수 있다 (무시).
/// </summary>
public class MaterialDropSimTest : MonoBehaviour
{
	int failures;

	IEnumerator Start()
	{
		MonsterReward reward = null;
		PlayerProgress player = null;
		float giveUp = Time.time + 15f;
		while ((reward == null || player == null) && Time.time < giveUp)
		{
			reward = FindFirstObjectByType<MonsterReward>();
			if (reward == null)
			{
				// Test_JW의 테스트 몬스터에는 보상이 없어서 테스트 동안만 붙인다 (Play를 직접 부르므로 보상 값은 상관없음)
				Monster monster = FindFirstObjectByType<Monster>();
				if (monster != null && !monster.IsBoss) reward = monster.gameObject.AddComponent<MonsterReward>();
			}
			player = FindFirstObjectByType<PlayerProgress>();
			if (reward == null || player == null) yield return new WaitForSeconds(0.5f);
		}
		if (reward == null || player == null) { Check("MonsterReward 몬스터와 플레이어를 찾음 (Host를 눌렀는지 확인)", false); yield break; }
		Debug.Log($"[MaterialDropTest] 몬스터: {reward.name}, 플레이어: {player.name}");

		MaterialDropView view = reward.GetComponent<MaterialDropView>();
		if (view == null) view = reward.gameObject.AddComponent<MaterialDropView>();
		yield return null;

		// 1. 버그 조각 3개
		int before = view.SpawnedCount;
		view.Play(player, new List<MonsterReward.MaterialDrop> { Drop("bug_fragment", 3) });
		Check("버그 조각 3개 → 아이콘 3개", view.SpawnedCount - before == 3);
		MaterialDropFlight[] icons = FindObjectsByType<MaterialDropFlight>(FindObjectsSortMode.None);
		float startY = AverageY(icons);

		// 2. 튀어 오름
		yield return new WaitForSeconds(0.15f);
		Check("처음에는 위로 튀어 오름", icons.Length > 0 && AverageY(icons) > startY + 0.3f && !AnyHoming(icons));

		// 3. 날아가는 중
		yield return new WaitForSeconds(0.4f);
		Check("잠시 뒤 플레이어에게 날아가는 중", AllHomingOrGone(icons));

		// 4. 닿아서 사라짐
		yield return new WaitForSeconds(2f);
		Check("2.5초 안에 모두 플레이어에 닿아 사라짐", FindObjectsByType<MaterialDropFlight>(FindObjectsSortMode.None).Length == 0);

		// 5. 없는 재료
		before = view.SpawnedCount;
		view.Play(player, new List<MonsterReward.MaterialDrop> { Drop("no_such_material", 2) });
		Check("없는 재료 ID는 아무것도 안 띄움", view.SpawnedCount == before);

		// 6. 보스 코어
		if (Resources.Load<MaterialDropFlight>("MaterialDrops/boss_core") != null)
		{
			before = view.SpawnedCount;
			view.Play(player, new List<MonsterReward.MaterialDrop> { Drop("boss_core", 1) });
			Check("보스 코어 1개 → 아이콘 1개", view.SpawnedCount - before == 1);
		}
		else Debug.Log("[MaterialDropTest] 보스 코어 프리팹이 아직 없어서 건너뜀");

		if (failures == 0) Debug.Log("[MaterialDropTest] 전체 통과");
		else Debug.LogError($"[MaterialDropTest] 실패 {failures}개");
	}

	static MonsterReward.MaterialDrop Drop(string id, int count) => new MonsterReward.MaterialDrop { materialId = id, count = count, chance = 1f };

	static float AverageY(MaterialDropFlight[] icons)
	{
		float sum = 0f; int n = 0;
		foreach (MaterialDropFlight i in icons) if (i != null) { sum += i.transform.position.y; n++; }
		return n > 0 ? sum / n : 0f;
	}

	static bool AnyHoming(MaterialDropFlight[] icons)
	{
		foreach (MaterialDropFlight i in icons) if (i != null && i.IsHoming) return true;
		return false;
	}

	static bool AllHomingOrGone(MaterialDropFlight[] icons)
	{
		foreach (MaterialDropFlight i in icons) if (i != null && !i.IsHoming) return false;
		return true;
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[MaterialDropTest] {label}: 통과");
		else { failures++; Debug.LogError($"[MaterialDropTest] {label}: 실패"); }
	}
}
