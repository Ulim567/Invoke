using UnityEngine;

/// <summary>
/// [표시] 재료 아이콘 하나가 죽은 몬스터 자리에서 톡 튀어 오른 뒤 받은 플레이어에게 날아가 사라진다 (#404). MaterialDropView가 만든다.
/// <para>
/// - Launch로 튀어 오르는 속도와 받을 플레이어를 받는다. Pop Time 동안은 중력을 받아 포물선으로 튀고, 그 뒤 플레이어 몸 가운데로 점점 빨라지며 날아간다.<br/>
/// - 플레이어에 닿거나(Arrive Distance), 플레이어가 없어지거나, Max Lifetime이 지나면 스스로 없어진다.<br/>
/// - 보여 주기만 하는 오브젝트라 네트워크 오브젝트가 아니다. 반짝임은 같은 오브젝트의 SpriteFrameLoop가 맡는다.
/// </para>
/// </summary>
public class MaterialDropFlight : MonoBehaviour
{
	[Tooltip("튀어 오르는 시간 (초). 그 뒤 플레이어에게 날아간다")]
	[SerializeField, Min(0f)] float popTime = 0.4f;
	[Tooltip("튀어 오를 때 중력 (유닛/초²)")]
	[SerializeField, Min(0f)] float popGravity = 18f;
	[Tooltip("날아가기 시작할 때 속도 (유닛/초)")]
	[SerializeField, Min(0f)] float flyStartSpeed = 3f;
	[Tooltip("날아가는 동안 붙는 가속 (유닛/초²)")]
	[SerializeField, Min(0f)] float flyAcceleration = 45f;
	[Tooltip("플레이어 몸 가운데와 이 거리 안이면 닿은 것으로 본다 (유닛)")]
	[SerializeField, Min(0.01f)] float arriveDistance = 0.25f;
	[Tooltip("못 닿아도 이 시간이 지나면 없어진다 (초)")]
	[SerializeField, Min(0.1f)] float maxLifetime = 3f;

	Transform target;
	Collider2D targetBody;
	Vector2 velocity;
	float speed;
	float age;
	bool launched;

	/// <summary>튀어 오르는 중이면 false, 플레이어에게 날아가는 중이면 true. 테스트가 읽는다.</summary>
	public bool IsHoming => launched && age >= popTime;

	/// <summary>날아갈 플레이어와 처음 튀어 오르는 속도를 정한다. 만든 직후 한 번 부른다.</summary>
	public void Launch(Transform player, Vector2 popVelocity)
	{
		target = player;
		targetBody = player != null ? player.GetComponentInChildren<Collider2D>() : null;
		velocity = popVelocity;
		speed = flyStartSpeed;
		age = 0f;
		launched = true;
	}

	void Update()
	{
		if (!launched) return;
		age += Time.deltaTime;
		if (target == null || age >= maxLifetime)
		{
			Destroy(gameObject);
			return;
		}

		Vector2 pos = transform.position;
		if (age < popTime)
		{
			velocity.y -= popGravity * Time.deltaTime;
			pos += velocity * Time.deltaTime;
		}
		else
		{
			speed += flyAcceleration * Time.deltaTime;
			pos = Vector2.MoveTowards(pos, AimPoint(), speed * Time.deltaTime);
			if (Vector2.Distance(pos, AimPoint()) <= arriveDistance)
			{
				Destroy(gameObject);
				return;
			}
		}
		transform.position = new Vector3(pos.x, pos.y, transform.position.z);
	}

	// 플레이어 몸 가운데 (콜라이더가 없으면 발밑에서 1칸 위)
	Vector2 AimPoint() => targetBody != null ? (Vector2)targetBody.bounds.center : (Vector2)target.position + Vector2.up;
}
