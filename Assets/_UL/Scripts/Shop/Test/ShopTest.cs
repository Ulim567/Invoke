#if UNITY_SERVER || UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// 상점(#534)의 뽑기(ShopRoller)와 서버 판정·저장(PlayerProgress.TryBuyShopItem, TryRefreshShop)을 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - 호스트로 시작한 상태(서버가 켜진 상태)에서 아무 오브젝트에 붙인다. 서버가 꺼져 있으면 Mirror 컬렉션을 바꿀 수 없어 시험하지 않는다.<br/>
/// - Console에 항목마다 [통과]/[실패]가 나오고, 마지막에 통과 개수가 나온다.<br/>
/// - 실제 DB와 섞이지 않도록 테스트 전용 파일(ShopTest.db)을 쓰고, 시작할 때마다 지우고 새로 만든다.<br/>
/// - RequestBuyShopItem과 [Command]·[TargetRpc] 경로는 스폰된 내 플레이어가 필요하므로 이 테스트가 확인하지 않는다.
/// </para>
/// </summary>
public class ShopTest : MonoBehaviour
{
	const string FileName = "ShopTest.db";

	int passed;
	int total;

	void Start()
	{
		TestRoller();
		if (!NetworkServer.active)
		{
			Debug.LogError("[ShopTest] 서버가 켜져 있지 않아 저장·판정 시험은 건너뜁니다. 호스트로 시작한 뒤 붙이십시오.");
			Summary();
			return;
		}
		TestPlayer();
		TestTemporaryAccount();
		Summary();
	}

	// 뽑기 규칙: 후보, 99개 제외, 무게, 가격
	void TestRoller()
	{
		List<string> cStart = StartingUnlocks(CharacterLanguages.C);
		var noItems = new Dictionary<string, int>();

		Check("새 C 캐릭터는 문법 블록 후보가 없음 (순차 실행은 함수·wait만 엶)",
			ShopRoller.SyntaxCandidates(cStart, noItems, CharacterLanguages.C).Count == 0);
		HashSet<string> cFunctions = Ids(ShopRoller.FunctionCandidates(cStart, noItems, CharacterLanguages.C));
		Check("새 C 캐릭터의 함수 후보 = thrust, sword_throw, get_bug, is_valid_target, Wait",
			cFunctions.SetEquals(new[] { PredefinedFunctionIds.Thrust, PredefinedFunctionIds.SwordThrow, PredefinedFunctionIds.GetBug,
				PredefinedFunctionIds.IsValidTarget, "Wait" }));
		Check("대시·더블점프는 후보가 아님",
			!cFunctions.Contains(PredefinedFunctionIds.Dash) && !cFunctions.Contains(PredefinedFunctionIds.DoubleJump));

		ShopOffer[] start = ShopRoller.RollAll(cStart, noItems, CharacterLanguages.C);
		Check("칸은 언제나 8개, 새 C 캐릭터는 문법 4칸이 빈 칸·함수 4칸은 참",
			start.Length == ShopRules.SlotCount && start.Take(4).All(o => o.IsEmpty) && start.Skip(4).All(o => !o.IsEmpty));

		HashSet<string> allSyntaxC = Ids(ShopRoller.SyntaxCandidates(null, noItems, CharacterLanguages.C));
		Check("모두 해금(임시 계정)한 C: If·ElseIf·Else·Declare 있음, Python 블록(Global)·Wait 없음",
			allSyntaxC.Contains("If") && allSyntaxC.Contains("ElseIf") && allSyntaxC.Contains("Else") && allSyntaxC.Contains("Declare")
			&& !allSyntaxC.Contains("Global") && !allSyntaxC.Contains("Wait") && !allSyntaxC.Contains("Conditional"));
		HashSet<string> allFunctionsC = Ids(ShopRoller.FunctionCandidates(null, noItems, CharacterLanguages.C));
		Check("모두 해금한 C의 함수: Malloc·Free·검 소환 있음, Python 스킬·테스트 함수 없음",
			allFunctionsC.Contains("Malloc") && allFunctionsC.Contains("Free") && allFunctionsC.Contains(PredefinedFunctionIds.SummonSword)
			&& !allFunctionsC.Contains(PredefinedFunctionIds.PyFireball) && !allFunctionsC.Any(id => id.StartsWith("test.")));

		HashSet<string> allSyntaxPy = Ids(ShopRoller.SyntaxCandidates(null, noItems, CharacterLanguages.Python));
		HashSet<string> allFunctionsPy = Ids(ShopRoller.FunctionCandidates(null, noItems, CharacterLanguages.Python));
		Check("Python: ListNew 있음, Declare 없음, Malloc 없음, 구슬 확인 있음",
			allSyntaxPy.Contains("ListNew") && !allSyntaxPy.Contains("Declare") && !allFunctionsPy.Contains("Malloc")
			&& allFunctionsPy.Contains(PredefinedFunctionIds.PyIsValidOrb) && !allFunctionsPy.Contains(PredefinedFunctionIds.Thrust));
		Check("무게 없는 Python 블록은 임시 무게 1",
			ShopRoller.SyntaxCandidates(null, noItems, CharacterLanguages.Python).First(c => c.Id == "ListNew").Weight == ShopRules.DefaultSyntaxWeight);

		var fullThrust = new Dictionary<string, int> { [PredefinedFunctionIds.Thrust] = ItemCountRules.MaxCount };
		Check("99개 가진 thrust는 후보가 아님", !Ids(ShopRoller.FunctionCandidates(cStart, fullThrust, CharacterLanguages.C)).Contains(PredefinedFunctionIds.Thrust));

		// 무게: 굴린 값이 가장 작으면 첫 후보, 가장 크면 마지막 후보
		List<(string Id, int Weight)> candidates = ShopRoller.FunctionCandidates(cStart, noItems, CharacterLanguages.C);
		ShopOffer first = ShopRoller.RollSlot(4, cStart, noItems, CharacterLanguages.C, (min, max) => min);
		ShopOffer last = ShopRoller.RollSlot(4, cStart, noItems, CharacterLanguages.C, (min, max) => max);
		Check("무게대로 뽑음 (가장 작은 값 → 첫 후보, 가장 큰 값 → 마지막 후보)",
			first.ItemId == candidates[0].Id && last.ItemId == candidates[candidates.Count - 1].Id);

		ShopPrice[] cheap = ShopRules.RollPrice(0, "If", (min, max) => min);
		ShopPrice[] dear = ShopRules.RollPrice(0, "If", (min, max) => max);
		Check("문법 블록 가격 = 버그 조각 7~13, 보스 코어 없음",
			cheap.Length == 1 && cheap[0].MaterialId == ShopRules.BugFragment && cheap[0].Count == 7 && dear[0].Count == 13);
		ShopPrice[] epic = ShopRules.RollPrice(4, PredefinedFunctionIds.SwordDance, (min, max) => min);
		Check("함수 가격은 희귀도의 범위 (검무 = Epic)", epic[0].Count == ShopRules.RarityRules[ShopRarity.Epic].BugFragmentMin);

		var price = new[] { new ShopPrice(ShopRules.BugFragment, 7), new ShopPrice(ShopRules.BossCore, 1) };
		ShopPrice[] back = ShopPrices.FromText(ShopPrices.ToText(price));
		Check("가격 글자를 저장했다 읽으면 같음 (보스 코어 줄 포함)",
			back.Length == 2 && back[0].MaterialId == ShopRules.BugFragment && back[0].Count == 7 && back[1].MaterialId == ShopRules.BossCore && back[1].Count == 1);
	}

