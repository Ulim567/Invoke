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
/// - 레이드 던전 (시안 8-3): 왼쪽 위는 설정만, 바로가기 숨김. 파티원 목록(RaidPartyView)과 보스 체력 바(김도훈의 BossHealthBar + BossBarPercent)가 같은 프리팹에 있다.<br/>
/// - 슬롯 상태: 비어 있음 / 사용 가능 / 실행 중(밝게 덮음) / 쿨타임(어두운 덮개가 시계 방향으로 걷히고 「x.x sec」) / 시전 거부(잠깐 빨간 테두리).
///   공격할 수 없는 장소(로비)에서는 모두 무채색.<br/>
///   실행 중: FunctionExecutorNetwork.CastAccepted(슬롯, 작업) ~ TaskEnded(작업). 쿨타임: FunctionExecutor.GetRemainingCooldownTicks (함수 칸은 안의 C 스킬 중 가장 긴 것). 거부: CastRejected.<br/>
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
	PlayerHealth health;
	ProcessMemory memory;
	SlotLoadout loadout;
	FunctionExecutor executor;
	FunctionExecutorNetwork network;

	// 슬롯 상태
	readonly Dictionary<int, int> runningTaskSlot = new Dictionary<int, int>(); // 작업 번호 → 슬롯
	readonly float[] rejectedUntil = new float[SlotCount];
	readonly int[] cooldownTotal = new int[SlotCount]; // 이번 쿨타임이 처음 보였을 때의 tick 수 (덮개 비율의 기준)

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
		if (root != null)
		{
			root.SetActive(false);
			if (!root.TryGetComponent(out rootGroup)) rootGroup = root.AddComponent<CanvasGroup>();
		}
	}

	void OnDisable() => Unbind();

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
	}

	// 연결 ---------------------------------------------------------------------

	void Bind(GameObject me)
	{
		Unbind();
		player = me;
		if (player == null) return;
		progress = player.GetComponent<PlayerProgress>();
		swords = player.GetComponent<SummonedSwords>();
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
		PlaceScenes.HuntingGround => "사냥터",
		PlaceScenes.TrainingRoom => "연습장",
		PlaceScenes.RaidDungeon => "레이드 던전",
		_ => sceneName,
	};
}
