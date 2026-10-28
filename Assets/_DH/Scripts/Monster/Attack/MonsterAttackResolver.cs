using UnityEngine;

/// <summary>
/// 몬스터 공격이 맞았을 때 효과를 실제로 적용하는 곳. 모든 공격 형태가 이 함수 하나를 거친다.
/// <para>
/// - 체력 피해는 데미지 계산(박진우, DamageCalculator)을 거쳐 나온 정수로 맞은 대상의 IDamageable.TakeDamage를 부른다.<br/>
///   몬스터는 공격력 능력치가 없으므로 Amount를 그대로 넘기고(DamageRequest.Flat), 몬스터 공격은 처형하지 않으므로 대상 정보는 넘기지 않는다.<br/>
/// - 메모리 점유·탈취, 회수 방해, 실행 중단은 받을 시스템이 아직 없어서 로그만 남긴다. 시스템이 생기면 여기만 바꾼다.
/// </para>
/// <para>
/// 네트워크를 붙이면 이 함수는 서버에서만 부른다 (공격 판정은 서버 한 곳에서, 코드 작성 규칙 1·2).
/// </para>
/// </summary>
public static class MonsterAttackResolver
{
	/// <summary>
	/// attack의 효과를 hit(맞은 Collider)의 주인에게 차례로 적용한다.
	/// </summary>
	public static void Apply(MonsterAttackData attack, Collider2D hit)
	{
		if (attack == null || hit == null) return;

		var effects = attack.Effects;
		for (int i = 0; i < effects.Count; i++)
			ApplyEffect(effects[i], hit);
	}

	static void ApplyEffect(in AttackEffect effect, Collider2D hit)
	{
		switch (effect.Type)
		{
			case AttackEffectType.HealthDamage:
				// Collider가 자식에 있어도 찾도록 부모까지 올라가며 찾는다. 맞을 대상이 없으면 계산도 하지 않는다
				IDamageable target = hit.GetComponentInParent<IDamageable>();
				if (target == null) break;
				target.TakeDamage(DamageCalculator.Calculate(DamageRequest.Flat(effect.Amount)).Damage);
				break;

			default:
				// 받을 시스템(메모리, 함수 실행기)이 생기기 전까지는 로그로 확인한다
				Debug.Log($"[몬스터 공격] {hit.transform.root.name}: {effect.Type} {effect.Amount} ({effect.Duration}초) — 받는 시스템이 아직 없음");
				break;
		}
	}
}
