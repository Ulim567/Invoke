using UnityEngine;

/// <summary>
/// [표시] 날아가는 마법탄 그림 하나. 마법탄 프리팹(Resources/Magic/*_bolt)에 붙인다. MagicEffectView가 만들고 Launch로 날린다.
/// <para>
/// - 판정은 하지 않는다. 서버의 판정(MagicBoltSkill)과 같은 방향·속도·사거리로 그림만 움직인다.<br/>
/// - 사거리 끝에 닿으면 스스로 없앤다. 몬스터에 맞아 멈추는 것은 MagicEffectView가 Hit을 받아 없앤다.<br/>
/// - 그림은 오른쪽을 보는 기준이다. 날아가는 방향으로 돌리는 일은 MagicEffectView가 만들 때 한다.
/// </para>
/// </summary>
public class MagicBoltVisual : MonoBehaviour
{
	Vector2 direction;
	float speed;
	float range;
	float travelled;
	bool launched;

	/// <summary>날리기 시작한다. direction은 정규화하지 않아도 된다.</summary>
	public void Launch(Vector2 direction, float speed, float range)
	{
		this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
		this.speed = speed;
		this.range = range;
		travelled = 0f;
		launched = true;
	}

	void Update()
	{
		if (!launched) return;
		float step = Mathf.Min(speed * Time.deltaTime, range - travelled);
		transform.position += (Vector3)(direction * step);
		travelled += step;
		if (travelled >= range) Destroy(gameObject);
	}
}
