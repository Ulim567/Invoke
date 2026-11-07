using Mirror;
using UnityEngine;

/// <summary>
/// 서버의 몬스터 체력과 피격·사망 연출을 클라이언트에 전하는 컴포넌트 (#310). Monster는 Mirror 없이 두고 네트워크는 여기에 모은다.
/// <para>
/// - 체력·최대 체력: [SyncVar]. 나중에 그 장소에 들어와 몬스터를 처음 본 클라이언트도 지금 값을 받는다. 최대 체력은 레이드 보스가 인원에 맞출 때 바뀐다.<br/>
/// - 피격·사망 연출: [ClientRpc]. 피격 알림에는 맞은 뒤 체력을 함께 담는다. 연출이 체력 값보다 먼저 도착해 옛 체력을 읽지 않게 하기 위함이다.<br/>
/// - 클라이언트에서는 Monster의 연출 이벤트(HitEffectPlayed, DeathEffectPlayed)만 불린다. 로직 이벤트(Damaged, Died)는 서버에서만 불린다.<br/>
/// - 호스트는 서버의 Monster가 이미 연출 이벤트를 불렀으므로 받은 알림을 무시한다. 네트워크를 켜지 않은 테스트에서는 아무것도 하지 않는다.
/// </para>
/// <para>
/// 사용법: NetworkIdentity가 있는 몬스터 프리팹 최상위에 Monster와 함께 붙인다 (Gateway, GatewayClone, Pillar, Worm, Leak, Loop).
/// 다른 스크립트는 이 컴포넌트를 부르지 않는다. 체력과 연출은 지금처럼 Monster에서 읽는다.
/// </para>
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterNetwork : NetworkBehaviour
{
	// 서버의 Monster.Hp. 클라이언트에서 바뀌면 Monster에 넣는다
	[SyncVar(hook = nameof(OnHpChanged))]
	int hp;

	// 서버의 Monster.MaxHp. 레이드 보스는 인원에 따라 바뀐다 (Monster.TrySetMaxHpRate)
	[SyncVar(hook = nameof(OnMaxHpChanged))]
	int maxHp;

	Monster monster;

	void Awake() => monster = GetComponent<Monster>();

	public override void OnStartServer()
	{
		hp = monster.Hp;
		maxHp = monster.MaxHp;
		monster.HitEffectPlayed += OnServerHit;
		monster.DeathEffectPlayed += OnServerDeath;
		monster.MaxHpChanged += OnServerMaxHpChanged;
	}

	public override void OnStopServer()
	{
		monster.HitEffectPlayed -= OnServerHit;
		monster.DeathEffectPlayed -= OnServerDeath;
		monster.MaxHpChanged -= OnServerMaxHpChanged;
	}

	public override void OnStartClient()
	{
		// 처음 받은 체력을 넣는다 (생성 때 함께 온 SyncVar 값). 호스트는 서버의 Monster를 같이 쓰므로 넣지 않는다
		// 최대 체력을 먼저 넣어야 체력이 옛 최대 체력에 잘리지 않는다
		if (isServer) return;
		if (maxHp > 0) monster.ApplyServerMaxHp(maxHp);
		monster.ApplyServerHp(hp);
	}

	void OnServerMaxHpChanged()
	{
		maxHp = monster.MaxHp;
		hp = monster.Hp;
	}

	// 피격 연출 이벤트는 체력이 바뀐 뒤에 불리므로 여기서 체력을 맞추고 알린다
	void OnServerHit(int damage)
	{
		hp = monster.Hp;
		RpcHit(damage, monster.Hp);
	}

	void OnServerDeath()
	{
		hp = monster.Hp;
		RpcDeath();
	}

	[ClientRpc]
	void RpcHit(int damage, int hpAfter)
	{
		if (isServer) return;
		monster.PlayHitFromServer(damage, hpAfter);
	}

	[ClientRpc]
	void RpcDeath()
	{
		if (isServer) return;
		monster.PlayDeathFromServer();
	}

	void OnHpChanged(int _, int newHp)
	{
		if (isServer) return;
		monster.ApplyServerHp(newHp);
	}

	// 체력보다 늦게 도착해도 맞도록, 최대 체력을 넣은 뒤 체력을 다시 넣는다
	void OnMaxHpChanged(int _, int newMaxHp)
	{
		if (isServer) return;
		monster.ApplyServerMaxHp(newMaxHp);
		monster.ApplyServerHp(hp);
	}
}
