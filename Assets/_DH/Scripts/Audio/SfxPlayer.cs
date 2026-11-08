using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// 효과음을 낸다 (#436). 몬스터와 플레이어 스킬의 연출 이벤트에 SoundTable의 소리를 붙인다. 소리만 내고 게임 값은 바꾸지 않는다.
/// <para>
/// - 몬스터: Monster.Appeared(이 화면에 나타남)를 받아, 그 몬스터의 피격·사망·내려찍기·던지기·소환 연출 이벤트에 소리를 붙이고 나타남 소리를 낸다.<br/>
/// - 플레이어 스킬: PlayerRoster.PlayerAdded를 받아, 그 플레이어의 SkillEffectEvents.Fired(판정이 나간 순간)에 스킬 소리를 붙인다.<br/>
/// - 내 캐릭터와 같은 장소의 소리만 낸다 (호스트는 다른 파티의 레이드 던전도 띄워 두기 때문). 내 캐릭터가 없으면(네트워크 없는 테스트) 모두 낸다.<br/>
/// - 같은 소리가 Min Gap초 안에 다시 나면 건너뛴다 (여러 명을 한꺼번에 맞히는 스킬 등).<br/>
/// - 연출 이벤트는 모든 화면에서 불리므로 네트워크를 따로 붙이지 않는다.
/// </para>
/// <para>
/// 사용법: Base 씬의 Sound 오브젝트에 붙인다. Table(SoundTable), Output(InvokeMixer의 SFX 그룹)을 넣는다. AudioSource는 시작할 때 이 오브젝트에 붙인다.<br/>
/// 다른 스크립트에서 소리를 낼 때는 SfxPlayer.Play(소리 목록, 그 일이 일어난 씬)를 부른다.
/// </para>
/// </summary>
public class SfxPlayer : MonoBehaviour
{
	[SerializeField, Tooltip("소리 표")]
	SoundTable table;

	[SerializeField, Tooltip("효과음을 보낼 Mixer 그룹 (InvokeMixer의 SFX). 설정 창의 효과음 음량을 따른다")]
	AudioMixerGroup output;

	[SerializeField, Range(0f, 1f), Tooltip("효과음 기본 크기")]
	float volume = 0.8f;

	[SerializeField, Min(0f), Tooltip("같은 소리를 다시 내기까지 최소 간격(초)")]
	float minGap = 0.06f;

	static SfxPlayer instance;

	AudioSource source;
	readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

	/// <summary>Base 씬의 소리 표. 없으면 null.</summary>
	public static SoundTable Table => instance != null ? instance.table : null;

	void Awake()
	{
		instance = this;
		source = gameObject.AddComponent<AudioSource>();
		source.playOnAwake = false;
		source.spatialBlend = 0f;
		source.outputAudioMixerGroup = output;
	}

	void OnEnable()
	{
		Monster.Appeared += OnMonsterAppeared;
		PlayerRoster.PlayerAdded += OnPlayerAdded;
		// 이 컴포넌트보다 먼저 들어온 플레이어
		IReadOnlyList<PlayerHealth> players = PlayerRoster.Players;
		for (int i = 0; i < players.Count; i++) OnPlayerAdded(players[i]);
	}

	void OnDisable()
	{
		Monster.Appeared -= OnMonsterAppeared;
		PlayerRoster.PlayerAdded -= OnPlayerAdded;
		if (instance == this) instance = null;
	}

	/// <summary>
	/// clips 중 하나를 랜덤으로 낸다. scene이 내 캐릭터의 장소가 아니면 내지 않는다. clips가 비었거나 SfxPlayer가 없으면 아무것도 안 한다.
	/// </summary>
	public static void Play(AudioClip[] clips, Scene scene)
	{
		if (instance == null || clips == null || clips.Length == 0 || !IsMyScene(scene)) return;
		instance.PlayOne(clips[Random.Range(0, clips.Length)]);
	}

	void PlayOne(AudioClip clip)
	{
		if (clip == null) return;
		float now = Time.unscaledTime;
		if (lastPlayed.TryGetValue(clip, out float last) && now - last < minGap) return;
		lastPlayed[clip] = now;
		source.PlayOneShot(clip, volume);
	}

	// 내 캐릭터가 있는 씬이면 true. 내 캐릭터가 없으면(네트워크 없는 테스트) 늘 true
	static bool IsMyScene(Scene scene)
	{
		NetworkIdentity me = NetworkClient.localPlayer;
		return me == null || !scene.IsValid() || me.gameObject.scene == scene;
	}

	// ── 몬스터 ──

	void OnMonsterAppeared(Monster monster)
	{
		if (table == null || monster == null || !table.TryGetMonster(monster.name, out SoundTable.MonsterSounds s)) return;

		// 씬은 지금 기억해 둔다. 몬스터가 없어진 뒤 이벤트가 와도 없어진 오브젝트를 읽지 않게 하기 위함 (몬스터는 장소를 옮기지 않음)
		Scene scene = monster.gameObject.scene;
		Play(s.appear, scene);

		// 몬스터가 없어지면 이벤트도 같이 사라지므로 따로 떼지 않는다
		if (s.hit != null && s.hit.Length > 0) monster.HitEffectPlayed += _ => Play(s.hit, scene);
		if (s.death != null && s.death.Length > 0) monster.DeathEffectPlayed += () => Play(s.death, scene);

		if (monster.TryGetComponent(out GatewayAI ai))
		{
			if (s.slam != null && s.slam.Length > 0) ai.SlamEffectPlayed += () => Play(s.slam, scene);
			if (s.summon != null && s.summon.Length > 0) ai.SummonEffectPlayed += () => Play(s.summon, scene);
		}
		if (s.fire != null && s.fire.Length > 0 && monster.TryGetComponent(out ProjectileAttack thrower))
			thrower.FireEffectPlayed += _ => Play(s.fire, scene);
	}

	// ── 플레이어 스킬 ──

	void OnPlayerAdded(PlayerHealth player)
	{
		if (player == null || !player.TryGetComponent(out SkillEffectEvents effects)) return;
		effects.Fired += (skillId, origin, direction) =>
		{
			if (table == null || !PlayerRoster.IsInMyPlace(player)) return;
			Play(table.SkillFiredOf(skillId), default);
		};
	}
}
