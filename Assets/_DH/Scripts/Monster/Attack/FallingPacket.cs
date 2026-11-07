using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 공격 형태 "낙하". 떨어질 자리(땅)에 그림자를 먼저 보여 주고, Warn Time초 뒤 패킷이 떨어져
/// 착지하는 순간 그림자 위 판정 상자 안의 플레이어에게 공격 효과를 준다. 보스 Gateway의 패킷 낙하가 쓴다.
/// <para>
/// 사용법 (프리팹 설정)<br/>
/// - 최상위 오브젝트 위치 = 땅 위 착지 지점. 만든 쪽(GatewayAI)이 땅을 찾아 그 위치에 만든다.<br/>
/// - 자식 Shadow(그림자 그림)와 Packet(떨어지는 패킷 그림)을 Inspector에 넣는다. 둘 다 보여 주기만 한다.<br/>
/// - Attack에 MonsterAttackData, Target Layers는 Player로 둔다. Collider는 필요 없다(착지 순간 상자로 검사).<br/>
/// - 만들고 없애는 일은 NetworkSpawner를 거친다. 그래서 NetworkIdentity를 붙이고 NetworkManager에 등록한다.
/// </para>
/// <para>
/// 착지 판정은 그림자가 덮는 자리(Hit Size)에서 한 번만 한다. 그림자 밖으로 피하면 맞지 않는다.
/// 판정은 이 오브젝트가 속한 씬의 물리 세계에 한다. 판정과 삭제는 서버(또는 네트워크 없는 테스트)에서만 하고, 그림자·낙하·착지 연출은 각 화면이 생성된 순간부터 스스로 한다 (#310).
/// </para>
/// </summary>
public class FallingPacket : MonoBehaviour
{
	[SerializeField, Tooltip("착지할 때 적용할 공격 데이터")]
	MonsterAttackData attack;

	[SerializeField, Tooltip("맞힐 대상 레이어. Player로 둔다")]
	LayerMask targetLayers;

	[SerializeField, Min(0f), Tooltip("그림자를 보여 주고 패킷이 떨어지기 시작하기까지 시간(초). 피할 시간")]
	float warnTime = 1f;

	[SerializeField, Min(0.01f), Tooltip("패킷이 떨어지는 데 걸리는 시간(초)")]
	float fallTime = 0.3f;

	[SerializeField, Min(0f), Tooltip("패킷이 떨어지기 시작하는 높이 (착지 지점 위로 유닛)")]
	float dropHeight = 8f;

	[SerializeField, Tooltip("착지 판정 상자 크기. 아래 변이 땅에 붙는다. 가로는 그림자 폭에 맞춘다")]
	Vector2 hitSize = new Vector2(2f, 3f);

	[SerializeField, Min(0f), Tooltip("착지 뒤 오브젝트를 없애기까지 기다리는 시간(초). 착지 연출이 보일 시간")]
	float despawnDelay = 0.3f;

	[Header("그림 (연출)")]
	[SerializeField, Tooltip("바닥 그림자. 경고 동안 보이고 착지하면 숨긴다")]
	GameObject shadow;

	[SerializeField, Tooltip("떨어지는 패킷 그림. 떨어지는 동안만 보인다")]
	Transform packet;

	ContactFilter2D filter;
	// 착지 검사 결과. 재사용해서 메모리 할당을 없앤다
	readonly List<Collider2D> hits = new List<Collider2D>(4);
	// 같은 대상의 Collider가 여러 개여도 한 번만 맞도록 막는다
	readonly HashSet<GameObject> struck = new HashSet<GameObject>();

	float fallStartTime;
	float landTime;
	float despawnTime;
	bool landed;

	/// <summary>
	/// [연출] 그림자가 나타날 때(만들어진 직후) 불린다. 경고 소리 등에 쓴다.
	/// </summary>
	public event Action WarnEffectPlayed;

	/// <summary>
	/// [연출] 패킷이 땅에 닿을 때 불린다. 착지 이펙트·소리·화면 흔들림에 쓴다.
	/// </summary>
	public event Action LandEffectPlayed;

	void Awake()
	{
		filter.SetLayerMask(targetLayers);
		filter.useTriggers = false;
	}

	void Start()
	{
		fallStartTime = Time.time + warnTime;
		landTime = fallStartTime + fallTime;
		PlayWarnEffect();
	}

	void Update()
	{
		float now = Time.time;

		if (!landed)
		{
			ShowFall(now);
			if (now >= landTime)
			{
				landed = true;
				// 판정은 서버(또는 네트워크 없는 테스트)에서만 한다. 클라이언트는 연출만 한다
				if (IsAuthority()) Land();
				PlayLandEffect();
				despawnTime = now + despawnDelay;
			}
			return;
		}

		if (now >= despawnTime && IsAuthority()) Despawn();
	}

	// 서버이거나 네트워크를 켜지 않은 테스트면 true. 클라이언트에서는 판정·삭제를 하지 않는다
	static bool IsAuthority() => NetworkServer.active || !NetworkClient.active;

	// 착지 순간 그림자 위 상자 안의 대상에게 한 번씩 효과를 준다. 서버(또는 네트워크 없는 테스트)에서만 부른다
	void Land()
	{
		Vector2 center = (Vector2)transform.position + new Vector2(0f, hitSize.y * 0.5f);
		// 이 오브젝트가 속한 장소 씬의 물리 세계에 묻는다
		int count = gameObject.scene.GetPhysicsScene2D().OverlapBox(center, hitSize, 0f, filter, hits);
		struck.Clear();
		for (int i = 0; i < count; i++)
		{
			Collider2D hit = hits[i];
			GameObject owner = hit.attachedRigidbody != null ? hit.attachedRigidbody.gameObject : hit.gameObject;
			if (struck.Add(owner)) MonsterAttackResolver.Apply(attack, hit);
		}
	}

	// 아래 함수들은 연출만 한다 (코드 작성 규칙 1). 각 화면이 생성된 순간(Start)부터 자기 시계로 계산하므로 따로 보내지 않는다.
	// 클라이언트는 생성 알림이 도착한 순간부터 세므로 서버보다 핑만큼 늦게 착지한다 (#310)
	void PlayWarnEffect()
	{
		if (shadow != null) shadow.SetActive(true);
		if (packet != null) packet.gameObject.SetActive(false);
		WarnEffectPlayed?.Invoke();
	}

	// 경고가 끝나면 패킷을 dropHeight에서 착지 지점까지 내린다
	void ShowFall(float now)
	{
		if (packet == null || now < fallStartTime) return;
		if (!packet.gameObject.activeSelf) packet.gameObject.SetActive(true);
		float t = Mathf.Clamp01((now - fallStartTime) / fallTime);
		// 점점 빨라지게 떨어진다
		packet.localPosition = new Vector3(0f, dropHeight * (1f - t * t), 0f);
	}

	void PlayLandEffect()
	{
		if (shadow != null) shadow.SetActive(false);
		if (packet != null) packet.localPosition = Vector3.zero;
		LandEffectPlayed?.Invoke();
	}

	// 없애는 곳은 여기 하나다 (코드 작성 규칙 3)
	void Despawn() => NetworkSpawner.Despawn(gameObject);

	void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.red;
		Gizmos.DrawWireCube(transform.position + new Vector3(0f, hitSize.y * 0.5f, 0f), hitSize);
	}
}
