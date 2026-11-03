using System;

/// <summary>
/// 캐릭터의 언어 이름 상수. 캐릭터는 계정 하나에 언어마다 하나씩 만들 수 있다.
/// <para>
/// 언어 이름은 DB의 캐릭터 테이블에 그대로 저장되므로, 한 번 정한 이름은 바꾸거나 지우지 않는다.
/// 새 언어 캐릭터를 열 때는 All에 넣는다 (Shown에만 있는 언어는 화면에 「준비 중」으로 보인다).
/// </para>
/// </summary>
public static class CharacterLanguages
{
	public const string C = "C";
	public const string Java = "Java";
	public const string Python = "Python";

	/// <summary>지금 만들 수 있는 모든 언어. 서버는 이 목록에 없는 언어의 캐릭터를 만들지 않는다.</summary>
	public static readonly string[] All = { C };

	/// <summary>캐릭터 화면에 보여 줄 언어. 화면이 이 순서대로 카드를 그린다. All에 없는 언어는 잠긴 언어(준비 중).</summary>
	public static readonly string[] Shown = { C, Java, Python };

	/// <summary>All에 있는 언어 이름이면 true.</summary>
	public static bool IsKnown(string language) => Array.IndexOf(All, language) >= 0;
}
