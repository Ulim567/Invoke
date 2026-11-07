using UnityEngine;

/// <summary>
/// 레벨에 따라 능력치를 올린다. 레벨 1보다 높은 만큼 레벨당 최대 체력·공격력을, Memory Level Interval 레벨마다 메모리 크기를 PlayerStats.AddBonus로 더한다.
/// <para>
/// - 레벨은 같은 오브젝트의 PlayerProgress(이우림)에서 읽기만 한다. PlayerProgress가 없으면(테스트용 플레이어) 아무것도 하지 않는다.<br/>
/// - 레벨업뿐 아니라 접속할 때 불러온 레벨(예: 5레벨)에도 똑같이 맞춘다. 이미 더한 레벨을 기억해 두고 차이만 더한다.<br/>
/// - 레벨이 바뀐 값을 가진 곳(서버, 호스트, 내 캐릭터의 클라이언트)에서 돈다.
/// </para>
/// <para>사용법: 플레이어 프리팹 최상위(PlayerStats, PlayerProgress와 같은 곳)에 붙인다.</para>
/// </summary>
[RequireComponent(typeof(PlayerStats))]
public class PlayerLevelStats : MonoBehaviour
{
	[SerializeField, Min(0), Tooltip("레벨 1당 더할 최대 체력 (임시)")]
	int maxHpPerLevel = 10;

	[SerializeField, Min(0), Tooltip("레벨 1당 더할 공격력 (임시)")]
	int attackPowerPerLevel = 2;

	/// <summary>레벨 1당 오르는 최대 체력 (레벨업 창의 「다음 레벨」 표시용, 임혜원 #332)</summary>
	public int MaxHpPerLevel => maxHpPerLevel;

	/// <summary>레벨 1당 오르는 공격력 (레벨업 창의 「다음 레벨」 표시용, 임혜원 #332)</summary>
	public int AttackPowerPerLevel => attackPowerPerLevel;

	[SerializeField, Min(1), Tooltip("이 레벨마다 메모리 크기를 Memory Per Step만큼 올린다. 3이면 Lv4, 7, 10, … 28 (Lv30에 +9) (임시)")]
	int memoryLevelInterval = 3;

	[SerializeField, Min(0), Tooltip("Memory Level Interval 레벨마다 더할 메모리 칸 수 (임시)")]
	int memoryPerStep = 1;

	/// <summary>
	/// level일 때 레벨로 더해지는 메모리 칸 수 (레벨 1이면 0). 레벨업 창에서 다음 레벨과 비교해 「+1」을 보여 줄 때 쓴다.
	/// </summary>
	public int MemoryBonusAt(int level) => Mathf.Max(0, level - 1) / memoryLevelInterval * memoryPerStep;

	PlayerStats stats;
	PlayerProgress progress;
	// 능력치에 이미 반영한 레벨. 처음은 레벨 1(추가 능력치 없음)
	int appliedLevel = 1;

	void Awake()
	{
		stats = GetComponent<PlayerStats>();
		progress = GetComponent<PlayerProgress>();
	}

	void OnEnable()
	{
		if (progress == null) return;
		progress.Changed += ApplyLevel;
		ApplyLevel();
	}

	void OnDisable()
	{
		if (progress != null) progress.Changed -= ApplyLevel;
	}

	// Changed는 돈·재료가 바뀔 때도 불리므로, 레벨이 달라졌을 때만 차이만큼 더한다 (값은 PlayerStats만 바꾼다, 코드 작성 규칙 2)
	void ApplyLevel()
	{
		int diff = progress.Level - appliedLevel;
		if (diff == 0) return;

		int memoryDiff = MemoryBonusAt(progress.Level) - MemoryBonusAt(appliedLevel);
		appliedLevel = progress.Level;
		stats.AddBonus(StatType.MaxHp, diff * maxHpPerLevel);
		stats.AddBonus(StatType.AttackPower, diff * attackPowerPerLevel);
		// 메모리는 오른 칸이 있을 때만 알린다 (Changed를 받는 메모리 게이지가 괜히 다시 그리지 않게)
		if (memoryDiff != 0) stats.AddBonus(StatType.MemorySize, memoryDiff);
	}
}
