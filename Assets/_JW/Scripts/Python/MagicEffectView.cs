using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [표시] 파이썬 마법사 스킬의 이펙트를 튼다. 파이썬 플레이어 프리팹에 SkillEffectEvents와 함께 붙인다.
/// <para>
/// - SkillEffectEvents.Fired / Hit / Ended만 받아서 그린다. 값은 바꾸지 않는다 (코드 작성 규칙 1). 모든 화면에서 같은 event가 불리므로 각 화면이 직접 그린다.<br/>
/// - 이펙트 프리팹은 Resources/Magic/{이름}_{종류}. 이름은 스킬 ID에서 "py."를 뺀 것 (예: py.fireball → fireball). 없는 프리팹은 그리지 않고 넘어간다.<br/>
///   · _bolt: 마법탄. MagicBolts 표에 있는 스킬이 Fired를 내면 그 방향으로 표의 속도·사거리만큼 날린다 (서버 판정과 같은 길).<br/>
///   · _hit: 맞은 자리에 한 번. 관통이 아닌 마법탄은 이때 사라진다.<br/>
///   · _beam: 체인 라이트닝의 번개 줄기. Fired의 시작 자리에서 방향 벡터 끝까지 늘려 그린다.<br/>
///   · _zone: 범위 마법(블리자드, 독 안개). Fired 자리에 깔고 그 스킬이 끝날 때(Ended) 없앤다.<br/>
///   · _cast: 그 밖의 Fired 자리에 한 번 (메테오, 구슬 생성, 구슬 폭발, 섬광, 블링크). 블링크는 도착 자리에도 한 번 더.<br/>
/// - 구슬 난사는 구슬 발사의 그림을 같이 쓴다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 한 번 트는 프리팹은 OneShotEffect가 스스로 없앤다 (프리팹은 메뉴 Invoke → JW → 파이썬 마법사 만들기).
/// </para>
/// </summary>
[RequireComponent(typeof(SkillEffectEvents))]
public class MagicEffectView : MonoBehaviour
{
	const string Folder = "Magic/";
	const string Prefix = "py.";

	SkillEffectEvents events;
	// 스킬 ID → 지금 날고 있는 마법탄 그림 / 깔려 있는 범위 그림
	readonly Dictionary<string, List<MagicBoltVisual>> bolts = new Dictionary<string, List<MagicBoltVisual>>();
	readonly Dictionary<string, List<GameObject>> zones = new Dictionary<string, List<GameObject>>();
	// 한 번 찾은 프리팹(없음 포함)은 모든 캐릭터가 같이 쓴다
	static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

	/// <summary>지금까지 만든 이펙트 수. 테스트가 읽는다.</summary>
	public int SpawnedCount { get; private set; }

	void Awake() => events = GetComponent<SkillEffectEvents>();

	void OnEnable()
	{
		events.Fired += OnFired;
		events.Hit += OnHit;
		events.Ended += OnEnded;
	}

	void OnDisable()
	{
		events.Fired -= OnFired;
		events.Hit -= OnHit;
		events.Ended -= OnEnded;
		foreach (List<GameObject> list in zones.Values)
			foreach (GameObject zone in list) if (zone != null) Destroy(zone);
		zones.Clear();
	}

	// 그림 이름: 스킬 ID에서 "py."를 뺀다. 구슬 난사는 구슬 발사의 그림
	static string ArtName(string skillId) =>
		skillId == PredefinedFunctionIds.PyOrbBarrage ? "orb_shot" : skillId.Substring(Prefix.Length);

	static GameObject Prefab(string name)
	{
		if (!prefabs.TryGetValue(name, out GameObject prefab))
		{
			prefab = Resources.Load<GameObject>(Folder + name);
			prefabs[name] = prefab;
		}
		return prefab;
	}

