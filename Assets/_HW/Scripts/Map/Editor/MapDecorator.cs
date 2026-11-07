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
/// - 연습장은 BackWall 타일맵(충돌 없음, Sorting Layer Sky)에 CyberLab 벽 무늬도 깐다.<br/>
/// - 로비는 손으로 놓았으므로 배치표가 없다.
/// </para>
/// </summary>
public static class MapDecorator
{
	const string PropsFolder = "Assets/_HW/Map/Props/";
	const string NoMansLandPng = "Assets/_HW/Map/Tiles/NoMansLand_Tiles.png";
	const string NoMansLandTiles = "Assets/_HW/Map/Palettes/NoMansLand_Tiles";
	const string TilesFolder = "Assets/_HW/Map/Tiles/";
	const string CyberLabTileSetPng = "Assets/_HW/Map/Tiles/CyberLab_TileSet.png";
	const string CyberLabTiles = "Assets/_HW/Map/Palettes/CyberLab_Tiles";

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
		public RectInt backWall;   // 연습장처럼 실내 벽을 깔 칸 범위 (비우면 안 깖)
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

	// 사냥터 (흐린 낮). 가로 80칸, 바닥 윗면 Y -4. 발판 1층(윗면 -1): X -34~-23, -8~3, 18~29 / 2층(2): -26~-15, 8~19 / 3층(5): -18~-7
	// 키 큰 소품은 발판 아래가 아닌 곳(X -38~-35, 4~7, 30~37)에만 둔다
	const string Day = "9EA399";
	static Decoration HuntingGround() => new Decoration
	{
		props = new[]
		{
			new Prop("Grass", -31, -4, Day),
			new Prop("StopSign_Flowers_Moss", -21, -4, Day),
			new Prop("Map_Moss", -18, -4, Day),
			new Prop("UrbanDecay_20", -12, -2.34f, Day),
			new Prop("Grass", -9, -4, Day),
			new Prop("Bollard_Moss", -2, -4, Day),
			new Prop("Bollard_Moss", -1.3f, -4, Day),
			new Prop("Grass_WTrash", 12, -4, Day),
			new Prop("UrbanDecay_13", 23, -3, Day),
			new Prop("Flowers_Assorted", 31, -4, Day),
			new Prop("TrafficLight_Moss", 34, -4, Day),
			new Prop("Foxtail_Blue", 37, -4, Day),
			new Prop("FloatingFlower_Blue", -20, 4, null, "Props", 5),
			new Prop("FloatingFlower_Blue", 5, 6, null, "Props", 5),
			new Prop("FloatingFlower_Blue", 33, 4, null, "Props", 5),
		},
		stamps = new[]
		{
			RockBig(-38, -4), Bush(-33, -4), TreeDead(-27, -4), Grave(-24, -4),
			Lamp(-16, -4), TreeRound(-6, -4), RockTall(1, -4),
			Mound(9, -4), Bush(14, -4), Fence(19, -4), Thorns(27, -4), TreeDead(29, -4),
		},
	};

	// 레이드 던전 (폭풍 밤). 가로 40칸 평지, 바닥 윗면 Y -4, 양쪽 벽 X -20·-19, 18·19. 가운데(X -4~4)는 Gateway 자리라 비운다
	const string Night = "5A5C80";
	static Decoration RaidDungeon() => new Decoration
	{
		props = new[]
		{
			new Prop("UrbanDecay_13", 9, -3, Night),
			new Prop("Grass_WTrash", -9, -4, Night),
			new Prop("Foxtail_Pink", -6, -4, Night),
			new Prop("Grass", 6, -4, Night),
			new Prop("FloatingFlower_Blue", -11, 6, null, "Props", 5),
			new Prop("FloatingFlower_Blue", 10, 7, null, "Props", 5),
		},
		stamps = new[]
		{
			Lamp(-17, -4), Lamp(16, -4),
			RockTall(-12, -4), Grave(-10, -4), Thorns(5, -4), RockBig(11, -4),
		},
	};

	// 연습장 (실내). 바닥 윗면 Y -4, 천장 아랫면 Y 9, 양쪽 벽 X -25·-24, 23·24. CyberLab 소품은 그림 조각이라 Tiles 폴더에서 찾는다
	static Decoration TrainingRoom() => new Decoration
	{
		backWall = new RectInt(-23, -4, 46, 13),
		props = new[]
		{
			new Prop("CyberLab_Objects_2", -14, 8.75f), new Prop("CyberLab_Objects_2", -2, 8.75f), new Prop("CyberLab_Objects_2", 10, 8.75f),
			new Prop("CyberLab_Objects_1", -20, 8.75f), new Prop("CyberLab_Objects_1", 4, 8.75f), new Prop("CyberLab_Objects_1", 16, 8.75f),
			new Prop("CyberLab_Objects_14", -21, -3.16f), new Prop("CyberLab_Objects_14", -18.4f, -3.16f), new Prop("CyberLab_Objects_15", -16.4f, -3.16f),
			new Prop("CyberLab_Objects_12", 6, -3.16f), new Prop("CyberLab_Objects_13", 8.8f, -3.53f),
			new Prop("CyberLab_Objects_4", 19, -3.19f), new Prop("CyberLab_Objects_6", 21, -3.59f),
			new Prop("CyberLab_Objects_9", -8, 3), new Prop("CyberLab_Objects_10", -5.9f, 3),
			new Prop("CyberLab_Objects_7", 21, 4), new Prop("CyberLab_Objects_8", -22, 2),
			new Prop("CyberLab_Objects_11", 14, 5), new Prop("CyberLab_Objects_0", -1, 4.5f),
		},
	};

