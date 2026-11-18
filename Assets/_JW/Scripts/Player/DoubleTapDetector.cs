/// <summary>
/// 방향키 두 번 누르기 판정 (#557). PlayerMovement가 ←← / →→ 대시에 쓴다. Unity 입력 없이 값만 받아서, 시간과 함께 테스트하기 쉽게 따로 뒀다.
/// <para>
/// - 매 프레임 Update(좌우 입력, 지금 시간, 간격)를 부른다. 두 번 누르기가 된 프레임에만 그 방향(1 오른쪽, -1 왼쪽)을, 아니면 0을 돌려준다.<br/>
/// - 두 번 누르기: 한쪽 키를 window초 안에 눌렀다 떼고, 뗀 뒤 window초 안에 같은 쪽을 다시 누름.<br/>
/// - 오래 누르고 걷다가 떼고 다시 누르는 것, 반대쪽으로 바꾸는 것, 천천히 두 번 누르는 것은 두 번 누르기가 아니다.<br/>
/// - 한 번 두 번 누르기가 되면 그 두 번째 누름은 다음 두 번 누르기의 첫 번째로 쓰지 않는다 (←←← 가 대시 두 번이 되지 않게).
/// </para>
/// </summary>
public class DoubleTapDetector
{
	// 좌우 입력을 이 값보다 크게 기울였을 때 누른 것으로 본다 (키보드는 -1, 0, 1)
	const float PressThreshold = 0.5f;

	int lastSign;
	float pressTime;
	// 짧게 눌렀다 뗀 쪽 (첫 번째 누름). 없으면 0
	int tappedSign;
	float releaseTime;
	// 이번 누름이 두 번 누르기의 두 번째였으면 true. 이 누름은 다음 첫 번째로 쓰지 않는다
	bool consumed;

	/// <summary>매 프레임 부른다. 두 번 누르기가 된 프레임에만 방향(1, -1), 아니면 0</summary>
	public int Update(float moveX, float now, float window)
	{
		int sign = moveX > PressThreshold ? 1 : moveX < -PressThreshold ? -1 : 0;
		if (sign == lastSign) return 0;

		int result = 0;
		// 떼거나 반대쪽으로 바꿈: 짧게 눌렀다 뗀 것만 첫 번째 누름으로 기억한다
		if (lastSign != 0)
		{
			bool shortPress = now - pressTime <= window && !consumed;
			tappedSign = shortPress ? lastSign : 0;
			releaseTime = now;
		}
		// 새로 누름: 같은 쪽을 뗀 지 window초 안이면 두 번 누르기
		if (sign != 0)
		{
			consumed = false;
			if (tappedSign == sign && now - releaseTime <= window)
			{
				result = sign;
				consumed = true;
			}
			tappedSign = 0;
			pressTime = now;
		}
		lastSign = sign;
		return result;
	}
}