	void OnFired(string skillId, Vector2 origin, Vector2 direction)
	{
		if (skillId == null || !skillId.StartsWith(Prefix)) return;
		string art = ArtName(skillId);

		// 마법탄
		if (MagicBolts.TryGet(skillId, out MagicBoltSpec spec))
		{
			GameObject obj = Spawn(Prefab(art + "_bolt"), origin, Angle(direction));
			if (obj == null) return;
			MagicBoltVisual bolt = obj.GetComponent<MagicBoltVisual>();
			if (bolt == null)
			{
				Destroy(obj);
				return;
			}
			bolt.Launch(direction, spec.Speed, spec.Range);
			ListOf(bolts, skillId).Add(bolt);
			return;
		}

		// 번개 줄기: 시작 자리에서 방향 벡터 끝까지 늘린다. 그림은 왼쪽 끝이 기준점이고 길이 1유닛을 기준으로 만든다
		if (skillId == PredefinedFunctionIds.PyChainLightning)
		{
			GameObject beam = Spawn(Prefab(art + "_beam"), origin, Angle(direction));
			if (beam != null)
			{
				Vector3 scale = beam.transform.localScale;
				beam.transform.localScale = new Vector3(scale.x * direction.magnitude, scale.y, scale.z);
			}
			return;
		}

		// 범위 마법: 스킬이 끝날 때까지 깔아 둔다
		GameObject zonePrefab = Prefab(art + "_zone");
		if (zonePrefab != null && skillId != PredefinedFunctionIds.PyOrbShield)
		{
			GameObject zone = Spawn(zonePrefab, origin, 0f);
			if (zone != null) ListOf(zones, skillId).Add(zone);
			return;
		}

		// 그 밖: 그 자리에 한 번
		Spawn(Prefab(art + "_cast"), origin, 0f);
		// 블링크: 방향에 이동 벡터가 들어 있다. 도착 자리에도 한 번 더
		if (skillId == PredefinedFunctionIds.PyBlink) Spawn(Prefab(art + "_cast"), origin + direction, 0f);
	}

	void OnHit(string skillId, GameObject target, Vector2 point, DamageResult result)
	{
		if (skillId == null || !skillId.StartsWith(Prefix)) return;
		Spawn(Prefab(ArtName(skillId) + "_hit"), point, 0f);

		// 관통이 아닌 마법탄은 맞은 자리에서 사라진다: 맞은 자리에 가장 가까운 그림 하나를 없앤다
		if (!MagicBolts.TryGet(skillId, out MagicBoltSpec spec) || spec.Pierce) return;
		if (!bolts.TryGetValue(skillId, out List<MagicBoltVisual> flying)) return;
		flying.RemoveAll(b => b == null);
		MagicBoltVisual nearest = null;
		float best = float.MaxValue;
		foreach (MagicBoltVisual bolt in flying)
		{
			float dist = ((Vector2)bolt.transform.position - point).sqrMagnitude;
			if (dist < best) { best = dist; nearest = bolt; }
		}
		// 폭발에 휩쓸린 다른 몬스터의 Hit으로 엉뚱한 마법탄을 지우지 않게, 가까이 있을 때만 없앤다
		if (nearest == null || best > 4f) return;
		flying.Remove(nearest);
		Destroy(nearest.gameObject);
	}

	// 스킬이 끝나면(정상·끊김 모두) 깔아 둔 범위 그림을 없앤다. 마법탄 그림은 서버 판정도 같이 끝나므로 같이 없앤다
	void OnEnded(string skillId, bool completed)
	{
		if (skillId == null) return;
		if (zones.TryGetValue(skillId, out List<GameObject> placed))
		{
			foreach (GameObject zone in placed) if (zone != null) Destroy(zone);
			placed.Clear();
		}
		if (bolts.TryGetValue(skillId, out List<MagicBoltVisual> flying))
		{
			foreach (MagicBoltVisual bolt in flying) if (bolt != null) Destroy(bolt.gameObject);
			flying.Clear();
		}
	}

	static List<T> ListOf<T>(Dictionary<string, List<T>> map, string key)
	{
		if (!map.TryGetValue(key, out List<T> list))
		{
			list = new List<T>();
			map[key] = list;
		}
		return list;
	}

	static float Angle(Vector2 direction) =>
		direction.sqrMagnitude > 0.0001f ? Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg : 0f;

	// 이펙트는 이 함수로만 만든다 (코드 작성 규칙 3). 플레이어가 있는 장소 씬에 둔다 (Instantiate는 활성 씬에 만들기 때문)
	GameObject Spawn(GameObject prefab, Vector2 at, float angle)
	{
		if (prefab == null) return null;
		GameObject obj = Instantiate(prefab, at, Quaternion.Euler(0f, 0f, angle), null);
		if (obj.scene != gameObject.scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, gameObject.scene);
		SpawnedCount++;
		return obj;
	}
}
