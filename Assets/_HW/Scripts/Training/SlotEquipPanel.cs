using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 슬롯 장착 화면. 저장한 블록 함수와 C 스킬을 슬롯 8칸(Q W E R A S D F)에 넣고 뺀다. 연습장의 장착대(PanelStation)가 연다.
/// <para>
/// - 장착은 내 캐릭터의 <c>SlotLoadout</c>(박진우) API만 부른다: EquipFunction, EquipSkill, Unequip, CodeSize, Changed (slot-caster.md).<br/>
///   장착이 바뀌면 FunctionExecutorNetwork가 서버로 슬롯과 함수를 다시 보낸다 (이우림, network.md).<br/>
/// - 쓰는 법: 위의 슬롯 칸을 눌러 고른 뒤 아래 목록에서 함수나 스킬을 누르면 그 칸에 들어간다. 「비우기」는 고른 칸을 비운다.<br/>
/// - 버튼은 모두 Button Prefab에서 만들고, 지우는 일은 ClearButtons 한 곳에서 한다 (CLAUDE.md 코드 작성 규칙 3, 4).
/// </para>
/// </summary>
public class SlotEquipPanel : InputBlockingPanel
{
	static readonly string[] SlotKeys = { "Q", "W", "E", "R", "A", "S", "D", "F" };

	[SerializeField, Tooltip("버튼 (Button - TextMeshPro). BlockCoding의 BlockButton 프리팹을 써도 된다")]
	Button buttonPrefab;

	[SerializeField, Tooltip("슬롯 8칸 버튼이 만들어질 줄 (Horizontal Layout Group)")]
	RectTransform slotRow;

	[SerializeField, Tooltip("함수·스킬 버튼이 만들어질 곳 (Vertical 또는 Grid Layout Group. 스크롤을 쓰면 Scroll Rect의 Content)")]
	RectTransform list;

	[SerializeField, Tooltip("고른 슬롯, 코드 영역 크기, 안내를 보여 줄 글자")]
	TMP_Text infoText;

	[SerializeField, Tooltip("고른 슬롯 버튼 색")]
	Color selectedColor = new Color32(0x6A, 0x5A, 0xC9, 0xFF);

	[SerializeField, Tooltip("고르지 않은 슬롯 버튼 색")]
	Color normalColor = new Color32(0x2A, 0x2F, 0x45, 0xFF);

	SlotLoadout loadout;
	int selectedSlot;
	string message = "";
	readonly List<Button> slotButtons = new List<Button>();
	readonly List<Button> listButtons = new List<Button>();

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

		message = "";
		base.OpenFor(player);
		BuildSlots();
		BuildList(); // 편집 화면에서 새로 저장한 함수도 보이도록 열 때마다 다시 만든다
		Refresh();
	}

	void OnDestroy()
	{
		if (loadout != null) loadout.Changed -= OnLoadoutChanged;
	}

	void OnLoadoutChanged(int slot) => Refresh();

	// 화면 만들기 ---------------------------------------------------------------

	void BuildSlots()
	{
		if (slotButtons.Count > 0) return;
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			int slot = i;
			slotButtons.Add(CreateButton(slotRow, "", () => SelectSlot(slot)));
		}
	}

	void BuildList()
	{
		ClearButtons(listButtons);

		CreateListButton("비우기", () =>
		{
			loadout.Unequip(selectedSlot);
			message = $"{SlotKeys[selectedSlot]} 칸을 비움";
			Refresh();
		});

		// 블록 함수 (편집 화면에서 저장한 것)
		foreach (FunctionData function in FunctionStorage.LoadAll())
		{
			string guid = function.Guid;
			string label = $"[함수] {function.Name}";
			CreateListButton(label, () => Equip(loadout.EquipFunction(selectedSlot, guid), label));
		}

		// C 스킬, 공통 이동 (시험용 test.는 빼고)
		foreach (PredefinedFunction skill in PredefinedFunctionRegistry.All)
		{
			string id = skill.Id;
			if (!(id.StartsWith("c.") || id.StartsWith("common.")) || !SlotLoadout.IsEquippableSkill(id)) continue;
			string label = $"[스킬] {SkillName(id)}";
			CreateListButton(label, () => Equip(loadout.EquipSkill(selectedSlot, id), label));
		}
	}

	void CreateListButton(string label, System.Action onClick) => listButtons.Add(CreateButton(list, label, onClick));

	Button CreateButton(RectTransform parent, string label, System.Action onClick)
	{
		Button button = Instantiate(buttonPrefab, parent);
		TMP_Text text = button.GetComponentInChildren<TMP_Text>();
		if (text != null) text.text = label;
		button.onClick.AddListener(() => onClick());
		return button;
	}

	void ClearButtons(List<Button> buttons)
	{
		foreach (Button button in buttons)
		{
			if (button != null) Destroy(button.gameObject);
		}
		buttons.Clear();
	}

	// 버튼 ---------------------------------------------------------------------

	void SelectSlot(int slot)
	{
		selectedSlot = slot;
		message = "";
		Refresh();
	}

	void Equip(bool ok, string label)
	{
		message = ok ? $"{SlotKeys[selectedSlot]} 칸에 {label}" : $"{label}은(는) 넣을 수 없음";
		Refresh();
	}

	// 다시 그리기 -----------------------------------------------------------------

	void Refresh()
	{
		if (loadout == null) return;

		for (int i = 0; i < slotButtons.Count; i++)
		{
			TMP_Text text = slotButtons[i].GetComponentInChildren<TMP_Text>();
			if (text != null) text.text = $"{SlotKeys[i]} {EntryName(loadout.GetEntry(i))}";
			Image image = slotButtons[i].GetComponent<Image>();
			if (image != null) image.color = i == selectedSlot ? selectedColor : normalColor;
		}

		if (infoText != null)
		{
			infoText.text = $"고른 칸: {SlotKeys[selectedSlot]}    코드 영역 {loadout.CodeSize}" +
				(string.IsNullOrEmpty(message) ? "" : $"\n{message}");
		}
	}

	static string EntryName(SlotLoadout.Entry entry)
	{
		if (entry.IsEmpty) return "-";
		if (entry.Kind == SlotLoadout.EntryKind.Skill) return SkillName(entry.Id);
		FunctionData function = FunctionStorage.Load(entry.Id);
		return function != null ? function.Name : "(지워진 함수)";
	}

	static string SkillName(string id) => SkillTable.TryGet(id, out SkillData data) ? data.DisplayName : id;
}
