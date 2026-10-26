using UnityEngine;

/// <summary>
/// 테스트용. Inspector의 컴포넌트 메뉴(⋮)에서 몬스터에게 데미지를 주고, 피격·사망 이벤트를 로그로 확인한다.
/// PR 전에 지운다.
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterTester : MonoBehaviour
{
	[SerializeField, Min(1)]
	int testDamage = 30;

	Monster monster;

	void Awake()
	{
		monster = GetComponent<Monster>();
	}

	void OnEnable()
	{
		monster.Damaged += LogDamaged;
		monster.Died += LogDied;
	}

	void OnDisable()
	{
		monster.Damaged -= LogDamaged;
		monster.Died -= LogDied;
	}

	[ContextMenu("Deal Test Damage")]
	void DealTestDamage() => monster.TakeDamage(testDamage);

	void LogDamaged(int damage) => Debug.Log($"[{name}] {damage} 피해 → 체력 {monster.Hp}/{monster.MaxHp}");

	void LogDied() => Debug.Log($"[{name}] 사망");
}
