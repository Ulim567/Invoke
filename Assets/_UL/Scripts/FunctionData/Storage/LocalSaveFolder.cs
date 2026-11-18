using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// 임시 계정(에디터 Host의 로그인 건너뛰기)·오프라인 테스트가 이 컴퓨터에 저장하는 폴더를 캐릭터 언어마다 나눈다 (#570).
/// <para>
/// - 폴더: Application.persistentDataPath/Local/{언어} (예: Local/C, Local/Python).<br/>
/// - 함수(FunctionStorage), 전역 변수(GlobalVariablesStorage)는 Current, 슬롯 장착 내용(SlotLoadout)은 그 캐릭터의 언어로 PathFor를 쓴다.<br/>
/// - 실제 계정은 서버 DB에 저장하므로 이 폴더를 쓰지 않는다.
/// </para>
/// </summary>
// 예전에는 세 파일이 컴퓨터에 하나씩이라, Host에서 캐릭터를 바꾸면 C·Python이 같은 슬롯·함수·전역 변수를 썼다
public static class LocalSaveFolder
{
	/// <summary>
	/// 이 컴퓨터의 내 캐릭터 언어 폴더. 아직 들어가지 않았거나 오프라인 테스트면 C 폴더.<br/>
	/// 내 캐릭터가 바뀌면(캐릭터 화면에서 다른 언어로 들어감) 다음 호출부터 그 언어 폴더가 된다.
	/// </summary>
	public static string Current => FolderOf(PlayerCharacter.LocalLanguage);

	/// <summary>그 언어 캐릭터의 폴더 안 파일 경로. 예: PathFor("Python", "SlotLoadout.json")</summary>
	public static string PathFor(string language, string fileName) => Path.Combine(FolderOf(language), fileName);

	/// <summary>그 언어 캐릭터의 폴더. 없으면 만든다.</summary>
	public static string FolderOf(string language)
	{
		// 모르는 언어(빈 값 등)는 C로 본다. PlayerCharacter.LanguageOf와 같은 규칙
		string folder = Path.Combine(Application.persistentDataPath, "Local", string.IsNullOrEmpty(language) ? CharacterLanguages.C : language);
		// HUD처럼 자주 읽는 곳이 있으므로, 폴더를 만드는 일은 처음 한 번만 한다 (저장하는 쪽이 폴더를 따로 만들지 않아도 되게)
		if (created.Add(folder)) Directory.CreateDirectory(folder);
		return folder;
	}

	static readonly HashSet<string> created = new HashSet<string>();

	// Play 모드에 들어갈 때 도메인 다시 불러오기를 꺼 두었으므로 static 값을 직접 비운다 (폴더를 지운 뒤 다시 Play해도 만들게)
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => created.Clear();
}
