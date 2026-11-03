using System;
using System.IO;
using Mirror;
using Mirror.Transports.Encryption;
using UnityEngine;

/// <summary>
/// EncryptionTransport의 서버 키 파일과 클라이언트의 서버 키 확인 방식을 코드로 정하는 컴포넌트.
/// <para>
/// 서버<br/>
/// - 키 파일은 persistentDataPath/server-keys.json에 둔다. 빌드 폴더와 레포 밖이다.<br/>
/// - 파일이 없으면 새 키를 만들어 저장하고, 다음부터 그 파일을 읽는다. 서버를 켤 때마다 지문을 로그에 남긴다.
/// </para>
/// <para>
/// 클라이언트<br/>
/// - EncryptionTransport의 지문 목록(Trusted Pub Key Signatures)에 있는 서버만 받는다.<br/>
/// - localhost로 접속할 때는 지문을 보지 않는다. 내 컴퓨터 안의 통신이라 가로챌 수 없고, 컴퓨터마다 키가 달라 목록에 넣을 수 없기 때문이다.
/// </para>
/// <para>
/// 사용법: Base 씬의 NetworkManager 오브젝트(EncryptionTransport가 붙은 오브젝트)에 붙인다.
/// </para>
/// </summary>
public class EncryptionKeySetup : MonoBehaviour
{
	const string KeyFileName = "server-keys.json";

	EncryptionTransport transport;

	/// <summary>서버 키 파일의 위치. 계정 DB(Accounts.db)와 같은 폴더.</summary>
	public static string KeyPath => Path.Combine(Application.persistentDataPath, KeyFileName);

	void Awake()
	{
		// 서버는 NetworkManager.Start에서 켜질 수 있으므로(서버 빌드의 자동 시작), 그보다 먼저인 Awake에서 설정한다
		transport = GetComponent<EncryptionTransport>();
		if (transport == null)
		{
			Debug.LogError("[EncryptionKeySetup] 같은 오브젝트에 EncryptionTransport가 없습니다.");
			return;
		}

		SetUpClient();
#if UNITY_SERVER || UNITY_EDITOR
		// 클라이언트 빌드는 서버를 켜지 않으므로 키 파일을 만들지 않는다
		SetUpServer();
#endif
	}

	void SetUpClient()
	{
		transport.ClientValidateServerPubKey = EncryptionTransport.ValidationMode.Callback;
		transport.OnClientValidateServerPubKey = IsTrustedServer;
	}

	bool IsTrustedServer(PubKeyInfo pubKey)
	{
		if (IsLocalAddress(NetworkManager.singleton.networkAddress)) return true;

		string[] trusted = transport.ClientTrustedPubKeySignatures;
		if (trusted != null && Array.IndexOf(trusted, pubKey.Fingerprint) >= 0) return true;

		Debug.LogError($"[EncryptionKeySetup] 서버 키의 지문이 목록에 없어서 접속하지 않습니다: {pubKey.Fingerprint}");
		return false;
	}

	static bool IsLocalAddress(string address)
	{
		return address == "localhost" || address == "127.0.0.1" || address == "::1";
	}

#if UNITY_SERVER || UNITY_EDITOR
	void SetUpServer()
	{
		string path = KeyPath;
		if (!File.Exists(path))
		{
			EncryptionCredentials.Generate().SaveToFile(path);
			Debug.Log($"[EncryptionKeySetup] 서버 키를 새로 만들었습니다: {path}");
		}

		transport.ServerLoadKeyPairFromFile = true;
		transport.ServerKeypairPath = path;

		// EC2 서버의 지문을 클라이언트의 지문 목록에 넣을 수 있도록 남긴다
		Debug.Log($"[EncryptionKeySetup] 서버 키 지문: {EncryptionCredentials.LoadFromFile(path).PublicKeyFingerprint}");
	}
#endif
}
