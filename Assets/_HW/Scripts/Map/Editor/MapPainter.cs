using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// (에디터 전용) 글자로 적은 맵 설계도대로 열려 있는 장소 씬의 땅(Ground)과 발판(Platform)을 칠한다.
/// <para>
/// 사용법: 장소 씬(Lobby, HuntingGround, HuntingGround2, HuntingGround3, TrainingRoom, RaidDungeon)을 열고 메뉴 Invoke → 맵 → 「지금 씬 땅 칠하기」.<br/>
/// - 씬 이름과 같은 설계도를 찾아서, Grid 아래 Ground·Platform 타일맵을 비우고 다시 칠한다. Ctrl/Cmd+Z로 되돌릴 수 있다.<br/>
/// - 칠한 뒤 씬 저장(Ctrl/Cmd+S)은 사람이 한다.<br/>
/// - 타일은 Tile Palette로 만든 타일 에셋(Palettes 폴더)에서, 원본 그림의 칸 위치로 찾는다.
/// </para>
/// <para>
/// 설계도 글자 (1글자 = 1칸 = 1유닛, 맨 윗줄이 top, 맨 왼쪽이 left)<br/>
/// . 빈칸 / T 윗면 / L 왼쪽 윗모서리 / R 오른쪽 윗모서리 / l 왼쪽 옆면 / r 오른쪽 옆면 / # 속<br/>
/// 발판: &lt; 왼쪽 끝 / = 가운데 / &gt; 오른쪽 끝
/// </para>
/// <para>
/// 점프 높이가 4칸이라, 발판·단은 바로 아래 땅보다 3칸 이하로 높게 둔다.
/// </para>
/// <para>
/// 발판 나누기 (노션 11/16 겹친 발판 점프 불가): 발판을 다 칠한 뒤 발판 하나(가로로 이어진 칸)마다 Platform을 복제한 오브젝트(Platform_1, 2, …)로 옮긴다.
/// 원래 Platform 타일맵은 비워 두고 설정(레이어, Platform Effector, 콜라이더) 원본으로만 쓴다.<br/>
/// 이유: 발판이 콜라이더 하나로 합쳐져 있으면, 박진우의 PlayerMovement가 아래에서 뚫고 오를 때 끈 충돌이 「몸이 그 콜라이더에서 빠져나올 때」 다시 켜지는데,
/// 위아래로 겹친 발판(층 사이 3칸, 캐릭터 3.75칸)에서는 아래 발판에 서 있어도 몸이 위 발판에 걸쳐 있어 계속 「발판 안」으로 쳐서 점프가 안 됐다.<br/>
/// 발판 그림 (노션 11/16): 바깥 장소의 발판은 단단한 부분을 2픽셀 → 5픽셀로 두껍게 그린 platform_thick_left / mid / right (Assets/_HW/Map/Tiles)를 쓴다. 타일 에셋은 처음 칠할 때 Palettes/PlatformThick에 만든다.
/// 콜라이더는 그림 .meta의 Physics Shape로 맨 위 5픽셀 띠만 쓴다. 아래 장식까지 잡으면 발판 아래 땅에 선 캐릭터 머리가 걸쳐 그 자리에서 점프로 못 오른다.
/// </para>
/// </summary>
public static class MapPainter
{
	// 글자 → 어느 그림의 어느 칸(왼쪽 위가 0,0)
	class Tileset
	{
		public string tileFolder;
		public Dictionary<char, (string texture, Vector2Int cell)> cells;
	}

	class Layout
	{
		public int left, top;      // 설계도 맨 왼쪽 위 칸의 좌표
		public Tileset tiles;
		public string[] ground;
		public string[] platform;
		public string platformStyle = "thick"; // 바깥 장소 발판 그림 (platform_<style>_left/mid/right). 사냥터2 cloud, 사냥터3 neon (#518)
	}

	const string NoMansLandPng = "Assets/_HW/Map/Tiles/NoMansLand_Tiles.png";
	const string CyberLabGroundPng = "Assets/_HW/Map/Tiles/CyberLab_Ground.png";
	const string CyberLabTileSetPng = "Assets/_HW/Map/Tiles/CyberLab_TileSet.png";

