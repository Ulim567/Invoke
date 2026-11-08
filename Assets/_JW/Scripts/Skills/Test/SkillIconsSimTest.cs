using UnityEngine;

/// <summary>
/// 스킬 아이콘 테스트 (#444). 아무 씬의 아무 오브젝트에 붙이고 Play (Host 필요 없음). 콘솔에 「[SkillIconTest] … 통과/실패」가 찍힌다.
/// <para>
/// 1. 스킬 트리의 스킬 노드마다 아이콘이 있다 (24개)<br/>
/// 2. 아이콘이 24×24 픽셀이고 Point 필터다<br/>
/// 3. 없는 ID와 문법 노드는 null
/// </para>
/// </summary>
public class SkillIconsSimTest : MonoBehaviour
{
	int failures;

	void Start()
	{
		if (SkillTree.Instance == null) { Check("SkillTree 에셋을 찾음", false); Finish(); return; }

		int skills = 0, found = 0;
		foreach (SkillTreeNode node in SkillTree.Instance.Nodes)
		{
			if (node.Kind == SkillTreeNodeKind.Skill)
			{
				skills++;
				Sprite icon = SkillIcons.Get(node.Id);
				if (icon == null) { Check($"{node.DisplayName}({node.Id}) 아이콘", false); continue; }
				found++;
				bool size = Mathf.RoundToInt(icon.rect.width) == 24 && Mathf.RoundToInt(icon.rect.height) == 24;
				if (!size) Check($"{node.Id} 아이콘이 24×24 (지금 {icon.rect.width}×{icon.rect.height})", false);
				if (icon.texture.filterMode != FilterMode.Point) Check($"{node.Id} 아이콘이 Point 필터", false);
			}
			else if (SkillIcons.Get(node.Id) != null) Check($"문법 노드 {node.Id}는 아이콘 없음", false);
		}
		Check($"스킬 노드 {skills}개 중 아이콘 {found}개", skills > 0 && found == skills);
		Check("없는 ID는 null", SkillIcons.Get("no.such_skill") == null && SkillIcons.Get(null) == null);
		Finish();
	}

	void Finish()
	{
		if (failures == 0) Debug.Log("[SkillIconTest] 전체 통과");
		else Debug.LogError($"[SkillIconTest] 실패 {failures}개");
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[SkillIconTest] {label}: 통과");
		else { failures++; Debug.LogError($"[SkillIconTest] {label}: 실패"); }
	}
}
