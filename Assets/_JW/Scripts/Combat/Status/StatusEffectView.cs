using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// [표시] 상태 이상이 걸린 대상의 모습(몸 효과)을 보여 준다 (#353). StatusEffects와 같은 오브젝트(몬스터 프리팹 최상위)에 붙인다.
/// <para>
/// - StatusEffects.StatusApplied / StatusRemoved를 받아 그린다. 상태 값은 읽기만 한다 (코드 작성 규칙 1). 이 두 이벤트는 서버·Host·클라이언트 모든 화면에서 불린다 (#325).<br/>
/// - 색 효과: 실명은 어두운 틴트, 이속 감소는 파란 틴트. 둘 다 걸리면 곱해서 섞는다. 출혈은 데미지가 들어갈 때마다(1초마다) 빨갛게 번쩍인다.<br/>
///   · 서버·Host는 StatusEffects.BleedTicked(실제 데미지)에 맞춰 번쩍인다. 클라이언트에는 BleedTicked가 없어서, 출혈이 걸린 순간부터 BleedInterval마다 번쩍인다.<br/>
///   · 색은 상태가 걸린 동안만 바꾸고, 모두 풀리면 처음 색으로 되돌린다.<br/>
/// - 그림 효과(핏방울, 표식, 머리 위 별, 검은 연기, 발밑 얼음): 걸린 동안 대상의 자식으로 띄운다. 대상 크기가 1이 아니어도 그림은 원래 비율 (#456). Inspector 칸이 비어 있으면 기본 그림(Resources/StatusEffects/상태 이름, #363)을 쓰고, 그것도 없으면 색 효과만 낸다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 각 화면이 직접 만들고 없앤다.
/// </para>
/// </summary>
[RequireComponent(typeof(StatusEffects))]
public class StatusEffectView : MonoBehaviour
{
	[Header("색을 바꿀 그림")]
	[Tooltip("비워 두면 이 오브젝트(없으면 자식)의 첫 SpriteRenderer")]
	[SerializeField] SpriteRenderer targetRenderer;

	[Header("색 효과 (임시)")]
	[Tooltip("실명: 곱하는 색 (어둡게)")]
	[SerializeField] Color blindTint = new Color(0.45f, 0.45f, 0.5f, 1f);
	[Tooltip("이속 감소: 곱하는 색 (파랗게)")]
	[SerializeField] Color slowTint = new Color(0.6f, 0.75f, 1f, 1f);
	[Tooltip("출혈: 번쩍일 때 색")]
	[SerializeField] Color bleedFlashColor = new Color(1f, 0.25f, 0.25f, 1f);
	[Tooltip("출혈: 번쩍이는 시간 (초)")]
	[SerializeField, Min(0.02f)] float bleedFlashSeconds = 0.15f;

	[Header("그림 효과 (비워 두면 Resources/StatusEffects의 기본 그림, #363)")]
	[Tooltip("출혈: 몸에서 떨어지는 핏방울 (몸 가운데)")]
	[SerializeField] GameObject bleedEffect;
	[Tooltip("표식: 표적 링 (몸 가운데)")]
	[SerializeField] GameObject markEffect;
	[Tooltip("기절: 머리 위에서 도는 별 (머리 위)")]
	[SerializeField] GameObject stunEffect;
	[Tooltip("실명: 검은 연기 (머리 쪽)")]
	[SerializeField] GameObject blindEffect;
	[Tooltip("이속 감소: 발밑 기운 (발밑)")]
	[SerializeField] GameObject slowEffect;
	[Tooltip("머리 위 효과(별)를 콜라이더 윗면에서 얼마나 올릴지 (유닛). 아이콘(StatusIconView)보다 낮게")]
	[SerializeField, Min(0f)] float headEffectHeight = 0.2f;

	StatusEffects status;
	Collider2D body;
	Color baseColor = Color.white;
	bool tinted;
	float flashEndTime = -1f;
	float nextClientBleedFlash;
	readonly HashSet<StatusType> shown = new HashSet<StatusType>();
	readonly Dictionary<StatusType, GameObject> effects = new Dictionary<StatusType, GameObject>();

	/// <summary>지금 몸 효과를 보여 주는 상태 이상. 테스트가 읽는다.</summary>
	public IReadOnlyCollection<StatusType> ShownStatuses => shown;

	/// <summary>출혈로 번쩍인 횟수 (보여 준 뒤로 센다). 테스트가 읽는다.</summary>
	public int BleedFlashCount { get; private set; }

	/// <summary>지금 번쩍이는 중이면 true. 테스트가 읽는다.</summary>
	public bool IsFlashing => Time.time < flashEndTime;

	/// <summary>처음 색 (상태가 없을 때의 색). 테스트가 읽는다.</summary>
	public Color BaseColor => baseColor;

	/// <summary>색을 바꾸는 그림. 테스트가 읽는다.</summary>
	public SpriteRenderer TargetRenderer => targetRenderer;

	/// <summary>그 상태의 그림 효과 오브젝트가 떠 있으면 true. 테스트가 읽는다.</summary>
	public bool HasEffectObject(StatusType type) => effects.TryGetValue(type, out GameObject obj) && obj != null;

	// 서버(Host 포함)·네트워크 없는 테스트는 실제 출혈 데미지(BleedTicked)에 맞춰 번쩍인다
	static bool IsClientOnly => NetworkClient.active && !NetworkServer.active;

	void Awake()
	{
		status = GetComponent<StatusEffects>();
		body = GetComponentInChildren<Collider2D>();
		if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
		if (targetRenderer == null) targetRenderer = GetComponentInChildren<SpriteRenderer>();
		if (targetRenderer != null) baseColor = targetRenderer.color;
	}

