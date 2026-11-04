using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오류 창의 오류 하나 (ErrorItem 프리팹): 오류가 난 줄의 C 글자, 이유, 「블록 보기」 버튼. 보여 주기만 한다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_card) + Vertical Layout Group + 이 컴포넌트, 자식에 Bar(빨간 세로줄), Code, Message(Text - TextMeshPro), ShowButton(Button).
/// </para>
/// </summary>
public class ErrorItemView : MonoBehaviour
{
	[SerializeField, Tooltip("오류가 난 줄 (예: hp = hpp - 1;)")]
	TMP_Text codeText;

	[SerializeField, Tooltip("이유 (예: 선언하지 않은 변수 hpp)")]
	TMP_Text messageText;

	[SerializeField, Tooltip("「블록 보기」 버튼. 오류가 난 블록이 없으면 숨긴다")]
	Button showButton;

	/// <summary>
	/// 글자를 정한다. onShow가 있으면 「블록 보기」를 누를 때 부른다.
	/// </summary>
	public void Show(string code, string message, Action onShow)
	{
		if (codeText != null) codeText.text = code ?? "";
		if (messageText != null) messageText.text = message ?? "";
		if (showButton == null) return;
		showButton.onClick.RemoveAllListeners();
		showButton.gameObject.SetActive(onShow != null);
		if (onShow != null) showButton.onClick.AddListener(() => onShow());
	}
}
