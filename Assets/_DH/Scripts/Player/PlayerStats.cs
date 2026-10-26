using System;
using UnityEngine;

/// <summary>
/// 플레이어 능력치 6종(최대 체력, 공격력, 공격 속도, 메모리 크기, 쿨타임 감소, 이동 속도)을 한 곳에서 관리하는 컴포넌트.<br/>
/// 최종 능력치 = 기본 능력치(PlayerBaseStats 파일) + 추가 능력치(레벨·강화 등으로 더한 값).
/// <para>
/// 사용법<br/>
/// - 플레이어 프리팹 최상위 오브젝트에 붙이고, Base Stats 칸에 캐릭터의 PlayerBaseStats 파일을 넣는다.<br/>
/// - 다른 시스템은 MaxHp, AttackPower 같은 값을 읽기만 한다. 값은 AddBonus로만 바꾼다.<br/>
/// - 값이 바뀌면 Changed 이벤트로 어떤 능력치가 바뀌었는지 알린다. 값을 미리 저장해 둔 시스템은 이때 다시 읽는다.
/// </para>
/// <para>
/// 현재 체력과 피격은 이 컴포넌트가 하지 않는다. 최대 체력만 알려 준다.
/// </para>
/// </summary>
public class PlayerStats : MonoBehaviour
{
	[SerializeField, Tooltip("이 캐릭터의 기본 능력치 파일")]
	PlayerBaseStats baseStats;

	// 능력치별 추가 값. 네트워크를 붙이면 변수마다 [SyncVar]를 단다 (배열은 SyncVar로 동기화할 수 없어 따로 둔다)
	float maxHpBonus;
	float attackPowerBonus;
	float attackSpeedBonus;
	float memorySizeBonus;
	float cooldownReductionBonus;
	float moveSpeedBonus;

	/// <summary>
	/// 능력치가 바뀌었을 때 불린다. 바뀐 능력치 종류를 넘긴다.
	/// </summary>
	public event Action<StatType> Changed;

	/// <summary>최대 체력. 1 이상이다.</summary>
	public int MaxHp => Mathf.Max(1, baseStats.MaxHp + Mathf.RoundToInt(maxHpBonus));

	/// <summary>공격력. 데미지 계산에서 공격력 × 배율로 쓴다. 0 이상이다.</summary>
	public int AttackPower => Mathf.Max(0, baseStats.AttackPower + Mathf.RoundToInt(attackPowerBonus));

	/// <summary>공격 속도 배율. 1이면 원래 속도, 클수록 함수 동작 시간이 짧아진다. 0.1 이상이다.</summary>
	public float AttackSpeed => Mathf.Max(0.1f, baseStats.AttackSpeed + attackSpeedBonus);

	/// <summary>메모리 전체 칸 수. 0 이상이다.</summary>
	public int MemorySize => Mathf.Max(0, baseStats.MemorySize + Mathf.RoundToInt(memorySizeBonus));

	/// <summary>쿨타임 감소 비율. 0~1 사이이며, 0.2면 쿨타임이 20% 줄어든다.</summary>
	public float CooldownReduction => Mathf.Clamp01(baseStats.CooldownReduction + cooldownReductionBonus);

	/// <summary>이동 속도 (초당 유닛). 0 이상이다.</summary>
	public float MoveSpeed => Mathf.Max(0f, baseStats.MoveSpeed + moveSpeedBonus);

	/// <summary>
	/// 공격 속도를 적용한 함수 동작 시간(틱 수)을 돌려준다. 최소 1틱이다.<br/>
	/// 예: 공격 속도 1.25에서 20틱 함수 → 16틱
	/// </summary>
	public int ApplyAttackSpeed(int tickCount) => Mathf.Max(1, Mathf.RoundToInt(tickCount / AttackSpeed));

	/// <summary>
	/// 쿨타임 감소를 적용한 쿨타임을 돌려준다. 단위는 넘긴 값과 같다(초 또는 틱).
	/// </summary>
	public float ApplyCooldownReduction(float cooldown) => cooldown * (1f - CooldownReduction);

	/// <summary>
	/// type 능력치에 amount를 더한다. 빼려면 음수를 넘긴다. 레벨업, 강화, 버프에서 쓴다.<br/>
	/// 정수 능력치(최대 체력, 공격력, 메모리 크기)는 읽을 때 반올림한다.
	/// </summary>
	public void AddBonus(StatType type, float amount)
	{
		if (amount == 0f) return;

		switch (type)
		{
			case StatType.MaxHp: maxHpBonus += amount; break;
			case StatType.AttackPower: attackPowerBonus += amount; break;
			case StatType.AttackSpeed: attackSpeedBonus += amount; break;
			case StatType.MemorySize: memorySizeBonus += amount; break;
			case StatType.CooldownReduction: cooldownReductionBonus += amount; break;
			case StatType.MoveSpeed: moveSpeedBonus += amount; break;
			default: return;
		}
		NotifyChanged(type);
	}

	// 알림만 한다. 네트워크를 붙이면 SyncVar의 hook에서 각 화면이 부르도록 바꾼다
	void NotifyChanged(StatType type) => Changed?.Invoke(type);

	void Awake()
	{
		if (baseStats == null)
			Debug.LogError($"[{name}] PlayerStats의 Base Stats 칸이 비어 있습니다.", this);
	}
}
