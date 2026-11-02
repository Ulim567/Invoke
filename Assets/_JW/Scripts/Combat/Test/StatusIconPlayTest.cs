using UnityEngine;

/// <summary>
/// (임시 · 직접 해 보는 테스트) 상태 이상 거는 스킬이 아직 없어서, 스킬로 몬스터를 맞힐 때마다
/// 상태 이상을 출혈 → 표식 → 기절 → 실명 → 이속 감소 순서로 하나씩 건다.
/// <para>
/// - Test_JW의 빈 오브젝트에 붙이고 Play → Host. 플레이어가 생기면 SkillEffectEvents.Hit을 받는다.<br/>
/// - 맞은 몬스터에 StatusEffects / StatusIconView가 없으면 붙인다 (TestMonster에는 없음).<br/>
/// - 보기 편하게 지속 시간은 duration(기본 8초)으로 건다. 평소에는 꺼 둔다.
/// </para>
/// </summary>
public class StatusIconPlayTest : MonoBehaviour
{
	[SerializeField, Min(0.5f)] float duration = 8f;

	SkillEffectEvents events;
	int next;

	void Update()
	{
		if (events != null) return;
		events = FindAnyObjectByType<SkillEffectEvents>();
		if (events == null) return;
		events.Hit += HandleHit;
		Debug.Log("[StatusIconPlayTest] 준비됨. Q(찌르기)로 몬스터를 맞히면 상태 이상이 하나씩 걸림");
	}

	void OnDestroy()
	{
		if (events != null) events.Hit -= HandleHit;
	}

	void HandleHit(string skillId, GameObject target, Vector2 point, DamageResult result)
	{
		if (target == null) return;
		Monster monster = target.GetComponentInParent<Monster>();
		GameObject root = monster != null ? monster.gameObject : target;

		if (root.GetComponent<StatusIconView>() == null) root.AddComponent<StatusIconView>();
		StatusEffects status = root.GetComponent<StatusEffects>();

		StatusType type = (StatusType)(next % 5);
		next++;
		status.Apply(type, duration);
		Debug.Log($"[StatusIconPlayTest] {root.name}에 {type} {duration}초");
	}
}
