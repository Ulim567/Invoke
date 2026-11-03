using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스킬 트리 화면의 노드 하나 (SkillTreeNode 프리팹). SkillTreeView가 만들고 색·글자를 정한다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Image + Button + Outline + 이 컴포넌트, 자식에 Text - TextMeshPro(Raycast Target 끔).
/// </para>
/// </summary>
public class SkillTreeNodeView : MonoBehaviour
{
	[SerializeField, Tooltip("노드 바탕 (최상위 Image)")]
	Image background;

	[SerializeField, Tooltip("노드 테두리 (최상위 Outline)")]
	Outline outline;

	[SerializeField, Tooltip("노드 이름 (자식 Text - TextMeshPro)")]
	TMP_Text label;

	[SerializeField, Tooltip("누르면 노드를 고른다 (최상위 Button)")]
	Button button;

	public RectTransform Rect => (RectTransform)transform;
	public Button Button => button;

	/// <summary>
	/// 바탕색, 테두리, 글자를 정한다.
	/// </summary>
	public void Show(Color fill, Color border, float borderWidth, string text)
	{
		if (background != null) background.color = fill;
		if (outline != null)
		{
			outline.effectColor = border;
			outline.effectDistance = new Vector2(borderWidth, -borderWidth);
		}
		if (label != null)
		{
			label.color = Color.white; // 어두운 바탕에서 읽히게. 잠긴 노드의 회색은 글자 안의 색 태그로 준다
			label.text = text;
		}
	}
}
