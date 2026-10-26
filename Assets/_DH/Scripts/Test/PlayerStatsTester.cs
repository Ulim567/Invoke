using UnityEngine;

/// <summary>
/// 테스트용. 시작할 때 능력치를 로그로 보여 주고, Inspector의 컴포넌트 메뉴(⋮)에서 능력치를 올려 Changed 이벤트를 확인한다.
/// PR 전에 지운다.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerStatsTester : MonoBehaviour
{
	PlayerStats stats;

	void Awake()
	{
		stats = GetComponent<PlayerStats>();
	}

	void OnEnable() => stats.Changed += LogChanged;

	void OnDisable() => stats.Changed -= LogChanged;

	void Start() => LogAll();

	[ContextMenu("Add Attack Power +5")]
	void AddAttackPower() => stats.AddBonus(StatType.AttackPower, 5);

	[ContextMenu("Add Attack Speed +0.25")]
	void AddAttackSpeed() => stats.AddBonus(StatType.AttackSpeed, 0.25f);

	[ContextMenu("Add Cooldown Reduction +0.2")]
	void AddCooldownReduction() => stats.AddBonus(StatType.CooldownReduction, 0.2f);

	[ContextMenu("Log All Stats")]
	void LogAll()
	{
		Debug.Log($"[{name}] 체력 {stats.MaxHp}, 공격력 {stats.AttackPower}, 공격 속도 {stats.AttackSpeed}, " +
			$"메모리 {stats.MemorySize}, 쿨타임 감소 {stats.CooldownReduction:P0}, 이동 속도 {stats.MoveSpeed} " +
			$"/ 20틱 함수 → {stats.ApplyAttackSpeed(20)}틱, 쿨타임 10초 → {stats.ApplyCooldownReduction(10f):F1}초");
	}

	void LogChanged(StatType type)
	{
		Debug.Log($"[{name}] {type} 변경됨");
		LogAll();
	}
}
