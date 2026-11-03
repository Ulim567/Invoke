using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레이드 시작·끝을 알리는 곳. 김도훈(레이드 내용)과 이우림(네트워크, 던전 불러오기·플레이어 옮기기)이 만나는 접점이다.
/// <para>
/// - 레이드 시작: 파티방에서 시작하면 RaidRooms가 RequestStart(파티원 이름 목록)를 부르고, StartRequested로 알린다.<br/>
/// - 받는 쪽(이우림)이 StartRequested를 받아 던전을 불러오고 파티원을 옮긴다. 지금은 받는 쪽이 없어서 로그만 남긴다 (임시).<br/>
/// - 레이드 끝(클리어·실패)은 판정 이슈에서 여기에 추가한다.
/// </para>
/// </summary>
public static class RaidFlow
{
	/// <summary>
	/// [로직] 레이드 시작을 요청할 때. 파티원 이름 목록(0번 파티장)을 넘긴다.
	/// </summary>
	public static event Action<IReadOnlyList<string>> StartRequested;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics() => StartRequested = null;

	/// <summary>
	/// 레이드 시작 함수. 파티원 이름 목록을 넘긴다. RaidRooms.TryStart가 부른다.
	/// </summary>
	public static void RequestStart(IReadOnlyList<string> memberNames)
	{
		Debug.Log($"[RaidFlow] 레이드 시작 요청: {string.Join(", ", memberNames)}");
		if (StartRequested == null)
		{
			Debug.Log("[RaidFlow] 레이드 시작을 받는 쪽이 아직 없습니다 (임시).");
			return;
		}
		StartRequested.Invoke(memberNames);
	}
}
