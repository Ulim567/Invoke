using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상점 카드 한 장 (#534, 게임 UI 캔버스 18). 이름·표기, 가격, 가진 개수를 보이고, 살 수 없을 때 흔들린다 (18-2).
/// <para>
/// - 화면만 맡는다. 누르면 무엇을 할지는 ShopPanel이 Button의 onClick에 붙인다.<br/>
/// - 모양은 메뉴 Invoke → UI → 상점 창 만들기(ShopUIBuilder)가 만들고 칸을 채운다.
/// </para>
/// </summary>
public class ShopCardView : MonoBehaviour
{
	static readonly Color BorderNormal = new Color32(0x4A, 0x3D, 0x5C, 0xFF);
	static readonly Color ShortColor = new Color32(0xFF, 0x9B, 0x8A, 0xFF);
	// 이름·표기 칸의 바탕은 분류와 관계없이 하나의 어두운 색. 분류 색을 넓게 깔면(특히 노랑) 탁해져서
	// 그 위의 코드 색 글자가 읽히지 않으므로, 분류 색은 왼쪽 띠와 제목 글자에만 쓴다
	static readonly Color TileFill = new Color32(0x24, 0x1B, 0x36, 0xFF);

	[SerializeField] Button button;
	[SerializeField, Tooltip("카드 테두리 (바깥 단색). 살 수 없을 때 빨갛게")] Image border;
	[SerializeField, Tooltip("이름·표기 칸의 바탕. 분류와 관계없이 하나의 어두운 색")] Image tile;
	[SerializeField, Tooltip("이름·표기 칸 왼쪽의 세로 띠. 블록 분류 색")] Image stripe;
	[SerializeField] TMP_Text title;
	[SerializeField] TMP_Text notation;
	[SerializeField, Tooltip("가격 줄 (재료 그림 + 「버그 조각 ×7」). 가격 재료 수만큼 켠다")] Image[] priceIcons;
	[SerializeField] TMP_Text[] priceTexts;
	[SerializeField] TMP_Text owned;
	[SerializeField, Tooltip("이름·가격·가진 개수를 담은 부분. 빈 칸이면 끈다")] GameObject content;
	[SerializeField, Tooltip("빈 칸 글자")] TMP_Text emptyText;
	[SerializeField, Tooltip("흔들 부분")] RectTransform shakeTarget;

	ShopPrice[] shownPrice = Array.Empty<ShopPrice>();
	Coroutine flashing;
	Vector2 shakeBase;
	bool hasShakeBase;

	/// <summary>카드의 버튼. ShopPanel이 onClick에 구매를 붙인다.</summary>
	public Button Button => button;

	/// <summary>
	/// 상품을 보인다. ownedCount가 0보다 작으면 가진 개수 줄을 숨긴다(임시 계정). iconOf: 재료 ID → 그림.
	/// </summary>
	public void Show(ShopOffer offer, int ownedCount, Func<string, Sprite> iconOf)
	{
		if (offer.IsEmpty)
		{
			ShowEmpty();
			return;
		}
		if (content != null) content.SetActive(true);
		if (emptyText != null) emptyText.gameObject.SetActive(false);
		if (button != null) button.interactable = true;

		// 첫 줄은 한글 이름(함수), 둘째 줄은 원형. 원형에 영문 이름이 들어 있다
		if (title != null) title.text = ShopItemInfo.DisplayNameOf(offer.ItemId);
		if (notation != null) notation.text = ShopItemInfo.NotationOf(offer.ItemId);
		// 분류 색(블록 편집 화면과 같은 BlockLabels 색)은 왼쪽 띠와 제목 글자에만
		Color kindColor = ShopItemInfo.ColorOf(offer.ItemId);
		if (tile != null) tile.color = TileFill;
		if (stripe != null) stripe.color = kindColor;
		if (title != null) title.color = kindColor;

		shownPrice = offer.Price ?? Array.Empty<ShopPrice>();
		DrawPrice(null);

		if (owned != null)
		{
			owned.gameObject.SetActive(ownedCount >= 0);
			owned.text = $"가진 개수 <color=#FFFFFF><b>{ownedCount}</b></color>";
		}

		for (int i = 0; i < priceIcons.Length; i++)
		{
			if (priceIcons[i] == null) continue;
			Sprite icon = i < shownPrice.Length ? iconOf?.Invoke(shownPrice[i].MaterialId) : null;
			priceIcons[i].sprite = icon;
			priceIcons[i].enabled = icon != null;
		}
	}

	/// <summary>빈 칸 (올릴 후보가 없었음). 누를 수 없다.</summary>
	public void ShowEmpty()
	{
		if (content != null) content.SetActive(false);
		if (emptyText != null) emptyText.gameObject.SetActive(true);
		if (button != null) button.interactable = false;
		shownPrice = Array.Empty<ShopPrice>();
	}

	/// <summary>
	/// 살 수 없을 때: 카드를 흔들고 테두리를 빨갛게 한 뒤 되돌린다 (18-2). shortMaterials에 든 재료의 가격 줄만 빨갛게 한다.
	/// </summary>
	public void Flash(ICollection<string> shortMaterials)
	{
		if (!isActiveAndEnabled) return;
		if (flashing != null) StopCoroutine(flashing);
		flashing = StartCoroutine(FlashRoutine(shortMaterials));
	}

	void OnDisable()
	{
		// 창을 닫으면 흔들던 자리와 색을 되돌린다
		if (flashing != null) StopCoroutine(flashing);
		flashing = null;
		ResetFlash();
	}

	IEnumerator FlashRoutine(ICollection<string> shortMaterials)
	{
		RectTransform target = shakeTarget != null ? shakeTarget : (RectTransform)transform;
		if (!hasShakeBase)
		{
			shakeBase = target.anchoredPosition;
			hasShakeBase = true;
		}
		if (border != null) border.color = ShortColor;
		DrawPrice(shortMaterials);

		// 시안 18-2의 흔들기: 좌우 9픽셀(1920 캔버스)로 네 번, 0.36초
		const float Duration = 0.36f, Distance = 9f;
		for (float t = 0; t < Duration; t += Time.unscaledDeltaTime)
		{
			float x = Mathf.Sin(t / Duration * Mathf.PI * 8f) * Distance * (1f - t / Duration);
			target.anchoredPosition = shakeBase + new Vector2(x, 0f);
			yield return null;
		}
		target.anchoredPosition = shakeBase;

		// 빨간 표시는 잠깐 남겼다가 지운다 (「한 번 빨갛게 표시됨」)
		yield return new WaitForSecondsRealtime(0.6f);
		ResetFlash();
		flashing = null;
	}

	void ResetFlash()
	{
		if (hasShakeBase)
		{
			RectTransform target = shakeTarget != null ? shakeTarget : (RectTransform)transform;
			target.anchoredPosition = shakeBase;
		}
		if (border != null) border.color = BorderNormal;
		DrawPrice(null);
	}

	// 가격 줄 글자. 모자란 재료는 빨갛게
	void DrawPrice(ICollection<string> shortMaterials)
	{
		for (int i = 0; i < priceTexts.Length; i++)
		{
			if (priceTexts[i] == null) continue;
			bool used = i < shownPrice.Length;
			priceTexts[i].gameObject.SetActive(used);
			if (i < priceIcons.Length && priceIcons[i] != null) priceIcons[i].gameObject.SetActive(used);
			if (!used) continue;
			ShopPrice line = shownPrice[i];
			priceTexts[i].text = $"{ShopItemInfo.MaterialNameOf(line.MaterialId)} <b>×{line.Count}</b>";
			priceTexts[i].color = shortMaterials != null && shortMaterials.Contains(line.MaterialId) ? ShortColor : Color.white;
		}
	}
}
