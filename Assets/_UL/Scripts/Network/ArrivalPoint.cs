using UnityEngine;

/// <summary>
/// 장소 사이를 이동한 플레이어가 도착할 위치. 장소 씬 안의 GameObject에 붙인다.
/// <para>
/// - 서버가 플레이어를 이 씬으로 옮길 때, 플레이어가 떠나온 장소(FromPlace)가 같은 ArrivalPoint의 위치에 도착시킨다.<br/>
/// - 이동 요청에 도착 자리 이름(arrival)이 있으면 Id가 같은 ArrivalPoint를 먼저 찾는다 (#518, 사냥터 사이 포탈).<br/>
/// - 같은 ArrivalPoint가 없으면 그 씬의 NetworkStartPosition에 도착시킨다.<br/>
/// - 예: 로비의 사냥터 입구 앞에 fromPlace가 HuntingGround인 ArrivalPoint를 두면, 사냥터에서 돌아온 플레이어가 그 입구 앞에 도착한다.
/// </para>
/// </summary>
public class ArrivalPoint : MonoBehaviour
{
	[SerializeField, Tooltip("어느 장소에서 온 플레이어가 여기에 도착하는지. PlaceScenes의 씬 이름(HuntingGround, TrainingRoom, RaidDungeon)")]
	string fromPlace;

	/// <summary>
	/// 이 위치에 도착하는 플레이어가 떠나온 장소의 씬 이름 (PlaceScenes).
	/// </summary>
	public string FromPlace => fromPlace;

	[SerializeField, Tooltip("(#518) 이 도착 자리의 이름. 이동 요청에 이 이름(TravelRequestMessage.arrival)이 오면 떠나온 장소와 상관없이 여기에 도착한다. 사냥터 사이 포탈처럼 같은 두 장소 사이에 문이 여러 쌍일 때 쓴다")]
	string id;

	/// <summary>
	/// 이 도착 자리의 이름 (#518). 비어 있으면 이름으로는 찾지 않는다.
	/// </summary>
	public string Id => id;
}
