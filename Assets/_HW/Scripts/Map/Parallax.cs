using UnityEngine;

/// <summary>
/// 배경 한 겹(하늘, 폐허)을 카메라보다 느리게 움직여서 멀리 있는 것처럼 보이게 한다 (패럴랙스). 보여 주기만 한다.
/// <para>
/// 사용법<br/>
/// - 씬의 Background 아래 각 겹(Sprite Renderer)에 붙이고 Factor만 다르게 한다.<br/>
///   하늘 0.95 / 먼 폐허 0.8 / 중간 폐허 0.6 / 가까운 폐허 0.4 (1에 가까울수록 멀리 있는 것처럼 느리게 움직임)<br/>
/// - 가로로 끊기지 않게 Sprite Renderer의 Draw Mode를 Tiled로 하고 Width를 넉넉히 늘린다. 그림은 좌우가 이어지게 만들어 두었다.<br/>
/// - 카메라는 Camera.main(내 캐릭터를 따라가는 카메라)을 쓴다. 서버처럼 카메라가 없으면 아무것도 하지 않는다.
/// </para>
/// <para>
/// 픽셀 떨림 막기<br/>
/// - 위치를 1/16 단위(1픽셀, PPU 16)로 반올림한다. 카메라도 PixelPerfectCamera로 픽셀에 맞추므로 둘이 같은 칸에 맞는다.<br/>
/// - 카메라를 옮기는 PlayerCameraFollow(LateUpdate)보다 뒤에 실행되도록 실행 순서를 늦춘다. 먼저 실행되면 한 프레임 늦게 따라와서 떨린다.
/// </para>
/// </summary>
[DefaultExecutionOrder(1000)]
public class Parallax : MonoBehaviour
{
	// 그림의 Pixels Per Unit. 위치를 1 / PixelsPerUnit 단위로 맞춘다
	const float PixelsPerUnit = 16f;

	[SerializeField, Range(0f, 1f), Tooltip("0이면 땅과 같이 움직이고, 1이면 화면에 붙어 있다")]
	float factor = 0.5f;

	[SerializeField, Range(0f, 1f), Tooltip("위아래 움직임에 쓰는 Factor. 점프할 때 배경이 너무 흔들리지 않게 보통 가로보다 크게 둔다")]
	float verticalFactor = 0.9f;

	Transform cam;
	Vector3 startPosition;
	Vector3 cameraStart;

	void LateUpdate()
	{
		// 카메라는 내 캐릭터가 생긴 뒤에 처음 움직이므로, 카메라를 찾은 그 순간을 기준으로 삼는다
		if (cam == null)
		{
			if (Camera.main == null) return;
			cam = Camera.main.transform;
			startPosition = transform.position;
			cameraStart = cam.position;
		}

		Vector3 moved = cam.position - cameraStart;
		transform.position = new Vector3(
			SnapToPixel(startPosition.x + moved.x * factor),
			SnapToPixel(startPosition.y + moved.y * verticalFactor),
			startPosition.z);
	}

	// 1픽셀(1/16 유닛) 단위로 반올림한다
	static float SnapToPixel(float value) => Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;
}
