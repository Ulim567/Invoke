using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// C 스킬 서버 동기화 확인용 로그 (#223). Test_JW의 SkillSyncLog. 켜 두고 에디터를 Host, 빌드를 Client로 접속해서 클라이언트 캐릭터로 스킬을 쓴다.
/// <para>
/// - 서버(Host)에서 모든 플레이어의 SkillEffectEvents를 받아 콘솔에 남긴다: 누가 어떤 스킬을, 어느 방향으로, 누구에게 몇 데미지(치명타), 상태 이상(실명·이속 감소).<br/>
/// - 클라이언트 플레이어의 돌진·방향·급소공략 결과가 서버에 제대로 왔는지 Host 콘솔로 확인하는 용도다. 판정은 사람이 로그를 보고 한다.<br/>
/// - 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class SkillSyncLogTest : MonoBehaviour
{
	const string Tag = "[SkillSyncLog]";

	readonly HashSet<SkillEffectEvents> watched = new HashSet<SkillEffectEvents>();

	IEnumerator Start()
	{
		// 플레이어가 접속할 때마다 새로 생기므로 1초마다 찾는다
		while (true)
		{
			if (NetworkServer.active)
			{
				foreach (SkillEffectEvents events in FindObjectsByType<SkillEffectEvents>())
				{
					if (events == null || !watched.Add(events)) continue;
					Watch(events);
					Debug.Log($"{Tag} {Who(events)} 스킬 연출 기록 시작");
				}
			}
			yield return new WaitForSeconds(1f);
		}
	}

	void Watch(SkillEffectEvents events)
	{
		events.Fired += (id, origin, dir) =>
			Debug.Log($"{Tag} {Who(events)} {id} 발사: {(dir.x < 0f ? "왼쪽" : "오른쪽")} (몸 x {origin.x:0.0})");
		events.Hit += (id, target, point, result) =>
			StartCoroutine(LogHit(events, id, target, result));
	}

	// 상태 이상은 데미지 뒤에 걸리므로 한 프레임 기다렸다가 같이 남긴다
	IEnumerator LogHit(SkillEffectEvents events, string id, GameObject target, DamageResult result)
	{
		yield return null;
		string status = "";
		StatusEffects effects = target != null ? target.GetComponent<StatusEffects>() : null;
		if (effects != null)
		{
			if (effects.Has(StatusType.Blind)) status += " 실명";
			if (effects.Has(StatusType.Slow)) status += " 이속감소";
		}
		Debug.Log($"{Tag} {Who(events)} {id} 명중: {(target != null ? target.name : "?")} 데미지 {result.Damage}{(result.IsCritical ? " 치명타" : "")}{(result.IsExecution ? " 처형" : "")}{status}");
	}

	// Host의 내 캐릭터인지, 다른 컴퓨터(클라이언트)의 캐릭터인지
	static string Who(SkillEffectEvents events)
	{
		NetworkIdentity identity = events.GetComponent<NetworkIdentity>();
		bool host = identity != null && identity.isLocalPlayer;
		return $"[{(host ? "Host" : "클라이언트")} {events.name}]";
	}
}
