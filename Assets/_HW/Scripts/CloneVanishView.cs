using UnityEngine;

/// <summary>
/// 보스 기믹 3 분신(GatewayClone)이 사라질 때 짧은 글리치 소멸 장면을 트는 컴포넌트. 연출만 한다.
/// <para>
/// 사용법: GatewayClone 프리팹 최상위(SpriteRenderer, Animator, GatewayAnimator와 같은 곳)에 붙인다.<br/>
/// - 사망(<c>Monster.DeathEffectPlayed</c>) 때 Animator와 GatewayAnimator를 끄고, 소멸 장면을 한 번 틀고 마지막 장면에서 멈춘다.<br/>
///   본체의 Death 모션(1.5초)은 분신의 Despawn Delay(0.75초)보다 길어서 잘리므로 분신은 이 장면을 쓴다.<br/>
/// - 좌우 뒤집기(flipX)는 그대로 둔다. 소멸 장면도 같은 방향으로 보인다.
/// </para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CloneVanishView : MonoBehaviour
{
	[SerializeField, Tooltip("소멸 장면들 (Gateway_CloneVanish_0 ~ 5)")]
	Sprite[] vanishFrames;

	[SerializeField, Min(1f), Tooltip("초당 장면 수. 6장 ÷ 12 = 0.5초 (분신 Despawn Delay 0.75초 안)")]
	float fps = 12f;

	SpriteRenderer spriteRenderer;
	Animator animator;
	GatewayAnimator gatewayAnimator;
	Monster monster;
	float startTime = -1f;

	void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		animator = GetComponent<Animator>();
		gatewayAnimator = GetComponent<GatewayAnimator>();
		monster = GetComponent<Monster>();
	}

	void OnEnable()
	{
		if (monster != null) monster.DeathEffectPlayed += OnDeath;
	}

	void OnDisable()
	{
		if (monster != null) monster.DeathEffectPlayed -= OnDeath;
	}

	void OnDeath()
	{
		if (vanishFrames == null || vanishFrames.Length == 0) return;
		startTime = Time.time;
		// 본체 Death 모션이 그림을 덮어쓰지 않게 끈다
		if (gatewayAnimator != null) gatewayAnimator.enabled = false;
		if (animator != null) animator.enabled = false;
	}

#if UNITY_EDITOR
	// 확인용. Play 중에 Inspector의 Clone Vanish View ⋮ 메뉴에서 누른다. 체력은 Monster.TakeDamage로만 줄인다
	[ContextMenu("시험: 사라지기")]
	void TestVanish()
	{
		if (Application.isPlaying && monster != null) monster.TakeDamage(monster.Hp);
	}
#endif

	// Animator보다 뒤에 그림을 바꾼다
	void LateUpdate()
	{
		if (startTime < 0f) return;
		int frame = Mathf.Min(vanishFrames.Length - 1, (int)((Time.time - startTime) * fps));
		spriteRenderer.sprite = vanishFrames[frame];
	}
}
