using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 레이드 던전에 보스를 한 마리 만드는 컴포넌트. 보스가 죽어도 다시 만들지 않는다.
/// <para>
/// - 레이드 던전 씬이 불러와지면 서버에서 한 번, 이 오브젝트 아래 땅 위에 보스를 만든다. 파티마다 던전 씬을 따로 불러오므로 던전마다 보스가 한 마리씩 생긴다.<br/>
/// - 위치: 이 오브젝트에서 아래로 땅(Ground Layers)을 찾아, 보스 판정(BoxCollider2D) 바닥이 땅에서 Hover Height만큼 뜨게 둔다. 땅을 못 찾으면 이 오브젝트 위치에 만든다.<br/>
/// - 보스는 NetworkSpawner.Spawn으로 이 씬에 만든다. 땅 찾기도 이 씬의 물리 세계에 한다.<br/>
/// - 서버(Host 포함)이거나 네트워크를 켜지 않은 테스트에서만 만든다. 클라이언트는 서버가 만든 보스를 받기만 한다.
/// </para>
/// <para>
/// 사용법: 레이드 던전 씬의 빈 오브젝트에 붙이고, 보스를 세울 땅 위(가로 위치)에 둔다. Scene 창에서 노란 상자가 보스가 설 자리다.<br/>
/// Boss Prefab에는 NetworkIdentity가 있고 NetworkManager에 등록된 보스 프리팹(Gateway)을 넣는다.<br/>
/// 레이드 판정 등 다른 스크립트는 <see cref="Boss"/>를 읽거나 <see cref="BossSpawned"/>를 받는다.
/// </para>
/// </summary>
public class RaidBossSpawner : MonoBehaviour
{
	// 땅을 찾는 선의 길이 (이 오브젝트에서 아래로)
	const float GroundSearchDistance = 40f;

	[SerializeField, Tooltip("만들 보스 프리팹. NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	Monster bossPrefab;

	[SerializeField, Tooltip("보스를 세울 땅 레이어. Ground로 둔다")]
	LayerMask groundLayers;

	[SerializeField, Min(0f), Tooltip("보스 판정 바닥을 땅에서 띄울 높이 (유닛). 0이면 땅에 닿게 선다")]
	float hoverHeight;

	// 보스 중심(pivot)에서 판정 바닥까지의 높이
	float footHeight;
	bool spawned;

	/// <summary>
	/// 이 던전에 만든 보스. 아직 만들지 않았으면 null. 서버에서만 값이 있다.
	/// </summary>
	public Monster Boss { get; private set; }

	/// <summary>
	/// [로직] 보스를 만든 순간 불린다. 만든 보스를 넘긴다. 서버에서만 불린다.
	/// </summary>
	public event Action<Monster> BossSpawned;

	void Awake()
	{
		footHeight = FootHeight();
		// 설정이 비어 있으면 보스가 조용히 안 생기므로 원인을 바로 알린다
		if (bossPrefab == null) Debug.LogWarning($"[RaidBossSpawner] {name}: Boss Prefab이 비어 있어 보스를 만들지 않습니다.", this);
		if (groundLayers.value == 0) Debug.LogWarning($"[RaidBossSpawner] {name}: Ground Layers가 비어 있어 땅을 찾지 못하고 이 위치에 만듭니다.", this);
	}

	void Update()
	{
		// 씬이 불러와진 뒤 첫 프레임에 한 번만 만든다
		if (spawned || !IsAuthority()) return;
		spawned = true;
		Spawn();
	}

	// 생성은 NetworkSpawner를 거친다 (코드 작성 규칙 3)
	void Spawn()
	{
		if (bossPrefab == null) return;

		GameObject obj = NetworkSpawner.Spawn(bossPrefab.gameObject, SpawnPosition(), Quaternion.identity, gameObject.scene);
		if (obj == null) return;

		Boss = obj.GetComponent<Monster>();
		BossSpawned?.Invoke(Boss);
	}

	// 아래 땅 위에 판정 바닥이 hoverHeight만큼 뜨는 위치. 땅이 없으면 이 위치
	Vector2 SpawnPosition()
	{
		if (TryGroundPosition(footHeight, out Vector2 position)) return position;
		if (groundLayers.value != 0)
			Debug.LogWarning($"[RaidBossSpawner] {name}: 아래 {GroundSearchDistance}칸 안에 땅이 없어 이 위치에 보스를 만듭니다.", this);
		return transform.position;
	}

	// 이 오브젝트에서 아래로 땅을 찾아, 판정 바닥이 땅에서 hoverHeight만큼 뜨는 위치를 구한다
	bool TryGroundPosition(float foot, out Vector2 position)
	{
		position = transform.position;
		if (groundLayers.value == 0) return false;

		RaycastHit2D hit = gameObject.scene.GetPhysicsScene2D().Raycast(position, Vector2.down, GroundSearchDistance, groundLayers);
		if (!hit) return false;

		position.y = hit.point.y + foot + hoverHeight;
		return true;
	}

	// 프리팹은 씬에 없어서 Collider.bounds를 못 쓰므로 BoxCollider2D 값으로 구한다
	float FootHeight()
	{
		if (bossPrefab == null || !bossPrefab.TryGetComponent(out BoxCollider2D box)) return 0f;
		return (box.size.y * 0.5f - box.offset.y) * bossPrefab.transform.localScale.y;
	}

	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;

	// Scene 창에서 보스가 설 자리(판정 상자)를 노란 상자로 보여 준다. 실제로 만들 때와 같이 땅을 찾아 그린다
	void OnDrawGizmos()
	{
		if (bossPrefab == null || !bossPrefab.TryGetComponent(out BoxCollider2D box)) return;
		Vector2 scale = bossPrefab.transform.localScale;
		TryGroundPosition(FootHeight(), out Vector2 position);

		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube(position + Vector2.Scale(box.offset, scale), Vector2.Scale(box.size, scale));
	}
}
