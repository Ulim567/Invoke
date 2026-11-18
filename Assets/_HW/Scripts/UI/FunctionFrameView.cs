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

	[SerializeField, Tooltip("반환 타입 버튼 오른쪽의 ▾ 그림. 반환 타입을 고를 수 있을 때만 보인다")]
	GameObject returnArrow;

	[SerializeField, Tooltip("함수 이름")]
	TMP_Text nameText;

	[SerializeField, Tooltip("파라미터와 여는 중괄호 (예: ( ) {)")]
	TMP_Text paramsText;

	[SerializeField, Tooltip("오른쪽 빈 곳을 채우는 글자 칸. 함수 종류 안내는 위쪽 줄 상태 글자와 겹쳐 비워 둔다")]
	TMP_Text tagText;

	[SerializeField, Tooltip("아래 줄의 닫는 중괄호 글자")]
	TMP_Text footerText;

	// 반환 타입 버튼 글자 양옆 여백
	const float ReturnPadding = 6f;
	// ▾ 그림이 차지하는 오른쪽 너비 (글자와의 간격 4 + 그림 8 + 오른쪽 여백 6)
	const float ArrowSpace = 18f;

	/// <summary>반환 타입 버튼. BlockEditor가 누를 때 할 일을 연결한다</summary>
	public Button ReturnButton => returnButton;

	/// <summary>
	/// 함수 이름 자리에 이름 칸(BlockField)을 넣는다 (BlockEditor가 처음 한 번 부른다). 이름은 이 칸에만 보이고,
	/// 페이지 함수는 여기서 바로 고친다. 칸을 넣으면 원래의 이름 글자는 숨긴다 (이름이 두 번 보이지 않게)
	/// </summary>
	public void PlaceNameField(RectTransform field)
	{
		field.SetParent(transform, false);
		if (nameText != null)
		{
			field.SetSiblingIndex(nameText.transform.GetSiblingIndex());
			nameText.gameObject.SetActive(false);
		}
		// 안내 칸(오른쪽 빈 곳 채우기)과 아래 경계선 앞, 반환 타입 버튼 뒤
		else if (returnButton != null) field.SetSiblingIndex(returnButton.transform.GetSiblingIndex() + 1);
	}

	/// <summary>
	/// 함수 틀을 보여 준다. parameters는 「int a, int b」처럼 이미 만든 글자이고, returnEditable이 false이면 반환 타입을 누를 수 없다.
	/// 반환 타입을 고를 수 있으면 글자 오른쪽에 ▾ 그림을 보인다. 함수 종류(슬롯·페이지) 안내는 위쪽 줄 상태 글자가 보이므로 여기에는 없다.
	/// </summary>
	public void ShowFunction(string returnType, string name, string parameters, bool returnEditable)
	{
		if (returnButton != null)
		{
			returnButton.gameObject.SetActive(true);
			returnButton.interactable = returnEditable;
		}
		// ▾를 글자로 붙이지 않고 그림으로 보인다. 글꼴(Galmuri11 TMP)에 ▾ 글자가 없어 글자로는 보이지 않기 때문
		bool arrow = returnEditable && returnArrow != null;
		if (returnArrow != null) returnArrow.SetActive(returnEditable);
		if (returnLabel != null)
		{
			returnLabel.text = returnType;
			FitReturnButton(arrow);
		}
		if (nameText != null) nameText.text = name;
		if (paramsText != null) paramsText.text = $"({(string.IsNullOrEmpty(parameters) ? " " : parameters)}) {{";
		if (tagText != null) tagText.text = "";
		if (footerText != null) footerText.text = "}";
	}

	/// <summary>
	/// Python 함수의 틀 (#532): 「def 이름(a, b):」. 반환 타입이 없으므로 반환 타입 자리에 def를 누를 수 없게 보이고, 끝의 }가 없다(들여쓰기로 몸통을 나타냄).
	/// </summary>
	public void ShowPythonFunction(string name, string parameters)
	{
		ShowFunction("def", name, parameters, false);
		if (paramsText != null) paramsText.text = $"({parameters}):";
		if (footerText != null) footerText.text = "";
	}

	/// <summary>
	/// 전역 변수를 고치는 동안의 틀: 함수가 아니므로 반환 타입과 중괄호 없이 이름(Global Variables)만 보인다.
	/// </summary>
	public void ShowGlobals()
	{
		if (returnButton != null) returnButton.gameObject.SetActive(false);
		if (nameText != null) nameText.text = "Global Variables";
		if (paramsText != null) paramsText.text = "";
		if (tagText != null) tagText.text = "";
		if (footerText != null) footerText.text = "";
	}

	// 반환 타입 버튼 너비를 글자(+ ▾ 그림)에 맞춘다. 「float」처럼 글자가 길어져도 ▾가 잘리지 않게.
	// 글자 칸은 ▾ 자리를 뺀 왼쪽에 두어, 글자가 ▾와 겹치지 않고 남은 자리 가운데에 오게 한다
	void FitReturnButton(bool arrow)
	{
		float right = arrow ? ArrowSpace : ReturnPadding;
		RectTransform label = returnLabel.rectTransform;
		label.offsetMin = new Vector2(ReturnPadding, label.offsetMin.y);
		label.offsetMax = new Vector2(-right, label.offsetMax.y);
		if (returnButton == null || !returnButton.TryGetComponent(out LayoutElement size)) return;
		size.preferredWidth = Mathf.Max(size.minWidth, returnLabel.GetPreferredValues(returnLabel.text).x + ReturnPadding + right);
	}
}
