using UnityEngine;

/// <summary>
/// 몬스터가 투사체를 모으는 동안(ProjectileAttack.Fire Delay) 날아갈 줄을 빨간 띠로 보여 준다. 연출만 한다.
/// <para>
/// - 같은 오브젝트의 ProjectileAttack.FireEffectPlayed(방향)를 받아, 발사 위치(Fire Point Offset)에서 그 방향으로 Length만큼 띠를 그린다.<br/>
/// - 띠는 깜빡이며 점점 진해지고, 투사체가 나가는 순간 사라진다.<br/>
/// - FireEffectPlayed는 [ClientRpc]라 이 몬스터를 보는 모든 화면(호스트 포함)에서 보인다. 서버 판정에는 관여하지 않는다.
/// </para>
/// <para>사용법: GatewayAI가 시작할 때 Gateway에 붙인다. 다른 몬스터에 쓰려면 ProjectileAttack이 있는 오브젝트에 붙인다.</para>
/// </summary>
[RequireComponent(typeof(ProjectileAttack))]
public class ThrowWarning : MonoBehaviour
{
	[SerializeField, Min(1f), Tooltip("띠 길이 (유닛). 투사체가 날아가는 거리를 덮게 둔다")]
	float length = 20f;

	[SerializeField, Min(0.1f), Tooltip("띠 두께 (유닛). 투사체 크기보다 조금 크게")]
	float thickness = 1.2f;

	[SerializeField, Tooltip("띠 색. 알파가 가장 진할 때 값")]
	Color color = new Color(1f, 0.15f, 0.15f, 0.45f);

	[SerializeField, Min(1f), Tooltip("초당 깜빡이는 횟수")]
	float blinkPerSecond = 6f;

	[SerializeField, Tooltip("띠를 그릴 Sorting Layer")]
	string sortingLayer = "Effect";

	ProjectileAttack thrower;
	SpriteRenderer bar;
	float startTime;
	float endTime;

	// 모든 띠가 같이 쓰는 1×1 흰 그림
	static Sprite whiteSprite;

	void Awake()
	{
		thrower = GetComponent<ProjectileAttack>();
		CreateBar();
	}

	void OnEnable() => thrower.FireEffectPlayed += Show;

	void OnDisable()
	{
		thrower.FireEffectPlayed -= Show;
		if (bar != null) bar.enabled = false;
	}

	// 띠 오브젝트를 만드는 곳은 여기 하나다 (코드 작성 규칙 3). 이 오브젝트의 자식이라 같이 없어진다
	void CreateBar()
	{
		if (whiteSprite == null)
			whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 4f);

		var go = new GameObject("ThrowWarning");
		go.transform.SetParent(transform, false);
		bar = go.AddComponent<SpriteRenderer>();
		bar.sprite = whiteSprite;
		bar.sortingLayerName = sortingLayer;
		bar.enabled = false;
	}

	void Show(Vector2 direction)
	{
		float dir = direction.x < 0f ? -1f : 1f;
		Vector2 offset = thrower.FirePointOffset;
		Transform t = bar.transform;
		// 부모(몬스터)가 뒤집혀 있어도 월드 기준으로 맞춘다
		t.position = (Vector2)transform.position + new Vector2(offset.x * dir, offset.y);
		t.rotation = Quaternion.identity;
		Vector3 parentScale = transform.lossyScale;
		t.localScale = new Vector3(length * dir / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), thickness / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);

		startTime = Time.time;
		endTime = Time.time + Mathf.Max(0.1f, thrower.FireDelay);
		bar.enabled = true;
	}

	void Update()
	{
		if (bar == null || !bar.enabled) return;
		if (Time.time >= endTime)
		{
			bar.enabled = false;
			return;
		}

		// 점점 진해지면서 깜빡인다
		float progress = Mathf.InverseLerp(startTime, endTime, Time.time);
		float blink = Mathf.PingPong((Time.time - startTime) * blinkPerSecond * 2f, 1f);
		Color c = color;
		c.a = color.a * Mathf.Lerp(0.35f, 1f, progress) * Mathf.Lerp(0.5f, 1f, blink);
		bar.color = c;
	}
}
