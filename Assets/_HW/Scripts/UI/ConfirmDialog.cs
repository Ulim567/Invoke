using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 「예 / 아니오」를 묻는 확인창. 열려 있는 동안 Enter는 예, ESC는 아니오다. 편집 화면(BlockEditor)이 저장하지 않은 변경을 버리기 전에 묻는 데 쓴다.
/// <para>
/// 사용법: Show(문구, 예를 눌렀을 때 할 일)로 열면 된다. 아니오를 누르면 아무 일도 없이 닫힌다.<br/>
/// - 화면 전체를 덮는 어두운 바탕이 뒤의 버튼을 막는다 (프리팹 구성은 InvokeUIBuilder가 만든다).<br/>
/// - 열 때 EventSystem의 선택을 비운다. 선택된 버튼이 있으면 그 버튼이 Enter를 먼저 받아서 「예」가 눌리지 않기 때문이다 (UIShortcut 규칙).<br/>
/// - Enter·ESC는 이 오브젝트에 붙는 UIShortcut이 받는다. 나중에 열린 화면이 맨 위이므로 아래의 편집 화면은 반응하지 않는다.
/// </para>
/// </summary>
public class ConfirmDialog : MonoBehaviour
{
	[SerializeField, Tooltip("묻는 문구")]
	TMP_Text messageText;

	[SerializeField, Tooltip("예 버튼")]
	Button yesButton;

	[SerializeField, Tooltip("아니오 버튼")]
	Button noButton;

	Action onYes;
	bool initialized;
	TMP_Text yesLabel, noLabel;
	string defaultYesLabel, defaultNoLabel; // 프리팹에 적힌 버튼 글자 (「예 (Enter)」, 「아니오 (Esc)」)

	/// <summary>확인창이 열려 있으면 true</summary>
	public bool IsOpen => gameObject.activeSelf;

	// 꺼 둔 채로 시작하므로 처음 열 때 Awake가 불리고, 그때 연결한다
	void Awake() => Initialize();

	// Show가 버튼 글자를 바꾸기 전에 프리팹의 글자를 기억해야 하므로, Awake보다 먼저 Show가 불려도 여기서 연결한다
	void Initialize()
	{
		if (initialized) return;
		initialized = true;
		if (yesButton != null) yesButton.onClick.AddListener(Yes);
		if (noButton != null) noButton.onClick.AddListener(No);
		yesLabel = yesButton != null ? yesButton.GetComponentInChildren<TMP_Text>(true) : null;
		noLabel = noButton != null ? noButton.GetComponentInChildren<TMP_Text>(true) : null;
		defaultYesLabel = yesLabel != null ? yesLabel.text : null;
		defaultNoLabel = noLabel != null ? noLabel.text : null;

		if (!TryGetComponent(out UIShortcut shortcut)) shortcut = gameObject.AddComponent<UIShortcut>();
		shortcut.OnSubmit.AddListener(Yes);
		shortcut.OnCancel.AddListener(No);
	}

	/// <summary>
	/// 확인창을 연다. 예를 누르면(또는 Enter) 닫고 onYes를 부른다. 아니오(또는 ESC)는 닫기만 한다.
	/// yesText·noText를 주면 버튼 글자를 바꾼다 (예: 「나가기 (Enter)」 「취소 (Esc)」, #494). 주지 않으면 프리팹의 「예」 「아니오」.
	/// </summary>
	public void Show(string message, Action onYes, string yesText = null, string noText = null)
	{
		Initialize();
		this.onYes = onYes;
		if (messageText != null) messageText.text = message;
		if (yesLabel != null) yesLabel.text = yesText ?? defaultYesLabel;
		if (noLabel != null) noLabel.text = noText ?? defaultNoLabel;
		if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
		transform.SetAsLastSibling(); // 다른 UI 위에 그린다
		gameObject.SetActive(true);
	}

	/// <summary>확인창을 닫는다 (예 또는 아니오를 누르지 않고 화면이 바뀔 때)</summary>
	public void Hide()
	{
		onYes = null;
		gameObject.SetActive(false);
	}

	// 닫은 뒤에 할 일을 부른다. 할 일이 이 확인창을 다시 열어도 문제없도록 먼저 닫는다
	void Yes()
	{
		Action action = onYes;
		Hide();
		action?.Invoke();
	}

	void No() => Hide();
}