	void OnEnable()
	{
		status.StatusApplied += OnApplied;
		status.StatusRemoved += OnRemoved;
		status.BleedTicked += OnBleedTicked;
	}

	void OnDisable()
	{
		status.StatusApplied -= OnApplied;
		status.StatusRemoved -= OnRemoved;
		status.BleedTicked -= OnBleedTicked;
		foreach (StatusType type in new List<StatusType>(effects.Keys)) DespawnEffect(type);
		shown.Clear();
		flashEndTime = -1f;
		RestoreColor();
	}

	void OnApplied(StatusType type)
	{
		// 다시 걸면(지속 시간만 새로 시작) 그대로 둔다. 출혈 번쩍임 간격도 서버처럼 그대로
		if (!shown.Add(type)) return;
		if (type == StatusType.Bleed) nextClientBleedFlash = Time.time + StatusDefaults.BleedInterval;
		SpawnEffect(type);
	}

	void OnRemoved(StatusType type)
	{
		if (!shown.Remove(type)) return;
		DespawnEffect(type);
	}

	void OnBleedTicked(int damage) => Flash();

	void Flash()
	{
		flashEndTime = Time.time + bleedFlashSeconds;
		BleedFlashCount++;
	}

	void Update()
	{
		// 클라이언트는 BleedTicked가 없어서 걸린 순간부터 간격마다 번쩍인다
		if (IsClientOnly && shown.Contains(StatusType.Bleed) && Time.time >= nextClientBleedFlash)
		{
			nextClientBleedFlash += StatusDefaults.BleedInterval;
			Flash();
		}
	}

	// 다른 스크립트가 같은 프레임에 색을 바꿔도(애니메이션 등) 마지막에 덮도록 LateUpdate에서 칠한다
	void LateUpdate()
	{
		if (targetRenderer == null) return;

		Color tint = Color.white;
		bool any = false;
		if (shown.Contains(StatusType.Blind)) { tint *= blindTint; any = true; }
		if (shown.Contains(StatusType.Slow)) { tint *= slowTint; any = true; }
		bool flashing = IsFlashing;

		if (!any && !flashing)
		{
			// 상태가 모두 풀리면 한 번만 처음 색으로 되돌린다. 그 뒤로는 손대지 않는다 (다른 스크립트의 색을 덮지 않게)
			RestoreColor();
			return;
		}

		Color color = baseColor * tint;
		if (flashing) color = new Color(bleedFlashColor.r, bleedFlashColor.g, bleedFlashColor.b, color.a);
		targetRenderer.color = color;
		tinted = true;
	}

	void RestoreColor()
	{
		if (!tinted || targetRenderer == null) return;
		targetRenderer.color = baseColor;
		tinted = false;
	}

	// 그림 효과는 이 두 함수로만 만들고 없앤다 (코드 작성 규칙 3). 대상의 자식이라 같이 움직이고 같이 없어진다
	void SpawnEffect(StatusType type)
	{
		GameObject prefab = PrefabOf(type);
		if (prefab == null || HasEffectObject(type)) return;
		GameObject obj = Instantiate(prefab, transform);
		obj.transform.position = AnchorOf(type);
		// 대상 크기가 1이 아니어도(Leak 1.5×1) 그림은 원래 비율로 보이게 되돌린다 (#456)
		obj.transform.localScale = Vector3.Scale(prefab.transform.localScale, InverseScale(transform));
		effects[type] = obj;
	}

	void DespawnEffect(StatusType type)
	{
		if (effects.TryGetValue(type, out GameObject obj) && obj != null) Destroy(obj);
		effects.Remove(type);
	}

	GameObject PrefabOf(StatusType type)
	{
		GameObject assigned = type switch
		{
			StatusType.Bleed => bleedEffect,
			StatusType.Mark => markEffect,
			StatusType.Stun => stunEffect,
			StatusType.Blind => blindEffect,
			StatusType.Slow => slowEffect,
			_ => null,
		};
		return assigned != null ? assigned : DefaultPrefab(type);
	}

	// 칸이 비어 있으면 Resources/StatusEffects/{상태 이름} 프리팹을 쓴다 (#363). 몬스터 프리팹마다 칸을 채우지 않아도 되게.
	// 한 번 찾은 결과(없음 포함)는 모든 몬스터가 같이 쓴다
	static readonly Dictionary<StatusType, GameObject> defaults = new Dictionary<StatusType, GameObject>();

	static GameObject DefaultPrefab(StatusType type)
	{
		if (!defaults.TryGetValue(type, out GameObject prefab))
		{
			prefab = Resources.Load<GameObject>($"StatusEffects/{type}");
			defaults[type] = prefab;
		}
		return prefab;
	}

	/// <summary>parent 자식의 크기를 parent 크기만큼 되돌리는 배율 (#456). 몬스터 프리팹 크기가 (1.5, 1)이어도 자식 그림이 늘어나지 않게.</summary>
	public static Vector3 InverseScale(Transform parent)
	{
		Vector3 s = parent.lossyScale;
		return new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
	}

	// 효과를 놓을 자리: 별은 머리 위, 연기는 머리 쪽, 발밑 기운은 발밑, 나머지는 몸 가운데
	Vector3 AnchorOf(StatusType type)
	{
		Bounds b = body != null ? body.bounds : new Bounds(transform.position + Vector3.up, Vector3.one * 2f);
		float z = transform.position.z;
		return type switch
		{
			StatusType.Stun => new Vector3(b.center.x, b.max.y + headEffectHeight, z),
			StatusType.Blind => new Vector3(b.center.x, b.max.y - b.size.y * 0.2f, z),
			StatusType.Slow => new Vector3(b.center.x, b.min.y, z),
			_ => new Vector3(b.center.x, b.center.y, z),
		};
	}
}
