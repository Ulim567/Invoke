using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 연습장과 레이드 던전처럼 입장마다 같은 씬을 한 번 더 불러와 따로 쓰는 씬을 불러오고 내리는 도구 (#301).
/// <para>
/// 서버가 하는 일<br/>
/// - 씬을 한 번 더 불러오고(Load) 내린다(Unload). 같은 이름의 씬이 여러 개이므로 Scene 값으로 구분해 보관한다.<br/>
/// - 불러오기 요청을 한 줄로 세워 하나씩 처리한다.<br/>
/// - 씬을 언제 불러오고 내릴지는 정하지 않는다. 연습장은 TrainingRooms가, 레이드 던전은 RaidParties가 정한다.
/// </para>
/// <para>
/// 사용법<br/>
/// - InvokeNetworkManager가 서버를 켜고 끌 때 부른다.<br/>
/// - TrainingRooms는 이 클래스의 Load·Unload를 직접 부르고, RaidParties는 InvokeNetworkManager.LoadRaidDungeon·UnloadRaidDungeon을 통해 쓴다. 그 밖의 스크립트는 부르지 않는다.
/// </para>
/// </summary>
public static class InstanceScenes
{
	static InvokeNetworkManager manager;

	// 서버가 따로 불러온 씬. 같은 이름이 여러 개이므로 Scene 값으로 구분한다
	static readonly List<Scene> scenes = new List<Scene>();
	// 따로 불러올 씬의 요청. 동시에 불러오면 어느 씬이 어느 요청의 것인지 섞이므로 줄을 세워 하나씩 처리한다
	static readonly Queue<(string sceneName, Action<Scene> onLoaded)> requests = new Queue<(string, Action<Scene>)>();
	static bool loading;

	/// <summary>[서버] 서버를 켤 때 InvokeNetworkManager가 부른다.</summary>
	public static void StartServer(InvokeNetworkManager networkManager)
	{
		manager = networkManager;
	}

	/// <summary>
	/// [서버] 처리하지 못한 요청과 기록을 버린다. 서버를 끌 때 부른다.
	/// 불러온 씬은 InvokeNetworkManager가 서버를 끌 때 함께 내린다.
	/// </summary>
	public static void StopServer()
	{
		requests.Clear();
		scenes.Clear();
		manager = null;
	}

	/// <summary>
	/// [서버] 장소 씬(로비·사냥터)을 다 불러왔을 때 InvokeNetworkManager가 부른다.
	/// 장소 씬을 불러오는 사이에 들어온 요청을 이제 처리한다.
	/// </summary>
	public static void OnPlaceScenesLoaded() => TryStartLoads();

	/// <summary>
	/// [서버] 연습장·레이드 던전 씬을 한 번 더 불러온다.<br/>
	/// 씬은 여러 프레임에 걸쳐 불러오므로 바로 돌려주지 않고, 다 불러온 뒤 onLoaded에 그 씬을 넘긴다.
	/// 플레이어를 그 씬에 넣을 때는 InvokeNetworkManager.TravelPlayer에, 씬을 없앨 때는 Unload에 이 Scene 값을 넘긴다.
	/// </summary>
	public static void Load(string sceneName, Action<Scene> onLoaded)
	{
		if (!NetworkServer.active || manager == null)
		{
			Debug.LogWarning($"[InstanceScenes] {sceneName} 씬은 서버에서만 불러올 수 있습니다.");
			return;
		}

		requests.Enqueue((sceneName, onLoaded));
		TryStartLoads();
	}

	/// <summary>
	/// [서버] Load로 불러온 씬 하나를 내린다.<br/>
	/// 그 씬에 있던 플레이어는 이 함수를 부르기 전에 부르는 쪽이 다른 씬으로 옮겨야 한다. 남아 있으면 씬과 함께 사라진다.
	/// </summary>
	public static void Unload(Scene scene)
	{
		if (!scenes.Remove(scene))
		{
			Debug.LogWarning("[InstanceScenes] 이 서버가 따로 불러온 씬이 아니어서 내리지 않습니다.");
			return;
		}

		// 내리는 동안 FixedUpdate가 물리를 돌리지 않도록 목록에서 먼저 뺀다
		string sceneName = scene.name;
		manager.RemoveSimulatedScene(scene);
		SceneManager.UnloadSceneAsync(scene);
		Debug.Log($"[InstanceScenes] {sceneName} 씬을 내렸습니다. 따로 연 씬이 지금 {scenes.Count}개");
	}

	/// <summary>[서버] 서버가 따로 불러온 레이드 던전이면 true. 이름만 보면 안 되고 불러온 기록에 있어야 한다 (내려 가는 중인 씬 등을 거르기 위함)</summary>
	public static bool IsRaidScene(Scene scene) => scene.name == PlaceScenes.RaidDungeon && scenes.Contains(scene);

	static void TryStartLoads()
	{
		// 장소 씬을 불러오는 중에 시작하면 "방금 불러온 씬은 목록의 마지막" 규칙이 깨지므로, 장소 씬을 다 불러온 뒤에 시작한다
		if (manager == null || loading || !manager.ScenesLoaded || requests.Count == 0) return;
		manager.StartCoroutine(LoadScenes());
	}

	static IEnumerator LoadScenes()
	{
		loading = true;
		var parameters = new LoadSceneParameters(LoadSceneMode.Additive, LocalPhysicsMode.Physics2D);

		while (requests.Count > 0)
		{
			(string sceneName, Action<Scene> onLoaded) = requests.Dequeue();

			yield return SceneManager.LoadSceneAsync(sceneName, parameters);
			// 하나씩 불러오므로 방금 불러온 씬은 목록의 마지막에 있다
			Scene scene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);

			// 불러오는 사이에 서버가 꺼졌으면, 방금 불러온 씬이 정리되지 않고 남지 않도록 내리고 멈춘다
			if (!NetworkServer.active || manager == null)
			{
				SceneManager.UnloadSceneAsync(scene);
				break;
			}

			scenes.Add(scene);
			manager.AddSimulatedScene(scene);
			Debug.Log($"[InstanceScenes] {sceneName} 씬을 불러왔습니다. 따로 연 씬이 지금 {scenes.Count}개");
			onLoaded?.Invoke(scene);
		}

		loading = false;
	}
}
