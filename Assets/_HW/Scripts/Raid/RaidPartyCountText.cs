using TMPro;
using UnityEngine;

/// <summary>
/// 레이드 파티방 제목 옆의 인원 글자 (「2 / 3」, 시안 13). 김도훈의 RaidRooms를 읽기만 한다.
/// 같은 오브젝트의 TMP 글자에 내 방(RaidRooms.FindRoomOf(MyName))의 인원을 매 프레임 쓴다. 방이 없으면 비운다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class RaidPartyCountText : MonoBehaviour
{
	TMP_Text text;
	int shown = -1;

	void Awake() => text = GetComponent<TMP_Text>();

	void Update()
	{
		RaidRoom room = RaidRooms.FindRoomOf(RaidRooms.MyName);
		int count = room != null ? room.Members.Count : 0;
		if (count == shown) return;
		shown = count;
		text.text = room != null ? $"{count} / {RaidRooms.MaxMembers}" : "";
	}
}
