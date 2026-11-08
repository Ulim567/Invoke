using System;
using UnityEngine;

/// <summary>
/// 모든 몬스터(잡몹, 보스)의 체력·피격·사망을 맡는 컴포넌트.<br/>
/// 체력은 이 컴포넌트만 바꾼다. 다른 스크립트는 TakeDamage를 부르고, Hp·MaxHp는 읽기만 한다.
/// <para>
/// 사용법<br/>
/// - 몬스터 프리팹 최상위 오브젝트에 붙인다. Layer는 Monster로 둔다.<br/>
/// - 체력이 0이 되면 Died를 알리고, despawnDelay초 뒤에 오브젝트를 없앤다.
/// </para>
/// <para>
/// 이벤트는 쓰임에 따라 둘로 나뉜다. 네트워크를 붙이면 실행되는 곳이 다르기 때문이다.<br/>
/// - 게임 로직(드롭, 스포너, 보스 페이즈, 레이드 판정): Damaged, Died. 서버에서만 불린다.<br/>
/// - 연출(이펙트, 소리, 애니메이션, 데미지 숫자): HitEffectPlayed, DeathEffectPlayed. 각 플레이어 화면에서 불린다.
/// </para>
/// <para>
/// 네트워크 (#310): 이 컴포넌트는 Mirror 없이 둔다. 네트워크 몬스터 프리팹에는 MonsterNetwork를 함께 붙여, 서버의 체력과 연출을 클라이언트에 전한다.
/// 클라이언트에서는 MonsterNetwork가 ApplyServerHp, PlayHitFromServer, PlayDeathFromServer를 부른다.
/// </para>
/// </summary>
public class Monster : MonoBehaviour, IDamageable
{
	[SerializeField, Min(1), Tooltip("최대 체력")]
	int maxHp = 100;

	[SerializeField, Min(0f), Tooltip("사망 후 오브젝트를 없애기까지 기다리는 시간(초). 사망 연출이 보일 시간")]
	float despawnDelay = 0.5f;

	[SerializeField, Tooltip("보스면 체크한다. 처형(체력 2% 이하 즉사) 같은 보스 제외 규칙이 이 값을 본다")]
	bool isBoss;

	[SerializeField, Tooltip("체크하면 맞아도 체력이 줄지 않고 죽지 않는다. Damaged·HitEffectPlayed는 그대로 불린다 (연습장 허수아비)")]
	bool invulnerable;

	/// <summary>
	/// 보스면 true (Inspector 값). 처형처럼 보스를 빼야 하는 규칙에서 읽는다.
	/// </summary>
	public bool IsBoss => isBoss;

	/// <summary>
	/// 체력이 줄지 않는 몬스터면 true (Inspector 값). 연습장 허수아비처럼 맞기만 하고 죽지 않는다.
	/// </summary>
	public bool IsInvulnerable => invulnerable;

	/// <summary>
	/// 최대 체력.
	/// </summary>
	public int MaxHp => maxHp;

	/// <summary>
	/// 지금 체력. 0이면 죽은 것이다.
	/// </summary>
	public int Hp { get; private set; }

	/// <summary>
	/// 죽었으면 true. 죽은 뒤에는 데미지를 받지 않는다.
	/// </summary>
	public bool IsDead => Hp <= 0;

	/// <summary>
	/// [로직] 데미지를 받았을 때 불린다. 받은 데미지를 넘긴다. 보스 페이즈 전환처럼 체력에 따라 바뀌는 로직에 쓴다.
	/// Invulnerable이면 체력은 그대로지만 똑같이 불린다.
	/// </summary>
	public event Action<int> Damaged;

	/// <summary>
	/// [로직] 체력이 0이 된 순간 한 번 불린다. 드롭, 스포너 집계, 레이드 판정에 쓴다.
	/// </summary>
	public event Action Died;

	/// <summary>
	/// [연출] 피격 연출을 할 때 불린다. 받은 데미지를 넘긴다. 이펙트, 소리, 데미지 숫자 표시에 쓴다.
	/// </summary>
	public event Action<int> HitEffectPlayed;

	/// <summary>
	/// [연출] 사망 연출을 할 때 한 번 불린다. 이펙트, 소리, 사망 애니메이션에 쓴다.
	/// </summary>
	public event Action DeathEffectPlayed;

	/// <summary>
	/// [로직] 최대 체력이 바뀐 순간 불린다 (TrySetMaxHpRate). 서버에서만. MonsterNetwork가 클라이언트에 전한다.
	/// </summary>
	public event Action MaxHpChanged;

	/// <summary>
	/// [연출] 몬스터가 이 화면에 나타난 순간(Start). 모든 화면(서버·호스트·클라이언트)에서 불린다. 소리(SfxPlayer)가 몬스터마다 연출 이벤트를 붙일 때 쓴다 (#436).
	/// </summary>
	public static event Action<Monster> Appeared;

