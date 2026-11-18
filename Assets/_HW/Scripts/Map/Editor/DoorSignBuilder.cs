using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 로비 문 위치와 모든 문 위의 행선지 글씨를 노션 11/16 「맵 확장」대로 맞추는 에디터 도구. 게임에는 들어가지 않는다.
/// 메뉴 Invoke → 맵 → 문 위치·행선지 표시 맞추기. 장소 씬 네 개를 차례로 열어 바꾸고 저장한 뒤, 처음 열려 있던 씬으로 돌아온다. 여러 번 눌러도 된다.
/// <para>
/// - 로비 문 세 개(사냥터, 연습장, 레이드)를 로비 중간 바닥(윗면 Y -4)으로 옮긴다 (X <see cref="LobbyDoorX"/>). 로비로 돌아올 때 서는 자리(Arrival_장소)도 각 문 앞으로 같이 옮긴다.<br/>
///   예전 자리(오른쪽 높은 단 X 21, 25, 29)는 비게 된다. X -2 근처 낮은 발판(X 3~7)과 겹치지 않게 골랐다.<br/>
/// - 모든 문(로비 입구 LobbyEntrance, 각 장소의 출구 PlaceExit) 위에 행선지 안내판(Sign)을 만든다. 입구는 「가는 곳으로 이동」, 출구는 「로비로 이동」.
///   모양은 다른 게임의 상호작용 안내처럼 한 줄 안내판 「[↑] 사냥터로 이동」 (어두운 보라 바탕 + 테두리, ↑ 키 그림, 흰 글씨 Galmuri11).
///   캐릭터 머리와 이름표에 가리지 않게 문 위 끝에서 1.8칸 위. 가까이 갔을 때만 올라오며 나타난다 (PlaceInteractable).<br/>
/// - 문 그림은 메뉴 Invoke → 맵 → 입구·출구 문 그림 바꾸기(EntranceGateBuilder)가 바꾼다. 출구는 로비 귀환 문 그림(gate_lobby).
/// </para>
/// </summary>
public static class DoorSignBuilder
{
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string PillSpritePath = "Assets/_HW/UI/Sign/sign_pill.png";
	const string KeySpritePath = "Assets/_HW/UI/Sign/sign_key.png";
	const float FloorTop = -4f;
	const float GateHeight = 3f;       // 문 그림 높이 (48픽셀)
	const float SignAboveGate = 1.8f;  // 문 위 끝에서 안내판까지. 캐릭터(3.75칸)와 머리 위 이름표보다 위
	const float SignFontSize = 4.4f;   // Galmuri11이 11픽셀쯤으로 보이는 크기

	// 로비 중간 바닥에 놓을 문 X (사냥터, 연습장, 레이드 순서)
	static readonly float[] LobbyDoorX = { -6f, -2f, 2f };

	static readonly string[] Scenes = { "Lobby", "HuntingGround", "TrainingRoom", "RaidDungeon" };

	[MenuItem("Invoke/맵/문 위치·행선지 표시 맞추기")]
	static void Build()
	{
		if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
		string original = EditorSceneManager.GetActiveScene().path;
		TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		Sprite pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
		Sprite keyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(KeySpritePath);
		if (font == null) Debug.LogWarning($"[DoorSignBuilder] {FontPath}를 찾지 못함");
		if (pill == null || keyIcon == null) Debug.LogWarning("[DoorSignBuilder] Assets/_HW/UI/Sign 그림을 찾지 못함");

		foreach (string name in Scenes)
		{
			string path = $"Assets/Scenes/{name}.unity";
			if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
			var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
			int signs = 0;

			foreach (LobbyEntrance entrance in Object.FindObjectsByType<LobbyEntrance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				var so = new SerializedObject(entrance);
				var destination = (LobbyEntrance.Destination)so.FindProperty("destination").enumValueIndex;
				if (name == "Lobby") MoveLobbyDoor(entrance.transform, destination);
				MakeSign(entrance, MoveText(entrance.DisplayName), font, pill, keyIcon);
				signs++;
			}
			foreach (PlaceExit exit in Object.FindObjectsByType<PlaceExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				MakeSign(exit, MoveText("로비"), font, pill, keyIcon);
				signs++;
			}

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Debug.Log($"[DoorSignBuilder] {name}: 행선지 글씨 {signs}개");
		}

		if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
		Debug.Log("[DoorSignBuilder] 문 위치·행선지 글씨를 맞추고 씬을 저장함");
	}

