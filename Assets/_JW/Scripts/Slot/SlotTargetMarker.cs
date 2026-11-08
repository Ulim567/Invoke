using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [표시] 슬롯으로 고르고 있는 대상과 찍은 대상에 조준점을 띄운다 (#438). SlotCaster와 같은 오브젝트(플레이어)에 붙인다.
/// <para>
/// - SlotCaster.TargetChanged, MarkedChanged만 받아서 그린다 (코드 작성 규칙 1: 고르는 로직과 표시를 나눔).<br/>
/// - 고른 대상은 빨간 조준점(Resources/TargetReticle), 찍은 대상은 하늘색 조준점(Resources/TargetReticleMarked)을 대상 몸(콜라이더) 가운데에 겹쳐 띄운다.<br/>
/// - 고른 대상이 찍힌 대상이기도 하면 하늘색을 Marked Under Scale배 크게 뒤에, 빨간 것을 위에 겹친다.<br/>
/// - 조준점은 대상의 자식이 아니다. 매 프레임 대상을 따라가고, 대상이 없어지면 숨긴다. 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다.
/// </para>
/// </summary>
[RequireComponent(typeof(SlotCaster))]
public class SlotTargetMarker : MonoBehaviour
{
	[Tooltip("고른 대상 조준점 프리팹. 비워 두면 Resources/TargetReticle")]
	[SerializeField] SpriteRenderer reticlePrefab;
	[Tooltip("찍은 대상 조준점 프리팹. 비워 두면 Resources/TargetReticleMarked")]
	[SerializeField] SpriteRenderer markedPrefab;
	[Tooltip("조준점 크기 배율 (1 = 그림 그대로 48px = 3칸, 0.5 = 1.5칸)")]
	[SerializeField, Min(0.1f)] float scale = 0.5f;
	[Tooltip("고른 대상이 찍힌 대상이기도 할 때 뒤에 까는 하늘색 조준점 배율")]
	[SerializeField, Min(1f)] float markedUnderScale = 1.25f;

	SlotCaster caster;
	GameObject target;
	readonly List<GameObject> marked = new List<GameObject>();

	SpriteRenderer targetReticle;
	readonly List<SpriteRenderer> markedReticles = new List<SpriteRenderer>();

	/// <summary>조준점 크기 배율 (테스트용).</summary>
	public float Scale => scale;
	/// <summary>고른 대상 조준점이 보이는지 (테스트용).</summary>
	public bool IsTargetShown => targetReticle != null && targetReticle.gameObject.activeSelf;
	/// <summary>고른 대상 조준점 위치 (테스트용).</summary>
	public Vector3 TargetReticlePosition => targetReticle != null ? targetReticle.transform.position : Vector3.zero;
	/// <summary>보이는 찍은 대상 조준점 수 (테스트용).</summary>
	public int ShownMarkedCount
	{
		get
		{
			int n = 0;
			foreach (SpriteRenderer r in markedReticles) if (r != null && r.gameObject.activeSelf) n++;
			return n;
		}
	}
	/// <summary>보이는 찍은 대상 조준점 중 가장 큰 배율 (테스트용).</summary>
	public float LargestMarkedScale
	{
		get
		{
			float s = 0f;
			foreach (SpriteRenderer r in markedReticles) if (r != null && r.gameObject.activeSelf) s = Mathf.Max(s, r.transform.localScale.x);
			return s;
		}
	}

	void Awake()
	{
		caster = GetComponent<SlotCaster>();
		if (reticlePrefab == null) reticlePrefab = Resources.Load<SpriteRenderer>("TargetReticle");
		if (markedPrefab == null) markedPrefab = Resources.Load<SpriteRenderer>("TargetReticleMarked");
	}

	void OnEnable()
	{
		caster.TargetChanged += SetTarget;
		caster.MarkedChanged += SetMarked;
	}

	void OnDisable()
	{
		caster.TargetChanged -= SetTarget;
		caster.MarkedChanged -= SetMarked;
		HideAll();
	}

	void OnDestroy()
	{
		if (targetReticle != null) Destroy(targetReticle.gameObject);
		foreach (SpriteRenderer r in markedReticles) if (r != null) Destroy(r.gameObject);
	}

	/// <summary>고른 대상을 바꾼다. TargetChanged가 부르고, 테스트도 직접 부른다. null이면 숨긴다.</summary>
	public void SetTarget(GameObject newTarget) => target = newTarget;

	/// <summary>찍은 대상을 바꾼다. MarkedChanged가 부르고, 테스트도 직접 부른다.</summary>
	public void SetMarked(IReadOnlyList<GameObject> targets)
	{
		marked.Clear();
		if (targets != null) marked.AddRange(targets);
	}

	void LateUpdate()
	{
		// 찍은 대상
		int used = 0;
		foreach (GameObject m in marked)
		{
			if (m == null || !m.activeInHierarchy) continue;
			if (used >= markedReticles.Count)
			{
				SpriteRenderer made = Spawn(markedPrefab, 0);
				if (made == null) break;
				markedReticles.Add(made);
			}
			SpriteRenderer r = markedReticles[used++];
			float s = m == target ? scale * markedUnderScale : scale;
			Place(r, m, s);
		}
		for (int i = used; i < markedReticles.Count; i++) if (markedReticles[i] != null) markedReticles[i].gameObject.SetActive(false);

		// 고른 대상 (찍은 대상 위에 그린다)
		if (target != null && target.activeInHierarchy)
		{
			if (targetReticle == null) targetReticle = Spawn(reticlePrefab, 1);
			if (targetReticle != null) Place(targetReticle, target, scale);
		}
		else if (targetReticle != null) targetReticle.gameObject.SetActive(false);
	}

	static void Place(SpriteRenderer reticle, GameObject obj, float s)
	{
		Collider2D col = obj.GetComponentInChildren<Collider2D>();
		Vector3 center = col != null ? (Vector3)(Vector2)col.bounds.center : obj.transform.position;
		reticle.transform.position = new Vector3(center.x, center.y, reticle.transform.position.z);
		reticle.transform.localScale = new Vector3(s, s, 1f);
		if (!reticle.gameObject.activeSelf) reticle.gameObject.SetActive(true);
	}

	// 조준점은 이 함수로만 만들고 OnDestroy에서만 없앤다 (코드 작성 규칙 3). 플레이어와 같은 씬에 만든다
	SpriteRenderer Spawn(SpriteRenderer prefab, int order)
	{
		if (prefab == null)
		{
			Debug.LogError($"[SlotTargetMarker] {name}: 조준점 프리팹(Resources/TargetReticle, TargetReticleMarked)을 찾지 못했습니다. 메뉴 Invoke → JW → 조준점 프리팹 만들기를 눌러 주세요.");
			enabled = false;
			return null;
		}
		SpriteRenderer r = Instantiate(prefab);
		if (gameObject.scene.IsValid()) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(r.gameObject, gameObject.scene);
		r.sortingOrder = order;
		r.gameObject.SetActive(false);
		return r;
	}

	void HideAll()
	{
		if (targetReticle != null) targetReticle.gameObject.SetActive(false);
		foreach (SpriteRenderer r in markedReticles) if (r != null) r.gameObject.SetActive(false);
	}
}
