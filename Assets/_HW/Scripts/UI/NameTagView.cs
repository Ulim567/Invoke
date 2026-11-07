using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// 플레이어 머리 위 닉네임 이름표 (#362). 이 화면의 플레이어(PlayerRoster) 중 내 캐릭터와 같은 장소에 있는 플레이어마다 하나씩 띄운다.
/// <para>
/// - 닉네임은 이우림의 PlayerPublicInfo.Nickname을 읽기만 한다. 비어 있으면(시험용 플레이어) 이름표를 띄우지 않는다.<br/>
/// - 위치: 플레이어 콜라이더 윗변보다 Head Offset만큼 위를 화면 좌표로 바꿔서 놓는다. 내 이름은 청록, 다른 플레이어는 흰색.<br/>
/// - 위치는 보간된 캐릭터 위치를 기준으로, 카메라가 모두 움직인 뒤 UI를 그리기 직전(Canvas.willRenderCanvases)에 정한다 (#426).<br/>
/// - 화면 좌표는 Camera.WorldToScreenPoint를 쓰지 않고 카메라 transform 위치로 직접 계산한다 (#426). 회전하지 않는 직교 카메라라고 가정한다.<br/>
/// - 쓰러져 숨겨진 플레이어(PlayerHealth.IsHidden)는 띄우지 않는다.<br/>
/// - 이름표는 HudCanvas 프리팹에 미리 만들어 둔 만큼만 쓴다 (런타임에 만들지 않음). 남는 이름표는 끈다.<br/>
/// - 플레이어 프리팹은 고치지 않는다. HUD 캔버스 위에 그린다.
/// </para>
/// </summary>
public class NameTagView : MonoBehaviour
{
	[SerializeField, Tooltip("이름표 (배경 Image + 자식 TMP 글자). 미리 만들어 둔 만큼만 쓴다")] RectTransform[] tags;
	[SerializeField] TMP_Text[] texts;
	[SerializeField, Tooltip("콜라이더 윗변에서 이름표까지 (월드 단위)")] float headOffset = 0.35f;
	[SerializeField] Color myColor = new Color32(0x5E, 0xF2, 0xD6, 0xFF);
	[SerializeField] Color otherColor = Color.white;

	RectTransform area;
	readonly Dictionary<PlayerHealth, (PlayerPublicInfo info, Collider2D body)> cache = new Dictionary<PlayerHealth, (PlayerPublicInfo, Collider2D)>();
	// 이름표마다 LateUpdate에서 정한 플레이어. 위치는 UI를 그리기 직전(PlaceTags)에 이 목록을 보고 정한다
	(PlayerHealth player, Collider2D body)[] shown;
	int shownCount;
	// 화면 좌표 계산에 쓰는 카메라와 그 카메라의 Pixel Perfect Camera. 카메라가 바뀔 때만 다시 찾는다
	Camera cachedCamera;
	PixelPerfectCamera pixelPerfect;

	void Awake()
	{
		area = (RectTransform)transform;
		shown = new (PlayerHealth, Collider2D)[tags != null ? tags.Length : 0];
	}

	// 위치는 카메라가 모두 움직인 뒤(PlayerCameraFollow, CameraBounds의 LateUpdate 뒤)에 정해야 이름표가 끊기지 않는다.
	// Canvas.willRenderCanvases는 모든 LateUpdate가 끝나고 UI를 그리기 직전에 불리므로, 실행 순서 번호 없이 그 뒤를 보장한다
	void OnEnable() => Canvas.willRenderCanvases += PlaceTags;
	void OnDisable() => Canvas.willRenderCanvases -= PlaceTags;

