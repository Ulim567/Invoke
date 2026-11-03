using UnityEngine;

/// <summary>
/// 보스 기믹 1 기둥(Pillar)의 그림을 체력에 맞춰 바꾸는 컴포넌트. 연출만 한다.
/// <para>
/// 사용법: Pillar 프리팹 최상위(Monster와 같은 곳)에 붙이고, Sprite Renderer 칸에 그림을 그릴 자식(Body)의 SpriteRenderer를 넣는다.<br/>
/// - 체력 50% 초과: 멀쩡 / 50% 이하: 금 감 / 사망(<c>Monster.DeathEffectPlayed</c>): 부서지는 장면을 한 번 틀고 마지막 장면에서 멈춘다.<br/>
/// - 값은 <c>Monster.Hp</c>, <c>MaxHp</c>(김도훈)를 읽기만 한다. Animator 없이 스프라이트만 바꾼다.<br/>
/// - 맞으면 잠깐 밝게 깜빡인다 (<c>Monster.HitEffectPlayed</c>).
/// </para>
/// </summary>
public class PillarView : MonoBehaviour
{
	[SerializeField, Tooltip("그림을 바꿀 SpriteRenderer (Pillar의 자식 Body)")]
	SpriteRenderer spriteRenderer;

	[Header("그림 (Assets/_HW/Boss/Pillar)")]
	[SerializeField, Tooltip("체력 50% 초과")]
	Sprite intact;
	[SerializeField, Tooltip("체력 50% 이하")]
	Sprite cracked;
	[SerializeField, Tooltip("부서지는 장면들 (Pillar_Break_0 ~ 5)")]
	Sprite[] breakFrames;

	[SerializeField, Range(0f, 1f), Tooltip("이 비율 이하로 체력이 줄면 금 간 그림")]
	float crackedRatio = 0.5f;

	[SerializeField, Min(1f), Tooltip("부서지는 장면의 초당 장면 수. 6장 ÷ 12 = 0.5초 = Pillar의 Despawn Delay")]
	float breakFps = 12f;

	[SerializeField, Tooltip("맞았을 때 깜빡이는 색")]
	Color hitFlashColor = new Color(1f, 0.75f, 0.9f, 1f);

	[SerializeField, Min(0f), Tooltip("맞았을 때 깜빡이는 시간(초)")]
	float hitFlashTime = 0.08f;

	Monster monster;
	Color baseColor = Color.white;
	float flashEndTime;
	float breakStartTime = -1f;

	void Awake()
	{
		monster = GetComponent<Monster>();
		if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
		if (spriteRenderer != null) baseColor = spriteRenderer.color;
	}

	void OnEnable()
	{
		if (monster == null) return;
		monster.HitEffectPlayed += OnHit;
		monster.DeathEffectPlayed += OnDeath;
	}

	void OnDisable()
	{
		if (monster == null) return;
		monster.HitEffectPlayed -= OnHit;
		monster.DeathEffectPlayed -= OnDeath;
	}

	void Update()
	{
		if (spriteRenderer == null) return;

		spriteRenderer.sprite = ChooseSprite();
		spriteRenderer.color = Time.time < flashEndTime ? hitFlashColor : baseColor;
	}

	Sprite ChooseSprite()
	{
		// 부서지는 중: 장면을 차례로 틀고 마지막 장면에서 멈춘다 (오브젝트는 Despawn Delay 뒤에 없어진다)
		if (breakStartTime >= 0f && breakFrames != null && breakFrames.Length > 0)
		{
			int frame = Mathf.Min(breakFrames.Length - 1, (int)((Time.time - breakStartTime) * breakFps));
			return breakFrames[frame];
		}

		if (monster == null || monster.MaxHp <= 0) return intact;
		return monster.Hp <= monster.MaxHp * crackedRatio ? cracked : intact;
	}

	void OnHit(int damage)
	{
		if (breakStartTime >= 0f) return;
		flashEndTime = Time.time + hitFlashTime;
	}

	void OnDeath()
	{
		breakStartTime = Time.time;
		flashEndTime = 0f;
	}
}
