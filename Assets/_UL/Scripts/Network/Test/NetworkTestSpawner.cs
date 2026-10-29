using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 서버에서 로비나 사냥터에 테스트 상자를 만드는 테스트 컴포넌트.
/// 다른 씬의 네트워크 오브젝트가 클라이언트에 보이지 않는지, 씬마다 물리가 따로 동작하는지 확인할 때 쓴다.
/// <para>
/// 사용법<br/>
/// - Base 씬의 아무 오브젝트에 붙이고 Box Prefab에 테스트 상자 프리팹을 넣는다.<br/>
/// - Play → Server Only로 시작하고 장소 씬 로그가 찍힌 뒤, 이 컴포넌트의 Inspector ⋮ 메뉴에서 항목을 고른다.
/// </para>
/// </summary>
public class NetworkTestSpawner : MonoBehaviour
{
	[SerializeField, Tooltip("테스트 상자 프리팹. NetworkIdentity가 붙어 있고 Registered Spawnable Prefabs에 등록되어 있어야 한다")]
	GameObject boxPrefab;

	[SerializeField, Tooltip("씬의 시작 위치에서 얼마나 위에 만들지(유닛). 바닥으로 떨어지는 것을 보기 위함")]
	float spawnHeight = 3f;

	// 이 테스트로 만든 상자. 한 번에 모두 없애기 위해 보관한다
	readonly List<GameObject> spawned = new List<GameObject>();

	[ContextMenu("로비에 테스트 상자 만들기")]
	void SpawnInLobby() => SpawnIn(PlaceScenes.Lobby);

	[ContextMenu("사냥터에 테스트 상자 만들기")]
	void SpawnInHuntingGround() => SpawnIn(PlaceScenes.HuntingGround);

	[ContextMenu("테스트 상자 모두 없애기")]
	void DespawnAll()
	{
		foreach (GameObject box in spawned)
			NetworkSpawner.Despawn(box);
		spawned.Clear();
	}

	void SpawnIn(string sceneName)
	{
		// 로비·사냥터는 서버에 한 번씩만 불러오므로 이름으로 찾아도 하나로 정해진다
		Scene scene = SceneManager.GetSceneByName(sceneName);
		if (!scene.isLoaded)
		{
			Debug.LogWarning($"[NetworkTestSpawner] {sceneName} 씬을 아직 불러오지 않았습니다. Server Only로 시작하고 장소 씬 로그가 찍힌 뒤에 고르세요.");
			return;
		}

		Vector3 position = FindStartPosition(scene) + Vector3.up * spawnHeight;
		GameObject box = NetworkSpawner.Spawn(boxPrefab, position, Quaternion.identity, scene);
		if (box == null) return;

		// 충돌 로그와 Hierarchy에서 어느 씬의 상자인지 구분하기 위해 이름에 씬 이름을 붙인다
		box.name = $"TestBox [{sceneName}]";
		spawned.Add(box);
	}

	static Vector3 FindStartPosition(Scene scene)
	{
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			NetworkStartPosition start = root.GetComponentInChildren<NetworkStartPosition>();
			if (start != null) return start.transform.position;
		}
		return Vector3.zero;
	}
}
