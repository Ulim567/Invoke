using Mirror;
using UnityEngine;

/// <summary>
/// 클라이언트의 장소 이동. 입구·출구의 요청을 받아 서버에 보내고, 서버의 답이 올 때까지 로딩 화면을 띄운다.
/// <para>
/// 하는 일<br/>
/// - 로비 입구의 이벤트(LobbyEntrance.TravelRequested, RaidEntryRequested)를 받아 서버에 TravelRequestMessage를 보낸다.<br/>
/// - 요청을 보낸 뒤 서버의 답(TravelEndedMessage)이 올 때까지 로딩 화면을 보이고, 로컬 플레이어의 Player·Attack 액션 맵을 비활성화한다.<br/>
/// - 접속 직후에도 서버가 로컬 플레이어의 함수 데이터를 다 받을 때까지(FunctionExecutorNetwork.IsDataReady) 같은 방법으로 막는다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 기준 씬(Base)에 하나 둔다. 장소 씬을 언로드해도 사라지지 않게 하기 위함이다.<br/>
/// - 다른 스크립트는 PlaceTravelClient.Instance.RequestTravel(씬 이름)으로 이동을 요청한다. 예: 출구는 PlaceScenes.Lobby를 넘긴다.
/// </para>
/// </summary>
public class PlaceTravelClient : MonoBehaviour
{
	[SerializeField, Tooltip("이동하는 동안과 접속 직후에 화면을 덮는 로딩 화면")]
	LoadingScreen loadingScreen;

	// 서버에 이동을 요청하고 답(TravelEndedMessage)을 기다리는 중이면 true. 답을 받기 전에 다시 요청하지 않기 위함
	bool traveling;
	// 접속 직후 서버가 로컬 플레이어의 함수 데이터를 다 받기를 기다리는 중이면 true
	bool waitingForData;
	// 이번 접속에서 로컬 플레이어의 함수 데이터가 준비되었으면 true. 접속이 끊기면 다시 false
	bool dataReady;
	// 이 컴포넌트가 로컬 플레이어의 Player·Attack 액션 맵을 비활성화해 두었으면 true
	bool inputBlocked;

	/// <summary>
	/// 기준 씬에 있는 PlaceTravelClient. 장소 씬의 오브젝트(출구 등)는 Inspector에서 다른 씬의 오브젝트를 연결할 수 없으므로 이것으로 찾는다.
	/// </summary>
	public static PlaceTravelClient Instance { get; private set; }

	void OnEnable()
	{
		Instance = this;
		LobbyEntrance.TravelRequested += RequestTravel;
		LobbyEntrance.RaidEntryRequested += RequestRaidDungeon;
	}

	void OnDisable()
	{
		if (Instance == this) Instance = null;
		LobbyEntrance.TravelRequested -= RequestTravel;
		LobbyEntrance.RaidEntryRequested -= RequestRaidDungeon;
	}

	/// <summary>
	/// 서버에 장소 이동을 요청한다. 로컬 플레이어의 클라이언트에서 부른다.<br/>
	/// 서버에 접속하지 않았거나, 로컬 플레이어가 아직 없거나, 이전 요청의 답을 기다리는 중이면 아무것도 하지 않는다.
	/// </summary>
	/// <param name="destination">갈 씬 이름 (PlaceScenes의 Lobby, HuntingGround, TrainingRoom, RaidDungeon)</param>
	public void RequestTravel(string destination)
	{
		if (!NetworkClient.isConnected || NetworkClient.localPlayer == null) return;
		if (traveling || waitingForData) return;

		traveling = true;
		// 입구 창(LobbyEntrancePanel)은 「들어가기」를 누르면 창을 닫으며 액션 맵을 다시 활성화하므로, 이동하는 동안 다시 비활성화한다
		SetInputBlocked(true);
		// 로딩 화면이 화면을 다 덮은 뒤에 요청을 보낸다. 먼저 보내면 이전 장소가 사라지는 모습이 페이드 사이로 보인다
		loadingScreen.Show(() =>
		{
			// 페이드하는 사이에 접속이 끊겼으면 보내지 않는다. 원래대로 돌리는 일은 Update가 한다
			if (NetworkClient.isConnected) NetworkClient.Send(new TravelRequestMessage { destination = destination });
		});
	}

	// 레이드 던전 입구는 씬 이름을 넘기지 않으므로 여기서 채운다
	void RequestRaidDungeon() => RequestTravel(PlaceScenes.RaidDungeon);

	/// <summary>
	/// 서버의 TravelEndedMessage를 받았을 때 InvokeNetworkManager가 부른다. 다른 스크립트는 부르지 않는다.<br/>
	/// 서버가 거절했을 때와 이동을 마쳤을 때 모두 불린다.
	/// </summary>
	public void HandleTravelEnded()
	{
		if (!traveling) return;
		traveling = false;

		// 접속 직후의 로딩이 아직 끝나지 않았으면 그쪽이 로딩 화면을 닫는다
		if (waitingForData) return;
		loadingScreen.Hide();
		SetInputBlocked(false);
	}

	void Update()
	{
		if (!NetworkClient.active)
		{
			// 접속하지 않았거나 접속이 끊겼으면 원래대로 돌린다
			// 로컬 플레이어 오브젝트는 삭제되고, 액션 맵은 PlayerInputReader.OnStopLocalPlayer가 다시 활성화한다
			if (traveling || waitingForData) loadingScreen.Hide();
			traveling = false;
			waitingForData = false;
			dataReady = false;
			inputBlocked = false;
			return;
		}
		if (dataReady) return;

		// 접속 직후: 서버가 로컬 플레이어를 만들고 그 플레이어의 함수 데이터를 다 받을 때까지 로딩 화면으로 게임을 막는다
		if (!waitingForData)
		{
			waitingForData = true;
			// 접속하자마자 덮어야 기준 씬만 있는 빈 화면과 장소 씬이 로드되는 모습이 보이지 않으므로, 페이드 없이 보인다
			loadingScreen.ShowImmediate();
		}

		NetworkIdentity localPlayer = NetworkClient.localPlayer;
		if (localPlayer == null) return;

		// 함수 시전 동기화(FunctionExecutorNetwork)가 없는 플레이어 프리팹은 기다릴 데이터가 없다
		if (localPlayer.TryGetComponent(out FunctionExecutorNetwork network) && !network.IsDataReady)
		{
			SetInputBlocked(true);
			return;
		}

		dataReady = true;
		waitingForData = false;
		// 이동 요청의 답을 기다리는 중이면 그 답이 로딩 화면을 닫는다
		if (traveling) return;
		loadingScreen.Hide();
		SetInputBlocked(false);
	}

	// 로컬 플레이어의 Player·Attack 액션 맵을 비활성화하거나 다시 활성화한다
	// 이 컴포넌트가 비활성화한 적이 없으면 활성화하지 않는다. 다른 창이 비활성화해 둔 것을 풀지 않기 위함
	void SetInputBlocked(bool blocked)
	{
		if (inputBlocked == blocked) return;
		if (NetworkClient.localPlayer == null || !NetworkClient.localPlayer.TryGetComponent(out PlayerInputReader input)) return;

		inputBlocked = blocked;
		input.SetPlayerMapEnabled(!blocked);
		input.SetAttackMapEnabled(!blocked);
	}
}
