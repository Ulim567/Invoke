using UnityEngine;

/// <summary>
/// [표시] 일반 몬스터 머리 위에 작은 체력 바를 띄운다 (#388). 몬스터 프리팹 최상위(Monster와 같은 곳)에 붙인다. 값은 바꾸지 않는다 (코드 작성 규칙 1).
/// <para>
/// - Monster.Hp / MaxHp 비율로 빨간 채움을 1픽셀 단위로 줄인다 (안쪽 20픽셀). 체력이 1이라도 남아 있으면 1픽셀은 남긴다.<br/>
/// - 흰 잔상: 맞은 뒤 Trail Delay(0.3초) 동안 맞기 전 너비를 유지하다가 Trail Speed(초당 30픽셀)로 빨간 쪽까지 따라 줄어든다.<br/>
/// - 체력이 가득이면 숨기고, 줄어 있으면 보이고, 죽으면 숨긴다. 보스(IsBoss)·체력이 안 주는 몬스터(IsInvulnerable, 허수아비)는 띄우지 않는다.<br/>
/// - 클라이언트에서도 Hp·HitEffectPlayed가 서버와 같으므로(MonsterNetwork) 모든 화면에서 같은 바가 보인다.<br/>
/// - 바 그림은 Bar Prefab, 비어 있으면 Resources/MonsterHealthBar. 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterHealthBarView : MonoBehaviour
{
	[Tooltip("비워 두면 Resources/MonsterHealthBar")]
	[SerializeField] MonsterHealthBar barPrefab;
	[Tooltip("콜라이더 윗변에서 바 가운데까지 높이 (유닛)")]
	[SerializeField, Min(0f)] float heightAboveTop = 0.6f;
	[Tooltip("맞은 뒤 흰 잔상이 줄기 시작할 때까지 (초)")]
	[SerializeField, Min(0f)] float trailDelay = 0.3f;
	[Tooltip("흰 잔상이 줄어드는 속도 (초당 픽셀)")]
	[SerializeField, Min(1f)] float trailSpeed = 30f;

	Monster monster;
	Collider2D body;
	MonsterHealthBar bar;
	float trailPixels = MonsterHealthBar.InnerPixels;
	float trailHoldUntil;

	/// <summary>바가 보이고 있으면 true. 테스트가 읽는다.</summary>
	public bool IsShown => bar != null && bar.gameObject.activeSelf;

	/// <summary>빨간 채움 너비 (픽셀). 테스트가 읽는다.</summary>
	public int FillPixels { get; private set; } = MonsterHealthBar.InnerPixels;

	/// <summary>흰 잔상 너비 (픽셀). 테스트가 읽는다.</summary>
	public int TrailPixels => Mathf.CeilToInt(trailPixels);

	void Awake()
	{
		monster = GetComponent<Monster>();
		body = GetComponentInChildren<Collider2D>();
		if (barPrefab == null) barPrefab = Resources.Load<MonsterHealthBar>("MonsterHealthBar");
	}

	void OnEnable() => monster.HitEffectPlayed += OnHit;
	void OnDisable() => monster.HitEffectPlayed -= OnHit;

	void Start()
	{
		// 보스는 화면 위 BossHealthBar가, 허수아비는 체력이 안 줄어서 띄우지 않는다
		if (monster.IsBoss || monster.IsInvulnerable || barPrefab == null)
		{
			enabled = false;
			return;
		}
		// 바는 이 함수에서만 만든다 (코드 작성 규칙 3). 몬스터의 자식이라 같이 없어진다
		bar = Instantiate(barPrefab, transform);
		// 몬스터 프리팹 크기가 1이 아니어도 바는 그림 그대로(16 PPU) 보이게 한다
		Vector3 s = transform.lossyScale;
		bar.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.0001f, Mathf.Abs(s.y)), 1f);
		bar.gameObject.SetActive(false);
	}

	// 맞은 순간: 잔상은 맞기 전 너비에서 잠깐 멈춰 있다가 줄어든다
	void OnHit(int damage) => trailHoldUntil = Time.time + trailDelay;

	// 몬스터가 움직이거나 그림이 뒤집혀도(flipX) 바는 늘 머리 위 가운데에 똑바로 둔다
	void LateUpdate()
	{
		if (bar == null) return;
		int maxHp = Mathf.Max(1, monster.MaxHp);
		bool show = !monster.IsDead && monster.Hp < maxHp;
		if (bar.gameObject.activeSelf != show) bar.gameObject.SetActive(show);
		if (!show)
		{
			trailPixels = MonsterHealthBar.InnerPixels;
			return;
		}

		FillPixels = Mathf.Clamp(Mathf.CeilToInt((float)MonsterHealthBar.InnerPixels * monster.Hp / maxHp), 1, MonsterHealthBar.InnerPixels);
		if (trailPixels < FillPixels) trailPixels = FillPixels; // 체력이 다시 차면 잔상도 같이
		else if (Time.time >= trailHoldUntil) trailPixels = Mathf.Max(FillPixels, trailPixels - trailSpeed * Time.deltaTime);
		bar.SetPixels(FillPixels, TrailPixels);

		float top = body != null ? body.bounds.max.y : transform.position.y + 1f;
		float centerX = body != null ? body.bounds.center.x : transform.position.x;
		// 16 PPU 픽셀 격자에 맞춰 바가 떨리지 않게 한다
		const float ppu = 16f;
		bar.transform.position = new Vector3(Mathf.Round(centerX * ppu) / ppu, Mathf.Round((top + heightAboveTop) * ppu) / ppu, transform.position.z);
	}
}
