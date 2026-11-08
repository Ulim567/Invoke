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
/// - 위치를 1/16 단위(1픽셀, PPU 16)로 반올림한다. 카메라도 CameraBounds가 1픽셀 단위로 맞추므로 둘이 같은 칸에 맞는다.<br/>
/// - 카메라를 옮기는 PlayerCameraFollow(LateUpdate)보다 뒤에 실행되도록 실행 순서를 늦춘다. 먼저 실행되면 한 프레임 늦게 따라와서 떨린다.
/// </para>
/// <para>
/// 아래 끝 (#431)<br/>
/// - 점프로 카메라가 올라가면 땅은 화면에서 그만큼 내려가지만 배경은 Vertical Factor만큼 같이 올라가서 덜 내려간다. 그대로 두면 폐허 그림 아래 끝이 땅 위로 떠서 그 틈으로 하늘이 보인다.<br/>
/// - 그래서 그림 아래 끝이 Max Bottom Y(바닥 윗면보다 조금 아래)에 닿으면 더 올라가지 않고 땅과 같이 움직인다. 아래로 갈 때는 그대로.<br/>
/// - 하늘처럼 아래 끝이 바닥보다 훨씬 아래(Bottom Check Range 4칸 넘게)에 있는 그림은 막지 않는다. 막으면 높이 올라갔을 때 하늘 위쪽 끝이 보인다.<br/>
/// - 하늘은 거꾸로 위쪽 끝이 화면 위 끝보다 아래로 내려오지 않게 한다. 하늘은 카메라보다 조금 덜 올라가서, 높이 올라가면 위쪽 끝과 그 위의 카메라 바탕색이 보였다.
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

	[SerializeField, Tooltip("그림 아래 끝이 이 높이보다 위로 올라가지 않는다 (#431). 바닥 윗면(-4)보다 조금 아래. 점프로 카메라가 올라갈 때 폐허 그림이 땅보다 덜 내려가서 그 사이로 하늘이 보이지 않게 한다")]
	float maxBottomY = -4.5f;

	[SerializeField, Min(0f), Tooltip("처음 아래 끝이 Max Bottom Y보다 이만큼 넘게 아래에 있는 그림(하늘)은 막지 않는다. 하늘은 위로 올라가지 않으면 위쪽 끝이 보인다")]
	float bottomCheckRange = 4f;

	Transform cam;
	Camera viewCamera;
	bool keepsTopAbove; // 하늘: 위쪽 끝이 화면 위 끝보다 아래로 내려오지 않게 한다
	float topOffset;    // 위치에서 그림 위쪽 끝까지
	Vector3 startPosition;
	Vector3 cameraStart;
	float maxRise = float.PositiveInfinity; // 시작 위치에서 위로 올라갈 수 있는 거리

	void LateUpdate()
	{
		// 카메라는 내 캐릭터가 생긴 뒤에 처음 움직이므로, 카메라를 찾은 그 순간을 기준으로 삼는다
		if (cam == null)
		{
			if (Camera.main == null) return;
			viewCamera = Camera.main;
			cam = viewCamera.transform;
			startPosition = transform.position;
			cameraStart = cam.position;
			if (TryGetComponent(out SpriteRenderer sprite))
			{
				// 폐허: 처음부터 아래 끝이 그 높이보다 위에 있는 그림은 더 올라가지만 않게 한다
				if (sprite.bounds.min.y >= maxBottomY - bottomCheckRange) maxRise = Mathf.Max(0f, maxBottomY - sprite.bounds.min.y);
				// 하늘: 아래 끝은 폐허·땅 뒤에 숨어 있으므로, 위쪽 끝만 화면 밖에 있게 한다
				else
				{
					keepsTopAbove = true;
					topOffset = sprite.bounds.max.y - transform.position.y;
				}
			}
		}

		Vector3 moved = cam.position - cameraStart;
		float y = startPosition.y + Mathf.Min(moved.y * verticalFactor, maxRise);
		// 하늘이 카메라보다 덜 올라가서 위쪽 끝(그 위는 카메라 바탕색)이 화면에 들어오면 그만큼 올린다 (#434)
		if (keepsTopAbove && viewCamera != null && viewCamera.orthographic)
			y = Mathf.Max(y, cam.position.y + viewCamera.orthographicSize - topOffset + 1f / PixelsPerUnit);
		transform.position = new Vector3(
			SnapToPixel(startPosition.x + moved.x * factor),
			keepsTopAbove ? Mathf.Ceil(y * PixelsPerUnit) / PixelsPerUnit : SnapToPixel(y), // 하늘은 올려서 맞춘다 (위쪽 끝이 1픽셀 비지 않게)
			startPosition.z);
	}

	// 1픽셀(1/16 유닛) 단위로 반올림한다
	static float SnapToPixel(float value) => Mathf.Round(value * PixelsPerUnit) / PixelsPerUnit;
}
