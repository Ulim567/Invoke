using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 슬롯 8개(Q W E R A S D F)에 무엇을 넣었는지 가진 컴포넌트. 플레이어 프리팹에 붙인다 (#159).
/// <para>
/// - 슬롯 하나에는 비어 있음 / 블록코딩으로 만든 함수(GUID) / C 캐릭터 스킬(고정 ID) 중 하나가 들어간다.<br/>
/// - 장착 화면(임혜원)은 EquipFunction, EquipSkill, Unequip, Swap을 부르고 Changed를 받아 다시 그린다.<br/>
/// - SlotCaster는 TryGetFunction으로 시전할 함수(원본)를 받는다. 같은 슬롯은 같은 원본을 계속 돌려주므로 쿨타임·실행 중 검사·캔슬이 이어진다.<br/>
/// - 장착 내용은 이 컴퓨터 파일(persistentDataPath/SlotLoadout.json)에 저장한다. 서버 저장이 생기면 바꾼다.<br/>
/// - 코드 영역 크기는 CodeSize로 알려 주기만 한다. 한도 검사는 메모리 작업에서 한다.
/// </para>
/// </summary>
public class SlotLoadout : MonoBehaviour
{
	public const int SlotCount = 8;

	/// <summary>슬롯에 든 것의 종류</summary>
	public enum EntryKind
	{
		Empty,        // 비어 있음
		UserFunction, // 블록코딩으로 만든 함수. Id = GUID
		Skill,        // C 캐릭터 스킬 등 사전 정의 함수. Id = 고정 ID (예: c.thrust)
	}

	/// <summary>슬롯 하나의 내용</summary>
	[Serializable]
	public struct Entry
	{
		public EntryKind Kind;
		public string Id;

		public bool IsEmpty => Kind == EntryKind.Empty || string.IsNullOrEmpty(Id);
		public static Entry Empty => new Entry { Kind = EntryKind.Empty, Id = "" };
		public static Entry Function(string guid) => new Entry { Kind = EntryKind.UserFunction, Id = guid };
		public static Entry Skill(string id) => new Entry { Kind = EntryKind.Skill, Id = id };
	}

	[Tooltip("저장한 장착 내용이 없을 때(처음 켤 때) 넣어 둘 C 스킬 고정 ID. 슬롯 1~8 순서. 비우면 빈 슬롯")]
	[SerializeField] string[] defaultSkillIds =
	{
		PredefinedFunctionIds.Thrust, "test.function_a", PredefinedFunctionIds.BattlefieldSweep, PredefinedFunctionIds.WideSlash,
		"", "", "", "",
	};

	readonly Entry[] entries = new Entry[SlotCount];
	// 슬롯마다 시전할 원본. 장착을 바꾸거나 ReloadFunctions를 부를 때만 새로 만든다
	readonly FunctionBlock[] originals = new FunctionBlock[SlotCount];
	bool loaded;

	static string SavePath => Path.Combine(Application.persistentDataPath, "SlotLoadout.json");

	/// <summary>[표시] 슬롯 내용이 바뀌었을 때 (바뀐 슬롯 번호). 불러오기·다시 읽기처럼 여러 슬롯이 바뀌면 -1</summary>
	public event Action<int> Changed;

	void Awake() => EnsureLoaded();

	/// <summary>슬롯의 내용. 범위 밖이면 빈 슬롯.</summary>
	public Entry GetEntry(int slot)
	{
		EnsureLoaded();
		return IsValidSlot(slot) ? entries[slot] : Entry.Empty;
	}

	/// <summary>
	/// 블록코딩으로 만든 함수를 슬롯에 넣는다. 저장소(FunctionStorage)에 없는 GUID면 false.<br/>
	/// 같은 함수가 다른 슬롯에 있으면 그 슬롯은 비운다 (한 함수는 한 슬롯에만).
	/// </summary>
	public bool EquipFunction(int slot, string guid)
	{
		EnsureLoaded();
		if (!IsValidSlot(slot) || string.IsNullOrEmpty(guid)) return false;
		if (FunctionStorage.Load(guid) == null)
		{
			Debug.LogWarning($"[SlotLoadout] 슬롯 {slot + 1}: 저장된 함수가 아님 ({guid})");
			return false;
		}
		Put(slot, Entry.Function(guid));
		return true;
	}

	/// <summary>
	/// C 캐릭터 스킬(사전 정의 함수)을 슬롯에 넣는다. 없는 고정 ID, 입력 함수(input.*)면 false.<br/>
	/// 같은 스킬이 다른 슬롯에 있으면 그 슬롯은 비운다.
	/// </summary>
	public bool EquipSkill(int slot, string skillId)
	{
		EnsureLoaded();
		if (!IsValidSlot(slot) || !IsEquippableSkill(skillId))
		{
			Debug.LogWarning($"[SlotLoadout] 슬롯 {slot + 1}: 넣을 수 없는 스킬 ({skillId})");
			return false;
		}
		Put(slot, Entry.Skill(skillId));
		return true;
	}

	/// <summary>슬롯을 비운다.</summary>
	public void Unequip(int slot)
	{
		EnsureLoaded();
		if (!IsValidSlot(slot) || entries[slot].IsEmpty) return;
		entries[slot] = Entry.Empty;
		originals[slot] = null;
		SaveAndNotify(slot);
	}

	/// <summary>두 슬롯의 내용을 맞바꾼다 (장착 화면에서 끌어 옮길 때).</summary>
	public void Swap(int a, int b)
	{
		EnsureLoaded();
		if (!IsValidSlot(a) || !IsValidSlot(b) || a == b) return;
		(entries[a], entries[b]) = (entries[b], entries[a]);
		(originals[a], originals[b]) = (originals[b], originals[a]);
		Save();
		Changed?.Invoke(a);
		Changed?.Invoke(b);
	}

	/// <summary>
	/// 슬롯에서 시전할 함수(원본)를 꺼낸다. 빈 슬롯, 지워진 함수, 실행기가 아직 실행할 수 없는 함수면 false.<br/>
	/// 블록 함수는 UserFunction, 스킬은 PredefinedFunction이다. 꺼낸 원본은 그대로 FunctionExecutor.Cast에 넘긴다.
	/// </summary>
	public bool TryGetFunction(int slot, out FunctionBlock function)
	{
		EnsureLoaded();
		function = null;
		if (!IsValidSlot(slot) || entries[slot].IsEmpty) return false;
		if (originals[slot] == null) originals[slot] = BuildOriginal(slot, FunctionStorage.LoadAll());
		function = originals[slot];
		return function != null;
	}

	/// <summary>
	/// 블록 함수들을 저장소에서 다시 읽어 원본을 새로 만든다. 편집 화면에서 함수를 저장한 뒤 부른다.<br/>
	/// 새 원본은 쿨타임·실행 중 검사가 처음부터 시작한다.
	/// </summary>
	public void ReloadFunctions()
	{
		EnsureLoaded();
		for (int i = 0; i < SlotCount; i++)
		{
			if (entries[i].Kind == EntryKind.UserFunction) originals[i] = null;
		}
		Changed?.Invoke(-1);
	}

	/// <summary>
	/// 슬롯 전체의 코드 영역 크기 (function-data.md 「코드 영역 크기 규칙」). 장착 화면에 보여 줄 값. 한도 검사는 하지 않는다.
	/// </summary>
	public int CodeSize
	{
		get
		{
			EnsureLoaded();
			List<string> guids = new List<string>();
			List<string> skillIds = new List<string>();
			foreach (Entry entry in entries)
			{
				if (entry.IsEmpty) continue;
				if (entry.Kind == EntryKind.UserFunction) guids.Add(entry.Id);
				else skillIds.Add(entry.Id);
			}
			return CodeSizeCalculator.Calculate(guids, skillIds, FunctionStorage.LoadAll());
		}
	}

	/// <summary>슬롯에 넣을 수 있는 스킬인지. 등록된 사전 정의 함수이고 입력 함수(input.*)가 아니면 true. 더블점프(패시브)는 false (#256).</summary>
	public static bool IsEquippableSkill(string skillId)
	{
		if (string.IsNullOrEmpty(skillId) || skillId.StartsWith("input.")) return false;
		// 더블점프는 해금하면 Space로 쓰는 패시브라 슬롯에 넣지 않는다 (#256). 블록코딩에서는 부를 수 있다
		if (skillId == PredefinedFunctionIds.DoubleJump) return false;
		return PredefinedFunctionRegistry.TryGet(skillId, out _);
	}

	static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

	// 넣는다. 같은 것이 다른 슬롯에 있으면 그 슬롯을 비운다
	void Put(int slot, Entry entry)
	{
		for (int i = 0; i < SlotCount; i++)
		{
			if (i == slot || entries[i].Kind != entry.Kind || entries[i].Id != entry.Id) continue;
			entries[i] = Entry.Empty;
			originals[i] = null;
			Changed?.Invoke(i);
		}
		entries[slot] = entry;
		originals[slot] = null;
		SaveAndNotify(slot);
	}

	FunctionBlock BuildOriginal(int slot, List<FunctionData> owned)
	{
		Entry entry = entries[slot];
		if (entry.Kind == EntryKind.Skill)
		{
			if (PredefinedFunctionRegistry.TryGet(entry.Id, out PredefinedFunction skill)) return skill;
			Debug.LogWarning($"[SlotLoadout] 슬롯 {slot + 1}: 없는 스킬 ID '{entry.Id}'");
			return null;
		}

		FunctionData data = owned.Find(f => f.Guid == entry.Id);
		if (data == null)
		{
			Debug.LogWarning($"[SlotLoadout] 슬롯 {slot + 1}: 지워진 함수 ({entry.Id})");
			return null;
		}
		// 실행할 수 없는 함수면 변환기가 콘솔 오류를 남긴다
		return FunctionDataConverter.TryConvert(data, owned, out UserFunction function) ? function : null;
	}

	void SaveAndNotify(int slot)
	{
		Save();
		Changed?.Invoke(slot);
	}

	// 저장 파일 형식. JsonUtility로 쓴다
	[Serializable]
	class SaveData
	{
		public Entry[] Slots;
	}

	void Save()
	{
		try
		{
			File.WriteAllText(SavePath, JsonUtility.ToJson(new SaveData { Slots = entries }, true));
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[SlotLoadout] 저장 실패: {e.Message}");
		}
	}

	void EnsureLoaded()
	{
		if (loaded) return;
		loaded = true;

		SaveData data = null;
		try
		{
			if (File.Exists(SavePath)) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
		}
		catch (Exception e)
		{
			Debug.LogWarning($"[SlotLoadout] 저장 파일을 읽을 수 없음: {e.Message}");
		}

		for (int i = 0; i < SlotCount; i++)
		{
			if (data?.Slots != null && i < data.Slots.Length) entries[i] = data.Slots[i];
			else if (data == null && defaultSkillIds != null && i < defaultSkillIds.Length && !string.IsNullOrWhiteSpace(defaultSkillIds[i]))
				entries[i] = Entry.Skill(defaultSkillIds[i].Trim());
			else entries[i] = Entry.Empty;
		}
	}
}
