using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerMove : NetworkBehaviour
{
    [SerializeField, Min(0f)] private float walkSpeed;
	[SerializeField, Min(0f)] private float jumpForce;
	[SerializeField] private Rigidbody2D rigidbody;

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();

		rigidbody.simulated = true;
	}

	void Update()
    {
        if (!isLocalPlayer) return;
        Move();
	}

    void Move()
    {
		Vector3 direction = Vector3.zero;
		if (Keyboard.current.aKey.isPressed)
		{
			direction = Vector3.left;
		}
		else if (Keyboard.current.dKey.isPressed)
		{
			direction = Vector3.right;
		}
		rigidbody.linearVelocity = new Vector2(direction.x * walkSpeed, rigidbody.linearVelocity.y);

		if (Keyboard.current.spaceKey.wasPressedThisFrame)
		{
			rigidbody.linearVelocity = new Vector2(rigidbody.linearVelocity.x, 0f);
			rigidbody.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
		}
	}
}
