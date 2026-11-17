using System.Collections.Generic;

/// <summary>
/// 캐릭터 하나의 진행도. 서버가 「들어가기」 때 ProgressStore.Load로 DB에서 불러온 값이다.
/// <para>
/// - SQLite를 모르는 일반 클래스다. 받는 쪽(PlayerProgress, FunctionExecutorNetwork)은 DB 테이블 모양을 몰라도 된다.<br/>
/// - 타입은 다른 시스템이 이미 쓰는 것을 그대로 쓴다. Slots·Functions·Globals는 FunctionExecutorNetwork.ApplyPlayerData가 받는 타입, Unlocked는 SkillTree가 받는 해금 목록이다.
/// </para>
/// </summary>
public class CharacterProgressData
{
	public int Level = 1;
	/// <summary>현재 레벨 안의 경험치. 레벨업하면 0부터 다시 센다.</summary>
	public int Exp;
	public int Money;

	/// <summary>재료 ID → 개수. 없는 재료는 0개다.</summary>
	public Dictionary<string, int> Materials = new Dictionary<string, int>();

	/// <summary>블록·함수의 항목 ID → 가진 개수 (#500, ItemCountRules). 없는 항목은 0개다.</summary>
	public Dictionary<string, int> ItemCounts = new Dictionary<string, int>();

	/// <summary>해금한 스킬 트리 노드 ID (문법 노드와 스킬 노드).</summary>
	public List<string> Unlocked = new List<string>();

	/// <summary>슬롯 0~7 (Q W E R A S D F). 실제 계정은 슬롯마다 그 슬롯의 슬롯 함수 GUID(EntryKind.UserFunction)가 들어 있다.</summary>
	public SlotLoadout.Entry[] Slots = NewEmptySlots();

	/// <summary>이 캐릭터의 함수 전부. 슬롯 함수 8개와 페이지 함수(최대 ProgressRules.MaxPageFunctions개).</summary>
	public List<FunctionData> Functions = new List<FunctionData>();

	public GlobalVariablesData Globals = new GlobalVariablesData();

	// 처음 슬롯 함수의 function body에 넣을 스킬 (슬롯 0부터, 캐릭터 언어마다). 나머지 슬롯 함수는 빈 본문
	// 해금 목록에서 만들지 않는 이유: 본문에는 스킬 ID만이 아니라 인자 블록(GetBug)까지 들어가야 하는데, 해금 목록에는 그 정보가 없기 때문
	// target: 대상 인자로 GetBug()를 넣는지. 대상이 없는 스킬(구슬 생성 등)은 인자 없이 부른다
	static readonly (string id, bool target)[] InitialSlotSkillsC =
	{
		(PredefinedFunctionIds.Thrust, true),
		(PredefinedFunctionIds.SwordThrow, true),
	};

	// 파이썬 마법사: 매직 미사일과 저주 (#532, 임시. 스킬 트리 이슈에서 다시 정함)
	// 구슬 스킬이 아닌 이유: 구슬 발사는 구슬을 변수에 담아 넘겨야 하는데(orb_shot(target, orb)), 처음에는 변수 블록(syntax.variable)이 잠겨 있기 때문
	static readonly (string id, bool target)[] InitialSlotSkillsPython =
	{
		(PredefinedFunctionIds.PyMagicMissile, true),
		(PredefinedFunctionIds.PyHex, true),
	};

	static (string id, bool target)[] InitialSlotSkillsOf(string language) =>
		language == CharacterLanguages.Python ? InitialSlotSkillsPython : InitialSlotSkillsC;

	/// <summary>모두 빈 슬롯 8개.</summary>
	public static SlotLoadout.Entry[] NewEmptySlots()
	{
		var slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++) slots[i] = SlotLoadout.Entry.Empty;
		return slots;
	}

	/// <summary>
	/// 새 C 캐릭터의 슬롯 함수 하나를 만든다. NewSlotFunction(slot, CharacterLanguages.C)와 같다.
	/// </summary>
	public static FunctionData NewSlotFunction(int slot) => NewSlotFunction(slot, CharacterLanguages.C);

	/// <summary>
	/// 새 캐릭터의 슬롯 함수 하나를 만든다. 새 GUID, 이름 slot1~slot8, 파라미터 없음. 본문은 캐릭터 언어(language)마다 다르다.<br/>
	/// - C: 슬롯 0 = thrust(GetBug());, 슬롯 1 = sword_throw(GetBug());<br/>
	/// - Python (#532, 임시): 슬롯 0 = magic_missile(get_bug()), 슬롯 1 = hex(get_bug())<br/>
	/// - 나머지 슬롯 = 빈 본문. 모르는 언어는 C
	/// </summary>
	public static FunctionData NewSlotFunction(int slot, string language)
	{
		FunctionData function = FunctionData.Create(ProgressRules.SlotFunctionName(slot));
		(string id, bool target)[] skills = InitialSlotSkillsOf(language);
		if (slot >= 0 && slot < skills.Length)
		{
			if (skills[slot].target)
				function.Blocks.Add(BlockData.CallPredefinedFunction(skills[slot].id,
					SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));
			else
				function.Blocks.Add(BlockData.CallPredefinedFunction(skills[slot].id));
		}
		return function;
	}

	/// <summary>새 캐릭터와 같은 슬롯 함수 8개를 Functions에 더하고, Slots 0~7에 그 GUID를 넣는다. 새 GUID로 만든다. language는 캐릭터 언어(없으면 C).</summary>
	public void FillInitialSlotFunctions(string language = CharacterLanguages.C)
	{
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			FunctionData function = NewSlotFunction(i, language);
			Functions.Add(function);
			Slots[i] = SlotLoadout.Entry.Function(function.Guid);
		}
	}
}
