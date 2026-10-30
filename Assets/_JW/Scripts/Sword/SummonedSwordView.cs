using UnityEngine;

/// <summary>
/// [임시 표시] 소환검 상태에 맞춰 그림을 바꾼다. 소환검 프리팹에 SummonedSword와 함께 붙인다.
/// 지금은 누수됐을 때 반투명하게만 한다. 실제 그림·연출은 임혜원 이슈에서 이 컴포넌트를 바꾼다.
/// </summary>
[RequireComponent(typeof(SummonedSword))]
public class SummonedSwordView : MonoBehaviour
{
	[Tooltip("누수됐을 때 그림 투명도")]
	[SerializeField, Range(0f, 1f)] float leakedAlpha = 0.35f;

	SummonedSword sword;
	SpriteRenderer sprite;
	float normalAlpha = 1f;

	void Awake()
	{
		sword = GetComponent<SummonedSword>();
		sprite = GetComponentInChildren<SpriteRenderer>();
		if (sprite != null) normalAlpha = sprite.color.a;
	}

	void OnEnable() => sword.StateChanged += OnStateChanged;

	void OnDisable() => sword.StateChanged -= OnStateChanged;

	void OnStateChanged(SummonedSwordState state)
	{
		if (sprite == null) return;
		Color c = sprite.color;
		c.a = state == SummonedSwordState.Leaked ? leakedAlpha : normalAlpha;
		sprite.color = c;
	}
}
