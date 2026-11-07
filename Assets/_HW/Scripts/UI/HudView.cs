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
/// - 왼쪽 위: 설정(Esc) · Lv · 버그 조각 · 보스 코어 (PlayerProgress). 그 아래 지금 장소 이름 (내 캐릭터가 있는 씬).<br/>
/// - 아래 가운데: HP(PlayerHealth), Memory(ProcessMemory: 왼쪽부터 Data·Heap, 오른쪽부터 Stack), 슬롯 8칸(Q W E R A S D F).<br/>
/// - 소환검 (#365): 아래 띠 왼쪽에 「소환검 n/5」와 5칸. SummonedSwords.Swords(박진우)를 읽기만 한다. 사용 가능 청록, 쓰는 중 어둡게, 누수 빨강.<br/>
/// - 레이드 던전 (시안 8-3): 왼쪽 위는 설정만, 바로가기 숨김. 파티원 목록(RaidPartyView)과 보스 체력 바(김도훈의 BossHealthBar + BossBarPercent)가 같은 프리팹에 있다.<br/>
/// - 슬롯 상태: 비어 있음 / 사용 가능 / 실행 중(밝게 덮음) / 쿨타임(어두운 덮개가 시계 방향으로 줄고 남은 초) / 시전 거부(잠깐 빨간 테두리).
///   공격할 수 없는 장소(로비)에서는 모두 무채색.<br/>
///   실행 중: FunctionExecutorNetwork.CastAccepted(슬롯, 작업) ~ TaskEnded(작업). 쿨타임: FunctionExecutor.GetRemainingCooldownTicks. 거부: CastRejected.<br/>
/// - 오른쪽 아래: 스킬 트리 · 레벨업 바로가기. 누르면 SkillTreeRequested / LevelUpRequested를 알린다. 스킬 트리는 로비에서만, 레벨업은 받는 화면이 있을 때만 켜진다.<br/>
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
	[SerializeField, Tooltip("슬롯에 장착된 스킬 이름 (마우스를 올리면 보이는 말풍선 글자). 슬롯 함수 안의 C 스킬, 여러 개면 「검 찌르기 +1」")] TMP_Text[] slotNames;
	[SerializeField, Tooltip("쿨타임 덮개 (Filled, Radial 360, 시계 방향)")] Image[] slotCooldowns;
	[SerializeField, Tooltip("실행 중 덮개 (밝은 색 50%)")] Image[] slotRunning;
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

			TMP_Text slotName = At(slotNames, i);
			if (slotName != null) slotName.text = SlotLabel(i); // 마우스를 올리면 칸 위 말풍선으로 보임 (SlotHoverTip)

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

	// 슬롯 이름 -----------------------------------------------------------------

	readonly string[] slotLabels = new string[SlotCount];
	float slotLabelsTime = -1f;

	// 슬롯에 보일 스킬 이름. 1초마다 다시 센다 (장착·저장이 바뀌면 따라감)
	string SlotLabel(int slot)
	{
		if (Time.unscaledTime - slotLabelsTime > 1f || slotLabelsTime < 0f)
		{
			slotLabelsTime = Time.unscaledTime;
			for (int i = 0; i < SlotCount; i++) slotLabels[i] = ComputeSlotLabel(i);
		}
		return slotLabels[slot] ?? "";
	}

	// 슬롯 내용 → 「검 찌르기」. C 스킬을 바로 넣었으면 그 이름, 함수를 넣었으면 그 안의 C 스킬(스킬 트리에 있는 것) 이름
	string ComputeSlotLabel(int slot)
	{
		if (loadout == null) return "";
		SlotLoadout.Entry entry = loadout.GetEntry(slot);
		if (entry.IsEmpty) return "";
		if (entry.Kind == SlotLoadout.EntryKind.Skill) return SkillName(entry.Id);

		FunctionData function = FindFunction(entry.Id);
		if (function == null) return "";
		List<string> skills = function.AllBlocks()
			.Where(b => b.Kind == BlockKind.Function && !b.IsUserFunction && !string.IsNullOrEmpty(b.FunctionId) && SkillTree.TryGet(b.FunctionId, out _))
			.Select(b => b.FunctionId).Distinct().ToList();
		if (skills.Count == 0) return function.CountBlocks() > 0 && !function.Name.StartsWith("slot") ? function.Name : "";
		return skills.Count == 1 ? SkillName(skills[0]) : $"{SkillName(skills[0])} +{skills.Count - 1}";
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
