using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임 중에 생기고 사라지는 네트워크 오브젝트(몬스터, 투사체, 소환검 등)를 만들고 없애는 공용 함수.
/// (생성·삭제를 한 곳에 모으기 위함, 코드 작성 규칙 3)
/// <para>
/// 네트워크 오브젝트: 서버가 판정에 쓰거나, 모든 플레이어가 같은 위치·상태로 봐야 하는 오브젝트. 프리팹에 NetworkIdentity를 붙인다.<br/>
/// 보여 주기만 하는 오브젝트(이펙트, 데미지 숫자)는 네트워크 오브젝트가 아니므로 이 함수로 만들지 않는다.
/// </para>
/// <para>
/// 사용법<br/>
/// - Instantiate 대신 Spawn, Destroy 대신 Despawn을 부른다.<br/>
/// - 프리팹은 NetworkIdentity를 붙이고, Base 씬 NetworkManager의 Registered Spawnable Prefabs에 등록한다.<br/>
/// - 네트워크를 켜지 않은 테스트 씬에서도 그대로 동작한다. 이때는 오브젝트를 만들고 없애기만 한다.
/// </para>
/// </summary>
public static class NetworkSpawner
{
	/// <summary>
	/// 네트워크 오브젝트를 scene에 만들고, 그 씬에 있는 클라이언트들에게 보인다. 서버에서만 부른다.<br/>
	/// scene에는 보통 부르는 쪽의 씬(gameObject.scene)을 넘긴다.
	/// Instantiate는 활성 씬(서버에서는 Base)에 만들어서, 그대로 두면 장소 씬의 플레이어와 판정되지 않고 클라이언트에게도 보이지 않기 때문이다.<br/>
	/// 만든 오브젝트를 돌려준다. 프리팹 설정이 잘못되었거나 클라이언트에서 부르면 원인을 로그로 남기고 null을 돌려준다.
	/// </summary>
	public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Scene scene)
	{
		if (prefab == null)
		{
			Debug.LogError("[NetworkSpawner] 만들 프리팹이 비어 있습니다.");
			return null;
		}

		if (NetworkServer.active)
		{
			if (!IsSpawnable(prefab)) return null;

			GameObject obj = Create(prefab, position, rotation, scene);
			NetworkServer.Spawn(obj);
			return obj;
		}

		if (NetworkClient.active)
		{
			Debug.LogError($"[NetworkSpawner] 네트워크 오브젝트는 서버만 만듭니다. 클라이언트에서 {prefab.name}을(를) 만들려고 했습니다.");
			return null;
		}

		// 네트워크를 켜지 않은 테스트 씬: 각자 Mirror 없이 테스트할 수 있도록 만들기만 한다
		return Create(prefab, position, rotation, scene);
	}

	/// <summary>
	/// Spawn으로 만든 오브젝트를 바로 없앤다. 서버에서 없애면 모든 클라이언트에서도 사라진다. 서버에서만 부른다.<br/>
	/// 시간을 두고 없애야 하면(예: 사망 후 몇 초 뒤) 기다리는 일은 부르는 쪽이 한다.
	/// </summary>
	public static void Despawn(GameObject obj)
	{
		if (obj == null) return;

		if (NetworkServer.active)
		{
			NetworkServer.Destroy(obj);
			return;
		}

		if (NetworkClient.active)
		{
			Debug.LogError($"[NetworkSpawner] 네트워크 오브젝트는 서버만 없앱니다. 클라이언트에서 {obj.name}을(를) 없애려고 했습니다.");
			return;
		}

		Object.Destroy(obj);
	}

	static GameObject Create(GameObject prefab, Vector3 position, Quaternion rotation, Scene scene)
	{
		GameObject obj = Object.Instantiate(prefab, position, rotation);
		// 씬마다 물리 세계가 따로 있으므로, 받은 씬으로 옮겨야 그 씬의 오브젝트와 판정된다
		if (scene.IsValid() && obj.scene != scene) SceneManager.MoveGameObjectToScene(obj, scene);
		return obj;
	}

	// Mirror가 클라이언트에 만들어 줄 수 있는 프리팹인지 확인한다.
	// 조건이 빠지면 Mirror가 원인을 알기 어려운 오류를 내므로, 여기서 원인을 이름으로 먼저 알려 준다
	static bool IsSpawnable(GameObject prefab)
	{
		if (!prefab.TryGetComponent(out NetworkIdentity _))
		{
			Debug.LogError($"[NetworkSpawner] {prefab.name}에 NetworkIdentity가 없습니다. " +
				"네트워크 오브젝트라면 프리팹에 NetworkIdentity를 붙이고, 보여 주기만 하는 오브젝트(이펙트 등)라면 이 함수로 만들지 않습니다.");
			return false;
		}

		NetworkManager manager = NetworkManager.singleton;
		if (manager != null && manager.playerPrefab != prefab && !manager.spawnPrefabs.Contains(prefab))
		{
			Debug.LogError($"[NetworkSpawner] {prefab.name}이(가) 등록되어 있지 않습니다. " +
				"Base 씬 NetworkManager의 Registered Spawnable Prefabs에 이 프리팹을 넣으세요.");
			return false;
		}

		return true;
	}
}
