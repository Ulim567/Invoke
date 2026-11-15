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

	[SerializeField, Range(0.3f, 1f), Tooltip("자식 크기 배율. 블록 목록 칩을 편집 영역 블록보다 작게 보일 때 쓴다 (시안 6)")]
	float childScale = 1f;

	[SerializeField, Tooltip("내어쓰기: 줄을 넘길 때 첫 자식 오른쪽에서 시작한다. 블록 목록의 소분류 글씨(조건, 반복) 아래로 칩이 들어가지 않게 (#487)")]
	bool hangingIndent;

	float height; // 줄 수만큼의 전체 높이 (CalculateLayoutInputVertical에서 계산)

	/// <summary>
	/// 내어쓰기. 켜면 둘째 줄부터 첫 자식 오른쪽에서 시작한다. 첫 자식이 소분류 글씨인 칩 줄에서 BlockEditor가 켠다 (#487).
	/// </summary>
	public bool HangingIndent
	{
		get => hangingIndent;
		set
		{
			if (hangingIndent == value) return;
			hangingIndent = value;
			SetDirty();
		}
	}

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

	// 자식 하나의 자리 (Arrange가 줄 높이를 다 센 뒤에 놓기 위해 모아 둔다)
	readonly System.Collections.Generic.List<(RectTransform child, float x, float w, float h, int row)> placed =
		new System.Collections.Generic.List<(RectTransform, float, float, float, int)>();
	readonly System.Collections.Generic.List<(float y, float height)> rows = new System.Collections.Generic.List<(float, float)>();

	// 자리를 계산한다. apply면 자식을 그 자리에 놓는다. 전체 높이를 돌려준다
	// 한 줄 안에서 높이가 다른 자식은 세로 가운데에 맞춘다. 칩 줄 맨 앞의 소분류 글씨(조건, 반복, #487)가 칩보다 낮아도 칩과 같은 높이에 보이게
	float Arrange(bool apply)
	{
		float width = rectTransform.rect.width;
		float right = width - padding.right;
		float scale = Mathf.Max(0.01f, childScale);
		float x = padding.left;
		float y = padding.top;
		float rowHeight = 0;
		float rowStart = padding.left;  // 지금 줄이 시작한 x
		float wrapStart = padding.left; // 줄을 넘길 때 시작할 x. 내어쓰기면 첫 자식 오른쪽
		placed.Clear();
		rows.Clear();

		foreach (RectTransform child in rectChildren)
		{
			// 자식 크기는 원래 크기, 자리는 배율을 곱한 크기로 계산한다
			float w = Mathf.Min(LayoutUtility.GetPreferredWidth(child), Mathf.Max(0, width - padding.horizontal) / scale);
			float h = LayoutUtility.GetPreferredHeight(child);
			float sw = w * scale, sh = h * scale;
			if (x > rowStart && x + sw > right)
			{
				rows.Add((y, rowHeight));
				x = rowStart = wrapStart;
				y += rowHeight + spacing.y;
				rowHeight = 0;
			}
			placed.Add((child, x, w, h, rows.Count));
			x += sw + spacing.x;
			rowHeight = Mathf.Max(rowHeight, sh);
			if (hangingIndent && placed.Count == 1) wrapStart = x;
		}
		rows.Add((y, rowHeight));

		if (apply)
		{
			foreach ((RectTransform child, float cx, float w, float h, int row) in placed)
			{
				(float rowY, float rowH) = rows[row];
				child.localScale = new Vector3(scale, scale, 1);
				SetChildAlongAxisWithScale(child, 0, cx, w, scale);
				SetChildAlongAxisWithScale(child, 1, rowY + (rowH - h * scale) / 2f, h, scale);
			}
		}
		return y + rowHeight + padding.bottom;
	}
}
