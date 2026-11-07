using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// [에디터 전용] 재료 드롭 아이콘 프리팹을 만든다 (#404). 메뉴 Invoke → JW → 재료 드롭 프리팹 만들기.
/// <para>
/// - Assets/_JW/Effects/Materials/material_{재료 ID}.png 시트(가로 한 줄, 정사각 칸)를 가져오기 설정(Sprite Multiple, 16 PPU, Point, 압축 없음)하고 칸 크기(= 시트 높이)로 자른다.<br/>
/// - 프리팹(SpriteRenderer + SpriteFrameLoop + MaterialDropFlight)을 Assets/_JW/Resources/MaterialDrops/{재료 ID}.prefab으로 저장한다. MaterialDropView가 재료 ID로 찾는다.<br/>
/// - 반짝임 사이에 쉬는 틈이 있게, 첫 프레임(빛줄기 없는 그림)을 Rest Frames 장 더 붙여 반복한다.<br/>
/// - 시트가 없는 재료는 건너뛴다. 다시 눌러도 같은 결과로 덮어쓴다 (프리팹에서 바꾼 값도 되돌아가니 주의).
/// </para>
/// </summary>
public static class MaterialDropPrefabBuilder
{
	const string SheetDir = "Assets/_JW/Effects/Materials/";
	const string PrefabDir = "Assets/_JW/Resources/MaterialDrops/";
	const string ThrustFlashPath = "Assets/_JW/ThrustFlash.prefab";

	// 재료 ID, 초당 프레임, 반짝임 뒤에 쉬는 장수
	static readonly (string id, float fps, int restFrames)[] Materials =
	{
		("bug_fragment", 16f, 6),
		("boss_core", 16f, 6),
	};

	[MenuItem("Invoke/JW/재료 드롭 프리팹 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources")) AssetDatabase.CreateFolder("Assets/_JW", "Resources");
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources/MaterialDrops")) AssetDatabase.CreateFolder("Assets/_JW/Resources", "MaterialDrops");

		int made = 0;
		foreach (var m in Materials)
		{
			string path = SheetDir + "material_" + m.id + ".png";
			if (AssetImporter.GetAtPath(path) == null)
			{
				Debug.LogWarning($"[MaterialDropPrefabBuilder] {path}가 없어서 {m.id}는 건너뜀");
				continue;
			}
			Sprite[] frames = SliceSheet(path);
			if (frames == null || frames.Length == 0) continue;
			MakePrefab(m.id, frames, m.fps, m.restFrames);
			made++;
		}
		AssetDatabase.SaveAssets();
		Debug.Log($"[MaterialDropPrefabBuilder] 프리팹 {made}개를 {PrefabDir}에 만들었음");
	}

	// 시트를 가져오기 설정하고 정사각 칸(시트 높이)으로 가로 한 줄 자른다. 잘린 스프라이트를 순서대로 돌려준다
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

	static void MakePrefab(string id, Sprite[] frames, float fps, int restFrames)
	{
		var obj = new GameObject(id);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		GameObject thrust = AssetDatabase.LoadAssetAtPath<GameObject>(ThrustFlashPath);
		SpriteRenderer thrustSr = thrust != null ? thrust.GetComponentInChildren<SpriteRenderer>() : null;
		if (thrustSr != null) sr.sharedMaterial = thrustSr.sharedMaterial;
		sr.sortingLayerName = "Effect";
		sr.sortingOrder = 10;

		// 반짝임(전체 프레임) 뒤에 빛줄기 없는 첫 프레임으로 잠깐 쉰다
		var sequence = new List<Sprite>(frames);
		for (int i = 0; i < restFrames; i++) sequence.Add(frames[0]);

		var loop = obj.AddComponent<SpriteFrameLoop>();
		var so = new SerializedObject(loop);
		SerializedProperty list = so.FindProperty("frames");
		list.arraySize = sequence.Count;
		for (int i = 0; i < sequence.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sequence[i];
		so.FindProperty("framesPerSecond").floatValue = fps;
		so.FindProperty("playOnceCount").intValue = 0;
		so.ApplyModifiedPropertiesWithoutUndo();

		obj.AddComponent<MaterialDropFlight>();

		PrefabUtility.SaveAsPrefabAsset(obj, PrefabDir + id + ".prefab");
		Object.DestroyImmediate(obj);
	}
}
