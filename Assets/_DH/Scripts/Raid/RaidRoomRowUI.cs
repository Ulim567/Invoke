using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레이드 방 목록의 한 줄 (방 이름, 인원, 자물쇠, 입장 버튼). 보여 주기만 하고, 입장은 RaidLobbyUI에 알린다.
/// 인원이 다 차면 인원 글자를 빨갛게 보여 준다.
/// <para>사용법: 줄 프리팹 최상위에 붙이고 부품을 넣는다. RaidLobbyUI가 만들고 Set으로 채운다.</para>
/// </summary>
public class RaidRoomRowUI : MonoBehaviour
{
	[SerializeField, Tooltip("방 이름 글자")]
	TMP_Text nameText;

	[SerializeField, Tooltip("인원 글자 (예: 2 / 3)")]
	TMP_Text countText;

	[SerializeField, Tooltip("비밀번호 방일 때만 켜는 자물쇠 표시. 비워 둬도 된다")]
	GameObject lockIcon;

	[SerializeField, Tooltip("입장 버튼")]
	Button joinButton;

	[SerializeField, Tooltip("인원이 다 찼을 때 인원 글자 색")]
	Color fullColor = new Color(1f, 0.45f, 0.45f);

	Color countColor;

	RaidRoom room;
	Action<RaidRoom> onJoin;

	void Awake()
	{
		countColor = countText.color;
		joinButton.onClick.AddListener(() => onJoin?.Invoke(room));
	}

	/// <summary>
	/// 이 줄에 room을 보여 준다. 입장 버튼을 누르면 join(room)을 부른다.
	/// </summary>
	public void Set(RaidRoom room, Action<RaidRoom> join)
	{
		this.room = room;
		onJoin = join;
		nameText.text = room.Name;
		countText.text = $"{room.Members.Count} / {RaidRooms.MaxMembers}";
		if (lockIcon != null) lockIcon.SetActive(room.HasPassword);
		// 꽉 찬 방도 입장 버튼은 눌린다 (누르면 RaidLobbyUI가 "인원이 다 찼습니다"를 알림). 인원 글자만 빨갛게
		countText.color = room.IsFull ? fullColor : countColor;
	}
}
