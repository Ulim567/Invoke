using UnityEngine;

/// <summary>
/// 찌르기 높이 판정 테스트 (#437). 아무 씬의 아무 오브젝트에 붙이고 Play (Host 필요 없음). 콘솔에 「[ThrustHeightTest] … 통과/실패」가 찍힌다.
/// <para>
/// ThrustSkill.OverlapsThrustHeight: 시전자 몸 가운데 ± 1.25칸 구간과 대상 몸 높이가 겹칠 때만 맞는다.<br/>
/// 같은 바닥의 몬스터는 맞고, 위 발판(3칸 위)·아래 발판(3칸 아래)의 몬스터는 안 맞는다. 살짝 걸치면 맞는다.
/// </para>
/// 실제 스킬은 ThrustSkillSimTest, Thrust2SimTest(같은 바닥)로 회귀 확인한다.
/// </summary>
public class ThrustHeightSimTest : MonoBehaviour
{
	int failures;

	void Start()
	{
		// 시전자: 발밑 0, 키 2.5 → 몸 가운데 1.25, 찌르기 높이 0 ~ 2.5
		const float center = 1.25f;
		Check("같은 바닥의 몬스터 (0 ~ 1.5)", ThrustSkill.OverlapsThrustHeight(center, 0f, 1.5f));
		Check("같은 바닥의 큰 몬스터 (0 ~ 4)", ThrustSkill.OverlapsThrustHeight(center, 0f, 4f));
		Check("위 발판의 몬스터 (3 ~ 4.5)는 안 맞음", !ThrustSkill.OverlapsThrustHeight(center, 3f, 4.5f));
		Check("아래 발판의 몬스터 (-3 ~ -1.5)는 안 맞음", !ThrustSkill.OverlapsThrustHeight(center, -3f, -1.5f));
		Check("머리 높이에 살짝 걸침 (2.4 ~ 3.9)은 맞음", ThrustSkill.OverlapsThrustHeight(center, 2.4f, 3.9f));
		Check("발밑에 살짝 걸침 (-1 ~ 0.1)은 맞음", ThrustSkill.OverlapsThrustHeight(center, -1f, 0.1f));
		Check("머리 바로 위 (2.6 ~ 4)는 안 맞음", !ThrustSkill.OverlapsThrustHeight(center, 2.6f, 4f));

		if (failures == 0) Debug.Log("[ThrustHeightTest] 전체 통과");
		else Debug.LogError($"[ThrustHeightTest] 실패 {failures}개");
	}

	void Check(string label, bool ok)
	{
		if (ok) Debug.Log($"[ThrustHeightTest] {label}: 통과");
		else { failures++; Debug.LogError($"[ThrustHeightTest] {label}: 실패"); }
	}
}
