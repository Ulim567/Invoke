using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// 만든 함수를 이 컴퓨터의 파일로 저장하고 불러온다. 서버 저장이 생기기 전까지 쓰는 임시 저장소.
/// <para>
/// - 저장 위치: Application.persistentDataPath/Functions/{GUID}.json (맥: ~/Library/Application Support/회사이름/Invoke/Functions)<br/>
/// - 형식: FunctionDataJson.ToJson 그대로 (function-data.md 「저장 형식」). 따로 바꾸지 않는다.<br/>
/// - 서버 저장은 「만든 함수를 서버로 보내 검사·저장하기」(이우림)에서 이 클래스를 바꾸거나 대신한다.
/// </para>
/// </summary>
public static class FunctionStorage
{
	static string Folder => Path.Combine(Application.persistentDataPath, "Functions");

	/// <summary>
	/// 함수 하나를 저장한다. 같은 GUID의 함수가 있으면 덮어쓴다.
	/// </summary>
	public static void Save(FunctionData function)
	{
		Directory.CreateDirectory(Folder);
		File.WriteAllText(PathOf(function.Guid), FunctionDataJson.ToJson(function));
	}

	/// <summary>
	/// GUID로 함수 하나를 불러온다. 없거나 읽을 수 없으면 null.
	/// </summary>
	public static FunctionData Load(string guid)
	{
		string path = PathOf(guid);
		if (path == null || !File.Exists(path)) return null;
		return FunctionDataJson.TryFromJson(File.ReadAllText(path), out FunctionData function) ? function : null;
	}

	/// <summary>
	/// 저장한 함수를 모두 이름 순서로 불러온다. 읽을 수 없는 파일은 건너뛴다.
	/// </summary>
	public static List<FunctionData> LoadAll()
	{
		var functions = new List<FunctionData>();
		if (!Directory.Exists(Folder)) return functions;

		foreach (string path in Directory.GetFiles(Folder, "*.json"))
		{
			if (FunctionDataJson.TryFromJson(File.ReadAllText(path), out FunctionData function)) functions.Add(function);
			else Debug.LogWarning($"[FunctionStorage] 읽을 수 없는 함수 파일: {path}");
		}
		return functions.OrderBy(f => f.Name).ToList();
	}

	/// <summary>
	/// 함수가 저장되는 파일 경로. 확인용으로 콘솔에 찍을 때 쓴다.
	/// </summary>
	public static string PathOf(FunctionData function) => PathOf(function.Guid);

	// 파일 이름은 GUID로만 만든다. GUID 모양이 아니면 다른 폴더의 파일을 가리키지 못하게 null
	static string PathOf(string guid) =>
		System.Guid.TryParse(guid, out _) ? Path.Combine(Folder, guid + ".json") : null;
}
