using UnityEngine;

/// <summary>
/// 투사체 그림이 날아가는 방향을 보게 한다. 연출만 한다.
/// <para>
/// 그림은 오른쪽으로 날아가는 모양(꼬리가 왼쪽)으로 그리고, 왼쪽으로 날아가면 flipX로 뒤집는다.<br/>
/// 투사체 프리팹 최상위에 SpriteRenderer, Rigidbody2D와 함께 붙인다. 속도는 읽기만 한다 (MonsterProjectile이 정함).
/// </para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ProjectileFacing : MonoBehaviour
{
	SpriteRenderer spriteRenderer;
	Rigidbody2D body;

	void Awake()
	{
		spriteRenderer = GetComponent<SpriteRenderer>();
		body = GetComponent<Rigidbody2D>();
	}

	void LateUpdate()
	{
		if (body == null) return;
		float x = body.linearVelocity.x;
		// 거의 수직으로 날 때는 방향을 바꾸지 않는다 (깜빡이지 않게)
		if (Mathf.Abs(x) > 0.01f) spriteRenderer.flipX = x < 0f;
	}
}
