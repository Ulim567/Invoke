using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 레이드 던전의 클리어·실패를 판정한다. 같은 오브젝트의 RaidBossSpawner가 만든 보스를 본다.
/// <para>
/// - 클리어: 보스가 죽으면(Monster.Died).<br/>
/// - 실패: 던전 안 플레이어가 한 명 이상 있고, 모두 죽었으면(PlayerHealth.IsDead).<br/>
/// - 던전에 들어온 플레이어는 죽어도 부활하지 않고 관전한다(SetAutoRespawn(false)). 던전을 나가거나 레이드가 끝나면 다시 켠다.<br/>
/// - 끝나면 ResultEffectPlayed(결과 화면용)를 부르고, Result Delay초 뒤 RaidFlow.ReportEnd(레이드 끝)를 부른다. 한 던전에서 한 번만 끝난다.<br/>
/// - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 판정한다. 플레이어는 이 씬의 최상위 오브젝트 중 PlayerHealth가 붙은 것이다.
/// </para>
/// <para>사용법: 레이드 던전 씬의 RaidBossSpawner 오브젝트에 함께 붙인다.</para>
/// </summary>
[RequireComponent(typeof(RaidBossSpawner))]
public class RaidJudge : MonoBehaviour
{
	[SerializeField, Min(0.05f), Tooltip("던전의 플레이어를 다시 확인하는 간격(초)")]
	float checkInterval = 0.25f;

	[SerializeField, Min(0f), Tooltip("결과를 보여 준 뒤 레이드 끝을 알리기까지 시간(초). 결과 화면이 보일 시간")]
	float resultDelay = 3f;

	// 이 씬 최상위 오브젝트 목록. 모든 던전이 함께 쓴다 (메인 스레드라 겹치지 않음)
	static readonly List<GameObject> roots = new List<GameObject>(32);

	RaidBossSpawner spawner;
	Monster boss;
	// 던전 안에 있는 플레이어 (관전으로 바꿔 둔 플레이어)
	readonly List<PlayerHealth> players = new List<PlayerHealth>(4);
	float nextCheckTime;
	bool ended;
	bool cleared;
	float reportTime;
	bool reported;

	/// <summary>레이드가 끝났으면 true</summary>
	public bool IsEnded => ended;

	/// <summary>
	/// [연출] 레이드가 끝난 순간. 클리어면 true, 실패면 false. 결과 화면("레이드 성공/실패")에 쓴다.
	/// </summary>
	public event Action<bool> ResultEffectPlayed;

	void Awake() => spawner = GetComponent<RaidBossSpawner>();

	void OnEnable() => spawner.BossSpawned += OnBossSpawned;

	void OnDisable()
	{
		spawner.BossSpawned -= OnBossSpawned;
		if (boss != null) boss.Died -= OnBossDied;
		// 던전이 내려가면 남은 플레이어의 부활을 되돌린다
		RestoreRespawnAll();
	}

	void OnBossSpawned(Monster spawned)
	{
		boss = spawned;
		boss.Died += OnBossDied;
	}

	void OnBossDied() => End(true);

	void Update()
	{
		if (!IsAuthority()) return;
		float now = Time.time;

		if (ended)
		{
			if (!reported && now >= reportTime) Report();
			return;
		}

		if (now < nextCheckTime) return;
		nextCheckTime = now + checkInterval;

		RefreshPlayers();
		if (players.Count > 0 && AllDead()) End(false);
	}

	// 새로 들어온 플레이어는 관전으로 바꾸고, 나간 플레이어는 되돌린다
	void RefreshPlayers()
	{
		// 나간 플레이어 (다른 씬으로 옮겨졌거나 없어짐)
		for (int i = players.Count - 1; i >= 0; i--)
		{
			PlayerHealth p = players[i];
			if (p != null && p.gameObject.scene == gameObject.scene) continue;
			if (p != null) p.SetAutoRespawn(true);
			players.RemoveAt(i);
		}

		// 들어온 플레이어
		gameObject.scene.GetRootGameObjects(roots);
		for (int i = 0; i < roots.Count; i++)
		{
			if (!roots[i].TryGetComponent(out PlayerHealth p) || players.Contains(p)) continue;
			p.SetAutoRespawn(false);
			players.Add(p);
		}
		roots.Clear();
	}

	bool AllDead()
	{
		for (int i = 0; i < players.Count; i++)
			if (!players[i].IsDead) return false;
		return true;
	}

	void End(bool isCleared)
	{
		if (ended || !IsAuthority()) return;
		ended = true;
		cleared = isCleared;
		reportTime = Time.time + resultDelay;
		PlayResultEffect(isCleared);
	}

	// 결과를 보여 준 뒤 레이드 끝을 알린다. 죽은 파티원도 부활하도록 먼저 되돌린다
	void Report()
	{
		reported = true;
		RestoreRespawnAll();
		RaidFlow.ReportEnd(gameObject.scene, cleared);
	}

	void RestoreRespawnAll()
	{
		for (int i = 0; i < players.Count; i++)
			if (players[i] != null) players[i].SetAutoRespawn(true);
		players.Clear();
	}

	// 연출만 한다. 네트워크를 붙이면 [ClientRpc]를 단다 (코드 작성 규칙 1)
	void PlayResultEffect(bool isCleared) => ResultEffectPlayed?.Invoke(isCleared);

	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;
}
