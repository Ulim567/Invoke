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

		// 처음 해금 목록과 처음 슬롯은 SkillTree 에셋에서 오므로, 기대값도 SkillTree에서 만든다
		List<string> startingNodes = SkillTree.StartingNodes();
		startingNodes.AddRange(SkillTree.AutoUnlockedBy(startingNodes));
		List<string> startingSkills = startingNodes.Where(id => SkillTree.TryGet(id, out SkillTreeNode node) && node.Kind == SkillTreeNodeKind.Skill).ToList();

		FunctionData function = FunctionData.Create("TestFunction");
		var globals = new GlobalVariablesData();
		globals.Declarations.Add(BlockData.Declare(VariableType.PointerTo(VariableType.StructOf("Sword")), "mySword", SlotData.FromNull()));

		string characterKey;
		string oldCharacterKey;
		using (var store = new AccountStore(path))
		{
			store.SignUp("progress", "password123", "Tester", out string accountKey);
			store.CreateCharacter(accountKey, CharacterLanguages.C, out CharacterRecord character);
			characterKey = character.Key;

			store.SignUp("oldchar", "password123", "OldTester", out string oldAccountKey);
			store.CreateCharacter(oldAccountKey, CharacterLanguages.C, out CharacterRecord oldCharacter);
			oldCharacterKey = oldCharacter.Key;

			ProgressStore progress = store.Progress;

			// 새 캐릭터의 처음 값
			CharacterProgressData first = progress.Load(characterKey);
			Check("처음 레벨 1, 경험치 0, 돈 0", first.Level == 1 && first.Exp == 0 && first.Money == 0);
			Check("처음 재료·함수·전역 변수 없음", first.Materials.Count == 0 && first.Functions.Count == 0 && first.Globals.Declarations.Count == 0);
			Check($"처음 해금 = SkillTree 처음 노드 ({string.Join(", ", startingNodes)})", startingNodes.Count > 0 && first.Unlocked.OrderBy(x => x).SequenceEqual(startingNodes.OrderBy(x => x)));
			Check($"처음 슬롯 = 처음 해금한 스킬을 슬롯 0부터 ({string.Join(", ", startingSkills)})", SlotsMatch(first.Slots, startingSkills));

			// 저장
			progress.SaveProgress(characterKey, 5, 30, 500);
			progress.SetMaterialCount(characterKey, Bug, 10);
			progress.SetMaterialCount(characterKey, BossCore, 2);
			progress.SetMaterialCount(characterKey, BossCore, 0);
			progress.SaveUnlock(characterKey, new List<string> { "syntax.variable", "removed.node" }, new Dictionary<string, int> { { Bug, 8 } });
			progress.SaveUnlock(characterKey, new List<string> { "syntax.variable" }, null);

			Check("새 함수는 없음", !progress.HasFunction(characterKey, function.Guid));
			progress.SaveFunction(characterKey, function.Guid, FunctionDataJson.ToJson(function));
			function.Name = "RenamedFunction";
			progress.SaveFunction(characterKey, function.Guid, FunctionDataJson.ToJson(function));
			Check("같은 함수를 다시 저장해도 함수 1개", progress.CountFunctions(characterKey) == 1 && progress.HasFunction(characterKey, function.Guid));

			// 슬롯 0과 1을 바꾸고(Swap), 슬롯 2에 함수를 넣는다
			progress.SaveSlots(characterKey, new Dictionary<int, SlotLoadout.Entry>
			{
				{ 0, first.Slots[1] },
				{ 1, first.Slots[0] },
				{ 2, SlotLoadout.Entry.Function(function.Guid) },
			});
			progress.SaveGlobals(characterKey, FunctionDataJson.ToJson(globals));

			Check("다른 캐릭터의 진행도는 그대로", progress.Load(oldCharacterKey).Level == 1 && progress.CountFunctions(oldCharacterKey) == 0);
		}

		// 이 기능 전에 만든 캐릭터처럼 진행도 row가 없는 캐릭터를 만든다
		using (var raw = new SQLiteConnection(path))
		{
			foreach (string table in new[] { "CharacterProgress", "CharacterUnlocks", "CharacterSlots", "CharacterGlobals" })
				raw.Execute($"DELETE FROM {table} WHERE CharacterKey = ?", oldCharacterKey);
		}

		// DB를 닫았다가 다시 열어도 남아 있는지 (서버를 껐다 켠 경우)
		using (var store = new AccountStore(path))
		{
			CharacterProgressData loaded = store.Progress.Load(characterKey);
			Check("다시 열어도 레벨 5, 경험치 30, 돈 500", loaded.Level == 5 && loaded.Exp == 30 && loaded.Money == 500);
			Check("재료: 버그 조각 8개, 0개로 만든 보스 코어는 없음", loaded.Materials.Count == 1 && loaded.Materials.TryGetValue(Bug, out int bug) && bug == 8);
			Check("해금: 처음 노드 + syntax.variable, 같은 노드는 한 번만", loaded.Unlocked.Count == startingNodes.Count + 1 && loaded.Unlocked.Contains("syntax.variable"));
			Check("SkillTree에 없는 노드(removed.node)는 건너뜀", !loaded.Unlocked.Contains("removed.node"));
			Check("슬롯 0·1이 바뀌고 슬롯 2에 함수",
				SameEntry(loaded.Slots[0], startingSkills.Count > 1 ? SlotLoadout.Entry.Skill(startingSkills[1]) : SlotLoadout.Entry.Empty)
				&& SameEntry(loaded.Slots[1], startingSkills.Count > 0 ? SlotLoadout.Entry.Skill(startingSkills[0]) : SlotLoadout.Entry.Empty)
				&& SameEntry(loaded.Slots[2], SlotLoadout.Entry.Function(function.Guid)));
			Check("함수는 마지막에 저장한 내용", loaded.Functions.Count == 1 && loaded.Functions[0].Guid == function.Guid && loaded.Functions[0].Name == "RenamedFunction");
			Check("전역 변수 선언 1개 (mySword)", loaded.Globals.Declarations.Count == 1);

			CharacterProgressData old = store.Progress.Load(oldCharacterKey);
			Check("진행도 row가 없던 캐릭터는 처음 값으로 채움", old.Level == 1 && SlotsMatch(old.Slots, startingSkills) && old.Unlocked.Count == startingNodes.Count);

			// 깨진 JSON은 건너뛴다 (경고 로그 2개가 나오는 것이 맞다)
			store.Progress.SaveFunction(characterKey, "broken-guid", "{ not json");
			store.Progress.SaveGlobals(characterKey, "{ not json");
			CharacterProgressData broken = store.Progress.Load(characterKey);
			Check("깨진 함수 JSON은 건너뛰고 나머지 함수는 불러옴", broken.Functions.Count == 1 && broken.Functions[0].Guid == function.Guid);
			Check("깨진 전역 변수 JSON은 전역 변수 없음", broken.Globals != null && broken.Globals.Declarations.Count == 0);
		}

		string summary = $"[ProgressStoreTest] {passed}/{total} 통과\n파일: {path}";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	// 슬롯이 skills를 0번부터 순서대로 담고 나머지는 비어 있으면 true
	static bool SlotsMatch(SlotLoadout.Entry[] slots, List<string> skills)
	{
		for (int i = 0; i < slots.Length; i++)
		{
			SlotLoadout.Entry expected = i < skills.Count ? SlotLoadout.Entry.Skill(skills[i]) : SlotLoadout.Entry.Empty;
			if (!SameEntry(slots[i], expected)) return false;
		}
		return true;
	}

	static bool SameEntry(SlotLoadout.Entry a, SlotLoadout.Entry b)
	{
		if (a.IsEmpty || b.IsEmpty) return a.IsEmpty && b.IsEmpty;
		return a.Kind == b.Kind && a.Id == b.Id;
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
