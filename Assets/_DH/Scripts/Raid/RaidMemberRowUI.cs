using TMPro;
using UnityEngine;

/// <summary>
/// 파티방 파티원 목록의 한 줄 (이름, 상태: 파티장 / 준비 완료 / 준비 중). 보여 주기만 한다.
/// <para>사용법: 줄 프리팹 최상위에 붙이고 부품을 넣는다. RaidLobbyUI가 만들고 Set으로 채운다.</para>
/// </summary>
public class RaidMemberRowUI : MonoBehaviour
{
	[SerializeField, Tooltip("파티원 이름 글자")]
	TMP_Text nameText;

	[SerializeField, Tooltip("상태 글자 (파티장 / 준비 완료 / 준비 중)")]
	TMP_Text stateText;

	[SerializeField, Tooltip("준비 완료일 때 상태 글자 색")]
	Color readyColor = new Color(0.4f, 1f, 0.4f);

	[SerializeField, Tooltip("준비 중일 때 상태 글자 색")]
	Color waitingColor = new Color(0.7f, 0.7f, 0.7f);

	[SerializeField, Tooltip("파티장 상태 글자 색")]
	Color leaderColor = new Color(1f, 0.85f, 0.3f);

	/// <summary>
	/// 이 줄에 member를 보여 준다. 내 줄이면 이름 뒤에 (나)를 붙인다.
	/// </summary>
	public void Set(RaidMember member, bool isLeader, bool isMe)
	{
		nameText.text = isMe ? $"{member.Name} (나)" : member.Name;
		if (isLeader)
		{
			stateText.text = "파티장";
			stateText.color = leaderColor;
		}
		else
		{
			stateText.text = member.IsReady ? "준비 완료" : "준비 중";
			stateText.color = member.IsReady ? readyColor : waitingColor;
		}
	}
}
