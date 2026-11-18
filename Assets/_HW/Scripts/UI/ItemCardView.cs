using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 블록·함수·type 설명 카드 (#544, 노션 11/16 「7. 해금 요소」, 캔버스 9-3). 네 곳에서 같은 모양으로 보인다.
/// <para>
/// - 해금 직후(ItemCardPopup), 스킬 트리 노드 클릭(설명 칸), 블록 목록·함수 탭의 항목 클릭(ItemCardPopup), 블록 편집 화면 참고 탭.<br/>
/// - 첫 화면: 이름(한글 + 영문), 분류 태그, 기능(하는 일), 함수 원형·형식, 블록 사용 예(블록 그림), 사용 방법.
///   나머지(용도, 예시, 슬롯·파라미터, return 값, 주의할 점, 실행)는 「자세히」를 누르면 펼친다.<br/>
/// - 카드가 여러 장이면(문법 노드가 여는 블록들) 아래 「&lt; 1 / 3 &gt;」로 넘긴다.<br/>
/// - 글은 설명 표(ItemDescriptions)에서 BlockReference가 만든 카드(BlockReferenceEntry)를 그대로 쓴다. 이 파일에 설명 문장을 적지 않는다.<br/>
/// - 크기: 카드는 640 기준 캔버스 단위로 만들고, 놓인 캔버스의 기준 가로(로비 창 1920, 편집 화면 960)에 맞춰 키워서 글자 크기가 어디서나 팝업과 같다 (Compact면 조금 작게).
///   가로는 놓인 자리에 맞춰 글을 다시 줄바꿈하고, 높이는 내용에 맞게 늘어 놓인 자리의 Layout Element에 알린다.<br/>
/// - 모양: 메뉴 Invoke → UI → 설명 카드 만들기(ItemCardUIBuilder)가 Resources/UI/ItemCard 프리팹으로 만든다. 게임 중에는 Create로 그 프리팹을 꺼낸다.
/// </para>
/// </summary>
[RequireComponent(typeof(LayoutElement))]
public class ItemCardView : MonoBehaviour, IPointerClickHandler
{
	/// <summary>팝업 카드의 가로 (640 기준 캔버스 단위)</summary>
	public const float PopupWidth = 300f;

	// 놓인 자리가 아주 좁아도 글이 한 글자씩 꺾이지 않게 하는 최소 가로
	const float MinWidth = 100f;

	// Compact 카드의 오른쪽 여백 (640 기준 캔버스 단위)
	const float RightInset = 5f;

	const string ResourcePath = "UI/ItemCard";
	static bool warned;

	[SerializeField, Tooltip("카드 몸통 (왼쪽 위 기준). 가로는 놓인 자리에 맞추고, 글자 크기가 팝업과 같도록 키운다")] RectTransform card;

	[SerializeField, Range(0.5f, 1f), Tooltip("설명 칸·참고 탭처럼 다른 글과 나란히 놓일 때(Compact) 팝업보다 작게 보이는 비율")]
	float compactScale = 0.85f;

	[Header("머리줄 (팝업에서만)")]
	[SerializeField] GameObject header;
	[SerializeField, Tooltip("「> 스킬 해금!」")] TMP_Text headerText;
	[SerializeField] Button closeButton;

	[Header("첫 화면")]
	[SerializeField, Tooltip("한글 이름이 있으면 한글 이름 (꼬챙이), 없으면 블록 이름 (If)")] TMP_Text titleText;
	[SerializeField, Tooltip("영문 이름 (skewer). 한글 이름이 없으면 숨긴다")] TMP_Text nameText;
	[SerializeField, Tooltip("분류 태그 바탕. 분류 색으로 칠한다")] Image categoryTag;
	[SerializeField] TMP_Text categoryText;
	[SerializeField, Tooltip("기능 (하는 일 한 문장)")] TMP_Text summaryText;
	[SerializeField, Tooltip("「함수 원형」 또는 「형식」")] TMP_Text codeLabel;
	[SerializeField, Tooltip("함수 원형·형식이 든 줄 (그 줄 전체를 켜고 끈다)")] GameObject codeRow;
	[SerializeField] TMP_Text codeText;
	[SerializeField, Tooltip("「블록 사용 예」 또는 「값 예」")] TMP_Text pictureLabel;
	[SerializeField, Tooltip("블록 그림을 놓는 칸 (Vertical Layout Group)")] RectTransform pictureBox;
	[SerializeField] GameObject usageRow;
	[SerializeField, Tooltip("「함수 탭」 칩 글자")] TMP_Text usageTab;
	[SerializeField, Tooltip("「에서 코드로 끌어다 놓기」")] TMP_Text usageText;

	[Header("자세히")]
	[SerializeField] Button detailsButton;
	[SerializeField] TMP_Text detailsLabel;
	[SerializeField] TMP_Text expandedText;

	[Header("넘기기")]
	[SerializeField] GameObject pager;
	[SerializeField] Button prevButton;
	[SerializeField] Button nextButton;
	[SerializeField] TMP_Text pageText;

	[Header("블록 그림 부품 (꺼 둔 견본)")]
	[SerializeField, Tooltip("문장 블록 한 줄: Image(블록 모양) + 자식 Text")] Image rowTemplate;
	[SerializeField, Tooltip("값 블록·값 알약: Image(알약 모양) + 자식 Text")] Image valueTemplate;
	[SerializeField, Tooltip("ㄷ자 블록의 안쪽 자리: Image(점선) + 자식 Text")] Image innerTemplate;
	[SerializeField, Tooltip("ㄷ자 블록의 아래 막대")] Image bottomTemplate;

	// 펼친 내용의 항목 이름 색 (참고 카드와 같음)
	const string SectionColor = "#8B9CFF";
	const string LabelColor = "#7C84A8";

	// 블록 색: BlockView와 같은 규칙으로 분류 색을 편집 영역 바탕 위에 연하게 섞는다 (#487)
	static readonly Color Backdrop = new Color32(0x14, 0x18, 0x23, 0xFF);
	const float RowAlpha = 0.40f, ValueAlpha = 0.39f, Saturation = 1.8f;
	static readonly Color TypeColor = new Color32(0xF2, 0xA6, 0x5A, 0xFF); // type 태그 (머리줄의 주황과 같음)

	LayoutElement fit;
	bool compact;
	IReadOnlyList<BlockReferenceEntry> entries;
	int index;
	bool expanded;
	bool wired;
	readonly List<GameObject> picture = new List<GameObject>();

	/// <summary>
	/// 카드 프리팹(Resources/UI/ItemCard)을 parent 아래에 만든다. 프리팹이 없으면(빌더를 아직 안 누름) null.
	/// </summary>
	public static ItemCardView Create(Transform parent)
	{
		var prefab = Resources.Load<ItemCardView>(ResourcePath);
		if (prefab == null)
		{
			// 참고 탭은 카드를 수십 장 만들므로 한 번만 알린다
			if (!warned) Debug.LogWarning("[ItemCardView] Resources/UI/ItemCard 프리팹이 없음. 메뉴 Invoke → UI → 설명 카드 만들기를 누르세요.");
			warned = true;
			return null;
		}
		return Instantiate(prefab, parent);
	}

	void Awake() => Wire();

	void Wire()
	{
		if (wired) return;
		wired = true;
		fit = GetComponent<LayoutElement>();
		if (detailsButton != null) detailsButton.onClick.AddListener(OnDetailsClicked);
		if (prevButton != null) prevButton.onClick.AddListener(() => ShowPage(index - 1));
		if (nextButton != null) nextButton.onClick.AddListener(() => ShowPage(index + 1));
		foreach (Image template in new[] { rowTemplate, valueTemplate, innerTemplate, bottomTemplate })
			if (template != null) template.gameObject.SetActive(false);
		if (card != null)
			foreach (TMP_Text text in card.GetComponentsInChildren<TMP_Text>(true)) baseSizes[text] = text.fontSize;
	}

	// 글자 크기 맞추기 -----------------------------------------------------------------
	// 게임 글꼴(Galmuri11)은 12픽셀로 그린 픽셀 글꼴이라, 화면에서 12픽셀의 정수배가 아니면 1픽셀 획(i, l, 받침)이 빠져 보인다.
	// 카드는 놓인 곳마다 배율이 달라서, 화면 픽셀로 따져 가장 가까운 12의 배수가 되게 글자마다 크기를 고친다 (프리팹에 적은 크기를 기준으로)

	const float FontPixels = 12f;
	readonly Dictionary<TMP_Text, float> baseSizes = new Dictionary<TMP_Text, float>();
	float snappedFor = -1f;

	// 배율이 바뀌었으면 글자 크기를 고치고 true
	bool SnapFonts(float screenPerUnit)
	{
		if (screenPerUnit <= 0 || Mathf.Approximately(snappedFor, screenPerUnit)) return false;
		snappedFor = screenPerUnit;
		foreach (KeyValuePair<TMP_Text, float> pair in baseSizes)
			if (pair.Key != null) pair.Key.fontSize = SnappedSize(pair.Value, screenPerUnit);
		return true;
	}

	static float SnappedSize(float size, float screenPerUnit)
	{
		float pixels = Mathf.Max(FontPixels, Mathf.Round(size * screenPerUnit / FontPixels) * FontPixels);
		return pixels / screenPerUnit;
	}

	// 카드 한 칸이 화면에서 몇 픽셀인지 (캔버스 배율 × 카드 배율)
	float ScreenPerUnit(float scale)
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		return canvas == null ? 0f : canvas.rootCanvas.scaleFactor * scale;
	}

	/// <summary>
	/// 머리줄. text가 비어 있으면 머리줄을 숨긴다 (스킬 트리 설명 칸, 참고 탭). onClose가 null이면 ✕를 숨긴다.
	/// </summary>
	public void SetHeader(string text, Action onClose)
	{
		Wire();
		if (header != null) header.SetActive(!string.IsNullOrEmpty(text));
		if (headerText != null) headerText.text = text ?? "";
		if (closeButton != null)
		{
			closeButton.onClick.RemoveAllListeners();
			closeButton.gameObject.SetActive(onClose != null);
			if (onClose != null) closeButton.onClick.AddListener(() => onClose());
		}
	}

	/// <summary>
	/// 카드 여러 장을 보인다. 첫 장부터, 접힌 채로. 비어 있으면 카드를 숨긴다.
	/// </summary>
	public void Show(IReadOnlyList<BlockReferenceEntry> list, int start = 0)
	{
		Wire();
		entries = list;
		bool any = list != null && list.Count > 0;
		if (card != null) card.gameObject.SetActive(any);
		if (!any) { Refit(); return; }
		ShowPage(start);
	}

	/// <summary>카드 한 장을 보인다</summary>
	public void Show(BlockReferenceEntry entry) => Show(entry == null ? null : new[] { entry });

	void ShowPage(int page)
	{
		if (entries == null || entries.Count == 0) return;
		index = Mathf.Clamp(page, 0, entries.Count - 1);
		BlockReferenceEntry entry = entries[index];

		bool korean = !string.IsNullOrEmpty(entry.KoreanName);
		titleName = $"<noparse>{(korean ? entry.KoreanName : entry.Name)}</noparse>";
		hasEnglish = korean;
		if (nameText != null) nameText.text = korean ? $"<noparse>{entry.Name}</noparse>" : "";
		if (categoryText != null) categoryText.text = $"<noparse>{entry.Category}</noparse>";
		if (categoryTag != null) categoryTag.color = entry.Picture != null ? BlockLabels.ColorOf(entry.Picture) : TypeColor;

		if (summaryText != null) summaryText.text = string.IsNullOrEmpty(entry.Summary) ? "" : $"<noparse>{entry.Summary}</noparse>";

		// 함수(사전 정의 함수, malloc·free·wait)는 「함수 원형」, 문법 블록은 「형식」. type은 그림 칸의 값이 형식이므로 줄을 숨긴다
		bool isType = entry.TypeLiteral != null;
		hasCode = !isType && !string.IsNullOrEmpty(entry.CodeText);
		if (codeLabel != null) codeLabel.text = entry.UsageTab == "함수 탭" ? "함수 원형" : "형식";
		if (codeText != null) codeText.text = BlockReference.ColorCode(entry.CodeText ?? "");

		if (pictureLabel != null) pictureLabel.text = isType ? "값 예" : "블록 사용 예";
		BuildPicture(entry);

		hasUsage = !string.IsNullOrEmpty(entry.UsageTab);
		if (usageTab != null) usageTab.text = entry.UsageTab ?? "";
		if (usageText != null) usageText.text = $"<noparse>{entry.UsageText ?? ""}</noparse>";

		string more = BuildExpanded(entry);
		if (expandedText != null) expandedText.text = more;
		hasMore = more.Length > 0;
		SetExpanded(false);

		if (pager != null) pager.SetActive(entries.Count > 1);
		if (pageText != null) pageText.text = $"{index + 1} / {entries.Count}";
		if (prevButton != null) prevButton.interactable = index > 0;
		if (nextButton != null) nextButton.interactable = index < entries.Count - 1;
		Refit();
	}

	// 블록 그림 -----------------------------------------------------------------
	// 편집 화면의 블록(BlockView)은 편집 화면 없이 만들 수 없어서(로비 스킬 트리), 같은 색·모양의 그림만 따로 그린다.
	// 문장 블록은 한 줄, 값 블록은 알약, ㄷ자 블록(if, for, while)은 머리줄 + 안쪽 자리 + 아래 막대. 글자는 블록 목록과 같은 한 줄 글자(BlockLabels.TextOf)

	void BuildPicture(BlockReferenceEntry entry)
	{
		foreach (GameObject old in picture)
		{
			if (old == null) continue;
			foreach (TMP_Text oldText in old.GetComponentsInChildren<TMP_Text>(true)) baseSizes.Remove(oldText);
			Destroy(old);
		}
		picture.Clear();
		if (pictureBox == null) return;

		if (entry.TypeLiteral != null)
		{
			AddPiece(valueTemplate, BlockReference.ColorCode(entry.TypeLiteral), Tint(TypeColor, ValueAlpha));
			return;
		}
		BlockData block = entry.Picture;
		if (block == null) return;

		Color color = BlockLabels.ColorOf(block);
		string text = $"<noparse>{BlockLabels.TextOf(block)}</noparse>";
		if (IsValueBlock(block))
		{
			AddPiece(valueTemplate, text, Tint(color, ValueAlpha));
			return;
		}
		Color fill = Tint(color, RowAlpha);
		AddPiece(rowTemplate, text, fill);
		if (block.InnerBlocks == null) return;
		AddPiece(innerTemplate, "<noparse>body</noparse>", fill);
		AddPiece(bottomTemplate, null, fill);
	}

	void AddPiece(Image template, string text, Color color)
	{
		if (template == null) return;
		Image piece = Instantiate(template, pictureBox);
		piece.gameObject.SetActive(true);
		piece.color = color;
		TMP_Text label = piece.GetComponentInChildren<TMP_Text>(true);
		if (label != null)
		{
			label.text = text ?? "";
			TMP_Text source = template.GetComponentInChildren<TMP_Text>(true);
			if (source != null && baseSizes.TryGetValue(source, out float size))
			{
				baseSizes[label] = size;
				if (snappedFor > 0) label.fontSize = SnappedSize(size, snappedFor);
			}
		}
		picture.Add(piece.gameObject);
	}

	// 값을 만드는 블록 (알약 모양). BlockView.IsValueBlock과 같은 분류
	static bool IsValueBlock(BlockData block) =>
		block.Kind is BlockKind.Arithmetic or BlockKind.Compare or BlockKind.Logic or BlockKind.Not or BlockKind.ArrayGet or BlockKind.Malloc
			or BlockKind.ListLen;

	static Color Tint(Color color, float alpha)
	{
		Color mixed = Color.Lerp(Backdrop, color, alpha);
		Color.RGBToHSV(mixed, out float h, out float s, out float v);
		// BlockView는 HSL 채도를 올린다. 여기서는 그림 하나만 보여 주므로 HSV로 가깝게 맞춘다
		mixed = Color.HSVToRGB(h, Mathf.Clamp01(s * Saturation * 0.75f), v);
		mixed.a = 1f;
		return mixed;
	}

	// 자세히 ---------------------------------------------------------------------

	// 참고 카드(ReferenceCardView)와 같은 모양: 항목마다 「이름」 줄과 내용, 사이는 한 줄 비움. 하는 일은 첫 화면에 있으므로 넣지 않는다
	static string BuildExpanded(BlockReferenceEntry entry)
	{
		var text = new StringBuilder();
		for (int i = 0; entry.Sections != null && i < entry.Sections.Count; i++)
		{
			ReferenceSection section = entry.Sections[i];
			if (text.Length > 0) text.Append("\n\n");
			text.Append($"<color={SectionColor}>{section.Title}</color>");
			if (section.Items != null)
			{
				foreach ((string name, string body) in section.Items)
				{
					if (string.IsNullOrEmpty(name)) { text.Append($"\n- <noparse>{body}</noparse>"); continue; }
					text.Append($"\n- {BlockReference.ColorCode(name)}");
					if (!string.IsNullOrEmpty(body)) text.Append($": <noparse>{body}</noparse>");
				}
				continue;
			}
			if (!string.IsNullOrEmpty(section.Body)) text.Append($"\n<noparse>{section.Body}</noparse>");
			if (!string.IsNullOrEmpty(section.Code)) text.Append($"\n{BlockReference.ColorCode(section.Code)}");
			if (!string.IsNullOrEmpty(section.Result)) text.Append($"\n<color={LabelColor}>결과</color>  <noparse>{section.Result}</noparse>");
		}
		return text.ToString();
	}

	void ToggleDetails()
	{
		// 참고 탭처럼 카드가 여럿 늘어선 곳에서는 하나만 펼친다 (#489 참고 카드와 같은 규칙)
		if (!expanded && transform.parent != null)
		{
			foreach (Transform sibling in transform.parent)
				if (sibling != transform && sibling.TryGetComponent(out ItemCardView other) && other.expanded) other.SetExpanded(false);
		}
		SetExpanded(!expanded);
	}

	void SetExpanded(bool value)
	{
		expanded = value && expandedText != null && expandedText.text.Length > 0;
		ApplyOpen();
	}

	/// <summary>펼쳐져 있으면 true</summary>
	public bool Expanded => expanded;

	// 접은 카드 (참고 탭) -------------------------------------------------------------------
	// 참고 탭은 카드가 수십 장이라, 처음에는 이름 줄과 형식 한 줄만 보이는 목록으로 두고 누르면 그 카드만 첫 화면으로 펼친다 (사용자 요청 11/18).
	// 다시 누르면 접고, 다른 카드를 펼치면 펼쳐져 있던 카드는 접는다. 검색해서 이름으로 훑어보기 쉽게 하기 위함

	bool collapsible;
	bool open = true;
	bool hasUsage, hasMore, hasCode, hasEnglish;
	string titleName = "";
	RectOffset openPadding;
	float openSpacing;
	float openTitleSize;

	// 접힌 카드의 모양: 이름 한 줄 (글자는 펼친 카드보다 작게, 위아래 여백도 줄여 목록처럼 훑어보게)
	// RectOffset은 static readonly로 두면 MonoBehaviour를 만드는 중에 만들어져 Unity가 막는다 (set_left 예외, 박진우). 쓸 때마다 새로 만든다
	static RectOffset ClosedPadding => new RectOffset(9, 9, 5, 6);
	const float ClosedTitleSize = 9f;
	const string ToggleColor = "#5EF2D6";
	GameObject summaryLabel;

	/// <summary>
	/// true면 이름 줄과 형식만 보이는 접힌 카드로 시작하고, 카드를 누르면 펼친다 (참고 탭).
	/// </summary>
	public bool Collapsible
	{
		get => collapsible;
		set
		{
			collapsible = value;
			open = !value;
			if (!open) expanded = false;
			ApplyOpen();
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (!collapsible || eventData.button != PointerEventData.InputButton.Left) return;
		ToggleOpen();
	}

	// 아래 줄 글자 버튼: 접힌 카드면 「펼치기 >」(카드 펼치기), 펼친 카드면 「자세히 >」(나머지 내용 펼치기)
	void OnDetailsClicked()
	{
		if (collapsible && !open) ToggleOpen();
		else ToggleDetails();
	}

	void ToggleOpen()
	{
		bool next = !open;
		if (next && transform.parent != null)
		{
			foreach (Transform sibling in transform.parent)
				if (sibling != transform && sibling.TryGetComponent(out ItemCardView other) && other.collapsible && other.open) other.SetOpen(false);
		}
		SetOpen(next);
	}

	void SetOpen(bool value)
	{
		open = value;
		if (!open) expanded = false;
		ApplyOpen();
	}

	// 접힌 카드는 여백·이름 글자를 줄인다. 처음 부를 때 프리팹의 펼친 값을 기억해 둔다
	void SetClosedLook(bool closed)
	{
		if (card == null || !card.TryGetComponent(out VerticalLayoutGroup column)) return;
		if (openPadding == null)
		{
			openPadding = new RectOffset(column.padding.left, column.padding.right, column.padding.top, column.padding.bottom);
			openSpacing = column.spacing;
			if (titleText != null) openTitleSize = baseSizes.TryGetValue(titleText, out float size) ? size : titleText.fontSize;
		}
		column.padding = closed ? ClosedPadding : openPadding;
		column.spacing = closed ? 3f : openSpacing;
		if (titleText != null && openTitleSize > 0)
		{
			baseSizes[titleText] = closed ? ClosedTitleSize : openTitleSize;
			if (snappedFor > 0) titleText.fontSize = SnappedSize(baseSizes[titleText], snappedFor);
		}
	}

	// 펼침 상태대로 줄을 켜고 끈다. 접힌 카드는 이름 줄(이름, 영문 이름, 분류 태그)과 형식 줄만
	void ApplyOpen()
	{
		bool show = !collapsible || open;
		// 이름 앞 표시: 접힌 카드 「+」, 펼친 카드 「-」 (펼치고 접는 목록이라는 것을 알린다. ▸▾는 글꼴에 없다)
		if (titleText != null)
			titleText.text = collapsible ? $"<color={ToggleColor}>{(open ? "-" : "+")}</color> {titleName}" : titleName;
		if (nameText != null) nameText.gameObject.SetActive(show && hasEnglish);
		if (codeRow != null) codeRow.SetActive(show && hasCode);
		SetClosedLook(!show);
		if (summaryLabel == null && card != null)
		{
			Transform label = card.Find("SummaryLabel");
			if (label != null) summaryLabel = label.gameObject;
		}
		if (summaryLabel != null) summaryLabel.SetActive(show);
		if (summaryText != null) summaryText.gameObject.SetActive(show);
		if (pictureLabel != null) pictureLabel.gameObject.SetActive(show);
		if (pictureBox != null) pictureBox.gameObject.SetActive(show);
		if (usageRow != null) usageRow.SetActive(show && hasUsage);
		// 접힌 카드에도 아래 줄에 「펼치기 >」를 보여 눌러서 펼치는 카드인 것을 알린다 (사용자 요청 11/18: 토글인지 모르겠음)
		if (detailsButton != null) detailsButton.gameObject.SetActive(show && hasMore);
		if (expandedText != null) expandedText.gameObject.SetActive(show && expanded);
		if (detailsLabel != null) detailsLabel.text = expanded ? "접기 <" : "자세히 >"; // ▾·▸는 글꼴(Galmuri11)에 없다
		Refit();
	}

	/// <summary>
	/// 다른 글과 나란히 놓이는 카드(스킬 트리 설명 칸, 참고 탭)면 true. 글자를 팝업보다 조금 작게 (Compact Scale).
	/// </summary>
	public bool Compact
	{
		get => compact;
		set { compact = value; Fit(); }
	}

	// 크기 맞추기 -------------------------------------------------------------------

	void Refit()
	{
		if (card == null) return;
		LayoutRebuilder.ForceRebuildLayoutImmediate(card);
		Fit();
		if (transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
	}

	void OnRectTransformDimensionsChange() => Fit();

	void LateUpdate() => Fit();

	// 글자 크기를 어느 캔버스에서나 팝업과 같게(설명 칸 등 좁은 곳은 Compact Scale만큼 작게) 맞추고, 카드 가로는 놓인 자리의 가로에 맞춰 글을 다시 줄바꿈한다.
	// 카드는 640 기준 캔버스 단위로 만들었으므로, 놓인 캔버스의 기준 가로(로비 창 1920, 편집 화면 960)를 640으로 나눈 만큼 키운다.
	// 그만큼의 높이를 Layout Element로 알려 위아래 칸이 밀리게 한다
	void Fit()
	{
		if (card == null || fit == null) return;
		float width = ((RectTransform)transform).rect.width;
		if (width <= 0) return;
		float scale = CanvasUnit() * (compact ? compactScale : 1f);
		// 나란히 놓인 카드는 오른쪽에 조금 비운다 (참고 탭의 스크롤 막대에 카드 테두리가 붙어 잘려 보이지 않게)
		float inset = compact ? RightInset * CanvasUnit() : 0f;
		float cardWidth = Mathf.Max(MinWidth, (width - inset) / scale);
		if (!Mathf.Approximately(card.localScale.x, scale)) card.localScale = new Vector3(scale, scale, 1f);
		if (SnapFonts(ScreenPerUnit(scale))) LayoutRebuilder.ForceRebuildLayoutImmediate(card);
		if (!Mathf.Approximately(card.sizeDelta.x, cardWidth))
		{
			card.sizeDelta = new Vector2(cardWidth, card.sizeDelta.y);
			LayoutRebuilder.ForceRebuildLayoutImmediate(card);
		}
		float height = card.gameObject.activeSelf ? card.rect.height * scale : 0f;
		if (!Mathf.Approximately(fit.preferredHeight, height))
		{
			fit.preferredHeight = height;
			if (transform.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
		}
	}

	// 놓인 캔버스에서 640 기준 캔버스 한 칸이 몇 칸인지. 기준 가로로 화면을 맞추는 캔버스가 아니면 1
	float CanvasUnit()
	{
		Canvas canvas = GetComponentInParent<Canvas>();
		if (canvas == null) return 1f;
		CanvasScaler scaler = canvas.rootCanvas.GetComponent<CanvasScaler>();
		if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) return 1f;
		return scaler.referenceResolution.x / 640f;
	}
}
