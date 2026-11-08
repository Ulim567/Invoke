using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 로비 입구와 각 장소 출구의 그림을 문(게이트) 그림으로 바꾸는 에디터 도구 (#455). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → 맵 → 입구·출구 문 그림 바꾸기. 장소 씬 네 개(로비, 사냥터, 연습장, 레이드 던전)를 차례로 열어 바꾸고 저장한 뒤, 처음 열려 있던 씬으로 돌아온다. 여러 번 눌러도 된다.
/// <para>
/// - 그림: Assets/_HW/Lobby/Gates/gate_{hunting, training, raid}.png (28×48픽셀 = 1.75×3칸). 남색 금속 문틀 + 이끼 덩굴 + 위의 작은 모니터, 안쪽은 가는 곳 분위기로 빛나는 화면 (노션 컨셉).
///   사냥터 청록 스캔 줄, 연습장 하늘색 격자, 레이드 마젠타 글리치 + 크리스털. 가져오기 설정(16 PPU, Point, 압축 없음, 기준점 아래 가운데)도 여기서 맞춘다.<br/>
/// - 로비 입구(LobbyEntrance)는 Destination에 맞는 그림, 출구(이우림의 PlaceExit)는 그 장소로 들어온 로비 입구와 같은 그림 (같은 문의 양쪽). 지금 그림의 아래 끝(땅)에 문 아래 끝을 맞춘다.
///   색은 흰색(그림 그대로), 범위(콜라이더)와 ↑ 안내 자리는 문 크기에 맞춘다. 프리팹(Entrance, PlaceExit)은 그대로 두고 씬에 놓인 것만 바꾼다.
/// </para>
/// </summary>
public static class EntranceGateBuilder
{
	const string Dir = "Assets/_HW/Lobby/Gates";
	const float PixelsPerUnit = 16f;
	const float GateHeight = 48f / PixelsPerUnit; // 3칸

	static readonly string[] Scenes = { "Lobby", "HuntingGround", "TrainingRoom", "RaidDungeon" };

	[MenuItem("Invoke/맵/입구·출구 문 그림 바꾸기")]
	static void Build()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		string original = EditorSceneManager.GetActiveScene().path;

		int changed = 0;
		foreach (string name in Scenes)
		{
			string path = $"Assets/Scenes/{name}.unity";
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			int inScene = 0;

			// 로비 입구: 가는 곳 색
			foreach (LobbyEntrance entrance in Object.FindObjectsByType<LobbyEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				var so = new SerializedObject(entrance);
				var destination = (LobbyEntrance.Destination)so.FindProperty("destination").enumValueIndex;
				string gate = destination switch
				{
					LobbyEntrance.Destination.TrainingRoom => "gate_training",
					LobbyEntrance.Destination.RaidDungeon => "gate_raid",
					_ => "gate_hunting",
				};
				if (Apply(entrance.transform, LoadGate(gate))) inScene++;
			}
			// 각 장소의 출구(이우림의 PlaceExit, 로비로 돌아감): 로비에서 이 장소로 들어온 문과 같은 색 (같은 문의 양쪽)
			string exitGate = name switch
			{
				"TrainingRoom" => "gate_training",
				"RaidDungeon" => "gate_raid",
				_ => "gate_hunting",
			};
			foreach (PlaceExit exit in Object.FindObjectsByType<PlaceExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
				if (Apply(exit.transform, LoadGate(exitGate))) inScene++;

			if (inScene > 0) EditorSceneManager.SaveScene(scene);
			changed += inScene;
			Debug.Log($"[EntranceGateBuilder] {name}: 문 {inScene}개");
		}

		if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
		Debug.Log($"[EntranceGateBuilder] 입구·출구 {changed}개를 문 그림으로 바꾸고 씬을 저장함");
	}

	// 그림을 바꾸고, 지금 그림의 아래 끝(땅 위)에 문 아래 끝을 맞춘다. 문 그림의 기준점이 아래 가운데라 위치 y = 아래 끝
	static bool Apply(Transform target, Sprite sprite)
	{
		if (sprite == null || !target.TryGetComponent(out SpriteRenderer renderer)) return false;
		float bottom = renderer.sprite != null ? renderer.bounds.min.y : target.position.y;
		Undo.RecordObject(target, "문 그림");
		Undo.RecordObject(renderer, "문 그림");
		Vector3 position = target.position;
		position.y = Mathf.Round(bottom * PixelsPerUnit) / PixelsPerUnit;
		target.position = position;
		renderer.sprite = sprite;
		renderer.color = Color.white;
		renderer.drawMode = SpriteDrawMode.Simple;
		PrefabUtility.RecordPrefabInstancePropertyModifications(target);
		PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);

		// 범위: 문 크기 (문 앞에 서면 들어감)
		if (target.TryGetComponent(out BoxCollider2D box))
		{
			Undo.RecordObject(box, "문 그림");
			box.size = new Vector2(1.5f, GateHeight - 0.25f);
			box.offset = new Vector2(0f, (GateHeight - 0.25f) * 0.5f);
			PrefabUtility.RecordPrefabInstancePropertyModifications(box);
		}

		// ↑ 안내(와 #434 마름모) 자리: 문 위 끝에서 반 칸 위
		Transform prompt = target.Find("Prompt");
		if (prompt != null)
		{
			Undo.RecordObject(prompt, "문 그림");
			prompt.localPosition = new Vector3(0f, GateHeight + 0.5f, 0f);
			PrefabUtility.RecordPrefabInstancePropertyModifications(prompt);
		}
		return true;
	}

	// 가져오기 설정: 16 PPU, 픽셀 그대로(Point, 압축 없음), 기준점 아래 가운데
	static Sprite LoadGate(string name)
	{
		string path = $"{Dir}/{name}.png";
		if (AssetImporter.GetAtPath(path) is TextureImporter importer)
		{
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
		}
		Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (sprite == null) Debug.LogWarning($"[EntranceGateBuilder] {path}를 찾지 못함");
		return sprite;
	}
}
