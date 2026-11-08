using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// [에디터 전용] 대상 선택 조준점 프리팹을 만든다 (#438). 메뉴 Invoke → JW → 조준점 프리팹 만들기.
/// <para>
/// - Assets/_JW/Effects/Target의 target_reticle.png(빨강), target_reticle_marked.png(하늘색) 시트(가로 한 줄, 48×48 칸 8장)를 가져오기 설정(Sprite Multiple, 16 PPU, Point, 압축 없음)하고 자른다.<br/>
/// - Assets/_JW/Resources/TargetReticle.prefab, TargetReticleMarked.prefab (SpriteRenderer + SpriteFrameLoop 반복, Sorting Layer WorldUI). SlotTargetMarker가 찾는다.<br/>
/// - 다시 눌러도 같은 결과로 덮어쓴다.
/// </para>
/// </summary>
public static class TargetReticleBuilder
{
	const string SheetDir = "Assets/_JW/Effects/Target/";
	const string PrefabDir = "Assets/_JW/Resources/";
	const float FramesPerSecond = 10f;

	static readonly (string sheet, string prefab)[] Reticles =
	{
		("target_reticle", "TargetReticle"),
		("target_reticle_marked", "TargetReticleMarked"),
	};

	[MenuItem("Invoke/JW/조준점 프리팹 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources")) AssetDatabase.CreateFolder("Assets/_JW", "Resources");
		int made = 0;
		foreach (var r in Reticles)
		{
			string path = SheetDir + r.sheet + ".png";
			if (AssetImporter.GetAtPath(path) == null)
			{
				Debug.LogError($"[TargetReticleBuilder] {path}가 없음");
				continue;
			}
			Sprite[] frames = SliceSheet(path);
			if (frames.Length == 0) continue;
			MakePrefab(r.prefab, frames);
			made++;
		}
		AssetDatabase.SaveAssets();
		Debug.Log($"[TargetReticleBuilder] 조준점 프리팹 {made}개를 {PrefabDir}에 만들었음");
	}

	// 정사각 칸(시트 높이)으로 가로 한 줄 자른다
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

	static void MakePrefab(string name, Sprite[] frames)
	{
		var obj = new GameObject(name);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		sr.sortingLayerName = "WorldUI";

		var loop = obj.AddComponent<SpriteFrameLoop>();
		var so = new SerializedObject(loop);
		SerializedProperty list = so.FindProperty("frames");
		list.arraySize = frames.Length;
		for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
		so.FindProperty("framesPerSecond").floatValue = FramesPerSecond;
		so.FindProperty("playOnceCount").intValue = 0;
		so.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(obj, PrefabDir + name + ".prefab");
		Object.DestroyImmediate(obj);
	}
}
