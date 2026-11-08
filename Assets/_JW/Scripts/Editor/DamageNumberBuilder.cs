using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// [에디터 전용] 픽셀 데미지 숫자 프리팹을 만든다 (#442). 메뉴 Invoke → JW → 데미지 숫자 프리팹 만들기.
/// <para>
/// - Assets/_JW/Effects/Damage의 damage_digits.png(일반), damage_digits_crit.png(표식 맞힘) 시트(가로 한 줄, 9×10px 칸 10개, 0~9)를 가져오기 설정(Sprite Multiple, 16 PPU, Point, 압축 없음)하고 자른다.<br/>
/// - Assets/_JW/Resources/DamageNumber.prefab (DamageNumber, 임혜원 코드)에 두 숫자 묶음을 넣는다. DamageNumberView가 Number Prefab이 비어 있으면 이 프리팹을 쓴다.<br/>
/// - 다시 눌러도 같은 결과로 덮어쓴다.
/// </para>
/// </summary>
public static class DamageNumberBuilder
{
	const string SheetDir = "Assets/_JW/Effects/Damage/";
	const string PrefabPath = "Assets/_JW/Resources/DamageNumber.prefab";
	const int CellWidth = 9;

	[MenuItem("Invoke/JW/데미지 숫자 프리팹 만들기")]
	public static void Build()
	{
		Sprite[] normal = SliceSheet(SheetDir + "damage_digits.png");
		Sprite[] critical = SliceSheet(SheetDir + "damage_digits_crit.png");
		if (normal == null || critical == null) return;
		if (!AssetDatabase.IsValidFolder("Assets/_JW/Resources")) AssetDatabase.CreateFolder("Assets/_JW", "Resources");

		var obj = new GameObject("DamageNumber");
		var number = obj.AddComponent<DamageNumber>();
		var so = new SerializedObject(number);
		Fill(so.FindProperty("digits"), normal);
		Fill(so.FindProperty("criticalDigits"), critical);
		so.ApplyModifiedPropertiesWithoutUndo();

		PrefabUtility.SaveAsPrefabAsset(obj, PrefabPath);
		Object.DestroyImmediate(obj);
		AssetDatabase.SaveAssets();
		Debug.Log($"[DamageNumberBuilder] {PrefabPath}를 만들었음");
	}

	static void Fill(SerializedProperty list, Sprite[] sprites)
	{
		list.arraySize = sprites.Length;
		for (int i = 0; i < sprites.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
	}

	// 9px 칸으로 가로 한 줄 자른다 (0~9)
	static Sprite[] SliceSheet(string path)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			Debug.LogError($"[DamageNumberBuilder] {path}가 없음");
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
		int count = texture.width / CellWidth;
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
				rect = new Rect(i * CellWidth, 0, CellWidth, texture.height),
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
}
