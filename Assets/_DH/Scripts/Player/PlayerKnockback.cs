using Mirror;
using UnityEngine;

/// <summary>
/// 플레이어가 피해를 받으면 때린 쪽 반대로 살짝 밀리게 하는 컴포넌트.
/// <para>
/// - 서버: PlayerHealth.Damaged(실제로 체력이 준 피격. 무적·보호막으로 막힌 피해는 안 불림)를 받아 방향을 정한다.<br/>
///   방향은 때린 쪽(SetHitSource로 받은 위치) 반대, 모르면 바라보는 반대쪽. 죽는 공격과 자해(TakeSelfDamage, 경직 없음)는 밀지 않는다.<br/>
/// - 소유 클라이언트: 플레이어 위치는 소유 클라이언트가 정하므로, 서버가 [TargetRpc]로 방향을 보내고 그쪽에서 민다. 호스트의 내 캐릭터와 네트워크 없는 테스트는 그 자리에서 민다.<br/>
/// - 밀기: knockbackTime초(경직 시간보다 길면 경직 시간) 동안 Rigidbody2D의 가로 속도를 knockbackSpeed로 둔다. 밀리기 시작할 때 위로 knockbackUpSpeed만큼 살짝 띄운다 (이미 그보다 빨리 오르는 중이면 그대로). 벽에 막히면 물리에 막혀 멈춘다.<br/>
/// - 죽었거나 대시·돌진 중이거나 움직임이 잠겨 있으면(PlayerMovement) 밀지 않는다.
/// </para>
/// <para>
/// 사용법: 공용 Player 프리팹 최상위에 PlayerHealth와 함께 붙인다 (변형 프리팹은 물려받음). 때리는 쪽(ContactDamage, MonsterAttackResolver)은 피해를 주기 직전에 NotifySource를 부른다.
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerHealth), typeof(Rigidbody2D))]
public class PlayerKnockback : NetworkBehaviour
{
	[SerializeField, Min(0f), Tooltip("밀리는 가로 속도 (초당 유닛). 0이면 밀리지 않는다")]
	float knockbackSpeed = 5f;

	[SerializeField, Min(0f), Tooltip("밀리는 시간(초). 경직 시간(PlayerHealth Hit Stun Time)보다 길면 경직 시간만큼만")]
	float knockbackTime = 0.15f;

	[SerializeField, Min(0f), Tooltip("밀리기 시작할 때 위로 띄우는 속도 (초당 유닛). 0이면 뜨지 않고 옆으로만 밀린다")]
	float knockbackUpSpeed = 4f;

	PlayerHealth health;
	PlayerMovement movement;
	Rigidbody2D body;

	// [서버] 때린 쪽 X와 알린 프레임. 같은 프레임의 피해에만 쓴다 (막힌 피해의 위치가 다음 피해에 남지 않게)
	float sourceX;
	int sourceFrame = -1;
	// [서버] 다음에 밀 수 있는 시각. 피격 뒤 무적 동안 오는 Damaged는 자해뿐이라 밀지 않는다
	float nextKnockTime;
	// [소유 클라이언트] 밀리는 방향과 끝나는 시각 (0이면 밀지 않는 중)
	int pushDir;
	float pushEndTime;
	// [소유 클라이언트] 이번 밀기에서 아직 위로 띄우지 않았으면 true. 다음 ApplyPush에서 한 번만 띄운다
	bool hopPending;

	void Awake()
	{
		health = GetComponent<PlayerHealth>();
		movement = GetComponent<PlayerMovement>();
		body = GetComponent<Rigidbody2D>();
	}

	void OnEnable() => health.Damaged += OnDamaged;

	void OnDisable() => health.Damaged -= OnDamaged;

	/// <summary>
	/// [서버] 피해를 주기 직전에 때린 쪽 위치를 알린다. 바로 다음 TakeDamage(같은 프레임)에만 쓴다.
	/// </summary>
	public void SetHitSource(Vector2 position)
	{
		sourceX = position.x;
		sourceFrame = Time.frameCount;
	}

