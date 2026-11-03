#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using SQLite;
using UnityEngine;

/// <summary>
/// 캐릭터의 진행도를 DB(SQLite)에 저장하고 읽는 곳. 서버에서만 쓴다.
/// <para>
/// - 진행도 테이블 6개(CharacterProgress, CharacterMaterials, CharacterUnlocks, CharacterSlots, CharacterFunctions, CharacterGlobals)만 다룬다. 계정·캐릭터는 AccountStore가 다룬다.<br/>
/// - AccountStore가 연 SQLite 연결을 함께 쓴다. 그래야 캐릭터를 만들 때 Characters row와 진행도 처음 row를 한 트랜잭션으로 넣을 수 있다.<br/>
/// - 부르는 곳은 AccountStore, AccountServer, PlayerProgress, FunctionExecutorNetwork뿐이다. 다른 시스템은 PlayerProgress를 부른다.
/// </para>
/// <para>
/// 사용법: new로 만들지 않고 AccountStore.Progress를 쓴다.
/// </para>
/// </summary>
public class ProgressStore
{
	readonly SQLiteConnection db;

	// 아래 row 클래스는 DB 테이블 모양이다. 다른 스크립트는 CharacterProgressData로 받으므로 밖으로 내보내지 않는다

	[Table("CharacterProgress")]
	class ProgressRow
	{
		[PrimaryKey]
		public string CharacterKey { get; set; }
		public int Level { get; set; }
		// 현재 레벨 안의 경험치. 경험치 표를 바꿔도 레벨이 내려가지 않도록 레벨과 함께 저장한다
		public int Exp { get; set; }
		public int Money { get; set; }
	}

