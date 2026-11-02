using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// (에디터 전용) 정해 둔 배치표대로 열려 있는 장소 씬에 소품을 놓는다. 보여 주기만 하는 그림이라 판정은 없다.
/// <para>
/// 사용법: 장소 씬을 열고 메뉴 Invoke → 맵 → 「지금 씬 소품 놓기」. Ctrl/Cmd+Z로 되돌릴 수 있고, 저장(Ctrl/Cmd+S)은 사람이 한다.<br/>
/// - 스프라이트 소품: 씬 맨 위의 Props 오브젝트 아래에 다시 만든다 (이미 있으면 안의 것을 지우고 새로 놓음).<br/>
/// - 타일 소품: Grid 아래 Decor 타일맵(충돌 없음, Sorting Layer BackProps)을 비우고 다시 찍는다. 없으면 만든다.<br/>
/// - 로비는 손으로 놓았으므로 배치표가 없다.
/// </para>
/// </summary>
public static class MapDecorator
{
	const string PropsFolder = "Assets/_HW/Map/Props/";
	const string NoMansLandPng = "Assets/_HW/Map/Tiles/NoMansLand_Tiles.png";
	const string NoMansLandTiles = "Assets/_HW/Map/Palettes/NoMansLand_Tiles";

	// 스프라이트 소품 하나. sprite는 Props 폴더의 그림 이름 (UrbanDecay는 잘린 조각 이름). 위치는 그림의 Pivot 기준
	struct Prop
	{
		public string sprite; public float x, y; public string layer; public int order; public string color;
		public Prop(string sprite, float x, float y, string color = null, string layer = "BackProps", int order = 0)
		{ this.sprite = sprite; this.x = x; this.y = y; this.color = color; this.layer = layer; this.order = order; }
	}

	// 타일 도장 하나. 원본 그림의 (col,row)부터 w×h칸을, 왼쪽 아래 칸이 (x,y)에 오게 찍는다. y는 땅 윗면 Y
	struct Stamp
	{
		public int col, row, w, h, x, y;
		public Stamp(int col, int row, int w, int h, int x, int y) { this.col = col; this.row = row; this.w = w; this.h = h; this.x = x; this.y = y; }
	}

	class Decoration
	{
		public Prop[] props;
		public Stamp[] stamps;
	}

	// No Man's Land 그림의 소품 칸 (왼쪽 위 칸, 가로, 세로)
	static Stamp RockBig(int x, int y) => new Stamp(1, 2, 2, 2, x, y);
	static Stamp RockTall(int x, int y) => new Stamp(3, 2, 2, 2, x, y);
	static Stamp Fence(int x, int y) => new Stamp(3, 4, 3, 1, x, y);
	static Stamp Bush(int x, int y) => new Stamp(4, 5, 3, 1, x, y);
	static Stamp TreeRound(int x, int y) => new Stamp(6, 4, 1, 1, x, y);
	static Stamp TreeSmall(int x, int y) => new Stamp(8, 4, 1, 1, x, y);
	static Stamp Mound(int x, int y) => new Stamp(8, 5, 1, 1, x, y);
	static Stamp Grave(int x, int y) => new Stamp(9, 4, 1, 1, x, y);
	static Stamp TreeDead(int x, int y) => new Stamp(10, 4, 1, 1, x, y);
	static Stamp Thorns(int x, int y) => new Stamp(11, 4, 1, 1, x, y);
	static Stamp Lamp(int x, int y) => new Stamp(12, 3, 1, 3, x, y);

	// 사냥터 (흐린 낮). 땅 윗면: X -43~-21 → -4, -20~-9 언덕 → -2, -8~5 → -4, 6~12 둔덕 → -3, 13~26 → -4, 29~42 높은 단 → -1
	const string Day = "9EA399";
	static Decoration HuntingGround() => new Decoration
	{
		props = new[]
		{
			new Prop("UrbanDecay_23", -38, -1.19f, Day),
			new Prop("Grass", -30, -4, Day),
			new Prop("StopSign_Flowers_Moss", -24, -4, Day),
			new Prop("Map_Moss", -14, -2, Day),
			new Prop("Grass", -11, -2, Day),
			new Prop("UrbanDecay_21", -3, -1.19f, Day),
			new Prop("Bollard_Moss", 2, -4, Day),
			new Prop("Bollard_Moss", 2.7f, -4, Day),
			new Prop("UrbanDecay_20", 9, -1.34f, Day),
			new Prop("Grass_WTrash", 15, -4, Day),
			new Prop("UrbanDecay_13", 21, -3, Day),
			new Prop("Grass", 25, -4, Day),
			new Prop("TrafficLight_Moss", 31, -1, Day),
			new Prop("Flowers_Assorted", 36, -1, Day),
			new Prop("Foxtail_Blue", 40, -1, Day),
			new Prop("FloatingFlower_Blue", -18, 1, null, "Props", 5),
			new Prop("FloatingFlower_Blue", 4, 2, null, "Props", 5),
			new Prop("FloatingFlower_Blue", 30, 3, null, "Props", 5),
		},
		stamps = new[]
		{
			RockBig(-42, -4), Bush(-37, -4), Lamp(-34, -4), TreeDead(-27, -4), Grave(-22, -4),
			TreeRound(-18, -2), TreeSmall(-16, -2), Thorns(-10, -2),
			RockTall(-7, -4), Bush(-1, -4),
			Mound(7, -3), Bush(10, -3),
			Fence(13, -4), RockBig(23, -4),
			Thorns(30, -1), TreeDead(34, -1), Bush(38, -1),
		},
	};

