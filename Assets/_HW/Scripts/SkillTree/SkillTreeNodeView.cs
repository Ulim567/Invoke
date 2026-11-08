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

	[SerializeField, Tooltip("스킬 아이콘 (#444). 비워 두면 처음 쓸 때 노드 왼쪽에 만든다")]
	Image icon;

	// 노드 높이(48)에 맞춘 아이콘 크기와, 아이콘이 있을 때 글자 왼쪽 여백
	const float IconSize = 40f, IconLeft = 4f;

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

	/// <summary>
	/// 스킬 아이콘을 노드 왼쪽에 보인다 (#444). sprite가 null이면(문법 노드) 숨기고 글자를 가운데로 되돌린다. dim이면 잠긴 노드라 어둡게.
	/// </summary>
	public void ShowIcon(Sprite sprite, bool dim)
	{
		if (sprite == null)
		{
			if (icon != null) icon.enabled = false;
			if (label != null) label.margin = Vector4.zero;
			return;
		}
		if (icon == null) icon = CreateIcon();
		icon.sprite = sprite;
		icon.enabled = true;
		icon.color = dim ? new Color(0.45f, 0.45f, 0.5f, 1f) : Color.white;
		if (label != null) label.margin = new Vector4(IconLeft + IconSize + 2f, 0f, 4f, 0f);
	}

	// 프리팹에 아이콘 자리가 없으면 노드 왼쪽 가운데에 만든다
	Image CreateIcon()
	{
		var obj = new GameObject("Icon", typeof(RectTransform));
		var rect = (RectTransform)obj.transform;
		rect.SetParent(transform, false);
		rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
		rect.pivot = new Vector2(0f, 0.5f);
		rect.anchoredPosition = new Vector2(IconLeft, 0f);
		rect.sizeDelta = new Vector2(IconSize, IconSize);
		var image = obj.AddComponent<Image>();
		image.raycastTarget = false;
		image.preserveAspect = true;
		return image;
	}
}
