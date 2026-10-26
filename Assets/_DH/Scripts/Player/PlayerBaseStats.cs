using UnityEngine;

/// <summary>
/// 캐릭터 하나의 기본 능력치를 담는 데이터 파일. 캐릭터(C, Java, Python)마다 파일을 하나씩 만든다.
/// <para>
/// 사용법<br/>
/// - Project 창에서 우클릭 → Create → Invoke → Player Base Stats로 만든다.<br/>
/// - PlayerStats의 Base Stats 칸에 넣는다.<br/>
/// - 게임 중에는 이 파일의 값을 바꾸지 않는다. 같은 파일을 쓰는 모든 플레이어가 함께 바뀐다. 레벨·강화로 오르는 값은 PlayerStats.AddBonus로 더한다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "BaseStats", menuName = "Invoke/Player Base Stats")]
public class PlayerBaseStats : ScriptableObject
{
	[SerializeField, Min(1), Tooltip("최대 체력")]
	int maxHp = 100;

	[SerializeField, Min(0), Tooltip("공격력. 데미지 = 공격력 × 배율")]
	int attackPower = 10;

	[SerializeField, Min(0.1f), Tooltip("공격 속도 배율. 1.25면 20틱 함수가 16틱이 된다")]
	float attackSpeed = 1f;

	[SerializeField, Min(0), Tooltip("메모리 전체 칸 수")]
	int memorySize = 10;

	[SerializeField, Range(0f, 1f), Tooltip("쿨타임 감소 비율. 0.2면 쿨타임이 20% 줄어든다")]
	float cooldownReduction;

	[SerializeField, Min(0f), Tooltip("이동 속도 (초당 유닛)")]
	float moveSpeed = 5f;

	public int MaxHp => maxHp;
	public int AttackPower => attackPower;
	public float AttackSpeed => attackSpeed;
	public int MemorySize => memorySize;
	public float CooldownReduction => cooldownReduction;
	public float MoveSpeed => moveSpeed;
}