	static Decoration Find(string sceneName)
	{
		switch (sceneName)
		{
			case PlaceScenes.HuntingGround: return HuntingGround();
			default: return null;
		}
	}

	[MenuItem("Invoke/맵/지금 씬 소품 놓기")]
	static void DecorateCurrentScene()
	{
		var scene = EditorSceneManager.GetActiveScene();
		Decoration deco = Find(scene.name);
		if (deco == null)
		{
			EditorUtility.DisplayDialog("소품 놓기", $"'{scene.name}' 씬의 소품 배치표가 없어요. (지금은 HuntingGround만 있음)", "확인");
			return;
		}

		int props = PlaceProps(scene, deco.props);
		int tiles = StampDecor(deco.stamps);
		EditorSceneManager.MarkSceneDirty(scene);
		Debug.Log($"[MapDecorator] {scene.name}: 스프라이트 소품 {props}개, 타일 소품 {tiles}칸. 확인하고 Ctrl/Cmd+S로 저장한다");
	}

	// 스프라이트 소품 -------------------------------------------------------

	static int PlaceProps(UnityEngine.SceneManagement.Scene scene, Prop[] props)
	{
		if (props == null || props.Length == 0) return 0;

		GameObject root = null;
		foreach (GameObject go in scene.GetRootGameObjects())
		{
			if (go.name == "Props") root = go;
		}
		if (root == null)
		{
			root = new GameObject("Props");
			Undo.RegisterCreatedObjectUndo(root, "Place Props");
		}
		for (int i = root.transform.childCount - 1; i >= 0; i--)
			Undo.DestroyObjectImmediate(root.transform.GetChild(i).gameObject);

		int count = 0;
		foreach (Prop p in props)
		{
			Sprite sprite = LoadSprite(p.sprite);
			if (sprite == null)
			{
				Debug.LogWarning($"[MapDecorator] 그림 '{p.sprite}'을 {PropsFolder}에서 못 찾음");
				continue;
			}
			var go = new GameObject(p.sprite);
			Undo.RegisterCreatedObjectUndo(go, "Place Props");
			go.transform.SetParent(root.transform, false);
			go.transform.localPosition = new Vector3(p.x, p.y, 0f);
			var sr = go.AddComponent<SpriteRenderer>();
			sr.sprite = sprite;
			sr.sortingLayerName = p.layer;
			sr.sortingOrder = p.order;
			if (p.color != null && ColorUtility.TryParseHtmlString("#" + p.color, out Color c)) sr.color = c;
			count++;
		}
		return count;
	}

	// 낱장 그림은 이름.png, UrbanDecay처럼 여러 조각으로 자른 그림은 조각 이름으로 찾는다
	static Sprite LoadSprite(string name)
	{
		var single = AssetDatabase.LoadAssetAtPath<Sprite>(PropsFolder + name + ".png");
		if (single != null) return single;
		int cut = name.LastIndexOf('_');
		if (cut < 0) return null;
		foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(PropsFolder + name.Substring(0, cut) + ".png"))
		{
			if (asset is Sprite s && s.name == name) return s;
		}
		return null;
	}

	// 타일 소품 -------------------------------------------------------------

	static int StampDecor(Stamp[] stamps)
	{
		if (stamps == null || stamps.Length == 0) return 0;

		Tilemap decor = FindOrCreateDecor();
		if (decor == null) return 0;

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NoMansLandPng);
		var byCell = new Dictionary<Vector2Int, TileBase>();
		foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { NoMansLandTiles }))
		{
			var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
			if (tile == null || tile.sprite == null || tile.sprite.texture != texture) continue;
			Rect rect = tile.sprite.rect;
			byCell[new Vector2Int(Mathf.RoundToInt(rect.x / 16f), Mathf.RoundToInt((texture.height - rect.yMax) / 16f))] = tile;
		}

		Undo.RegisterCompleteObjectUndo(decor, "Stamp Decor");
		decor.ClearAllTiles();
		int count = 0;
		foreach (Stamp s in stamps)
		{
			for (int dx = 0; dx < s.w; dx++)
			{
				for (int dy = 0; dy < s.h; dy++)
				{
					// 그림은 위쪽 줄부터, 맵은 아래쪽 줄부터 센다
					if (!byCell.TryGetValue(new Vector2Int(s.col + dx, s.row + s.h - 1 - dy), out TileBase tile)) continue;
					decor.SetTile(new Vector3Int(s.x + dx, s.y + dy, 0), tile);
					count++;
				}
			}
		}
		decor.CompressBounds();
		return count;
	}

	// Grid 아래 Decor 타일맵. 없으면 충돌 없이 만들고 BackProps 층에 그린다
	static Tilemap FindOrCreateDecor()
	{
		Grid grid = null;
		foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
		{
			if (map.name == "Decor") return map;
			if (map.name == "Ground") grid = map.layoutGrid;
		}
		if (grid == null)
		{
			EditorUtility.DisplayDialog("소품 놓기", "Grid(Ground 타일맵)를 못 찾았어요.", "확인");
			return null;
		}
		var go = new GameObject("Decor");
		Undo.RegisterCreatedObjectUndo(go, "Create Decor");
		go.transform.SetParent(grid.transform, false);
		var tilemap = go.AddComponent<Tilemap>();
		var renderer = go.AddComponent<TilemapRenderer>();
		renderer.sortingLayerName = "BackProps";
		return tilemap;
	}
}
