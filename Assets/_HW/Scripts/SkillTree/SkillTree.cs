using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 스킬 트리 표. 노드(문법 요소, C 스킬, Python 스킬)와 선행 조건, 해금 재료를 담는다.
/// <para>
/// - 에셋: Assets/_HW/Resources/SkillTree.asset (Resources에 두어 어디서든 SkillTree.TryGet으로 읽는다)<br/>
/// - 처음 만들 때: Project 창 오른쪽 클릭 → Create → Invoke → Skill Tree → 에셋 Inspector ⋮ → 「기획서대로 채우기」<br/>
/// - 고친 뒤: 에셋 Inspector ⋮ → 「검사」로 ID, 선행 조건, SkillTable과 맞는지 확인한다.
/// </para>
/// <para>
/// 이 표는 "무엇을 어떤 순서로 열 수 있는지"만 담는다.
/// 플레이어가 무엇을 해금했는지(저장), 재료를 빼는 일은 「재료 써서 스킬 트리 해금하기」에서 한다.
/// 아래 함수들은 해금한 노드 ID 목록을 받아서 판단만 한다.
/// </para>
/// <para>
/// 캐릭터 언어 (#523): 스킬 노드는 ID 앞부분으로 언어가 정해진다 (c. → C, py. → Python). 문법 노드와 공통 이동(common.)은 모든 언어가 쓴다.<br/>
/// 처음 노드(StartingNodes)와 같이 열리는 노드(AutoUnlockedBy)는 그 언어의 노드만 돌려준다. 해금 목록은 캐릭터마다 따로라서 공통 노드도 캐릭터마다 따로 해금한다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "SkillTree", menuName = "Invoke/Skill Tree")]
public class SkillTree : ScriptableObject
{
	const string ResourcePath = "SkillTree";

	[SerializeField] List<SkillTreeNode> nodes = new List<SkillTreeNode>();

	static SkillTree loaded;
	static Dictionary<string, SkillTreeNode> byId;

	/// <summary>표의 모든 노드. 스킬 트리 화면에서 쓴다.</summary>
	public IReadOnlyList<SkillTreeNode> Nodes => nodes;

	/// <summary>Resources에서 불러온 표. 에셋이 없으면 null.</summary>
	public static SkillTree Instance
	{
		get
		{
			if (loaded == null) loaded = Resources.Load<SkillTree>(ResourcePath);
			return loaded;
		}
	}

	/// <summary>
	/// ID로 노드를 찾는다. 없으면 false.
	/// </summary>
	public static bool TryGet(string id, out SkillTreeNode node)
	{
		node = null;
		if (id == null) return false;
		Dictionary<string, SkillTreeNode> index = byId;
		if (index == null)
		{
			BuildIndex();
			index = byId;
		}
		return index != null && index.TryGetValue(id, out node);
	}

	/// <summary>
	/// 노드의 캐릭터 언어 (CharacterLanguages). c. → C, py. → Python. 문법 노드·공통 이동(common.) 등 모든 언어가 쓰는 노드는 null (#523).
	/// </summary>
	public static string LanguageOfNode(string id)
	{
		if (string.IsNullOrEmpty(id)) return null;
		if (id.StartsWith("c.", System.StringComparison.Ordinal)) return CharacterLanguages.C;
		if (id.StartsWith("py.", System.StringComparison.Ordinal)) return CharacterLanguages.Python;
		return null;
	}

	/// <summary>
	/// 이 언어의 캐릭터가 쓰는 노드면 true. 모든 언어가 쓰는 노드(문법, common.)는 언제나 true. language가 null이면 모든 노드가 true (#523).
	/// </summary>
	public static bool IsFor(string id, string language)
	{
		string nodeLanguage = LanguageOfNode(id);
		return language == null || nodeLanguage == null || nodeLanguage == language;
	}

