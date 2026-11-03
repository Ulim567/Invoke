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

	/// <summary>해금한 스킬 트리 노드 ID (문법 노드와 스킬 노드).</summary>
	public List<string> Unlocked = new List<string>();

	/// <summary>슬롯 0~7 (Q W E R A S D F). 실제 계정은 슬롯마다 그 슬롯의 슬롯 함수 GUID(EntryKind.UserFunction)가 들어 있다.</summary>
	public SlotLoadout.Entry[] Slots = NewEmptySlots();

	/// <summary>이 캐릭터의 함수 전부. 슬롯 함수 8개와 페이지 함수(최대 ProgressRules.MaxPageFunctions개).</summary>
	public List<FunctionData> Functions = new List<FunctionData>();

	public GlobalVariablesData Globals = new GlobalVariablesData();

	// 처음 슬롯 함수의 function body에 넣을 C 스킬 (슬롯 0부터). 나머지 슬롯 함수는 빈 본문
	// 해금 목록에서 만들지 않는 이유: 본문에는 스킬 ID만이 아니라 인자 블록(GetBug)까지 들어가야 하는데, 해금 목록에는 그 정보가 없기 때문
	// 지금은 C 캐릭터만 있다. 다른 언어가 생기면 언어마다 나눈다
	static readonly string[] InitialSlotSkills = { PredefinedFunctionIds.Thrust, PredefinedFunctionIds.SwordThrow };

	/// <summary>모두 빈 슬롯 8개.</summary>
	public static SlotLoadout.Entry[] NewEmptySlots()
	{
		var slots = new SlotLoadout.Entry[SlotLoadout.SlotCount];
		for (int i = 0; i < slots.Length; i++) slots[i] = SlotLoadout.Entry.Empty;
		return slots;
	}

	/// <summary>
	/// 새 캐릭터의 슬롯 함수 하나를 만든다. 새 GUID, 이름 slot1~slot8, 파라미터 없음.<br/>
	/// function body: 슬롯 0 = thrust(GetBug());, 슬롯 1 = sword_throw(GetBug());, 나머지 = 빈 본문.
	/// </summary>
	public static FunctionData NewSlotFunction(int slot)
	{
		FunctionData function = FunctionData.Create(ProgressRules.SlotFunctionName(slot));
		if (slot >= 0 && slot < InitialSlotSkills.Length)
		{
			function.Blocks.Add(BlockData.CallPredefinedFunction(InitialSlotSkills[slot],
				SlotData.FromBlock(BlockData.CallPredefinedFunction(PredefinedFunctionIds.GetBug))));
		}
		return function;
	}

	/// <summary>새 캐릭터와 같은 슬롯 함수 8개를 Functions에 더하고, Slots 0~7에 그 GUID를 넣는다. 새 GUID로 만든다.</summary>
	public void FillInitialSlotFunctions()
	{
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			FunctionData function = NewSlotFunction(i);
			Functions.Add(function);
			Slots[i] = SlotLoadout.Entry.Function(function.Guid);
		}
	}
}
