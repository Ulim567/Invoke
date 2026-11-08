using UnityEditor;
using UnityEngine;

/// <summary>
/// [에디터 전용] Resources/SkillIcons 안의 그림을 넣으면 스킬 아이콘 가져오기 설정을 자동으로 한다 (#444).
/// Sprite(Single), 16 PPU, Point, 압축 없음, 밉맵 없음. 픽셀이 뭉개지지 않게.
/// </summary>
public class SkillIconImporter : AssetPostprocessor
{
	void OnPreprocessTexture()
	{
		if (!assetPath.Replace('\\', '/').Contains("/Resources/SkillIcons/")) return;
		var importer = (TextureImporter)assetImporter;
		importer.textureType = TextureImporterType.Sprite;
		importer.spriteImportMode = SpriteImportMode.Single;
		importer.spritePixelsPerUnit = 16;
		importer.filterMode = FilterMode.Point;
		importer.textureCompression = TextureImporterCompression.Uncompressed;
		importer.mipmapEnabled = false;
		importer.alphaIsTransparency = true;
	}
}
