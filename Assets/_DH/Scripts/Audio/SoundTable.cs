using System;
using UnityEngine;

/// <summary>
/// 어떤 상황에 어떤 소리를 낼지 적어 두는 표 (#436). 소리를 더할 때는 코드를 고치지 않고 이 표(SoundTable 에셋)에 줄을 더한다.
/// <para>
/// - BGM: 장소(씬 이름)마다 곡 하나. BgmPlayer가 읽는다.<br/>
/// - 몬스터: 프리팹 이름마다 나타남·피격·사망·내려찍기·던지기·소환 소리. SfxPlayer가 읽는다.<br/>
/// - 스킬: 스킬 ID마다 판정이 나간 순간(SkillEffectEvents.Fired)의 소리. SfxPlayer가 읽는다.<br/>
/// - 공통: 플레이어 피격·사망, UI 버튼 클릭, 스킬 해금 성공. SfxPlayer가 읽는다.<br/>
/// - 소리 칸에 여러 개를 넣으면 그중 하나를 랜덤으로 낸다. 비워 두면 소리를 내지 않는다.
/// </para>
/// <para>사용법: Project 창 Create → Invoke → Sound Table로 만든다. Base 씬의 Sound 오브젝트(BgmPlayer, SfxPlayer)에 넣는다.</para>
/// </summary>
[CreateAssetMenu(menuName = "Invoke/Sound Table", fileName = "SoundTable")]
public class SoundTable : ScriptableObject
{
	/// <summary>장소 하나의 BGM.</summary>
	[Serializable]
	public struct PlaceBgm
	{
		[Tooltip("장소 씬 이름 (Lobby, HuntingGround, TrainingRoom, RaidDungeon)")]
		public string place;

		[Tooltip("반복 재생할 곡")]
		public AudioClip clip;
	}

	/// <summary>몬스터 하나의 소리 묶음. 칸마다 여러 개를 넣으면 랜덤.</summary>
	[Serializable]
	public struct MonsterSounds
	{
		[Tooltip("몬스터 프리팹 이름 (Leak, Loop, Worm, Pillar, Gateway, GatewayClone)")]
		public string monster;

		[Tooltip("이 화면에 나타날 때 (예: 기둥 생성)")]
		public AudioClip[] appear;

		[Tooltip("맞을 때 (Monster.HitEffectPlayed)")]
		public AudioClip[] hit;

		[Tooltip("죽을 때 (Monster.DeathEffectPlayed)")]
		public AudioClip[] death;

		[Tooltip("내려찍기 (GatewayAI.SlamEffectPlayed: 패킷 낙하·때리기)")]
		public AudioClip[] slam;

		[Tooltip("던지기·쏘기 (ProjectileAttack.FireEffectPlayed)")]
		public AudioClip[] fire;

		[Tooltip("소환 (GatewayAI.SummonEffectPlayed: 웜 소환)")]
		public AudioClip[] summon;

		[Tooltip("전체 공격 기 모으기 시작 (GatewayPhases.ChargeEffectPlayed)")]
		public AudioClip[] charge;

		[Tooltip("전체 공격 폭발 (GatewayPhases.OverloadEffectPlayed)")]
		public AudioClip[] overload;

		[Tooltip("기둥을 다 부숴 그로기 (GatewayPhases.GroggyEffectPlayed)")]
		public AudioClip[] groggy;
	}

	/// <summary>몬스터·스킬이 아닌 공통 소리. 칸마다 여러 개를 넣으면 랜덤.</summary>
	[Serializable]
	public struct CommonSounds
	{
		[Tooltip("플레이어가 맞을 때 (PlayerHealth.HitEffectPlayed, 내 장소의 플레이어)")]
		public AudioClip[] playerHit;

		[Tooltip("플레이어가 죽을 때 (PlayerHealth.DeathEffectPlayed, 내 장소의 플레이어)")]
		public AudioClip[] playerDeath;

		[Tooltip("UI 버튼을 누를 때 (화면의 모든 Button)")]
		public AudioClip[] uiClick;

		[Tooltip("스킬 트리 해금 성공 (PlayerProgress.UnlockAnswered가 Unlocked, 내 캐릭터)")]
		public AudioClip[] skillUnlock;
	}

	/// <summary>스킬 하나의 소리.</summary>
	[Serializable]
	public struct SkillSound
	{
		[Tooltip("스킬 ID (PredefinedFunctionIds, 예: c.thrust)")]
		public string skillId;

		[Tooltip("판정이 나간 순간 (SkillEffectEvents.Fired)")]
		public AudioClip[] fired;
	}

	[SerializeField] PlaceBgm[] bgm;
	[SerializeField] MonsterSounds[] monsters;
	[SerializeField] SkillSound[] skills;
	[SerializeField] CommonSounds common;

	/// <summary>공통 소리 (플레이어 피격·사망, UI 클릭, 스킬 해금).</summary>
	public CommonSounds Common => common;

	/// <summary>장소의 BGM. 없으면 null.</summary>
	public AudioClip BgmOf(string place)
	{
		if (bgm == null || string.IsNullOrEmpty(place)) return null;
		for (int i = 0; i < bgm.Length; i++)
			if (bgm[i].place == place) return bgm[i].clip;
		return null;
	}

	/// <summary>몬스터 이름(프리팹 이름, "(Clone)"은 떼고 찾음)의 소리 묶음. 있으면 true.</summary>
	public bool TryGetMonster(string monsterName, out MonsterSounds sounds)
	{
		sounds = default;
		if (monsters == null || string.IsNullOrEmpty(monsterName)) return false;
		string key = StripClone(monsterName);
		for (int i = 0; i < monsters.Length; i++)
		{
			if (monsters[i].monster != key) continue;
			sounds = monsters[i];
			return true;
		}
		return false;
	}

	/// <summary>스킬의 판정 소리. 없으면 null.</summary>
	public AudioClip[] SkillFiredOf(string skillId)
	{
		if (skills == null || string.IsNullOrEmpty(skillId)) return null;
		for (int i = 0; i < skills.Length; i++)
			if (skills[i].skillId == skillId) return skills[i].fired;
		return null;
	}

	static string StripClone(string objectName)
	{
		const string clone = "(Clone)";
		return objectName.EndsWith(clone) ? objectName.Substring(0, objectName.Length - clone.Length).TrimEnd() : objectName;
	}
}