	/// <summary>
	/// 처음부터 해금된 노드 ID들. 선행 조건이 없고 autoUnlock인 노드 가운데 그 언어의 노드 (#523).
	/// C: 순차 실행, 검 찌르기, 검 던지기. Python: 순차 실행, 매직 미사일, 저주 (#548). 새 캐릭터를 만들 때 해금 목록에 넣는다.
	/// </summary>
	public static List<string> StartingNodes(string language = CharacterLanguages.C)
	{
		var result = new List<string>();
		if (Instance == null) return result;
		foreach (SkillTreeNode node in Instance.nodes)
		{
			if (node.AutoUnlock && node.Prerequisites.Count == 0 && IsFor(node.Id, language)) result.Add(node.Id);
		}
		return result;
	}

	/// <summary>
	/// 선행 조건을 모두 해금해서 지금 해금할 수 있으면 true. 이미 해금했거나 없는 노드면 false.
	/// 재료가 충분한지는 보지 않는다.
	/// </summary>
	public static bool CanUnlock(string id, ICollection<string> unlocked)
	{
		if (!TryGet(id, out SkillTreeNode node) || unlocked.Contains(id)) return false;
		return node.Prerequisites.All(unlocked.Contains);
	}

	/// <summary>
	/// 노드를 해금한 뒤 부른다. 선행 조건이 채워져서 재료 없이 같이 열리는 노드(autoUnlock) ID들을 돌려준다.
	/// 예: 검 소환을 해금하면 해제. 돌려받은 노드도 해금 목록에 넣는다. 그 언어(language)의 노드만 (#523).
	/// </summary>
	// 언어로 거르는 이유: 선행이 없는 다른 언어의 처음 노드(예: C 캐릭터에게 py.orb_create)가 같이 열리지 않게 하기 위함
	public static List<string> AutoUnlockedBy(ICollection<string> unlocked, string language = CharacterLanguages.C)
	{
		var result = new List<string>();
		if (Instance == null) return result;

		var all = new HashSet<string>(unlocked);
		bool added = true;
		// 같이 열린 노드 때문에 또 열리는 노드가 있을 수 있으므로 더 없을 때까지 반복한다
		while (added)
		{
			added = false;
			foreach (SkillTreeNode node in Instance.nodes)
			{
				if (!node.AutoUnlock || all.Contains(node.Id) || !IsFor(node.Id, language)) continue;
				if (!node.Prerequisites.All(all.Contains)) continue;
				all.Add(node.Id);
				result.Add(node.Id);
				added = true;
			}
		}
		return result;
	}

	/// <summary>
	/// 이 종류의 블록을 편집 화면에서 쓸 수 있으면 true.
	/// 어떤 노드의 unlocksBlocks에도 없는 블록 종류는 해금과 상관없이 쓸 수 있다.
	/// </summary>
	public static bool IsBlockUnlocked(BlockKind kind, ICollection<string> unlocked)
	{
		if (Instance == null) return true;
		bool lockedSomewhere = false;
		foreach (SkillTreeNode node in Instance.nodes)
		{
			if (!node.UnlocksBlocks.Contains(kind)) continue;
			if (unlocked.Contains(node.Id)) return true;
			lockedSomewhere = true;
		}
		return !lockedSomewhere;
	}

	static void BuildIndex()
	{
		// 에셋을 먼저 불러온다. 에디터에서는 처음 불러올 때 OnValidate가 불려 byId를 비우므로,
		// 목록은 새 변수에 다 채운 뒤 마지막에 넣는다 (먼저 넣으면 채우는 도중에 null이 되어 로그인 때 에러가 났음, #396)
		SkillTree tree = Instance;
		var index = new Dictionary<string, SkillTreeNode>();
		if (tree == null)
		{
			Debug.LogError($"[SkillTree] Resources/{ResourcePath} 에셋이 없음");
			byId = index;
			return;
		}
		foreach (SkillTreeNode node in tree.nodes)
		{
			if (node == null || string.IsNullOrEmpty(node.Id) || index.ContainsKey(node.Id)) continue;
			index.Add(node.Id, node);
		}
		byId = index;
	}

	// Inspector에서 노드를 더하거나 ID를 바꾸면 찾기 목록을 다시 만든다
	void OnValidate()
	{
		byId = null;
	}

