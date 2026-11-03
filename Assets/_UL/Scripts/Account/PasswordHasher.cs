using System.Security.Cryptography;

/// <summary>
/// 비밀번호를 DB에 저장할 해시로 바꾸고, 로그인 때 입력한 비밀번호가 맞는지 비교한다. 서버에서만 쓴다.
/// <para>
/// 비밀번호는 원래 글자로도, 되돌릴 수 있는 암호문으로도 저장하지 않는다.
/// DB 파일이 유출되어도 원래 비밀번호를 알 수 없게 하기 위함이다.<br/>
/// 계정마다 무작위 salt를 섞어서, 같은 비밀번호를 쓰는 두 계정의 해시도 서로 다르게 한다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 회원가입: CreateSalt()로 salt를 만들고, Hash(비밀번호, salt, DefaultIterations)의 결과와 salt, 반복 횟수를 함께 저장한다.<br/>
/// - 로그인: 저장해 둔 해시, salt, 반복 횟수로 Verify를 부른다.
/// </para>
/// </summary>
public static class PasswordHasher
{
	const int SaltSize = 16;
	const int HashSize = 32;

	/// <summary>
	/// 새 계정에 쓰는 PBKDF2 반복 횟수.
	/// 해시는 서버의 메인 스레드에서 계산하므로, 로그인 한 번에 게임이 멈칫하지 않을 만큼만 둔다.
	/// 계정마다 반복 횟수를 함께 저장하므로, 나중에 이 값을 올려도 예전 계정은 저장된 횟수로 그대로 로그인된다.
	/// </summary>
	public const int DefaultIterations = 10000;

	/// <summary>계정 하나에 쓸 무작위 salt를 만든다.</summary>
	public static byte[] CreateSalt()
	{
		var salt = new byte[SaltSize];
		using (var random = RandomNumberGenerator.Create())
			random.GetBytes(salt);
		return salt;
	}

	/// <summary>비밀번호와 salt로 PBKDF2(SHA-256) 해시를 만든다.</summary>
	public static byte[] Hash(string password, byte[] salt, int iterations)
	{
		using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
			return pbkdf2.GetBytes(HashSize);
	}

	/// <summary>입력한 비밀번호가 저장해 둔 해시와 맞으면 true.</summary>
	public static bool Verify(string password, byte[] salt, int iterations, byte[] expectedHash)
	{
		return FixedTimeEquals(Hash(password, salt, iterations), expectedHash);
	}

	// 처음 다른 바이트에서 바로 멈추면, 응답 시간 차이로 해시를 한 바이트씩 맞혀 볼 수 있다.
	// 그래서 길이가 같으면 끝까지 모두 비교한다
	static bool FixedTimeEquals(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length) return false;

		int diff = 0;
		for (int i = 0; i < a.Length; i++)
			diff |= a[i] ^ b[i];
		return diff == 0;
	}
}