	// 로비 문을 중간 바닥으로. 문 그림은 기준점이 아래 가운데라 y = 바닥 윗면
	static void MoveLobbyDoor(Transform door, LobbyEntrance.Destination destination)
	{
		int index = (int)destination;
		if (index < 0 || index >= LobbyDoorX.Length) return;
		Undo.RecordObject(door, "로비 문 옮기기");
		door.position = new Vector3(LobbyDoorX[index], FloorTop, door.position.z);
		PrefabUtility.RecordPrefabInstancePropertyModifications(door);

		// 로비로 돌아올 때 서는 자리 (이우림의 ArrivalPoint, 이름 Arrival_장소씬)
		string arrivalName = "Arrival_" + destination switch
		{
			LobbyEntrance.Destination.TrainingRoom => PlaceScenes.TrainingRoom,
			LobbyEntrance.Destination.RaidDungeon => PlaceScenes.RaidDungeon,
			_ => PlaceScenes.HuntingGround,
		};
		GameObject arrival = GameObject.Find(arrivalName);
		if (arrival == null)
		{
			Debug.LogWarning($"[DoorSignBuilder] {arrivalName}을 찾지 못함 (돌아올 자리는 그대로)");
			return;
		}
		Undo.RecordObject(arrival.transform, "로비 문 옮기기");
		arrival.transform.position = new Vector3(LobbyDoorX[index], FloorTop + 0.5f, arrival.transform.position.z);
		PrefabUtility.RecordPrefabInstancePropertyModifications(arrival.transform);
	}

	/// <summary>
	/// 문이 아닌 상호작용 자리에도 같은 모양의 「[↑] 글씨」 안내판을 붙인다 (#534, 로비 상점 자리). 씬 저장은 부르는 쪽이 한다.
	/// </summary>
	internal static void MakeSignFor(PlaceInteractable place, string text)
	{
		TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		Sprite pill = AssetDatabase.LoadAssetAtPath<Sprite>(PillSpritePath);
		Sprite keyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(KeySpritePath);
		if (font == null || pill == null || keyIcon == null) Debug.LogWarning("[DoorSignBuilder] 안내판 글꼴·그림을 찾지 못함");
		MakeSign(place, text, font, pill, keyIcon);
	}