	/// <summary>
	/// [서버] hit(맞은 Collider 등)의 주인이 플레이어면 때린 쪽 위치를 알린다. 플레이어가 아니면 아무것도 하지 않는다.
	/// ContactDamage, MonsterAttackResolver가 TakeDamage 직전에 부른다.
	/// </summary>
	public static void NotifySource(Component hit, Vector2 position)
	{
		if (hit == null) return;
		PlayerKnockback knockback = hit.GetComponentInParent<PlayerKnockback>();
		if (knockback != null) knockback.SetHitSource(position);
	}

	// [서버] 실제 피격. 방향을 정해 소유 클라이언트에서 밀게 한다
	void OnDamaged(int damage)
	{
		bool hasSource = sourceFrame == Time.frameCount;
		sourceFrame = -1;

		// 죽는 공격(Hp 0), 자해(경직을 걸지 않음), 무적 중의 자해는 밀지 않는다
		float now = Time.time;
		if (knockbackSpeed <= 0f || knockbackTime <= 0f || health.Hp <= 0 || !health.IsStunned || now < nextKnockTime) return;
		// 다음 피격은 경직+무적이 끝난 뒤에야 오므로, 그 전의 Damaged는 무시한다 (조금 일찍 열어 시각 오차를 피함)
		nextKnockTime = now + health.HitStunTime + health.InvincibleTime - 0.05f;

		int dir = movement != null ? -movement.Facing : -1;
		if (hasSource)
		{
			float dx = body.position.x - sourceX;
			if (Mathf.Abs(dx) > 0.01f) dir = dx > 0f ? 1 : -1;
		}

		if (!NetworkServer.active || isLocalPlayer) Push(dir);
		else if (connectionToClient != null) TargetPush(dir);
	}

	// [소유 클라이언트] 서버가 정한 방향으로 민다
	[TargetRpc]
	void TargetPush(int dir) => Push(dir);

	// 대시·돌진 중이면 그 움직임을 지킨다. 서버의 피격 알림(RpcHit)보다 먼저 와서 대시가 아직 끝나지 않은 때 본다
	void Push(int dir)
	{
		if (health.IsDead) return;
		if (movement != null && (movement.IsDashing || movement.IsLunging || movement.IsMovementLocked)) return;
		pushDir = dir;
		hopPending = knockbackUpSpeed > 0f;
		pushEndTime = Time.time + Mathf.Min(knockbackTime, health.HitStunTime);
	}

	// PlayerMovement는 경직 중 Update에서 가로 속도를 0으로 두므로, 그 뒤(LateUpdate)에 밀기 속도를 다시 넣어 다음 물리 단계까지 남긴다.
	// 서버(호스트)는 InvokeNetworkManager.FixedUpdate가 장소 씬 물리를 직접 돌리는데, 그 FixedUpdate가 이 FixedUpdate보다 먼저 불리면
	// FixedUpdate에서 넣은 속도는 Update에서 0이 된 뒤에야 쓰이므로 밀리지 않는다. 그래서 LateUpdate에서 넣는 것이 핵심이다
	void LateUpdate() => ApplyPush();

	// 물리가 한 프레임에 여러 번 돌 때, 그 사이에 충돌 등으로 바뀐 속도도 다시 맞춘다
	void FixedUpdate() => ApplyPush();

	void ApplyPush()
	{
		if (pushEndTime <= 0f) return;
		if (Time.time >= pushEndTime || health.IsDead)
		{
			pushEndTime = 0f;
			return;
		}
		float vy = body.linearVelocity.y;
		// 처음 한 번만 띄운다. 그 뒤로는 중력에 맡겨 포물선으로 떨어진다
		if (hopPending)
		{
			hopPending = false;
			vy = Mathf.Max(vy, knockbackUpSpeed);
		}
		body.linearVelocity = new Vector2(pushDir * knockbackSpeed, vy);
	}
}
