using UnityEngine;

/// <summary>
/// PlayerMovement.Facing에 맞춰 캐릭터 그림을 좌우로 뒤집는 표시 컴포넌트. 플레이어 프리팹에 PlayerMovement와 함께 붙인다.
/// <para>
/// - 그림은 오른쪽을 보고 그린다고 정한다(임혜원 #28 그림도 오른쪽). 왼쪽을 볼 때 SpriteRenderer.flipX를 켠다.<br/>
/// - 값(Facing)은 PlayerMovement가 정하고, 이 컴포넌트는 보여 주기만 한다 (코드 작성 규칙 1).<br/>
/// - flipX는 그림만 뒤집고 콜라이더나 자식 오브젝트는 뒤집지 않는다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerFacingView : MonoBehaviour
{
	[SerializeField] private PlayerMovement movement;
	[SerializeField] private SpriteRenderer spriteRenderer;

	void Reset()
	{
		movement = GetComponent<PlayerMovement>();
		spriteRenderer = GetComponentInChildren<SpriteRenderer>();
	}

	// 움직임이 끝난 뒤 그 프레임의 방향으로 그린다
	void LateUpdate()
	{
		spriteRenderer.flipX = movement.Facing < 0;
	}
}
