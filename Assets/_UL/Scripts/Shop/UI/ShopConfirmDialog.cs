using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 상점의 구매 확인 창 (#534, 게임 UI 캔버스 18-1). 「For 블록 1개를 구매하시겠습니까?」, 소비하는 재료, 가진 개수 변화를 보이고 예·아니오를 받는다.
/// <para>
/// - 이 오브젝트에 UIShortcut을 붙여 Enter = 예, ESC = 아니오. 상점 창보다 나중에 켜지므로 떠 있는 동안 키는 이 창이 먼저 받는다 (UIShortcut 규칙).<br/>
/// - 처음에는 꺼 둔다. 모양은 메뉴 Invoke → UI → 상점 창 만들기(ShopUIBuilder)가 만든다.
/// </para>
/// </summary>
public class ShopConfirmDialog : MonoBehaviour
{
	[SerializeField] TMP_Text title;
	[SerializeField, Tooltip("소비하는 재료 줄의 재료 그림. 가격 재료 수만큼 켠다")] Image[] costIcons;
	[SerializeField, Tooltip("소비하는 재료 줄의 글자 (「버그 조각 ×12」)")] TMP_Text[] costTexts;
	[SerializeField, Tooltip("「가진 개수 3 → 4」. 비우면 숨긴다")] TMP_Text owned;
	[SerializeField] Button yesButton;
	[SerializeField] Button noButton;

	Action onYes;
	bool wired;

	/// <summary>창이 떠 있으면 true.</summary>
	public bool IsOpen => gameObject.activeSelf;

	/// <summary>
	/// 창을 띄운다. 예(Enter)를 누르면 창을 닫고 yes를 부른다. 아니오(ESC)는 닫기만 한다.<br/>
	/// price: 소비하는 재료. 재료마다 한 줄에 그림(iconOf: 재료 ID → 그림)과 이름·개수를 보인다.<br/>
	/// ownedLine이 비어 있으면 가진 개수 줄을 숨긴다 (임시 계정).
	/// </summary>
	public void Open(string titleText, IReadOnlyList<ShopPrice> price, Func<string, Sprite> iconOf, string ownedLine, Action yes)
	{
		Wire();
		onYes = yes;
		if (title != null) title.text = titleText;
		int lines = price != null ? price.Count : 0;
		for (int i = 0; i < costTexts.Length; i++)
		{
			bool used = i < lines;
			if (costTexts[i] != null)
			{
				costTexts[i].gameObject.SetActive(used);
				if (used) costTexts[i].text = $"{ShopItemInfo.MaterialNameOf(price[i].MaterialId)} <b>×{price[i].Count}</b>";
			}
			if (i < costIcons.Length && costIcons[i] != null)
			{
				Sprite icon = used ? iconOf?.Invoke(price[i].MaterialId) : null;
				costIcons[i].gameObject.SetActive(used);
				costIcons[i].sprite = icon;
				costIcons[i].enabled = icon != null;
			}
		}
		if (owned != null)
		{
			owned.gameObject.SetActive(!string.IsNullOrEmpty(ownedLine));
			owned.text = ownedLine ?? "";
		}
		gameObject.SetActive(true);
		// 마우스로 누른 카드 버튼이 선택된 채로 남으면, UIShortcut은 그 버튼이 Enter를 처리한다고 보고 양보한다.
		// 카드는 확인 창이 떠 있으면 아무것도 하지 않으므로 Enter가 사라진다. 선택을 지워 Enter가 이 창의 「예」로 오게 한다
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
	}

	/// <summary>아무것도 하지 않고 닫는다.</summary>
	public void Close()
	{
		onYes = null;
		gameObject.SetActive(false);
	}

	void Confirm()
	{
		Action yes = onYes;
		Close();
		yes?.Invoke();
	}

	// 버튼과 키를 한 번만 붙인다. 꺼진 채로 시작하므로 Awake 대신 처음 열 때 붙인다
	void Wire()
	{
		if (wired) return;
		wired = true;
		if (yesButton != null) yesButton.onClick.AddListener(Confirm);
		if (noButton != null) noButton.onClick.AddListener(Close);
		if (!TryGetComponent(out UIShortcut shortcut)) shortcut = gameObject.AddComponent<UIShortcut>();
		shortcut.OnSubmit.AddListener(Confirm);
		shortcut.OnCancel.AddListener(Close);
	}
}
