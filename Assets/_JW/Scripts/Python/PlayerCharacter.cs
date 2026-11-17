using Mirror;
using UnityEngine;

/// <summary>
/// 이 플레이어 프리팹이 어느 언어의 캐릭터인지 적어 둔 컴포넌트. 언어마다 플레이어 프리팹이 따로라서 프리팹에 값을 적어 둔다 (보낼 필요 없음).
/// <para>
/// - 파이썬 플레이어 프리팹에 붙이고 Language를 Python으로 둔다. 이 컴포넌트가 없는 프리팹(Player)은 C 캐릭터로 본다.<br/>
/// - 화면(블록 편집기의 스킬 목록, 장착 화면, 스킬 트리)이 LocalLanguage, SkillPrefixOf로 내 캐릭터가 쓸 수 있는 스킬을 가린다.
/// </para>
/// </summary>
public class PlayerCharacter : MonoBehaviour
{
	[Tooltip("이 프리팹의 캐릭터 언어 (CharacterLanguages). 예: Python")]
	[SerializeField] string language = CharacterLanguages.C;

	/// <summary>이 캐릭터의 언어 (CharacterLanguages).</summary>
	public string Language => language;

	/// <summary>obj의 캐릭터 언어. PlayerCharacter가 없으면 C.</summary>
	public static string LanguageOf(GameObject obj) =>
		obj != null && obj.TryGetComponent(out PlayerCharacter character) ? character.Language : CharacterLanguages.C;

	/// <summary>이 컴퓨터의 내 캐릭터 언어. 아직 들어가지 않았으면 C.</summary>
	public static string LocalLanguage => LanguageOf(NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null);

	/// <summary>그 언어 캐릭터의 고유 스킬 ID 앞부분. C는 "c.", Python은 "py.". 공통(common.) 스킬은 모든 캐릭터가 쓴다.</summary>
	public static string SkillPrefixOf(string language) => language == CharacterLanguages.Python ? "py." : "c.";

	/// <summary>
	/// 그 언어 캐릭터가 쓸 수 있는 스킬 ID면 true: 자기 언어의 고유 스킬이거나 공통(common.) 스킬. 다른 언어의 고유 스킬은 false.
	/// 스킬이 아닌 함수(input., game. 등)는 true.
	/// </summary>
	public static bool CanUse(string language, string functionId)
	{
		if (string.IsNullOrEmpty(functionId)) return false;
		if (functionId.StartsWith("c.") || functionId.StartsWith("py.")) return functionId.StartsWith(SkillPrefixOf(language));
		return true;
	}
}
