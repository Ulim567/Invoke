using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 아이콘 (#444). 스킬 ID로 24×24 픽셀 아이콘을 찾는다. HUD 슬롯, 스킬 트리 노드가 쓴다.
/// <para>
/// - 그림은 Assets/_JW/Resources/SkillIcons/{스킬 ID}.png (예: c.thrust.png). 가져오기 설정은 SkillIconImporter가 자동으로 한다.<br/>
/// - 계열마다 판 색: 찌르기 청록, 던지기 주황, 휘두르기 빨강, 소환 보라, 이동 초록.<br/>
/// - 아이콘이 없는 ID(문법 노드, 테스트 함수 등)는 null.
/// </para>
/// </summary>
public static class SkillIcons
{
	const string Folder = "SkillIcons/";
	static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

	/// <summary>스킬 ID의 아이콘. 없으면 null. 한 번 찾은 것은 기억한다.</summary>
	public static Sprite Get(string skillId)
	{
		if (string.IsNullOrEmpty(skillId)) return null;
		if (!cache.TryGetValue(skillId, out Sprite sprite))
		{
			sprite = Resources.Load<Sprite>(Folder + skillId);
			cache[skillId] = sprite;
		}
		return sprite;
	}

	/// <summary>스킬 ID에 아이콘이 있으면 true.</summary>
	public static bool Has(string skillId) => Get(skillId) != null;
}
