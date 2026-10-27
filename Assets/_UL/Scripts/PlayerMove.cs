using UnityEngine;
using Mirror;

[RequireComponent(typeof(PlayerStats), typeof(PlayerInputReader), typeof(Rigidbody2D))]
public class PlayerMove : NetworkBehaviour
{
	[SerializeField, Min(0f)] private float jumpForce;
	[SerializeField] private Rigidbody2D rigidbody;
	[SerializeField] private PlayerInputReader input;
	[SerializeField] private PlayerStats stats;

	void Update()
    {
        if (!isLocalPlayer) return;
        Move();
	}

    void Move()
    {
		rigidbody.linearVelocity = new Vector2(input.MoveX * stats.MoveSpeed, rigidbody.linearVelocity.y);

		if (input.JumpPressed)
		{
			rigidbody.linearVelocity = new Vector2(rigidbody.linearVelocity.x, 0f);
			rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
		}
	}
}
