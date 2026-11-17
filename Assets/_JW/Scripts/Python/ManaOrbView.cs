using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [표시] 마나 구슬을 머리 위에 그린다. ManaOrbs와 같은 오브젝트(파이썬 플레이어 프리팹)에 붙인다.
/// <para>
/// - ManaOrbs.Changed(개수)를 받아 구슬 그림을 그 수만큼 맞춘다. 값은 읽기만 한다 (코드 작성 규칙 1).<br/>
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

	ManaOrbs orbs;
	readonly List<GameObject> shown = new List<GameObject>();

	/// <summary>지금 그리고 있는 구슬 수. 테스트가 읽는다.</summary>
	public int ShownCount => shown.Count;

	void Awake()
	{
		orbs = GetComponent<ManaOrbs>();
		if (orbPrefab == null) orbPrefab = Resources.Load<GameObject>("Magic/mana_orb");
	}

	void OnEnable()
	{
		orbs.Changed += OnChanged;
		OnChanged(orbs.Count);
	}

	void OnDisable()
	{
		orbs.Changed -= OnChanged;
		OnChanged(0);
	}

	void OnChanged(int count)
	{
		if (orbPrefab == null) return;
		while (shown.Count < count) shown.Add(SpawnOrb());
		while (shown.Count > count)
		{
			DespawnOrb(shown[shown.Count - 1]);
			shown.RemoveAt(shown.Count - 1);
		}
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
