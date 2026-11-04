using System;
using Mirror;
using UnityEngine;

/// <summary>
/// 패링 판정 (#245). 플레이어 프리팹 최상위에 붙인다. 패링 스킬(CParry)이 판정 시간을 열고, 몬스터 투사체가 플레이어에 닿을 때 TryParry로 물어본다.
/// <para>
/// 사용법<br/>
/// - 패링 스킬이 Open(초)으로 판정 시간을 연다. 그동안 패링 가능한 투사체(MonsterAttackData.Parryable)가 닿으면 잡는다.<br/>
/// - 몬스터 투사체(MonsterProjectile, 김도훈)는 플레이어에 공격 효과를 주기 전에 TryParry(this)를 부른다. true면 효과를 주지 않고 투사체를 없앤다.<br/>
/// - 한 번 잡으면 판정 시간이 닫힌다 (1회). 잡으면 Parried로 스킬에 알리고, ParryEffectPlayed로 모든 화면에 연출을 알린다.<br/>
/// - 판정은 서버에서만 한다 (스킬과 투사체 충돌이 서버에서 일어남).
/// </para>
/// </summary>
public class ParryGuard : NetworkBehaviour
{
	float openUntil = -1f;

	/// <summary>지금 패링 판정 시간이면 true</summary>
	public bool IsOpen => Time.time < openUntil;

	/// <summary>[로직] 투사체를 잡았을 때 (잡은 위치, 투사체가 날아온 쪽 방향 = 되돌려 보낼 방향). 패링 스킬이 반격한다. 서버에서만 불린다</summary>
	public event Action<Vector2, Vector2> Parried;

	/// <summary>[연출] 투사체를 잡았을 때 (잡은 위치). 모든 화면에서 불린다. 패링 이펙트·소리에 쓴다</summary>
	public event Action<Vector2> ParryEffectPlayed;

	/// <summary>seconds초 동안 패링 판정 시간을 연다. 패링 스킬이 시작할 때 부른다.</summary>
	public void Open(float seconds) => openUntil = Time.time + Mathf.Max(0f, seconds);

	/// <summary>판정 시간을 바로 닫는다. 패링 스킬이 끝나거나 끊길 때 부른다.</summary>
	public void Close() => openUntil = -1f;

	/// <summary>
	/// 몬스터 투사체가 이 플레이어에 닿았을 때 부른다 (MonsterProjectile, 김도훈). 잡았으면 true: 공격 효과를 주지 말고 투사체를 없앤다.<br/>
	/// 판정 시간이 아니거나, 패링할 수 없는 공격(Parryable이 꺼짐)이거나, 빗나가는 투사체(Misses)면 false.
	/// </summary>
	public bool TryParry(MonsterProjectile projectile)
	{
		if (projectile == null || !IsOpen) return false;
		if (projectile.Attack == null || !projectile.Attack.Parryable || projectile.Misses) return false;

		Close();
		Vector2 point = projectile.transform.position;
		// 날아온 쪽으로 되돌려 보낸다 (쏜 몬스터 쪽). 속도를 모르면 플레이어에서 투사체 쪽
		Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
		Vector2 back = body != null && body.linearVelocity.sqrMagnitude > 0.0001f
			? -body.linearVelocity.normalized
			: (point - (Vector2)transform.position).normalized;
		Parried?.Invoke(point, back);
		PlayParryEffect(point);
		return true;
	}

	// 연출은 모든 화면에서 (network.md 경우 2). 네트워크 없는 테스트에서는 그 자리에서
	void PlayParryEffect(Vector2 point)
	{
		ParryEffectPlayed?.Invoke(point);
		if (isServer) RpcParryEffect(point);
	}

	[ClientRpc]
	void RpcParryEffect(Vector2 point)
	{
		if (isServer) return;
		ParryEffectPlayed?.Invoke(point);
	}
}
