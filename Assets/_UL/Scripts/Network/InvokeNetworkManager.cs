using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 한 서버 프로그램 안에서 여러 장소 씬을 동시에 돌리는 NetworkManager.
/// <para>
/// 서버가 하는 일<br/>
/// - 시작할 때 로비·사냥터 씬(PlaceScenes.FixedPlaces)을 기준 씬 위에 겹쳐(Additive) 불러온다.<br/>
/// - 씬마다 물리 세계를 따로 두어(LocalPhysicsMode.Physics2D), 다른 씬의 오브젝트끼리 부딪히거나 판정되지 않게 한다.<br/>
/// - 따로 둔 물리 세계는 Unity가 자동으로 돌리지 않으므로, FixedUpdate마다 직접 돌린다.<br/>
/// - 접속한 플레이어를 로비 씬의 시작 위치에 만든다.<br/>
/// - 플레이어가 연습장으로 이동을 요청하면 연습장 씬을 그 플레이어를 위해 한 번 더 불러오고, 나가거나 접속이 끊기면 내린다.<br/>
/// - 레이드가 시작되면 레이드 던전 씬을 파티마다 한 번씩 더 불러오고 내린다. 파티원을 옮기는 규칙은 RaidParties가 맡는다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 기준 씬(Base)의 NetworkManager 오브젝트에 Mirror의 NetworkManager 대신 붙인다.<br/>
/// - 판정 코드는 Physics2D.OverlapBox처럼 Physics2D로 바로 부르지 않고 gameObject.scene.GetPhysicsScene2D()에 묻는다.
/// Physics2D로 바로 부르면 기준 씬의 물리 세계만 검사해서, 장소 씬의 오브젝트를 찾지 못한다.
/// </para>
/// </summary>
public class InvokeNetworkManager : NetworkManager
{
	// 장소 이름 → 서버가 불러온 씬. 한 번만 불러오는 장소(로비·사냥터)만 담는다
	readonly Dictionary<string, Scene> placeScenes = new Dictionary<string, Scene>();
	// 서버가 물리를 직접 돌릴 씬. 연습장·레이드 던전은 같은 씬을 여러 번 불러오므로 이름이 아니라 Scene 값으로 담는다
	readonly List<Scene> simulatedScenes = new List<Scene>();

	// 장소 씬을 모두 불러왔으면 true. 씬을 다 불러오기 전에 접속한 플레이어는 이 값이 켜질 때까지 기다린다
	bool scenesLoaded;

	// 시작 위치·도착 위치에서 아래로 땅을 찾는 거리. 표시를 땅보다 조금 높게 둔 경우만 맞추면 되므로 넉넉히 잡는다
	const float GroundSearchDistance = 10f;
	// PlaceOnGround의 레이캐스트 결과. 가장 가까운 땅 하나만 쓴다
	static readonly RaycastHit2D[] groundHits = new RaycastHit2D[1];

	/// <summary>
	/// [서버] SpawnPlayer가 플레이어 오브젝트를 만든 직후, 연결에 붙이기(AddPlayerForConnection) 전에 불린다 (연결, 플레이어).<br/>
	/// InvokeAuthenticator가 받아서 DB에서 불러온 진행도를 넣는다.
	/// </summary>
	public event Action<NetworkConnectionToClient, GameObject> ServerPlayerCreated;

	public override void Awake()
	{
		base.Awake();
		// Mirror는 이 값이 켜져 있으면 로그인하자마자 플레이어를 만든다.
		// 캐릭터를 고른 뒤에 만들어야 하므로 Inspector 값과 상관없이 끈다. 끄면 Mirror는 접속 뒤 준비(Ready)만 한다
		autoCreatePlayer = false;
	}

	public override void OnStartServer()
	{
		base.OnStartServer();
		// Mirror는 서버를 끌 때 등록한 처리 함수를 모두 지우므로, 서버를 켤 때마다 등록한다
		NetworkServer.RegisterHandler<TravelRequestMessage>(OnTravelRequest);
		ServerTravel.StartServer(this);
		RaidRoomsNetwork.StartServer();
		RaidParties.StartServer(this);
		InstanceScenes.StartServer(this);
		TrainingRooms.StartServer(this);
		StartCoroutine(LoadPlaceScenes());
	}

