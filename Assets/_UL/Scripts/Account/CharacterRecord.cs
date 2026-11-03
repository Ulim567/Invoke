using System;
using SQLite;

/// <summary>
/// 캐릭터 하나. DB의 캐릭터 테이블(Characters)의 한 줄이다.
/// AccountStore가 만들고 돌려준다. 다른 스크립트는 값을 읽기만 한다.
/// </summary>
[Table("Characters")]
public class CharacterRecord
{
	/// <summary>캐릭터 key(GUID). 다음 이슈의 진행도는 이 값으로 캐릭터를 가리킨다.</summary>
	[PrimaryKey]
	public string Key { get; set; }

	/// <summary>이 캐릭터를 가진 계정의 key.</summary>
	// 계정 하나에 같은 언어 캐릭터가 두 개 생기지 않도록 (계정, 언어) 짝에 unique index를 건다
	[Indexed(Name = "AccountLanguage", Order = 1, Unique = true)]
	public string AccountKey { get; set; }

	/// <summary>캐릭터의 언어. CharacterLanguages의 값.</summary>
	[Indexed(Name = "AccountLanguage", Order = 2, Unique = true)]
	public string Language { get; set; }

	/// <summary>다른 플레이어에게 보이는 이름. 서버 전체에서 겹치지 않는다(대소문자 무시).</summary>
	[Unique, Collation("NOCASE")]
	public string Nickname { get; set; }

	public DateTime CreatedAt { get; set; }
}
