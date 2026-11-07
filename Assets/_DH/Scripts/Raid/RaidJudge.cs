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
/// - 보스 체력은 던전 안 인원에 맞춘다 (Hp Rate By Players: 1명 40%, 2명 70%, 3명 100%). 보스가 한 번도 맞지 않은 동안만 다시 맞추고, 처음 맞으면 그대로 둔다.<br/>
/// - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 판정한다. 플레이어는 이 씬의 최상위 오브젝트 중 PlayerHealth가 붙은 것이다.<br/>
/// - 바깥에서 부르는 함수: FailByServer(서버, 파티원 이탈·레이드 포기로 실패), PlayResultOnly(클라이언트, 서버가 보낸 결과로 결과 화면만). 둘 다 RaidParties(이우림)가 부른다.
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

	[SerializeField, Tooltip("던전 안 인원별 보스 체력 비율 (Inspector 최대 체력 기준). 0번이 1명. 인원이 더 많으면 마지막 값")]
	float[] hpRateByPlayers = { 0.4f, 0.7f, 1f };

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
	// 보스가 처음 맞아서 체력을 더는 인원에 맞추지 않으면 true
	bool hpFitLocked;
	// 결과 화면 이벤트를 이미 불렀으면 true (한 던전에서 한 번만)
	bool resultPlayed;

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

	/// <summary>
	/// [서버] 레이드를 실패로 끝낸다. 파티원이 이탈하거나 레이드를 포기했을 때 RaidParties가 부른다.
	/// 모두 죽었을 때와 같다: ResultEffectPlayed(false) → Result Delay 뒤 RaidFlow.ReportEnd(던전 씬, false).
	/// 이미 끝났으면 아무것도 하지 않는다.
	/// </summary>
	public void FailByServer()
	{
		if (!IsAuthority())
		{
			Debug.LogWarning("[RaidJudge] FailByServer는 서버에서만 부릅니다.", this);
			return;
		}
		End(false);
	}

	/// <summary>
	/// [클라이언트] 판정 없이 결과 화면 이벤트(ResultEffectPlayed)만 부른다. 서버의 RaidResultMessage를 받은 클라이언트에서 RaidParties가 부른다.
	/// ReportEnd는 부르지 않는다. 이 던전에서 이미 결과 화면을 불렀으면(Host 등) 무시한다.
	/// </summary>
	public void PlayResultOnly(bool isCleared)
	{
		if (resultPlayed) return;
		PlayResultEffect(isCleared);
	}

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
		FitBossHp();
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

	// 보스 체력을 지금 인원에 맞춘다. 파티원은 몇 초 차이로 들어오므로, 보스가 처음 맞기 전까지 계속 맞춘다
	void FitBossHp()
	{
		if (hpFitLocked || boss == null || players.Count == 0 || hpRateByPlayers == null || hpRateByPlayers.Length == 0) return;
		float rate = hpRateByPlayers[Mathf.Clamp(players.Count - 1, 0, hpRateByPlayers.Length - 1)];
		// 이미 맞았으면 TrySetMaxHpRate가 false를 돌려준다. 그 뒤로는 바꾸지 않는다
		if (!boss.TrySetMaxHpRate(rate)) hpFitLocked = true;
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

	// 연출만 한다 (코드 작성 규칙 1). 씬에 놓인 오브젝트라 [ClientRpc]를 쓰지 않고, 클라이언트에는 RaidParties가 RaidResultMessage로 전한다 (PlayResultOnly)
	void PlayResultEffect(bool isCleared)
	{
		resultPlayed = true;
		ResultEffectPlayed?.Invoke(isCleared);
	}

	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;
}
