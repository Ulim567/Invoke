using System.Collections.Generic;
using Mirror;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// (에디터 전용) 사냥터2·3 씬을 만들고 사냥터 사이 포탈을 놓는다 (#518, 노션 11/16 맵 확장, 캔버스 17-1~17-4·17-7). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → 맵 → 사냥터 2·3 만들기·포탈 놓기. 여러 번 눌러도 된다 (포탈·도착 자리는 지우고 다시 놓음). 끝나면 처음 열려 있던 씬으로 돌아온다.
/// <para>
/// 하는 일<br/>
/// 1. 씬 파일: HuntingGround2·3이 없으면 HuntingGround를 복사해 만든다 (배경, Grid, 몬스터 스포너가 같이 복사됨). 빌드 씬 목록(전역)에 없으면 HuntingGround 다음에 넣는다.<br/>
/// 2. 사냥터2·3: 로비로 가는 출구(PlaceExit)를 지우고(로비 문은 사냥터1에만), MapPainter로 땅을 칠하고 MapDecorator로 소품을 놓고, 시작 위치를 옮긴다.
///    배경 그림을 장소 그림으로 바꾼다 (사냥터2 맑은 낮 하늘, 사냥터3 보라 밤 + 불 켜진 창문).<br/>
/// 3. 포탈(PlacePortal)과 도착 자리(ArrivalPoint)를 씬 맨 위 Portals 아래에 다시 만든다. 이어지는 곳은 아래 표.<br/>
/// 4. 사냥터1: 로비에서 온 플레이어의 도착 자리(Arrival_Lobby, 시작 위치)도 둔다. 도착 자리가 하나라도 있는 씬은 맞는 것이 없으면 경고를 남기기 때문이다.<br/>
/// 5. 로비: 사냥터2·3에서 죽어 로비로 돌아온 플레이어도 사냥터 문 앞에 도착하게 Arrival_HuntingGround2·3을 Arrival_HuntingGround 자리에 둔다.<br/>
/// 6. 바뀐 씬을 저장한다.
/// </para>
/// <para>
/// 포탈 연결 (도착 자리 Id)<br/>
/// - 사냥터1 맨 위 발판(X -12, Y 5) ↔ 사냥터2 바닥 가운데(X 0, Y -4): HG1_Portal ↔ HG2_Bottom<br/>
/// - 사냥터2 맨 위 왼쪽 발판(X -24, Y 17) ↔ 사냥터3 왼쪽 끝(X -56, Y -4): HG2_Left ↔ HG3_Left<br/>
/// - 사냥터2 맨 위 오른쪽 발판(X 24, Y 17) ↔ 사냥터3 오른쪽 끝(X 55, Y -4): HG2_Right ↔ HG3_Right
/// </para>
/// <para>
/// 몬스터 스포너(김도훈)는 사냥터1 것이 그대로 복사되므로 자리는 김도훈이 맞춘다. 이 도구는 스포너를 건드리지 않는다.
/// </para>
/// </summary>
public static class HuntingGroundBuilder
{
	const string ScenesDir = "Assets/Scenes";
	const string PortalDir = "Assets/_HW/Map/Portal";
	const float PixelsPerUnit = 16f;
	const int PortalKeySize = 13; // ↑ 키 13픽셀 (문은 9). 9의 2배(18)는 너무 컸음
	const string PortalsRoot = "Portals";

	struct PortalSpec
	{
		public string name; public float x, surface; public string destination, arrival;
		public string hereId, hereFrom; // 이 포탈 앞 도착 자리 Id, 떠나온 장소(이름으로 못 찾을 때)
	}

	static PortalSpec Portal(string name, float x, float surface, string destination, string arrival, string hereId, string hereFrom = "")
		=> new PortalSpec { name = name, x = x, surface = surface, destination = destination, arrival = arrival, hereId = hereId, hereFrom = hereFrom };