	// sqlite-net은 column 두 개를 묶은 기본 키를 지원하지 않는다.
	// 그래서 자동 번호를 기본 키로 두고, (캐릭터, 재료) 짝은 unique index로 지킨다. 아래 Unlock·Slot·Function도 같다
	[Table("CharacterMaterials")]
	class MaterialRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterMaterial", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		[Indexed(Name = "CharacterMaterial", Order = 2, Unique = true)]
		public string MaterialId { get; set; }
		// 1 이상. 0개가 되면 row를 지운다
		public int Count { get; set; }
	}

	[Table("CharacterUnlocks")]
	class UnlockRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterNode", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		[Indexed(Name = "CharacterNode", Order = 2, Unique = true)]
		public string NodeId { get; set; }
	}

	[Table("CharacterSlots")]
	class SlotRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterSlot", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		[Indexed(Name = "CharacterSlot", Order = 2, Unique = true)]
		public int SlotIndex { get; set; }
		// SlotLoadout.EntryKind의 이름. 숫자로 저장하면 EntryKind의 순서가 바뀔 때 다른 종류로 읽힌다
		public string Kind { get; set; }
		public string EntryId { get; set; }
	}

	[Table("CharacterFunctions")]
	class FunctionRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterFunction", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		[Indexed(Name = "CharacterFunction", Order = 2, Unique = true)]
		public string FunctionGuid { get; set; }
		// 서버가 함수 내용으로 찾을 일이 없으므로 이름·블록을 column으로 나누지 않는다
		public string Json { get; set; }
	}

	[Table("CharacterGlobals")]
	class GlobalsRow
	{
		[PrimaryKey]
		public string CharacterKey { get; set; }
		// 선언만 저장한다. 실행 중 바뀐 값은 FunctionExecutor의 데이터 영역에만 있다
		public string Json { get; set; }
	}

	/// <summary>AccountStore가 DB를 열 때 만든다. 테이블이 없으면 만든다.</summary>
	internal ProgressStore(SQLiteConnection db)
	{
		this.db = db;
		db.CreateTable<ProgressRow>();
		db.CreateTable<MaterialRow>();
		db.CreateTable<UnlockRow>();
		db.CreateTable<SlotRow>();
		db.CreateTable<FunctionRow>();
		db.CreateTable<GlobalsRow>();
	}

	/// <summary>
	/// 새 캐릭터의 처음 row를 넣는다. AccountStore.CreateCharacter가 Characters row와 같은 트랜잭션 안에서 부른다.<br/>
	/// 레벨 1, 경험치·돈 0, 재료·페이지 함수 없음, 해금은 SkillTree의 처음 노드, 전역 변수 없음.<br/>
	/// 슬롯 함수 8개(CharacterProgressData.NewSlotFunction)를 CharacterFunctions에 넣고, CharacterSlots 0~7에 그 GUID를 넣는다.<br/>
	/// 이미 있는 row는 건드리지 않고 없는 row만 넣는다. Load가 진행도 row가 빠진 캐릭터를 채울 때도 쓰기 때문이다.
	/// </summary>
	// 남아 있는 row를 지우지 않는 이유: 진행도 row만 빠진 캐릭터의 함수·재료·슬롯 함수를 잃지 않게 하기 위함
	// 있는 row에 다시 넣으면 unique index 때문에 예외가 나고, 「들어가기」 요청을 처리하던 서버가 그 연결을 끊는다
	public void CreateInitial(string characterKey)
	{
		List<string> unlocked = SkillTree.StartingNodes();
		unlocked.AddRange(SkillTree.AutoUnlockedBy(unlocked));

		db.RunInTransaction(() =>
		{
			if (db.Find<ProgressRow>(characterKey) == null)
				db.Insert(new ProgressRow { CharacterKey = characterKey, Level = 1, Exp = 0, Money = 0 });

			foreach (string id in unlocked)
			{
				if (db.Table<UnlockRow>().Where(r => r.CharacterKey == characterKey && r.NodeId == id).Count() == 0)
					db.Insert(new UnlockRow { CharacterKey = characterKey, NodeId = id });
			}

			// 슬롯 row가 있는 슬롯은 그 슬롯 함수를 그대로 두고, 빠진 슬롯에만 새 슬롯 함수를 만든다
			var existingSlots = new HashSet<int>();
			foreach (SlotRow row in db.Table<SlotRow>().Where(r => r.CharacterKey == characterKey)) existingSlots.Add(row.SlotIndex);
			for (int i = 0; i < SlotLoadout.SlotCount; i++)
			{
				if (existingSlots.Contains(i)) continue;
				InsertSlotFunction(characterKey, i, CharacterProgressData.NewSlotFunction(i));
			}

			if (db.Find<GlobalsRow>(characterKey) == null)
				db.Insert(new GlobalsRow { CharacterKey = characterKey, Json = FunctionDataJson.ToJson(new GlobalVariablesData()) });
		});
	}

	/// <summary>
	/// 캐릭터의 진행도를 테이블 6개에서 읽어 돌려준다. 「들어가기」 때 서버가 부른다.<br/>
	/// 진행도 row가 없는 캐릭터(이 기능 전에 만든 캐릭터)는 새 캐릭터와 같은 처음 row를 넣은 뒤 돌려준다.<br/>
	/// 읽을 수 없는 함수 JSON과 SkillTree에 없는 노드 ID는 건너뛰고 로그를 남긴다.
	/// </summary>
	public CharacterProgressData Load(string characterKey)
	{
		ProgressRow progress = db.Find<ProgressRow>(characterKey);
		if (progress == null)
		{
			CreateInitial(characterKey);
			progress = db.Find<ProgressRow>(characterKey);
		}

		var data = new CharacterProgressData
		{
			Level = progress.Level,
			Exp = progress.Exp,
			Money = progress.Money,
		};

		foreach (MaterialRow row in db.Table<MaterialRow>().Where(r => r.CharacterKey == characterKey))
			data.Materials[row.MaterialId] = row.Count;

		foreach (UnlockRow row in db.Table<UnlockRow>().Where(r => r.CharacterKey == characterKey))
		{
			// SkillTree 에셋에서 지운 노드가 DB에 남아 있어도 서버가 멈추지 않게 한다
			if (SkillTree.TryGet(row.NodeId, out _)) data.Unlocked.Add(row.NodeId);
			else Debug.LogWarning($"[ProgressStore] SkillTree에 없는 노드라 건너뜁니다: {row.NodeId} (캐릭터 {characterKey})");
		}

		foreach (SlotRow row in db.Table<SlotRow>().Where(r => r.CharacterKey == characterKey))
		{
			if (row.SlotIndex < 0 || row.SlotIndex >= data.Slots.Length) continue;
			data.Slots[row.SlotIndex] = ToEntry(row);
		}

		foreach (FunctionRow row in db.Table<FunctionRow>().Where(r => r.CharacterKey == characterKey))
		{
			// 함수 하나가 깨져도 나머지 함수는 불러온다
			if (FunctionDataJson.TryFromJson(row.Json, out FunctionData function)) data.Functions.Add(function);
			else Debug.LogWarning($"[ProgressStore] 함수 JSON을 읽을 수 없어 건너뜁니다: {row.FunctionGuid} (캐릭터 {characterKey})");
		}

		GlobalsRow globals = db.Find<GlobalsRow>(characterKey);
		if (globals != null && FunctionDataJson.TryGlobalVariablesFromJson(globals.Json, out GlobalVariablesData parsed) && parsed != null)
			data.Globals = parsed;
		else if (globals != null)
			Debug.LogWarning($"[ProgressStore] 전역 변수 JSON을 읽을 수 없어 전역 변수 없음으로 봅니다 (캐릭터 {characterKey})");

		return data;
	}

	/// <summary>
	/// [테스트용] 캐릭터의 진행도 테이블 6개를 data로 모두 바꾼다. 한 트랜잭션이므로 예전 값이 섞여 남지 않는다.<br/>
	/// 슬롯은 8개에 맞춘다. 슬롯의 함수가 data.Functions에 없으면 새 캐릭터와 같은 슬롯 함수를 만든다.<br/>
	/// 페이지 함수(슬롯에 걸리지 않은 함수)는 ProgressRules.MaxPageFunctions개까지만 넣고, GUID가 없는 함수는 건너뛴다.
	/// </summary>
	public void ReplaceAll(string characterKey, CharacterProgressData data)
	{
		db.RunInTransaction(() =>
		{
			foreach (string table in new[] { "CharacterProgress", "CharacterMaterials", "CharacterUnlocks", "CharacterSlots", "CharacterFunctions", "CharacterGlobals" })
				db.Execute($"DELETE FROM {table} WHERE CharacterKey = ?", characterKey);

			db.Insert(new ProgressRow { CharacterKey = characterKey, Level = Math.Max(1, data.Level), Exp = Math.Max(0, data.Exp), Money = Math.Max(0, data.Money) });

			if (data.Materials != null)
			{
				foreach (KeyValuePair<string, int> pair in data.Materials)
				{
					if (!string.IsNullOrEmpty(pair.Key) && pair.Value > 0)
						db.Insert(new MaterialRow { CharacterKey = characterKey, MaterialId = pair.Key, Count = pair.Value });
				}
			}

			var unlocked = new HashSet<string>();
			if (data.Unlocked != null)
			{
				foreach (string id in data.Unlocked)
				{
					if (!string.IsNullOrEmpty(id) && unlocked.Add(id)) db.Insert(new UnlockRow { CharacterKey = characterKey, NodeId = id });
				}
			}

			var functions = new List<FunctionData>();
			var functionsByGuid = new Dictionary<string, FunctionData>();
			if (data.Functions != null)
			{
				foreach (FunctionData function in data.Functions)
				{
					if (function == null || string.IsNullOrEmpty(function.Guid) || functionsByGuid.ContainsKey(function.Guid)) continue;
					functions.Add(function);
					functionsByGuid.Add(function.Guid, function);
				}
			}

			// 슬롯의 함수가 data.Functions에 있으면 그대로 쓰고, 없으면(빠진 슬롯, 스킬·빈 슬롯, 없는 GUID, 다른 슬롯과 같은 GUID) 새 캐릭터와 같은 슬롯 함수를 만든다
			// 실제 계정의 슬롯은 언제나 함수 하나이기 때문(빈 슬롯 없음)
			var slotGuids = new HashSet<string>();
			for (int i = 0; i < SlotLoadout.SlotCount; i++)
			{
				SlotLoadout.Entry entry = data.Slots != null && i < data.Slots.Length ? data.Slots[i] : SlotLoadout.Entry.Empty;
				if (entry.Kind == SlotLoadout.EntryKind.UserFunction && functionsByGuid.TryGetValue(entry.Id ?? "", out FunctionData function) && slotGuids.Add(function.Guid))
					InsertSlotFunction(characterKey, i, function);
				else
					InsertSlotFunction(characterKey, i, CharacterProgressData.NewSlotFunction(i));
			}

			// 슬롯에 걸리지 않은 함수는 페이지 함수다. ProgressRules.MaxPageFunctions개까지만 넣는다
			int pageFunctions = 0;
			foreach (FunctionData function in functions)
			{
				if (slotGuids.Contains(function.Guid)) continue;
				if (++pageFunctions > ProgressRules.MaxPageFunctions) break;
				db.Insert(new FunctionRow { CharacterKey = characterKey, FunctionGuid = function.Guid, Json = FunctionDataJson.ToJson(function) });
			}

			db.Insert(new GlobalsRow { CharacterKey = characterKey, Json = FunctionDataJson.ToJson(data.Globals ?? new GlobalVariablesData()) });
		});
	}

	/// <summary>레벨, 현재 레벨 안의 경험치, 돈을 덮어쓴다.</summary>
	public void SaveProgress(string characterKey, int level, int exp, int money)
	{
		db.InsertOrReplace(new ProgressRow { CharacterKey = characterKey, Level = level, Exp = exp, Money = money });
	}

	/// <summary>재료 하나의 개수를 정한다. 0 이하면 row를 지운다.</summary>
	public void SetMaterialCount(string characterKey, string materialId, int count)
	{
		db.RunInTransaction(() => WriteMaterialCount(characterKey, materialId, count));
	}

	/// <summary>
	/// 노드 해금과 재료 개수 변경을 한 트랜잭션으로 쓴다. 재료만 빠지고 노드가 안 열리는 일이 없게 하기 위함이다.<br/>
	/// nodeIds: 새로 연 노드(같이 열린 노드 포함). 이미 있는 노드는 건너뛴다. materialCounts: 바뀐 재료의 새 개수(뺀 뒤의 개수).
	/// </summary>
	public void SaveUnlock(string characterKey, IReadOnlyList<string> nodeIds, IReadOnlyDictionary<string, int> materialCounts)
	{
		db.RunInTransaction(() =>
		{
			foreach (string id in nodeIds)
			{
				bool exists = db.Table<UnlockRow>().Where(r => r.CharacterKey == characterKey && r.NodeId == id).Count() > 0;
				if (!exists) db.Insert(new UnlockRow { CharacterKey = characterKey, NodeId = id });
			}
			if (materialCounts == null) return;
			foreach (KeyValuePair<string, int> pair in materialCounts) WriteMaterialCount(characterKey, pair.Key, pair.Value);
		});
	}

	/// <summary>캐릭터의 페이지 함수(슬롯에 걸리지 않은 함수) 개수. 함수 개수 제한(ProgressRules.MaxPageFunctions)을 검사할 때 쓴다.</summary>
	// 함수 row 수로 센다. 읽을 수 없어 불러오지 못한 함수도 자리를 차지하고 있기 때문
	public int CountPageFunctions(string characterKey)
	{
		var slotGuids = new HashSet<string>();
		string functionKind = SlotLoadout.EntryKind.UserFunction.ToString();
		foreach (SlotRow row in db.Table<SlotRow>().Where(r => r.CharacterKey == characterKey && r.Kind == functionKind))
			slotGuids.Add(row.EntryId);

		int count = 0;
		foreach (FunctionRow row in db.Table<FunctionRow>().Where(r => r.CharacterKey == characterKey))
		{
			if (!slotGuids.Contains(row.FunctionGuid)) count++;
		}
		return count;
	}

	/// <summary>그 캐릭터에 이 GUID의 함수가 있으면 true. 새 함수인지(개수 제한 대상인지) 볼 때 쓴다.</summary>
	public bool HasFunction(string characterKey, string functionGuid)
	{
		return FindFunction(characterKey, functionGuid) != null;
	}

	/// <summary>함수 하나를 넣거나 덮어쓴다. 다른 함수 row는 건드리지 않는다. 개수 제한은 부르는 쪽이 먼저 검사한다.</summary>
	public void SaveFunction(string characterKey, string functionGuid, string json)
	{
		db.RunInTransaction(() =>
		{
			FunctionRow row = FindFunction(characterKey, functionGuid);
			if (row == null)
			{
				db.Insert(new FunctionRow { CharacterKey = characterKey, FunctionGuid = functionGuid, Json = json });
			}
			else
			{
				row.Json = json;
				db.Update(row);
			}
		});
	}

	/// <summary>전역 변수 선언 JSON을 덮어쓴다. 편집 화면이 전역 변수를 한꺼번에 저장하므로 JSON 전체를 바꾼다.</summary>
	public void SaveGlobals(string characterKey, string json)
	{
		db.InsertOrReplace(new GlobalsRow { CharacterKey = characterKey, Json = json });
	}

	// 트랜잭션 안에서 부른다
	void WriteMaterialCount(string characterKey, string materialId, int count)
	{
		MaterialRow row = db.Table<MaterialRow>().Where(r => r.CharacterKey == characterKey && r.MaterialId == materialId).FirstOrDefault();
		if (count <= 0)
		{
			// row가 없으면 0개로 보므로 지운다
			if (row != null) db.Delete(row);
		}
		else if (row == null)
		{
			db.Insert(new MaterialRow { CharacterKey = characterKey, MaterialId = materialId, Count = count });
		}
		else
		{
			row.Count = count;
			db.Update(row);
		}
	}

	// 슬롯 함수 하나를 CharacterFunctions와 그 슬롯의 CharacterSlots row에 넣는다. 트랜잭션 안에서 부른다
	void InsertSlotFunction(string characterKey, int slot, FunctionData function)
	{
		db.Insert(new FunctionRow { CharacterKey = characterKey, FunctionGuid = function.Guid, Json = FunctionDataJson.ToJson(function) });
		db.Insert(ToSlotRow(characterKey, slot, SlotLoadout.Entry.Function(function.Guid)));
	}

	FunctionRow FindFunction(string characterKey, string functionGuid)
	{
		return db.Table<FunctionRow>().Where(r => r.CharacterKey == characterKey && r.FunctionGuid == functionGuid).FirstOrDefault();
	}

	static SlotRow ToSlotRow(string characterKey, int slot, SlotLoadout.Entry entry)
	{
		return new SlotRow
		{
			CharacterKey = characterKey,
			SlotIndex = slot,
			Kind = entry.IsEmpty ? SlotLoadout.EntryKind.Empty.ToString() : entry.Kind.ToString(),
			EntryId = entry.IsEmpty ? "" : entry.Id,
		};
	}

	static SlotLoadout.Entry ToEntry(SlotRow row)
	{
		if (!Enum.TryParse(row.Kind, out SlotLoadout.EntryKind kind)) return SlotLoadout.Entry.Empty;
		switch (kind)
		{
			case SlotLoadout.EntryKind.UserFunction: return SlotLoadout.Entry.Function(row.EntryId);
			case SlotLoadout.EntryKind.Skill: return SlotLoadout.Entry.Skill(row.EntryId);
			default: return SlotLoadout.Entry.Empty;
		}
	}
}
#endif
