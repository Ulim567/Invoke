#if UNITY_SERVER || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;

/// <summary>회원가입 결과.</summary>
public enum SignUpResult
{
	Success,
	LoginIdTaken,
	NicknameTaken,
}

/// <summary>캐릭터 만들기 결과.</summary>
public enum CreateCharacterResult
{
	Success,
	UnknownLanguage,
	LanguageTaken,
}

/// <summary>
/// 계정과 캐릭터를 DB(SQLite)에 저장하고 읽는 곳. 서버에서만 쓴다.
/// <para>
/// DB를 읽고 쓰는 코드는 이 클래스와 ProgressStore(진행도)에만 둔다. 나중에 MySQL 같은 다른 DB로 옮기면 두 클래스만 고친다.<br/>
/// 입력 규칙(아이디 길이 등)은 검사하지 않는다. 규칙 검사는 클라이언트의 요청을 받는 쪽이 이 클래스를 부르기 전에 한다.<br/>
/// DB 코드가 클라이언트 빌드에 들어가지 않도록, 계정 DB 관련 파일은 서버 빌드와 에디터에서만 컴파일한다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 서버가 켜질 때 new AccountStore(AccountStore.DefaultPath)로 한 번 열고, 서버가 꺼질 때 Dispose한다.<br/>
/// - 처음 열면 DB 파일과 테이블을 만든다. 이미 있으면 그대로 쓴다.
/// </para>
/// </summary>
public class AccountStore : IDisposable
{
	public const string FileName = "Accounts.db";

	readonly SQLiteConnection db;

	/// <summary>같은 DB 파일의 진행도 테이블을 다루는 곳. 이 클래스와 같은 SQLite 연결을 쓴다.</summary>
	public ProgressStore Progress { get; }

	// 계정 하나. DB의 계정 테이블(Accounts)의 한 줄.
	// 비밀번호 해시가 들어 있으므로 이 클래스 밖으로 내보내지 않는다
	[Table("Accounts")]
	class AccountRow
	{
		[PrimaryKey]
		public string Key { get; set; }

		// 소문자로 바꿔 저장한다. 아이디는 대소문자를 구분하지 않기 때문이다
		[Unique, Collation("NOCASE")]
		public string LoginId { get; set; }

		// 다른 플레이어에게 보이는 이름. 계정마다 하나이고, 그 계정의 모든 언어 캐릭터가 같이 쓴다.
		// 입력한 그대로 저장하고, 겹치는지는 대소문자를 무시하고 본다
		[Unique, Collation("NOCASE")]
		public string Nickname { get; set; }

		public byte[] PasswordHash { get; set; }
		public byte[] PasswordSalt { get; set; }
		// 계정마다 저장해 두어야, 나중에 PasswordHasher.DefaultIterations를 올려도 예전 계정이 로그인된다
		public int HashIterations { get; set; }
		public DateTime CreatedAt { get; set; }
	}

	/// <summary>
	/// 실제 계정 DB 파일 위치. Application.persistentDataPath 안이다.
	/// Linux 서버에서는 ~/.config/unity3d/회사 이름/제품 이름 아래라서, 서버 빌드 폴더를 덮어써도 DB가 지워지지 않는다.
	/// </summary>
	public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

	/// <summary>path의 DB 파일을 연다. 파일이나 테이블이 없으면 만든다.</summary>
	public AccountStore(string path)
	{
		db = new SQLiteConnection(path);
		db.CreateTable<AccountRow>();
		db.CreateTable<CharacterRecord>();
		Progress = new ProgressStore(db);
	}

