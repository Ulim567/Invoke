using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 설정 창의 체크 칸 (#536). Enter·클릭으로 켜고 끄는 Toggle에 더해, 방향키 왼쪽으로 끄고 오른쪽으로 켠다.
/// <para>
/// 왼쪽·오른쪽을 받는 이유: 같은 창의 음량 줄이 왼쪽·오른쪽으로 값을 바꾸므로, 체크 칸도 같은 키로 바뀌게 하기 위함.<br/>
/// 키를 직접 읽지 않고 EventSystem의 이동(OnMove)을 받는다. 위·아래는 원래대로 다른 줄로 옮긴다.
/// </para>
/// </summary>
public class SettingsToggle : Toggle
{
	public override void OnMove(AxisEventData eventData)
	{
		switch (eventData.moveDir)
		{
			case MoveDirection.Left:
				isOn = false;
				eventData.Use();
				return;
			case MoveDirection.Right:
				isOn = true;
				eventData.Use();
				return;
		}
		base.OnMove(eventData);
	}
}
