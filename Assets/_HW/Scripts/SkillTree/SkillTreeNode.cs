using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>스킬 트리 노드의 종류</summary>
public enum SkillTreeNodeKind
{
	Syntax, // 문법 요소 (변수, if문, for문 ...). 해금하면 편집 화면에서 그 블록을 쓸 수 있다
	Skill,  // C 스킬, 공통 이동. 해금하면 그 사전 정의 함수를 쓸 수 있다
}

/// <summary>
/// 노드 하나를 해금하는 데 드는 재료 하나. 예: 버그 조각 3개
/// </summary>
[Serializable]
public class MaterialCost
{
	[Tooltip("재료 ID. 지금은 임시로 bug_fragment(잡몹), boss_core(보스)")]
	[SerializeField] string materialId;
	[SerializeField, Min(1)] int count = 1;

	public string MaterialId => materialId;
	public int Count => count;

	public MaterialCost(string materialId, int count)
	{
		this.materialId = materialId;
		this.count = count;
	}
}

/// <summary>
/// 스킬 트리의 노드 하나. SkillTree 표의 한 줄이다.<br/>
/// 값은 Inspector(SkillTree 에셋)에서만 바꾼다. 다른 코드는 읽기만 한다.
/// </summary>
[Serializable]
public class SkillTreeNode
{
	[Tooltip("고정 ID. 스킬은 PredefinedFunctionIds의 값 (예: c.thrust), 문법은 syntax.이름 (예: syntax.if). 한 번 정하면 바꾸지 않는다")]
	[SerializeField] string id;
	[SerializeField] SkillTreeNodeKind kind;
	[Tooltip("화면 이름. 스킬은 비워 두면 SkillTable(박진우)의 이름을 쓴다")]
	[SerializeField] string displayName;

	[Tooltip("선행 노드 ID. 모두 해금해야 이 노드를 해금할 수 있다. 비우면 선행 조건 없음")]
	[SerializeField] List<string> prerequisites = new List<string>();
	[Tooltip("선행 노드를 모두 해금하면 재료 없이 같이 해금된다. 선행이 없으면 처음부터 해금")]
	[SerializeField] bool autoUnlock;
	[Tooltip("해금 재료 (지금은 전부 임시)")]
	[SerializeField] List<MaterialCost> cost = new List<MaterialCost>();

	[Tooltip("해금하면 편집 화면에서 쓸 수 있게 되는 블록 종류. 문법 노드에 주로 쓴다")]
	[SerializeField] List<BlockKind> unlocksBlocks = new List<BlockKind>();

	[Tooltip("기획 문서와 다르게 가정한 것 등")]
	[SerializeField, TextArea(1, 3)] string note;

	public string Id => id;
	public SkillTreeNodeKind Kind => kind;
	public IReadOnlyList<string> Prerequisites => prerequisites;
	public bool AutoUnlock => autoUnlock;
	public IReadOnlyList<MaterialCost> Cost => cost;
	public IReadOnlyList<BlockKind> UnlocksBlocks => unlocksBlocks;
	public string Note => note;

	/// <summary>
	/// 화면 이름. 스킬이고 displayName이 비어 있으면 SkillTable의 이름, 그것도 없으면 ID.
	/// </summary>
	public string DisplayName
	{
		get
		{
			if (!string.IsNullOrEmpty(displayName)) return displayName;
			if (kind == SkillTreeNodeKind.Skill && SkillTable.TryGet(id, out SkillData data)) return data.DisplayName;
			return id;
		}
	}

	public SkillTreeNode(string id, SkillTreeNodeKind kind, string displayName, string[] prerequisites,
		bool autoUnlock, MaterialCost[] cost, BlockKind[] unlocksBlocks = null, string note = null)
	{
		this.id = id;
		this.kind = kind;
		this.displayName = displayName;
		this.prerequisites = new List<string>(prerequisites ?? Array.Empty<string>());
		this.autoUnlock = autoUnlock;
		this.cost = new List<MaterialCost>(cost ?? Array.Empty<MaterialCost>());
		this.unlocksBlocks = new List<BlockKind>(unlocksBlocks ?? Array.Empty<BlockKind>());
		this.note = note;
	}
}
