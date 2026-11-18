using UnityEngine;

/// <summary>
/// 사냥터 사이 포탈 (#518, 노션 11/16 맵 확장, 캔버스 17-1~17-4·17-7). 가까이 가서 ↑를 누르면 확인 창 없이 바로 다른 사냥터로 이동을 요청한다.
/// <para>
/// - 트리거 범위, 표시(마름모 + 크게 뜨는 ↑ 키, 글씨 없음), 내 캐릭터 판별, 검 타기·사망·경직 중 ↑ 무시는 부모 PlaceInteractable이 한다. 키 크기는 Key Size(빌더가 13픽셀로 넣음, 문은 9).<br/>
/// - 이동은 이우림의 PlaceTravelClient.RequestTravel(갈 씬, 도착 자리)로 요청한다. 도착 자리는 갈 씬의 ArrivalPoint Id.
///   사냥터2 ↔ 사냥터3은 포탈이 두 쌍이라 떠나온 장소만으로는 어느 포탈 앞에 도착할지 정할 수 없어서 Id로 정한다.<br/>
/// - 그림: Frames를 Frames Per Second로 돌려 소용돌이가 돈다. 서버(화면 없음)에서는 돌리지 않는다. Renderer.enabled는 건드리지 않는다 (호스트에서 HostPlaceVisibility가 씀).<br/>
/// - 씬에 놓는 일은 메뉴 Invoke → 맵 → 사냥터 2·3 만들기·포탈 놓기 (HuntingGroundBuilder)가 한다.
/// </para>
/// </summary>
public class PlacePortal : PlaceInteractable
{
	[SerializeField, Tooltip("갈 장소 씬 이름 (PlaceScenes: HuntingGround, HuntingGround2, HuntingGround3)")]
	string destination;

	[SerializeField, Tooltip("갈 씬에서 도착할 ArrivalPoint의 Id (보통 이 포탈과 이어진 포탈 앞)")]
	string arrival;

	[SerializeField, Tooltip("소용돌이 그림 (차례로 돌림). 비우면 지금 그림 그대로")]
	Sprite[] frames;

	[SerializeField, Min(1f)]
	float framesPerSecond = 8f;

	SpriteRenderer body;

	/// <summary>갈 장소 씬 이름</summary>
	public string Destination => destination;

	/// <summary>갈 씬에서 도착할 ArrivalPoint의 Id</summary>
	public string Arrival => arrival;

	void Start()
	{
		if (!Application.isBatchMode) TryGetComponent(out body);
	}

	void LateUpdate()
	{
		if (body == null || frames == null || frames.Length == 0) return;
		int frame = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
		if (frames[frame] != null && body.sprite != frames[frame]) body.sprite = frames[frame];
	}

	protected override void Interact(PlayerInputReader player)
	{
		if (PlaceTravelClient.Instance == null)
		{
			Debug.LogWarning("[PlacePortal] 기준 씬에 PlaceTravelClient가 없어서 이동할 수 없습니다.");
			return;
		}
		Debug.Log($"[PlacePortal] {name}: {destination}({arrival})(으)로 이동 요청");
		PlaceTravelClient.Instance.RequestTravel(destination, arrival);
	}
}
