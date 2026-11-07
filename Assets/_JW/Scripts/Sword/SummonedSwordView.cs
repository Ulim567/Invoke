using UnityEngine;

/// <summary>
/// [표시] 소환검 상태에 맞춰 그림을 바꾼다. 소환검 프리팹에 SummonedSword와 함께 붙인다.
/// <para>
/// - 누수됐을 때 반투명하게 한다.<br/>
/// - 사용 가능 상태에서 SummonedSword.DrawBehind면 캐릭터 뒤(Behind Sorting Layer / Order)에 그린다 (#415). 아니면 프리팹에 정한 정렬(Projectile) 그대로.
/// </para>
/// </summary>
[RequireComponent(typeof(SummonedSword))]
public class SummonedSwordView : MonoBehaviour
{
	[Tooltip("누수됐을 때 그림 투명도")]
	[SerializeField, Range(0f, 1f)] float leakedAlpha = 0.35f;
	[Tooltip("캐릭터 뒤에 그릴 때 Sorting Layer (캐릭터 그림이 Player 레이어 0번)")]
	[SerializeField] string behindSortingLayer = "Player";
	[Tooltip("캐릭터 뒤에 그릴 때 Order in Layer (캐릭터 0번보다 작게)")]
	[SerializeField] int behindSortingOrder = -5;

	SummonedSword sword;
	SpriteRenderer sprite;
	float normalAlpha = 1f;
	int frontLayerId;
	int frontOrder;
	bool drawnBehind;

	void Awake()
	{
		sword = GetComponent<SummonedSword>();
		sprite = GetComponentInChildren<SpriteRenderer>();
		if (sprite != null)
		{
			normalAlpha = sprite.color.a;
			frontLayerId = sprite.sortingLayerID;
			frontOrder = sprite.sortingOrder;
		}
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

	// 사용 가능한 검만 뒤에 그린다. 던지거나 찌를 때는 늘 앞에 보인다
	void LateUpdate()
	{
		if (sprite == null) return;
		bool behind = sword.DrawBehind && sword.State == SummonedSwordState.Ready;
		if (behind == drawnBehind) return;
		drawnBehind = behind;
		if (behind)
		{
			sprite.sortingLayerName = behindSortingLayer;
			sprite.sortingOrder = behindSortingOrder;
		}
		else
		{
			sprite.sortingLayerID = frontLayerId;
			sprite.sortingOrder = frontOrder;
		}
	}
}