	// 검사 -------------------------------------------------------------------

	/// <summary>
	/// 표가 올바른지 검사하고 찾은 문제를 돌려준다. 비어 있으면 통과.
	/// </summary>
	public List<string> Validate()
	{
		var problems = new List<string>();
		var ids = new HashSet<string>();

		foreach (SkillTreeNode node in nodes)
		{
			if (string.IsNullOrEmpty(node.Id)) problems.Add("ID가 빈 노드가 있음");
			else if (!ids.Add(node.Id)) problems.Add($"\"{node.Id}\"가 두 번 있음");
		}

		foreach (SkillTreeNode node in nodes)
		{
			foreach (string pre in node.Prerequisites)
			{
				if (!ids.Contains(pre)) problems.Add($"{node.Id}: 선행 노드 \"{pre}\"가 표에 없음");
				if (pre == node.Id) problems.Add($"{node.Id}: 자기 자신이 선행 노드임");
				// 다른 언어의 노드는 그 캐릭터가 해금할 수 없으므로 선행으로 걸면 영원히 못 연다 (#523)
				string nodeLanguage = LanguageOfNode(node.Id), preLanguage = LanguageOfNode(pre);
				if (nodeLanguage != null && preLanguage != null && nodeLanguage != preLanguage)
					problems.Add($"{node.Id}: 다른 언어의 노드 \"{pre}\"가 선행 노드임");
			}
			if (node.AutoUnlock && node.Cost.Count > 0) problems.Add($"{node.Id}: 같이 열리는 노드(autoUnlock)인데 재료가 있음");
			if (!node.AutoUnlock && node.Cost.Count == 0) problems.Add($"{node.Id}: 해금 재료가 비어 있음");
			foreach (MaterialCost c in node.Cost)
			{
				if (string.IsNullOrEmpty(c.MaterialId)) problems.Add($"{node.Id}: 재료 ID가 비어 있음");
			}
		}

		// 선행 조건이 돌고 돌면 영원히 해금할 수 없다
		string cycle = FindCycle();
		if (cycle != null) problems.Add($"선행 조건이 돌고 있음: {cycle}");

		// 자세 (#545): 자세 함수 노드는 처음 개수가 없어 노드가 없으면 상점 말고는 얻을 수 없다.
		// 규칙 노드는 자세 규칙 표(StanceRule)의 줄과 하나씩 맞아야 화면에 이름·효과가 보인다
		foreach (string function in StanceRule.FunctionIds)
		{
			if (!ids.Contains(function)) problems.Add($"{function}: 자세 함수인데 스킬 트리에 없음");
		}
		foreach (SkillTreeNode node in nodes.Where(n => n.Kind == SkillTreeNodeKind.Rule))
		{
			if (!StanceRule.TryGetRule(node.Id, out _)) problems.Add($"{node.Id}: 자세 규칙 표(StanceRule)에 없는 규칙 노드");
			if (node.UnlocksBlocks.Count > 0) problems.Add($"{node.Id}: 규칙 노드인데 여는 블록이 있음");
		}
		var ruleNodes = new HashSet<string>(nodes.Where(n => n.Kind == SkillTreeNodeKind.Rule).Select(n => n.Id));
		foreach (StanceRule language in StanceRule.All)
		{
			foreach (StanceRuleEntry rule in language.Rules)
			{
				if (!ruleNodes.Contains(rule.NodeId)) problems.Add($"{rule.NodeId}: 자세 규칙 표에는 있는데 스킬 트리에 규칙 노드(Rule)로 없음");
			}
		}

		// 스킬 노드와 SkillTable(박진우)의 스킬이 같아야 한다. 자세 함수는 스킬이 아니라서 SkillTable에 없으므로 뺀다 (#545)
		if (SkillTable.Instance != null)
		{
			var skillIds = new HashSet<string>(SkillTable.Instance.Skills.Select(s => s.Id));
			foreach (SkillTreeNode node in nodes.Where(n => n.Kind == SkillTreeNodeKind.Skill && !StanceRule.FunctionIds.Contains(n.Id)))
			{
				if (!skillIds.Contains(node.Id)) problems.Add($"{node.Id}: SkillTable에 없는 스킬");
			}
			var treeSkills = new HashSet<string>(nodes.Where(n => n.Kind == SkillTreeNodeKind.Skill).Select(n => n.Id));
			foreach (string skillId in skillIds)
			{
				if (!treeSkills.Contains(skillId)) problems.Add($"{skillId}: SkillTable에는 있는데 스킬 트리에 없음");
			}
		}
		else problems.Add("SkillTable 에셋을 찾지 못해서 스킬 이름 검사를 못 함");

		return problems;
	}

