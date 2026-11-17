using System.Collections.Generic;

/// <summary>
/// 블록·함수의 「가진 개수」 규칙 (#500). 서버와 클라이언트가 함께 읽는다.
/// <para>
/// - 항목 ID는 BlockUsageCounter.KeyOf와 같다 (고정 ID, If·ElseIf·Else, BlockKind 이름). 가진 개수와 쓴 개수를 이 ID로 맞춘다.<br/>
/// - 가진 개수는 블록을 놓거나 지워도 바뀌지 않는다. 남은 개수 = 가진 개수 − 함수에서 센 개수(BlockUsageCounter.Count).
/// </para>
/// </summary>
public static class ItemCountRules
{
	/// <summary>항목 하나당 가질 수 있는 최대 개수.</summary>
	public const int MaxCount = 99;

	/// <summary>
	/// 새 캐릭터의 처음 개수 (항목 ID → 개수). 캐릭터 언어(language)마다 다르다. 모르는 언어는 C.<br/>
	/// - C: thrust 1, sword_throw 1, get_bug 2<br/>
	/// - Python (#532, 임시): py.magic_missile 1, py.hex 1, get_bug 2<br/>
	/// 처음 슬롯 함수 8개(CharacterProgressData.NewSlotFunction)가 쓰는 개수와 같아서, 처음 남은 개수는 모두 0이다.
	/// </summary>
	// 숫자를 직접 적지 않고 처음 슬롯 함수를 세어 만드는 이유: 처음 슬롯 함수를 바꿔도 처음 개수가 저절로 맞아,
	// 슬롯 함수가 가진 것보다 많이 쓰는 캐릭터가 생기지 않게 하기 위함 (기획: 튜토리얼을 마치면 남는 개수 0)
	public static Dictionary<string, int> Initial(string language = CharacterLanguages.C)
	{
		var slots = new List<FunctionData>();
		var guids = new List<string>();
		for (int i = 0; i < SlotLoadout.SlotCount; i++)
		{
			FunctionData slot = CharacterProgressData.NewSlotFunction(i, language);
			slots.Add(slot);
			guids.Add(slot.Guid);
		}
		return BlockUsageCounter.Count(guids, slots, null);
	}

	/// <summary>
	/// 저장하면 개수가 모자라는 항목을 하나 찾는다. 없으면 null. 서버의 저장 검사(FunctionExecutorNetwork.CheckItemCounts)와
	/// 편집 화면의 먼저 안내가 같은 규칙을 쓰도록 둘 다 이 함수를 거친다.<br/>
	/// 모자람 = 저장한 뒤 쓴 개수가 가진 개수보다 많고, 저장하기 전보다 늘었음.
	/// </summary>
	/// <param name="usedBefore">저장하기 전에 쓴 개수 (BlockUsageCounter.Count)</param>
	/// <param name="usedAfter">저장한 뒤에 쓸 개수 (BlockUsageCounter.Count)</param>
	/// <param name="owned">가진 개수 (PlayerProgress.ItemCounts)</param>
	// 「늘었음」을 함께 보는 이유: 개수 기능 전에 만든 캐릭터는 이미 가진 것보다 많이 쓰고 있을 수 있다.
	// 그 항목을 더 쓰지 않는 저장(관계없는 함수 고치기, 줄이기)까지 막으면 고칠 방법이 없어진다
	public static Shortage? FindShortage(IReadOnlyDictionary<string, int> usedBefore, IReadOnlyDictionary<string, int> usedAfter,
		IReadOnlyDictionary<string, int> owned)
	{
		foreach (KeyValuePair<string, int> pair in usedAfter)
		{
			int have = owned != null && owned.TryGetValue(pair.Key, out int count) ? count : 0;
			int before = usedBefore != null && usedBefore.TryGetValue(pair.Key, out int previous) ? previous : 0;
			if (pair.Value > have && pair.Value > before) return new Shortage(pair.Key, pair.Value, have);
		}
		return null;
	}

	/// <summary>개수가 모자라는 항목 하나: 항목 ID, 저장하면 쓸 개수, 가진 개수.</summary>
	public readonly struct Shortage
	{
		public readonly string ItemId;
		public readonly int Used;
		public readonly int Owned;

		public Shortage(string itemId, int used, int owned)
		{
			ItemId = itemId;
			Used = used;
			Owned = owned;
		}
	}

	/// <summary>
	/// 노드를 해금할 때 1개씩 주는 항목 ID들.<br/>
	/// - 스킬 노드: 노드 ID (스킬 노드 ID는 그 함수의 고정 ID와 같다. 예: c.thrust)<br/>
	/// - 문법 노드: 노드가 여는 블록 종류(UnlocksBlocks)의 항목 ID. Conditional은 If, ElseIf, Else 셋.
	///   Function(함수 블록 종류)은 개수가 없으므로 주지 않는다
	/// </summary>
	public static List<string> KeysUnlockedBy(SkillTreeNode node)
	{
		var keys = new List<string>();
		if (node == null) return keys;

		if (node.Kind == SkillTreeNodeKind.Skill)
		{
			keys.Add(node.Id);
			return keys;
		}

		foreach (BlockKind kind in node.UnlocksBlocks)
		{
			switch (kind)
			{
				case BlockKind.Function:
					break;
				case BlockKind.Conditional:
					// 지금 트리는 if문 노드 하나가 Conditional을 열어 세 갈래를 모두 쓸 수 있게 하므로 셋 다 준다
					keys.Add(ConditionalBranch.If.ToString());
					keys.Add(ConditionalBranch.ElseIf.ToString());
					keys.Add(ConditionalBranch.Else.ToString());
					break;
				default:
					keys.Add(kind.ToString());
					break;
			}
		}
		return keys;
	}
}