	public override void OnStartClient()
	{
		base.OnStartClient();
		// Mirror는 클라이언트를 끌 때 등록한 처리 함수를 모두 지우므로, 접속할 때마다 등록한다
		NetworkClient.RegisterHandler<TravelEndedMessage>(_ => PlaceTravelClient.Instance?.HandleTravelEnded());
		NetworkClient.RegisterHandler<TravelStartedMessage>(OnTravelStarted);
		RaidRoomsNetwork.StartClient();
		RaidParties.StartClient();
	}

	// [클라이언트] 서버가 이동을 시작했다. 서버는 답(TravelReadyMessage)이 와야 옮기므로, 화면을 막을 PlaceTravelClient가 없어도 답은 보낸다
	static void OnTravelStarted(TravelStartedMessage _)
	{
		if (PlaceTravelClient.Instance != null) PlaceTravelClient.Instance.HandleTravelStarted();
		else NetworkClient.Send(new TravelReadyMessage());
	}

	// [서버] 서버가 시작할 때 불러온 장소(로비·사냥터)의 씬. RaidParties가 레이드를 마친 파티원을 로비로 옮길 때 쓴다
	internal bool TryGetPlaceScene(string place, out Scene scene) => placeScenes.TryGetValue(place, out scene);

	IEnumerator LoadPlaceScenes()
	{
		var parameters = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D);

		// 동시에 불러오면 어느 씬이 어느 장소인지 구분하기 어려우므로 하나씩 차례로 불러온다
		foreach (string sceneName in PlaceScenes.FixedPlaces)
		{
			yield return SceneManager.LoadSceneAsync(sceneName, parameters);

			// LoadSceneAsync는 불러온 씬을 돌려주지 않는다. 하나씩 불러오므로 방금 불러온 씬은 목록의 마지막에 있다
			Scene scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

			// 불러오는 사이에 서버가 꺼졌으면, 방금 불러온 씬이 정리되지 않고 남지 않도록 내리고 멈춘다
			if (!NetworkServer.active)
			{
				SceneManager.UnloadSceneAsync(scene);
				yield break;
			}

			placeScenes[sceneName] = scene;
			simulatedScenes.Add(scene);
		}

		scenesLoaded = true;
		Debug.Log($"[InvokeNetworkManager] 장소 씬 {placeScenes.Count}개를 불러왔습니다: {string.Join(", ", placeScenes.Keys)}");

