using System.Collections.Generic;

/// <summary>
/// 스킬마다 어떤 캐릭터 모션을 틀지 적어 둔 표. 연출만 한다 (PlayerAnimator가 읽는다).
/// <para>
/// - 스킬마다 Animator 상태가 하나씩 있다. 상태 이름은 스킬 ID의 점(.)을 밑줄(_)로 바꾼 것 (예: c.wide_slash → c_wide_slash).<br/>
/// - 처음에는 그 상태에 「임시 모션」(Base) 클립을 넣어 둔다 (메뉴 Invoke → 플레이어 → 스킬 모션 붙이기).<br/>
/// - 나중에 스킬 전용 그림을 만들면 Animator 창에서 그 스킬 상태의 Motion만 새 클립으로 바꾸면 된다. 코드는 안 고쳐도 된다.
///   메뉴를 다시 눌러도 사람이 바꾼 Motion은 덮어쓰지 않는다.<br/>
/// - 보여 주는 시간(Duration)이 바뀌면 이 표의 숫자만 고친다.<br/>
/// - Base가 null이면 모션 없음 (이동 계열: 움직임이 보여 준다).
/// </para>
/// </summary>
public static class SkillMotionTable
{
	// 임시 모션 = 원래 있는 Animator 상태 이름
	public const string Cast = "Cast";     // 찌르기
	public const string Lunge = "Lunge";   // 뛰어들며 찌르기
	public const string Slash = "Slash";   // 휘두르기
	public const string Throw = "Throw";   // 던지기
	public const string Summon = "Summon"; // 손짓 (소환·소환검에게 명령)

	public readonly struct Entry
	{
		public readonly string Id;
		public readonly string Name;     // 알아보기 쉽게 적은 한글 이름 (게임에서는 안 씀)
		public readonly string Base;     // 전용 그림이 없을 때 쓰는 임시 모션. null이면 모션 없음
		public readonly float Duration;  // 보여 주는 시간(초)

		public Entry(string id, string name, string baseState, float duration)
		{
			Id = id;
			Name = name;
			Base = baseState;
			Duration = duration;
		}

		public string StateName => StateNameOf(Id);
	}

	// 임시 모션 고르는 기준 (#288)
	// - 캐릭터가 직접 검을 쓰면 그 동작 (찌르기 Cast, 베기 Slash, 던지기 Throw)
	// - 소환검이 대신 움직이면 손짓(Summon): 소환검 휘두르기·던지기·찌르기, 검 돌아오기, 검무
	// - 패링은 막기(Guard) 그림이 생길 때까지 Cast
	// 전용 그림이 있는 스킬은 PlayerMotionBuilder.Dedicated에 적는다 (Base는 그 클립이 없을 때 쓰는 임시 모션)
	public static readonly Entry[] All =
	{
		// 찌르기
		new Entry("c.thrust", "검 찌르기", Cast, 0.5f),
		new Entry("c.focused_thrust", "검 집중찌르기", Lunge, 0.75f),
		new Entry("c.vital_strike", "급소공략", Cast, 0.5f),
		new Entry("c.skewer", "꼬챙이", Lunge, 0.75f),
		new Entry("c.summoned_thrust", "소환검 찌르기", Summon, 0.5f),
		new Entry("c.wound_exploit", "상처 공략", Cast, 0.5f),
		new Entry("c.deep_thrust", "검 깊이찌르기", Lunge, 0.75f),
		// 던지기
		new Entry("c.sword_throw", "검 던지기", Throw, 0.5f),
		new Entry("c.summoned_throw", "소환검 던지기", Summon, 0.5f),
		new Entry("c.sword_return", "검 돌아오기", Summon, 0.5f),
		// 휘두르기
		new Entry("c.sword_slash", "검 휘두르기", Slash, 0.5f),
		new Entry("c.wide_slash", "검 크게베기", Slash, 0.5f),
		new Entry("c.summoned_slash", "소환검 휘두르기", Summon, 0.5f),
		new Entry("c.wound_slash", "상처 가르기", Slash, 0.5f),
		new Entry("c.multi_slash", "검 다중베기", Slash, 0.67f), // 전용: C_MultiSlash (#295)
		new Entry("c.battlefield_sweep", "전장 휩쓸기", Slash, 0.67f), // 전용: C_Sweep (#295)
		// 소환
		new Entry("c.summon_sword", "검 소환", Summon, 0.5f),
		new Entry("c.free_sword", "해제", Summon, 0.5f),
		new Entry("c.parry", "패링", Cast, 0.5f), // 전용: C_Guard (#295)
		new Entry("c.mark", "표식 남기기", Cast, 0.5f),
		new Entry("c.sword_dance", "검무", Summon, 0.5f),
		// 이동 (모션 없음)
		new Entry("c.sword_ride", "검 타기", null, 0f),
		new Entry("common.dash", "대시", null, 0f),
		new Entry("common.double_jump", "더블 점프", null, 0f),
	};

	static Dictionary<string, Entry> byId;

	public static bool TryGet(string skillId, out Entry entry)
	{
		if (byId == null)
		{
			byId = new Dictionary<string, Entry>();
			foreach (Entry e in All) byId[e.Id] = e;
		}
		if (skillId != null && byId.TryGetValue(skillId, out entry)) return true;
		entry = default;
		return false;
	}

	public static string StateNameOf(string skillId) => skillId.Replace('.', '_');
}
