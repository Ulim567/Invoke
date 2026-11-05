using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// 화면에 붙여서 Enter·ESC를 누르면 Inspector에서 연결한 함수를 부르는 컴포넌트.<br/>
/// 버튼이 없는 화면도 쓸 수 있게, 버튼 대신 함수를 연결한다.
/// <para>
/// 사용법<br/>
/// - 화면 스크립트가 SetActive로 여닫는 오브젝트(또는 그 자식)에 붙인다. 부모에 붙이면 화면이 닫혀도 알지 못한다.<br/>
/// - On Submit(Enter), On Cancel(ESC)에 부를 함수를 연결한다. 버튼이 있으면 그 버튼이 부르는 함수와 같은 함수를 연결한다.<br/>
/// - 다른 화면 위에 뜨는 UI(팝업, 창)에는 칸이 비어 있어도 붙인다. 붙이지 않으면 밑에 있는 화면이 키에 반응한다.
/// </para>
/// <para>
/// 동작 규칙<br/>
/// - 여러 화면이 열려 있으면 마지막으로 열린 화면 하나만 반응한다.<br/>
/// - 한 프레임에 한 화면만 반응한다. ESC로 맨 위 화면을 닫은 프레임에 그 밑의 화면까지 닫히지 않게 하기 위함이다.<br/>
/// - 선택된 UI가 그 키를 직접 처리하면(버튼·입력칸의 Enter, 입력칸·드롭다운의 ESC) 반응하지 않는다.
///   EventSystem이 선택된 UI에 같은 키를 보내므로, 두 동작이 함께 일어나지 않게 하기 위함이다.
/// </para>
/// <para>
/// 다른 키를 넣을 때 (예: Tab으로 다음 탭)<br/>
/// 1. InputSystem_Actions의 UI 액션 맵에 액션을 추가하고, Input Actions 편집 창에서 형식이 깨지지 않았는지 확인한다.<br/>
/// 2. GameInput.UI에 그 키의 값(예: NextTabPressed)을 추가한다.<br/>
/// 3. 이 컴포넌트에 Inspector 칸(UnityEvent)을 하나 추가하고, Update에서 그 값이 true이면 Handle로 부른다.<br/>
///    - Handle을 거쳐야 「맨 위 화면만」, 「한 프레임에 한 화면만」 규칙이 그 키에도 적용된다.<br/>
///    - 선택된 UI가 그 키를 직접 처리한다면(예: 입력칸의 글자 키) SelectedHandles로 양보한다.<br/>
///    - Update의 else if 순서가 우선순위다. 같은 프레임에 여러 키를 누르면 위에 있는 키만 처리된다.<br/>
/// 4. docs/systems/player-input.md의 표에 그 키를 추가한다.
/// </para>
/// </summary>
public class UIShortcut : MonoBehaviour
{
	[SerializeField] UnityEvent onSubmit;
	[SerializeField] UnityEvent onCancel;

	// 열린 순서. 마지막 칸이 맨 위 화면이다
	static readonly List<UIShortcut> openOrder = new List<UIShortcut>();
	// 키에 반응한 프레임. 반응한 함수가 화면을 여닫으면 같은 프레임에 다른 화면이 맨 위가 되므로, 그 화면이 같은 키에 또 반응하지 않게 막는다
	static int handledFrame = -1;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		openOrder.Clear();
		handledFrame = -1;
	}

	/// <summary>
	/// 이 화면이 지금 열린 화면 중 맨 위이면 true.
	/// </summary>
	public bool IsTop => openOrder.Count > 0 && openOrder[openOrder.Count - 1] == this;

	void OnEnable()
	{
		openOrder.Remove(this);
		openOrder.Add(this);
	}

	void OnDisable()
	{
		openOrder.Remove(this);
	}

	void Update()
	{
		if (!IsTop || handledFrame == Time.frameCount) return;

		if (GameInput.UI.CancelPressed && !SelectedHandles<ICancelHandler>())
			Handle(onCancel);
		else if (GameInput.UI.SubmitPressed && !SelectedHandles<ISubmitHandler>())
			Handle(onSubmit);
	}

	static void Handle(UnityEvent action)
	{
		handledFrame = Time.frameCount;
		action.Invoke();
	}

	// EventSystem이 선택된 UI에 그 키를 보내 처리하는지
	static bool SelectedHandles<T>() where T : IEventSystemHandler
	{
		var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
		// 마우스로 누른 버튼이 화면과 함께 닫혀도 선택은 남아 있으므로, 비활성화된 것은 처리하지 않는 것으로 본다
		return selected != null && selected.activeInHierarchy && selected.TryGetComponent<T>(out _);
	}
}
