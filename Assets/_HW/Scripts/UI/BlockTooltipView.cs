using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 블록 편집 화면의 설명창 (BlockTooltip 프리팹, #476). 블록·값 칸에 마우스를 올리면 BlockEditor가 띄운다. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_panel) + Vertical Layout Group + Content Size Fitter + Canvas Group(클릭 통과) + 이 컴포넌트,
/// 자식에 Text(Text - TextMeshPro, 너비 고정). 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기가 만든다 (InvokeUIBuilder).
/// </para>
/// </summary>
public class BlockTooltipView : MonoBehaviour
{
	[SerializeField, Tooltip("설명 글자 (리치 텍스트)")]
	TMP_Text text;

	// 마우스를 올린 칸과 설명창 사이 간격 (캔버스 기준 픽셀)
	const float Gap = 3f;

	/// <summary>
	/// content를 보이고, anchor(마우스를 올린 블록·값 칸) 바로 아래에 놓는다. 아래가 모자라면 위에, 오른쪽이 모자라면 왼쪽으로 당겨 area 안에 둔다.
	/// </summary>
	public void Show(string content, RectTransform anchor, RectTransform area)
	{
		gameObject.SetActive(true);
		transform.SetAsLastSibling(); // 끄는 블록, 드롭다운보다 위에
		if (text != null) text.text = content;
		var rect = (RectTransform)transform;
		LayoutRebuilder.ForceRebuildLayoutImmediate(rect); // 글자에 맞춘 크기를 바로 알기 위함

		// anchor의 네 모서리를 area 기준 좌표로 바꾼다 (0: 왼쪽 아래, 1: 왼쪽 위)
		var corners = new Vector3[4];
		anchor.GetWorldCorners(corners);
		Vector2 bottomLeft = area.InverseTransformPoint(corners[0]);
		Vector2 topLeft = area.InverseTransformPoint(corners[1]);
		Rect bounds = area.rect;
		Vector2 size = rect.rect.size;

		// 설명창의 기준점은 왼쪽 위 (pivot 0, 1). 기본은 칸 바로 아래, 왼쪽 끝을 칸과 맞춘다
		float x = Mathf.Min(bottomLeft.x, bounds.xMax - size.x);
		float y = bottomLeft.y - Gap;
		if (y - size.y < bounds.yMin) y = topLeft.y + Gap + size.y; // 아래가 모자라면 칸 위에
		x = Mathf.Max(x, bounds.xMin);
		y = Mathf.Min(y, bounds.yMax);

		// 설명창은 area의 자식이므로 area 기준 좌표를 그대로 쓴다
		rect.pivot = new Vector2(0, 1);
		rect.localPosition = new Vector3(x, y, 0);
	}

	/// <summary>설명창을 숨긴다</summary>
	public void Hide() => gameObject.SetActive(false);
}
