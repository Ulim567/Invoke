using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 자식을 왼쪽부터 가로로 늘어놓다가 줄이 차면 다음 줄로 넘기는 Layout Group. 블록 목록의 칩(If, For, Declare…)에 쓴다. 보여 주기만 한다.
/// <para>
/// 자식의 크기는 자식이 원하는 크기(Preferred Size)를 그대로 쓴다. 높이는 줄 수만큼 늘어난다 (부모의 Vertical Layout Group이 읽는다).
/// </para>
/// </summary>
public class FlowLayoutGroup : LayoutGroup
{
	[SerializeField, Tooltip("칩 사이 간격 (가로, 세로)")]
	Vector2 spacing = new Vector2(3, 3);

	float height; // 줄 수만큼의 전체 높이 (CalculateLayoutInputVertical에서 계산)

	// 너비는 부모가 정하고(늘어남), 높이는 줄 수만큼
	public override float minWidth => padding.horizontal;
	public override float preferredWidth => padding.horizontal;
	public override float flexibleWidth => 1;
	public override float minHeight => height;
	public override float preferredHeight => height;
	public override float flexibleHeight => 0;

	public override void CalculateLayoutInputVertical() => height = Arrange(false);

	public override void SetLayoutHorizontal() => Arrange(true);

	public override void SetLayoutVertical() => Arrange(true);

	// 자리를 계산한다. apply면 자식을 그 자리에 놓는다. 전체 높이를 돌려준다
	float Arrange(bool apply)
	{
		float width = rectTransform.rect.width;
		float right = width - padding.right;
		float x = padding.left;
		float y = padding.top;
		float rowHeight = 0;

		foreach (RectTransform child in rectChildren)
		{
			float w = Mathf.Min(LayoutUtility.GetPreferredWidth(child), Mathf.Max(0, width - padding.horizontal));
			float h = LayoutUtility.GetPreferredHeight(child);
			if (x > padding.left && x + w > right)
			{
				x = padding.left;
				y += rowHeight + spacing.y;
				rowHeight = 0;
			}
			if (apply)
			{
				SetChildAlongAxis(child, 0, x, w);
				SetChildAlongAxis(child, 1, y, h);
			}
			x += w + spacing.x;
			rowHeight = Mathf.Max(rowHeight, h);
		}
		return y + rowHeight + padding.bottom;
	}
}
