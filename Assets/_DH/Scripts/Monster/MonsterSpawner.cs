using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 사냥터의 정해진 구역 안 랜덤 위치에 몬스터를 만들고, 죽으면 시간이 지난 뒤 다시 만드는 컴포넌트.
/// <para>
/// - 구역(Area Size, Area Offset) 안에 몬스터를 Max Count 마리까지 둔다. 한 마리가 죽으면 respawnDelay초 뒤 구역 안 새 랜덤 위치에 한 마리를 만든다.<br/>
/// - 위치 고르기: 구역 안 랜덤한 x의 아주 높은 하늘에서 아래로 선을 그어 땅(groundLayers)의 "윗면"들을 찾고, 구역 안에 있는 윗면 하나에 발이 닿게 만든다(몬스터의 BoxCollider2D 또는 CircleCollider2D 기준).<br/>
///   타일맵 Collider는 테두리(Outlines)만 있어서, 선은 땅에 들어갈 때(윗면)와 나올 때(아랫면)를 번갈아 맞는다. 하늘에서 시작하므로 짝수 번째(0, 2, 4…)가 윗면이고, 땅 속에는 만들지 않는다.<br/>
///   고른 자리 위로 몸 높이만큼 비어 있지 않으면(천장이 낮으면) 다른 x를 다시 뽑는다.<br/>
/// - 몬스터는 NetworkSpawner.Spawn으로 이 스포너가 있는 씬에 만든다. 땅 찾기도 이 씬의 물리 세계에 한다.<br/>
/// - 서버(Host 포함)에서만 만든다. 클라이언트는 서버가 만든 몬스터를 받기만 한다. 네트워크를 켜지 않으면 만들지 않는다.
/// </para>
/// <para>
/// 사용법<br/>
/// - 빈 오브젝트에 붙이고, 몬스터가 생길 땅과 발판을 초록 상자(Area Size, Area Offset)로 덮는다. 상자 위치는 땅 속이든 공중이든 상관없다.<br/>
/// - Monster Prefab에는 NetworkIdentity가 있고 NetworkManager의 Registered Spawnable Prefabs에 등록된 몬스터 프리팹을 넣는다.
/// </para>
/// </summary>
public class MonsterSpawner : MonoBehaviour
{
	// 한 번 만들 때 위치를 고르는 최대 횟수. 모두 실패하면 이번에는 만들지 않고 나중에 다시 해 본다
	const int MaxPickTries = 10;
	// 선을 시작할 높이(구역 맨 위에서 위로). 맵보다 충분히 높은 하늘에서 시작해야 윗면·아랫면 순서가 맞는다
	const float SkyHeight = 100f;

	[SerializeField, Tooltip("만들 몬스터 프리팹. NetworkIdentity가 있고 NetworkManager에 등록돼 있어야 한다")]
	Monster monsterPrefab;

	[SerializeField, Min(0), Tooltip("구역 안에 동시에 있을 최대 마리 수")]
	int maxCount = 3;

	[SerializeField, Min(0f), Tooltip("몬스터가 죽은 뒤 다시 만들기까지 기다리는 시간(초)")]
	float respawnDelay = 5f;

	[Header("구역")]
	[SerializeField, Tooltip("몬스터를 만들 구역 크기. 몬스터가 다닐 땅과 발판을 덮게 둔다")]
	Vector2 areaSize = new Vector2(20f, 6f);

	[SerializeField, Tooltip("스포너 위치에서 구역 중심까지의 거리")]
	Vector2 areaOffset;

	[SerializeField, Tooltip("몬스터가 설 수 있는 레이어. Ground, Platform으로 둔다")]
	LayerMask groundLayers;

	// 몬스터 중심(pivot)에서 발(Collider 아래)까지의 높이. 땅 위에 만들 때 이만큼 올린다
	float footHeight;
	// 몬스터 몸 높이. 고른 자리 위가 이만큼 비어 있는지 확인한다
	float bodyHeight = 1f;
	ContactFilter2D groundFilter;
	// 선이 맞은 땅 목록. 재사용해서 메모리 할당을 없앤다
	readonly List<RaycastHit2D> groundHits = new List<RaycastHit2D>(8);
	// 가까운 순서로 정렬하는 비교 함수. 한 번만 만들어 둔다
	static readonly System.Comparison<RaycastHit2D> ByDistance = (a, b) => a.distance.CompareTo(b.distance);
	// 지금 살아 있는 마리 수
	int alive;
	// 다시 만들 시각들. 죽은 순서대로 들어가므로 앞이 가장 이르다
	readonly Queue<float> respawnTimes = new Queue<float>();
	// 서버가 켜져서 스폰을 시작했으면 true
	bool running;

