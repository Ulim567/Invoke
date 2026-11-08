using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯 장착 화면. 저장한 블록 함수와 C 스킬을 슬롯 8칸(Q W E R A S D F)에 넣고 뺀다. 연습장의 장착대(PanelStation)가 연다.
/// <para>
/// - 장착은 내 캐릭터의 <c>SlotLoadout</c>(박진우) API만 부른다: EquipFunction, EquipSkill, Unequip, CodeSize, Changed (slot-caster.md).<br/>
///   장착이 바뀌면 FunctionExecutorNetwork가 서버로 슬롯과 함수를 다시 보낸다 (이우림, network.md).<br/>
/// - 쓰는 법: 위의 슬롯 카드를 눌러 고른 뒤 아래 「내 함수」나 「C 스킬」에서 누르면 그 칸에 들어간다. 「칸 비우기」는 고른 칸을 비운다.<br/>
/// - 실제 계정(FunctionExecutorNetwork.UsesServerData)은 슬롯 8개가 늘 슬롯 함수 slot1~slot8이라 장착을 바꾸지 않는다 (이우림 #239).
///   이때는 슬롯 카드에 slot1~slot8만 보여 주고, 슬롯 함수는 편집 터미널에서 고치라고 안내한다.<br/>
/// - 카드와 목록 줄은 모두 프리팹에서 만들고, 지우는 일은 Clear 한 곳에서 한다 (CLAUDE.md 코드 작성 규칙 3, 4).
/// </para>
/// </summary>
public class SlotEquipPanel : InputBlockingPanel
{
	static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };

	[Header("프리팹")]
	[SerializeField, Tooltip("슬롯 카드 (SlotCard 프리팹)")]
	SlotCardView slotCardPrefab;

	[SerializeField, Tooltip("목록 한 줄 (ListItem 프리팹)")]
	ListItemView itemPrefab;

	[Header("화면")]
	[SerializeField, Tooltip("슬롯 카드 8개가 만들어질 줄 (Horizontal Layout Group)")]
	RectTransform slotRow;

	[SerializeField, Tooltip("「내 함수」 목록 (Vertical 또는 Grid Layout Group)")]
	RectTransform functionList;

	[SerializeField, Tooltip("「C 스킬」 목록 (Grid Layout Group)")]
	RectTransform skillList;

	[SerializeField, Tooltip("아래 줄 글자: 고른 칸, 코드 영역, 안내")]
	TMP_Text infoText;

	[SerializeField, Tooltip("「칸 비우기」 버튼")]
	Button clearButton;

	[SerializeField, Tooltip("슬롯 줄 머리글 오른쪽 안내 글자. 비워도 된다")]
	TMP_Text hintText;

	[Header("색")]
	[SerializeField, Tooltip("색 띠: 내 함수")]
	Color functionTagColor = new Color32(0x3F, 0x8A, 0x80, 0xFF);

	[SerializeField, Tooltip("색 띠: C 스킬")]
	Color skillTagColor = new Color32(0x6A, 0x5A, 0xC9, 0xFF);

	[SerializeField, Tooltip("색 띠: 공통 이동 (대시, 더블점프)")]
	Color commonTagColor = new Color32(0xC9, 0xA0, 0x5A, 0xFF);

	[SerializeField, Tooltip("아래 줄의 강조 색 (고른 칸 글자)")]
	Color accentColor = new Color32(0xFF, 0xCC, 0x4D, 0xFF);

	[SerializeField, Tooltip("아래 줄 안내 색: 잘 됐을 때")]
	Color okColor = new Color32(0x8A, 0xD0, 0xA0, 0xFF);

	[SerializeField, Tooltip("아래 줄 안내 색: 안 됐을 때")]
	Color errorColor = new Color32(0xFF, 0x8A, 0x96, 0xFF);

	SlotLoadout loadout;
	bool fixedSlots; // 실제 계정이면 true: 슬롯 함수가 고정이라 장착 목록을 만들지 않는다
	int selectedSlot;
	string message = "";
	bool messageIsError;
	readonly List<SlotCardView> slotCards = new List<SlotCardView>();
	readonly List<GameObject> listItems = new List<GameObject>();

	/// <summary>
	/// 장착 화면을 연다. 그 플레이어의 SlotLoadout을 쓴다.
	/// </summary>
	public override void OpenFor(PlayerInputReader player)
	{
		SlotLoadout next = player != null ? player.GetComponent<SlotLoadout>() : null;
		if (next == null)
		{
			Debug.LogError("[SlotEquipPanel] 내 캐릭터에 SlotLoadout이 없어서 장착 화면을 열 수 없음");
			return;
		}

		if (loadout != null) loadout.Changed -= OnLoadoutChanged;
		loadout = next;
		loadout.Changed += OnLoadoutChanged;

		fixedSlots = player.TryGetComponent(out FunctionExecutorNetwork network) && network.UsesServerData;
		SetMessage(fixedSlots ? "실제 계정은 슬롯 함수가 고정: 편집 터미널에서 slot1~slot8을 고치세요" : "");
		if (hintText != null) hintText.text = fixedSlots ? "슬롯 함수는 편집 터미널에서 고칩니다" : "칸을 고르고 아래에서 넣을 것을 누르세요";
		if (clearButton != null) clearButton.gameObject.SetActive(!fixedSlots);

		base.OpenFor(player);
		BuildSlots();
		BuildLists(); // 편집 화면에서 새로 저장한 함수도 보이도록 열 때마다 다시 만든다
		Refresh();
	}

	/// <summary>
	/// 임시 계정(이 컴퓨터의 파일을 쓰는 플레이어)만 쓸 수 있다. 실제 계정은 슬롯이 늘 slot1~slot8이라 장착할 것이 없으므로 장착대를 숨긴다 (#470).
	/// </summary>
	public override bool IsAvailableFor(PlayerInputReader player) =>
		player == null || !player.TryGetComponent(out FunctionExecutorNetwork network) || !network.UsesServerData;

	// Awake는 InputBlockingPanel이 창을 숨기는 데 쓰므로 여기서는 Start를 쓴다
	void Start()
	{
		if (clearButton != null) clearButton.onClick.AddListener(ClearSelectedSlot);
	}

	void OnDestroy()
	{
		if (loadout != null) loadout.Changed -= OnLoadoutChanged;
	}

	void OnLoadoutChanged(int slot) => Refresh();

	// 화면 만들기 ---------------------------------------------------------------

	void BuildSlots()
	{
		if (slotCards.Count > 0 || slotCardPrefab == null || slotRow == null) return;
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			int slot = i;
			SlotCardView card = Instantiate(slotCardPrefab, slotRow);
			card.Button.onClick.AddListener(() => SelectSlot(slot));
			slotCards.Add(card);
		}
	}

	void BuildLists()
	{
		Clear();
		if (fixedSlots || itemPrefab == null) return;

		// 블록 함수 (편집 화면에서 저장한 것)
		if (functionList != null)
		{
			GlobalVariablesData globals = GlobalVariablesStorage.Load();
			foreach (FunctionData function in FunctionStorage.LoadAll())
			{
				string guid = function.Guid;
				string name = function.Name;
				// 컴파일 오류가 있는 함수는 흐리게 「오류」로 보이고 넣을 수 없다 (FunctionChecker, 편집 화면의 컴파일과 같은 검사)
				if (FunctionChecker.HasErrors(function, globals))
				{
					CreateItem(functionList, name, "오류", functionTagColor, () =>
					{
						SetMessage($"{name}에 컴파일 오류가 있어서 넣을 수 없어요. 편집 터미널에서 고치세요", true);
						Refresh();
					}, true);
					continue;
				}
				CreateItem(functionList, name, "", functionTagColor, () => Equip(loadout.EquipFunction(selectedSlot, guid), name));
			}
		}

		// C 스킬, 공통 이동 (시험용 test.는 빼고)
		if (skillList != null)
		{
			foreach (PredefinedFunction skill in PredefinedFunctionRegistry.All)
			{
				string id = skill.Id;
				if (!(id.StartsWith("c.") || id.StartsWith("common.")) || !SlotLoadout.IsEquippableSkill(id)) continue;
				string name = SkillName(id);
				Color tag = id.StartsWith("common.") ? commonTagColor : skillTagColor;
				CreateItem(skillList, name, "", tag, () => Equip(loadout.EquipSkill(selectedSlot, id), name));
			}
		}
	}

	void CreateItem(RectTransform parent, string name, string detail, Color tag, System.Action onClick, bool broken = false)
	{
		ListItemView item = Instantiate(itemPrefab, parent);
		item.Show("", name, detail, false, broken, broken);
		item.SetTagColor(tag);
		item.Button.onClick.AddListener(() => onClick());
		listItems.Add(item.gameObject);
	}

	void Clear()
	{
		foreach (GameObject item in listItems)
		{
			if (item != null) Destroy(item);
		}
		listItems.Clear();
	}

	// 버튼 ---------------------------------------------------------------------

	void SelectSlot(int slot)
	{
		if (fixedSlots) return; // 실제 계정은 칸을 골라 넣을 것이 없다
		selectedSlot = slot;
		SetMessage("");
		Refresh();
	}

	void ClearSelectedSlot()
	{
		if (loadout == null || fixedSlots) return;
		loadout.Unequip(selectedSlot);
		SetMessage($"{SlotKeys[selectedSlot]} 칸을 비움");
		Refresh();
	}

	void Equip(bool ok, string name)
	{
		SetMessage(ok ? $"{SlotKeys[selectedSlot]} 칸에 {name}" : $"{name}은(는) 넣을 수 없음", !ok);
		Refresh();
	}

	void SetMessage(string text, bool error = false)
	{
		message = text;
		messageIsError = error;
	}

	// 다시 그리기 -----------------------------------------------------------------

	void Refresh()
	{
		if (loadout == null) return;

		for (int i = 0; i < slotCards.Count; i++)
		{
			if (fixedSlots)
			{
				slotCards[i].Show(SlotKeys[i], ProgressRules.SlotFunctionName(i), false, false);
				continue;
			}
			SlotLoadout.Entry entry = loadout.GetEntry(i);
			slotCards[i].Show(SlotKeys[i], EntryName(entry), entry.IsEmpty, i == selectedSlot);
		}

		if (infoText != null)
		{
			string accent = ColorUtility.ToHtmlStringRGB(accentColor);
			string note = ColorUtility.ToHtmlStringRGB(messageIsError ? errorColor : okColor);
			string head = fixedSlots
				? $"코드 영역 {loadout.CodeSize}"
				: $"고른 칸 <color=#{accent}>{SlotKeys[selectedSlot]}</color>    코드 영역 {loadout.CodeSize}";
			infoText.text = head + (string.IsNullOrEmpty(message) ? "" : $"    <color=#{note}>{message}</color>");
		}
	}

	static string EntryName(SlotLoadout.Entry entry)
	{
		if (entry.IsEmpty) return "";
		if (entry.Kind == SlotLoadout.EntryKind.Skill) return SkillName(entry.Id);
		FunctionData function = FunctionStorage.Load(entry.Id);
		if (function == null) return "(지워진 함수)";
		return FunctionChecker.HasErrors(function, GlobalVariablesStorage.Load()) ? $"{function.Name} (오류)" : function.Name;
	}

	static string SkillName(string id) => SkillTable.TryGet(id, out SkillData data) ? data.DisplayName : id;
}
