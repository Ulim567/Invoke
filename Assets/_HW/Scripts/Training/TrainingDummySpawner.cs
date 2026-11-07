using Mirror;
using UnityEngine;

/// <summary>
/// 연습장 허수아비를 이 오브젝트 자리에 1마리 만든다 (#297). 연습장 씬(TrainingRoom)의 빈 오브젝트에 붙인다.
/// <para>
/// - 허수아비는 네트워크 오브젝트(NetworkIdentity, Base NetworkManager에 등록)라서 서버가 NetworkSpawner.Spawn으로 이 씬에 만든다.
///   그래야 전장 휩쓸기처럼 대상 배열로 시전할 때 서버가 허수아비를 알아본다.<br/>
/// - 땅을 찾지 않고 이 오브젝트 위치에 그대로 만든다. 허수아비 발이 땅에 닿게 이 오브젝트를 놓는다.<br/>
/// - 허수아비는 체력이 줄지 않아(Invulnerable) 죽지 않으므로 한 번만 만든다. 서버(Host 포함)에서만 만들고, 서버가 다시 켜지면 다시 만든다.<br/>
/// - 연습장은 플레이어마다 따로 열린다(#301). 이 스포너도 연습장마다 하나라 허수아비도 연습장마다 하나. 연습장 씬이 내려가면(OnDestroy) 허수아비도 없앤다.
/// </para>
/// </summary>
public class TrainingDummySpawner : MonoBehaviour
{
	[SerializeField, Tooltip("허수아비 프리팹 (Assets/_HW/Training/TrainingDummy). NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	GameObject dummyPrefab;

	GameObject spawned;
	bool running;

	void Awake()
	{
		if (dummyPrefab == null) Debug.LogWarning($"[TrainingDummySpawner] {name}: Dummy Prefab이 비어 있어 허수아비를 만들지 않습니다.", this);
	}

	void Update()
	{
		if (!NetworkServer.active)
		{
			running = false;
			return;
		}
		if (running) return;
		running = true;
		if (dummyPrefab == null || spawned != null) return;

		spawned = NetworkSpawner.Spawn(dummyPrefab, transform.position, Quaternion.identity, gameObject.scene);
		if (spawned == null) Debug.LogWarning($"[TrainingDummySpawner] {name}: 허수아비를 만들지 못했습니다 (NetworkSpawner 로그 확인).", this);
	}

	// 연습장은 플레이어마다 따로 열고 나가면 내린다 (place-scenes.md 「연습장」, #301). 씬이 내려갈 때 만든 허수아비도 서버에서 같이 없앤다
	void OnDestroy()
	{
		if (spawned != null && NetworkServer.active) NetworkSpawner.Despawn(spawned);
		spawned = null;
	}

	void OnDrawGizmos()
	{
		Gizmos.color = new Color(1f, 0.8f, 0.2f);
		Gizmos.DrawWireCube(transform.position, new Vector3(1f, 2f, 0f));
	}
}
