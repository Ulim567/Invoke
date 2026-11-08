using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼 안 글자 뒤에 작고 흐리게 붙는 키 표시 (「Enter」, 「ESC」) (#430). 어떤 키로 그 버튼을 누를 수 있는지 보여 주기만 한다. 키 연결은 UIShortcut·InputBlockingPanel이 한다.
/// <para>
/// 붙이는 법<br/>
/// - 코드: KeyBadge.Attach(버튼, "Enter", 글꼴, 바탕 그림, 크기 배율). 640×360 캔버스는 배율 1, 1920×1080 캔버스는 3.<br/>
/// - 에디터: 메뉴 Invoke → UI → 키 표시 붙이기가 화면마다 붙인다 (KeyBadgeInstaller).<br/>
/// - 버튼 안에 「KeyBadge」 자식(바탕 + 글자)을 만든다. 이미 있으면 글자·크기만 고친다. 마우스를 막지 않는다. 크기는 레이아웃이 글자 길이로 잰다.<br/>
/// - 글자가 한 글자 이하인 버튼(X)은 버튼 왼쪽 바깥에 둔다: 「[ESC] [X]」. 지금 화면들의 X에는 붙이지 않는다 (제목줄 글자와 겹침).<br/>
/// - 「들어가기  Enter」처럼 버튼 글자 뒤에 테두리 없이 흐린 글자로 놓는다 (앱 메뉴의 단축키 표시처럼). 버튼에 가로 레이아웃(가운데 정렬, 간격 4)을 붙여 둘을 묶으므로 버튼 글자가 바뀌어도(준비 / 준비 취소) 맞는다.
/// </para>
/// </summary>
public class KeyBadge : MonoBehaviour
{
	public const string ChildName = "KeyBadge";

	static readonly Color Back = new Color32(0x1A, 0x1E, 0x3A, 0xF0);
	static readonly Color Label = new Color32(0xB8, 0xBE, 0xD6, 0xFF);

	[SerializeField, Tooltip("보여 줄 키 이름")] string key = "Enter";

	/// <summary>보여 주는 키 이름</summary>
	public string Key => key;

	/// <summary>
	/// target(버튼) 안 글자 뒤에 키 표시를 붙이고, 둘을 묶어 가운데에 둔다 (버튼에 가로 레이아웃). 이미 있으면 글자와 크기만 바꾼다.<br/>
	/// scale: 640×360 기준 크기의 배율 (1920×1080 캔버스는 3). sprite: 지금은 쓰지 않음 (null). 키 표시는 테두리 없이 버튼 글자 색의 50%.
	/// </summary>
	public static KeyBadge Attach(RectTransform target, string key, TMP_FontAsset font, Sprite sprite, float scale = 1f)
	{
		Transform found = target.Find(ChildName);
		RectTransform badge = found != null ? (RectTransform)found : NewUI(ChildName, target);
		KeyBadge component = badge.GetComponent<KeyBadge>() ?? badge.gameObject.AddComponent<KeyBadge>();
		component.key = key;

		Image image = badge.GetComponent<Image>() ?? badge.gameObject.AddComponent<Image>();
		image.sprite = sprite;
		image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
		image.pixelsPerUnitMultiplier = 1f / scale; // 테두리를 늘 1픽셀(640 기준)로
		image.raycastTarget = false;

		Transform textFound = badge.Find("Text");
		TMP_Text text = textFound != null ? textFound.GetComponent<TMP_Text>() : NewUI("Text", badge).gameObject.AddComponent<TextMeshProUGUI>();
		if (font != null) text.font = font;
		text.text = key;
		text.fontSize = 6f * scale;
		text.alignment = TextAlignmentOptions.Center;
		text.textWrappingMode = TextWrappingModes.NoWrap;
		text.margin = Vector4.zero;
		text.raycastTarget = false;

		// 키 표시 크기: 글자 길이 + 양옆 3픽셀, 높이 11 (640 기준). 레이아웃이 Play 중에 직접 잰다
		HorizontalLayoutGroup badgeLayout = badge.GetComponent<HorizontalLayoutGroup>() ?? badge.gameObject.AddComponent<HorizontalLayoutGroup>();
		int pad = 0;
		badgeLayout.padding = new RectOffset(pad, pad, 0, 0);
		badgeLayout.childAlignment = TextAnchor.MiddleCenter;
		badgeLayout.childControlWidth = badgeLayout.childControlHeight = true;
		badgeLayout.childForceExpandWidth = badgeLayout.childForceExpandHeight = false;
		LayoutElement size = badge.GetComponent<LayoutElement>() ?? badge.gameObject.AddComponent<LayoutElement>();
		size.ignoreLayout = false;
		size.minHeight = size.preferredHeight = 10f * scale;
		size.minWidth = 13f * scale;

		TMP_Text label = FindLabel(target, badge);
		// 색: 버튼 글자 색을 흐리게 따라간다 (주황 버튼은 어두운 선, 어두운 버튼은 밝은 선). 버튼에 녹아 보이게
		Color ink = label != null ? label.color : Label;
		// 테두리 없이 흐린 글자만: 「만들기  Enter」. 앱 메뉴의 단축키 표시(저장 ⌘S)처럼 버튼 글자 뒤에 작고 흐리게
		// 주황 버튼 위 어두운 글자는 흐리게 하면 잘 안 보여서 덜 흐리게 (밝은 글자 50%, 어두운 글자 75%)
		bool darkInk = ink.r * 0.3f + ink.g * 0.59f + ink.b * 0.11f < 0.5f;
		text.color = new Color(ink.r, ink.g, ink.b, darkInk ? 0.75f : 0.5f);
		image.color = new Color(0, 0, 0, 0);
		image.enabled = false;
		bool small = label == null || label.text.Trim().Length <= 1; // X 같은 아이콘 버튼
		ContentSizeFitter fitter = badge.GetComponent<ContentSizeFitter>();

		if (small)
		{
			// 버튼 왼쪽 바깥: [ESC] [X]
			if (fitter == null) fitter = badge.gameObject.AddComponent<ContentSizeFitter>();
			fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
			fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
			size.ignoreLayout = true;
			badge.anchorMin = badge.anchorMax = new Vector2(0f, 0.5f);
			badge.pivot = new Vector2(1f, 0.5f);
			badge.anchoredPosition = new Vector2(-4f * scale, 0f);
			return component;
		}

		// 버튼 안: [Enter] 들어가기. 버튼에 가로 레이아웃을 두고 키 표시 → 글자 순서로 가운데 정렬 (버튼 글자가 바뀌어도 맞음)
		if (fitter != null) Object.DestroyImmediate(fitter);
		badge.SetSiblingIndex(label.transform.GetSiblingIndex() + 1); // 버튼 글자 뒤
		HorizontalLayoutGroup row = target.GetComponent<HorizontalLayoutGroup>() ?? target.gameObject.AddComponent<HorizontalLayoutGroup>();
		row.padding = new RectOffset();
		row.spacing = 5f * scale;
		row.childAlignment = TextAnchor.MiddleCenter;
		row.childControlWidth = row.childControlHeight = true;
		row.childForceExpandWidth = row.childForceExpandHeight = false;
		Vector4 margin = label.margin;
		margin.x = 0f;
		label.margin = margin;
		return component;
	}

	// 버튼의 글자 (키 표시 안의 글자는 빼고)
	static TMP_Text FindLabel(Transform button, Transform badge)
	{
		foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
			if (!text.transform.IsChildOf(badge)) return text;
		return null;
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}
}