	// 사냥터·로비·레이드: No Man's Land 돌 땅
	static readonly Tileset NoMansLand = new Tileset
	{
		tileFolder = "Assets/_HW/Map/Palettes/NoMansLand_Tiles",
		cells = new Dictionary<char, (string, Vector2Int)>
		{
			{ 'L', (NoMansLandPng, new Vector2Int(9, 1)) }, { 'T', (NoMansLandPng, new Vector2Int(10, 1)) }, { 'R', (NoMansLandPng, new Vector2Int(11, 1)) },
			{ 'l', (NoMansLandPng, new Vector2Int(9, 2)) }, { '#', (NoMansLandPng, new Vector2Int(10, 2)) }, { 'r', (NoMansLandPng, new Vector2Int(11, 2)) },
			{ '<', (NoMansLandPng, new Vector2Int(4, 1)) }, { '=', (NoMansLandPng, new Vector2Int(5, 1)) }, { '>', (NoMansLandPng, new Vector2Int(6, 1)) },
		},
	};

	// 연습장: CyberLab 실내. 바닥 윗면·발판은 밝은 판, 속·벽·천장은 어두운 벽 판
	static readonly Tileset CyberLab = new Tileset
	{
		tileFolder = "Assets/_HW/Map/Palettes/CyberLab_Tiles",
		cells = new Dictionary<char, (string, Vector2Int)>
		{
			{ 'L', (CyberLabGroundPng, new Vector2Int(1, 5)) }, { 'T', (CyberLabGroundPng, new Vector2Int(2, 5)) }, { 'R', (CyberLabGroundPng, new Vector2Int(3, 5)) },
			{ 'l', (CyberLabTileSetPng, new Vector2Int(4, 8)) }, { '#', (CyberLabTileSetPng, new Vector2Int(4, 8)) }, { 'r', (CyberLabTileSetPng, new Vector2Int(4, 8)) },
			{ '<', (CyberLabGroundPng, new Vector2Int(1, 5)) }, { '=', (CyberLabGroundPng, new Vector2Int(2, 5)) }, { '>', (CyberLabGroundPng, new Vector2Int(3, 5)) },
		},
	};

	// 로비: 가로 72칸. 왼쪽 시작 → 가운데 광장(문 3개, X -6·-2·2) → 오른쪽 높은 단. 바닥 윗면 Y -4, 높은 단 윗면 Y -2, 양쪽 벽 Y 3까지
	// 발판은 문 위 안내판(문 위 끝에서 1.8칸, 가로 X -11~7쯤)과 겹치지 않게 X -26~-22·11~15(윗면 -1), -18~-14(윗면 1) (#518)
	static Layout Lobby() => new Layout
	{
		left = -36, top = 10, tiles = NoMansLand,
		ground = new[]
		{
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"TR....................................................................LT",
			"#r....................................................................l#",
			"#r....................................................................l#",
			"#r....................................................................l#",
			"#r....................................................................l#",
			"#r....................................................................l#",
			"#r....................................................LTTTTTTTTTTTTTTTl#",
			"#r.................................................LTTl###############l#",
			"##TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT#####################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
			"########################################################################",
		},
		platform = new[]
		{
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"..................<===>.................................................",
			"........................................................................",
			"..........<===>................................<===>....................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
			"........................................................................",
		},
	};

	// 사냥터: 가로 80칸 (김도훈 요청). 평평한 바닥(윗면 Y -4) + 메이플처럼 발판 3층(윗면 Y -1, 2, 5, 층 사이 3칸). 발판은 전부 12칸, 위층 발판은 아래층과 4칸씩 겹쳐서 올라갈 수 있다. 오른쪽(X 20~37)은 위가 뚫려 있어 Loop가 날아다닐 넓은 구역. 양 끝 벽 Y 10까지
	static Layout HuntingGround() => new Layout
	{
		left = -40, top = 10, tiles = NoMansLand,
		ground = new[]
		{
			"TR............................................................................LT",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"#r............................................................................l#",
			"##TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT##",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
			"################################################################################",
		},
		platform = new[]
		{
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"......................<==========>..............................................",
			"................................................................................",
			"................................................................................",
			"..............<==========>......................<==========>....................",
			"................................................................................",
			"................................................................................",
			"......<==========>..............<==========>..............<==========>..........",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
			"................................................................................",
		},
	};

