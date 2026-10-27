using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 공격 하나의 데이터 파일. 공격 = 공격 형태 + 효과에서 "효과" 쪽을 담는다. (버그 0차)<br/>
/// 공격 형태(투사체, 범위 장판 등)는 그 형태를 맡은 컴포넌트가 정하고, 맞았을 때 이 데이터의 효과를 적용한다.
/// <para>
/// 사용법<br/>
/// - Project 창에서 우클릭 → Create → Invoke → Monster Attack으로 만든다.<br/>
/// - Effects에 효과를 여러 개 넣을 수 있다. 예: 체력 피해 10 + 메모리 점유 3칸 5초<br/>
/// - 게임 중에는 값을 바꾸지 않는다. 같은 파일을 쓰는 모든 공격이 함께 바뀐다.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "MonsterAttack", menuName = "Invoke/Monster Attack")]
public class MonsterAttackData : ScriptableObject
{
	[SerializeField, Tooltip("맞았을 때 적용할 효과 목록. 위에서부터 차례로 적용한다")]
	AttackEffect[] effects = new AttackEffect[0];

	[SerializeField, Tooltip("플레이어가 패링으로 잡을 수 있는 공격인지")]
	bool parryable;

	/// <summary>
	/// 맞았을 때 적용할 효과 목록. 읽기만 한다.
	/// </summary>
	public IReadOnlyList<AttackEffect> Effects => effects;

	/// <summary>
	/// 패링으로 잡을 수 있는 공격이면 true.
	/// </summary>
	public bool Parryable => parryable;
}