	// 실제 계정: DB에 저장하고 다시 읽어 같은지
	void TestPlayer()
	{
		string path = Path.Combine(Application.persistentDataPath, FileName);
		if (File.Exists(path)) File.Delete(path);

		using (var store = new AccountStore(path))
		{
			store.SignUp("shop", "password123", "Shopper", out string accountKey);
			store.CreateCharacter(accountKey, CharacterLanguages.C, out CharacterRecord character);
			string key = character.Key;

			PlayerProgress progress = SpawnPlayer("ShopTestPlayer");
			CharacterProgressData loaded = store.Progress.Load(key);
			Check("새 캐릭터는 DB에 상점 칸이 없음", loaded.ShopOffers.Count == 0);
			progress.ApplyLoaded(loaded, store.Progress, key);

			progress.PrepareShop();
			Check("상점을 열면 8칸이 생김", progress.ShopOffers.Count == ShopRules.SlotCount);
			string before = Describe(progress.ShopOffers);
			progress.PrepareShop();
			Check("다시 열어도 칸이 그대로", Describe(progress.ShopOffers) == before);
			Check("처음 칸이 DB에 저장됨", Describe(store.Progress.Load(key).ShopOffers) == before);

			Check("칸 번호 9 → InvalidSlot", progress.TryBuyShopItem(9) == ShopResult.InvalidSlot);
			Check("빈 문법 칸 0 → EmptySlot", progress.TryBuyShopItem(0) == ShopResult.EmptySlot);
			Check("재료 없이 함수 칸 4 → MaterialShort", progress.TryBuyShopItem(4) == ShopResult.MaterialShort);
			Check("거부되면 칸 그대로", Describe(progress.ShopOffers) == before);

			progress.AddMaterial(ShopRules.BugFragment, 100);
			ShopOffer bought = progress.ShopOffers[4];
			int price = bought.PriceByMaterial()[ShopRules.BugFragment];
			int ownedBefore = progress.GetItemCount(bought.ItemId);
			Check("재료가 있으면 칸 4 → Done", progress.TryBuyShopItem(4) == ShopResult.Done);
			Check("가격만큼 버그 조각이 줆", progress.GetMaterialCount(ShopRules.BugFragment) == 100 - price);
			Check("산 항목의 가진 개수 +1", progress.GetItemCount(bought.ItemId) == ownedBefore + 1);

			CharacterProgressData afterBuy = store.Progress.Load(key);
			Check("DB의 재료·개수·칸이 메모리와 같음",
				afterBuy.Materials[ShopRules.BugFragment] == progress.GetMaterialCount(ShopRules.BugFragment)
				&& afterBuy.ItemCounts[bought.ItemId] == progress.GetItemCount(bought.ItemId)
				&& Describe(afterBuy.ShopOffers) == Describe(progress.ShopOffers));

			string full = progress.ShopOffers[5].ItemId;
			progress.AddItem(full, ItemCountRules.MaxCount);
			int fragments = progress.GetMaterialCount(ShopRules.BugFragment);
			Check("99개 가진 항목의 칸 → MaxCount", progress.TryBuyShopItem(5) == ShopResult.MaxCount);
			Check("MaxCount면 재료 그대로", progress.GetMaterialCount(ShopRules.BugFragment) == fragments);

			// 새로고침: 비용보다 적으면 거부, 있으면 비용만큼 빼고 8칸을 다시 뽑음
			int cost = ShopPrices.Sum(ShopRules.RefreshCost)[ShopRules.BugFragment];
			SetMaterial(progress, store, key, ShopRules.BugFragment, cost - 1);
			Check($"버그 조각 {cost - 1}개로 새로고침 → MaterialShort", progress.TryRefreshShop() == ShopResult.MaterialShort);
			SetMaterial(progress, store, key, ShopRules.BugFragment, cost + 5);
			Check("새로고침 → Done", progress.TryRefreshShop() == ShopResult.Done);
			Check("새로고침 비용만큼 줆", progress.GetMaterialCount(ShopRules.BugFragment) == 5);
			Check("새로고침한 칸이 DB에 저장됨", Describe(store.Progress.Load(key).ShopOffers) == Describe(progress.ShopOffers));
			Check("99개 항목은 새로고침 뒤 함수 칸에 없음", progress.ShopOffers.Skip(4).All(o => o.ItemId != full));

			NetworkServer.Destroy(progress.gameObject);
		}
	}

