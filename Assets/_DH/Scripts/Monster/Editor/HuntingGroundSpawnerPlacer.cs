using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 사냥터 2·3 씬을 차례로 열어 배치표대로 몬스터 스포너 프리팹을 놓고 저장하는 에디터 도구.
/// 메뉴: Invoke → 맵 → 사냥터 2·3 몬스터 스포너 놓기. 여러 번 눌러도 기존 스포너를 지우고 다시 놓으므로 결과가 같다.
/// </summary>
public static class HuntingGroundSpawnerPlacer
{
	const string LeakPath = "Assets/_DH/Prefabs/LeakSpawner.prefab";
	const string LoopPath = "Assets/_DH/Prefabs/LoopSpawner.prefab";
	// 스포너들을 모아 둘 부모 오브젝트 이름
	const string ParentName = "Spawners";

	// 배치표 한 줄: 이름, 프리팹 경로, 초록 상자 중심 위치, 구역 크기(areaSize), 최대 마리 수(maxCount)
	readonly struct Entry
	{
		public readonly string Name, Prefab;
		public readonly Vector2 Pos, Size;
		public readonly int Max;
		public Entry(string name, string prefab, float x, float y, float w, float h, int max)
		{ Name = name; Prefab = prefab; Pos = new Vector2(x, y); Size = new Vector2(w, h); Max = max; }
	}

	// 씬 이름별 배치표
	static readonly (string scene, Entry[] entries)[] Layouts =
	{
		("HuntingGround2", new[]
		{
			new Entry("Leak_Ground_W", LeakPath, -17f, -4f, 22f, 2f, 2),
			new Entry("Leak_Ground_E", LeakPath, 17f, -4f, 22f, 2f, 2),
			new Entry("Leak_Tier12", LeakPath, 0f, 0.5f, 56f, 4f, 3),
			new Entry("Leak_Tier34", LeakPath, 0f, 6.5f, 56f, 4f, 2),
			new Entry("Loop_Tier34", LoopPath, 0f, 6.5f, 56f, 4f, 1),
			new Entry("Loop_Tier56", LoopPath, 0f, 12.5f, 56f, 6f, 3),
		}),
		("HuntingGround3", new[]
		{
			new Entry("Leak_Gnd_1", LeakPath, -42.5f, -4f, 15f, 2f, 2),
			new Entry("Leak_Gnd_2", LeakPath, -18f, -4f, 16f, 2f, 2),
			new Entry("Leak_Gnd_3", LeakPath, 7.5f, -4f, 19f, 2f, 2),
			new Entry("Leak_Gnd_4", LeakPath, 34.5f, -4f, 17f, 2f, 2),
			new Entry("Leak_Tier2", LeakPath, -8.5f, 2f, 65f, 2f, 3),
			new Entry("Loop_Tier1", LoopPath, -3.5f, -1f, 91f, 2f, 2),
			new Entry("Loop_Tier3", LoopPath, -0.5f, 5f, 65f, 2f, 3),
		}),
	};

	[MenuItem("Invoke/맵/사냥터 2·3 몬스터 스포너 놓기")]
	public static void PlaceAll()
	{
		// 지금 씬에 저장 안 한 변경이 있으면 저장할지 묻고, 취소하면 멈춘다
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

		foreach (var (sceneName, entries) in Layouts)
		{
			string path = FindScenePath(sceneName);
			if (path == null) { Debug.LogError($"[SpawnerPlacer] 씬 '{sceneName}'을 찾지 못했습니다."); continue; }
			Place(EditorSceneManager.OpenScene(path, OpenSceneMode.Single), entries);
		}
	}

	// 이름이 정확히 같은 씬 파일의 경로를 찾는다 (HuntingGround와 헷갈리지 않게)
	static string FindScenePath(string sceneName)
	{
		foreach (string guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
		{
			string p = AssetDatabase.GUIDToAssetPath(guid);
			if (System.IO.Path.GetFileNameWithoutExtension(p) == sceneName) return p;
		}
		return null;
	}

	// 한 씬에서 기존 스포너를 지우고 배치표대로 놓은 뒤 저장한다
	static void Place(Scene scene, Entry[] entries)
	{
		// 기존 스포너 삭제: MonsterSpawner가 붙은 오브젝트의 최상위 프리팹 인스턴스 루트(없으면 그 오브젝트)를 지운다
		int removed = 0;
		foreach (GameObject root in scene.GetRootGameObjects())
			foreach (MonsterSpawner s in root.GetComponentsInChildren<MonsterSpawner>(true))
			{
				if (s == null) continue; // 같은 루트를 이미 지웠으면 건너뛴다
				GameObject target = PrefabUtility.GetOutermostPrefabInstanceRoot(s.gameObject);
				Object.DestroyImmediate(target != null ? target : s.gameObject);
				removed++;
			}
		Debug.Log($"[SpawnerPlacer] {scene.name}: 기존 스포너 {removed}개 삭제");

		// 부모 "Spawners"를 찾거나 만들어 씬 맨 위에 둔다
		GameObject parent = System.Array.Find(scene.GetRootGameObjects(), g => g.name == ParentName);
		if (parent == null)
		{
			parent = new GameObject(ParentName);
			SceneManager.MoveGameObjectToScene(parent, scene);
		}
		parent.transform.SetSiblingIndex(0);

		int total = 0;
		foreach (Entry e in entries)
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(e.Prefab);
			var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
			go.name = e.Name;
			var spawner = go.GetComponentInChildren<MonsterSpawner>(true);

			// 스포너 오브젝트가 표의 위치에 오도록 루트를 차이만큼 옮긴다
			Vector3 targetPos = new Vector3(e.Pos.x, e.Pos.y, 0f);
			go.transform.position += targetPos - spawner.transform.position;

			// areaSize와 maxCount만 바꾸고 나머지는 프리팹 값 그대로 둔다
			var so = new SerializedObject(spawner);
			so.FindProperty("areaSize").vector2Value = e.Size;
			so.FindProperty("maxCount").intValue = e.Max;
			so.ApplyModifiedPropertiesWithoutUndo();

			// 월드 위치를 유지한 채 Spawners 아래로 넣는다
			go.transform.SetParent(parent.transform, true);
			total += e.Max;
		}

		EditorSceneManager.MarkSceneDirty(scene);
		EditorSceneManager.SaveScene(scene);
		Debug.Log($"[SpawnerPlacer] {scene.name}: 스포너 {entries.Length}개, 최대 마릿수 합계 {total}");
	}
}
