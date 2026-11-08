using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 참고(Reference) 창의 카드 하나 (ReferenceCard 프리팹): 블록 이름, 분류 태그, 설명, 사용법, C 표기·놓는 곳. 보여 주기만 한다.
/// 카드를 누르면 자세한 내용(형식, 채울 곳, 동작, 예시 등)이 펼쳐지고, 다시 누르면 접힌다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_card) + Button + Vertical Layout Group + 이 컴포넌트, 자식에 Title(이름 + 분류 태그 한 줄),
/// Summary, Usage, Details, Expanded, Hint(Text - TextMeshPro). 프리팹은 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기가 만든다 (InvokeUIBuilder).
/// </para>
/// </summary>
public class ReferenceCardView : MonoBehaviour
{
	[SerializeField, Tooltip("블록 이름 (예: If, thrust)")]
	TMP_Text nameText;

	[SerializeField, Tooltip("분류 태그의 바탕. 그 블록 분류의 색(BlockLabels)으로 칠한다")]
	Image categoryTag;

	[SerializeField, Tooltip("분류 태그 글자 (예: 제어, 함수)")]
	TMP_Text categoryText;

	[SerializeField, Tooltip("설명 한 문장")]
	TMP_Text summaryText;

	[SerializeField, Tooltip("사용법 한 문장 (C 스킬이 파라미터에 무엇을 넣는지). 없으면 숨긴다")]
	TMP_Text usageText;

	[SerializeField, Tooltip("C 표기, 놓는 곳 두 줄")]
	TMP_Text detailsText;

	[SerializeField, Tooltip("펼쳤을 때만 보이는 자세한 내용 (형식, 채울 곳, 동작, 예시, 주의)")]
	TMP_Text expandedText;

	[SerializeField, Tooltip("카드 맨 아래 안내 (눌러서 자세히 / 눌러서 접기)")]
	TMP_Text hintText;

	// 줄 이름 글자 색 (BlockEditor의 상태 글자와 같은 어두운 회색 파랑)
	const string LabelColor = "#7C84A8";

	// 펼친 내용의 항목 이름 색 (참고 탭의 강조색)
	const string SectionColor = "#8B9CFF";

	bool expanded;

	void Awake()
	{
		if (TryGetComponent(out Button button)) button.onClick.AddListener(Toggle);
	}

	/// <summary>
	/// 카드에 entry의 내용을 보여 준다. 접힌 상태로 시작한다.
	/// </summary>
	public void Show(BlockReferenceEntry entry)
	{
		if (nameText != null) nameText.text = entry.Name;
		if (categoryText != null) categoryText.text = entry.Category;
		if (categoryTag != null && entry.Sample != null) categoryTag.color = BlockLabels.ColorOf(entry.Sample);
		if (summaryText != null) summaryText.text = entry.Summary ?? "";

		if (usageText != null)
		{
			usageText.gameObject.SetActive(!string.IsNullOrEmpty(entry.Usage));
			usageText.text = entry.Usage ?? "";
		}

		// 값에 < 같은 글자가 들어 있어도 TextMeshPro 태그로 읽지 않도록 noparse로 감싼다 (SkillTreeView와 같은 방식)
		if (detailsText != null)
		{
			detailsText.text =
				$"<color={LabelColor}>C 표기</color>  <noparse>{entry.CodeText}</noparse>\n" +
				$"<color={LabelColor}>놓는 곳</color>  <noparse>{entry.WhereText}</noparse>";
		}

		// 펼칠 내용이 없는 카드는 눌러도 아무 일도 없으므로 안내도 숨긴다
		bool hasSections = entry.Sections != null && entry.Sections.Count > 0;
		if (expandedText != null) expandedText.text = hasSections ? BuildExpanded(entry) : "";
		if (hintText != null) hintText.gameObject.SetActive(hasSections);
		SetExpanded(false, hasSections);
	}

	// 항목마다 「이름」 줄과 내용을 놓고, 항목 사이는 한 줄 비운다
	static string BuildExpanded(BlockReferenceEntry entry)
	{
		var text = new StringBuilder();
		for (int i = 0; i < entry.Sections.Count; i++)
		{
			ReferenceSection section = entry.Sections[i];
			if (i > 0) text.Append("\n\n");
			text.Append($"<color={SectionColor}>{section.Title}</color>\n<noparse>{section.Body}</noparse>");
		}
		return text.ToString();
	}

	void Toggle()
	{
		if (expandedText == null || string.IsNullOrEmpty(expandedText.text)) return;
		SetExpanded(!expanded, true);
	}

	void SetExpanded(bool value, bool hasSections)
	{
		expanded = value && hasSections;
		if (expandedText != null) expandedText.gameObject.SetActive(expanded);
		if (hintText != null) hintText.text = expanded ? "눌러서 접기" : "눌러서 자세히 ▾";
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform); // 높이가 바뀌므로 목록의 배치를 다시 계산한다
	}
}
