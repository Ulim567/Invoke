using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 전용] 몬스터 체력 바 프리팹을 만든다 (#388). 메뉴 Invoke → JW → 몬스터 체력 바 프리팹 만들기.
/// <para>
/// - Assets/_JW/Effects/MonsterHp의 그림 3장을 가져오기 설정(Sprite Single, 16 PPU, Point, 압축 없음)한다. 테두리는 기준점 가운데, 채움·잔상은 왼쪽 가운데.<br/>
/// - Assets/_JW/Resources/MonsterHealthBar.prefab: 테두리(최상위) + 잔상 + 채움(자식), Sorting Layer WorldUI. 채움·잔상은 테두리 안쪽 칸(왼쪽에서 5px)에 맞춘다.<br/>
/// - 다시 눌러도 같은 결과로 덮어쓴다.
/// </para>
/// </summary>
public static class MonsterHealthBarBuilder
{
	const string Dir = "Assets/_JW/Effects/MonsterHp/";
	const string PrefabPath = "Assets/_JW/Resources/MonsterHealthBar.prefab";
	const float Ppu = 16f;
	const int FrameWidth = 30;
	const int InnerLeft = 5; // 테두리 왼쪽 끝에서 안쪽 칸까지 (px)

	[MenuItem("Invoke/JW/몬스터 체력 바 프리팹 만들기")]
	public static void Build()
	{
		Sprite frame = Import(Dir + "monster_hp_frame.png", new Vector2(0.5f, 0.5f));
		Sprite fill = Import(Dir + "monster_hp_fill.png", new Vector2(0f, 0.5f));
		Sprite trail = Import(Dir + "monster_hp_trail.png", new Vector2(0f, 0.5f));
		if (frame == null || fill == null || trail == null) return;
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources")) AssetDatabase.CreateFolder("Assets/_JW", "Resources");

		var root = new GameObject("MonsterHealthBar");
		var frameSr = root.AddComponent<SpriteRenderer>();
		frameSr.sprite = frame;
		frameSr.sortingLayerName = "WorldUI";
		frameSr.sortingOrder = 0;

		// 안쪽 칸 왼쪽 끝 = 테두리 가운데에서 (InnerLeft - FrameWidth/2) px
		float innerX = (InnerLeft - FrameWidth / 2f) / Ppu;
		Transform trailT = MakePart(root.transform, "Trail", trail, innerX, 1);
		Transform fillT = MakePart(root.transform, "Fill", fill, innerX, 2);

		var bar = root.AddComponent<MonsterHealthBar>();
		var so = new SerializedObject(bar);
		so.FindProperty("fill").objectReferenceValue = fillT;
		so.FindProperty("trail").objectReferenceValue = trailT;
		so.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
		Object.DestroyImmediate(root);
		AssetDatabase.SaveAssets();
		Debug.Log($"[MonsterHealthBarBuilder] {PrefabPath}를 만들었음");
	}

	static Transform MakePart(Transform parent, string name, Sprite sprite, float x, int order)
	{
		var obj = new GameObject(name);
		obj.transform.SetParent(parent, false);
		obj.transform.localPosition = new Vector3(x, 0f, 0f);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = sprite;
		sr.sortingLayerName = "WorldUI";
		sr.sortingOrder = order;
		return obj.transform;
	}

	static Sprite Import(string path, Vector2 pivot)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			Debug.LogError($"[MonsterHealthBarBuilder] {path}가 없음");
			return null;
		}
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.spritePixelsPerUnit = Ppu;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.mipmapEnabled = false;
		importer.alphaIsTransparency = true;
		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		settings.spriteAlignment = (int)SpriteAlignment.Custom;
		settings.spritePivot = pivot;
		importer.SetTextureSettings(settings);
		importer.SaveAndReimport();
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}
}
