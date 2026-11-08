using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 참고(Reference) 창의 카드 하나 (ReferenceCard 프리팹): 블록 이름, 분류 태그, 형식, 설명 한 문장. 보여 주기만 한다.
/// 카드를 누르면 자세한 내용(채울 곳·파라미터, 돌려주는 값, 예시, 실행)이 펼쳐지고, 다시 누르면 접힌다 (#476 레퍼런스 페이지 형식).
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

	[SerializeField, Tooltip("형식 한 줄 (코드 색)")]
	TMP_Text detailsText;

	[SerializeField, Tooltip("펼쳤을 때만 보이는 자세한 내용 (형식, 채울 곳, 동작, 예시, 주의)")]
	TMP_Text expandedText;

	[SerializeField, Tooltip("카드 맨 아래 안내 (눌러서 자세히 / 눌러서 접기)")]
	TMP_Text hintText;

	// 줄 이름 글자 색 (BlockEditor의 상태 글자와 같은 어두운 회색 파랑)
	const string LabelColor = "#7C84A8";

	// 펼친 내용의 항목 이름 색 (참고 탭의 강조색)
	const string SectionColor = "#8B9CFF";

	// 코드(예시) 색: 흰색. 설명 글자(회색 파랑)와 구분되게
	const string CodeColor = "#FFFFFF";

	// 접힌 카드의 형식(원형) 색: 주황. 카드마다 가장 먼저 눈에 들어오게 (#476)
	const string FormatColor = "#F0A860";

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
		// 설명 한 문장은 접힌 카드에 보이지 않고, 펼친 내용의 맨 위에 보인다 (#476). 접힌 카드는 이름과 형식만 보여 목록을 훑기 쉽게 하기 위함
		if (summaryText != null) summaryText.gameObject.SetActive(false);

		// 사용법 줄은 쓰지 않는다 (#476, 파라미터 설명과 겹침). 프리팹의 칸은 남겨 두고 숨긴다
		if (usageText != null) usageText.gameObject.SetActive(false);

		// 형식 한 줄 (if (조건) { 블록 }, void thrust(Target target);). 레퍼런스 페이지처럼 코드 색으로
		// 값에 < 같은 글자가 들어 있어도 TextMeshPro 태그로 읽지 않도록 noparse로 감싼다 (SkillTreeView와 같은 방식)
		if (detailsText != null) detailsText.text = $"<color={FormatColor}><noparse>{entry.CodeText}</noparse></color>";

		// 펼칠 내용이 없는 카드는 눌러도 아무 일도 없으므로 안내도 숨긴다
		bool hasSections = !string.IsNullOrEmpty(entry.Summary) || (entry.Sections != null && entry.Sections.Count > 0);
		if (expandedText != null) expandedText.text = hasSections ? BuildExpanded(entry) : "";
		if (hintText != null) hintText.gameObject.SetActive(hasSections);
		SetExpanded(false, hasSections);
	}

	// 맨 위에 설명 한 문장, 그 아래 항목마다 「이름」 줄과 내용을 놓고, 사이는 한 줄 비운다.
	// 이름 붙은 줄(파라미터 등)은 「- 이름: 설명」으로, 이름은 강조색. 예시는 코드 색
	// 줄 앞 표시는 「-」: Galmuri11 글꼴에 「·」「—」가 없어 빈칸으로 보이기 때문 (#476)
	static string BuildExpanded(BlockReferenceEntry entry)
	{
		var text = new StringBuilder();
		if (!string.IsNullOrEmpty(entry.Summary)) text.Append($"<noparse>{entry.Summary}</noparse>");
		for (int i = 0; entry.Sections != null && i < entry.Sections.Count; i++)
		{
			ReferenceSection section = entry.Sections[i];
			if (text.Length > 0) text.Append("\n\n");
			text.Append($"<color={SectionColor}>{section.Title}</color>");
			if (section.Items != null)
			{
				foreach ((string name, string body) in section.Items)
				{
					text.Append($"\n- <color={SectionColor}><noparse>{name}</noparse></color>");
					if (!string.IsNullOrEmpty(body)) text.Append($": <noparse>{body}</noparse>");
				}
			}
			else if (section.IsCode) text.Append($"\n<color={CodeColor}><noparse>{section.Body}</noparse></color>");
			else text.Append($"\n<noparse>{section.Body}</noparse>");
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
		if (hintText != null) hintText.text = expanded ? "눌러서 접기" : "눌러서 자세히"; // ▾는 글꼴(Galmuri11)에 없어 뺀다
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform); // 높이가 바뀌므로 목록의 배치를 다시 계산한다
	}
}
