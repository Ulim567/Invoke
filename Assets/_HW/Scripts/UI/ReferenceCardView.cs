using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 참고(Reference) 창의 카드 하나 (ReferenceCard 프리팹): 블록 이름, 분류 태그, 형식, 설명 한 문장. 보여 주기만 한다.
/// 카드를 누르면 자세한 내용(하는 일, 용도, 예시, 슬롯, return 값, 주의할 점, 실행)이 펼쳐지고, 다시 누르면 접힌다 (#476 레퍼런스 페이지 형식).
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
		// 한글 이름이 있는 카드(함수 카드)는 제목에 한글 이름만 (#489, 사용자가 정함). 함수 탭 항목 제목과 같다.
		// 영문 이름은 형식 줄의 원형(void skewer(Target target);)에 들어 있다. 문법 블록은 한글 이름이 없어 영문 이름(If, For)
		if (nameText != null) nameText.text = string.IsNullOrEmpty(entry.KoreanName) ? entry.Name : entry.KoreanName;
		if (categoryText != null) categoryText.text = entry.Category;
		if (categoryTag != null && entry.Sample != null) categoryTag.color = BlockLabels.ColorOf(entry.Sample);
		// 설명 한 문장은 접힌 카드에 보이지 않고, 펼친 내용의 맨 위에 보인다 (#476). 접힌 카드는 이름과 형식만 보여 목록을 훑기 쉽게 하기 위함
		if (summaryText != null) summaryText.gameObject.SetActive(false);

		// 사용법 줄은 쓰지 않는다 (#476, 파라미터 설명과 겹침). 프리팹의 칸은 남겨 두고 숨긴다
		if (usageText != null) usageText.gameObject.SetActive(false);

		// 형식 한 줄 (if (조건) { 블록 }, void thrust(Target target);). VS Code처럼 낱말마다 색을 나눈다 (#489, BlockReference.ColorCode)
		// 칠한 낱말마다 noparse로 감싸서 < 같은 글자가 들어 있어도 TextMeshPro 태그로 읽지 않는다
		if (detailsText != null) detailsText.text = BlockReference.ColorCode(entry.CodeText);

		// 펼칠 내용이 없는 카드는 눌러도 아무 일도 없으므로 안내도 숨긴다
		bool hasSections = !string.IsNullOrEmpty(entry.Summary) || (entry.Sections != null && entry.Sections.Count > 0);
		if (expandedText != null) expandedText.text = hasSections ? BuildExpanded(entry) : "";
		if (hintText != null) hintText.gameObject.SetActive(hasSections);
		SetExpanded(false, hasSections);
	}

	// 맨 위에 하는 일 한 문장, 그 아래 항목마다 「이름」 줄과 내용을 놓고, 사이는 한 줄 비운다.
	// 이름 붙은 줄(슬롯, return 값)은 「- 이름: 설명」으로, 이름은 형식 줄과 같은 코드 색. 예시는 상황 → 코드(코드 색) → 결과
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
				// 줄 이름은 형식 줄·원형과 같은 규칙(BlockReference.ColorCode)으로 칠한다 (#489, 사용자가 통일을 요청).
				// 형식 줄의 같은 글자와 같은 색이 되어, 이 줄이 형식의 어느 자리를 설명하는지 바로 보인다
				// 예: 채울 곳 「타입」「처음 값」 = 형식의 한글 자리(흐린 보라), 파라미터 「target」 = 원형의 파라미터(하늘색), return 값 「Target」 = 원형의 반환 타입(청록)
				foreach ((string name, string body) in section.Items)
				{
					// 이름이 없는 줄(type의 「넣을 수 있는 빈칸: ...」)은 글만
					if (string.IsNullOrEmpty(name)) { text.Append($"\n- <noparse>{body}</noparse>"); continue; }
					text.Append($"\n- {BlockReference.ColorCode(name)}");
					if (!string.IsNullOrEmpty(body)) text.Append($": <noparse>{body}</noparse>");
				}
				continue;
			}
			if (!string.IsNullOrEmpty(section.Body)) text.Append($"\n<noparse>{section.Body}</noparse>");
			// 예시 코드도 형식 줄과 같은 코드 색 (#489). 흰색 한 색이면 키워드·함수 이름이 구분되지 않기 때문
			if (!string.IsNullOrEmpty(section.Code)) text.Append($"\n{BlockReference.ColorCode(section.Code)}");
			if (!string.IsNullOrEmpty(section.Result)) text.Append($"\n<color={LabelColor}>결과</color>  <noparse>{section.Result}</noparse>");
		}
		return text.ToString();
	}

	void Toggle()
	{
		if (expandedText == null || string.IsNullOrEmpty(expandedText.text)) return;
		// 카드는 하나만 펼친다 (#489, 사용자가 정함): 이 카드를 펼치기 전에 같은 목록에서 펼쳐져 있던 카드를 접는다
		if (!expanded) CollapseSiblings();
		SetExpanded(!expanded, true);
	}

	// 같은 목록(참고 창의 카드 자리)에 있는 다른 카드를 모두 접는다
	void CollapseSiblings()
	{
		if (transform.parent == null) return;
		foreach (Transform sibling in transform.parent)
		{
			if (sibling == transform || !sibling.TryGetComponent(out ReferenceCardView card) || !card.expanded) continue;
			card.SetExpanded(false, true);
		}
	}

	void SetExpanded(bool value, bool hasSections)
	{
		expanded = value && hasSections;
		if (expandedText != null) expandedText.gameObject.SetActive(expanded);
		if (hintText != null) hintText.text = expanded ? "눌러서 접기" : "눌러서 자세히"; // ▾는 글꼴(Galmuri11)에 없어 뺀다
		LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform); // 높이가 바뀌므로 목록의 배치를 다시 계산한다
	}
}
