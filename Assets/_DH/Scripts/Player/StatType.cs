/// <summary>
/// 플레이어 능력치 종류. PlayerStats.AddBonus, PlayerStats.Changed에서 어떤 능력치인지 가리킬 때 쓴다.
/// </summary>
public enum StatType
{
	/// <summary>최대 체력</summary>
	MaxHp,
	/// <summary>공격력</summary>
	AttackPower,
	/// <summary>공격 속도 (배율). 함수 동작 시간(틱 수)을 줄인다</summary>
	AttackSpeed,
	/// <summary>메모리 크기 (칸 수)</summary>
	MemorySize,
	/// <summary>쿨타임 감소 (0~1 비율)</summary>
	CooldownReduction,
	/// <summary>이동 속도</summary>
	MoveSpeed,
}
