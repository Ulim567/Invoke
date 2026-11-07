using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// [에디터 전용] 상태 이상 그림 효과 프리팹 5개를 만든다 (#363). 메뉴 Invoke → JW → 상태 이상 그림 프리팹 만들기.
/// <para>
/// - Assets/_JW/Effects/Status/status_*.png 시트를 가져오기 설정(Sprite Multiple, 16 PPU, Point, 압축 없음)하고 칸 크기로 자른다.<br/>
/// - 프리팹(SpriteRenderer + SpriteFrameLoop)을 Assets/_JW/Resources/StatusEffects/{Bleed|Mark|Stun|Blind|Slow}.prefab으로 저장한다. StatusEffectView가 칸이 비면 이걸 쓴다.<br/>
/// - 재질과 정렬(Sorting Layer Effect)은 ThrustFlash 프리팹을 따른다. 다시 눌러도 같은 결과로 덮어쓴다 (프리팹에서 바꾼 값도 되돌아가니 주의).
/// </para>
/// </summary>
public static class StatusEffectPrefabBuilder
{
	const string SheetDir = "Assets/_JW/Effects/Status/";
	const string PrefabDir = "Assets/_JW/Resources/StatusEffects/";
	const string ThrustFlashPath = "Assets/_JW/ThrustFlash.prefab";

	// 프리팹 이름(= StatusType 이름), 시트, 칸 크기, 기준점 높이(칸 아래에서 몇 px, -1이면 가운데), 초당 프레임, 한 번만 보여 줄 앞 장수
	static readonly (string name, string sheet, int cell, int pivotPx, float fps, int playOnce)[] Effects =
	{
		("Bleed", "status_bleed", 32, -1, 10f, 0),
		("Mark", "status_mark", 32, -1, 10f, 0),
		("Stun", "status_stun", 32, -1, 12f, 0),
		("Blind", "status_blind", 32, -1, 12f, 0),
		// 얼음은 그림 아래 3px가 비어 있어서, 기준점을 얼음 밑동(아래에서 3px)에 둬 발밑에 딱 붙게 한다
		("Slow", "status_slow", 48, 3, 12f, 9),
	};

	[MenuItem("Invoke/JW/상태 이상 그림 프리팹 만들기")]
	public static void Build()
	{
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources")) AssetDatabase.CreateFolder("Assets/_JW", "Resources");
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources/StatusEffects")) AssetDatabase.CreateFolder("Assets/_JW/Resources", "StatusEffects");

		int made = 0;
		foreach (var e in Effects)
		{
			Sprite[] frames = SliceSheet(SheetDir + e.sheet + ".png", e.cell, e.pivotPx);
			if (frames == null) continue;
			MakePrefab(e.name, frames, e.fps, e.playOnce);
			made++;
		}
		AssetDatabase.SaveAssets();
		Debug.Log($"[StatusEffectPrefabBuilder] 프리팹 {made}개를 {PrefabDir}에 만들었음");
	}

	// 시트를 가져오기 설정하고 cell × cell 칸으로 가로 한 줄 자른다. 잘린 스프라이트를 순서대로 돌려준다
	static Sprite[] SliceSheet(string path, int cell, int pivotPx)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			Debug.LogError($"[StatusEffectPrefabBuilder] {path}가 없음");
			return null;
		}
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Multiple;
		importer.spritePixelsPerUnit = 16;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.mipmapEnabled = false;
		importer.alphaIsTransparency = true;
		importer.SaveAndReimport();

		var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
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
				alignment = pivotPx < 0 ? SpriteAlignment.Center : SpriteAlignment.Custom,
				pivot = pivotPx < 0 ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, (float)pivotPx / cell),
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

	static void MakePrefab(string name, Sprite[] frames, float fps, int playOnce)
	{
		var obj = new GameObject(name);
		var sr = obj.AddComponent<SpriteRenderer>();
		sr.sprite = frames[0];
		GameObject thrust = AssetDatabase.LoadAssetAtPath<GameObject>(ThrustFlashPath);
		SpriteRenderer thrustSr = thrust != null ? thrust.GetComponentInChildren<SpriteRenderer>() : null;
		if (thrustSr != null) sr.sharedMaterial = thrustSr.sharedMaterial;
		sr.sortingLayerName = "Effect";
		sr.sortingOrder = 5;

		var loop = obj.AddComponent<SpriteFrameLoop>();
		var so = new SerializedObject(loop);
		SerializedProperty list = so.FindProperty("frames");
		list.arraySize = frames.Length;
		for (int i = 0; i < frames.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
		so.FindProperty("framesPerSecond").floatValue = fps;
		so.FindProperty("playOnceCount").intValue = playOnce;
		so.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(obj, PrefabDir + name + ".prefab");
		Object.DestroyImmediate(obj);
	}
}