	static Decoration Find(string sceneName)
	{
		switch (sceneName)
		{
			case PlaceScenes.HuntingGround: return HuntingGround();
			case PlaceScenes.RaidDungeon: return RaidDungeon();
			case PlaceScenes.TrainingRoom: return TrainingRoom();
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
			EditorUtility.DisplayDialog("소품 놓기", $"'{scene.name}' 씬의 소품 배치표가 없어요. (사냥터·레이드 던전·연습장만 있음)", "확인");
			return;
		}

		int props = PlaceProps(scene, deco.props);
		int tiles = StampDecor(deco.stamps) + FillBackWall(deco.backWall);
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
		foreach (string folder in new[] { PropsFolder, TilesFolder })
		{
			var single = AssetDatabase.LoadAssetAtPath<Sprite>(folder + name + ".png");
			if (single != null) return single;
			int cut = name.LastIndexOf('_');
			if (cut < 0) continue;
			foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(folder + name.Substring(0, cut) + ".png"))
			{
				if (asset is Sprite s && s.name == name) return s;
			}
		}
		return null;
	}

	// 실내 벽 -----------------------------------------------------------------

	// CyberLab 벽 판(6×6칸 무늬)을 범위 안에 반복해서 BackWall 타일맵에 깐다. 충돌 없음, Sky 층(가장 뒤)에 그린다
	static int FillBackWall(RectInt area)
	{
		if (area.width <= 0 || area.height <= 0) return 0;
		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(CyberLabTileSetPng);
		Dictionary<Vector2Int, TileBase> byCell = TilesOf(texture, CyberLabTiles);
		Tilemap wall = FindOrCreateTilemap("BackWall", "Sky");
		if (wall == null) return 0;

		Undo.RegisterCompleteObjectUndo(wall, "Fill Back Wall");
		wall.ClearAllTiles();
		int count = 0;
		for (int x = area.xMin; x < area.xMax; x++)
		{
			for (int y = area.yMin; y < area.yMax; y++)
			{
				// 무늬 판: 그림의 (1,7)부터 6×6칸. 맵은 아래쪽 줄부터 센다
				int col = 1 + Mod(x - area.xMin, 6);
				int row = 12 - Mod(y - area.yMin, 6);
				if (!byCell.TryGetValue(new Vector2Int(col, row), out TileBase tile)) continue;
				wall.SetTile(new Vector3Int(x, y, 0), tile);
				count++;
			}
		}
		wall.CompressBounds();
		return count;
	}

	static int Mod(int a, int n) => ((a % n) + n) % n;

	// 타일 소품 -------------------------------------------------------------

	static int StampDecor(Stamp[] stamps)
	{
		if (stamps == null || stamps.Length == 0) return 0;

		Tilemap decor = FindOrCreateTilemap("Decor", "BackProps");
		if (decor == null) return 0;

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(NoMansLandPng);
		Dictionary<Vector2Int, TileBase> byCell = TilesOf(texture, NoMansLandTiles);

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

	// 그 그림으로 만든 타일들을 원본 그림의 칸(왼쪽 위가 0,0)으로 찾을 수 있게 모은다
	static Dictionary<Vector2Int, TileBase> TilesOf(Texture2D texture, string folder)
	{
		var byCell = new Dictionary<Vector2Int, TileBase>();
		if (texture == null) return byCell;
		foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { folder }))
		{
			var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
			if (tile == null || tile.sprite == null || tile.sprite.texture != texture) continue;
			Rect rect = tile.sprite.rect;
			byCell[new Vector2Int(Mathf.RoundToInt(rect.x / 16f), Mathf.RoundToInt((texture.height - rect.yMax) / 16f))] = tile;
		}
		return byCell;
	}

	// Grid 아래 이름이 name인 타일맵. 없으면 충돌 없이 만들고 sortingLayer 층에 그린다
	static Tilemap FindOrCreateTilemap(string name, string sortingLayer)
	{
		Grid grid = null;
		foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
		{
			if (map.name == name) return map;
			if (map.name == "Ground") grid = map.layoutGrid;
		}
		if (grid == null)
		{
			EditorUtility.DisplayDialog("소품 놓기", "Grid(Ground 타일맵)를 못 찾았어요.", "확인");
			return null;
		}
		var go = new GameObject(name);
		Undo.RegisterCreatedObjectUndo(go, "Create " + name);
		go.transform.SetParent(grid.transform, false);
		var tilemap = go.AddComponent<Tilemap>();
		var renderer = go.AddComponent<TilemapRenderer>();
		renderer.sortingLayerName = sortingLayer;
		return tilemap;
	}
}