	/// <summary>
	/// 새 계정을 저장한다. 성공하면 accountKey에 새 계정의 key를 넣는다. 실패하면 null.<br/>
	/// 실패: 같은 아이디가 있음(LoginIdTaken), 서버 전체에 같은 닉네임이 있음(NicknameTaken). 둘 다 대소문자 무시.
	/// </summary>
	public SignUpResult SignUp(string loginId, string password, string nickname, out string accountKey)
	{
		accountKey = null;
		string normalizedId = NormalizeLoginId(loginId);

		// 아래 두 검사는 결과를 이름으로 알려 주기 위함이다. 겹치는 줄이 들어가지 않도록 막는 것은 테이블의 unique index다
		if (FindAccount(normalizedId) != null) return SignUpResult.LoginIdTaken;
		if (db.Table<AccountRow>().Where(a => a.Nickname == nickname).Count() > 0) return SignUpResult.NicknameTaken;

		byte[] salt = PasswordHasher.CreateSalt();
		var row = new AccountRow
		{
			Key = Guid.NewGuid().ToString(),
			LoginId = normalizedId,
			Nickname = nickname,
			PasswordSalt = salt,
			HashIterations = PasswordHasher.DefaultIterations,
			PasswordHash = PasswordHasher.Hash(password, salt, PasswordHasher.DefaultIterations),
			CreatedAt = DateTime.UtcNow,
		};
		db.Insert(row);

		accountKey = row.Key;
		return SignUpResult.Success;
	}

	/// <summary>
	/// 아이디와 비밀번호가 맞으면 true를 돌려주고 accountKey와 nickname에 그 계정의 key와 닉네임을 넣는다.<br/>
	/// 아이디가 없을 때와 비밀번호가 틀렸을 때를 구분하지 않는다. 구분해서 알려 주면 남이 어떤 아이디가 있는지 알아낼 수 있기 때문이다.
	/// </summary>
	public bool TryLogin(string loginId, string password, out string accountKey, out string nickname)
	{
		accountKey = null;
		nickname = null;

		AccountRow row = FindAccount(NormalizeLoginId(loginId));
		if (row == null) return false;
		if (!PasswordHasher.Verify(password, row.PasswordSalt, row.HashIterations, row.PasswordHash)) return false;

		accountKey = row.Key;
		nickname = row.Nickname;
		return true;
	}

	/// <summary>그 계정의 캐릭터들. 없으면 빈 목록.</summary>
	public List<CharacterRecord> GetCharacters(string accountKey)
	{
		return db.Table<CharacterRecord>().Where(c => c.AccountKey == accountKey).ToList();
	}

	/// <summary>
	/// 그 계정에 새 캐릭터를 저장한다. 진행도 처음 값(ProgressStore.CreateInitial)도 함께 넣는다. 성공하면 created에 새 캐릭터를 넣는다. 실패하면 null.<br/>
	/// 실패: 모르는 언어(UnknownLanguage), 그 계정에 이미 같은 언어 캐릭터가 있음(LanguageTaken).
	/// </summary>
	public CreateCharacterResult CreateCharacter(string accountKey, string language, out CharacterRecord created)
	{
		created = null;

		if (!CharacterLanguages.IsKnown(language)) return CreateCharacterResult.UnknownLanguage;

		// 결과를 이름으로 알려 주기 위한 검사다. 겹치는 줄이 들어가지 않도록 막는 것은 테이블의 unique index다
		if (db.Table<CharacterRecord>().Where(c => c.AccountKey == accountKey && c.Language == language).Count() > 0)
			return CreateCharacterResult.LanguageTaken;

		var record = new CharacterRecord
		{
			Key = Guid.NewGuid().ToString(),
			AccountKey = accountKey,
			Language = language,
			CreatedAt = DateTime.UtcNow,
		};
		// 캐릭터는 있는데 진행도 row가 없는 상태가 생기지 않도록, 진행도 처음 row와 한 트랜잭션으로 넣는다
		db.RunInTransaction(() =>
		{
			db.Insert(record);
			Progress.CreateInitial(record.Key);
		});

		created = record;
		return CreateCharacterResult.Success;
	}

	/// <summary>DB 파일을 닫는다. 서버가 꺼질 때 부른다.</summary>
	public void Dispose()
	{
		db.Close();
	}

	AccountRow FindAccount(string normalizedId)
	{
		return db.Table<AccountRow>().Where(a => a.LoginId == normalizedId).FirstOrDefault();
	}

	/// <summary>아이디를 저장하는 모양(소문자)으로 바꾼다. 아이디는 대소문자를 구분하지 않기 때문이다.</summary>
	public static string NormalizeLoginId(string loginId) => loginId.ToLowerInvariant();
}
#endif