	// 문 아래에 한 줄 안내판 Sign(바탕 + ↑ 키 + 글씨)을 만들거나 고치고 PlaceInteractable.sign에 넣는다
	// 모양: 다른 게임의 상호작용 안내처럼 「[↑] 사냥터로 이동」 한 줄. 높이 13픽셀, 캐릭터 머리·이름표보다 위(문 위 끝 + 1.8칸)
	static void MakeSign(PlaceInteractable door, string text, TMP_FontAsset font, Sprite pillSprite, Sprite keySprite)
	{
		const float Px = 1f / 16f;
		Transform signTransform = Child(door.transform, "Sign");
		GameObject sign = signTransform.gameObject;
		// 꺼진 오브젝트의 글씨는 그려지지 않아 크기를 잴 수 없으므로, 만드는 동안 켜 둔다 (끝에서 다시 끔)
		sign.SetActive(true);
		Vector3 lossy = door.transform.lossyScale;
		signTransform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 1f);
		signTransform.localPosition = new Vector3(0f, (GateHeight + SignAboveGate) / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)), 0f);
		// 예전에 만든 바탕 오브젝트 정리 (이름이 바뀜)
		Transform oldBack = signTransform.Find("Back");
		if (oldBack != null) Undo.DestroyObjectImmediate(oldBack.gameObject);

		// 그리는 층: 문과 같은 층, 문보다 위
		int layerId = 0, order = 20;
		if (door.TryGetComponent(out SpriteRenderer doorRenderer))
		{
			layerId = doorRenderer.sortingLayerID;
			order = doorRenderer.sortingOrder + 20;
		}

		// 글씨 (너비를 먼저 재서 안내판 크기를 정한다)
		Transform textTransform = Child(signTransform, "Text");
		if (!textTransform.TryGetComponent(out TextMeshPro label)) label = Undo.AddComponent<TextMeshPro>(textTransform.gameObject);
		if (font != null) label.font = font;
		label.text = text;
		label.fontSize = SignFontSize;
		label.color = Color.white;
		label.alignment = TextAlignmentOptions.Center;
		label.textWrappingMode = TextWrappingModes.NoWrap;
		label.margin = Vector4.zero;
		label.sortingLayerID = layerId;
		label.sortingOrder = order + 2;
		label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
		label.rectTransform.sizeDelta = new Vector2(10f, 2f);
		label.rectTransform.localPosition = Vector3.zero;
		label.ForceMeshUpdate(true, true);
		// 실제로 그려진 글자 범위로 잰다 (글꼴의 줄 높이·여백 때문에 GetPreferredValues는 실제 글자보다 넓고 아래로 치우침)
		Bounds glyphs = label.textBounds;
		if (glyphs.size.x <= 0f)
		{
			// 그래도 못 쟀으면 글꼴 기준 너비로 (조금 넓을 수 있음)
			Vector2 preferred = label.GetPreferredValues(text);
			glyphs = new Bounds(Vector3.zero, new Vector3(preferred.x, preferred.y, 0f));
			Debug.LogWarning($"[DoorSignBuilder] {door.name}: 글자 범위를 못 재서 글꼴 기준 너비를 씀");
		}
		float textWidth = Mathf.Ceil(glyphs.size.x / Px) * Px;

		// 안내판 크기: 왼쪽 여백 2 + 키 9 + 사이 3 + 글씨 + 오른쪽 여백 4 (픽셀), 높이 13
		float width = 2 * Px + 9 * Px + 3 * Px + textWidth + 4 * Px;
		float height = 13 * Px;
		float left = -Mathf.Round(width / 2f / Px) * Px;

		Transform pill = Child(signTransform, "Pill");
		if (!pill.TryGetComponent(out SpriteRenderer pillRenderer)) pillRenderer = Undo.AddComponent<SpriteRenderer>(pill.gameObject);
		pillRenderer.sprite = pillSprite;
		pillRenderer.drawMode = SpriteDrawMode.Sliced;
		pillRenderer.size = new Vector2(width, height);
		pillRenderer.color = Color.white;
		pillRenderer.sortingLayerID = layerId;
		pillRenderer.sortingOrder = order;
		pill.localPosition = new Vector3(left + width / 2f, height / 2f, 0f);

		Transform key = Child(signTransform, "Key");
		if (!key.TryGetComponent(out SpriteRenderer keyRenderer)) keyRenderer = Undo.AddComponent<SpriteRenderer>(key.gameObject);
		keyRenderer.sprite = keySprite;
		keyRenderer.color = Color.white;
		keyRenderer.sortingLayerID = layerId;
		keyRenderer.sortingOrder = order + 1;
		key.localPosition = new Vector3(left + 2 * Px + 4.5f * Px, height / 2f, 0f);

		// 글자 왼쪽 끝을 키 오른쪽 3픽셀 뒤에, 글자 세로 가운데를 안내판 가운데에 맞춘다 (픽셀 격자에 맞춰 반올림)
		Vector3 textPosition = new Vector3(left + 14 * Px - glyphs.min.x, height / 2f - glyphs.center.y, 0f);
		textPosition.x = Mathf.Round(textPosition.x / Px) * Px;
		textPosition.y = Mathf.Round(textPosition.y / Px) * Px;
		label.rectTransform.localPosition = textPosition;

		var so = new SerializedObject(door);
		so.FindProperty("sign").objectReferenceValue = sign;
		so.FindProperty("keyScale").intValue = 1;
		so.ApplyModifiedPropertiesWithoutUndo();
		sign.SetActive(false);
		PrefabUtility.RecordPrefabInstancePropertyModifications(door);
	}

	static Transform Child(Transform parent, string name)
	{
		Transform child = parent.Find(name);
		if (child != null) return child;
		var go = new GameObject(name);
		Undo.RegisterCreatedObjectUndo(go, "행선지 안내판");
		go.transform.SetParent(parent, false);
		return go.transform;
	}

	// 「사냥터로 이동」, 「연습장으로 이동」: 받침이 있으면(ㄹ 받침 제외) 으로
	static string MoveText(string place)
	{
		if (string.IsNullOrEmpty(place)) return "이동";
		char last = place[place.Length - 1];
		bool batchim = last >= 0xAC00 && last <= 0xD7A3 && (last - 0xAC00) % 28 != 0 && (last - 0xAC00) % 28 != 8;
		return place + (batchim ? "으로 이동" : "로 이동");
	}
}
