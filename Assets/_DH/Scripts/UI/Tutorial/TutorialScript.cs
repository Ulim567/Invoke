using System;

/// <summary>튜토리얼에서 플레이어가 해 볼 일. 하나씩 TutorialTracker가 지켜본다.</summary>
public enum TutorialTask
{
	Move,
	Jump,
	Run,
	EnterTraining,
	CastQ,
	OpenEditor,
	CloseEditor,
	EnterHunting,
	GetReward,
	EnterLobby,
	OpenSkillTree,
	OpenLevelUp,
	/// <summary>장착대(SlotEquipPanel)를 염. 실제 계정은 장착대가 없어 이 일이 있는 쪽을 건너뛴다</summary>
	OpenEquip,
	/// <summary>로비의 상점 창(ShopPanel)을 엶</summary>
	OpenShop,
}

/// <summary>그 쪽에서 화면에 가리켜 줄 곳. 실제로 어느 오브젝트인지는 TutorialGuide가 내 장소를 보고 찾는다.</summary>
public enum TutorialGoal
{
	None,
	/// <summary>로비의 연습장 문</summary>
	TrainingDoor,
	/// <summary>연습장의 허수아비</summary>
	Dummy,
	/// <summary>연습장의 편집 터미널</summary>
	EditorTerminal,
	/// <summary>사냥터로 가는 길: 로비면 사냥터 문, 연습장이면 연습장 출구</summary>
	ToHunting,
	/// <summary>로비로 가는 길: 사냥터·연습장의 출구</summary>
	ToLobby,
	/// <summary>로비의 레이드 던전 문</summary>
	RaidDoor,
	/// <summary>연습장의 스킬 장착대</summary>
	EquipStation,
	/// <summary>로비의 상점 자리</summary>
	ShopStation,
}

/// <summary>
/// 튜토리얼 대사와 해 볼 일. 화면에 보이는 글자는 여기서만 고친다.
/// 대사는 문장마다 \n으로 줄을 바꾼다 (한 문장이 창 너비보다 길 때만 띄어쓰기에서 저절로 넘어간다).
/// 한 쪽(Page)은 대사 한 줄, 해 볼 일 0~3개, 가리킬 곳을 가진다. 해 볼 일이 없는 쪽은 설명만 하고 Enter로 넘긴다.
/// </summary>
public static class TutorialScript
{
	/// <summary>한 쪽에 보여 줄 수 있는 해 볼 일의 최대 개수 (화면의 줄 수)</summary>
	public const int MaxTasks = 3;

	public readonly struct Page
	{
		public readonly string Line;
		public readonly TutorialTask[] Tasks;
		public readonly TutorialGoal Goal;
		/// <summary>HUD의 Memory 막대 테두리를 깜빡이는 쪽</summary>
		public readonly bool HighlightMemory;
		/// <summary>0보다 크면 Enter를 안 눌러도 이 시간(초) 뒤에 넘어간다 (해 볼 일이 없는 쪽)</summary>
		public readonly float AutoSeconds;

		public Page(string line, TutorialGoal goal, bool highlightMemory, float autoSeconds, TutorialTask[] tasks)
		{
			Line = line;
			Goal = goal;
			HighlightMemory = highlightMemory;
			AutoSeconds = autoSeconds;
			Tasks = tasks;
		}

		/// <summary>설명만 하는 쪽 (Enter로 넘김)</summary>
		public static Page Talk(string line, TutorialGoal goal = TutorialGoal.None, bool highlightMemory = false)
			=> new Page(line, goal, highlightMemory, 0f, Array.Empty<TutorialTask>());

		/// <summary>해 볼 일이 있는 쪽 (모두 하면 저절로 넘어감)</summary>
		public static Page Do(string line, TutorialGoal goal, params TutorialTask[] tasks)
			=> new Page(line, goal, false, 0f, tasks);
	}

	public static readonly Page[] Pages =
	{
		// 시작 연출: 잠깐 보이고 저절로 넘어감
		new Page("[ . . . . ]  . . . . .", TutorialGoal.None, false, 1.5f, Array.Empty<TutorialTask>()),

		// 0쪽
		Page.Talk("[부팅] 시스템 재가동..... 잔존 기체 확인...\n전체 점검을 시작합니다."),

		// 1쪽
		Page.Do("[부팅] 시스템 복구 중...\n이동 모듈부터 점검합니다.", TutorialGoal.None,
			TutorialTask.Move, TutorialTask.Jump, TutorialTask.Run),

		// 2쪽
		Page.Do("[안내] 이제 스킬 점검을 진행하겠습니다.\n연습장으로 이동하세요.", TutorialGoal.TrainingDoor,
			TutorialTask.EnterTraining),

		// 3쪽
		Page.Do("[연습] 표적 허수아비 감지.\n스킬을 시험 가동하세요.", TutorialGoal.Dummy,
			TutorialTask.CastQ),

		// 4쪽
		Page.Talk("[경고] 스킬은 메모리를 소모합니다.\n메모리가 0이 되지 않게 주의하세요.", TutorialGoal.None, true),

		// 5쪽
		Page.Do("[도구] 편집 터미널 발견.\n블록으로 스킬 코드를 수정할 수 있습니다.", TutorialGoal.EditorTerminal,
			TutorialTask.OpenEditor),

		// 5-1쪽: 편집한 코드는 장착대에서 장착해야 한다
		// 실제 계정은 장착대가 숨겨져 있으므로(#470) TutorialView가 이 쪽을 건너뛴다
		Page.Do("[안내] 편집한 스킬 코드는 장착해야 작동합니다.", TutorialGoal.EquipStation,
			TutorialTask.OpenEquip),

		// 6쪽
		Page.Do("[임무] 전체 점검 완료.\n사냥터로 이동해 버그를 제거하고 조각을 회수하세요.", TutorialGoal.ToHunting,
			TutorialTask.EnterHunting, TutorialTask.GetReward),

		// 7-1쪽
		Page.Do("[확인] 업그레이드를 확인했습니다.\n로비로 이동하세요.", TutorialGoal.ToLobby,
			TutorialTask.EnterLobby),

		// 7-2쪽
		Page.Do("[확인] 1번 키와 2번 키를 눌러 업그레이드를 확인하세요.", TutorialGoal.None,
			TutorialTask.OpenSkillTree, TutorialTask.OpenLevelUp),

		// 7-3쪽: 로비의 상점. 열기만 한다 (사는 것까지 시키지 않음)
		// TODO(김도훈): 대사를 적는다
		Page.Do("[안내] 상점에서는 다양한 스킬과 함수를 구매할 수 있습니다.", TutorialGoal.ShopStation, TutorialTask.OpenShop),

		// 8쪽: 레이드 문을 가리키고 2초 뒤 저절로 넘어감
		new Page("[경고] 현재 강력한 버그가 감지됩니다.\n동료와 함께 이동을 권장합니다.", TutorialGoal.RaidDoor, false, 2f, Array.Empty<TutorialTask>()),

		// 9쪽
		// 마지막 쪽은 잠깐 보여 준 뒤 저절로 끝난다 (Enter로도 바로 끝남)
		new Page("[완료] 전체 점검을 완료했습니다.\n안전모드를 종료합니다.", TutorialGoal.None, false, 3f, Array.Empty<TutorialTask>()),
	};