	void Awake()
	{
		// 프리팹은 씬에 없어서 Collider.bounds를 못 쓰므로, BoxCollider2D·CircleCollider2D 값으로 직접 구한다
		if (monsterPrefab != null)
		{
			float scaleY = monsterPrefab.transform.localScale.y;
			if (monsterPrefab.TryGetComponent(out BoxCollider2D box))
			{
				footHeight = (box.size.y * 0.5f - box.offset.y) * scaleY;
				bodyHeight = box.size.y * scaleY;
			}
			else if (monsterPrefab.TryGetComponent(out CircleCollider2D circle))
			{
				footHeight = (circle.radius - circle.offset.y) * scaleY;
				bodyHeight = circle.radius * 2f * scaleY;
			}
		}

		groundFilter.SetLayerMask(groundLayers);
		groundFilter.useTriggers = false;
	}

	void Update()
	{
		// 서버(Host 포함)에서만 만든다. 서버가 켜지는 순간 Max Count만큼 만들고, 꺼지면 멈춘다
		if (!NetworkServer.active)
		{
			running = false;
			return;
		}

		float now = Time.time;
		if (!running)
		{
			running = true;
			// 서버가 다시 켜지면 처음처럼 바로 Max Count만큼 만든다
			alive = 0;
			respawnTimes.Clear();
			for (int i = 0; i < maxCount; i++) respawnTimes.Enqueue(now);
		}

		// 시간이 된 것만 만든다. 큐가 시각 순서라 앞이 아직이면 뒤도 아직이다
		while (respawnTimes.Count > 0 && respawnTimes.Peek() <= now)
		{
			respawnTimes.Dequeue();
			if (!TrySpawn())
			{
				// 위치를 못 찾았거나 설정이 잘못됐다. 매 프레임 반복하지 않도록 뒤로 미루고 이번 프레임은 멈춘다
				respawnTimes.Enqueue(now + Mathf.Max(respawnDelay, 1f));
				break;
			}
		}
	}

	// 구역 안 랜덤 위치에 몬스터를 하나 만든다. 생성은 NetworkSpawner를 거친다 (코드 작성 규칙 3)
	bool TrySpawn()
	{
		if (monsterPrefab == null || alive >= maxCount || !TryPickPosition(out Vector2 position)) return false;

		GameObject obj = NetworkSpawner.Spawn(monsterPrefab.gameObject, position, Quaternion.identity, gameObject.scene);
		if (obj == null) return false;

		alive++;
		obj.GetComponent<Monster>().Died += OnMonsterDied;
		return true;
	}

	void OnMonsterDied()
	{
		alive--;
		respawnTimes.Enqueue(Time.time + respawnDelay);
	}

	// 구역 안 랜덤한 x의 하늘에서 아래로 땅의 윗면을 찾아, 구역 안 윗면 위 위치를 고른다
	bool TryPickPosition(out Vector2 position)
	{
		// 이 스포너가 속한 장소 씬의 물리 세계. 저장하지 않고 쓸 때마다 가져온다
		PhysicsScene2D physics = gameObject.scene.GetPhysicsScene2D();
		Vector2 center = (Vector2)transform.position + areaOffset;
		Vector2 half = areaSize * 0.5f;
		float top = center.y + half.y;
		float bottom = center.y - half.y;

		for (int i = 0; i < MaxPickTries; i++)
		{
			// 하늘(땅 밖)에서 시작해야 맞은 순서로 윗면·아랫면을 구분할 수 있다
			Vector2 start = new Vector2(center.x + Random.Range(-half.x, half.x), top + SkyHeight);
			int count = physics.Raycast(start, Vector2.down, SkyHeight + areaSize.y, groundFilter, groundHits);
			if (count == 0) continue;
			groundHits.Sort(ByDistance);

			// 짝수 번째(0, 2, 4…)가 땅에 들어가는 윗면이다. 그중 구역 안에 있는 것만 후보로 센다
			int candidates = 0;
			for (int h = 0; h < count; h += 2)
				if (groundHits[h].point.y <= top && groundHits[h].point.y >= bottom) candidates++;
			if (candidates == 0) continue;

			// 후보 중 하나를 랜덤으로 고른다
			int pick = Random.Range(0, candidates);
			RaycastHit2D surface = default;
			for (int h = 0; h < count; h += 2)
			{
				if (groundHits[h].point.y > top || groundHits[h].point.y < bottom) continue;
				if (pick-- == 0) { surface = groundHits[h]; break; }
			}

			// 고른 자리 위로 몸 높이만큼 비어 있어야 한다 (낮은 천장·발판 밑에 끼지 않게)
			Vector2 feet = surface.point + new Vector2(0f, 0.02f);
			if (physics.Raycast(feet, Vector2.up, bodyHeight, groundLayers)) continue;

			// 몸이 땅에 박히지 않도록 발 높이만큼 올리고, 땅과 살짝 띄운다
			position = surface.point + new Vector2(0f, footHeight + 0.01f);
			return true;
		}

		position = default;
		return false;
	}

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube((Vector2)transform.position + areaOffset, areaSize);
	}
}