	// 레이드 던전: 가로 44칸 보스방 평지. 바닥 윗면 Y -4, 양 끝 벽은 Y 12까지(바닥에서 16칸). 발판 없음. Gateway는 가운데(X 0)
	// 화면 가로(40칸)보다 넓어야 카메라가 양 끝 벽을 안 보여 준다 (CameraBounds Edge Inset 2). 40칸이던 때는 벽이 늘 보였음 (#421). 안쪽 X -20~19는 그대로
	static Layout RaidDungeon() => new Layout
	{
		left = -22, top = 12, tiles = NoMansLand,
		ground = new[]
		{
			"TR........................................LT",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"#r........................................l#",
			"##TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT##",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
			"############################################",
		},
		platform = new[]
		{
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
			"............................................",
		},
	};

	// 연습장: 가로 50칸 실내. 천장 Y 9, 발판 3개. 바닥 윗면 Y -4
	static Layout TrainingRoom() => new Layout
	{
		left = -25, top = 16, tiles = CyberLab,
		ground = new[]
		{
			"TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"#r..............................................l#",
			"##TTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT##",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
			"##################################################",
		},
		platform = new[]
		{
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"......................<===>.......................",
			"..................................................",
			"..........<===>....................<===>..........",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
			"..................................................",
		},
	};

	// 사냥터2·3 (#518)처럼 큰 맵은 설계도 글자를 손으로 적지 않고, 벽·바닥 모양 + 발판 목록으로 만든다.
	// 맨 윗줄(top)에 양쪽 벽 꼭대기, 바닥 윗면 floorTop, 바닥 아래 9줄은 속. 양 끝 벽 2칸
	// 발판 하나 = (왼쪽 칸 X, 오른쪽 칸 X, 윗면 Y). 칸 X는 그 칸의 왼쪽 끝 좌표
	// 땅 모양 바꾸기 (#518): height > 0이면 X x0~x1에 height칸 높은 언덕, < 0이면 그만큼 깊은 구덩이. 점프 높이(4칸) 안으로, 발판 아래에는 두지 않는다 (머리가 발판에 걸림)
	static Layout Generated(int left, int width, int top, int floorTop, (int x0, int x1, int surface)[] platforms, (int x0, int x1, int height)[] terrain = null)
	{
		int floorRow = top - (floorTop - 1);
		int rows = floorRow + 10;
		var groundCells = new char[rows][];
		var platform = new char[rows][];
		for (int r = 0; r < rows; r++)
		{
			string inner = new string('.', width - 4);
			string row;
			if (r == 0) row = "TR" + inner + "LT";
			else if (r < floorRow) row = "#r" + inner + "l#";
			else if (r == floorRow) row = "##" + new string('T', width - 4) + "##";
			else row = new string('#', width);
			groundCells[r] = row.ToCharArray();
			platform[r] = new string('.', width).ToCharArray();
		}
		if (terrain != null)
		{
			foreach (var (x0, x1, height) in terrain)
			{
				int c0 = x0 - left, c1 = x1 - left;
				if (height > 0)
				{
					// 언덕: 윗줄 L T… R, 아래 l #… r, 원래 바닥 윗면 줄은 속으로
					for (int c = c0; c <= c1; c++) groundCells[floorRow][c] = '#';
					for (int h = 1; h <= height; h++)
					{
						int r = floorRow - h;
						bool topRow = h == height;
						for (int c = c0; c <= c1; c++)
							groundCells[r][c] = c == c0 ? (topRow ? 'L' : 'l') : c == c1 ? (topRow ? 'R' : 'r') : (topRow ? 'T' : '#');
					}
				}
				else if (height < 0)
				{
					// 구덩이: 바닥을 파내고 그 아래 줄이 새 바닥 윗면. 양옆 땅은 구덩이 쪽을 향한 옆면
					int depth = -height;
					for (int d = 0; d < depth; d++)
					{
						int r = floorRow + d;
						for (int c = c0; c <= c1; c++) groundCells[r][c] = '.';
						groundCells[r][c0 - 1] = d == 0 ? 'R' : 'r';
						groundCells[r][c1 + 1] = d == 0 ? 'L' : 'l';
					}
					for (int c = c0; c <= c1; c++) groundCells[floorRow + depth][c] = 'T';
				}
			}
		}
		var ground = new string[rows];
		for (int r = 0; r < rows; r++) ground[r] = new string(groundCells[r]);
		foreach (var (x0, x1, surface) in platforms)
		{
			int r = top - (surface - 1);
			for (int x = x0; x <= x1; x++) platform[r][x - left] = x == x0 ? '<' : x == x1 ? '>' : '=';
		}
		var platformRows = new string[rows];
		for (int r = 0; r < rows; r++) platformRows[r] = new string(platform[r]);
		return new Layout { left = left, top = top, tiles = NoMansLand, ground = ground, platform = platformRows };
	}

