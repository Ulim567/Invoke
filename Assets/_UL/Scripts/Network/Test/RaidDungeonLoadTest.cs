using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 레이드 던전을 여러 번 불러오고 내리는지 확인하는 테스트 컴포넌트.
/// <para>
/// 사용법<br/>
/// - Base 씬의 아무 오브젝트에 붙인다.<br/>
/// - Play → Server Only로 시작한 뒤, 이 컴포넌트의 Inspector ⋮ 메뉴에서 항목을 고른다.
/// </para>
/// </summary>
public class RaidDungeonLoadTest : MonoBehaviour
{
	// 이 테스트로 불러온 레이드 던전. 나중에 불러온 것부터 내린다
	readonly List<Scene> loaded = new List<Scene>();

	[ContextMenu("레이드 던전 불러오기")]
	void Load()
	{
		InvokeNetworkManager manager = FindManager();
		if (manager == null) return;

		manager.LoadRaidDungeon(scene => loaded.Add(scene));
	}

	[ContextMenu("마지막 레이드 던전 내리기")]
	void UnloadLast()
	{
		InvokeNetworkManager manager = FindManager();
		if (manager == null) return;

		if (loaded.Count == 0)
		{
			Debug.LogWarning("[RaidDungeonLoadTest] 이 테스트로 불러온 레이드 던전이 없습니다.");
			return;
		}

		Scene scene = loaded[loaded.Count - 1];
		loaded.RemoveAt(loaded.Count - 1);
		manager.UnloadRaidDungeon(scene);
	}

	static InvokeNetworkManager FindManager()
	{
		var manager = NetworkManager.singleton as InvokeNetworkManager;
		if (manager == null) Debug.LogWarning("[RaidDungeonLoadTest] InvokeNetworkManager가 없습니다. Base 씬에서 Play 중인지 확인하세요.");
		return manager;
	}
}
