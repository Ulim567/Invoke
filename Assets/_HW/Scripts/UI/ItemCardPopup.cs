using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 가운데에 뜨는 설명 카드 (#544). 해금 직후 카드(캔버스 9-3)와, 블록 목록·함수 탭 항목을 눌렀을 때의 카드가 쓴다.
/// <para>
/// - 화면을 어둡게 덮고 가운데에 설명 카드(ItemCardView) 한 장. 카드가 길면 그 안에서 세로로 스크롤한다.<br/>
/// - 닫기: ESC, 카드의 ✕, 카드 밖 어두운 곳 누르기. ESC는 맨 위 창 하나만 받으므로(UIShortcut) 아래의 스킬 트리·편집 화면은 닫히지 않는다.<br/>
/// - 처음 부를 때 Resources/UI/ItemCardPopup 프리팹을 한 번 만들어 두고(장면을 옮겨도 남김) 다시 쓴다.
///   캔버스 높이(Sorting Order 30)는 편집 화면·로비 창(10)보다 위, 설정(40)·로딩(100)보다 아래.<br/>
/// - 모양: 메뉴 Invoke → UI → 설명 카드 만들기(ItemCardUIBuilder).
/// </para>
/// </summary>
public class ItemCardPopup : MonoBehaviour
{
	const string ResourcePath = "UI/ItemCardPopup";

	[SerializeField, Tooltip("열고 닫는 부분 (어두운 판 + 카드). UIShortcut을 붙여 둔다")] GameObject root;
	[SerializeField, Tooltip("카드 밖 어두운 판. 누르면 닫는다")] Button dim;
	[SerializeField, Tooltip("카드를 넣을 곳 (Scroll Rect의 Content)")] RectTransform cardSlot;
	[SerializeField, Tooltip("카드가 화면보다 길 때 쓰는 스크롤")] ScrollRect scroll;
	[SerializeField, Tooltip("스크롤 칸 (높이를 카드에 맞추고, 최대 Max Height)")] RectTransform viewport;
	[SerializeField, Min(100)] float maxHeight = 330f;

	static ItemCardPopup instance;
	ItemCardView card;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => instance = null;

	/// <summary>떠 있으면 true</summary>
	public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

	/// <summary>
	/// 카드를 띄운다. entries가 여러 장이면 「&lt; 1 / 3 &gt;」로 넘긴다. 비어 있으면 아무것도 하지 않는다.
	/// headerText는 머리줄 글 (예: 「> 스킬 해금!」, 「> 블록 설명」).
	/// </summary>
	public static void Show(IReadOnlyList<BlockReferenceEntry> entries, string headerText)
	{
		if (entries == null || entries.Count == 0) return;
		if (instance == null)
		{
			var prefab = Resources.Load<ItemCardPopup>(ResourcePath);
			if (prefab == null)
			{
				Debug.LogWarning("[ItemCardPopup] Resources/UI/ItemCardPopup 프리팹이 없음. 메뉴 Invoke → UI → 설명 카드 만들기를 누르세요.");
				return;
			}
			instance = Instantiate(prefab);
			instance.name = prefab.name;
			DontDestroyOnLoad(instance.gameObject);
		}
		instance.Open(entries, headerText);
	}

	/// <summary>카드 한 장을 띄운다</summary>
	public static void Show(BlockReferenceEntry entry, string headerText)
	{
		if (entry != null) Show(new[] { entry }, headerText);
	}

	/// <summary>떠 있으면 닫는다</summary>
	public static void Hide()
	{
		if (instance != null) instance.Close();
	}

	void Awake()
	{
		if (root != null)
		{
			if (!root.TryGetComponent(out UIShortcut shortcut)) shortcut = root.AddComponent<UIShortcut>();
			shortcut.OnCancel.AddListener(Close);
			root.SetActive(false);
		}
		if (dim != null) dim.onClick.AddListener(Close);
	}

	void Open(IReadOnlyList<BlockReferenceEntry> entries, string headerText)
	{
		if (card == null && cardSlot != null) card = ItemCardView.Create(cardSlot);
		if (card == null) return;
		card.SetHeader(headerText, Close);
		card.Show(entries);
		if (root != null)
		{
			// 이미 떠 있어도 껐다 켜서 UIShortcut의 맨 위 창이 되게 한다
			root.SetActive(false);
			root.SetActive(true);
		}
		if (scroll != null) scroll.verticalNormalizedPosition = 1f;
	}

	/// <summary>닫는다. 카드의 ✕, ESC, 어두운 판이 부른다</summary>
	public void Close()
	{
		if (root != null) root.SetActive(false);
	}

	// 스크롤 칸 높이를 카드에 맞춘다 (짧은 카드는 화면 가운데, 긴 카드는 Max Height까지만 보이고 스크롤)
	void LateUpdate()
	{
		if (viewport == null || cardSlot == null || root == null || !root.activeSelf) return;
		float height = Mathf.Min(cardSlot.rect.height, maxHeight);
		if (!Mathf.Approximately(viewport.sizeDelta.y, height)) viewport.sizeDelta = new Vector2(viewport.sizeDelta.x, height);
	}
}
