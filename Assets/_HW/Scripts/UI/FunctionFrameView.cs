using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 편집 영역 위아래에 놓이는 함수 틀: 위에는 「반환 타입 이름(파라미터) {」, 아래에는 「}」. 보여 주기만 한다.
/// 블록은 이 두 줄 사이(함수 body)에만 놓는다. 편집 영역(BlockList)의 자식이 아니라 따로 둔 줄이다.
/// <para>
/// 프리팹 구성: 위 줄(이 컴포넌트가 붙음)에 ReturnButton(반환 타입, 누르면 BlockEditor가 드롭다운을 연다), Name, Params, Tag(Text - TextMeshPro).
/// 아래 줄에 「}」 글자. 프리팹은 메뉴 Invoke → UI → 편집·장착 화면 새 디자인으로 만들기가 만든다 (InvokeUIBuilder).
/// </para>
/// </summary>
public class FunctionFrameView : MonoBehaviour
{
	[SerializeField, Tooltip("반환 타입 버튼 (예: void ▾). 슬롯 함수는 고정이라 누를 수 없다")]
	Button returnButton;

	[SerializeField, Tooltip("반환 타입 버튼 글자")]
	TMP_Text returnLabel;

	[SerializeField, Tooltip("함수 이름")]
	TMP_Text nameText;

	[SerializeField, Tooltip("파라미터와 여는 중괄호 (예: ( ) {)")]
	TMP_Text paramsText;

	[SerializeField, Tooltip("오른쪽 안내 (슬롯 함수 · 이름과 반환 타입은 고정)")]
	TMP_Text tagText;

	[SerializeField, Tooltip("아래 줄의 닫는 중괄호 글자")]
	TMP_Text footerText;

	/// <summary>반환 타입 버튼. BlockEditor가 누를 때 할 일을 연결한다</summary>
	public Button ReturnButton => returnButton;

	/// <summary>
	/// 함수 틀을 보여 준다. parameters는 「int a, int b」처럼 이미 만든 글자이고, returnEditable이 false이면 반환 타입을 누를 수 없다.
	/// </summary>
	public void ShowFunction(string returnType, string name, string parameters, string tag, bool returnEditable)
	{
		if (returnButton != null)
		{
			returnButton.gameObject.SetActive(true);
			returnButton.interactable = returnEditable;
		}
		if (returnLabel != null) returnLabel.text = returnEditable ? returnType + " ▾" : returnType;
		if (nameText != null) nameText.text = name;
		if (paramsText != null) paramsText.text = $"({(string.IsNullOrEmpty(parameters) ? " " : parameters)}) {{";
		if (tagText != null) tagText.text = tag;
		if (footerText != null) footerText.text = "}";
	}

	/// <summary>
	/// 전역 변수를 고치는 동안의 틀: 함수가 아니므로 반환 타입과 중괄호 없이 이름과 안내만 보인다.
	/// </summary>
	public void ShowGlobals()
	{
		if (returnButton != null) returnButton.gameObject.SetActive(false);
		if (nameText != null) nameText.text = "Global Variables";
		if (paramsText != null) paramsText.text = "";
		if (tagText != null) tagText.text = "전역 변수 선언";
		if (footerText != null) footerText.text = "";
	}
}
