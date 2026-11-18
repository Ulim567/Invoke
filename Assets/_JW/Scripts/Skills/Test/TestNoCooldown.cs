using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// [테스트 전용, #566] 켜 두면 서버(Host)의 모든 플레이어에게 쿨타임 감소 100%(PlayerStats.AddBonus(CooldownReduction, 1))를 준다.
/// <para>
/// - C 스킬에 쿨타임이 생긴 뒤(#566), 같은 스킬을 연달아 시전하는 시험(ThrustSkill, Slash2, MarkDance 등)이 쿨타임에 막히지 않게 둔다.<br/>
/// - 쿨타임은 서버의 실행기가 재므로 서버에서만 준다. 네트워크 없는 시험에서는 그 자리의 플레이어에게 준다.<br/>
/// - 끄거나 없어지면 준 만큼 다시 뺀다. 쿨타임 자체를 보는 시험(StanceTest의 쿨타임, CooldownTest)과는 같이 켜지 않는다.<br/>
/// - 씬을 바꿔도(장소 이동) 계속 동작하게 DontDestroyOnLoad로 둔다. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class TestNoCooldown : MonoBehaviour
{
	[Tooltip("새 플레이어를 찾는 간격 (초)")]
	[SerializeField, Min(0.1f)] float interval = 1f;

	// 쿨타임 감소를 준 플레이어. 끌 때 그만큼 다시 뺀다
	readonly HashSet<PlayerStats> given = new HashSet<PlayerStats>();

	IEnumerator Start()
	{
		DontDestroyOnLoad(gameObject);
		var wait = new WaitForSeconds(interval);
		while (true)
		{
			if (NetworkServer.active || !NetworkClient.active)
			{
				foreach (PlayerStats stats in FindObjectsByType<PlayerStats>())
				{
					if (stats == null || given.Contains(stats)) continue;
					stats.AddBonus(StatType.CooldownReduction, 1f);
					given.Add(stats);
					Debug.Log($"[TestNoCooldown] {stats.name}: 쿨타임 감소 100% (시험용)");
				}
			}
			yield return wait;
		}
	}

	void OnDisable()
	{
		foreach (PlayerStats stats in given) if (stats != null) stats.AddBonus(StatType.CooldownReduction, -1f);
		given.Clear();
	}
}
