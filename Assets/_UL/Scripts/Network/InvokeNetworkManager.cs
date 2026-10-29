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
/// - 시작할 때 로비·사냥터·연습장 씬(PlaceScenes.FixedPlaces)을 기준 씬 위에 겹쳐(Additive) 불러온다.<br/>
/// - 씬마다 물리 세계를 따로 두어(LocalPhysicsMode.Physics2D), 다른 씬의 오브젝트끼리 부딪히거나 판정되지 않게 한다.<br/>
/// - 따로 둔 물리 세계는 Unity가 자동으로 돌리지 않으므로, FixedUpdate마다 직접 돌린다.<br/>
/// - 접속한 플레이어를 로비 씬의 시작 위치에 만든다.<br/>
/// - 요청이 오면 레이드 던전 씬을 파티마다 한 번씩 더 불러오고 내린다.
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
	// 장소 이름 → 서버가 불러온 씬. 한 번만 불러오는 장소(로비·사냥터·연습장)만 담는다
	readonly Dictionary<string, Scene> placeScenes = new Dictionary<string, Scene>();
	// 서버가 물리를 직접 돌릴 씬. 레이드 던전은 같은 씬을 여러 번 불러오므로 이름이 아니라 Scene 값으로 담는다
	readonly List<Scene> simulatedScenes = new List<Scene>();

	// 서버가 불러온 레이드 던전. 모두 이름이 같으므로 Scene 값으로 구분한다
	readonly List<Scene> raidScenes = new List<Scene>();
	// 레이드 던전 불러오기 요청. 동시에 불러오면 어느 씬이 어느 요청의 것인지 섞이므로 줄을 세워 하나씩 처리한다
	readonly Queue<Action<Scene>> raidLoadRequests = new Queue<Action<Scene>>();
	bool loadingRaid;

	// 장소 씬을 모두 불러왔으면 true. 씬을 다 불러오기 전에 접속한 플레이어는 이 값이 켜질 때까지 기다린다
	bool scenesLoaded;

	public override void OnStartServer()
	{
		base.OnStartServer();
		StartCoroutine(LoadPlaceScenes());
	}

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

		// 장소 씬을 불러오는 사이에 들어온 레이드 던전 요청을 이제 처리한다
		TryStartRaidLoads();
	}

	/// <summary>
	/// 서버에서 레이드 던전 씬을 한 번 더 불러온다. 파티마다 한 번씩 부른다. 서버에서만 부른다.<br/>
	/// 씬은 여러 프레임에 걸쳐 불러오므로 바로 돌려주지 않고, 다 불러온 뒤 onLoaded에 그 씬을 넘긴다.
	/// <para>
	/// 레이드 던전은 모두 이름이 같으므로, 받은 Scene 값으로 던전을 구분해 보관한다.<br/>
	/// 플레이어를 던전에 넣을 때는 PutPlayerInScene에, 던전을 없앨 때는 UnloadRaidDungeon에 이 Scene 값을 넘긴다.
	/// </para>
	/// </summary>
	public void LoadRaidDungeon(Action<Scene> onLoaded)
	{
		if (!NetworkServer.active)
		{
			Debug.LogWarning("[InvokeNetworkManager] 레이드 던전은 서버에서만 불러올 수 있습니다.");
			return;
		}

		raidLoadRequests.Enqueue(onLoaded);
		TryStartRaidLoads();
	}

	void TryStartRaidLoads()
	{
		// 장소 씬을 불러오는 중에 시작하면 "방금 불러온 씬은 목록의 마지막" 규칙이 깨지므로, 장소 씬을 다 불러온 뒤에 시작한다
		if (loadingRaid || !scenesLoaded || raidLoadRequests.Count == 0) return;
		StartCoroutine(LoadRaidDungeons());
	}

	IEnumerator LoadRaidDungeons()
	{
		loadingRaid = true;
		var parameters = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D);

		while (raidLoadRequests.Count > 0)
		{
			Action<Scene> onLoaded = raidLoadRequests.Dequeue();

			yield return SceneManager.LoadSceneAsync(PlaceScenes.RaidDungeon, parameters);
			// 하나씩 불러오므로 방금 불러온 씬은 목록의 마지막에 있다
			Scene scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

			// 불러오는 사이에 서버가 꺼졌으면, 방금 불러온 씬이 정리되지 않고 남지 않도록 내리고 멈춘다
			if (!NetworkServer.active)
			{
				SceneManager.UnloadSceneAsync(scene);
				break;
			}

			raidScenes.Add(scene);
			simulatedScenes.Add(scene);
			Debug.Log($"[InvokeNetworkManager] 레이드 던전을 불러왔습니다. 지금 {raidScenes.Count}개");
			onLoaded?.Invoke(scene);
		}

		loadingRaid = false;
	}

	/// <summary>
	/// 서버에서 레이드 던전 씬 하나를 내린다. 서버에서만 부른다.<br/>
	/// 그 던전에 있던 플레이어는 이 함수를 부르기 전에 부르는 쪽이 다른 씬으로 옮겨야 한다. 남아 있으면 씬과 함께 사라진다.
	/// </summary>
	public void UnloadRaidDungeon(Scene scene)
	{
		if (!raidScenes.Remove(scene))
		{
			Debug.LogWarning("[InvokeNetworkManager] 이 서버가 불러온 레이드 던전이 아니어서 내리지 않습니다.");
			return;
		}

		// 내리는 동안 FixedUpdate가 물리를 돌리지 않도록 목록에서 먼저 뺀다
		simulatedScenes.Remove(scene);
		SceneManager.UnloadSceneAsync(scene);
		Debug.Log($"[InvokeNetworkManager] 레이드 던전을 내렸습니다. 지금 {raidScenes.Count}개");
	}

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
		// Mirror 기본 코드는 플레이어를 기준 씬에 만들므로 부르지 않고, 로비에 만든다
		StartCoroutine(SpawnPlayerInLobby(conn));
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
		// 여러 플레이어를 Hierarchy에서 구분하기 위해 연결 번호를 붙인다
		player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";

		// 씬을 불러오라는 알림이 플레이어 생성보다 먼저 가야, 클라이언트에 바닥이 생긴 뒤에 플레이어가 나타난다
		PutPlayerInScene(conn, player, lobby);
		NetworkServer.AddPlayerForConnection(conn, player);
	}

	/// <summary>
	/// 서버에서 플레이어를 장소 씬에 넣는다. 서버에서만 부른다.<br/>
	/// - 서버: 플레이어 오브젝트를 그 씬으로 옮긴다. SceneInterestManagement가 이 소속을 보고 같은 씬의 오브젝트만 그 클라이언트에 보낸다.<br/>
	/// - 클라이언트: 그 씬을 불러오라고 알린다. 클라이언트는 물리 분리 없이 기본 물리 세계에 불러온다.
	/// <para>
	/// 씬 이름이 아니라 Scene 값을 받는다. 레이드 던전처럼 같은 씬을 여러 번 불러와도 그대로 쓰기 위함이다.<br/>
	/// 클라이언트가 이전 씬을 내리는 일과 플레이어 위치를 옮기는 일은 하지 않는다. 장소 사이 이동은 이 함수를 부른 뒤 따로 한다.
	/// </para>
	/// </summary>
	public void PutPlayerInScene(NetworkConnectionToClient conn, GameObject player, Scene scene)
	{
		SceneManager.MoveGameObjectToScene(player, scene);

		// 호스트의 클라이언트는 서버와 씬을 같이 쓰므로 이미 불러와 있다. 알리면 Mirror가 "이미 불러왔다"는 경고를 남긴다
		if (conn is LocalConnectionToClient) return;
		conn.Send(new SceneMessage { sceneName = scene.name, sceneOperation = SceneOperation.LoadAdditive });
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

	public override void OnStopServer()
	{
		base.OnStopServer();
		scenesLoaded = false;
		// 처리하지 못한 레이드 던전 요청은 버린다
		raidLoadRequests.Clear();

		// 에디터에서 서버를 껐다 켤 때 같은 씬이 두 번 불러와지지 않도록 내린다
		// 오브젝트가 꺼진 채로 불리면 코루틴을 시작할 수 없으므로 확인한다
		if (gameObject.activeInHierarchy) StartCoroutine(UnloadServerScenes());
	}

	IEnumerator UnloadServerScenes()
	{
		// 장소 씬과 레이드 던전이 모두 simulatedScenes에 있으므로 이 목록으로 모두 내린다
		// 내리는 동안 FixedUpdate가 물리를 돌리지 않도록 목록부터 비운다
		var scenes = new List<Scene>(simulatedScenes);
		simulatedScenes.Clear();
		placeScenes.Clear();
		raidScenes.Clear();

		foreach (Scene scene in scenes)
			if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
	}

	public override void OnStopClient()
	{
		base.OnStopClient();

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
