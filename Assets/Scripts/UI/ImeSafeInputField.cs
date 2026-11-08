using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 한글 조합 중에 칸을 벗어나도 멈추지 않는 TMP 입력 칸. TMP_InputField 대신 이 컴포넌트를 쓴다.
/// <para>
/// 고치는 문제 (Unity 6000.6 ugui의 TMP_InputField):<br/>
/// - 「제어」의 「어」처럼 글자를 조합하는 중에 다른 곳을 누르거나 Tab으로 넘어가면, TMP_InputField.OnDeselect가 조합 글자를 Append로 붙인다.<br/>
/// - 이때 Append가 지울 범위를 조합 글자까지 그려진 화면 글자 기준으로 구하고, 조합 글자가 없는 text를 잘라 ArgumentOutOfRangeException이 난다.<br/>
/// - 예외 때문에 칸이 비활성화되지 못하고 남아, 그 칸과 다른 칸에 조합 글자가 계속 보이고 입력이 막힌다.
/// </para>
/// <para>
/// 프리팹에서는 메뉴 Invoke → UI → 입력 칸을 한글 조합 안전 칸으로 바꾸기가 TMP_InputField를 이 컴포넌트로 바꾼다 (설정은 그대로 남는다).
/// </para>
/// </summary>
public class ImeSafeInputField : TMP_InputField
{
	public override void OnDeselect(BaseEventData eventData)
	{
		// 조합 글자는 입력기 하나가 모든 칸에 같이 알려 준다. 이 칸에서 적던 글자일 때(입력 중인 칸일 때)만 넣는다. 다른 칸의 글자가 끼어들지 않게
		string composing = isFocused ? CurrentComposition() : null;
		if (string.IsNullOrEmpty(composing) || readOnly)
		{
			base.OnDeselect(eventData);
			return;
		}

		// 조합 중인 글자를 TMP의 Append 대신 직접 커서 자리에 넣는다. 마지막 음절(「제어」의 「어」)을 잃지 않기 위함
		// 커서 자리: TMP는 조합 중일 때 위치에 조합 글자 수를 더해 돌려주므로 그만큼 뺀다. 넘치면 끝에 붙인다
		int at = Mathf.Clamp(selectionStringAnchorPosition - composing.Length, 0, text.Length);
		text = text.Insert(at, composing);

		// readOnly인 동안 TMP는 조합 글자 수를 0으로 보아 Append를 건너뛴다. 나머지(칸 비활성화, 포커스 잃음 알림)는 원래대로 한다
		readOnly = true;
		try
		{
			base.OnDeselect(eventData);
		}
		finally
		{
			readOnly = false;
		}
	}

	// 지금 입력기가 조합 중인 글자. TMP_InputField가 읽는 곳과 같다 (EventSystem의 입력 모듈, 없으면 Input)
	static string CurrentComposition()
	{
		if (EventSystem.current != null && EventSystem.current.currentInputModule != null)
			return EventSystem.current.currentInputModule.input.compositionString;
		return Input.compositionString;
	}
}
