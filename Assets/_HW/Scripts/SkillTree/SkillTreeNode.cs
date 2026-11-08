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
/// 노드 하나의 플레이어용 설명. 문법·스킬 노드가 같은 규칙으로 쓴다.<br/>
/// - Summary: 짧은 설명 한 문장. 스킬 트리 문법 탭의 노드 옆에 보인다<br/>
/// - Detail: 자세한 설명 문단. 스킬 트리 오른쪽 설명 칸의 이름 아래에 보인다<br/>
/// 비어 있으면 그 자리에 아무것도 보이지 않는다. 개발 메모는 여기가 아니라 SkillTreeNode.Note에 적는다.
/// </summary>
[Serializable]
public class SkillTreeNodeDescription
{
	[Tooltip("짧은 설명 한 문장. 문법 탭의 노드 옆에 보인다")]
	[SerializeField, TextArea(1, 2)] string summary;
	[Tooltip("자세한 설명 문단. 오른쪽 설명 칸의 이름 아래에 보인다")]
	[SerializeField, TextArea(2, 6)] string detail;

	public string Summary => summary;
	public string Detail => detail;
	/// <summary>짧은 설명과 자세한 설명이 모두 비어 있으면 true</summary>
	public bool IsEmpty => string.IsNullOrEmpty(summary) && string.IsNullOrEmpty(detail);

	public SkillTreeNodeDescription() { }

	public SkillTreeNodeDescription(string summary, string detail)
	{
		this.summary = summary;
		this.detail = detail;
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

	[Tooltip("플레이어에게 보이는 설명. 짧은 설명은 노드 옆, 자세한 설명은 오른쪽 설명 칸")]
	[SerializeField] SkillTreeNodeDescription description = new SkillTreeNodeDescription();

	[Tooltip("기획 문서와 다르게 가정한 것 등")]
	[SerializeField, TextArea(1, 3)] string note;

	public string Id => id;
	public SkillTreeNodeKind Kind => kind;
	public IReadOnlyList<string> Prerequisites => prerequisites;
	public bool AutoUnlock => autoUnlock;
	public IReadOnlyList<MaterialCost> Cost => cost;
	public IReadOnlyList<BlockKind> UnlocksBlocks => unlocksBlocks;
	public string Note => note;
	/// <summary>플레이어에게 보이는 설명 (짧은 설명, 자세한 설명). 비어 있을 수 있다</summary>
	public SkillTreeNodeDescription Description => description;

	/// <summary>
	/// 설명을 바꾼다. SkillTree 에셋의 메뉴(「기획서대로 채우기」, 「비어 있는 설명만 채우기」)만 부른다. 게임 코드는 읽기만 한다.
	/// </summary>
	internal void SetDescription(SkillTreeNodeDescription value) => description = value;

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
