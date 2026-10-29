using UnityEngine;

/// <summary>
/// 테스트용. Inspector의 컴포넌트 메뉴(⋮)에서 몬스터에게 데미지를 주고, 피격·사망과 추적 대상을 로그로 확인한다.
/// 플레이어 스킬 판정이 생기기 전까지 "처치할 수 있다"를 확인하는 용도. PR 전에 지운다.
/// </summary>
[RequireComponent(typeof(Monster))]
public class MonsterTester : MonoBehaviour
{
	[SerializeField, Min(1)]
	int testDamage = 10;

	Monster monster;
	GroundChaseAI ai;
	PlayerHealth lastTarget;

	void Awake()
	{
		monster = GetComponent<Monster>();
		ai = GetComponent<GroundChaseAI>();
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

	void Update()
	{
		if (ai == null || ai.Target == lastTarget) return;
		lastTarget = ai.Target;
		Debug.Log(lastTarget != null ? $"[{name}] 발견 → 추적 시작: {lastTarget.name}" : $"[{name}] 놓침 → 순찰");
	}

	[ContextMenu("Deal Test Damage")]
	void DealTestDamage() => monster.TakeDamage(testDamage);

	void LogDamaged(int damage) => Debug.Log($"[{name}] {damage} 피해 → 체력 {monster.Hp}/{monster.MaxHp}");

	void LogDied() => Debug.Log($"[{name}] 처치");
}