	// 선행 조건을 따라가다 다시 만나는 노드가 있으면 그 경로를 글자로, 없으면 null
	string FindCycle()
	{
		var map = new Dictionary<string, SkillTreeNode>();
		foreach (SkillTreeNode node in nodes)
		{
			if (!string.IsNullOrEmpty(node.Id) && !map.ContainsKey(node.Id)) map.Add(node.Id, node);
		}

		var done = new HashSet<string>();
		var path = new List<string>();

		string Visit(string id)
		{
			if (done.Contains(id) || !map.TryGetValue(id, out SkillTreeNode node)) return null;
			int index = path.IndexOf(id);
			if (index >= 0) return string.Join(" → ", path.Skip(index).Append(id));
			path.Add(id);
			foreach (string pre in node.Prerequisites)
			{
				string found = Visit(pre);
				if (found != null) return found;
			}
			path.RemoveAt(path.Count - 1);
			done.Add(id);
			return null;
		}

		foreach (string id in map.Keys)
		{
			string found = Visit(id);
			if (found != null) return found;
		}
		return null;
	}

	[ContextMenu("검사")]
	void LogValidation()
	{
		List<string> problems = Validate();
		if (problems.Count == 0) Debug.Log($"[SkillTree] 검사 통과: 노드 {nodes.Count}개, 처음부터 해금 C {StartingNodes(CharacterLanguages.C).Count}개, Python {StartingNodes(CharacterLanguages.Python).Count}개");
		else Debug.LogError($"[SkillTree] 문제 {problems.Count}개\n- " + string.Join("\n- ", problems));
	}

	[ContextMenu("기획서대로 채우기")]
	void FillDefault()
	{
		nodes = SkillTreeDefaults.Create();
		byId = null;
#if UNITY_EDITOR
		UnityEditor.EditorUtility.SetDirty(this);
#endif
		Debug.Log($"[SkillTree] 기획서대로 노드 {nodes.Count}개를 채움. Ctrl/Cmd+S로 저장하고 「검사」를 눌러 확인한다");
	}

	// 「기획서대로 채우기」와 달리 에셋에 있는 노드(Inspector에서 고친 재료·메모 등)는 그대로 두고,
	// 기획서(SkillTreeDefaults)에만 있는 노드를 맨 뒤에 더한다. 새 노드를 코드에 먼저 적은 뒤 에셋에 옮길 때 쓴다 (#545 자세 노드 19개)
	[ContextMenu("기획서에서 빠진 노드 더하기")]
	void AddMissingDefaults()
	{
		var have = new HashSet<string>(nodes.Where(n => n != null).Select(n => n.Id));
		var added = new List<string>();
		foreach (SkillTreeNode node in SkillTreeDefaults.Create())
		{
			if (have.Contains(node.Id)) continue;
			nodes.Add(node);
			added.Add(node.Id);
		}
		byId = null;
#if UNITY_EDITOR
		if (added.Count > 0) UnityEditor.EditorUtility.SetDirty(this);
#endif
		Debug.Log(added.Count == 0
			? "[SkillTree] 빠진 노드 없음"
			: $"[SkillTree] 노드 {added.Count}개를 더함: {string.Join(", ", added)}. Ctrl/Cmd+S로 저장하고 「검사」를 눌러 확인한다");
	}
}
