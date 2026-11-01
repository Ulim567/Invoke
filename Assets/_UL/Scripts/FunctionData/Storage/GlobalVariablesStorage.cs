using System.IO;
using UnityEngine;

/// <summary>
/// 전역 변수 파일을 이 컴퓨터의 파일로 저장하고 불러온다. 서버 저장이 생기기 전까지 쓰는 임시 저장소.
/// <para>
/// - 저장 위치: Application.persistentDataPath/GlobalVariables.json. 이 컴퓨터에 파일 하나다.<br/>
/// - 형식: FunctionDataJson.ToJson 그대로.<br/>
/// - 내용은 검사하지 않는다. 저장하기 전에 GlobalVariablesConverter.TryConvert로 검사한다.<br/>
/// - 서버 저장은 「만든 함수를 서버로 보내 검사·저장하기」 이슈에서 이 클래스를 바꾸거나 대신한다.
/// </para>
/// </summary>
public static class GlobalVariablesStorage
{
	static string FilePath => Path.Combine(Application.persistentDataPath, "GlobalVariables.json");

	/// <summary>
	/// 전역 변수 파일을 저장한다. 이미 있으면 덮어쓴다.
	/// </summary>
	public static void Save(GlobalVariablesData globals)
	{
		File.WriteAllText(FilePath, FunctionDataJson.ToJson(globals));
	}

	/// <summary>
	/// 저장한 전역 변수 파일을 불러온다. 파일이 없거나 읽을 수 없으면 선언이 없는 빈 파일을 돌려준다.
	/// </summary>
	// null 대신 빈 파일을 돌려주는 이유: 전역 변수를 한 번도 저장하지 않은 캐릭터도 같은 방법으로 초기화하기 위함
	public static GlobalVariablesData Load()
	{
		if (!File.Exists(FilePath)) return new GlobalVariablesData();
		if (FunctionDataJson.TryGlobalVariablesFromJson(File.ReadAllText(FilePath), out GlobalVariablesData globals)) return globals;

		Debug.LogWarning($"[GlobalVariablesStorage] 읽을 수 없는 전역 변수 파일: {FilePath}");
		return new GlobalVariablesData();
	}

	/// <summary>
	/// 전역 변수 파일이 저장되는 경로. 확인용으로 콘솔에 찍을 때 쓴다.
	/// </summary>
	public static string PathOfFile => FilePath;
}
