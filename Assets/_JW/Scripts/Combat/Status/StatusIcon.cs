using UnityEngine;

/// <summary>
/// [표시] 상태 이상 아이콘 하나 (StatusIcon 프리팹). StatusIconView가 만들고 없앤다.
/// Sprites는 StatusType 순서(출혈, 표식, 기절, 실명, 이속 감소)로 넣는다. 그림은 임혜원 #114 (_HW/Effects/StatusIcons).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class StatusIcon : MonoBehaviour
{
	[Tooltip("StatusType 순서: 출혈, 표식, 기절, 실명, 이속 감소")]
	[SerializeField] Sprite[] sprites = new Sprite[5];

	/// <summary>지금 보여 주는 상태.</summary>
	public StatusType Type { get; private set; }

	/// <summary>type의 아이콘으로 바꾼다.</summary>
	public void Show(StatusType type)
	{
		Type = type;
		int index = (int)type;
		SpriteRenderer sprite = GetComponent<SpriteRenderer>();
		sprite.sprite = index < sprites.Length ? sprites[index] : null;
	}
}
