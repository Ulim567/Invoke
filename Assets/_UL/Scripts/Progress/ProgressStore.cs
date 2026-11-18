#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using SQLite;
using UnityEngine;

/// <summary>
/// 캐릭터의 진행도를 DB(SQLite)에 저장하고 읽는 곳. 서버에서만 쓴다.
/// <para>
/// - 진행도 테이블 8개(CharacterProgress, CharacterMaterials, CharacterItemCounts, CharacterUnlocks, CharacterSlots, CharacterFunctions, CharacterGlobals, CharacterShopOffers)만 다룬다. 계정·캐릭터는 AccountStore가 다룬다.<br/>
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

	// 블록·함수의 가진 개수 (#500). 모양은 재료와 같다. 블록을 놓거나 지워도 바뀌지 않는다 (남은 개수는 함수를 세어 계산)
	[Table("CharacterItemCounts")]
	class ItemCountRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterItem", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		// 항목 ID (ItemCountRules, BlockUsageCounter.KeyOf)
		[Indexed(Name = "CharacterItem", Order = 2, Unique = true)]
		public string ItemId { get; set; }
		// 1 이상 ItemCountRules.MaxCount 이하. 0개가 되면 row를 지운다
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

	// 상점 칸 (#534). 캐릭터마다 칸 8개(ShopRules.SlotCount). 구매·새로고침 때만 바뀐다 (상점을 열 때는 바뀌지 않음)
	[Table("CharacterShopOffers")]
	class ShopOfferRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		[Indexed(Name = "CharacterShopSlot", Order = 1, Unique = true)]
		public string CharacterKey { get; set; }
		// 칸 번호 0~7 (0~3 문법 블록, 4~7 함수)
		[Indexed(Name = "CharacterShopSlot", Order = 2, Unique = true)]
		public int SlotIndex { get; set; }
		// 항목 ID (가진 개수와 같은 ID). 빈 글자면 빈 칸
		public string ItemId { get; set; }
		// 가격 "재료ID:개수"를 쉼표로 이은 글자 (ShopPrices.ToText). 재료가 늘어도 column을 늘리지 않게 글자 하나로 둔다
		public string Price { get; set; }
	}

	/// <summary>AccountStore가 DB를 열 때 만든다. 테이블이 없으면 만든다.</summary>
	internal ProgressStore(SQLiteConnection db)
	{
		this.db = db;
		db.CreateTable<ProgressRow>();
		db.CreateTable<MaterialRow>();
		db.CreateTable<ItemCountRow>();
		db.CreateTable<UnlockRow>();
		db.CreateTable<SlotRow>();
		db.CreateTable<FunctionRow>();
		db.CreateTable<GlobalsRow>();
		db.CreateTable<ShopOfferRow>();
	}

	/// <summary>
	/// 새 캐릭터의 처음 row를 넣는다. AccountStore.CreateCharacter가 Characters row와 같은 트랜잭션 안에서 부른다.<br/>
	/// 레벨 1, 경험치·돈 0, 재료·페이지 함수 없음, 해금은 SkillTree의 처음 노드, 전역 변수 없음, 블록·함수 개수는 ItemCountRules.Initial.<br/>
	/// 슬롯 함수 8개(CharacterProgressData.NewSlotFunction)를 CharacterFunctions에 넣고, CharacterSlots 0~7에 그 GUID를 넣는다.<br/>
	/// 이미 있는 row는 건드리지 않고 없는 row만 넣는다. Load가 진행도 row가 빠진 캐릭터를 채울 때도 쓰기 때문이다.
	/// </summary>
	// 남아 있는 row를 지우지 않는 이유: 진행도 row만 빠진 캐릭터의 함수·재료·슬롯 함수를 잃지 않게 하기 위함
	// 있는 row에 다시 넣으면 unique index 때문에 예외가 나고, 「들어가기」 요청을 처리하던 서버가 그 연결을 끊는다
	public void CreateInitial(string characterKey)
	{
		// 처음 슬롯 함수, 처음 개수(#486), 처음 해금(#523)은 캐릭터 언어마다 다르다
		string language = LanguageOf(characterKey);
		List<string> unlocked = StartingUnlocks(language);

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
				InsertSlotFunction(characterKey, i, CharacterProgressData.NewSlotFunction(i, language));
			}

			if (db.Find<GlobalsRow>(characterKey) == null)
				db.Insert(new GlobalsRow { CharacterKey = characterKey, Json = FunctionDataJson.ToJson(new GlobalVariablesData()) });

			InsertInitialItemCountsIfNone(characterKey);
		});
	}

	// 개수 row가 하나도 없는 캐릭터에만 처음 개수를 넣는다. 트랜잭션 안에서 부른다
	// 가진 개수는 줄지 않고 처음 개수는 0보다 크므로, row가 하나도 없으면 개수 기능(#500) 전에 만든 캐릭터다
	void InsertInitialItemCountsIfNone(string characterKey)
	{
		if (db.Table<ItemCountRow>().Where(r => r.CharacterKey == characterKey).Count() > 0) return;
		foreach (KeyValuePair<string, int> pair in ItemCountRules.Initial(LanguageOf(characterKey))) WriteItemCount(characterKey, pair.Key, pair.Value);
	}

	// 캐릭터의 언어 (#486). 캐릭터 테이블(AccountStore와 같은 DB)에서 읽는다. 캐릭터 row가 없으면(테스트 등) C
	// 진행도 함수의 매개변수로 받지 않는 이유: 처음 값을 넣는 곳(CreateInitial, Load, ReplaceAll)이 여럿이라 모두 같은 언어를 쓰게 하기 위함
	string LanguageOf(string characterKey) => db.Find<CharacterRecord>(characterKey)?.Language ?? CharacterLanguages.C;

	// 그 언어의 처음 해금: 처음 노드와 같이 열리는 노드 (#523)
	static List<string> StartingUnlocks(string language)
	{
		List<string> unlocked = SkillTree.StartingNodes(language);
		unlocked.AddRange(SkillTree.AutoUnlockedBy(unlocked, language));
		return unlocked;
	}

	// 그 언어의 처음 해금 가운데 빠진 노드를 넣는다. 트랜잭션 안에서 부른다 (#523)
	// 파이썬 처음 노드(지금은 매직 미사일·저주)가 해금 목록에 없는 캐릭터(스킬 트리·처음 노드가 바뀌기 전에 만든 파이썬 캐릭터)는 불러올 때 채운다
	void InsertMissingStartingUnlocks(string characterKey)
	{
		foreach (string id in StartingUnlocks(LanguageOf(characterKey)))
		{
			if (db.Table<UnlockRow>().Where(r => r.CharacterKey == characterKey && r.NodeId == id).Count() == 0)
				db.Insert(new UnlockRow { CharacterKey = characterKey, NodeId = id });
		}
	}

	/// <summary>
	/// 캐릭터의 진행도를 테이블 8개에서 읽어 돌려준다. 「들어가기」 때 서버가 부른다.<br/>
	/// 진행도 row가 없는 캐릭터(이 기능 전에 만든 캐릭터)는 새 캐릭터와 같은 처음 row를 넣은 뒤 돌려준다.<br/>
	/// 개수 row가 없는 캐릭터(#500 전에 만든 캐릭터, 개수를 비운 테스트 진행도)는 처음 개수를 넣은 뒤 돌려준다.<br/>
	/// 그 언어의 처음 해금이 빠진 캐릭터(#523 전에 만든 파이썬 캐릭터)는 빠진 노드를 넣은 뒤 돌려준다.<br/>
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

		db.RunInTransaction(() => InsertInitialItemCountsIfNone(characterKey));
		foreach (ItemCountRow row in db.Table<ItemCountRow>().Where(r => r.CharacterKey == characterKey))
			data.ItemCounts[row.ItemId] = row.Count;

		db.RunInTransaction(() => InsertMissingStartingUnlocks(characterKey));
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

		data.ShopOffers = LoadShopOffers(characterKey);
		return data;
	}

	// 상점 칸 8개를 칸 번호 순서로 읽는다 (#534). 하나라도 빠졌으면 빈 목록을 돌려주고, 상점을 열 때 서버가 8칸을 새로 뽑는다
	List<ShopOffer> LoadShopOffers(string characterKey)
	{
		var offers = new ShopOffer?[ShopRules.SlotCount];
		foreach (ShopOfferRow row in db.Table<ShopOfferRow>().Where(r => r.CharacterKey == characterKey))
		{
			if (row.SlotIndex < 0 || row.SlotIndex >= offers.Length) continue;
			offers[row.SlotIndex] = new ShopOffer(row.ItemId, ShopPrices.FromText(row.Price));
		}
		var list = new List<ShopOffer>();
		foreach (ShopOffer? offer in offers)
		{
			if (offer == null) return new List<ShopOffer>();
			list.Add(offer.Value);
		}
		return list;
	}

	/// <summary>
	/// [테스트용] 캐릭터의 진행도 테이블 8개를 data로 모두 바꾼다. 한 트랜잭션이므로 예전 값이 섞여 남지 않는다.<br/>
	/// 상점 칸(#534)은 지우기만 한다. 다음에 상점을 열 때 서버가 바뀐 해금 목록으로 새로 뽑는다.<br/>
	/// 개수(ItemCounts)를 비우면 처음 개수를 넣는다. 개수는 ItemCountRules.MaxCount에서 멈춘다.<br/>
	/// 슬롯은 8개에 맞춘다. 슬롯의 함수가 data.Functions에 없으면 새 캐릭터와 같은 슬롯 함수를 만든다.<br/>
	/// 페이지 함수(슬롯에 걸리지 않은 함수)는 ProgressRules.MaxPageFunctions개까지만 넣고, GUID가 없는 함수는 건너뛴다.
	/// </summary>
	public void ReplaceAll(string characterKey, CharacterProgressData data)
	{
		db.RunInTransaction(() =>
		{
			foreach (string table in new[] { "CharacterProgress", "CharacterMaterials", "CharacterItemCounts", "CharacterUnlocks", "CharacterSlots", "CharacterFunctions", "CharacterGlobals", "CharacterShopOffers" })
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

			if (data.ItemCounts != null)
			{
				foreach (KeyValuePair<string, int> pair in data.ItemCounts)
				{
					if (!string.IsNullOrEmpty(pair.Key)) WriteItemCount(characterKey, pair.Key, pair.Value);
				}
			}
			InsertInitialItemCountsIfNone(characterKey);

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
					InsertSlotFunction(characterKey, i, CharacterProgressData.NewSlotFunction(i, LanguageOf(characterKey)));
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

	/// <summary>블록·함수 항목 하나의 가진 개수를 정한다 (#500). 0 이하면 row를 지우고, ItemCountRules.MaxCount에서 멈춘다.</summary>
	public void SetItemCount(string characterKey, string itemId, int count)
	{
		db.RunInTransaction(() => WriteItemCount(characterKey, itemId, count));
	}

	/// <summary>
	/// 노드 해금, 재료 개수 변경, 블록·함수 지급을 한 트랜잭션으로 쓴다. 재료만 빠지고 노드가 안 열리는 일이 없게 하기 위함이다.<br/>
	/// nodeIds: 새로 연 노드(같이 열린 노드 포함). 이미 있는 노드는 건너뛴다. materialCounts: 바뀐 재료의 새 개수(뺀 뒤의 개수).
	/// itemCounts: 바뀐 블록·함수 항목의 새 가진 개수(지급한 뒤의 개수). 없으면 null.
	/// </summary>
	public void SaveUnlock(string characterKey, IReadOnlyList<string> nodeIds, IReadOnlyDictionary<string, int> materialCounts,
		IReadOnlyDictionary<string, int> itemCounts = null)
	{
		db.RunInTransaction(() =>
		{
			foreach (string id in nodeIds)
			{
				bool exists = db.Table<UnlockRow>().Where(r => r.CharacterKey == characterKey && r.NodeId == id).Count() > 0;
				if (!exists) db.Insert(new UnlockRow { CharacterKey = characterKey, NodeId = id });
			}
			if (materialCounts != null)
			{
				foreach (KeyValuePair<string, int> pair in materialCounts) WriteMaterialCount(characterKey, pair.Key, pair.Value);
			}
			if (itemCounts != null)
			{
				foreach (KeyValuePair<string, int> pair in itemCounts) WriteItemCount(characterKey, pair.Key, pair.Value);
			}
		});
	}

	/// <summary>
	/// 상점의 구매·새로고침·처음 칸을 한 트랜잭션으로 쓴다 (#534). 재료만 빠지고 칸이 안 바뀌는 일이 없게 하기 위함이다.<br/>
	/// materialCounts: 바뀐 재료의 새 개수(뺀 뒤의 개수). itemCounts: 바뀐 블록·함수의 새 가진 개수. offers: 칸 번호 → 새 상품.
	/// 바뀌지 않는 것은 null.
	/// </summary>
	public void SaveShop(string characterKey, IReadOnlyDictionary<string, int> materialCounts, IReadOnlyDictionary<string, int> itemCounts,
		IReadOnlyDictionary<int, ShopOffer> offers)
	{
		db.RunInTransaction(() =>
		{
			if (materialCounts != null)
			{
				foreach (KeyValuePair<string, int> pair in materialCounts) WriteMaterialCount(characterKey, pair.Key, pair.Value);
			}
			if (itemCounts != null)
			{
				foreach (KeyValuePair<string, int> pair in itemCounts) WriteItemCount(characterKey, pair.Key, pair.Value);
			}
			if (offers != null)
			{
				foreach (KeyValuePair<int, ShopOffer> pair in offers) WriteShopOffer(characterKey, pair.Key, pair.Value);
			}
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

	/// <summary>
	/// 함수 하나를 지운다 (#494). 그 row가 있어서 지웠으면 true, 없으면 false.<br/>
	/// 슬롯 함수인지(지울 수 없음)와 다른 함수가 부르는지는 부르는 쪽이 먼저 본다. 이 함수는 row만 지운다.
	/// </summary>
	public bool DeleteFunction(string characterKey, string functionGuid)
	{
		FunctionRow row = FindFunction(characterKey, functionGuid);
		if (row == null) return false;
		db.Delete(row);
		return true;
	}

	/// <summary>전역 변수 선언 JSON을 덮어쓴다. 편집 화면이 전역 변수를 한꺼번에 저장하므로 JSON 전체를 바꾼다.</summary>
	public void SaveGlobals(string characterKey, string json)
	{
		db.InsertOrReplace(new GlobalsRow { CharacterKey = characterKey, Json = json });
	}

	/// <summary>
	/// 편집 화면이 고친 파일들을 한 트랜잭션으로 저장한다 (#506). 모두 쓰이거나, 중간에 실패하면 하나도 쓰이지 않는다.<br/>
	/// functions: (GUID, JSON) 목록. 같은 GUID의 row는 덮어쓰고 없으면 넣는다. globalsJson: 전역 변수 선언 JSON, null이면 전역 변수는 그대로 둔다.<br/>
	/// 검사(개수, 해금, 슬롯 함수의 틀 등)는 부르는 쪽이 먼저 한다.
	/// </summary>
	public void SaveFiles(string characterKey, IReadOnlyList<(string Guid, string Json)> functions, string globalsJson)
	{
		db.RunInTransaction(() =>
		{
			foreach ((string guid, string json) in functions)
			{
				FunctionRow row = FindFunction(characterKey, guid);
				if (row == null)
				{
					db.Insert(new FunctionRow { CharacterKey = characterKey, FunctionGuid = guid, Json = json });
				}
				else
				{
					row.Json = json;
					db.Update(row);
				}
			}
			if (globalsJson != null) db.InsertOrReplace(new GlobalsRow { CharacterKey = characterKey, Json = globalsJson });
		});
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

	// 트랜잭션 안에서 부른다. 0 이하면 row를 지우고, 최대 개수를 넘으면 최대 개수로 쓴다
	void WriteItemCount(string characterKey, string itemId, int count)
	{
		count = Math.Min(count, ItemCountRules.MaxCount);
		ItemCountRow row = db.Table<ItemCountRow>().Where(r => r.CharacterKey == characterKey && r.ItemId == itemId).FirstOrDefault();
		if (count <= 0)
		{
			if (row != null) db.Delete(row);
		}
		else if (row == null)
		{
			db.Insert(new ItemCountRow { CharacterKey = characterKey, ItemId = itemId, Count = count });
		}
		else
		{
			row.Count = count;
			db.Update(row);
		}
	}

	// 상점 칸 하나를 넣거나 덮어쓴다. 트랜잭션 안에서 부른다
	void WriteShopOffer(string characterKey, int slot, ShopOffer offer)
	{
		ShopOfferRow row = db.Table<ShopOfferRow>().Where(r => r.CharacterKey == characterKey && r.SlotIndex == slot).FirstOrDefault();
		if (row == null)
		{
			db.Insert(new ShopOfferRow { CharacterKey = characterKey, SlotIndex = slot, ItemId = offer.ItemId ?? "", Price = offer.PriceText });
		}
		else
		{
			row.ItemId = offer.ItemId ?? "";
			row.Price = offer.PriceText;
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
