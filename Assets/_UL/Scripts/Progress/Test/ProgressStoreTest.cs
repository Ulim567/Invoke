#if UNITY_SERVER || UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using UnityEngine;

/// <summary>
/// ProgressStore(진행도 DB)를 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - 아무 씬의 아무 오브젝트에 붙이고 Play한다. 네트워크를 켜지 않아도 된다.<br/>
/// - Console에 항목마다 [통과]/[실패]가 나오고, 마지막에 통과 개수가 나온다.<br/>
/// - 실제 DB와 섞이지 않도록 테스트 전용 파일(ProgressTest.db)을 쓰고, Play할 때마다 지우고 새로 만든다.
/// </para>
/// </summary>
public class ProgressStoreTest : MonoBehaviour
{
	const string FileName = "ProgressTest.db";
	const string Bug = "bug_fragment";
	const string BossCore = "boss_core";

	int passed;
	int total;

	void Start()
	{
		string path = Path.Combine(Application.persistentDataPath, FileName);
		if (File.Exists(path)) File.Delete(path);

		// 처음 해금 목록은 SkillTree 에셋에서 오므로, 기대값도 SkillTree에서 만든다
		List<string> startingNodes = SkillTree.StartingNodes();
		startingNodes.AddRange(SkillTree.AutoUnlockedBy(startingNodes));

		FunctionData function = FunctionData.Create("TestFunction");
		var globals = new GlobalVariablesData();
		globals.Declarations.Add(BlockData.Declare(VariableType.PointerTo(VariableType.StructOf("Sword")), "mySword", SlotData.FromNull()));

		string characterKey;
		string oldCharacterKey;
		string partialCharacterKey;
		List<string> firstSlotGuids;
		List<string> partialSlotGuids;
		using (var store = new AccountStore(path))
		{
			store.SignUp("progress", "password123", "Tester", out string accountKey);
			store.CreateCharacter(accountKey, CharacterLanguages.C, out CharacterRecord character);
			characterKey = character.Key;

			store.SignUp("oldchar", "password123", "OldTester", out string oldAccountKey);
			store.CreateCharacter(oldAccountKey, CharacterLanguages.C, out CharacterRecord oldCharacter);
			oldCharacterKey = oldCharacter.Key;

			store.SignUp("partial", "password123", "Partial", out string partialAccountKey);
			store.CreateCharacter(partialAccountKey, CharacterLanguages.C, out CharacterRecord partialCharacter);
			partialCharacterKey = partialCharacter.Key;

			store.SignUp("replaced", "password123", "Replaced", out string replacedAccountKey);
			store.CreateCharacter(replacedAccountKey, CharacterLanguages.C, out CharacterRecord replacedCharacter);

			ProgressStore progress = store.Progress;

			// 진행도 row만 빠질 캐릭터: 페이지 함수를 먼저 저장해 둔다
			progress.SaveFunction(partialCharacterKey, function.Guid, FunctionDataJson.ToJson(function));
			partialSlotGuids = SlotGuids(progress.Load(partialCharacterKey));

			// 새 캐릭터의 처음 값
			CharacterProgressData first = progress.Load(characterKey);
			firstSlotGuids = SlotGuids(first);
			Check("처음 레벨 1, 경험치 0, 돈 0", first.Level == 1 && first.Exp == 0 && first.Money == 0);
			Check("처음 재료·전역 변수 없음", first.Materials.Count == 0 && first.Globals.Declarations.Count == 0);
			Check($"처음 해금 = SkillTree 처음 노드 ({string.Join(", ", startingNodes)})", startingNodes.Count > 0 && first.Unlocked.OrderBy(x => x).SequenceEqual(startingNodes.OrderBy(x => x)));
			Check("처음 함수 = 슬롯 함수 8개, 페이지 함수 0개", first.Functions.Count == SlotLoadout.SlotCount && progress.CountPageFunctions(characterKey) == 0);
			Check("처음 슬롯 = slot1~slot8, 파라미터 없음, slot1 thrust(GetBug()), slot2 sword_throw(GetBug()), 나머지 빈 본문", IsInitialSlots(first));
			// 자세 함수는 스킬 트리에서 해금해야 받으므로 처음 개수에 없다 (#545)
			Check("처음 개수 = thrust 1, sword_throw 1, get_bug 2, printf 1, 자세 함수 없음 (#500, #536, #545)", SameCounts(first.ItemCounts, new Dictionary<string, int>
				{ { PredefinedFunctionIds.Thrust, 1 }, { PredefinedFunctionIds.SwordThrow, 1 }, { PredefinedFunctionIds.GetBug, 2 }, { PredefinedFunctionIds.Printf, 1 } }));
			// 출력 함수만 슬롯 함수가 쓰지 않는 1개를 더 받는다 (#536)
			Dictionary<string, int> slotUsage = BlockUsageCounter.Count(firstSlotGuids, first.Functions, first.Globals);
			slotUsage[PredefinedFunctionIds.Printf] = ItemCountRules.InitialPrintCount;
			Check("처음 슬롯 함수가 쓰는 개수 + printf 1 = 처음 가진 개수 (남은 개수는 printf 1, 나머지 0)", SameCounts(slotUsage, first.ItemCounts));
			Dictionary<string, int> pyInitial = ItemCountRules.Initial(CharacterLanguages.Python);
			Check("새 Python 캐릭터도 자세 함수 처음 개수 없음 (#545)",
				!pyInitial.ContainsKey(PredefinedFunctionIds.PyInitStance) && !pyInitial.ContainsKey(PredefinedFunctionIds.PyGetStance));
			SkillTree.TryGet(CStanceRule.OddNode, out SkillTreeNode ruleNode);
			SkillTree.TryGet(PredefinedFunctionIds.InitStance, out SkillTreeNode initNode);
			Check("규칙 노드를 해금하면 개수를 주지 않고, 자세 정하기는 init_stance 1개 (#545)",
				ruleNode != null && ItemCountRules.KeysUnlockedBy(ruleNode).Count == 0
				&& initNode != null && ItemCountRules.KeysUnlockedBy(initNode).SequenceEqual(new[] { PredefinedFunctionIds.InitStance }));

			// 저장
			progress.SaveProgress(characterKey, 5, 30, 500);
			progress.SetMaterialCount(characterKey, Bug, 10);
			progress.SetMaterialCount(characterKey, BossCore, 2);
			progress.SetMaterialCount(characterKey, BossCore, 0);
			progress.SaveUnlock(characterKey, new List<string> { "syntax.variable", "removed.node" }, new Dictionary<string, int> { { Bug, 8 } });
			progress.SaveUnlock(characterKey, new List<string> { "syntax.variable" }, null);
			// 블록·함수 개수 (#500): 넣기, 최대 개수에서 멈춤, 0이면 row를 지움, 해금과 한 트랜잭션으로 지급
			progress.SetItemCount(characterKey, "Declare", 3);
			progress.SetItemCount(characterKey, "For", ItemCountRules.MaxCount + 50);
			progress.SetItemCount(characterKey, PredefinedFunctionIds.Thrust, 0);
			progress.SaveUnlock(characterKey, new List<string> { "syntax.variable" }, null, new Dictionary<string, int> { { "Assign", 1 } });

			Check("새 함수는 없음", !progress.HasFunction(characterKey, function.Guid));
			progress.SaveFunction(characterKey, function.Guid, FunctionDataJson.ToJson(function));
			function.Name = "RenamedFunction";
			progress.SaveFunction(characterKey, function.Guid, FunctionDataJson.ToJson(function));
			Check("같은 페이지 함수를 다시 저장해도 페이지 함수 1개", progress.CountPageFunctions(characterKey) == 1 && progress.HasFunction(characterKey, function.Guid));

			// 슬롯 함수 slot1의 본문을 비워 저장한다. 슬롯 함수는 페이지 함수 개수에 들어가지 않는다
			FunctionData slot1 = first.Functions.Find(f => f.Guid == firstSlotGuids[0]);
			slot1.Blocks.Clear();
			progress.SaveFunction(characterKey, slot1.Guid, FunctionDataJson.ToJson(slot1));
			Check("슬롯 함수를 저장해도 페이지 함수는 1개", progress.CountPageFunctions(characterKey) == 1);
			progress.SaveGlobals(characterKey, FunctionDataJson.ToJson(globals));

			Check("다른 캐릭터의 진행도는 그대로", progress.Load(oldCharacterKey).Level == 1 && progress.CountPageFunctions(oldCharacterKey) == 0);

			// 함수 지우기 (#494): 지운 페이지 함수는 개수에서 빠지고, 없는 함수와 다른 캐릭터의 함수는 지우지 않는다
			FunctionData doomed = FunctionData.Create("Doomed");
			progress.SaveFunction(characterKey, doomed.Guid, FunctionDataJson.ToJson(doomed));
			progress.SaveFunction(oldCharacterKey, doomed.Guid, FunctionDataJson.ToJson(doomed));
			bool deleted = progress.DeleteFunction(characterKey, doomed.Guid);
			Check("페이지 함수를 지우면 true, row가 없어지고 페이지 함수 1개로 돌아감",
				deleted && !progress.HasFunction(characterKey, doomed.Guid) && progress.CountPageFunctions(characterKey) == 1);
			Check("없는 함수를 지우면 false", !progress.DeleteFunction(characterKey, doomed.Guid));
			Check("다른 캐릭터의 같은 GUID 함수는 그대로", progress.HasFunction(oldCharacterKey, doomed.Guid));
			progress.DeleteFunction(oldCharacterKey, doomed.Guid);

			// 테스트 데이터 넣기: 슬롯 0만 data의 함수, 슬롯 1은 옛 스킬 장착, 나머지 슬롯은 빠짐. 페이지 함수는 제한보다 2개 많이 넣는다
			var replaced = new CharacterProgressData { Level = 12 };
			FunctionData customSlot = FunctionData.Create(ProgressRules.SlotFunctionName(0));
			replaced.Functions.Add(customSlot);
			replaced.Slots = new[] { SlotLoadout.Entry.Function(customSlot.Guid), SlotLoadout.Entry.Skill(PredefinedFunctionIds.Thrust) };
			for (int i = 0; i < ProgressRules.MaxPageFunctions + 2; i++) replaced.Functions.Add(FunctionData.Create($"Page{i}"));
			progress.ReplaceAll(replacedCharacter.Key, replaced);
			CharacterProgressData replacedLoaded = progress.Load(replacedCharacter.Key);
			Check("ReplaceAll: 슬롯 0은 data의 함수, 나머지 슬롯은 새 슬롯 함수",
				replacedLoaded.Level == 12 && replacedLoaded.Slots[0].Id == customSlot.Guid && AllSlotsAreFunctions(replacedLoaded)
				&& replacedLoaded.Functions.Find(f => f.Guid == replacedLoaded.Slots[1].Id)?.Name == ProgressRules.SlotFunctionName(1));
			Check($"ReplaceAll: 페이지 함수는 {ProgressRules.MaxPageFunctions}개까지",
				progress.CountPageFunctions(replacedCharacter.Key) == ProgressRules.MaxPageFunctions
				&& replacedLoaded.Functions.Count == SlotLoadout.SlotCount + ProgressRules.MaxPageFunctions);
			Check("ReplaceAll: 개수를 비우면 처음 개수", SameCounts(replacedLoaded.ItemCounts, ItemCountRules.Initial()));

			var replacedWithCounts = new CharacterProgressData();
			replacedWithCounts.ItemCounts["If"] = 4;
			progress.ReplaceAll(replacedCharacter.Key, replacedWithCounts);
			Check("ReplaceAll: 개수를 적으면 적은 개수만", SameCounts(progress.Load(replacedCharacter.Key).ItemCounts, new Dictionary<string, int> { { "If", 4 } }));
		}

		// 이 기능 전에 만든 캐릭터처럼 진행도 row가 없는 캐릭터를 만든다
		using (var raw = new SQLiteConnection(path))
		{
			foreach (string table in new[] { "CharacterProgress", "CharacterItemCounts", "CharacterUnlocks", "CharacterSlots", "CharacterFunctions", "CharacterGlobals" })
				raw.Execute($"DELETE FROM {table} WHERE CharacterKey = ?", oldCharacterKey);
			raw.Execute("DELETE FROM CharacterProgress WHERE CharacterKey = ?", partialCharacterKey);
		}

		// DB를 닫았다가 다시 열어도 남아 있는지 (서버를 껐다 켠 경우)
		using (var store = new AccountStore(path))
		{
			CharacterProgressData loaded = store.Progress.Load(characterKey);
			Check("다시 열어도 레벨 5, 경험치 30, 돈 500", loaded.Level == 5 && loaded.Exp == 30 && loaded.Money == 500);
			Check("재료: 버그 조각 8개, 0개로 만든 보스 코어는 없음", loaded.Materials.Count == 1 && loaded.Materials.TryGetValue(Bug, out int bug) && bug == 8);
			Check("해금: 처음 노드 + syntax.variable, 같은 노드는 한 번만", loaded.Unlocked.Count == startingNodes.Count + 1 && loaded.Unlocked.Contains("syntax.variable"));
			Check("SkillTree에 없는 노드(removed.node)는 건너뜀", !loaded.Unlocked.Contains("removed.node"));
			Check("슬롯 GUID는 그대로, slot1은 빈 본문", SlotGuids(loaded).SequenceEqual(firstSlotGuids)
				&& loaded.Functions.Find(f => f.Guid == firstSlotGuids[0]).Blocks.Count == 0);
			FunctionData page = loaded.Functions.Find(f => f.Guid == function.Guid);
			Check("페이지 함수는 마지막에 저장한 내용", loaded.Functions.Count == SlotLoadout.SlotCount + 1 && page != null && page.Name == "RenamedFunction");
			Check("전역 변수 선언 1개 (mySword)", loaded.Globals.Declarations.Count == 1);
			Check($"개수: Declare 3, For {ItemCountRules.MaxCount}(최대에서 멈춤), Assign 1(해금과 함께), 0으로 만든 thrust는 없음, sword_throw 1, get_bug 2, printf 1(#536)",
				SameCounts(loaded.ItemCounts, new Dictionary<string, int>
				{
					{ "Declare", 3 }, { "For", ItemCountRules.MaxCount }, { "Assign", 1 },
					{ PredefinedFunctionIds.SwordThrow, 1 }, { PredefinedFunctionIds.GetBug, 2 },
					{ PredefinedFunctionIds.Printf, ItemCountRules.InitialPrintCount },
				}));

			CharacterProgressData old = store.Progress.Load(oldCharacterKey);
			Check("진행도 row가 없던 캐릭터는 처음 값으로 채움", old.Level == 1 && IsInitialSlots(old) && old.Functions.Count == SlotLoadout.SlotCount && old.Unlocked.Count == startingNodes.Count);
			Check("개수 row가 없던 캐릭터는 처음 개수로 채움", SameCounts(old.ItemCounts, ItemCountRules.Initial()));

			CharacterProgressData partial = store.Progress.Load(partialCharacterKey);
			Check("진행도 row만 빠진 캐릭터: 예외 없이 레벨 1, 슬롯 함수·페이지 함수·해금은 그대로",
				partial.Level == 1 && partial.Functions.Count == SlotLoadout.SlotCount + 1 && SlotGuids(partial).SequenceEqual(partialSlotGuids)
				&& partial.Unlocked.Count == startingNodes.Count);

			// 깨진 JSON은 건너뛴다 (경고 로그 2개가 나오는 것이 맞다)
			store.Progress.SaveFunction(characterKey, "broken-guid", "{ not json");
			store.Progress.SaveGlobals(characterKey, "{ not json");
			CharacterProgressData broken = store.Progress.Load(characterKey);
			Check("깨진 함수 JSON은 건너뛰고 나머지 함수는 불러옴", broken.Functions.Count == SlotLoadout.SlotCount + 1);
			Check("깨진 함수 row도 페이지 함수 개수에 들어감", store.Progress.CountPageFunctions(characterKey) == 2);
			Check("깨진 전역 변수 JSON은 전역 변수 없음", broken.Globals != null && broken.Globals.Declarations.Count == 0);
		}

		string summary = $"[ProgressStoreTest] {passed}/{total} 통과\n파일: {path}";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	static List<string> SlotGuids(CharacterProgressData data) => data.Slots.Select(entry => entry.Id).ToList();

	// 두 개수 사전의 항목과 개수가 모두 같으면 true
	static bool SameCounts(IReadOnlyDictionary<string, int> a, IReadOnlyDictionary<string, int> b) =>
		a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out int count) && count == pair.Value);

	// 슬롯마다 서로 다른 함수(EntryKind.UserFunction)가 있고, 그 함수가 Functions에 있으면 true
	static bool AllSlotsAreFunctions(CharacterProgressData data)
	{
		var guids = new HashSet<string>();
		foreach (SlotLoadout.Entry entry in data.Slots)
		{
			if (entry.Kind != SlotLoadout.EntryKind.UserFunction || !guids.Add(entry.Id)) return false;
			if (!data.Functions.Exists(f => f.Guid == entry.Id)) return false;
		}
		return guids.Count == SlotLoadout.SlotCount;
	}

	// 새 캐릭터와 같은 슬롯 함수 8개이면 true
	static bool IsInitialSlots(CharacterProgressData data)
	{
		if (!AllSlotsAreFunctions(data)) return false;
		string[] skills = { PredefinedFunctionIds.Thrust, PredefinedFunctionIds.SwordThrow };
		for (int i = 0; i < data.Slots.Length; i++)
		{
			FunctionData function = data.Functions.Find(f => f.Guid == data.Slots[i].Id);
			if (function.Name != ProgressRules.SlotFunctionName(i) || function.Parameters.Count != 0) return false;
			if (i >= skills.Length)
			{
				if (function.Blocks.Count != 0) return false;
				continue;
			}
			// 스킬 호출 블록 하나, 그 첫 값 칸에 GetBug 호출 블록
			if (function.Blocks.Count != 1) return false;
			BlockData call = function.Blocks[0];
			if (call.FunctionId != skills[i] || call.IsUserFunction || call.Slots.Count < 1) return false;
			BlockData argument = call.Slots[0].Block;
			if (argument == null || argument.FunctionId != PredefinedFunctionIds.GetBug) return false;
		}
		return true;
	}

	void Check(string name, bool ok)
	{
		total++;
		if (ok)
		{
			passed++;
			Debug.Log($"[ProgressStoreTest] [통과] {name}");
		}
		else
		{
			Debug.LogError($"[ProgressStoreTest] [실패] {name}");
		}
	}
}
#endif
