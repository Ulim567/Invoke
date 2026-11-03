using UnityEngine;

/// <summary>
/// 보스 전체 공격(Gateway 기믹 2)의 안전지대. 땅 위에 놓이고, 이 칸 안에 있는 플레이어는 전체 공격을 맞지 않는다.
/// <para>
/// - 판정 칸: 오브젝트 위치(땅 위, Pivot 바닥)를 아래 가운데로 하는 Width × Height 상자.<br/>
/// - 보여 주기는 자식 그림이 한다. 이 컴포넌트는 칸 크기와 "안에 있는지"만 알려 준다. 값은 바꾸지 않는다.<br/>
/// - 만들고 없애는 일은 GatewayPhases가 NetworkSpawner로 한다. 그래서 프리팹에 NetworkIdentity를 붙이고 NetworkManager에 등록한다.
/// </para>
/// </summary>
public class SafeZone : MonoBehaviour
{
	[SerializeField, Min(0.5f), Tooltip("안전지대 가로 (유닛). 바닥 그림 폭과 맞춘다")]
	float width = 4f;

	[SerializeField, Min(0.5f), Tooltip("안전지대 높이 (유닛). 땅에서 이 높이까지 안이면 안전하다")]
	float height = 5f;

	/// <summary>
	/// 안전지대 가로 (유닛).
	/// </summary>
	public float Width => width;

	/// <summary>
	/// point(월드 좌표, 보통 플레이어 발 위치)가 안전지대 안이면 true.
	/// </summary>
	public bool Contains(Vector2 point)
	{
		Vector2 p = transform.position;
		return Mathf.Abs(point.x - p.x) <= width * 0.5f && point.y >= p.y - 0.1f && point.y <= p.y + height;
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.green;
		Gizmos.DrawWireCube(transform.position + new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, 0f));
	}
}