	public static string Label(TutorialTask task) => task switch
	{
		// 폰트(Galmuri11 TMP)에 화살표 ← → ↑와 말줄임표 …가 없어 빈칸으로 보인다. 방향키는 말로 쓴다
		TutorialTask.Move => "이동 입력: 좌우 방향키",
		TutorialTask.Jump => "점프 입력: Space",
		TutorialTask.Run => "달리기 입력: Shift 누른 채 이동",
		TutorialTask.EnterTraining => "연습장 진입: 문 앞에서 위 방향키",
		TutorialTask.CastQ => "스킬 가동: Q 누른 채 좌우 방향키, 떼기",
		TutorialTask.OpenEditor => "터미널 접속: 앞에서 위 방향키",
		TutorialTask.CloseEditor => "접속 종료: ESC",
		TutorialTask.EnterHunting => "사냥터 진입: 로비 문 앞에서 위 방향키",
		TutorialTask.GetReward => "버그 조각 회수",
		TutorialTask.EnterLobby => "로비로 이동",
		TutorialTask.OpenSkillTree => "1 : 스킬 트리 확인",
		TutorialTask.OpenLevelUp => "2 : 레벨업 확인",
		TutorialTask.OpenEquip => "장착대 접속: 앞에서 위 방향키",
		TutorialTask.OpenShop => "상점 접속: 앞에서 위 방향키",
		_ => "",
	};

	/// <summary>
	/// 그 쪽에 와 있을 때만 세는 일이면 true. 미리 해 둔 것으로 치지 않는다.
	/// 「로비로 이동」은 지금 로비에 있는지를 보는 일이라, 앞에서 로비를 지나왔다고 이미 한 것이 되면 안 된다.
	/// </summary>
	public static bool OnlyOnItsPage(TutorialTask task) => task == TutorialTask.EnterLobby;
}

/// <summary>
/// 튜토리얼을 어디까지 했는지. 값만 가진다 (화면·저장·감지와 따로).
/// 한 일은 쪽과 관계없이 기억해서, 미리 해 둔 일은 그 쪽이 오면 이미 체크되어 있다 (TutorialScript.OnlyOnItsPage인 일은 제외).
/// </summary>
public class TutorialProgress
{
	readonly bool[] done = new bool[Enum.GetValues(typeof(TutorialTask)).Length];

	/// <summary>지금 쪽 번호 (0부터). 끝나면 쪽 수와 같다</summary>
	public int PageIndex { get; private set; }

	/// <summary>모든 쪽을 넘겼으면 true</summary>
	public bool Finished => PageIndex >= TutorialScript.Pages.Length;

	/// <summary>지금 쪽. 끝났으면 사용하지 않는다</summary>
	public TutorialScript.Page Page => TutorialScript.Pages[PageIndex];

	public bool IsDone(TutorialTask task) => done[(int)task];

	/// <summary>일을 했다고 적는다. 이번에 처음 한 것이면 true</summary>
	public bool Complete(TutorialTask task)
	{
		if (done[(int)task] || Finished) return false;
		if (TutorialScript.OnlyOnItsPage(task) && Array.IndexOf(Page.Tasks, task) < 0) return false;
		done[(int)task] = true;
		return true;
	}

	/// <summary>지금 쪽의 해 볼 일을 모두 했으면 true. 해 볼 일이 없는 쪽은 false (Enter나 시간으로 넘어간다)</summary>
	public bool PageCompleted
	{
		get
		{
			if (Finished) return false;
			TutorialTask[] tasks = Page.Tasks;
			if (tasks.Length == 0) return false;
			for (int i = 0; i < tasks.Length; i++) if (!done[(int)tasks[i]]) return false;
			return true;
		}
	}

	/// <summary>다음 쪽으로 넘어간다. 마지막 쪽을 넘기면 Finished가 된다</summary>
	public void Next()
	{
		if (!Finished) PageIndex++;
	}

	/// <summary>처음부터 다시</summary>
	public void Reset()
	{
		Array.Clear(done, 0, done.Length);
		PageIndex = 0;
	}
}