	// 이름표를 띄울 플레이어를 고르고 글자·색·켜기를 정한다.
	// 글자와 색을 UI를 그리기 직전에 바꾸면 한 프레임 늦게 그려지므로 여기서 바꾸고, 위치만 PlaceTags에서 정한다
	void LateUpdate()
	{
		int used = 0;
		if (Camera.main != null && tags != null)
		{
			foreach (PlayerHealth player in PlayerRoster.Players)
			{
				if (used >= tags.Length) break;
				if (player == null || player.IsHidden || !PlayerRoster.IsInMyPlace(player)) continue;
				if (!cache.TryGetValue(player, out var parts))
				{
					parts = (player.GetComponent<PlayerPublicInfo>(), player.GetComponent<Collider2D>());
					cache[player] = parts;
				}
				string nickname = parts.info != null ? parts.info.Nickname : "";
				if (string.IsNullOrEmpty(nickname)) continue;

				RectTransform tag = tags[used];
				TMP_Text text = texts != null && used < texts.Length ? texts[used] : null;
				if (!tag.gameObject.activeSelf) tag.gameObject.SetActive(true);
				if (text != null)
				{
					if (text.text != nickname) text.text = nickname;
					text.color = PlayerRoster.IsLocal(player) ? myColor : otherColor;
				}
				shown[used] = (player, parts.body);
				used++;
			}
		}
		shownCount = used;

		for (int i = used; tags != null && i < tags.Length; i++)
		{
			shown[i] = default;
			if (tags[i] != null && tags[i].gameObject.activeSelf) tags[i].gameObject.SetActive(false);
		}

		// 사라진 플레이어 정리
		if (cache.Count > 16) cache.Clear();
	}

	// 이름표를 플레이어 머리 위 화면 좌표에 놓는다. 보여 주기만 한다
	void PlaceTags()
	{
		Camera camera = Camera.main;
		if (camera == null || !camera.orthographic) return;
		if (camera != cachedCamera)
		{
			cachedCamera = camera;
			pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
		}

		// Pixel Perfect Camera는 화면을 그리기 직전에만 카메라 행렬(worldToCameraMatrix)을 넣고, 그동안 Unity는 행렬을 transform으로 다시 계산하지 않는다.
		// 그래서 여기서 WorldToScreenPoint를 쓰면 지난 프레임의 카메라 위치로 계산되어, 카메라가 움직인 프레임마다 이름표가 튄다.
		// 카메라 transform 위치를 Pixel Perfect Camera와 같은 방법(RoundToPixel)으로 맞춰서 직접 계산한다
		Vector3 cameraPosition = camera.transform.position;
		if (pixelPerfect != null && pixelPerfect.isActiveAndEnabled) cameraPosition = pixelPerfect.RoundToPixel(cameraPosition);
		Rect view = camera.pixelRect;
		float pixelsPerUnit = view.height / (camera.orthographicSize * 2f);

		for (int i = 0; i < shownCount; i++)
		{
			var (player, body) = shown[i];
			// LateUpdate 뒤에 플레이어가 사라졌으면 다음 LateUpdate에서 이름표를 끈다
			if (player == null) continue;
			Vector3 head = HeadPosition(player, body) + Vector3.up * headOffset;
			Vector2 screen = view.center + (Vector2)(head - cameraPosition) * pixelsPerUnit;
			if (RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local))
				tags[i].anchoredPosition = local;
		}
	}

	// 콜라이더 윗변 가운데를 보간된 캐릭터 위치 기준으로 구한다.
	// bounds는 물리 단계(FixedUpdate)마다만 바뀌어서 그대로 쓰면 이름표가 끊긴다. bounds와 Rigidbody2D.position은 둘 다 물리 단계의 위치라
	// 둘의 차이(몸 기준점에서 머리까지)는 늘 같으므로, 이 차이를 매 프레임 보간되는 transform 위치에 더한다
	static Vector3 HeadPosition(PlayerHealth player, Collider2D body)
	{
		if (body == null) return player.transform.position;
		Bounds bounds = body.bounds;
		Rigidbody2D physicsBody = body.attachedRigidbody;
		if (physicsBody == null) return new Vector3(bounds.center.x, bounds.max.y, 0f);
		Vector3 position = physicsBody.transform.position;
		return new Vector3(
			position.x + bounds.center.x - physicsBody.position.x,
			position.y + bounds.max.y - physicsBody.position.y,
			0f);
	}
}