		// 장소 씬을 불러오는 사이에 들어온 연습장·레이드 던전 요청을 이제 처리한다
		InstanceScenes.OnPlaceScenesLoaded();
	}

	// [서버] 장소 씬을 모두 불러왔는지. InstanceScenes가 따로 여는 씬을 불러와도 되는지 볼 때 쓴다
	internal bool ScenesLoaded => scenesLoaded;

	// [서버] 서버가 물리를 직접 돌릴 씬 목록에 넣고 뺀다. InstanceScenes가 따로 여는 씬을 불러오고 내릴 때 쓴다
	internal void AddSimulatedScene(Scene scene) => simulatedScenes.Add(scene);
	internal void RemoveSimulatedScene(Scene scene) => simulatedScenes.Remove(scene);

	/// <summary>
	/// 서버에서 레이드 던전 씬을 한 번 더 불러온다. 파티마다 한 번씩 부른다. 서버에서만 부른다.<br/>
	/// 씬은 여러 프레임에 걸쳐 불러오므로 바로 돌려주지 않고, 다 불러온 뒤 onLoaded에 그 씬을 넘긴다.
	/// 레이드 던전은 모두 이름이 같으므로, 받은 Scene 값으로 던전을 구분해 보관한다.
	/// </summary>
	public void LoadRaidDungeon(Action<Scene> onLoaded) => InstanceScenes.Load(PlaceScenes.RaidDungeon, onLoaded);

	/// <summary>
	/// 서버에서 레이드 던전 씬 하나를 내린다. 서버에서만 부른다.<br/>
	/// 그 던전에 있던 플레이어는 이 함수를 부르기 전에 부르는 쪽이 다른 씬으로 옮겨야 한다. 남아 있으면 씬과 함께 사라진다.
	/// </summary>
	public void UnloadRaidDungeon(Scene scene) => InstanceScenes.Unload(scene);

	void FixedUpdate()
	{
		// 클라이언트는 장소 씬을 기본 물리 세계에 불러오므로 Unity가 자동으로 돌린다. 서버만 직접 돌린다
		if (!NetworkServer.active) return;

		for (int i = 0; i < simulatedScenes.Count; i++)
		{
			Scene scene = simulatedScenes[i];
			if (scene.isLoaded) scene.GetPhysicsScene2D().Simulate(Time.fixedDeltaTime);
		}
	}

	public override void OnServerAddPlayer(NetworkConnectionToClient conn)
	{
		// 플레이어는 캐릭터를 골라 「들어가기」에 성공했을 때만 SpawnPlayer로 만든다.
		// autoCreatePlayer를 꺼 두었으므로 이 함수가 불리면 캐릭터를 고르지 않은 요청이다
		Debug.LogWarning($"[InvokeNetworkManager] 캐릭터를 고르지 않은 플레이어 생성 요청을 무시합니다 (connId={conn.connectionId}).");
	}

	/// <summary>
	/// 서버에서 그 연결의 플레이어를 로비 시작 위치에 만든다. 서버에서만 부른다.<br/>
	/// InvokeAuthenticator가 「들어가기」에 성공한 연결에 대해 부른다. 이미 플레이어가 있는 연결이면 아무것도 하지 않는다.
	/// </summary>
	public void SpawnPlayer(NetworkConnectionToClient conn)
	{
		if (conn.identity != null) return;
		StartCoroutine(SpawnPlayerInLobby(conn));
	}

	/// <summary>
	/// 서버에서 그 연결의 플레이어를 접속은 둔 채 없앤다. 서버에서만 부른다.<br/>
	/// 같은 계정이 다시 로그인해 먼저 접속한 연결을 끊기 전에 InvokeAuthenticator가 부른다. 끊김 알림이 도착하는 동안 같은 캐릭터가 둘이 되지 않게 하기 위함이다.
	/// </summary>
	public void DespawnPlayer(NetworkConnectionToClient conn)
	{
		// 플레이어를 만들기 전(장소 씬을 기다리는 중)이어도 방에는 들어가 있을 수 있으므로, 플레이어가 있는지 보기 전에 뺀다
		RaidRoomsNetwork.OnPlayerDespawned(conn);
		if (conn.identity == null) return;

		NetworkServer.RemovePlayerForConnection(conn, RemovePlayerOptions.Destroy);
		ServerTravel.OnPlayerGone(conn);
		// 연습장이 있었으면 내린다. 플레이어를 없앤 뒤에 알려야 플레이어가 연습장과 함께 사라지지 않는다
		TrainingRooms.OnPlayerGone(conn);
		// 레이드 중이었으면 파티원 이탈이다. 플레이어를 없앤 뒤에 알려야, 남은 파티원이 없을 때 던전을 내려도 이 플레이어가 던전과 함께 사라지지 않는다
		RaidParties.OnMemberGone(conn);
	}

	IEnumerator SpawnPlayerInLobby(NetworkConnectionToClient conn)
	{
		// 서버가 켜지자마자 접속하면 장소 씬을 아직 불러오는 중일 수 있으므로 기다린다
		while (!scenesLoaded)
		{
			if (!NetworkServer.active) yield break;
			yield return null;
		}
		// 기다리는 사이에 접속이 끊겼으면 만들지 않는다
		if (!NetworkServer.connections.ContainsKey(conn.connectionId)) yield break;

		// 플레이어를 만드는 곳은 여기 하나다 (코드 작성 규칙 3)
		Scene lobby = placeScenes[PlaceScenes.Lobby];
		GameObject player = Instantiate(playerPrefab, FindStartPosition(lobby), Quaternion.identity);
		// 플레이어 콜라이더의 크기를 알아야 땅 위의 높이를 정할 수 있으므로, 만든 뒤에 땅 위로 옮긴다
		player.transform.position = PlaceOnGround(lobby, player.transform.position, player);
		// 여러 플레이어를 Hierarchy에서 구분하기 위해 연결 번호를 붙인다
		player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";
		// 부활할 때 부활 목적지로 옮긴다 (#310). 플레이어가 없어지면 이벤트도 함께 사라지므로 떼지 않는다
		if (player.TryGetComponent(out PlayerHealth health)) health.RespawnDue += () => OnPlayerRespawnDue(conn, player, health);

		// 씬을 불러오라는 알림이 플레이어 생성보다 먼저 가야, 클라이언트에 바닥이 생긴 뒤에 플레이어가 나타난다
		PutPlayerInScene(conn, player, lobby);
		// 진행도를 AddPlayerForConnection 전에 넣어야, 클라이언트가 플레이어를 처음 받을 때 DB의 값(레벨, 재료 등)도 함께 받는다
		ServerPlayerCreated?.Invoke(conn, player);
		NetworkServer.AddPlayerForConnection(conn, player);
		RaidRoomsNetwork.SendList(conn);
	}

	/// <summary>
	/// 서버에서 플레이어를 장소 씬에 넣는다. 서버에서만 부른다.<br/>
	/// - 서버: 플레이어 오브젝트를 그 씬으로 옮긴다. SceneInterestManagement가 이 소속을 보고 같은 씬의 오브젝트만 그 클라이언트에 보낸다.<br/>
	/// - 클라이언트: 그 씬을 불러오라고 알린다. 클라이언트는 물리 분리 없이 기본 물리 세계에 불러온다.
	/// <para>
	/// 씬 이름이 아니라 Scene 값을 받는다. 레이드 던전처럼 같은 씬을 여러 번 불러와도 그대로 쓰기 위함이다.<br/>
	/// 클라이언트가 이전 씬을 내리는 일과 플레이어 위치를 옮기는 일은 하지 않는다. 장소 사이 이동은 TravelPlayer가 이 함수를 부르며 함께 한다.
	/// </para>
	/// </summary>
	public void PutPlayerInScene(NetworkConnectionToClient conn, GameObject player, Scene scene)
	{
		// 씬을 옮기면 오브젝트가 꺼졌다 켜지며 위치 동기화 기준값이 서버에서만 0으로 지워진다. 그러면 클라이언트의 위치가 어긋나 보이므로, 기준값을 지키는 MoveToScene으로 옮긴다
		if (player.TryGetComponent(out NetworkRigidbodyReliable2DKeepBaseline keepBaseline)) keepBaseline.MoveToScene(scene);
		else SceneManager.MoveGameObjectToScene(player, scene);

		// 클라이언트의 플레이어는 늘 기준 씬(Base)에 있어서 씬 이름으로는 장소를 알 수 없으므로, 장소 이름을 값으로 보내 준다
		if (player.TryGetComponent(out PlayerPublicInfo info)) info.ServerSetPlace(scene.name);
		else Debug.LogError($"[InvokeNetworkManager] {player.name}에 PlayerPublicInfo가 없어서 현재 장소를 넣지 못했습니다. 플레이어 프리팹에 붙입니다.", player);

		// 호스트의 클라이언트는 서버와 씬을 같이 쓰므로 이미 불러와 있다. 알리면 Mirror가 "이미 불러왔다"는 경고를 남긴다
		if (conn is LocalConnectionToClient) return;
		conn.Send(new SceneMessage { sceneName = scene.name, sceneOperation = SceneOperation.LoadAdditive });
	}

	// [서버] 클라이언트의 장소 이동 요청(TravelRequestMessage)을 받는다
	void OnTravelRequest(NetworkConnectionToClient conn, TravelRequestMessage msg)
	{
		// 조작된 요청(로비를 거치지 않는 이동 등)을 막는 검사는 아직 하지 않는다. 넣을 때는 여기에 넣는다

		GameObject player = conn.identity != null ? conn.identity.gameObject : null;
		if (player == null)
		{
			RejectTravel(conn, msg.destination, "플레이어 오브젝트가 아직 없습니다");
			return;
		}
		// 서버가 이동을 알린 플레이어(레이드 시작·끝, 부활해 로비로)가 다른 이동을 함께 받지 않게 한다
		// 거절 답(TravelEndedMessage)은 보내지 않는다. 클라이언트는 이동 끝을 하나만 기다리므로, 보내면 서버가 알린 이동 전에 로딩 화면을 걷는다.
		// 이 요청을 보낸 클라이언트의 로딩 화면은 서버가 알린 이동의 TravelEndedMessage가 걷는다
		if (RaidParties.IsWaitingForTravel(conn) || ServerTravel.IsWaiting(conn))
		{
			Debug.LogWarning($"[InvokeNetworkManager] connId={conn.connectionId}의 {msg.destination} 이동 요청을 무시합니다: 서버가 알린 이동을 기다리는 중입니다");
			return;
		}
		if (player.scene.name == msg.destination)
		{
			RejectTravel(conn, msg.destination, "이미 그 장소에 있습니다");
			return;
		}
		// 레이드 던전에는 파티장이 시작할 때 서버가 옮겨 줄 때만 들어간다 (RaidParties)
		if (msg.destination == PlaceScenes.RaidDungeon)
		{
			RejectTravel(conn, msg.destination, "레이드 던전은 레이드 방에서 시작해야 갑니다");
			return;
		}

		// 연습장은 요청한 플레이어마다 씬을 한 번 더 불러온 뒤 옮긴다 (TrainingRooms)
		if (msg.destination == PlaceScenes.TrainingRoom)
		{
			// 씬을 불러오는 동안 같은 연결의 요청이 또 오면 씬이 두 개 열리므로 무시한다
			// 거절 답(TravelEndedMessage)은 보내지 않는다. 먼저 한 요청의 로딩 화면을 이 답이 일찍 걷기 때문이다
			if (!TrainingRooms.TryOpen(conn))
				Debug.LogWarning($"[InvokeNetworkManager] connId={conn.connectionId}의 {msg.destination} 이동 요청을 무시합니다: 씬을 불러오는 중입니다");
			return;
		}

		if (!placeScenes.TryGetValue(msg.destination, out Scene target))
		{
			RejectTravel(conn, msg.destination, "모르는 장소입니다");
			return;
		}
		TravelPlayer(conn, player, target);
	}

	// 이동하지 않을 때도 답을 보내야, 클라이언트가 로딩 화면을 닫고 액션 맵을 다시 활성화한다
	static void RejectTravel(NetworkConnectionToClient conn, string destination, string reason)
	{
		Debug.LogWarning($"[InvokeNetworkManager] connId={conn.connectionId}의 {destination} 이동 요청을 거절합니다: {reason}");
		conn.Send(new TravelEndedMessage());
	}

	/// <summary>
	/// 서버에서 플레이어를 다른 장소 씬으로 옮긴다. 서버에서만 부른다.
	/// <para>
	/// - 서버: 플레이어가 실행 중인 함수를 모두 끊고, 그 씬으로 옮기고, 도착 위치(ArrivalPoint)로 순간이동시킨다.<br/>
	/// - 클라이언트: 이전 씬 언로드 → 새 씬 로드 → teleport → TravelEndedMessage 순서로 받는다.<br/>
	/// - 이전 씬이 연습장이면 그 연습장을 내린다.<br/>
	/// - 이전 씬이 레이드 던전이면 RaidParties에 알린다. 던전을 내릴지는 RaidParties가 정한다.
	/// </para>
	/// <para>
	/// 씬 이름이 아니라 Scene 값을 받는다. 연습장·레이드 던전처럼 같은 씬을 여러 번 불러와도 그대로 쓰기 위함이다.
	/// </para>
	/// </summary>
	public void TravelPlayer(NetworkConnectionToClient conn, GameObject player, Scene target)
	{
		// 이전 씬에서 시전한 함수가 새 씬에서 계속 실행되지 않도록 끊는다 (몬스터를 기다리던 작업, 이전 씬에 만들 검 등)
		if (player.TryGetComponent(out FunctionExecutor executor)) executor.InterruptAll();

		Scene previous = player.scene;

		// 클라이언트는 알린 씬을 쌓아 두기만 하므로, 이전 장소의 바닥과 오브젝트가 남지 않도록 내리라고 알린다
		// 호스트의 클라이언트는 서버와 씬을 같이 쓰므로 알리지 않는다 (서버는 이전 씬을 계속 돌려야 함)
		if (!(conn is LocalConnectionToClient))
			conn.Send(new SceneMessage { sceneName = previous.name, sceneOperation = SceneOperation.UnloadAdditive });

		PutPlayerInScene(conn, player, target);

		// Player는 클라이언트가 위치를 정하므로(Client To Server) 서버에서 위치만 바꾸면 클라이언트가 덮어쓴다
		// ServerTeleport는 서버 쪽 위치를 바꾸고 클라이언트에도 순간이동(RpcTeleport)을 보낸다
		// 씬 메시지 뒤에 보내는 이유: 클라이언트는 새 씬을 다 로드한 뒤에 teleport를 처리하므로, 로드하는 동안 떨어진 플레이어도 도착 위치로 돌아온다
		// 로드하는 동안 클라이언트의 플레이어가 밟을 땅이 없어 떨어지지 않도록, PlaceTravelClient가 이동하는 동안 움직임을 잠근다 (#216)
		Vector3 position = PlaceOnGround(target, FindArrivalPosition(target, previous.name), player);
		player.GetComponent<NetworkTransformBase>().ServerTeleport(position, player.transform.rotation);

		// 소환검은 플레이어를 따라 새 씬으로 옮기고, 던져서 떨어진 물리 검은 손으로 돌려받는다 (#325)
		// 이전 씬을 내리기(아래 OnLeftRoom, OnLeftDungeon) 전에 해야 연습장·레이드를 나올 때 검이 같이 Destroy되지 않는다
		if (player.TryGetComponent(out SummonedSwords summonedSwords)) summonedSwords.MoveToScene(target);
		if (player.TryGetComponent(out PhysicalSword physicalSword)) physicalSword.Recall();

		// 죽은 채로 옮겨진 플레이어(PlayerHealth.DeferRespawn으로 부활을 미뤘음)는 도착한 뒤, 로딩 화면이 걷히기 전에 살린다 (#391)
		if (player.TryGetComponent(out PlayerHealth arrivedHealth)) arrivedHealth.CompleteRespawn();

		// 클라이언트는 받은 메시지를 보낸 순서대로 처리하므로, 이 메시지를 받으면 새 씬 로드와 teleport까지 끝난 것이다
		conn.Send(new TravelEndedMessage());

		// 연습장에서 나온 것이면 그 연습장을 내린다 (TrainingRooms)
		TrainingRooms.OnLeftRoom(conn, previous);
		// 레이드가 끝나 로비로 온 것이면 기록에서 빠지고, 레이드 도중이면 파티원 이탈이다. 남은 파티원이 없으면 RaidParties가 던전을 내린다
		if (InstanceScenes.IsRaidScene(previous)) RaidParties.OnLeftDungeon(conn, previous);
	}

	// [서버] 플레이어가 부활할 때가 된 순간(PlayerHealth.RespawnDue). 옮길 곳이 있으면 죽은 채로 두고, 로딩 화면이 덮인 뒤 옮기고 나서 살린다 (#310, #391)
	// 죽은 자리에서 먼저 살아나 보이지 않게 하기 위함이다. TravelPlayer가 TravelEndedMessage를 보내기 직전에 미뤄 둔 부활을 실행한다
	void OnPlayerRespawnDue(NetworkConnectionToClient conn, GameObject player, PlayerHealth health)
	{
		if (player == null || conn.identity == null || conn.identity.gameObject != player) return;
		// 서버가 알린 다른 이동(레이드 끝에 로비로 등)을 기다리는 중이면 그 이동이 끝날 때 살린다
		if (ServerTravel.IsWaiting(conn) || RaidParties.IsWaitingForTravel(conn))
		{
			health.DeferRespawn();
			return;
		}
		if (!TryGetRespawnDestination(player, out Scene target)) return;

		health.DeferRespawn();
		ServerTravel.Begin(conn, () =>
		{
			// 기다리는 사이에 플레이어가 없어졌거나 이미 그 장소면 옮기지 않고 그 자리에서 살린 뒤 로딩 화면만 걷는다
			if (conn.identity != null && conn.identity.gameObject == player && player.scene != target) TravelPlayer(conn, player, target);
			else
			{
				if (health != null) health.CompleteRespawn();
				conn.Send(new TravelEndedMessage());
			}
		});
	}

	/// <summary>
	/// [서버] 부활한 플레이어를 옮길 장소. 옮기지 않으면(그 자리에서 부활) false.<br/>
	/// 부활 목적지를 정하는 곳은 여기 하나다. 장소마다 다르게 하려면 이 함수에서 플레이어가 있는 씬의 설정을 읽게 바꾼다 (사용자 결정 #310: 지금은 레이드 밖은 모두 로비로)
	/// </summary>
	bool TryGetRespawnDestination(GameObject player, out Scene target)
	{
		target = default;
		Scene current = player.scene;
		// 레이드 던전: 부활하지 않고 관전하다가, 레이드가 끝나면 RaidParties가 파티원 모두를 로비로 옮긴다
		if (InstanceScenes.IsRaidScene(current)) return false;
		// 로비에서 죽었으면 그 자리에서 부활한다
		if (current.name == PlaceScenes.Lobby) return false;
		return TryGetPlaceScene(PlaceScenes.Lobby, out target);
	}

	// 장소 사이를 이동한 플레이어가 도착할 위치를 찾는다.
	// 떠나온 장소(fromPlace)가 같은 ArrivalPoint가 있으면 그 위치, 없으면 씬의 NetworkStartPosition 위치
	static Vector3 FindArrivalPosition(Scene scene, string fromPlace)
	{
		bool hasArrivalPoint = false;
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			foreach (ArrivalPoint point in root.GetComponentsInChildren<ArrivalPoint>())
			{
				if (point.FromPlace == fromPlace) return point.transform.position;
				hasArrivalPoint = true;
			}
		}

		// 사냥터처럼 ArrivalPoint를 두지 않은 씬은 시작 위치가 정상이다.
		// ArrivalPoint가 있는데 맞는 것이 없으면 fromPlace 오타일 수 있으므로 알린다
		if (hasArrivalPoint)
			Debug.LogWarning($"[InvokeNetworkManager] {scene.name} 씬에 {fromPlace}에서 온 플레이어의 ArrivalPoint가 없어서 시작 위치에 도착시킵니다.");
		return FindStartPosition(scene);
	}

	// 씬 안의 NetworkStartPosition 위치를 찾는다.
	// Mirror의 GetStartPosition()은 불러온 모든 씬의 시작 위치를 섞어서 고르므로 쓰지 않는다
	static Vector3 FindStartPosition(Scene scene)
	{
		foreach (GameObject root in scene.GetRootGameObjects())
		{
			NetworkStartPosition start = root.GetComponentInChildren<NetworkStartPosition>();
			if (start != null) return start.transform.position;
		}

		Debug.LogWarning($"[InvokeNetworkManager] {scene.name} 씬에 NetworkStartPosition이 없어서 (0, 0)에 만듭니다.");
		return Vector3.zero;
	}

	// 표시(시작 위치·도착 위치) 바로 아래의 땅을 찾아, 플레이어 콜라이더의 아래쪽이 땅 윗면에 닿는 위치를 돌려준다.
	// 표시를 땅보다 높게 두면 플레이어가 생기자마자 떨어지므로, 표시를 땅에 딱 맞게 두지 않아도 되게 하기 위함
	// 아래 GroundSearchDistance 안에 땅이 없으면 표시 위치를 그대로 쓴다
	static Vector3 PlaceOnGround(Scene scene, Vector3 point, GameObject player)
	{
		if (!player.TryGetComponent(out Collider2D body)) return point;

		// 소환검·애니메이션과 같은 기준으로 Ground·Platform만 땅으로 본다. 다른 플레이어나 몬스터 위에 세우지 않기 위함
		var filter = new ContactFilter2D { useTriggers = false };
		filter.SetLayerMask(LayerMask.GetMask("Ground", "Platform"));

		// 장소 씬은 물리 세계가 따로 있으므로 그 씬의 물리 세계에 묻는다
		int count = scene.GetPhysicsScene2D().Raycast(point, Vector2.down, GroundSearchDistance, filter, groundHits);
		if (count == 0)
		{
			Debug.LogWarning($"[InvokeNetworkManager] {scene.name} 씬의 {point} 아래 {GroundSearchDistance} 안에 땅이 없어서 그 위치에 그대로 둡니다.");
			return point;
		}

		float footOffset = player.transform.position.y - body.bounds.min.y;
		return new Vector3(point.x, groundHits[0].point.y + footOffset, point.z);
	}

	public override void OnServerDisconnect(NetworkConnectionToClient conn)
	{
		// 끊긴 계정을 접속 중 목록에서 지워야 그 계정이 다시 로그인할 때 이미 끊긴 연결을 또 끊으려 하지 않는다
		if (authenticator is InvokeAuthenticator accountAuthenticator) accountAuthenticator.OnServerDisconnected(conn);
		RaidRoomsNetwork.OnServerDisconnect(conn);

		base.OnServerDisconnect(conn);
		ServerTravel.OnPlayerGone(conn);
		// 연습장이 있었으면 내린다. base가 플레이어를 없앤 뒤에 알려야 플레이어가 연습장과 함께 사라지지 않는다
		TrainingRooms.OnPlayerGone(conn);
		// 레이드 중이었으면 파티원 이탈이다. base가 플레이어를 없앤 뒤에 알려야, 던전을 내려도 이 플레이어가 던전과 함께 사라지지 않는다
		RaidParties.OnMemberGone(conn);
	}

	public override void OnStopServer()
	{
		base.OnStopServer();
		scenesLoaded = false;
		InstanceScenes.StopServer();
		TrainingRooms.StopServer();
		RaidRoomsNetwork.StopServer();
		RaidParties.StopServer();
		ServerTravel.StopServer();

		// 에디터에서 서버를 껐다 켤 때 같은 씬이 두 번 불러와지지 않도록 내린다
		// 오브젝트가 꺼진 채로 불리면 코루틴을 시작할 수 없으므로 확인한다
		if (gameObject.activeInHierarchy) StartCoroutine(UnloadServerScenes());
	}

	IEnumerator UnloadServerScenes()
	{
		// 장소 씬과 따로 연 씬(연습장, 레이드 던전)이 모두 simulatedScenes에 있으므로 이 목록으로 모두 내린다
		// 내리는 동안 FixedUpdate가 물리를 돌리지 않도록 목록부터 비운다
		var scenes = new List<Scene>(simulatedScenes);
		simulatedScenes.Clear();
		placeScenes.Clear();

		foreach (Scene scene in scenes)
			if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
	}

	public override void OnStopClient()
	{
		base.OnStopClient();
		RaidRoomsNetwork.StopClient();

		// 호스트면 서버와 씬을 같이 쓰므로, 서버가 OnStopServer에서 내린다
		if (NetworkServer.active) return;
		// 장소 씬이 남은 채로 다시 접속하면 이전 접속 때의 바닥과 오브젝트가 남으므로 내린다
		if (gameObject.activeInHierarchy) StartCoroutine(UnloadClientPlaceScenes());
	}

	IEnumerator UnloadClientPlaceScenes()
	{
		// 기준 씬(활성 씬)만 남기고 서버가 불러오라고 알린 장소 씬을 모두 내린다
		Scene baseScene = SceneManager.GetActiveScene();
		var scenes = new List<Scene>();
		for (int i = 0; i < SceneManager.sceneCount; i++)
		{
			Scene scene = SceneManager.GetSceneAt(i);
			if (scene != baseScene) scenes.Add(scene);
		}

		foreach (Scene scene in scenes)
			if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
	}
}
