using System;
using Mirror;
using UnityEngine;

/// <summary>
/// C 스킬의 연출용 event를 모아 둔 컴포넌트. 플레이어 프리팹에 붙인다 (#152).
/// <para>
/// - 스킬(DataDrivenSkill)이 값을 바꾼 뒤 이 event들을 부르고, 이펙트·소리·애니메이션(임혜원)은 이 event만 받아서 튼다 (코드 작성 규칙 1).<br/>
/// - 모든 event는 스킬의 고정 ID(PredefinedFunctionIds)를 같이 보낸다. 받는 쪽은 ID로 어떤 이펙트를 틀지 고른다.<br/>
/// - 순서: Started → (Fired → Hit 0번 이상) → Ended. 필요 조건(맨손 등)에 걸려 시작하지 못한 스킬은 아무 event도 내지 않는다.<br/>
/// - 네트워크 (#223, network.md 「경우별 처리 방법」 2번): 스킬은 서버에서만 실행된다. 서버에서 Raise를 부르면 그 자리(서버·Host)에서 event를 내고, [ClientRpc]로 다른 클라이언트에서도 같은 event를 낸다.<br/>
///   네트워크 없는 테스트에서는 그 자리에서만 낸다. Hit의 대상은 NetworkIdentity가 있는 오브젝트만 클라이언트에 전해지고, 없으면 클라이언트에서는 null이다 (맞은 위치는 그대로 옴).
/// </para>
/// </summary>
public class SkillEffectEvents : NetworkBehaviour
{
	/// <summary>[연출] 스킬이 시작했을 때 (스킬 ID). 시전 자세, 시전 소리</summary>
	public event Action<string> Started;

	/// <summary>[연출] 판정이 나간 순간 (스킬 ID, 시작 위치, 방향). 찌르기 섬광, 휘두르기 궤적. 아무도 못 맞혀도(빗나감) 불린다</summary>
	public event Action<string, Vector2, Vector2> Fired;

	/// <summary>[연출] 대상이 맞은 순간 (스킬 ID, 맞은 대상, 맞은 위치, 데미지 결과). 맞은 이펙트, 치명타·처형 연출. 관통처럼 여러 명이면 대상마다 불린다</summary>
	public event Action<string, GameObject, Vector2, DamageResult> Hit;

	/// <summary>[연출] 스킬이 끝났을 때 (스킬 ID, 끝까지 실행했으면 true / 끊겼거나 실패했으면 false). 자세 풀기</summary>
	public event Action<string, bool> Ended;

	// Host는 서버와 클라이언트가 한 프로그램이라, 서버에서 바로 낸 event를 Rpc로 한 번 더 내지 않는다
	// 서버에서 바로 내는 이유: Host의 테스트와 이펙트가 지금처럼 같은 틱에 event를 받게 하기 위함

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseStarted(string skillId)
	{
		Started?.Invoke(skillId);
		if (isServer) RpcStarted(skillId);
	}

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseFired(string skillId, Vector2 origin, Vector2 direction)
	{
		Fired?.Invoke(skillId, origin, direction);
		if (isServer) RpcFired(skillId, origin, direction);
	}

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseHit(string skillId, GameObject target, Vector2 point, DamageResult result)
	{
		Hit?.Invoke(skillId, target, point, result);
		if (!isServer) return;
		// 네트워크 오브젝트가 아닌 대상(NetworkIdentity 없음)은 보낼 수 없어서 null로 보낸다
		NetworkIdentity identity = target != null ? target.GetComponent<NetworkIdentity>() : null;
		// DamageResult는 readonly struct라 Mirror가 그대로 보내지 못해 값을 풀어서 보낸다
		RpcHit(skillId, identity, point, result.Damage, result.IsCritical, result.IsExecution);
	}

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseEnded(string skillId, bool completed)
	{
		Ended?.Invoke(skillId, completed);
		if (isServer) RpcEnded(skillId, completed);
	}

	[ClientRpc]
	void RpcStarted(string skillId)
	{
		if (isServer) return;
		Started?.Invoke(skillId);
	}

	[ClientRpc]
	void RpcFired(string skillId, Vector2 origin, Vector2 direction)
	{
		if (isServer) return;
		Fired?.Invoke(skillId, origin, direction);
	}

	[ClientRpc]
	void RpcHit(string skillId, NetworkIdentity target, Vector2 point, int damage, bool isCritical, bool isExecution)
	{
		if (isServer) return;
		Hit?.Invoke(skillId, target != null ? target.gameObject : null, point, new DamageResult(damage, isCritical, isExecution));
	}

	[ClientRpc]
	void RpcEnded(string skillId, bool completed)
	{
		if (isServer) return;
		Ended?.Invoke(skillId, completed);
	}
}
