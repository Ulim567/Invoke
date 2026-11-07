using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [표시] 몬스터가 재료를 떨구면 재료 아이콘을 띄워 받은 플레이어에게 날려 보낸다 (#404). MonsterReward와 같은 오브젝트(몬스터 프리팹 최상위)에 붙인다.
/// <para>
/// - MonsterReward.RewardEffectPlayed(이도훈)를 받아서, 받은 플레이어마다 재료 개수만큼 아이콘(MaterialDropFlight)을 만든다. 재료 값은 바꾸지 않는다 (코드 작성 규칙 1).<br/>
/// - 아이콘 프리팹은 재료 ID로 찾는다: Overrides에 있으면 그것, 없으면 Resources/MaterialDrops/{재료 ID}. 둘 다 없는 재료는 띄우지 않는다.<br/>
/// - 한 재료가 많이 나와도 Max Per Material 개까지만 띄운다. 아이콘은 몬스터가 아니라 몬스터의 씬에 만들어서, 몬스터가 먼저 없어져도 끝까지 날아간다.<br/>
/// - RewardEffectPlayed는 지금 서버(Host)에서만 불린다. 그래서 호스트 화면에서만 보인다 (다른 플레이어 화면은 MonsterReward 네트워크 작업 뒤).
/// </para>
/// </summary>
[RequireComponent(typeof(MonsterReward))]
public class MaterialDropView : MonoBehaviour
{
	[Serializable]
	public struct Override
	{
		[Tooltip("재료 ID (bug_fragment, boss_core)")]
		public string materialId;
		public MaterialDropFlight prefab;
	}

	[Tooltip("재료별 아이콘 프리팹. 비워 두면 Resources/MaterialDrops/{재료 ID}")]
	[SerializeField] Override[] overrides;
	[Tooltip("한 재료를 한 플레이어에게 최대 몇 개까지 띄울지")]
	[SerializeField, Min(1)] int maxPerMaterial = 10;
	[Tooltip("튀어 오르는 가로 속도 범위 (유닛/초, 왼쪽~오른쪽)")]
	[SerializeField] Vector2 popSpeedX = new Vector2(-2.5f, 2.5f);
	[Tooltip("튀어 오르는 세로 속도 범위 (유닛/초)")]
	[SerializeField] Vector2 popSpeedY = new Vector2(6f, 8f);

	MonsterReward reward;
	Collider2D body;
	readonly Dictionary<string, MaterialDropFlight> loaded = new Dictionary<string, MaterialDropFlight>();

	/// <summary>지금까지 띄운 아이콘 수. 테스트가 읽는다.</summary>
	public int SpawnedCount { get; private set; }

	void Awake()
	{
		reward = GetComponent<MonsterReward>();
		body = GetComponentInChildren<Collider2D>();
	}

	void OnEnable() => reward.RewardEffectPlayed += OnReward;
	void OnDisable() => reward.RewardEffectPlayed -= OnReward;

	void OnReward(PlayerProgress player, int exp, int money, IReadOnlyList<MonsterReward.MaterialDrop> drops) => Play(player, drops);

	/// <summary>
	/// player에게 drops를 날려 보낸다. RewardEffectPlayed가 부르고, 테스트도 직접 부른다. drops는 이 함수 안에서만 읽는다.
	/// </summary>
	public void Play(PlayerProgress player, IReadOnlyList<MonsterReward.MaterialDrop> drops)
	{
		if (player == null || drops == null) return;
		Vector2 from = body != null ? (Vector2)body.bounds.center : (Vector2)transform.position;
		for (int i = 0; i < drops.Count; i++)
		{
			MaterialDropFlight prefab = PrefabFor(drops[i].materialId);
			if (prefab == null) continue;
			int count = Mathf.Min(drops[i].count, maxPerMaterial);
			for (int n = 0; n < count; n++) Spawn(prefab, from, player.transform);
		}
	}

	// 아이콘은 이 함수로만 만든다 (코드 작성 규칙 3). 없애는 일은 MaterialDropFlight가 스스로 한다
	void Spawn(MaterialDropFlight prefab, Vector2 from, Transform player)
	{
		MaterialDropFlight icon = Instantiate(prefab, new Vector3(from.x, from.y, transform.position.z), Quaternion.identity);
		if (gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(icon.gameObject, gameObject.scene);
		Vector2 pop = new Vector2(UnityEngine.Random.Range(popSpeedX.x, popSpeedX.y), UnityEngine.Random.Range(popSpeedY.x, popSpeedY.y));
		icon.Launch(player, pop);
		SpawnedCount++;
	}

	MaterialDropFlight PrefabFor(string materialId)
	{
		if (string.IsNullOrEmpty(materialId)) return null;
		if (overrides != null)
			foreach (Override o in overrides)
				if (o.materialId == materialId && o.prefab != null) return o.prefab;
		if (!loaded.TryGetValue(materialId, out MaterialDropFlight prefab))
		{
			prefab = Resources.Load<MaterialDropFlight>("MaterialDrops/" + materialId);
			loaded[materialId] = prefab; // 없는 재료도 null로 기억해서 매번 찾지 않는다
		}
		return prefab;
	}
}