	// 임시 계정(DB 없음): 모두 해금으로 뽑고, 사면 재료만 빠짐
	void TestTemporaryAccount()
	{
		PlayerProgress progress = SpawnPlayer("ShopTestTemporary");
		progress.PrepareShop();
		Check("임시 계정은 문법 칸도 참 (모두 해금으로 봄)", progress.ShopOffers.Take(4).All(o => !o.IsEmpty));
		progress.AddMaterial(ShopRules.BugFragment, 100);
		ShopOffer offer = progress.ShopOffers[0];
		int price = offer.PriceByMaterial()[ShopRules.BugFragment];
		Check("임시 계정 구매 → Done", progress.TryBuyShopItem(0) == ShopResult.Done);
		Check("임시 계정: 재료만 빠지고 가진 개수는 그대로",
			progress.GetMaterialCount(ShopRules.BugFragment) == 100 - price && progress.GetItemCount(offer.ItemId) == 0);
		NetworkServer.Destroy(progress.gameObject);
	}

	// 재료 개수를 정한다. 빼는 함수가 따로 없으므로 DB에 쓰고 다시 불러온다
	static void SetMaterial(PlayerProgress progress, AccountStore store, string key, string materialId, int count)
	{
		store.Progress.SetMaterialCount(key, materialId, count);
		progress.ApplyLoaded(store.Progress.Load(key), store.Progress, key);
	}

	// Mirror는 NetworkIdentity가 깨어날 때 그 오브젝트의 NetworkBehaviour만 등록하므로, 비활성 상태로 붙인 뒤 켜고 스폰한다 (UnlockTest와 같음)
	static PlayerProgress SpawnPlayer(string name)
	{
		var playerObject = new GameObject(name);
		playerObject.SetActive(false);
		playerObject.AddComponent<NetworkIdentity>();
		var progress = playerObject.AddComponent<PlayerProgress>();
		playerObject.SetActive(true);
		NetworkServer.Spawn(playerObject);
		return progress;
	}

	static List<string> StartingUnlocks(string language)
	{
		List<string> unlocked = SkillTree.StartingNodes(language);
		unlocked.AddRange(SkillTree.AutoUnlockedBy(unlocked, language));
		return unlocked;
	}

	static HashSet<string> Ids(List<(string Id, int Weight)> candidates) => new HashSet<string>(candidates.Select(c => c.Id));

	static string Describe(IEnumerable<ShopOffer> offers) => string.Join("|", offers.Select(o => $"{o.ItemId}={o.PriceText}"));

	void Summary()
	{
		string summary = $"[ShopTest] {passed}/{total} 통과";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	void Check(string name, bool ok)
	{
		total++;
		if (ok)
		{
			passed++;
			Debug.Log($"[ShopTest] [통과] {name}");
		}
		else
		{
			Debug.LogError($"[ShopTest] [실패] {name}");
		}
	}
}
#endif