	static Layout WithPlatformStyle(Layout layout, string style)
	{
		layout.platformStyle = style;
		return layout;
	}

	// 사냥터2 (#518, 캔버스 17-2 「세로로 더 길게, 위로 올라갈 수 있도록」): 가로 60칸(X -30~29), 양 끝 벽 Y 24까지. 바닥 윗면 Y -4.
	// 발판 7층(윗면 Y -1, 2, 5, 8, 11, 14, 17, 층 사이 3칸)을 좌우로 지그재그. 위층 발판은 아래층과 2칸 넘게 겹쳐 올라갈 수 있다.
	// 포탈: 바닥 가운데(X 0) ↔ 사냥터1, 맨 위 왼쪽 발판(X -24) ↔ 사냥터3 왼쪽, 맨 위 오른쪽 발판(X 24) ↔ 사냥터3 오른쪽 (HuntingGroundBuilder)
	internal static readonly (int, int, int)[] HuntingGround2Platforms =
	{
		(-24, -11, -1), (10, 23, -1),
		(-14, -3, 2), (2, 13, 2),
		(-6, 5, 5),
		(-16, -4, 8), (3, 15, 8),
		(-26, -13, 11), (12, 25, 11),
		(-18, -7, 14), (6, 17, 14),
		(-28, -15, 17), (14, 27, 17),
	};
	// 발판 그림: 구름 (맑은 하늘 탑 분위기)
	static Layout HuntingGround2() => WithPlatformStyle(Generated(-30, 60, 24, -4, HuntingGround2Platforms), "cloud");

	// 사냥터3 (#518, 캔버스 17-3 「가로로 더 길게」): 가로 120칸(X -60~59), 양 끝 벽 Y 10까지. 바닥 윗면 Y -4.
	// 사냥터1처럼 발판 3층(윗면 Y -1, 2, 5), 12칸짜리를 26칸 간격으로 되풀이. 포탈은 바닥 양 끝(X -56, 55) ↔ 사냥터2 맨 위 왼쪽·오른쪽
	// 땅이 평평하기만 하면 사냥터1과 똑같아 보여서 언덕·구덩이를 둔다 (1층 발판 아래가 아닌 곳): 언덕 X -34~-28(1칸), 18~21(1칸), 22~25(2칸, 계단), 구덩이 X -8~-4·44~48(2칸)
	internal static readonly (int, int, int)[] HuntingGround3Terrain =
	{
		(-34, -28, 1), (18, 21, 1), (22, 25, 2), (-8, -4, -2), (44, 48, -2),
	};
	internal static readonly (int, int, int)[] HuntingGround3Platforms =
	{
		(-48, -37, -1), (-22, -11, -1), (4, 15, -1), (30, 41, -1),
		(-40, -29, 2), (-14, -3, 2), (12, 23, 2),
		(-32, -21, 5), (-6, 5, 5), (20, 31, 5),
	};
	// 발판 그림: 네온 금속판 (보라 밤, 민트 빛 윗면 + 분홍 글리치 조각)
	static Layout HuntingGround3() => WithPlatformStyle(Generated(-60, 120, 10, -4, HuntingGround3Platforms, HuntingGround3Terrain), "neon");

	static Layout Find(string sceneName)
	{
		switch (sceneName)
		{
			case PlaceScenes.Lobby: return Lobby();
			case PlaceScenes.HuntingGround: return HuntingGround();
			case PlaceScenes.HuntingGround2: return HuntingGround2();
			case PlaceScenes.HuntingGround3: return HuntingGround3();
			case PlaceScenes.TrainingRoom: return TrainingRoom();
			case PlaceScenes.RaidDungeon: return RaidDungeon();
			default: return null;
		}
	}

	[MenuItem("Invoke/맵/지금 씬 땅 칠하기")]
	static void PaintCurrentScene()
	{
		string error = PaintActiveScene();
		if (error != null) EditorUtility.DisplayDialog("맵 칠하기", error, "확인");
	}