	static readonly Dictionary<string, PortalSpec[]> Portals = new Dictionary<string, PortalSpec[]>
	{
		{ PlaceScenes.HuntingGround, new[]
		{
			Portal("Portal_HuntingGround2", -12f, 5f, PlaceScenes.HuntingGround2, "HG2_Bottom", "HG1_Portal", PlaceScenes.HuntingGround2),
		} },
		{ PlaceScenes.HuntingGround2, new[]
		{
			Portal("Portal_HuntingGround1", 0f, -4f, PlaceScenes.HuntingGround, "HG1_Portal", "HG2_Bottom", PlaceScenes.HuntingGround),
			Portal("Portal_HuntingGround3_Left", -24f, 17f, PlaceScenes.HuntingGround3, "HG3_Left", "HG2_Left", PlaceScenes.HuntingGround3),
			Portal("Portal_HuntingGround3_Right", 24f, 17f, PlaceScenes.HuntingGround3, "HG3_Right", "HG2_Right"),
		} },
		{ PlaceScenes.HuntingGround3, new[]
		{
			Portal("Portal_HuntingGround2_Left", -56f, -4f, PlaceScenes.HuntingGround2, "HG2_Left", "HG3_Left", PlaceScenes.HuntingGround2),
			Portal("Portal_HuntingGround2_Right", 55f, -4f, PlaceScenes.HuntingGround2, "HG2_Right", "HG3_Right"),
		} },
	};

	// 새 사냥터의 시작 위치 (도착 자리가 맞지 않을 때만 씀)
	static readonly Dictionary<string, Vector2> StartPositions = new Dictionary<string, Vector2>
	{
		{ PlaceScenes.HuntingGround2, new Vector2(-4f, -3.5f) },
		{ PlaceScenes.HuntingGround3, new Vector2(-52f, -3.5f) },
	};

	// 포탈 그림의 Sorting Layer·순서·오브젝트 레이어는 사냥터1 출구(PlaceExit)와 같게 한다
	static int sortingLayerId, sortingOrder, objectLayer;

	[MenuItem("Invoke/맵/사냥터 2·3 만들기·포탈 놓기")]
	static void Build()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		string original = EditorSceneManager.GetActiveScene().path;

		if (LoadFrames("portal").Length == 0)
		{
			EditorUtility.DisplayDialog("사냥터 만들기", $"{PortalDir}에 portal_0~3.png가 없어요.", "확인");
			return;
		}

		string source = ScenePath(PlaceScenes.HuntingGround);
		foreach (string place in new[] { PlaceScenes.HuntingGround2, PlaceScenes.HuntingGround3 })
		{
			string path = ScenePath(place);
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
			{
				if (!AssetDatabase.CopyAsset(source, path))
				{
					Debug.LogError($"[HuntingGroundBuilder] {source}를 {path}로 복사하지 못함");
					return;
				}
				Debug.Log($"[HuntingGroundBuilder] {path}를 사냥터1을 복사해 만듦");
			}
			AddToBuildList(path, source);
		}

		// 사냥터1: 출구 그림 설정을 읽고, 포탈·도착 자리를 놓는다
		Scene scene = EditorSceneManager.OpenScene(source, OpenSceneMode.Single);
		ReadExitStyle();
		RebuildPortals(scene, PlaceScenes.HuntingGround);
		AddLobbyArrivalAtStart(scene);
		EditorSceneManager.SaveScene(scene);

		foreach (string place in new[] { PlaceScenes.HuntingGround2, PlaceScenes.HuntingGround3 })
		{
			scene = EditorSceneManager.OpenScene(ScenePath(place), OpenSceneMode.Single);
			RemoveExits();
			string error = MapPainter.PaintActiveScene();
			if (error != null) Debug.LogError($"[HuntingGroundBuilder] {place}: {error}");
			MapDecorator.DecorateActiveScene();
			UseSolidGround();
			MapPainter.RebuildColliders(scene);
			ApplyTheme(place);
			MoveStart(StartPositions[place]);
			RebuildPortals(scene, place);
			EditorSceneManager.SaveScene(scene);
		}

		// 로비: 사냥터2·3에서 (죽어서) 돌아와도 사냥터 문 앞에 도착
		scene = EditorSceneManager.OpenScene(ScenePath(PlaceScenes.Lobby), OpenSceneMode.Single);
		if (AddLobbyArrivals()) EditorSceneManager.SaveScene(scene);

