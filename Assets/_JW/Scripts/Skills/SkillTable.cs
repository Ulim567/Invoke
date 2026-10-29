using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 수치 표. 스킬마다 한 줄(SkillData)이다. 수치를 코드가 아니라 이 에셋에서 고치기 위함 (#67).
/// <para>
/// - 에셋: Assets/_JW/Resources/SkillTable.asset (Resources에 두어 어디서든 SkillTable.TryGet으로 읽는다)<br/>
/// - 스킬 클래스(DataDrivenSkill)는 값을 쓸 때마다 이 표를 읽으므로, Inspector에서 바꾸면 다음 시전부터 바로 반영된다.<br/>
/// - 한 ID에 한 줄. 같은 ID가 두 줄이면 위의 줄을 쓰고 경고를 남긴다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "SkillTable", menuName = "Invoke/Skill Table")]
public class SkillTable : ScriptableObject
{
	const string ResourcePath = "SkillTable";

	[SerializeField] List<SkillData> skills = new List<SkillData>();

	static SkillTable loaded;
	static Dictionary<string, SkillData> byId;
	// 표에 없는 ID를 매번 오류로 찍으면 콘솔이 넘치므로 한 번만 알린다
	static readonly HashSet<string> reportedMissing = new HashSet<string>();

	/// <summary>표의 모든 줄.</summary>
	public IReadOnlyList<SkillData> Skills => skills;

	/// <summary>Resources에서 불러온 표. 에셋이 없으면 null.</summary>
	public static SkillTable Instance
	{
		get
		{
			if (loaded == null) loaded = Resources.Load<SkillTable>(ResourcePath);
			return loaded;
		}
	}

	/// <summary>
	/// 고정 ID의 수치를 찾는다. 표에 없으면 false를 돌려주고 콘솔에 한 번 알린다.
	/// </summary>
	public static bool TryGet(string id, out SkillData data)
	{
		data = null;
		if (id == null) return false;

		if (byId == null) BuildIndex();
		if (byId.TryGetValue(id, out data)) return true;

		if (reportedMissing.Add(id)) Debug.LogError($"[SkillTable] \"{id}\"의 수치가 표(SkillTable)에 없음");
		return false;
	}

	static void BuildIndex()
	{
		// 표를 먼저 불러온다. 에디터에서는 에셋을 처음 불러올 때 OnValidate가 불려 byId를 비우기 때문
		SkillTable table = Instance;
		Dictionary<string, SkillData> index = new Dictionary<string, SkillData>();
		if (table == null)
		{
			Debug.LogError($"[SkillTable] Resources/{ResourcePath} 에셋이 없음");
			byId = index;
			return;
		}

		foreach (SkillData data in table.skills)
		{
			if (data == null || string.IsNullOrEmpty(data.Id)) continue;
			if (index.ContainsKey(data.Id))
			{
				Debug.LogWarning($"[SkillTable] \"{data.Id}\"가 두 줄 있음. 위의 줄을 씀");
				continue;
			}
			index.Add(data.Id, data);
		}
		byId = index;
	}

	// Inspector에서 줄을 더하거나 ID를 바꾸면 찾기 목록을 다시 만든다
	void OnValidate()
	{
		byId = null;
		reportedMissing.Clear();
	}
}