	/// <summary>
	/// 열려 있는 씬을 설계도대로 칠한다. 다른 에디터 도구(HuntingGroundBuilder)도 부른다. 저장은 하지 않는다. 못 칠하면 이유, 칠하면 null
	/// </summary>
	internal static string PaintActiveScene()
	{
		var scene = EditorSceneManager.GetActiveScene();
		Layout layout = Find(scene.name);
		if (layout == null) return $"'{scene.name}' 씬의 설계도가 없어요. 장소 씬을 열고 눌러 주세요.";

		Tilemap ground = FindTilemap("Ground");
		Tilemap platform = FindTilemap("Platform");
		if (ground == null || platform == null) return "Grid 아래에 Ground, Platform 타일맵이 있어야 해요.";

		Dictionary<char, TileBase> tiles = LoadTiles(layout.tiles);
		if (layout.tiles == NoMansLand) UsePlatformTiles(tiles, layout.platformStyle);

		Undo.RegisterCompleteObjectUndo(new Object[] { ground, platform }, "Paint Map");
		ground.ClearAllTiles();
		platform.ClearAllTiles();
		int count = Paint(ground, layout.ground, layout, tiles) + Paint(platform, layout.platform, layout, tiles);
		ground.CompressBounds();
		int pieces = SplitPlatforms(platform);
		RebuildColliders(scene);

		EditorSceneManager.MarkSceneDirty(scene);
		Debug.Log($"[MapPainter] {scene.name}: 타일 {count}칸을 칠함, 발판 {pieces}개로 나눔. 확인하고 Ctrl/Cmd+S로 저장한다");
		return null;
	}

