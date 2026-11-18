using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 상점 창 (#534, 게임 UI 캔버스 18~18-2). 문법 블록 4칸과 함수 4칸을 보이고, 구매·새로고침을 서버에 요청한다.
/// <para>
/// - 여는 법: 로비의 상점 자리(PanelStation)에서 ↑. 닫기는 ESC 또는 「닫기」.<br/>
/// - 칸·재료·가진 개수는 내 캐릭터의 PlayerProgress에서 읽고, Changed마다 다시 그린다. 판정과 저장은 서버가 한다 (PlayerProgress.TryBuyShopItem).<br/>
/// - 카드를 누르면 재료를 먼저 보고, 모자라면 카드를 흔들고(18-2) 아니면 구매 확인 창(18-1)을 띄운다.<br/>
/// - 버그 조각이 새로고침 비용보다 적으면 새로고침 버튼을 비활성화한다. 구매·새로고침이 끝나면 창 위쪽에 칩을 잠깐 띄운다.<br/>
/// - 「로그인 건너뛰기」 임시 계정: 가진 개수를 보이지 않는다 (개수를 쓰지 않음). 사면 재료만 빠진다.<br/>
/// - 모양은 메뉴 Invoke → UI → 상점 창 만들기(ShopUIBuilder)가 LobbyUI 프리팹 안에 만든다.
/// </para>
/// </summary>
public class ShopPanel : InputBlockingPanel
{
	const string HintText = "구매한 자리에는 다른 블록이나 함수가 새로 나타납니다. 사고 싶은 것이 없으면 새로고침할 수 있습니다.";
	static readonly Color HintColor = new Color32(0xB8, 0xB3, 0xC7, 0xFF);
	static readonly Color ErrorColor = new Color32(0xFF, 0x9B, 0x8A, 0xFF);
	const string AccentHex = "#F2A65A";

	[SerializeField, Tooltip("카드 8장. 0~3 문법 블록, 4~7 함수 (ShopRules)")] ShopCardView[] cards = new ShopCardView[ShopRules.SlotCount];
	[SerializeField] TMP_Text bugFragmentText;
	[SerializeField] TMP_Text bossCoreText;
	[SerializeField] Button closeButton;
	[SerializeField] Button refreshButton;
	[SerializeField, Tooltip("새로고침 버튼 안의 비용 글자")] TMP_Text refreshCostText;
	[SerializeField, Tooltip("아래 줄 안내. 거부되면 이유를 빨갛게")] TMP_Text hintText;
	[SerializeField] ShopConfirmDialog confirm;
	[SerializeField, Tooltip("창 위쪽 가운데의 알림 칩. 구매·새로고침이 끝나면 서서히 나타났다가 사라진다")] CanvasGroup chip;
	[SerializeField] TMP_Text chipText;
	[SerializeField, Min(0f), Tooltip("칩이 다 보이는 시간(초). 나타나고 사라지는 시간은 빼고")] float chipTime = 1.5f;
	[SerializeField, Min(0f), Tooltip("칩이 나타나는 시간(초)")] float chipFadeIn = 0.15f;
	[SerializeField, Min(0f), Tooltip("칩이 사라지는 시간(초)")] float chipFadeOut = 0.3f;
	[SerializeField, Tooltip("재료 그림. 빌더가 박진우의 material_{ID}.png 첫 장을 넣는다")] List<MaterialIcon> materialIcons = new List<MaterialIcon>();

	[Serializable]
	public struct MaterialIcon
	{
		public string MaterialId;
		public Sprite Icon;
	}

	PlayerProgress progress;
	bool wired;
	// 구매를 요청한 항목의 이름. 서버 답이 올 때는 산 칸에 이미 새 상품이 올라와 있을 수 있어서, 요청할 때 적어 둔다
	string pendingBuyName;
	Coroutine chipShowing;

	// 로비 창은 HUD를 숨기지 않는다 (SkillTreePanel과 같음, #431)
	protected override bool HidesHud => false;

	void Start() => Wire();

	void OnDestroy() => Unbind();

	/// <summary>상점 창을 연다. 로비의 상점 자리(PanelStation)가 부른다.</summary>
	public override void OpenFor(PlayerInputReader player)
	{
		if (IsOpen) return;
		Wire();
		Show(player);
		Bind();
		SetHint(HintText, false);
		HideChip();
		if (confirm != null) confirm.Close();
		// 칸이 없으면(처음 여는 캐릭터) 서버가 뽑는다. 이미 있으면 그대로 (열 때마다 바뀌지 않음)
		if (progress != null) progress.RequestOpenShop();
		Redraw();
	}

	/// <summary>창을 닫는다. 확인 창도 닫는다. 「닫기」 버튼과 ESC가 부른다.</summary>
	public override void Close()
	{
		if (confirm != null) confirm.Close();
		HideChip();
		Unbind();
		base.Close();
	}

	// 버튼은 코드로 한 번만 붙인다 (빌더가 다시 만들어도 연결이 끊기지 않게)
	void Wire()
	{
		if (wired) return;
		wired = true;
		for (int i = 0; i < cards.Length; i++)
		{
			int slot = i;
			if (cards[i] != null && cards[i].Button != null) cards[i].Button.onClick.AddListener(() => OnCardClicked(slot));
		}
		if (closeButton != null) closeButton.onClick.AddListener(Close);
		if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshClicked);
	}

	void Bind()
	{
		Unbind();
		if (NetworkClient.localPlayer == null || !NetworkClient.localPlayer.TryGetComponent(out progress)) return;
		progress.Changed += Redraw;
		progress.ShopAnswered += OnAnswered;
	}

	void Unbind()
	{
		if (progress == null) return;
		progress.Changed -= Redraw;
		progress.ShopAnswered -= OnAnswered;
		progress = null;
	}

	// 실제 계정이면 true. 임시 계정은 가진 개수를 쓰지 않으므로 보이지 않는다
	static bool UsesCounts =>
		NetworkClient.localPlayer != null && NetworkClient.localPlayer.TryGetComponent(out FunctionExecutorNetwork network) && network.UsesServerData;

	void Redraw()
	{
		if (!IsOpen) return;
		if (bugFragmentText != null) bugFragmentText.text = $"버그 조각 <b>{Count(ShopRules.BugFragment)}</b>";
		if (bossCoreText != null) bossCoreText.text = $"보스 코어 <b>{Count(ShopRules.BossCore)}</b>";
		if (refreshCostText != null) refreshCostText.text = PriceLine(ShopRules.RefreshCost);
		// 버그 조각이 모자라면 새로고침 버튼을 누를 수 없게 한다 (Button의 disabledColor로 흐려짐)
		if (refreshButton != null) refreshButton.interactable = progress != null && ShortMaterials(ShopRules.RefreshCost).Count == 0;

		IReadOnlyList<ShopOffer> offers = progress != null ? progress.ShopOffers : null;
		bool counts = UsesCounts;
		for (int i = 0; i < cards.Length; i++)
		{
			if (cards[i] == null) continue;
			if (offers == null || offers.Count != ShopRules.SlotCount) cards[i].ShowEmpty();
			else cards[i].Show(offers[i], counts ? progress.GetItemCount(offers[i].ItemId) : -1, IconOf);
		}
	}

	void OnCardClicked(int slot)
	{
		if (progress == null || (confirm != null && confirm.IsOpen)) return;
		IReadOnlyList<ShopOffer> offers = progress.ShopOffers;
		if (offers.Count != ShopRules.SlotCount || offers[slot].IsEmpty) return;
		ShopOffer offer = offers[slot];

		bool counts = UsesCounts;
		int have = progress.GetItemCount(offer.ItemId);
		if (counts && have >= ItemCountRules.MaxCount)
		{
			cards[slot].Flash(null);
			SetHint($"{ShopItemInfo.DisplayNameOf(offer.ItemId)}: 이미 {ItemCountRules.MaxCount}개를 가지고 있습니다.", true);
			return;
		}
		List<string> shortMaterials = ShortMaterials(offer.Price);
		if (shortMaterials.Count > 0)
		{
			cards[slot].Flash(shortMaterials);
			SetHint("재료가 모자랍니다.", true);
			return;
		}

		SetHint(HintText, false);
		string name = ShopItemInfo.DisplayNameOf(offer.ItemId);
		string kind = ShopItemInfo.KindWordOf(slot);
		string titleText = $"<color={AccentHex}>{name}</color> {kind} 1개를 구매하시겠습니까?";
		// 게임 글꼴에 → 글자가 없어서 >로 적는다 (ShopItemInfo.PriceLineOf)
		string ownedLine = counts ? $"가진 개수 {have} > {Math.Min(have + 1, ItemCountRules.MaxCount)}" : "";
		if (confirm != null) confirm.Open(titleText, offer.Price, IconOf, ownedLine, () => Buy(slot, $"{name} {kind}"));
		else Buy(slot, $"{name} {kind}");
	}

	void Buy(int slot, string itemName)
	{
		if (progress == null) return;
		pendingBuyName = itemName;
		progress.RequestBuyShopItem(slot);
	}

	void OnRefreshClicked()
	{
		if (progress == null || (confirm != null && confirm.IsOpen)) return;
		if (ShortMaterials(ShopRules.RefreshCost).Count > 0)
		{
			SetHint("새로고침할 버그 조각이 모자랍니다.", true);
			return;
		}
		progress.RequestRefreshShop();
	}

	// 서버가 거부하면 이유를 아래 줄에 보인다. 화면이 먼저 막지만, 그사이 재료가 바뀌었거나 서버 값이 다를 수 있다
	void OnAnswered(ShopRequest request, int slot, ShopResult result)
	{
		if (result == ShopResult.Done)
		{
			if (request == ShopRequest.Open) return;
			SetHint(HintText, false);
			// 칩 문장: 「상처 가르기 함수를 구매했습니다.」, 「새로고침했습니다.」
			if (request == ShopRequest.Buy)
				ShowChip(string.IsNullOrEmpty(pendingBuyName) ? "구매했습니다." : $"{pendingBuyName}{ObjectParticle(pendingBuyName)} 구매했습니다.");
			else ShowChip("새로고침했습니다.");
			pendingBuyName = null;
			return;
		}
		if (request == ShopRequest.Buy) pendingBuyName = null;
		SetHint(result switch
		{
			ShopResult.MaterialShort => request == ShopRequest.Refresh ? "새로고침할 버그 조각이 모자랍니다." : "재료가 모자랍니다.",
			ShopResult.MaxCount => $"이미 {ItemCountRules.MaxCount}개를 가지고 있습니다.",
			ShopResult.EmptySlot => "빈 칸입니다.",
			_ => "지금은 살 수 없습니다.",
		}, true);
	}

	void SetHint(string text, bool error)
	{
		if (hintText == null) return;
		hintText.text = text;
		hintText.color = error ? ErrorColor : HintColor;
	}

	void ShowChip(string text)
	{
		if (chip == null) return;
		if (chipText != null) chipText.text = text;
		if (chipShowing != null) StopCoroutine(chipShowing);
		chip.gameObject.SetActive(true);
		chipShowing = StartCoroutine(FadeChip());
	}

	// 지금 투명도에서 시작해 나타나고, 머문 뒤, 사라진다. 연달아 사면 사라지던 칩이 다시 나타난다
	IEnumerator FadeChip()
	{
		for (float from = chip.alpha, t = 0f; t < chipFadeIn; t += Time.unscaledDeltaTime)
		{
			chip.alpha = Mathf.Lerp(from, 1f, t / chipFadeIn);
			yield return null;
		}
		chip.alpha = 1f;
		yield return new WaitForSecondsRealtime(chipTime);
		for (float t = 0f; t < chipFadeOut; t += Time.unscaledDeltaTime)
		{
			chip.alpha = 1f - t / chipFadeOut;
			yield return null;
		}
		chipShowing = null;
		HideChip();
	}

	void HideChip()
	{
		if (chipShowing != null) StopCoroutine(chipShowing);
		chipShowing = null;
		if (chip == null) return;
		chip.alpha = 0f;
		chip.gameObject.SetActive(false);
	}

	// 「~을/를」: 마지막 글자에 받침이 있으면 「을」. 칩 문장은 항상 「블록」·「함수」로 끝나지만 낱말이 바뀌어도 맞게 고른다
	static string ObjectParticle(string word)
	{
		char last = word[word.Length - 1];
		if (last < '가' || last > '힣') return "를";
		return (last - '가') % 28 != 0 ? "을" : "를";
	}

	int Count(string materialId) => progress != null ? progress.GetMaterialCount(materialId) : 0;

	// 가격 가운데 가진 개수가 모자란 재료 ID
	List<string> ShortMaterials(IReadOnlyList<ShopPrice> price)
	{
		var shortIds = new List<string>();
		foreach (KeyValuePair<string, int> pair in ShopPrices.Sum(price))
			if (Count(pair.Key) < pair.Value) shortIds.Add(pair.Key);
		return shortIds;
	}

	Sprite IconOf(string materialId)
	{
		foreach (MaterialIcon icon in materialIcons)
			if (icon.MaterialId == materialId) return icon.Icon;
		return null;
	}

	static IEnumerable<string> PriceLines(IReadOnlyList<ShopPrice> price)
	{
		foreach (ShopPrice line in price) yield return ShopItemInfo.PriceLineOf(line);
	}

	static string PriceLine(IReadOnlyList<ShopPrice> price) => string.Join("  ", PriceLines(price));
}
