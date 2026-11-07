using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 플레이어 머리 위 닉네임 이름표 (#362). 이 화면의 플레이어(PlayerRoster) 중 내 캐릭터와 같은 장소에 있는 플레이어마다 하나씩 띄운다.
/// <para>
/// - 닉네임은 이우림의 PlayerPublicInfo.Nickname을 읽기만 한다. 비어 있으면(시험용 플레이어) 이름표를 띄우지 않는다.<br/>
/// - 위치: 플레이어 콜라이더 윗변보다 Head Offset만큼 위를 화면 좌표로 바꿔서 놓는다. 내 이름은 청록, 다른 플레이어는 흰색.<br/>
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

	void Awake() => area = (RectTransform)transform;

	void LateUpdate()
	{
		Camera camera = Camera.main;
		int used = 0;
		if (camera != null && tags != null)
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

				Vector3 head = parts.body != null
					? new Vector3(parts.body.bounds.center.x, parts.body.bounds.max.y, 0f)
					: player.transform.position;
				Vector3 screen = camera.WorldToScreenPoint(head + Vector3.up * headOffset);
				if (screen.z < 0f) continue;
				if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screen, null, out Vector2 local)) continue;

				RectTransform tag = tags[used];
				TMP_Text text = texts != null && used < texts.Length ? texts[used] : null;
				if (!tag.gameObject.activeSelf) tag.gameObject.SetActive(true);
				tag.anchoredPosition = local;
				if (text != null)
				{
					if (text.text != nickname) text.text = nickname;
					text.color = PlayerRoster.IsLocal(player) ? myColor : otherColor;
				}
				used++;
			}
		}

		for (int i = used; tags != null && i < tags.Length; i++)
			if (tags[i] != null && tags[i].gameObject.activeSelf) tags[i].gameObject.SetActive(false);

		// 사라진 플레이어 정리
		if (cache.Count > 16) cache.Clear();
	}
}
