using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;

public class PlayerMove : NetworkBehaviour
{
    [SerializeField, Min(0f)] private float speed;

    void Update()
    {
        if (!isLocalPlayer) return;
        Move();
	}

    void Move()
    {
        Vector3 direction = Vector3.zero;
        if (Keyboard.current.wKey.isPressed)
        {
            direction = Vector3.up;
        }
        else if (Keyboard.current.sKey.isPressed)
        {
            direction = Vector3.down;
		}
		else if (Keyboard.current.aKey.isPressed)
		{
			direction = Vector3.left;
		}
		else if (Keyboard.current.dKey.isPressed)
		{
			direction = Vector3.right;
		}

		transform.position += (Vector3)(speed * Time.deltaTime * direction);
	}
}
