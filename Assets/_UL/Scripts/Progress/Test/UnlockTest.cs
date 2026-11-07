#if UNITY_SERVER || UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEngine;

/// <summary>
/// PlayerProgress의 노드 해금(TryUnlock, RequestUnlock)을 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - 호스트로 시작한 상태(서버가 켜진 상태)에서 아무 오브젝트에 붙인다. 서버가 꺼져 있으면 Mirror 컬렉션을 바꿀 수 없어 시험하지 않는다.<br/>
/// - Console에 항목마다 [통과]/[실패]가 나오고, 마지막에 통과 개수가 나온다.<br/>
/// - 실제 DB와 섞이지 않도록 테스트 전용 파일(UnlockTest.db)을 쓰고, Play할 때마다 지우고 새로 만든다.<br/>
/// - 재료 수는 임시 값이므로 SkillTree의 Cost를 읽어 쓴다. 숫자를 직접 적지 않는다.<br/>
/// - TryUnlock만 시험한다. RequestUnlock과 [Command]·[TargetRpc] 경로는 스폰된 플레이어가 필요하므로 이 테스트가 확인하지 않는다.
/// </para>
/// </summary>
public class UnlockTest : MonoBehaviour
{
	const string FileName = "UnlockTest.db";

	int passed;
	int total;

