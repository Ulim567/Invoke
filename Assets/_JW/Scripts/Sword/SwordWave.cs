using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 검기 투사체 (#163). 검 휘두르기·검 크게베기가 SwordWaveLauncher로 만들어 Launch한다.
/// <para>
/// - 정한 방향으로 날아가며 매 물리 프레임 판정 상자(hitSize)에 걸린 몬스터를 MonsterHit으로 알린다. 데미지는 알림을 받은 스킬이 준다.<br/>
/// - 관통이 아니면 처음 맞은 몬스터에서 멈추고, 관통이면 같은 몬스터를 두 번 맞히지 않고 계속 간다.<br/>
/// - 사거리 끝에 닿거나 멈추면 IsFinished가 된다. 없애는 일은 스킬이 SwordWaveLauncher.DespawnWave로 한다.<br/>
/// - 판정은 이 오브젝트가 속한 장소 씬의 물리 세계에 묻는다 (place-scenes.md).<br/>
/// - 그림은 오른쪽을 보는 기준이다. 날아가는 방향으로 돌린다.
/// </para>
/// </summary>
public class SwordWave : MonoBehaviour
{
	Vector2 direction;
	float speed;
	float range;
	float travelled;
	Vector2 hitSize;
	bool pierce;
	bool launched;
	readonly HashSet<Monster> alreadyHit = new HashSet<Monster>();

	/// <summary>[로직] 몬스터가 맞았을 때 (맞은 몬스터). 몬스터마다 한 번</summary>
	public event Action<Monster> MonsterHit;

	/// <summary>사거리 끝에 닿았거나 첫 대상에서 멈춰서 더 이상 판정하지 않으면 true</summary>
	public bool IsFinished { get; private set; }

	/// <summary>지금까지 맞은 몬스터 수</summary>
	public int HitCount => alreadyHit.Count;

	/// <summary>
	/// 날리기 시작한다. SwordWaveLauncher.SpawnWave로 만든 직후 스킬이 한 번 부른다.
	/// </summary>
	/// <param name="direction">날아갈 방향 (정규화하지 않아도 됨)</param>
	/// <param name="speed">초당 유닛</param>
	/// <param name="range">날아갈 거리 (유닛)</param>
	/// <param name="hitSize">판정 상자 크기 (가로 = 날아가는 방향, 세로)</param>
	/// <param name="pierce">true면 관통, false면 첫 대상에서 멈춤</param>
	public void Launch(Vector2 direction, float speed, float range, Vector2 hitSize, bool pierce)
	{
		this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
		this.speed = speed;
		this.range = range;
		this.hitSize = hitSize;
		this.pierce = pierce;
		travelled = 0f;
		IsFinished = false;
		alreadyHit.Clear();
		launched = true;
		transform.rotation = Quaternion.FromToRotation(Vector3.right, this.direction);

		// 만든 자리에 이미 몬스터가 있으면 바로 맞힌다
		CheckHits();
	}

	void FixedUpdate()
	{
		if (!launched || IsFinished) return;

		float step = Mathf.Min(speed * Time.fixedDeltaTime, range - travelled);
		transform.position += (Vector3)(direction * step);
		travelled += step;
		CheckHits();
		if (travelled >= range) IsFinished = true;
	}

	void CheckHits()
	{
		if (IsFinished) return;

		ContactFilter2D filter = new ContactFilter2D();
		filter.SetLayerMask(LayerMask.GetMask("Monster"));
		filter.useTriggers = Physics2D.queriesHitTriggers;
		List<Collider2D> hits = new List<Collider2D>();
		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		gameObject.scene.GetPhysicsScene2D().OverlapBox(transform.position, hitSize, angle, filter, hits);

		// 가까운 몬스터부터 맞힌다 (관통이 아니면 가장 먼저 닿은 한 명)
		hits.Sort((a, b) => ((Vector2)a.bounds.center - (Vector2)transform.position).sqrMagnitude
			.CompareTo(((Vector2)b.bounds.center - (Vector2)transform.position).sqrMagnitude));
		foreach (Collider2D hit in hits)
		{
			Monster monster = hit.GetComponentInParent<Monster>();
			if (monster == null || monster.IsDead || !alreadyHit.Add(monster)) continue;
			MonsterHit?.Invoke(monster);
			if (!pierce)
			{
				IsFinished = true;
				return;
			}
		}
	}

	void OnDrawGizmos()
	{
		if (!launched) return;
		Gizmos.color = Color.cyan;
		Gizmos.matrix = Matrix4x4.TRS(transform.position, Quaternion.FromToRotation(Vector3.right, direction), Vector3.one);
		Gizmos.DrawWireCube(Vector3.zero, hitSize);
	}
}
