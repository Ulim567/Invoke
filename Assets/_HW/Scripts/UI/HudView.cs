using System;
using System.Collections.Generic;
using System.Linq;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 중 HUD (우림이 시안 8, 8-1). 내 캐릭터의 값을 읽어서 보여 주기만 한다.
/// <para>
/// - 왼쪽 위: 설정(Esc) · Lv · 버그 조각 · 보스 코어 (PlayerProgress, 재료 그림은 박진우의 드롭 그림 첫 장). 그 아래 지금 장소 이름 (내 캐릭터가 있는 씬).<br/>
/// - 아래 가운데: HP(PlayerHealth, 닳은 부분이 흰 잔상으로 잠시 남았다가 줄어듦 #431), Memory(ProcessMemory: 왼쪽부터 Data·Heap, 오른쪽부터 Stack), 슬롯 8칸(Q W E R A S D F).<br/>
/// - 소환검 (#365): 아래 띠 왼쪽에 「소환검 n/5」와 5칸. SummonedSwords.Swords(박진우)를 읽기만 한다. 사용 가능 청록, 쓰는 중 어둡게, 누수 빨강.<br/>
///   파이썬 마법사(ManaOrbs가 있는 캐릭터, #527)는 같은 자리에 「마나 구슬 n/5」와 5칸. n은 다 쓴 구슬도 센 수. 쓸 수 있음 청록, 다 씀 빨강(#532).<br/>
/// - 레이드 던전 (시안 8-3): 왼쪽 위는 설정만, 바로가기 숨김. 파티원 목록(RaidPartyView)과 보스 체력 바(김도훈의 BossHealthBar + BossBarPercent)가 같은 프리팹에 있다.<br/>
/// - 슬롯 상태: 비어 있음 / 사용 가능 / 실행 중(밝게 덮음) / 쿨타임(어두운 덮개가 시계 방향으로 걷히고 「x.x sec」) / 시전 거부(잠깐 빨간 테두리).
///   공격할 수 없는 장소(로비)에서는 모두 무채색.<br/>
///   실행 중: FunctionExecutorNetwork.CastAccepted(슬롯, 작업) ~ TaskEnded(작업). 쿨타임: FunctionExecutor.GetRemainingCooldownTicks (함수 칸은 안의 C 스킬 중 가장 긴 것). 거부: CastRejected.<br/>
/// - 알림 목록 (#480, #536): 왼쪽 아래 띠 위에 오류와 출력을 최대 5개 쌓고, 알림마다 3초 뒤 뺀다.
///   오류는 「[Runtime Error] 이름」과 그 아래 「Q > 함수 이름 : 설명」. 문장은 FailureNotice가 만들고, 슬롯·함수 이름은 이 화면이 붙인다.
///   바로 앞 알림과 글이 같으면 새로 쌓지 않고 첫 줄 끝에 「(x4)」처럼 횟수를 붙인다.
///   알림마다 반투명 상자 하나. 정해진 크기의 영역(noticePanel) 안에서 새 알림이 아래에서 부드럽게 밀어 올리고, 영역 위쪽은 마스크가 흐리게 잘라 낸다. 시간이 다 된 알림은 제자리에서 흐려진다.
///   시전 거부는 CastRejected(메모리 부족, 컴파일 오류), 내 작업의 실행 중단·runtime error는 FunctionExecutorNetwork.OwnTaskEnded.
///   출력은 FunctionExecutorNetwork.OwnOutput을 받아 「[Q] n = 3」.<br/>
/// - 오른쪽 아래: 스킬 트리(1) · 레벨업(2) 바로가기. 누르거나 숫자 키를 누르면 SkillTreeRequested / LevelUpRequested를 알린다. 스킬 트리는 로비에서만, 레벨업은 받는 화면이 있을 때만 켜진다.<br/>
/// - 내 캐릭터는 들어간 뒤에 생기므로 매 프레임 찾고, 없으면 HUD를 숨긴다. HUD를 숨기는 창(InputBlockingPanel.HudHidden)이 열린 동안에도 숨긴다.
///   로비 창(입구, 스킬 트리)처럼 HUD를 남기는 창이 열린 동안은 보이기만 하고 누를 수 없다 (#431). HudCanvas 프리팹(Base 씬)에 붙인다. 프리팹은 메뉴 Invoke → UI → 게임 HUD 만들기.
/// </para>
/// </summary>
public class HudView : MonoBehaviour
{
	const int SlotCount = 8;
	static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };
	const string BugFragment = "bug_fragment";
	const string BossCore = "boss_core";

	/// <summary>[표시] HUD의 「스킬 트리」 버튼을 눌렀을 때. 스킬 트리 화면이 받아서 연다</summary>
	public static event Action SkillTreeRequested;
	/// <summary>[표시] HUD의 「레벨업」 버튼을 눌렀을 때. 레벨업 화면이 받아서 연다</summary>
	public static event Action LevelUpRequested;
	/// <summary>[표시] HUD의 「설정」 버튼이나 Esc를 눌렀을 때. 설정 화면이 받아서 연다</summary>
	public static event Action SettingsRequested;

	[SerializeField, Tooltip("HUD 전체. 내 캐릭터가 없으면 숨긴다")]
	GameObject root;

	[Header("왼쪽 위")]
	[SerializeField] Button settingsButton;
	[SerializeField] TMP_Text levelText;
	[SerializeField] TMP_Text bugFragmentText;
	[SerializeField] TMP_Text bossCoreText;
	[SerializeField] TMP_Text placeText;
	[SerializeField, Tooltip("레이드 던전에서 숨기는 것 (시안 8-3: Lv, 재료, 스킬 트리·레벨업 바로가기)")] GameObject[] hideInRaid;

	[Header("아래")]
	[SerializeField] Image hpFill;
	[SerializeField, Tooltip("HP 막대의 흰 잔상 (#431). 맞기 전 너비로 남았다가 빨간 쪽까지 줄어든다. 몬스터·보스 체력 바와 같은 방식")] Image hpTrail;
	[SerializeField, Min(0f), Tooltip("잔상이 줄어들기 시작할 때까지 기다리는 시간(초). 몬스터 체력 바와 같은 0.3초")] float hpTrailDelay = 0.3f;
	[SerializeField, Min(0.01f), Tooltip("잔상이 줄어드는 빠르기 (막대 전체 = 1, 초당)")] float hpTrailSpeed = 0.6f;
	[SerializeField] TMP_Text hpText;
	[SerializeField, Tooltip("왼쪽부터 차는 Data + Heap (Filled, Horizontal, Left)")] Image heapFill;
	[SerializeField, Tooltip("왼쪽부터 차는 Data만 (Heap 위에 덮임)")] Image dataFill;
	[SerializeField, Tooltip("오른쪽부터 차는 Stack (Filled, Horizontal, Right)")] Image stackFill;
	[SerializeField] TMP_Text memoryText;

	[Header("슬롯 (Q W E R A S D F 순서)")]
	[SerializeField] Image[] slotFrames;
	[SerializeField] TMP_Text[] slotKeys;
	[SerializeField, Tooltip("슬롯에 장착된 스킬 이름 (마우스를 올리면 보이는 말풍선 글자). 슬롯 함수 안의 C 스킬, 여러 개면 「검 찌르기 +1」")] TMP_Text[] slotNames;
	[SerializeField, Tooltip("쿨타임 덮개 (Filled, Radial 360, 시계 방향)")] Image[] slotCooldowns;
	[SerializeField, Tooltip("실행 중 덮개 (밝은 색 50%)")] Image[] slotRunning;
	[SerializeField, Tooltip("슬롯 아이콘 (#444, 박진우 SkillIcons). 칸 가운데 24×24. 함수 칸은 함수 안의 첫 C 스킬 아이콘")] Image[] slotIcons;
	[SerializeField, Tooltip("공격할 수 없는 장소에서 아이콘 색")] Color slotIconDisabledColor = new Color(0.45f, 0.45f, 0.5f, 1f);
	[SerializeField] Sprite slotSprite;
	[SerializeField] Sprite slotEmptySprite;
	[SerializeField] Sprite slotRejectedSprite;

	[Header("소환검 (아래 띠 왼쪽, #365)")]
	[SerializeField] TMP_Text swordText;
	[SerializeField, Tooltip("소환 순서대로 5칸")] Image[] swordPips;
	[SerializeField] Color swordEmptyColor = new Color32(0x2A, 0x22, 0x40, 0xFF);
	[SerializeField] Color swordReadyColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	[SerializeField] Color swordBusyColor = new Color32(0x2F, 0x6E, 0x68, 0xFF);
	[SerializeField] Color swordLeakedColor = new Color32(0xE0, 0x70, 0x70, 0xFF);

	[Header("오른쪽 아래")]
	[SerializeField] Button skillTreeButton;
	[SerializeField] Button levelUpButton;

	[Header("실패 알림 (#480)")]
	[SerializeField, Tooltip("왼쪽 아래 띠 위의 알림 영역(정해진 크기, 위쪽이 흐려지는 마스크). 알림이 없으면 숨긴다")] GameObject noticePanel;
	[SerializeField, Tooltip("알림 상자들을 세로로 쌓는 목록 (#536). 세로 위치를 움직여 새 알림이 부드럽게 밀어 올리게 한다")] RectTransform noticeContent;
	[SerializeField, Tooltip("알림 상자 견본 (#536). 알림 하나에 하나씩 복사해 쓴다. 바탕 Image, CanvasGroup, 글자(TMP)를 가진다")] GameObject noticeTemplate;
	[SerializeField, Min(0f), Tooltip("알림 하나를 보여 주는 시간(초)")] float noticeTime = 3f;
	[SerializeField, Min(0.01f), Tooltip("시간이 다 된 알림이 제자리에서 흐려지는 시간(초, #536). 보여 주는 시간의 끝부분이다")] float noticeFadeTime = 0.4f;
	[SerializeField, Min(0.1f), Tooltip("새 알림이 위 알림들을 밀어 올리는 빠르기 (#536). 클수록 빨리 제자리에 온다")] float noticeRiseSharpness = 12f;
	[SerializeField, Tooltip("알림 앞의 [Runtime Error], [Cast Rejected] 글자 색")] Color noticeLabelColor = new Color32(0xE0, 0x70, 0x70, 0xFF);
	[SerializeField, Tooltip("출력 알림 앞의 [Q] 글자 색 (#536). 오류와 구별되게 청록")] Color outputLabelColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	[SerializeField, Tooltip("오류 알림 둘째 줄의 「Q > 함수 이름」 글자 색 (#536). 블록 편집기의 변수 색과 같은 하늘색")] Color noticeLocationColor = new Color32(0x5E, 0xB7, 0xF2, 0xFF);
	[SerializeField, Min(1), Tooltip("한 번에 두는 알림 수 (#536). 넘치면 가장 오래된 알림부터 뺀다")] int maxNotices = 5;
	[SerializeField, Min(0), Tooltip("알림 영역 위쪽에서 흐려지는 높이 (#536). 시작할 때 영역의 마스크(RectMask2D)에 넣는다")] int noticeFadeHeight = 45;
	[SerializeField, Min(20f), Tooltip("알림 영역 너비 (#536). 시작할 때 영역에 넣는다. 이보다 넓은 글은 이 너비에서 줄을 바꾼다")] float noticeAreaWidth = 150f;

	[Header("값")]
	[SerializeField, Min(0f), Tooltip("시전 거부 빨간 테두리를 보여 주는 시간(초)")]
	float rejectFlashTime = 0.5f;
	[SerializeField] Color keyColor = Color.white;
	[SerializeField] Color keyDisabledColor = new Color32(0x4E, 0x53, 0x6A, 0xFF);
	[SerializeField, Tooltip("공격할 수 없는 장소(로비)의 슬롯 칸 색. 밝은 테두리를 어둡게 눌러 무채색으로 보이게 한다")]
	Color slotDisabledColor = new Color32(0x58, 0x5C, 0x70, 0xFF);

	// 내 캐릭터
	GameObject player;
	PlayerProgress progress;
	SummonedSwords swords;
	// 파이썬 마법사의 마나 구슬 (#527). 있으면 소환검 대신 보여 준다
	ManaOrbs orbs;
	PlayerHealth health;
	ProcessMemory memory;
	SlotLoadout loadout;
	FunctionExecutor executor;
	FunctionExecutorNetwork network;

	// 슬롯 상태
	readonly Dictionary<int, int> runningTaskSlot = new Dictionary<int, int>(); // 작업 번호 → 슬롯
	readonly float[] rejectedUntil = new float[SlotCount];
	readonly int[] cooldownTotal = new int[SlotCount]; // 이번 쿨타임이 처음 보였을 때의 tick 수 (덮개 비율의 기준)

	// 알림 하나와 그 알림을 보여 주는 상자 (#536). 상자는 noticeTemplate을 복사한 것이다
	// Count는 같은 알림이 연달아 온 횟수. 2 이상이면 첫 줄 끝에 「(x2)」를 붙인다
	sealed class Notice
	{
		public string Text;
		public int Count;
		public float Until;
		public GameObject Box;
		public TMP_Text Label;
		public CanvasGroup Group;
	}

	// 쌓인 알림(오류, 출력). 맨 앞이 가장 오래된 것(목록 맨 위), 맨 뒤가 가장 새 것(맨 아래)이다 (#536)
	// 한 줄씩 덮어쓰지 않고 쌓는 이유: 반복문 안의 출력처럼 거의 동시에 여러 알림이 와도 모두 읽을 수 있게 하기 위함
	readonly List<Notice> notices = new List<Notice>();
	// 다 쓴 상자. 알림이 올 때마다 새로 만들지 않고 다시 쓴다
	readonly List<GameObject> freeNoticeBoxes = new List<GameObject>();
	// 목록의 세로 위치. 새 알림이 오면 그 높이만큼 아래로 내렸다가 0으로 부드럽게 돌아가, 위 알림들이 툭 튀지 않고 밀려 올라가게 한다
	float noticeContentOffset;

	// HP 잔상
	float hpTrailValue = -1f;
	float hpLastValue = -1f;
	float hpTrailHoldUntil;

	CanvasGroup rootGroup;

	void Awake()
	{
		if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
		if (skillTreeButton != null) skillTreeButton.onClick.AddListener(() => SkillTreeRequested?.Invoke());
		if (levelUpButton != null) levelUpButton.onClick.AddListener(() => LevelUpRequested?.Invoke());
		if (noticeTemplate != null) noticeTemplate.SetActive(false);
		// 흐려지는 높이를 프리팹이 아니라 여기서 정한다. 값을 바꿔 볼 때 HUD 프리팹을 다시 만들지 않아도 되게 하기 위함
		// softness는 위아래에 함께 걸리므로, 아래 경계를 같은 만큼 영역 밖으로 내려(padding 음수) 아래쪽은 흐려지지 않게 한다
		// 영역 너비도 같은 이유로 여기서 정한다
		if (noticePanel != null)
		{
			RectTransform area = (RectTransform)noticePanel.transform;
			area.sizeDelta = new Vector2(noticeAreaWidth, area.sizeDelta.y);
		}
		if (noticePanel != null && noticePanel.TryGetComponent(out RectMask2D noticeMask))
		{
			noticeMask.softness = new Vector2Int(0, noticeFadeHeight);
			noticeMask.padding = new Vector4(0, -noticeFadeHeight, 0, 0);
		}
		if (root != null)
		{
			root.SetActive(false);
			if (!root.TryGetComponent(out rootGroup)) rootGroup = root.AddComponent<CanvasGroup>();
		}
	}

	void OnEnable() => GameSettings.Changed += OnSettingsChanged;

	void OnDisable()
	{
		GameSettings.Changed -= OnSettingsChanged;
		Unbind();
	}

	// 설정에서 콘솔 출력 표시를 끄면 떠 있던 알림도 바로 지운다 (#536)
	void OnSettingsChanged()
	{
		if (!GameSettings.ShowConsoleOutput) ClearNotices();
	}

	/// <summary>설정 버튼, Esc(UIShortcut). 설정 화면을 연다</summary>
	public void OpenSettings() => SettingsRequested?.Invoke();

	/// <summary>숫자 1(UIShortcut, #431). 스킬 트리 버튼을 누를 수 있을 때만 연다</summary>
	public void OpenSkillTreeByKey()
	{
		if (CanPress(skillTreeButton)) SkillTreeRequested?.Invoke();
	}

	/// <summary>숫자 2(UIShortcut, #431). 레벨업 버튼을 누를 수 있을 때만 연다</summary>
	public void OpenLevelUpByKey()
	{
		if (CanPress(levelUpButton)) LevelUpRequested?.Invoke();
	}

	// 버튼이 보이고(레이드에서는 숨김) 켜져 있을 때만. 다른 창이 열려 있으면 HUD의 UIShortcut이 맨 위가 아니라 애초에 불리지 않는다
	bool CanPress(Button button) => player != null && button != null && button.interactable && button.gameObject.activeInHierarchy;

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player) Bind(me);
		// 창(편집, 장착, 스킬 트리, 레이드 방 등)이 열린 동안은 HUD를 숨겨서 창 뒤로 겹쳐 보이지 않게 한다
		bool show = player != null && !InputBlockingPanel.HudHidden;
		if (root != null && root.activeSelf != show) root.SetActive(show);
		if (!show) return;
		// HUD를 남기는 창(로비 입구, 스킬 트리, 설정)이 열린 동안은 보이기만 하고 누를 수 없게 한다 (#431)
		bool interactive = !InputBlockingPanel.AnyOpen;
		if (rootGroup != null && rootGroup.interactable != interactive) rootGroup.interactable = rootGroup.blocksRaycasts = interactive;

		RefreshTop();
		RefreshBars();
		RefreshSlots();
		RefreshShortcuts();
		RefreshSwords();
		RefreshNotice();
	}

	// 연결 ---------------------------------------------------------------------

	void Bind(GameObject me)
	{
		Unbind();
		player = me;
		if (player == null) return;
		progress = player.GetComponent<PlayerProgress>();
		swords = player.GetComponent<SummonedSwords>();
		orbs = player.GetComponent<ManaOrbs>();
		health = player.GetComponent<PlayerHealth>();
		memory = player.GetComponent<ProcessMemory>();
		loadout = player.GetComponent<SlotLoadout>();
		// 서버의 슬롯 데이터(SetEntries)가 Bind보다 늦게 와도, 장착을 바꿔도 다음 프레임에 슬롯 이름·아이콘을 다시 센다
		if (loadout != null) loadout.Changed += OnLoadoutChanged;
		// 이전 캐릭터의 슬롯 이름·아이콘이 1초 동안 남지 않게 바로 다시 센다
		slotLabelsTime = -1f;
		executor = player.GetComponent<FunctionExecutor>();
		network = player.GetComponent<FunctionExecutorNetwork>();
		if (network != null)
		{
			network.CastAccepted += OnCastAccepted;
			network.CastRejected += OnCastRejected;
			network.TaskEnded += OnTaskEnded;
			network.OwnTaskEnded += OnOwnTaskEnded;
			network.OwnOutput += OnOwnOutput;
		}
	}

	void Unbind()
	{
		if (network != null)
		{
			network.CastAccepted -= OnCastAccepted;
			network.CastRejected -= OnCastRejected;
			network.TaskEnded -= OnTaskEnded;
			network.OwnTaskEnded -= OnOwnTaskEnded;
			network.OwnOutput -= OnOwnOutput;
		}
		if (loadout != null) loadout.Changed -= OnLoadoutChanged;
		loadout = null;
		network = null;
		player = null;
		runningTaskSlot.Clear();
		// 다른 캐릭터로 바뀌면(다시 접속 등) 이전 캐릭터의 알림을 남기지 않는다
		ClearNotices();
		hpTrailValue = hpLastValue = -1f;
	}

	void OnCastAccepted(int slot, int taskId)
	{
		if (slot >= 0 && slot < SlotCount) runningTaskSlot[taskId] = slot;
	}

	void OnTaskEnded(int taskId, FunctionTaskEndReason reason) => runningTaskSlot.Remove(taskId);

	void OnCastRejected(int slot, CastRejection reason)
	{
		// 쿨타임 중에 누른 것은 덮개가 이미 보여 주므로 빨간 테두리를 띄우지 않는다
		if (slot >= 0 && slot < SlotCount && reason != CastRejection.OnCooldown) rejectedUntil[slot] = Time.time + rejectFlashTime;
		// 컴파일 오류 거부를 내 캐릭터 언어의 이름으로 보인다(Python은 SyntaxError, #532, 이우림)
		// 함수 이름은 서버가 보내지 않으므로 내 슬롯 내용에서 찾는다 (#536)
		ShowError(FailureNotice.ForRejection(reason, LanguageFamilies.Of(PlayerCharacter.LocalLanguage)), slot, SlotFunctionName(slot));
	}

	// [내 화면] 내 작업이 끝남. 실행 중단, runtime error면 이유를 어느 슬롯·함수인지와 함께 알린다 (#480, #536)
	void OnOwnTaskEnded(int taskId, TaskEndInfo info) => ShowError(FailureNotice.ForTaskEnd(info), info.Slot, info.FunctionName);

	// [내 화면] 내 작업의 출력 함수가 출력함 (#536). 「[Q] n = 3」, 슬롯이 없는 작업은 「[함수 이름] n = 3」
	void OnOwnOutput(int slot, string functionName, string text)
	{
		string where = slot >= 0 && slot < SlotCount ? SlotKeys[slot] : functionName;
		AddNotice($"<color=#{ColorUtility.ToHtmlStringRGB(outputLabelColor)}>[{NoParse(where)}]</color> {NoParse(text)}");
	}

	// 오류 알림을 띄운다. message가 null이면(알리지 않는 이유) 아무것도 하지 않는다
	// message는 FailureNotice의 두 줄(「[분류] 이름」, 「설명」)이다. 둘째 줄 앞에 「Q > 함수 이름 : 」을 붙인다 (#536)
	// 앞의 [분류] 글자만 색을 바꾼다. 무엇이 일어났는지보다 에러라는 것을 먼저 보이게 하기 위함
	void ShowError(string message, int slot, string functionName)
	{
		if (string.IsNullOrEmpty(message)) return;
		int labelEnd = message.StartsWith("[") ? message.IndexOf(']') : -1;
		string text = labelEnd > 0
			? $"<color=#{ColorUtility.ToHtmlStringRGB(noticeLabelColor)}>{message.Substring(0, labelEnd + 1)}</color>{message.Substring(labelEnd + 1)}"
			: message;

		string location = LocationText(slot, functionName);
		if (location.Length > 0)
		{
			// 어느 슬롯·함수인지를 설명과 구별되게 파란색으로 칠한다 (#536, 이우림 결정 11/18)
			location = $"<color=#{ColorUtility.ToHtmlStringRGB(noticeLocationColor)}>{location}</color>";
			int lineBreak = text.IndexOf('\n');
			text = lineBreak >= 0
				? $"{text.Substring(0, lineBreak)}\n{location} : {text.Substring(lineBreak + 1)}"
				: $"{text}\n{location}";
		}
		AddNotice(text);
	}

	// 「Q > attack_combo」. 슬롯이 없으면 함수 이름만, 함수 이름이 없으면 슬롯 키만
	// 「>」를 쓰는 이유: HUD 글꼴(Galmuri)에 「·」, 「—」가 없어 빈칸으로 보이기 때문. 블록 목록의 소분류 표기(「함수 > 스킬」)와도 같다 (이우림 결정 11/18)
	static string LocationText(int slot, string functionName)
	{
		string key = slot >= 0 && slot < SlotCount ? SlotKeys[slot] : "";
		string name = NoParse(functionName ?? "");
		if (key.Length > 0 && name.Length > 0) return $"{key} > {name}";
		return key.Length > 0 ? key : name;
	}

	// 슬롯에 넣은 것의 이름. 스킬을 바로 넣었으면 스킬 이름, 함수를 넣었으면 그 함수의 이름, 비었으면 빈 글자
	string SlotFunctionName(int slot)
	{
		if (loadout == null || slot < 0 || slot >= SlotCount) return "";
		SlotLoadout.Entry entry = loadout.GetEntry(slot);
		if (entry.IsEmpty) return "";
		if (entry.Kind == SlotLoadout.EntryKind.Skill) return SkillName(entry.Id);
		FunctionData function = FindFunction(entry.Id);
		return function != null ? function.Name : "";
	}

	// 플레이어가 정한 글(출력 값, 함수 이름)을 글자 그대로 보인다. 「<b>」 같은 글이 TMP 서식으로 읽혀 알림 모양이 깨지지 않게 하기 위함
	static string NoParse(string text) => string.IsNullOrEmpty(text) ? "" : $"<noparse>{text}</noparse>";

	// 알림을 목록 맨 뒤에 붙인다. 최대 수를 넘으면 가장 오래된 알림부터 뺀다
	// 바로 앞 알림과 글이 같으면 새 줄을 만들지 않고 횟수만 늘리고 3초를 다시 센다 (#536)
	// 합치는 이유: 반복문 안에서 실패하거나 같은 슬롯을 여러 번 눌러 같은 오류가 이어지면, 같은 줄이 목록을 채워 화면을 가리기 때문
	void AddNotice(string text)
	{
		if (noticeTemplate == null || noticeContent == null) return;
		// 설정에서 콘솔 출력 표시를 끄면 출력·오류 알림 모두 띄우지 않는다 (#536, settings.md)
		if (!GameSettings.ShowConsoleOutput) return;
		if (noticePanel != null && !noticePanel.activeSelf) noticePanel.SetActive(true);

		Notice last = notices.Count > 0 ? notices[notices.Count - 1] : null;
		if (last != null && last.Text == text)
		{
			last.Count++;
			last.Until = Time.time + noticeTime;
			last.Group.alpha = 1f;
			SetNoticeLabel(last.Label, NoticeText(last.Text, last.Count));
			return;
		}

		// 붙이기 전의 목록 높이. 붙인 뒤의 높이와의 차이만큼만 목록을 내린다
		LayoutRebuilder.ForceRebuildLayoutImmediate(noticeContent);
		float heightBefore = noticeContent.rect.height;

		var notice = new Notice { Text = text, Count = 1, Until = Time.time + noticeTime, Box = TakeNoticeBox() };
		notice.Label = notice.Box.GetComponentInChildren<TMP_Text>(true);
		if (!notice.Box.TryGetComponent(out notice.Group)) notice.Group = notice.Box.AddComponent<CanvasGroup>();
		notice.Group.alpha = 1f;
		// 다시 쓰는 상자는 이전 글(예: 두 줄 오류)의 크기를 기억하고 있다. 바로 다시 계산해야 아래에서 잰 높이가 새 글의 높이가 된다
		SetNoticeLabel(notice.Label, NoticeText(text, 1));
		// 목록 맨 아래(마지막 자식)에 둔다. 세로로 쌓는 목록은 자식 순서대로 위에서 아래로 놓기 때문
		notice.Box.transform.SetAsLastSibling();
		notices.Add(notice);

		// 늘어난 높이만큼 목록을 내렸다가 RefreshNotice가 0으로 되돌린다. 그래서 위 알림들은 처음 자리에서 그대로 밀려 올라가고 새 상자는 아래에서 올라온다
		// 상자 하나의 높이가 아니라 목록 전체 높이 차이를 쓰는 이유: 상자 사이 간격까지 정확히 들어가고, 상자 크기를 잘못 재도 위 알림이 아래로 튀지 않게 하기 위함
		// 넘친 알림을 빼기 전에 잰다. 맨 위 알림을 빼도 아래 기준인 목록에서 다른 알림은 움직이지 않으므로 계산에 넣지 않는다
		LayoutRebuilder.ForceRebuildLayoutImmediate(noticeContent);
		noticeContentOffset -= Mathf.Max(0f, noticeContent.rect.height - heightBefore);
		// 내린 위치를 바로 적용한다. RefreshNotice까지 기다리면, 그 사이 화면이 한 번 그려질 때 위 알림들이 새 높이만큼 먼저 튀어 올랐다가
		// 다음 프레임에 다시 내려온 뒤 올라가, 아래로 내려가는 것처럼 보인다 (#536, 위치를 재어 확인)
		noticeContent.anchoredPosition = new Vector2(0f, noticeContentOffset);
		while (notices.Count > maxNotices) RemoveNotice(0);
	}

	// 상자 글자를 넣는다. 글이 영역보다 넓으면 영역 너비에서 줄을 바꾼다 (#536, 이우림 결정 11/18 (가))
	// 넓은 상자가 영역 밖으로 나가 잘리지 않게 하기 위함. 줄바꿈은 넓을 때만 켠다: 늘 켜 두면 짧은 글도 상자가 영역 너비만큼 넓어지기 때문
	void SetNoticeLabel(TMP_Text label, string text)
	{
		if (label == null) return;
		if (!label.TryGetComponent(out LayoutElement size)) size = label.gameObject.AddComponent<LayoutElement>();

		label.text = text;
		float maxWidth = NoticeMaxTextWidth();
		bool wide = maxWidth > 0f && label.GetPreferredValues(text, float.PositiveInfinity, float.PositiveInfinity).x > maxWidth;
		label.textWrappingMode = wide ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
		// 넓으면 상자 목록이 글자에 이 너비를 주고, 그 너비에서 줄을 바꾼 높이를 쓴다. 넓지 않으면 -1(정하지 않음)로 글자 길이에 맞춘다
		size.preferredWidth = wide ? maxWidth : -1f;
		label.ForceMeshUpdate();
	}

	// 상자 안 글자가 쓸 수 있는 최대 너비. 영역 너비에서 상자 안쪽 여백(좌우)을 뺀다
	float NoticeMaxTextWidth()
	{
		if (noticePanel == null || noticeTemplate == null) return 0f;
		float width = ((RectTransform)noticePanel.transform).rect.width;
		if (noticeTemplate.TryGetComponent(out VerticalLayoutGroup boxLayout)) width -= boxLayout.padding.left + boxLayout.padding.right;
		return width;
	}

	// 다 쓴 상자가 있으면 다시 쓰고, 없으면 견본을 복사한다
	GameObject TakeNoticeBox()
	{
		GameObject box;
		if (freeNoticeBoxes.Count > 0)
		{
			box = freeNoticeBoxes[freeNoticeBoxes.Count - 1];
			freeNoticeBoxes.RemoveAt(freeNoticeBoxes.Count - 1);
		}
		else
		{
			box = Instantiate(noticeTemplate, noticeContent);
			box.name = "Notice";
		}
		box.SetActive(true);
		return box;
	}

	// index번 알림을 빼고 상자를 돌려 놓는다. 목록은 아래쪽 기준이라 위 알림을 빼도 아래 알림들은 움직이지 않는다
	void RemoveNotice(int index)
	{
		GameObject box = notices[index].Box;
		box.SetActive(false);
		freeNoticeBoxes.Add(box);
		notices.RemoveAt(index);
	}

	// 알림을 모두 빼고 목록 위치를 처음으로 되돌린다. 다른 캐릭터로 바뀌었을 때 부른다
	void ClearNotices()
	{
		while (notices.Count > 0) RemoveNotice(notices.Count - 1);
		noticeContentOffset = 0f;
		if (noticeContent != null) noticeContent.anchoredPosition = Vector2.zero;
		if (noticePanel != null) noticePanel.SetActive(false);
	}

	// 알림 하나의 글. 2번 이상 왔으면 첫 줄 끝에 「(x2)」를 붙인다
	// 「×」 대신 「x」: HUD 글꼴(Galmuri)에 없는 글자일 수 있어 ASCII만 쓴다
	static string NoticeText(string text, int count)
	{
		if (count <= 1) return text;
		int lineBreak = text.IndexOf('\n');
		return lineBreak >= 0
			? $"{text.Substring(0, lineBreak)} (x{count}){text.Substring(lineBreak)}"
			: $"{text} (x{count})";
	}

	// 그리기 -------------------------------------------------------------------

	// 알림마다: 보여 주는 시간의 끝부분(noticeFadeTime)에 제자리에서 흐려지고, 시간이 다 되면 뺀다 (#536, 이우림 결정 11/18 (가))
	// 목록: 새 알림 때문에 내려 둔 위치를 0으로 부드럽게 되돌린다. 영역 위쪽을 지나는 알림은 마스크가 흐리게 잘라 낸다(HudUIBuilder)
	void RefreshNotice()
	{
		for (int i = notices.Count - 1; i >= 0; i--)
		{
			float remaining = notices[i].Until - Time.time;
			if (remaining <= 0f) RemoveNotice(i);
			else notices[i].Group.alpha = Mathf.Clamp01(remaining / noticeFadeTime);
		}

		if (noticeContent != null && noticeContentOffset != 0f)
		{
			// 남은 거리의 일정 비율씩 다가가 처음엔 빠르고 끝에선 천천히 멈춘다. 프레임 빠르기와 상관없이 같은 모양이 되게 시간으로 비율을 정한다
			noticeContentOffset = Mathf.Lerp(noticeContentOffset, 0f, 1f - Mathf.Exp(-noticeRiseSharpness * Time.deltaTime));
			if (Mathf.Abs(noticeContentOffset) < 0.5f) noticeContentOffset = 0f;
			noticeContent.anchoredPosition = new Vector2(0f, noticeContentOffset);
		}

		if (noticePanel == null) return;
		bool show = notices.Count > 0;
		if (noticePanel.activeSelf != show) noticePanel.SetActive(show);
	}

	void RefreshTop()
	{
		if (progress != null)
		{
			SetText(levelText, $"Lv. {progress.Level}");
			SetText(bugFragmentText, $"버그 조각 {progress.GetMaterialCount(BugFragment)}");
			SetText(bossCoreText, $"보스 코어 {progress.GetMaterialCount(BossCore)}");
		}
		// 장소는 PlayerPublicInfo.Place로 본다 (클라이언트에서는 플레이어가 늘 Base 씬에 있어 scene.name으로는 모름, #362)
		string place = PlayerPlace.Of(player);
		SetText(placeText, place.Length > 0 ? "> " + PlaceName(place) : "");

		bool raid = place == PlaceScenes.RaidDungeon;
		if (hideInRaid != null)
			foreach (GameObject item in hideInRaid)
				if (item != null && item.activeSelf == raid) item.SetActive(!raid);
	}

	// 받는 화면이 없으면 버튼을 끈다. 스킬 트리 창은 로비에만 있으므로 내 캐릭터가 로비에 있을 때만 켠다
	// (호스트는 모든 장소 씬을 띄워 두어 다른 장소에서도 창이 살아 있으므로, 받는 쪽이 있는지만으로는 안 됨)
	void RefreshShortcuts()
	{
		SetUsable(skillTreeButton, SkillTreeRequested != null && PlayerPlace.Of(player) == PlaceScenes.Lobby);
		SetUsable(levelUpButton, LevelUpRequested != null);
	}

	// 버튼 바탕이 투명해서 색 전환으로는 꺼진 게 안 보이므로 아이콘·글자를 함께 흐리게 한다
	static void SetUsable(Button button, bool usable)
	{
		if (button == null || button.interactable == usable) return;
		button.interactable = usable;
		if (!button.TryGetComponent(out CanvasGroup group)) group = button.gameObject.AddComponent<CanvasGroup>();
		group.alpha = usable ? 1f : 0.35f;
	}

	// 소환검: 칸마다 소환 순서대로 사용 가능(청록) / 쓰는 중(어두운 청록: 던짐, 찌르기, 검무, 검 타기 등) / 누수(빨강) / 없음
	void RefreshSwords()
	{
		if (orbs != null)
		{
			RefreshOrbs();
			return;
		}
		int count = swords != null ? swords.Swords.Count : 0;
		SetText(swordText, $"소환검 {count}/{SummonedSwords.MaxCount}");
		if (swordPips == null) return;
		for (int i = 0; i < swordPips.Length; i++)
		{
			if (swordPips[i] == null) continue;
			SummonedSword sword = i < count ? swords.Swords[i] : null;
			swordPips[i].color = sword == null ? swordEmptyColor
				: sword.State == SummonedSwordState.Ready ? swordReadyColor
				: sword.State == SummonedSwordState.Leaked ? swordLeakedColor
				: swordBusyColor;
		}
	}

	// 마나 구슬 (#527): 소환검 자리에 「마나 구슬 n/5」. n은 다 쓴 구슬도 센 수(최대 5개 제한과 같은 기준, #532)
	// 칸은 쓸 수 있는 구슬 청록, 다 쓴 구슬 빨강(누수 소환검과 같은 색: 자리를 차지하지만 쓸 수 없음), 없음
	void RefreshOrbs()
	{
		int count = Mathf.Clamp(orbs.TotalCount, 0, ManaOrbs.MaxCount);
		SetText(swordText, $"마나 구슬 {count}/{ManaOrbs.MaxCount}");
		if (swordPips == null) return;
		for (int i = 0; i < swordPips.Length; i++)
		{
			if (swordPips[i] == null) continue;
			swordPips[i].color = i >= count ? swordEmptyColor : orbs.IsSpentAt(i) ? swordLeakedColor : swordReadyColor;
		}
	}

	void RefreshBars()
	{
		if (health != null)
		{
			int max = Mathf.Max(1, health.MaxHp);
			float hp = Mathf.Clamp01((float)health.Hp / max);
			if (hpFill != null) hpFill.fillAmount = hp;
			RefreshHpTrail(hp);
			SetText(hpText, $"{health.Hp} / {health.MaxHp}");
		}
		if (memory != null)
		{
			float capacity = Mathf.Max(1, memory.Capacity);
			int used = memory.DataUsed + memory.HeapUsed + memory.StackUsed;
			if (heapFill != null) heapFill.fillAmount = Mathf.Clamp01((memory.DataUsed + memory.HeapUsed) / capacity);
			if (dataFill != null) dataFill.fillAmount = Mathf.Clamp01(memory.DataUsed / capacity);
			if (stackFill != null) stackFill.fillAmount = Mathf.Clamp01(memory.StackUsed / capacity);
			SetText(memoryText, $"{used} / {memory.Capacity}");
		}
	}

	// 흰 잔상: 줄어들면 Delay 동안 맞기 전 너비로 남았다가 Speed로 빨간 쪽까지 줄어든다. 맞는 중에는 다시 기다린다. 회복하면 바로 맞춘다
	void RefreshHpTrail(float hp)
	{
		if (hpTrailValue < 0f) hpTrailValue = hpLastValue = hp;
		if (hp < hpLastValue) hpTrailHoldUntil = Time.time + hpTrailDelay;
		hpLastValue = hp;
		if (hp >= hpTrailValue) hpTrailValue = hp;
		else if (Time.time >= hpTrailHoldUntil) hpTrailValue = Mathf.MoveTowards(hpTrailValue, hp, hpTrailSpeed * Time.deltaTime);
		if (hpTrail != null) hpTrail.fillAmount = hpTrailValue;
	}

	void RefreshSlots()
	{
		bool noCast = PlaceScenes.IsPlayerInNoCastPlace(player);
		var running = new bool[SlotCount];
		foreach (int slot in runningTaskSlot.Values) running[slot] = true;

		for (int i = 0; i < SlotCount; i++)
		{
			bool empty = loadout == null || loadout.GetEntry(i).IsEmpty;
			FunctionBlock function = null;
			bool hasFunction = !empty && loadout.TryGetFunction(i, out function);

			string label = SlotLabel(i); // 1초마다 슬롯 안의 C 스킬 목록도 새로 셈

			// 쿨타임: C 스킬 칸은 그 스킬, 함수 칸은 안의 C 스킬 중 가장 긴 것 (함수 자체는 쿨타임이 없음)
			int remaining = hasFunction && executor != null && !noCast ? RemainingCooldown(i, function) : 0;
			if (remaining <= 0) cooldownTotal[i] = 0;
			else if (remaining > cooldownTotal[i]) cooldownTotal[i] = remaining;
			bool cooling = remaining > 0 && !running[i];
			bool isRunning = running[i] && !noCast;
			bool rejected = Time.time < rejectedUntil[i] && !noCast;

			Image frame = At(slotFrames, i);
			if (frame != null)
			{
				Sprite sprite = empty ? slotEmptySprite : rejected ? slotRejectedSprite : slotSprite;
				if (sprite != null) frame.sprite = sprite;
				frame.color = empty ? new Color(1f, 1f, 1f, 0.55f) : noCast ? slotDisabledColor : Color.white;
			}

			TMP_Text slotName = At(slotNames, i);
			if (slotName != null) slotName.text = label; // 마우스를 올리면 칸 위 말풍선으로 보임 (SlotHoverTip)

			// 스킬 아이콘 (#444): C 스킬 칸은 그 스킬, 함수 칸은 함수 안의 첫 C 스킬. 없으면 숨김
			Image icon = At(slotIcons, i);
			bool hasIcon = false;
			if (icon != null)
			{
				Sprite iconSprite = empty ? null : SkillIcons.Get(slotIconIds[i]);
				if (icon.sprite != iconSprite) icon.sprite = iconSprite;
				hasIcon = iconSprite != null;
				icon.enabled = hasIcon;
				icon.color = noCast ? slotIconDisabledColor : Color.white;
			}

			TMP_Text key = At(slotKeys, i);
			if (key != null)
			{
				float seconds = executor != null ? remaining * executor.SecondsPerTick : 0f;
				key.text = cooling ? $"{seconds:0.0}\n<size=5>sec</size>" : SlotKeys[i];
				key.color = noCast || empty ? keyDisabledColor : keyColor;
				// 아이콘이 있으면 키 글자는 오른쪽 아래 구석에 작게, 쿨타임 초는 가운데에 크게
				bool corner = hasIcon && !cooling;
				key.alignment = corner ? TextAlignmentOptions.BottomRight : TextAlignmentOptions.Center;
				key.fontSize = corner ? 6 : cooling ? 8 : 9;
				key.lineSpacing = cooling ? -15f : 0f;
			}

			Image cooldown = At(slotCooldowns, i);
			if (cooldown != null)
			{
				cooldown.enabled = cooling;
				if (cooling) cooldown.fillAmount = cooldownTotal[i] > 0 ? (float)remaining / cooldownTotal[i] : 1f;
			}

			Image runningCover = At(slotRunning, i);
			if (runningCover != null) runningCover.enabled = isRunning;
		}
	}

	// 슬롯 이름 -----------------------------------------------------------------

	readonly string[] slotLabels = new string[SlotCount];
	readonly string[] slotIconIds = new string[SlotCount]; // 슬롯 아이콘으로 쓸 스킬 ID (#444)
	readonly List<string>[] slotSkillIds = new List<string>[SlotCount]; // 함수 칸 안의 C 스킬 ID들 (쿨타임, #431)
	float slotLabelsTime = -1f;

	// 슬롯 내용이 바뀌면 1초를 기다리지 않고 다음 SlotLabel에서 다시 센다
	void OnLoadoutChanged(int slot) => slotLabelsTime = -1f;

	// 슬롯에 보일 스킬 이름. 1초마다 다시 센다 (장착·저장이 바뀌면 따라감)
	string SlotLabel(int slot)
	{
		if (Time.unscaledTime - slotLabelsTime > 1f || slotLabelsTime < 0f)
		{
			slotLabelsTime = Time.unscaledTime;
			for (int i = 0; i < SlotCount; i++) slotLabels[i] = ComputeSlotLabel(i, out slotIconIds[i], out slotSkillIds[i]);
		}
		return slotLabels[slot] ?? "";
	}

	// 슬롯 내용 → 「검 찌르기」. C 스킬을 바로 넣었으면 그 이름, 함수를 넣었으면 그 안의 C 스킬(스킬 트리에 있는 것) 이름
	// iconSkillId: 슬롯 아이콘으로 쓸 스킬 ID (#444). C 스킬 칸은 그 스킬, 함수 칸은 함수 안의 첫 C 스킬, 없으면 null
	// skillIds: 함수 칸 안의 C 스킬 ID들 (쿨타임을 볼 것). C 스킬 칸·빈 칸은 null
	string ComputeSlotLabel(int slot, out string iconSkillId, out List<string> skillIds)
	{
		iconSkillId = null;
		skillIds = null;
		if (loadout == null) return "";
		SlotLoadout.Entry entry = loadout.GetEntry(slot);
		if (entry.IsEmpty) return "";
		if (entry.Kind == SlotLoadout.EntryKind.Skill)
		{
			iconSkillId = entry.Id;
			return SkillName(entry.Id);
		}

		FunctionData function = FindFunction(entry.Id);
		if (function == null) return "";
		List<string> skills = function.AllBlocks()
			.Where(b => b.Kind == BlockKind.Function && !b.IsUserFunction && !string.IsNullOrEmpty(b.FunctionId) && SkillTree.TryGet(b.FunctionId, out _))
			.Select(b => b.FunctionId).Distinct().ToList();
		skillIds = skills;
		if (skills.Count == 0) return function.CountBlocks() > 0 && !function.Name.StartsWith("slot") ? function.Name : "";
		iconSkillId = skills[0];
		return skills.Count == 1 ? SkillName(skills[0]) : $"{SkillName(skills[0])} +{skills.Count - 1}";
	}

	// 남은 쿨타임(tick). C 스킬 칸은 그 스킬, 함수 칸은 안의 C 스킬마다 PredefinedFunctionRegistry로 스킬을 꺼내 물어보고 가장 긴 것
	int RemainingCooldown(int slot, FunctionBlock function)
	{
		int remaining = executor.GetRemainingCooldownTicks(function);
		List<string> skills = slotSkillIds[slot];
		if (function is PredefinedFunction || skills == null) return remaining;
		foreach (string id in skills)
			if (PredefinedFunctionRegistry.TryGet(id, out PredefinedFunction skill))
				remaining = Mathf.Max(remaining, executor.GetRemainingCooldownTicks(skill));
		return remaining;
	}

	// 실제 계정은 서버가 보낸 함수 목록, 임시 계정은 이 컴퓨터의 함수 파일에서 찾는다
	FunctionData FindFunction(string guid)
	{
		if (network != null && network.UsesServerData && network.ReceivedFunctions != null)
			return network.ReceivedFunctions.FirstOrDefault(f => f.Guid == guid);
		return FunctionStorage.Load(guid);
	}

	static string SkillName(string id) => SkillTree.TryGet(id, out SkillTreeNode node) ? node.DisplayName : id;

	// 도구 ---------------------------------------------------------------------

	static T At<T>(T[] array, int index) where T : class => array != null && index < array.Length ? array[index] : null;

	static void SetText(TMP_Text text, string value)
	{
		if (text != null && text.text != value) text.text = value;
	}

	/// <summary>장소 씬 이름의 한글 이름 (place-scenes.md). 연습장·레이드 던전은 들어갈 때마다 따로 열려도 씬 이름은 같다</summary>
	public static string PlaceName(string sceneName) => sceneName switch
	{
		PlaceScenes.Lobby => "로비",
		PlaceScenes.HuntingGround => "사냥터 1",
		PlaceScenes.HuntingGround2 => "사냥터 2",
		PlaceScenes.HuntingGround3 => "사냥터 3",
		PlaceScenes.TrainingRoom => "연습장",
		PlaceScenes.RaidDungeon => "레이드 던전",
		_ => sceneName,
	};
}
