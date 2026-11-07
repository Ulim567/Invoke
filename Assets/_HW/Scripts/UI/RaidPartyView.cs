using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 HUD의 파티원 목록 (우림이 시안 8-3). 레이드 던전에 있을 때만 보이고, 나를 뺀 같은 장소의 플레이어를 줄마다 보여 준다.
/// <para>
/// - 목록은 PlayerRoster(이우림), 체력은 PlayerHealth의 Hp, MaxHp, IsDead를 읽기만 한다 (place-scenes.md, player-health.md).<br/>
/// - 줄은 프리팹에 미리 만들어 둔 만큼만 쓴다 (레이드 최대 3명 → 나를 빼고 2줄). 남는 줄은 끈다.<br/>
/// - 이름: PlayerPublicInfo.Nickname (이우림, #359). 비어 있으면(시험용 플레이어) 「파티원 1」, 「파티원 2」.<br/>
/// - 쓰러진 파티원: 어두운 줄, 이름 흐리게, 오른쪽에 「사망」, 체력 막대 비움.<br/>
/// - HudCanvas 프리팹 안에 있다 (메뉴 Invoke → UI → 게임 HUD 만들기).
/// </para>
/// </summary>
public class RaidPartyView : MonoBehaviour
{
	[SerializeField, Tooltip("「파티」 글자와 줄 전체. 레이드 던전이 아니면 끈다")] GameObject panel;
	[SerializeField] Image[] rowImages;
	[SerializeField] TMP_Text[] nameTexts;
	[SerializeField, Tooltip("Filled, Horizontal, Left")] Image[] hpFills;
	[SerializeField] TMP_Text[] deadTexts;
	[SerializeField] Sprite aliveSprite;
	[SerializeField] Sprite deadSprite;
	[SerializeField] Color nameColor = Color.white;
	[SerializeField] Color deadNameColor = new Color32(0x7C, 0x84, 0xA8, 0xFF);

	readonly List<PlayerHealth> others = new List<PlayerHealth>();

	void Update()
	{
		GameObject me = NetworkClient.localPlayer != null ? NetworkClient.localPlayer.gameObject : null;
		bool inRaid = me != null && PlayerPlace.Of(me) == PlaceScenes.RaidDungeon; // #362: scene.name은 클라이언트에서 늘 Base

		others.Clear();
		if (inRaid)
		{
			foreach (PlayerHealth player in PlayerRoster.Players)
				if (player != null && !PlayerRoster.IsLocal(player) && PlayerRoster.IsInMyPlace(player)) others.Add(player);
		}

		bool show = inRaid && others.Count > 0;
		if (panel != null && panel.activeSelf != show) panel.SetActive(show);
		if (!show) return;

		int rows = rowImages != null ? rowImages.Length : 0;
		for (int i = 0; i < rows; i++)
		{
			bool used = i < others.Count;
			if (rowImages[i] != null && rowImages[i].gameObject.activeSelf != used) rowImages[i].gameObject.SetActive(used);
			if (used) Draw(i, others[i]);
		}
	}

	void Draw(int row, PlayerHealth player)
	{
		bool dead = player.IsDead;
		if (rowImages[row] != null)
		{
			Sprite sprite = dead ? deadSprite : aliveSprite;
			if (sprite != null && rowImages[row].sprite != sprite) rowImages[row].sprite = sprite;
		}

		TMP_Text name = At(nameTexts, row);
		if (name != null)
		{
			string nickname = player.TryGetComponent(out PlayerPublicInfo info) ? info.Nickname : "";
			SetText(name, $"[{(string.IsNullOrEmpty(nickname) ? $"파티원 {row + 1}" : nickname)}]");
			name.color = dead ? deadNameColor : nameColor;
		}

		Image fill = At(hpFills, row);
		if (fill != null) fill.fillAmount = dead ? 0f : Mathf.Clamp01((float)player.Hp / Mathf.Max(1, player.MaxHp));

		TMP_Text deadText = At(deadTexts, row);
		if (deadText != null && deadText.gameObject.activeSelf != dead) deadText.gameObject.SetActive(dead);
	}

	static T At<T>(T[] array, int index) where T : class => array != null && index < array.Length ? array[index] : null;

	static void SetText(TMP_Text text, string value)
	{
		if (text.text != value) text.text = value;
	}
}
