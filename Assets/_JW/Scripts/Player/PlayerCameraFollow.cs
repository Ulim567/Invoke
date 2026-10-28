using Mirror;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 메인 카메라가 내 캐릭터를 따라가게 하는 컴포넌트. 플레이어 프리팹에 붙인다.
/// <para>
/// - 내 캐릭터(isLocalPlayer)에서만 카메라를 움직인다. 다른 플레이어가 접속해도 카메라를 빼앗지 않는다.<br/>
/// - 카메라 위치를 Pixel Perfect Camera의 픽셀 격자에 맞춰서(RoundToPixel) 픽셀이 떨리지 않게 한다.<br/>
/// - 카메라 Z는 Camera Z(기본 -10)로 유지한다.<br/>
/// - 씬의 공용 카메라(MainCamera 태그)를 움직이기만 하고, 카메라 프리팹은 고치지 않는다.
/// </para>
/// </summary>
public class PlayerCameraFollow : NetworkBehaviour
{
	[SerializeField, Tooltip("캐릭터 위치(발바닥)에서 카메라 중심까지의 거리. 캐릭터가 화면 가운데쯤 오게 위로 올린다")]
	private Vector2 offset = new Vector2(0f, 2f);

	[SerializeField, Tooltip("카메라 Z 위치")]
	private float cameraZ = -10f;

	Camera cam;
	PixelPerfectCamera pixelPerfect;

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();
		cam = Camera.main;
		if (cam == null)
		{
			Debug.LogWarning("[PlayerCameraFollow] MainCamera 태그가 붙은 카메라가 없음");
			return;
		}
		pixelPerfect = cam.GetComponent<PixelPerfectCamera>();
		Follow();
	}

	// 캐릭터가 이번 프레임 움직임을 끝낸 뒤 카메라를 옮긴다
	void LateUpdate()
	{
		if (!isLocalPlayer || cam == null) return;
		Follow();
	}

	void Follow()
	{
		Vector3 target = new Vector3(transform.position.x + offset.x, transform.position.y + offset.y, cameraZ);
		if (pixelPerfect != null) target = pixelPerfect.RoundToPixel(target);
		target.z = cameraZ;
		cam.transform.position = target;
	}
}
