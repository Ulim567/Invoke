using System;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임 중 HUD (우림이 시안 8, 8-1). 내 캐릭터의 값을 읽어서 보여 주기만 한다.
/// <para>
/// - 왼쪽 위: 설정(Esc) · Lv · 버그 조각 · 보스 코어 (PlayerProgress). 그 아래 지금 장소 이름 (내 캐릭터가 있는 씬).<br/>
/// - 아래 가운데: HP(PlayerHealth), Memory(ProcessMemory: 왼쪽부터 Data·Heap, 오른쪽부터 Stack), 슬롯 8칸(Q W E R A S D F).<br/>
/// - 슬롯 상태: 비어 있음 / 사용 가능 / 실행 중(밝게 덮음) / 쿨타임(어두운 덮개가 시계 방향으로 줄고 남은 초) / 시전 거부(잠깐 빨간 테두리).
///   공격할 수 없는 장소(로비)에서는 모두 무채색.<br/>
///   실행 중: FunctionExecutorNetwork.CastAccepted(슬롯, 작업) ~ TaskEnded(작업). 쿨타임: FunctionExecutor.GetRemainingCooldownTicks. 거부: CastRejected.<br/>
/// - 오른쪽 아래: 스킬 트리 · 레벨업 바로가기. 누르면 SkillTreeRequested / LevelUpRequested를 알린다 (화면 연결은 다음 이슈).<br/>
/// - 레이드 던전 (시안 8-3): 왼쪽 위는 설정만, 바로가기 숨김. 파티원 목록(RaidPartyView)과 보스 체력 바(김도훈의 BossHealthBar + BossBarPercent)가 같은 프리팹에 있다.<br/>
/// - 내 캐릭터는 들어간 뒤에 생기므로 매 프레임 찾고, 없으면 HUD를 숨긴다. 창(InputBlockingPanel)이 열린 동안에도 숨긴다. HudCanvas 프리팹(Base 씬)에 붙인다. 프리팹은 메뉴 Invoke → UI → 게임 HUD 만들기.
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
	[SerializeField] TMP_Text hpText;
	[SerializeField, Tooltip("왼쪽부터 차는 Data + Heap (Filled, Horizontal, Left)")] Image heapFill;
	[SerializeField, Tooltip("왼쪽부터 차는 Data만 (Heap 위에 덮임)")] Image dataFill;
	[SerializeField, Tooltip("오른쪽부터 차는 Stack (Filled, Horizontal, Right)")] Image stackFill;
	[SerializeField] TMP_Text memoryText;

	[Header("슬롯 (Q W E R A S D F 순서)")]
	[SerializeField] Image[] slotFrames;
	[SerializeField] TMP_Text[] slotKeys;
	[SerializeField, Tooltip("쿨타임 덮개 (Filled, Radial 360, 시계 방향)")] Image[] slotCooldowns;
	[SerializeField, Tooltip("실행 중 덮개 (밝은 색 50%)")] Image[] slotRunning;
	[SerializeField] Sprite slotSprite;
	[SerializeField] Sprite slotEmptySprite;
	[SerializeField] Sprite slotRejectedSprite;

	[Header("오른쪽 아래")]
	[SerializeField] Button skillTreeButton;
	[SerializeField] Button levelUpButton;

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
	PlayerHealth health;
	ProcessMemory memory;
	SlotLoadout loadout;
	FunctionExecutor executor;
	FunctionExecutorNetwork network;

	// 슬롯 상태
	readonly Dictionary<int, int> runningTaskSlot = new Dictionary<int, int>(); // 작업 번호 → 슬롯
	readonly float[] rejectedUntil = new float[SlotCount];
	readonly int[] cooldownTotal = new int[SlotCount]; // 이번 쿨타임이 처음 보였을 때의 tick 수 (덮개 비율의 기준)

	void Awake()
	{
		if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
		if (skillTreeButton != null) skillTreeButton.onClick.AddListener(() => SkillTreeRequested?.Invoke());
		if (levelUpButton != null) levelUpButton.onClick.AddListener(() => LevelUpRequested?.Invoke());
		if (root != null) root.SetActive(false);
	}

	void OnDisable() => Unbind();

	/// <summary>설정 버튼, Esc(UIShortcut). 설정 화면을 연다</summary>
	public void OpenSettings() => SettingsRequested?.Invoke();

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		if (me != player) Bind(me);
		// 창(편집, 장착, 스킬 트리, 레이드 방 등)이 열린 동안은 HUD를 숨겨서 창 뒤로 겹쳐 보이지 않게 한다
		bool show = player != null && !InputBlockingPanel.HudHidden;
		if (root != null && root.activeSelf != show) root.SetActive(show);
		if (!show) return;

		RefreshTop();
		RefreshBars();
		RefreshSlots();
	}

	// 연결 ---------------------------------------------------------------------

	void Bind(GameObject me)
	{
		Unbind();
		player = me;
		if (player == null) return;
		progress = player.GetComponent<PlayerProgress>();
		health = player.GetComponent<PlayerHealth>();
		memory = player.GetComponent<ProcessMemory>();
		loadout = player.GetComponent<SlotLoadout>();
		executor = player.GetComponent<FunctionExecutor>();
		network = player.GetComponent<FunctionExecutorNetwork>();
		if (network != null)
		{
			network.CastAccepted += OnCastAccepted;
			network.CastRejected += OnCastRejected;
			network.TaskEnded += OnTaskEnded;
		}
	}

	void Unbind()
	{
		if (network != null)
		{
			network.CastAccepted -= OnCastAccepted;
			network.CastRejected -= OnCastRejected;
			network.TaskEnded -= OnTaskEnded;
		}
		network = null;
		player = null;
		runningTaskSlot.Clear();
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
	}

	// 그리기 -------------------------------------------------------------------

	void RefreshTop()
	{
		if (progress != null)
		{
			SetText(levelText, $"Lv. {progress.Level}");
			SetText(bugFragmentText, $"버그 조각 {progress.GetMaterialCount(BugFragment)}");
			SetText(bossCoreText, $"보스 코어 {progress.GetMaterialCount(BossCore)}");
		}
		SetText(placeText, "> " + PlaceName(player.scene.name));

		bool raid = player.scene.name == PlaceScenes.RaidDungeon;
		if (hideInRaid != null)
			foreach (GameObject item in hideInRaid)
				if (item != null && item.activeSelf == raid) item.SetActive(!raid);
	}

	void RefreshBars()
	{
		if (health != null)
		{
			int max = Mathf.Max(1, health.MaxHp);
			if (hpFill != null) hpFill.fillAmount = Mathf.Clamp01((float)health.Hp / max);
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

			// 쿨타임
			int remaining = hasFunction && executor != null && !noCast ? executor.GetRemainingCooldownTicks(function) : 0;
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

			TMP_Text key = At(slotKeys, i);
			if (key != null)
			{
				float seconds = executor != null ? remaining * executor.SecondsPerTick : 0f;
				key.text = cooling ? seconds.ToString("0.0") : SlotKeys[i];
				key.color = noCast || empty ? keyDisabledColor : keyColor;
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
		PlaceScenes.HuntingGround => "사냥터",
		PlaceScenes.TrainingRoom => "연습장",
		PlaceScenes.RaidDungeon => "레이드 던전",
		_ => sceneName,
	};
}
