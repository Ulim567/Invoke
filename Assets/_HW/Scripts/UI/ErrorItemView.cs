using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 오류 창의 오류 하나 (ErrorItem 프리팹): 설명, 오류가 난 블록의 C 글자, 「> 함수 이름」 세 줄. 보여 주기만 한다.
/// 「> 함수 이름」을 누르면 BlockEditor가 그 함수를 열고 오류가 난 블록을 깜빡인다.
/// <para>
/// 프리팹 구성: 최상위에 Image(ui_card) + Vertical Layout Group + 이 컴포넌트, 자식에 Bar(빨간 세로줄), Message, Code(Text - TextMeshPro),
/// LocationButton(Button, 바탕 없이 청록 글자).
/// </para>
/// </summary>
public class ErrorItemView : MonoBehaviour
{
	[SerializeField, Tooltip("설명 (예: 선언하지 않은 식별자: hpp)")]
	TMP_Text messageText;

	[SerializeField, Tooltip("오류가 난 블록의 글자 (예: hp = hpp - 1;). 블록이 없는 오류면 숨긴다")]
	TMP_Text codeText;

	[SerializeField, Tooltip("「> 함수 이름」 버튼. 누르면 그 함수를 연다")]
	Button locationButton;

	[SerializeField, Tooltip("「> 함수 이름」 버튼의 글자")]
	TMP_Text locationText;

	/// <summary>
	/// 글자를 정한다. code가 비었으면 코드 줄을 숨긴다. onLocation이 있으면 「> 함수 이름」을 누를 때 부른다 (없으면 누를 수 없음).
	/// </summary>
	public void Show(string message, string code, string location, Action onLocation)
	{
		if (messageText != null) messageText.text = message ?? "";
		if (codeText != null)
		{
			codeText.text = code ?? "";
			codeText.gameObject.SetActive(!string.IsNullOrEmpty(code));
		}
		if (locationText != null) locationText.text = $"> {location}";
		if (locationButton == null) return;
		locationButton.gameObject.SetActive(!string.IsNullOrEmpty(location));
		locationButton.onClick.RemoveAllListeners();
		locationButton.interactable = onLocation != null;
		if (onLocation != null) locationButton.onClick.AddListener(() => onLocation());
	}
}
