using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [표시] 마나 구슬을 머리 위에 그린다. ManaOrbs와 같은 오브젝트(파이썬 플레이어 프리팹)에 붙인다.
/// <para>
/// - ManaOrbs.Changed를 받아 구슬 그림을 TotalCount(다 쓴 구슬 포함)만큼 맞춘다. 다 쓴 구슬(IsSpentAt)은 빨갛게 칠한다 (#532). 값은 읽기만 한다 (코드 작성 규칙 1).<br/>
/// - 자리는 ManaOrbs.OrbPosition을 매 프레임 따라가고, 살짝 둥둥 뜬다.<br/>
/// - 구슬 그림 프리팹은 비워 두면 Resources/Magic/mana_orb를 쓴다. 없으면 아무것도 그리지 않는다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 직접 만들고 없앤다.
/// </para>
/// </summary>
[RequireComponent(typeof(ManaOrbs))]
public class ManaOrbView : MonoBehaviour
{
	[Tooltip("구슬 그림 프리팹. 비워 두면 Resources/Magic/mana_orb")]
	[SerializeField] GameObject orbPrefab;
	[Tooltip("둥둥 떠 있는 높이 (유닛)")]
	[SerializeField, Min(0f)] float bobHeight = 0.06f;
	[Tooltip("둥둥 떠 있는 빠르기 (라디안/초)")]
	[SerializeField, Min(0f)] float bobSpeed = 3f;
	[Tooltip("다 쓴 구슬의 색 (#532). HUD의 누수 소환검 칸과 같은 빨강")]
	[SerializeField] Color spentColor = new Color32(0xE0, 0x70, 0x70, 0xFF);

	ManaOrbs orbs;
	readonly List<GameObject> shown = new List<GameObject>();
	// 프리팹 그림의 원래 색. 다 쓴 구슬이 아닌 구슬은 이 색으로 되돌린다
	Color usableColor = Color.white;

	/// <summary>지금 그리고 있는 구슬 수. 테스트가 읽는다.</summary>
	public int ShownCount => shown.Count;

	void Awake()
	{
		orbs = GetComponent<ManaOrbs>();
		if (orbPrefab == null) orbPrefab = Resources.Load<GameObject>("Magic/mana_orb");
		SpriteRenderer prefabRenderer = orbPrefab != null ? orbPrefab.GetComponentInChildren<SpriteRenderer>() : null;
		if (prefabRenderer != null) usableColor = prefabRenderer.color;
	}

	void OnEnable()
	{
		orbs.Changed += OnChanged;
		OnChanged();
	}

	void OnDisable()
	{
		orbs.Changed -= OnChanged;
		Show(0);
	}

	void OnChanged() => Show(orbs.TotalCount);

	void Show(int count)
	{
		if (orbPrefab == null) return;
		while (shown.Count < count) shown.Add(SpawnOrb());
		while (shown.Count > count)
		{
			DespawnOrb(shown[shown.Count - 1]);
			shown.RemoveAt(shown.Count - 1);
		}
		for (int i = 0; i < shown.Count; i++) Paint(shown[i], orbs.IsSpentAt(i));
	}

	// 다 쓴 구슬은 빨갛게, 아니면 원래 색. 반투명하게 하면 작은 구슬이 더 안 보여서 색만 바꾼다
	void Paint(GameObject orb, bool spent)
	{
		if (orb == null) return;
		SpriteRenderer renderer = orb.GetComponentInChildren<SpriteRenderer>();
		if (renderer != null) renderer.color = spent ? spentColor : usableColor;
	}

	void LateUpdate()
	{
		for (int i = 0; i < shown.Count; i++)
		{
			if (shown[i] == null) continue;
			Vector2 at = orbs.OrbPosition(i, shown.Count);
			at.y += Mathf.Sin(Time.time * bobSpeed + i * 1.3f) * bobHeight;
			shown[i].transform.position = new Vector3(at.x, at.y, transform.position.z);
		}
	}

	// 구슬 그림은 이 두 함수로만 만들고 없앤다 (코드 작성 규칙 3). 캐릭터의 자식이 아니라 같은 씬에 두어 캐릭터가 뒤집혀도 그림이 뒤집히지 않는다
	GameObject SpawnOrb()
	{
		GameObject orb = Instantiate(orbPrefab, transform.position, Quaternion.identity, null);
		if (orb.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(orb, gameObject.scene);
		return orb;
	}

	void DespawnOrb(GameObject orb)
	{
		if (orb != null) Destroy(orb);
	}

	// 캐릭터가 사라지면 구슬 그림도 남기지 않는다 (자식이 아니라서 직접 없앤다)
	void OnDestroy()
	{
		foreach (GameObject orb in shown) DespawnOrb(orb);
		shown.Clear();
	}
}
