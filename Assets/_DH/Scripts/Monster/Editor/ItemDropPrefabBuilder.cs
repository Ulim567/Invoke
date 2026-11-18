using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// [에디터 전용] 블록·함수 드롭 아이콘 프리팹을 만든다. 메뉴 Invoke → DH → 블록 드롭 프리팹 만들기.
/// <para>
/// - Assets/_DH/Sprites/material_item_block.png 시트(16×16 칸 8장, 가로 한 줄)를 가져오기 설정(Sprite Multiple, 16 PPU, Point, 압축 없음)하고 자른다.<br/>
/// - 버그 조각 아이콘(박진우 MaterialDropPrefabBuilder)과 같은 구성(SpriteRenderer + SpriteFrameLoop + MaterialDropFlight)으로
///   Assets/_DH/Resources/MaterialDrops/item_block.prefab에 저장한다. MaterialDropView가 MonsterReward.ItemDropIconId로 찾는다.<br/>
/// - 다시 눌러도 같은 결과로 덮어쓴다 (프리팹에서 바꾼 값도 되돌아가니 주의).
/// </para>
/// </summary>
public static class ItemDropPrefabBuilder
{
	const string SheetPath = "Assets/_DH/Sprites/material_item_block.png";
	const string PrefabDir = "Assets/_DH/Resources/MaterialDrops/";
	// 버그 조각 아이콘과 같은 머티리얼을 쓰려고 빌려 온다 (MaterialDropPrefabBuilder와 같음)
	const string ThrustFlashPath = "Assets/_JW/ThrustFlash.prefab";
	const float FramesPerSecond = 16f;
	// 반짝임 뒤에 빛줄기 없는 첫 장으로 쉬는 장수
	const int RestFrames = 6;

	[MenuItem("Invoke/DH/블록 드롭 프리팹 만들기")]
	public static void Build()
	{
		if (AssetImporter.GetAtPath(SheetPath) == null)
		{
			Debug.LogWarning($"[ItemDropPrefabBuilder] {SheetPath}가 없어서 만들지 않음");
			return;
		}
		if (!AssetDatabase.IsValidFolder("Assets/_DH/Resources")) AssetDatabase.CreateFolder("Assets/_DH", "Resources");
		if (!AssetDatabase.IsValidFolder("Assets/_DH/Resources/MaterialDrops")) AssetDatabase.CreateFolder("Assets/_DH/Resources", "MaterialDrops");

		Sprite[] frames = SliceSheet(SheetPath);
		if (frames.Length == 0) return;
		MakePrefab(MonsterReward.ItemDropIconId, frames);
		AssetDatabase.SaveAssets();
		Debug.Log($"[ItemDropPrefabBuilder] {PrefabDir}{MonsterReward.ItemDropIconId}.prefab을 만들었음");
	}

	// 시트를 정사각 칸(시트 높이)으로 가로 한 줄 자른다. 잘린 스프라이트를 순서대로 돌려준다
	static Sprite[] SliceSheet(string path)
	{
		var importer = (TextureImporter)AssetImporter.GetAtPath(path);
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Multiple;
		importer.spritePixelsPerUnit = 16;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.mipmapEnabled = false;
		importer.alphaIsTransparency = true;
		importer.SaveAndReimport();

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
		int cell = texture.height;
		int count = texture.width / cell;
		string baseName = System.IO.Path.GetFileNameWithoutExtension(path);

		var factory = new SpriteDataProviderFactories();
		factory.Init();
		ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
		provider.InitSpriteEditorDataProvider();
		var rects = new List<SpriteRect>();
		for (int i = 0; i < count; i++)
		{
			rects.Add(new SpriteRect
			{
				name = $"{baseName}_{i}",
				rect = new Rect(i * cell, 0, cell, cell),
				alignment = SpriteAlignment.Center,
				pivot = new Vector2(0.5f, 0.5f),
				spriteID = GUID.Generate(),
			});
		}
		provider.SetSpriteRects(rects.ToArray());
		var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
		names?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
		provider.Apply();
		importer.SaveAndReimport();

		return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
			.OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1)))
			.ToArray();
	}

	static void MakePrefab(string id, Sprite[] frames)
	{
		var obj = new GameObject(id);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		GameObject thrust = AssetDatabase.LoadAssetAtPath<GameObject>(ThrustFlashPath);
		SpriteRenderer thrustSr = thrust != null ? thrust.GetComponentInChildren<SpriteRenderer>() : null;
		if (thrustSr != null) sr.sharedMaterial = thrustSr.sharedMaterial;
		sr.sortingLayerName = "Effect";
		sr.sortingOrder = 10;

		var sequence = new List<Sprite>(frames);
		for (int i = 0; i < RestFrames; i++) sequence.Add(frames[0]);

		var loop = obj.AddComponent<SpriteFrameLoop>();
		var so = new SerializedObject(loop);
		SerializedProperty list = so.FindProperty("frames");
		list.arraySize = sequence.Count;
		for (int i = 0; i < sequence.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sequence[i];
		so.FindProperty("framesPerSecond").floatValue = FramesPerSecond;
		so.FindProperty("playOnceCount").intValue = 0;
		so.ApplyModifiedPropertiesWithoutUndo();

		obj.AddComponent<MaterialDropFlight>();

		PrefabUtility.SaveAsPrefabAsset(obj, PrefabDir + id + ".prefab");
		Object.DestroyImmediate(obj);
	}
}