	void Start()
	{
		// PlayerProgress의 재료·해금 목록은 Mirror의 SyncDictionary, SyncList라서 서버가 켜져 있어야 바꿀 수 있다
		if (!NetworkServer.active)
		{
			Debug.LogError("[UnlockTest] 서버가 켜져 있지 않습니다. 호스트로 시작한 뒤 붙이십시오.");
			return;
		}

		string path = Path.Combine(Application.persistentDataPath, FileName);
		if (File.Exists(path)) File.Delete(path);

		SkillTree.TryGet(PredefinedFunctionIds.FocusedThrust, out SkillTreeNode focused);
		SkillTree.TryGet(PredefinedFunctionIds.SummonSword, out SkillTreeNode summon);
		SkillTree.TryGet(PredefinedFunctionIds.SummonedThrust, out SkillTreeNode summonedThrust);
		if (focused == null || summon == null || summonedThrust == null)
		{
			Debug.LogError("[UnlockTest] SkillTree 에셋에서 시험할 노드(집중찌르기, 검 소환, 소환검 찌르기)를 찾지 못했습니다.");
			return;
		}

		CharacterProgressData loaded;
		using (var store = new AccountStore(path))
		{
			store.SignUp("unlock", "password123", "Unlocker", out string accountKey);
			store.CreateCharacter(accountKey, CharacterLanguages.C, out CharacterRecord character);
			string key = character.Key;

			// Mirror는 NetworkIdentity가 깨어날 때 그 오브젝트에 있는 NetworkBehaviour만 등록한다.
			// 그래서 비활성 상태로 두 컴포넌트를 모두 붙인 뒤 활성화하고, 스폰해야 동기화 컬렉션을 바꿀 수 있다
			var playerObject = new GameObject("UnlockTestPlayer");
			playerObject.SetActive(false);
			playerObject.AddComponent<NetworkIdentity>();
			var progress = playerObject.AddComponent<PlayerProgress>();
			playerObject.SetActive(true);
			NetworkServer.Spawn(playerObject);
			progress.ApplyLoaded(store.Progress.Load(key), store.Progress, key);

			// 재료가 없으면 거부하고 아무것도 바꾸지 않는다
			Check("재료 없이 집중찌르기 → MaterialShort", progress.TryUnlock(focused.Id) == UnlockResult.MaterialShort);
			Check("거부되면 해금 목록 그대로", !progress.Unlocked.Contains(focused.Id));

			// 선행이 없으면 재료가 있어도 거부하고, 재료를 빼지 않는다
			Give(progress, summonedThrust);
			int before = CountOf(progress, summonedThrust);
			Check("선행(검 소환) 없이 소환검 찌르기 → PrerequisiteMissing", progress.TryUnlock(summonedThrust.Id) == UnlockResult.PrerequisiteMissing);
			Check("선행 거부되면 재료 그대로", CountOf(progress, summonedThrust) == before);

			// 선행도 재료도 모자라면 선행 이유가 먼저 나온다 (검사 순서 3이 4보다 먼저)
			Check("선행·재료 모두 모자란 노드(검 소환 뒤 노드 해금 전) → PrerequisiteMissing",
				progress.TryUnlock(PredefinedFunctionIds.SwordRide) == UnlockResult.PrerequisiteMissing);

			// 선행을 열었고 재료가 충분하면 해금하고 재료를 뺀다
			Give(progress, focused);
			int focusedBefore = progress.GetMaterialCount(focused.Cost[0].MaterialId);
			Check("집중찌르기 → Unlocked", progress.TryUnlock(focused.Id) == UnlockResult.Unlocked);
			Check("집중찌르기가 해금 목록에 있음", progress.Unlocked.Contains(focused.Id));
			Check("집중찌르기 재료가 Cost만큼 줆", progress.GetMaterialCount(focused.Cost[0].MaterialId) == focusedBefore - focused.Cost[0].Count);

			// 이미 해금한 노드, 없는 노드
			int afterUnlock = progress.GetMaterialCount(focused.Cost[0].MaterialId);
			Check("이미 해금한 집중찌르기 → AlreadyUnlocked", progress.TryUnlock(focused.Id) == UnlockResult.AlreadyUnlocked);
			Check("AlreadyUnlocked면 재료 그대로", progress.GetMaterialCount(focused.Cost[0].MaterialId) == afterUnlock);
			Check("null → UnknownNode", progress.TryUnlock(null) == UnlockResult.UnknownNode);
			Check("없는 ID → UnknownNode", progress.TryUnlock("no.such.node") == UnlockResult.UnknownNode);

			// 같이 열리는 노드 (검 소환 → 해제)
			Give(progress, summon);
			Check("검 소환 → Unlocked", progress.TryUnlock(summon.Id) == UnlockResult.Unlocked);
			Check("같이 열리는 해제도 해금됨", progress.Unlocked.Contains(PredefinedFunctionIds.FreeSword));

			// 접속을 끊고 다시 들어오는 것을 DB에서 다시 읽는 것으로 대신 확인
			loaded = store.Progress.Load(key);
			Check("DB에 집중찌르기·검 소환·해제가 저장됨",
				loaded.Unlocked.Contains(focused.Id) && loaded.Unlocked.Contains(summon.Id) && loaded.Unlocked.Contains(PredefinedFunctionIds.FreeSword));
			Check("DB의 재료 개수가 메모리와 같음", SameMaterials(loaded, progress, focused, summon, summonedThrust));
			NetworkServer.Destroy(playerObject);
		}

		string summary = $"[UnlockTest] {passed}/{total} 통과";
		if (passed == total) Debug.Log(summary);
		else Debug.LogError(summary);
	}

	// 노드의 Cost만큼 재료를 준다
	static void Give(PlayerProgress progress, SkillTreeNode node)
	{
		foreach (MaterialCost cost in node.Cost) progress.AddMaterial(cost.MaterialId, cost.Count);
	}

	static int CountOf(PlayerProgress progress, SkillTreeNode node) => node.Cost.Sum(c => progress.GetMaterialCount(c.MaterialId));

	// 시험한 노드들의 Cost에 나오는 재료 ID마다 DB의 개수와 메모리의 개수를 비교한다
	static bool SameMaterials(CharacterProgressData loaded, PlayerProgress progress, params SkillTreeNode[] nodes)
	{
		foreach (string id in nodes.SelectMany(n => n.Cost).Select(c => c.MaterialId).Distinct())
		{
			loaded.Materials.TryGetValue(id, out int saved);
			if (saved != progress.GetMaterialCount(id)) return false;
		}
		return true;
	}

	void Check(string name, bool ok)
	{
		total++;
		if (ok)
		{
			passed++;
			Debug.Log($"[UnlockTest] [통과] {name}");
		}
		else
		{
			Debug.LogError($"[UnlockTest] [실패] {name}");
		}
	}
}
#endif
