using UnityEngine;

/// <summary>
/// 창 크기·비율이 어떻든 맵 밖(하늘 그림 끝, 땅 끝)이 보이지 않게 카메라가 보는 넓이를 맞춘다. 메인 카메라(공용 Camera 프리팹)에 붙인다. 연출만 한다.
/// <para>
/// - 보이는 넓이가 늘 가로 Max Width칸(40) × 세로 Max Height칸(22.5) 상자 안에 들어가게 Orthographic Size를 정한다 (#429).<br/>
///   16:9 창: 딱 40 × 22.5칸. 가로로 긴 창: 가로 40칸에 맞추고 위아래를 조금 덜 보여 준다. 세로로 긴 창: 세로 22.5칸에 맞추고 양옆을 조금 덜 보여 준다.<br/>
/// - 검은 띠 없이 창을 꽉 채우고, 늘린 화면을 다시 키우지 않아서 흐려지지 않는다. 그래서 Pixel Perfect Camera는 쓰지 않는다 (같이 쓰면 그쪽이 Size를 덮어씀).<br/>
/// - 창이 640×360의 정수배가 아니면 픽셀 몇 줄이 한 줄씩 두꺼워질 수 있다. 1920×1080, 2560×1440 같은 16:9 전체 화면은 딱 맞는다.<br/>
/// - CameraBounds(900)보다 먼저 돌아서, CameraBounds가 이번 프레임의 넓이로 범위를 잰다.
/// </para>
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
	[SerializeField, Min(1f), Tooltip("가장 넓게 보이는 가로 칸 수 (기준 640px ÷ 16 PPU)")]
	float maxWidth = 40f;

	[SerializeField, Min(1f), Tooltip("가장 넓게 보이는 세로 칸 수 (기준 360px ÷ 16 PPU)")]
	float maxHeight = 22.5f;

	Camera cam;

	void OnEnable() => cam = GetComponent<Camera>();

	void Update() => Fit();
	void LateUpdate() => Fit();

	void Fit()
	{
		if (cam == null || !cam.orthographic || cam.aspect <= 0f) return;
		float halfHeight = Mathf.Min(maxHeight, maxWidth / cam.aspect) * 0.5f;
		if (!Mathf.Approximately(cam.orthographicSize, halfHeight)) cam.orthographicSize = halfHeight;
	}
}