		if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
		Debug.Log("[HuntingGroundBuilder] 사냥터2·3과 포탈을 만들고 씬을 저장함. Base 씬에서 플레이해 포탈을 확인한다");
	}

	static string ScenePath(string place) => $"{ScenesDir}/{place}.unity";

	// 빌드 씬 목록(전역, Build Profiles가 따로 정하지 않음)에 없으면 기준 씬 다음에 넣는다
	static void AddToBuildList(string path, string after)
	{
		var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		if (scenes.Exists(s => s.path == path)) return;
		int index = scenes.FindIndex(s => s.path == after);
		// 사냥터2 다음에 사냥터3이 오게, 이미 넣은 새 사냥터 뒤로
		while (index >= 0 && index + 1 < scenes.Count && scenes[index + 1].path.StartsWith($"{ScenesDir}/{PlaceScenes.HuntingGround}")) index++;
		scenes.Insert(index >= 0 ? index + 1 : scenes.Count, new EditorBuildSettingsScene(path, true));
		EditorBuildSettings.scenes = scenes.ToArray();
		Debug.Log($"[HuntingGroundBuilder] 빌드 씬 목록에 {path}를 넣음");
	}

	static void ReadExitStyle()
	{
		sortingLayerId = SortingLayer.NameToID("Props");
		sortingOrder = 0;
		objectLayer = 0;
		foreach (PlaceExit exit in Object.FindObjectsByType<PlaceExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			objectLayer = exit.gameObject.layer;
			if (exit.TryGetComponent(out SpriteRenderer sr))
			{
				sortingLayerId = sr.sortingLayerID;
				sortingOrder = sr.sortingOrder;
			}
			return;
		}
	}

	// 사냥터1을 복사해 온 로비 출구를 지운다 (로비 문은 사냥터1에만, 캔버스 17-4)
	static void RemoveExits()
	{
		foreach (PlaceExit exit in Object.FindObjectsByType<PlaceExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(exit.gameObject) ?? exit.gameObject;
			Undo.DestroyObjectImmediate(root);
		}
	}

	// 벽에 대시해도 뚫리지 않게 땅 콜라이더 속을 채운다 (GroundColliderFixer와 같음). 복사한 씬은 이미 채워져 있지만 확인한다
	static void UseSolidGround()
	{
		foreach (CompositeCollider2D composite in Object.FindObjectsByType<CompositeCollider2D>(FindObjectsSortMode.None))
		{
			if (composite.name != "Ground" || composite.geometryType == CompositeCollider2D.GeometryType.Polygons) continue;
			Undo.RecordObject(composite, "Solid Ground");
			composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
		}
	}

	// 장소마다 분위기를 다르게 (#518, 임혜원 결정 「낮 하늘 탑 + 보라 밤」). 사냥터1을 복사해 온 배경 겹의 그림을 그 장소 그림으로 바꾼다
	// - Sky_HuntingGround_N → Sky_<장소>_N (Assets/_HW/Map/Sky), Ruins_HuntingGround_N → Ruins_<장소>_N (Assets/_HW/Map/Buildings). 겹 위치·Factor는 그대로
	// - 사냥터3(밤)은 땅 타일도 보랏빛으로 물들인다. 발판 그림은 MapPainter가 장소별로 칠한다 (사냥터2 구름, 사냥터3 네온)
	static readonly Dictionary<string, Color> GroundTint = new Dictionary<string, Color>
	{
		{ PlaceScenes.HuntingGround2, Color.white },
		{ PlaceScenes.HuntingGround3, new Color32(0xC4, 0xB8, 0xE8, 0xFF) },
	};

	// 장소에서 가장 낮은 바닥 윗면 (구덩이). 배경 폐허 그림 아래 끝을 이보다 반 칸 아래까지 내린다
	static readonly Dictionary<string, float> LowestFloor = new Dictionary<string, float>
	{
		{ PlaceScenes.HuntingGround3, -6f },
	};

	static void ApplyTheme(string place)
	{
		int swapped = 0;
		foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (sr.sprite == null) continue;
			string name = sr.sprite.name;
			string folder = name.StartsWith("Sky_HuntingGround_") ? "Sky" : name.StartsWith("Ruins_HuntingGround_") ? "Buildings" : null;
			if (folder == null) continue;
			string themed = name.Replace("_HuntingGround_", $"_{place}_");
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_HW/Map/{folder}/{themed}.png");
			if (sprite == null) { Debug.LogWarning($"[HuntingGroundBuilder] {themed}.png가 없어 {name} 그대로 둠"); continue; }
			Undo.RecordObject(sr, "Theme");
			sr.sprite = sprite;
			swapped++;
		}
		foreach (UnityEngine.Tilemaps.Tilemap map in Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None))
		{
			if (map.name != "Ground") continue; // 발판은 장소별 그림(구름, 네온)이 따로 있어서 물들이지 않는다
			Undo.RecordObject(map, "Theme");
			map.color = GroundTint[place];
		}
		// 사냥터3은 구덩이(바닥 Y -6)가 있어서, 폐허 그림 아래 끝(Max Bottom Y)이 -4.5에서 멈추면 높이 올라갔을 때 구덩이 안에 그 아래 하늘이 보여 건물이 떠 보였다
		// 가장 깊은 바닥보다 반 칸 아래까지 내려 둔다. 하늘처럼 이미 더 아래에 있는 겹은 그대로
		if (LowestFloor.TryGetValue(place, out float lowest))
		{
			foreach (Parallax layer in Object.FindObjectsByType<Parallax>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				var so = new SerializedObject(layer);
				SerializedProperty bottom = so.FindProperty("maxBottomY");
				if (bottom == null || bottom.floatValue <= lowest - 0.5f) continue;
				bottom.floatValue = lowest - 0.5f;
				so.ApplyModifiedProperties();
			}
		}
		Debug.Log($"[HuntingGroundBuilder] {place}: 배경 그림 {swapped}장을 바꿈");
	}

	static void MoveStart(Vector2 position)
	{
		NetworkStartPosition start = Object.FindFirstObjectByType<NetworkStartPosition>();
		if (start == null) return;
		Undo.RecordObject(start.transform, "Move Start");
		start.transform.position = new Vector3(position.x, position.y, start.transform.position.z);
	}

	// 씬 맨 위 Portals를 지우고 표대로 포탈과 도착 자리를 다시 만든다
	static void RebuildPortals(Scene scene, string place)
	{
		foreach (GameObject old in scene.GetRootGameObjects())
			if (old.name == PortalsRoot) Undo.DestroyObjectImmediate(old);

		var root = new GameObject(PortalsRoot);
		Undo.RegisterCreatedObjectUndo(root, "Build Portals");
		SceneManager.MoveGameObjectToScene(root, scene);

		foreach (PortalSpec spec in Portals[place])
		{
			// 그림 (#518): 받침은 지금 장소(사냥터1 돌, 사냥터2 구름, 사냥터3 네온 금속), 소용돌이 색은 가는 곳(사냥터1 민트, 사냥터2 하늘색, 사냥터3 보라)
			// 로비 문이 가는 곳 색을 쓰는 것과 같다. 그림이 없으면 예전 민트 포탈(portal_0~3)
			Sprite[] frames = LoadFrames($"portal_{place}_to_{spec.destination}");
			if (frames.Length == 0) frames = LoadFrames("portal");
			// 포탈: 그림 기준점이 아래 가운데라 위치 y = 서 있는 땅·발판 윗면
			var portal = new GameObject(spec.name) { layer = objectLayer };
			portal.transform.SetParent(root.transform, false);
			portal.transform.position = new Vector3(spec.x, spec.surface, 0f);
			var sr = portal.AddComponent<SpriteRenderer>();
			sr.sprite = frames[0];
			sr.sortingLayerID = sortingLayerId;
			sr.sortingOrder = sortingOrder;
			var box = portal.AddComponent<BoxCollider2D>();
			box.isTrigger = true;
			box.size = new Vector2(1.5f, 2.5f);
			box.offset = new Vector2(0f, 1.25f);
			var component = portal.AddComponent<PlacePortal>();
			var so = new SerializedObject(component);
			so.FindProperty("destination").stringValue = spec.destination;
			so.FindProperty("arrival").stringValue = spec.arrival;
			so.FindProperty("keyScale").intValue = 1;
			so.FindProperty("keySize").intValue = PortalKeySize;
			SerializedProperty list = so.FindProperty("frames");
			list.arraySize = frames.Length;
			for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
			so.ApplyModifiedPropertiesWithoutUndo();

			// 이 포탈 앞 도착 자리: 서버가 아래 땅을 찾아 발을 맞추므로 반 칸 위에 둔다
			var arrival = new GameObject($"Arrival_{spec.hereId}");
			arrival.transform.SetParent(root.transform, false);
			arrival.transform.position = new Vector3(spec.x, spec.surface + 0.5f, 0f);
			SetArrival(arrival.AddComponent<ArrivalPoint>(), spec.hereFrom, spec.hereId);
		}
	}

	static void SetArrival(ArrivalPoint point, string fromPlace, string id)
	{
		var so = new SerializedObject(point);
		so.FindProperty("fromPlace").stringValue = fromPlace;
		so.FindProperty("id").stringValue = id;
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	// 사냥터1: 로비에서 온 플레이어는 지금처럼 시작 위치에 도착 (도착 자리가 생겨서 맞는 것이 없으면 경고가 나므로)
	static void AddLobbyArrivalAtStart(Scene scene)
	{
		NetworkStartPosition start = Object.FindFirstObjectByType<NetworkStartPosition>();
		GameObject root = null;
		foreach (GameObject go in scene.GetRootGameObjects()) if (go.name == PortalsRoot) root = go;
		if (start == null || root == null) return;
		var arrival = new GameObject("Arrival_Lobby");
		arrival.transform.SetParent(root.transform, false);
		arrival.transform.position = start.transform.position;
		SetArrival(arrival.AddComponent<ArrivalPoint>(), PlaceScenes.Lobby, "");
	}

	// 로비: Arrival_HuntingGround 자리에 사냥터2·3에서 온 플레이어의 도착 자리를 둔다
	static bool AddLobbyArrivals()
	{
		ArrivalPoint hunting = null;
		var existing = new List<ArrivalPoint>();
		foreach (ArrivalPoint point in Object.FindObjectsByType<ArrivalPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
		{
			if (point.FromPlace == PlaceScenes.HuntingGround) hunting = point;
			if (point.FromPlace == PlaceScenes.HuntingGround2 || point.FromPlace == PlaceScenes.HuntingGround3) existing.Add(point);
		}
		if (hunting == null)
		{
			Debug.LogWarning("[HuntingGroundBuilder] 로비에 사냥터에서 온 도착 자리(Arrival_HuntingGround)가 없어 사냥터2·3 도착 자리를 놓지 않음");
			return false;
		}
		foreach (ArrivalPoint point in existing) Undo.DestroyObjectImmediate(point.gameObject);
		foreach (string place in new[] { PlaceScenes.HuntingGround2, PlaceScenes.HuntingGround3 })
		{
			var go = new GameObject($"Arrival_{place}");
			Undo.RegisterCreatedObjectUndo(go, "Lobby Arrivals");
			go.transform.SetParent(hunting.transform.parent, false);
			go.transform.position = hunting.transform.position;
			SetArrival(go.AddComponent<ArrivalPoint>(), place, "");
		}
		return true;
	}

	// 포탈 그림: 28×44픽셀 소용돌이 4장 + 받침 (이름_0~3.png. 가져오기: 16 PPU, Point, 압축 없음, 기준점 아래 가운데)
	static Sprite[] LoadFrames(string prefix)
	{
		var frames = new List<Sprite>();
		for (int i = 0; ; i++)
		{
			string path = $"{PortalDir}/{prefix}_{i}.png";
			if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) break;
			var settings = new TextureImporterSettings();
			importer.ReadTextureSettings(settings);
			bool dirty = importer.textureType != TextureImporterType.Sprite || importer.spritePixelsPerUnit != PixelsPerUnit
				|| importer.filterMode != FilterMode.Point || importer.textureCompression != TextureImporterCompression.Uncompressed
				|| settings.spriteAlignment != (int)SpriteAlignment.BottomCenter || importer.spriteImportMode != SpriteImportMode.Single;
			if (dirty)
			{
				importer.textureType = TextureImporterType.Sprite;
				importer.spriteImportMode = SpriteImportMode.Single;
				importer.ReadTextureSettings(settings);
				settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
				importer.SetTextureSettings(settings);
				importer.spritePixelsPerUnit = PixelsPerUnit;
				importer.filterMode = FilterMode.Point;
				importer.mipmapEnabled = false;
				importer.textureCompression = TextureImporterCompression.Uncompressed;
				importer.SaveAndReimport();
			}
			Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (sprite != null) frames.Add(sprite);
		}
		return frames.ToArray();
	}
}