	/// <summary>
	/// 칠한 타일대로 콜라이더 모양을 바로 다시 만든다 (#518). 타일맵 콜라이더는 다음 에디터 갱신 때 늦게 바뀌어서,
	/// 칠하자마자 저장하면(HuntingGroundBuilder) 복사해 온 사냥터1의 땅 모양이 씬에 남아 사냥터2 양 끝을 걸어 나가고 사냥터3 끝에서 바닥 밑으로 떨어졌다
	/// </summary>
	internal static void RebuildColliders(UnityEngine.SceneManagement.Scene scene)
	{
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			foreach (TilemapCollider2D tiles in root.GetComponentsInChildren<TilemapCollider2D>(true)) tiles.ProcessTilemapChanges();
			foreach (CompositeCollider2D composite in root.GetComponentsInChildren<CompositeCollider2D>(true)) composite.GenerateGeometry();
		}
	}

	// 발판 하나(같은 줄에서 가로로 이어진 칸)마다 Platform을 복제한 오브젝트로 옮긴다. 예전에 나눈 Platform_* 는 먼저 지운다
	static int SplitPlatforms(Tilemap platform)
	{
		Transform parent = platform.transform.parent;
		if (parent != null)
		{
			var old = new List<GameObject>();
			foreach (Transform child in parent)
				if (child.name.StartsWith("Platform_")) old.Add(child.gameObject);
			foreach (GameObject go in old) Undo.DestroyObjectImmediate(go);
		}

		platform.CompressBounds();
		BoundsInt bounds = platform.cellBounds;
		var pieces = new List<List<(Vector3Int cell, TileBase tile)>>();
		for (int y = bounds.yMin; y < bounds.yMax; y++)
		{
			List<(Vector3Int, TileBase)> run = null;
			for (int x = bounds.xMin; x <= bounds.xMax; x++)
			{
				var cell = new Vector3Int(x, y, 0);
				TileBase tile = x < bounds.xMax ? platform.GetTile(cell) : null;
				if (tile == null) { run = null; continue; }
				if (run == null) { run = new List<(Vector3Int, TileBase)>(); pieces.Add(run); }
				run.Add((cell, tile));
			}
		}

		for (int i = 0; i < pieces.Count; i++)
		{
			GameObject copy = Object.Instantiate(platform.gameObject, parent);
			copy.name = $"Platform_{i + 1}";
			Undo.RegisterCreatedObjectUndo(copy, "Paint Map");
			Tilemap map = copy.GetComponent<Tilemap>();
			map.ClearAllTiles();
			foreach (var (cell, tile) in pieces[i]) map.SetTile(cell, tile);
			map.CompressBounds();
		}

		// 원본은 비워 두고 설정 원본으로만 쓴다
		platform.ClearAllTiles();
		platform.CompressBounds();
		return pieces.Count;
	}

	// 바깥 장소의 발판 칸(< = >)을 두꺼운 발판 그림으로. 타일 에셋이 없으면 만든다
	// 바깥 장소의 발판 칸(< = >)을 장소별 발판 그림으로. 타일 에셋(Palettes/Platform<Style>)이 없으면 만든다
	// thick: 두꺼운 돌 발판 (로비·사냥터1·레이드) / cloud: 구름 (사냥터2) / neon: 네온 금속판 (사냥터3). 콜라이더는 셋 다 그림 .meta의 Physics Shape(맨 위 5픽셀 띠)
	static void UsePlatformTiles(Dictionary<char, TileBase> tiles, string style)
	{
		if (string.IsNullOrEmpty(style)) style = "thick";
		string folderName = "Platform" + char.ToUpper(style[0]) + style.Substring(1);
		string folder = $"Assets/_HW/Map/Palettes/{folderName}";
		(char ch, string name)[] parts = { ('<', $"platform_{style}_left"), ('=', $"platform_{style}_mid"), ('>', $"platform_{style}_right") };
		if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_HW/Map/Palettes", folderName);
		foreach (var (ch, name) in parts)
		{
			string tilePath = $"{folder}/{name}.asset";
			var tile = AssetDatabase.LoadAssetAtPath<Tile>(tilePath);
			if (tile == null)
			{
				Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_HW/Map/Tiles/{name}.png");
				if (sprite == null)
				{
					Debug.LogWarning($"[MapPainter] Assets/_HW/Map/Tiles/{name}.png를 못 찾아 예전 발판 그림을 씀");
					continue;
				}
				tile = ScriptableObject.CreateInstance<Tile>();
				tile.sprite = sprite;
				tile.colliderType = Tile.ColliderType.Sprite;
				AssetDatabase.CreateAsset(tile, tilePath);
			}
			tiles[ch] = tile;
		}
		AssetDatabase.SaveAssets();
	}

	static Tilemap FindTilemap(string name)
	{
		foreach (Tilemap map in Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
		{
			if (map.name == name) return map;
		}
		return null;
	}

	static int Paint(Tilemap map, string[] rows, Layout layout, Dictionary<char, TileBase> tiles)
	{
		if (rows == null) return 0;
		int count = 0;
		for (int r = 0; r < rows.Length; r++)
		{
			for (int c = 0; c < rows[r].Length; c++)
			{
				char ch = rows[r][c];
				if (ch == '.' || !tiles.TryGetValue(ch, out TileBase tile)) continue;
				map.SetTile(new Vector3Int(layout.left + c, layout.top - r, 0), tile);
				count++;
			}
		}
		return count;
	}

	// 타일 폴더의 타일들 중에서, 원본 그림의 그 칸을 그린 타일을 글자마다 찾는다
	static Dictionary<char, TileBase> LoadTiles(Tileset set)
	{
		var byCell = new Dictionary<(string, Vector2Int), TileBase>();
		foreach (string guid in AssetDatabase.FindAssets("t:Tile", new[] { set.tileFolder }))
		{
			var tile = AssetDatabase.LoadAssetAtPath<Tile>(AssetDatabase.GUIDToAssetPath(guid));
			if (tile == null || tile.sprite == null) continue;
			Texture2D texture = tile.sprite.texture;
			Rect rect = tile.sprite.rect; // 아래쪽이 0인 픽셀 좌표
			var cell = new Vector2Int(Mathf.RoundToInt(rect.x / 16f), Mathf.RoundToInt((texture.height - rect.yMax) / 16f));
			byCell[(AssetDatabase.GetAssetPath(texture), cell)] = tile;
		}

		var result = new Dictionary<char, TileBase>();
		foreach (var pair in set.cells)
		{
			if (byCell.TryGetValue(pair.Value, out TileBase tile)) result[pair.Key] = tile;
			else Debug.LogWarning($"[MapPainter] '{pair.Key}' → {pair.Value.texture} 칸({pair.Value.cell.x}, {pair.Value.cell.y}) 타일을 {set.tileFolder}에서 못 찾음");
		}
		return result;
	}
}
