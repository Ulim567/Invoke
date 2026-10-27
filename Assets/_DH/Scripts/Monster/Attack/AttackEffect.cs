using System;
using UnityEngine;

/// <summary>
/// 몬스터(버그) 공격의 효과 종류. (버그 0차)
/// </summary>
public enum AttackEffectType
{
	/// <summary>체력 피해. Amount만큼 체력을 줄인다</summary>
	HealthDamage,
	/// <summary>메모리 점유. 맞은 플레이어의 힙을 원치 않는 객체가 Amount칸 차지한다</summary>
	MemoryOccupy,
	/// <summary>메모리 탈취. 쓸 수 있는 남은 메모리가 Amount칸 줄어든다</summary>
	MemorySteal,
	/// <summary>회수 방해. Duration초 동안 힙이 회수되지 않는다</summary>
	ReclaimBlock,
	/// <summary>실행 중단. 실행 중인 함수가 끊긴다</summary>
	Interrupt,
}

/// <summary>
/// 공격 효과 하나. 종류, 세기(Amount), 지속 시간(Duration)으로 이루어진다.<br/>
/// 쓰지 않는 값은 0으로 둔다. 예: 체력 피해는 Duration을 쓰지 않는다.
/// </summary>
[Serializable]
public struct AttackEffect
{
	[SerializeField, Tooltip("효과 종류")]
	AttackEffectType type;

	[SerializeField, Min(0), Tooltip("세기. 체력 피해는 데미지, 메모리 효과는 칸 수")]
	int amount;

	[SerializeField, Min(0f), Tooltip("지속 시간(초). 효과가 계속되는 시간")]
	float duration;

	public AttackEffectType Type => type;
	public int Amount => amount;
	public float Duration => duration;
}
