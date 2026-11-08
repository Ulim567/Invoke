using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 입구 창(LobbyUI 프리팹의 EntrancePanel/Window)을 다른 창(계정 화면, 스킬 트리)과 같은 모양으로 바꾸는 에디터 도구 (#434). 게임에는 들어가지 않는다.
/// 메뉴 Invoke → UI → 로비 입구 창 시안대로. 여러 번 눌러도 된다.
/// <para>
/// - 있는 오브젝트(Title, Description, EnterButton, CloseButton)의 모양만 바꾼다. 버튼의 On Click()과 LobbyEntrancePanel의 칸 연결은 그대로 둔다.<br/>
/// - 바탕은 acc_panel(보라 바탕 + 민트 테두리), 들어가기는 주황 acc_button, 닫기는 테두리 버튼(set_button). LobbyUI 캔버스는 1920×1080이라 시안 값 × 3.
/// </para>
/// </summary>
public static class LobbyEntranceUIBuilder
{
	const string PrefabPath = "Assets/_HW/Lobby/LobbyUI.prefab";
	const string FontPath = "Assets/Fonts/Galmuri11 TMP.asset";
	const string Panel = "Assets/_HW/UI/Account/acc_panel.png";
	const string Orange = "Assets/_HW/UI/Account/acc_button.png";
	const string Outline = "Assets/_HW/UI/Settings/set_button.png";
	const string OutlineOn = "Assets/_HW/UI/Settings/set_button_selected.png";
	const string White = "Assets/_HW/UI/Settings/set_white.png";

	static readonly Color Teal = Hex("5EF2D6");
	static readonly Color Label = Hex("B8BED6");
	static readonly Color Muted = Hex("8A84A8");
	static readonly Color Line = Hex("443F53");
	static readonly Color Dark = Hex("1A1E3A");

	const float W = 720, H = 396; // 시안 240×132 × 3

	static TMP_FontAsset font;

	[MenuItem("Invoke/UI/로비 입구 창 시안대로")]
	public static void Build()
	{
		font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
		try
		{
			Transform panel = Find(root.transform, "EntrancePanel");
			Transform window = panel != null ? panel.Find("Window") : null;
			if (window == null)
			{
				Debug.LogError("[LobbyEntranceUIBuilder] LobbyUI 안에서 EntrancePanel/Window를 찾지 못함");
				return;
			}

			// 창: 보라 바탕 + 민트 테두리 (스킬 트리 창과 같은 두께)
			Image windowImage = SetImage(window.gameObject, Panel);
			windowImage.pixelsPerUnitMultiplier = 0.4f;
			windowImage.color = Color.white; // 예전 남색이 곱해지지 않게
			Place((RectTransform)window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(W, H));

			// 제목 「> 사냥터」 (글자는 LobbyEntrancePanel이 열 때 넣는다)
			TMP_Text title = window.Find("Title").GetComponent<TMP_Text>();
			Style(title, "> 입구", 33, Teal, TextAlignmentOptions.MidlineLeft, true);
			TopStretch(title.rectTransform, 36, 36, 21, 60);

			RectTransform headerLine = Child(window, "HeaderLine");
			TopStretch(headerLine, 0, 0, 96, 3);
			Solid(headerLine.gameObject, Line);

			// 설명
			TMP_Text description = window.Find("Description").GetComponent<TMP_Text>();
			Style(description, "설명", 24, Label, TextAlignmentOptions.TopLeft, false);
			description.textWrappingMode = TextWrappingModes.Normal;
			TopStretch(description.rectTransform, 36, 36, 129, 120);

			// 아래: 왼쪽에 키 안내, 오른쪽에 닫기 · 들어가기
			TMP_Text hint = GetOrText(window, "KeyHint", "Enter 들어가기   Esc 닫기", 18, Muted, TextAlignmentOptions.MidlineLeft);
			Place(hint.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 33), new Vector2(330, 60));

			Transform enter = window.Find("EnterButton");
			Button enterButton = SetButton(enter, Orange, null);
			ColorBlock colors = enterButton.colors;
			colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1);
			colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.85f);
			enterButton.colors = colors;
			Place((RectTransform)enter, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36, 33), new Vector2(195, 63));
			Style(enter.GetComponentInChildren<TMP_Text>(), "들어가기", 24, Dark, TextAlignmentOptions.Center, true);

			Transform close = window.Find("CloseButton");
			SetButton(close, Outline, OutlineOn);
			Place((RectTransform)close, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-36 - 195 - 18, 33), new Vector2(150, 63));
			Style(close.GetComponentInChildren<TMP_Text>(), "닫기", 24, Color.white, TextAlignmentOptions.Center, false);

			foreach (TMP_Text label in new[] { enter.GetComponentInChildren<TMP_Text>(), close.GetComponentInChildren<TMP_Text>() })
			{
				RectTransform r = label.rectTransform;
				r.anchorMin = Vector2.zero;
				r.anchorMax = Vector2.one;
				r.offsetMin = r.offsetMax = Vector2.zero;
			}

			PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
			Debug.Log("[LobbyEntranceUIBuilder] 로비 입구 창을 다른 창과 같은 모양으로 바꿈 (LobbyUI 프리팹)");
		}
		finally
		{
			PrefabUtility.UnloadPrefabContents(root);
		}
	}

	// 도구 ---------------------------------------------------------------------

	static Transform Find(Transform parent, string name)
	{
		if (parent.name == name) return parent;
		foreach (Transform child in parent)
		{
			Transform found = Find(child, name);
			if (found != null) return found;
		}
		return null;
	}

	static Button SetButton(Transform t, string sprite, string hoverSprite)
	{
		Image image = SetImage(t.gameObject, sprite);
		image.color = Color.white;
		image.pixelsPerUnitMultiplier = 0.4f;
		Button button = t.GetComponent<Button>();
		button.targetGraphic = image;
		if (hoverSprite != null)
		{
			Sprite on = AssetDatabase.LoadAssetAtPath<Sprite>(hoverSprite);
			button.transition = Selectable.Transition.SpriteSwap;
			button.spriteState = new SpriteState { highlightedSprite = on, selectedSprite = on, pressedSprite = on };
		}
		else button.transition = Selectable.Transition.ColorTint;
		return button;
	}

	static Image SetImage(GameObject go, string path)
	{
		Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (image.sprite == null) Debug.LogWarning($"[LobbyEntranceUIBuilder] {path}를 찾지 못함");
		image.type = Image.Type.Sliced;
		return image;
	}

	static void Solid(GameObject go, Color color)
	{
		Image image = go.GetComponent<Image>() ?? go.AddComponent<Image>();
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(White);
		image.type = Image.Type.Simple;
		image.color = color;
		image.raycastTarget = false;
	}

	static void Style(TMP_Text text, string value, float size, Color color, TextAlignmentOptions alignment, bool bold)
	{
		if (text == null) return;
		if (font != null) text.font = font;
		text.text = value;
		text.fontSize = size;
		text.color = color;
		text.alignment = alignment;
		text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
		text.extraPadding = false;
		text.raycastTarget = false;
	}

	static TMP_Text GetOrText(Transform parent, string name, string value, float size, Color color, TextAlignmentOptions alignment)
	{
		Transform found = parent.Find(name);
		TMP_Text text = found != null ? found.GetComponent<TMP_Text>() : NewUI(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
		Style(text, value, size, color, alignment, false);
		text.textWrappingMode = TextWrappingModes.NoWrap;
		return text;
	}

	static RectTransform Child(Transform parent, string name)
	{
		Transform t = parent.Find(name);
		return t != null ? (RectTransform)t : NewUI(name, parent);
	}

	static RectTransform NewUI(string name, Transform parent)
	{
		var go = new GameObject(name, typeof(RectTransform));
		go.layer = 5;
		go.transform.SetParent(parent, false);
		return (RectTransform)go.transform;
	}

	static void Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
	{
		r.anchorMin = r.anchorMax = anchor;
		r.pivot = pivot;
		r.sizeDelta = size;
		r.anchoredPosition = position;
	}

	static void TopStretch(RectTransform r, float left, float right, float top, float height)
	{
		r.anchorMin = new Vector2(0, 1);
		r.anchorMax = new Vector2(1, 1);
		r.pivot = new Vector2(0.5f, 1);
		r.offsetMin = new Vector2(left, -top - height);
		r.offsetMax = new Vector2(-right, -top);
	}

	static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out Color c) ? c : Color.white;
}