	// 사망 연출을 이미 했으면 true. 클라이언트에서 서버의 사망 알림이 늦게 와도 한 번만 부르기 위함
	bool deathPlayed;
	// Inspector의 최대 체력. 인원에 따라 바꿀 때 이 값에 비율을 곱한다
	int baseMaxHp;

	void Start() => Appeared?.Invoke(this);

	void Awake()
	{
		baseMaxHp = maxHp;
		Hp = maxHp;
	}

	/// <summary>
	/// [서버] 최대 체력을 Inspector 값의 rate배로 바꾸고 체력을 가득 채운다. 레이드 보스를 인원에 맞출 때 RaidJudge가 부른다.<br/>
	/// 한 번도 맞지 않아 체력이 가득 찬 동안만 바꾼다 (싸움 중에 체력이 갑자기 늘거나 줄지 않게). 바꿨거나 이미 그 값이면 true.
	/// </summary>
	public bool TrySetMaxHpRate(float rate)
	{
		if (IsDead || Hp != maxHp) return false;
		int value = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * rate));
		if (value == maxHp) return true;

		maxHp = value;
		Hp = value;
		MaxHpChanged?.Invoke();
		return true;
	}

	/// <summary>
	/// [클라이언트] 서버가 보낸 최대 체력을 넣는다. MonsterNetwork만 부른다.
	/// </summary>
	public void ApplyServerMaxHp(int value) => maxHp = Mathf.Max(1, value);

	/// <summary>
	/// damage만큼 체력을 줄인다. 0 이하이거나 이미 죽었으면 무시한다. 체력이 0이 되면 사망한다.
	/// Invulnerable이면 체력은 그대로 두고 Damaged·HitEffectPlayed만 부른다.
	/// </summary>
	public void TakeDamage(int damage)
	{
		if (damage <= 0 || IsDead) return;

		// 허수아비는 체력을 그대로 두지만, 데미지 숫자를 띄울 수 있게 이벤트는 똑같이 부른다
		if (!invulnerable) Hp = Mathf.Max(Hp - damage, 0);
		Damaged?.Invoke(damage);
		PlayHitEffect(damage);

		if (Hp == 0) Die();
	}

	void Die()
	{
		Died?.Invoke();
		PlayDeathEffect();
		Despawn();
	}

	/// <summary>
	/// [클라이언트] 서버가 보낸 체력을 넣는다. MonsterNetwork만 부른다. 로직 이벤트(Damaged, Died)는 부르지 않는다.
	/// </summary>
	public void ApplyServerHp(int hp) => Hp = Mathf.Clamp(hp, 0, maxHp);

	/// <summary>
	/// [클라이언트] 서버에서 맞았다는 알림을 받았을 때 MonsterNetwork가 부른다. 맞은 뒤 체력을 넣고 HitEffectPlayed만 부른다.
	/// 체력을 먼저 넣는 이유: 연출 쪽(기둥 금 가는 그림 등)이 이벤트 안에서 Hp를 읽기 때문이다.
	/// </summary>
	public void PlayHitFromServer(int damage, int hpAfter)
	{
		ApplyServerHp(hpAfter);
		PlayHitEffect(damage);
	}

	/// <summary>
	/// [클라이언트] 서버에서 죽었다는 알림을 받았을 때 MonsterNetwork가 부른다. 체력을 0으로 두고 DeathEffectPlayed만 부른다.
	/// 오브젝트는 서버가 없애면 Mirror가 클라이언트에서도 없앤다.
	/// </summary>
	public void PlayDeathFromServer()
	{
		ApplyServerHp(0);
		PlayDeathEffect();
	}

	// 아래 두 함수는 연출만 한다. 클라이언트에는 MonsterNetwork가 [ClientRpc]로 전한다 (코드 작성 규칙 1)
	// 로직 이벤트(Damaged, Died)를 여기서 부르면 클라이언트에서도 드롭·스폰이 돌므로 따로 둔다
	void PlayHitEffect(int damage) => HitEffectPlayed?.Invoke(damage);

	void PlayDeathEffect()
	{
		if (deathPlayed) return;
		deathPlayed = true;
		DeathEffectPlayed?.Invoke();
	}

	// 몬스터를 없애는 곳은 여기 하나다 (코드 작성 규칙 3). 사망 연출이 보일 시간을 기다린 뒤 NetworkSpawner로 없앤다.
	// 서버에서 없애면 Mirror가 모든 클라이언트에서도 없앤다. 네트워크를 켜지 않은 테스트에서는 Destroy와 같다
	void Despawn() => Invoke(nameof(DespawnNow), despawnDelay);

	void DespawnNow() => NetworkSpawner.Despawn(gameObject);
}
