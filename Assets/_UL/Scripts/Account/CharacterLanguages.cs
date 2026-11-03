using System;

/// <summary>
/// 캐릭터의 언어 이름 상수. 캐릭터는 계정 하나에 언어마다 하나씩 만들 수 있다.
/// <para>
/// 언어 이름은 DB의 캐릭터 테이블에 그대로 저장되므로, 한 번 정한 이름은 바꾸거나 지우지 않는다.
/// 새 언어 캐릭터가 생기면 상수를 더하고 All에도 넣는다.
/// </para>
/// </summary>
public static class CharacterLanguages
{
	public const string C = "C";

	/// <summary>지금 만들 수 있는 모든 언어. 캐릭터 화면이 이 순서대로 칸을 보여 준다.</summary>
	public static readonly string[] All = { C };

	/// <summary>All에 있는 언어 이름이면 true.</summary>
	public static bool IsKnown(string language) => Array.IndexOf(All, language) >= 0;
}
