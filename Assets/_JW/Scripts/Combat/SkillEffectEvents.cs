using System;
using UnityEngine;

/// <summary>
/// C 스킬의 연출용 event를 모아 둔 컴포넌트. 플레이어 프리팹에 붙인다 (#152).
/// <para>
/// - 스킬(DataDrivenSkill)이 값을 바꾼 뒤 이 event들을 부르고, 이펙트·소리·애니메이션(임혜원)은 이 event만 받아서 튼다 (코드 작성 규칙 1).<br/>
/// - 모든 event는 스킬의 고정 ID(PredefinedFunctionIds)를 같이 보낸다. 받는 쪽은 ID로 어떤 이펙트를 틀지 고른다.<br/>
/// - 순서: Started → (Fired → Hit 0번 이상) → Ended. 필요 조건(맨손 등)에 걸려 시작하지 못한 스킬은 아무 event도 내지 않는다.<br/>
/// - 네트워크를 붙이면 Raise 함수들이 [ClientRpc]로 바뀌어 모든 화면에서 불린다.
/// </para>
/// </summary>
public class SkillEffectEvents : MonoBehaviour
{
	/// <summary>[연출] 스킬이 시작했을 때 (스킬 ID). 시전 자세, 시전 소리</summary>
	public event Action<string> Started;

	/// <summary>[연출] 판정이 나간 순간 (스킬 ID, 시작 위치, 방향). 찌르기 섬광, 휘두르기 궤적. 아무도 못 맞혀도(빗나감) 불린다</summary>
	public event Action<string, Vector2, Vector2> Fired;

	/// <summary>[연출] 대상이 맞은 순간 (스킬 ID, 맞은 대상, 맞은 위치, 데미지 결과). 맞은 이펙트, 치명타·처형 연출. 관통처럼 여러 명이면 대상마다 불린다</summary>
	public event Action<string, GameObject, Vector2, DamageResult> Hit;

	/// <summary>[연출] 스킬이 끝났을 때 (스킬 ID, 끝까지 실행했으면 true / 끊겼거나 실패했으면 false). 자세 풀기</summary>
	public event Action<string, bool> Ended;

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseStarted(string skillId) => Started?.Invoke(skillId);

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseFired(string skillId, Vector2 origin, Vector2 direction) => Fired?.Invoke(skillId, origin, direction);

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseHit(string skillId, GameObject target, Vector2 point, DamageResult result) => Hit?.Invoke(skillId, target, point, result);

	/// <summary>C 스킬만 부른다.</summary>
	public void RaiseEnded(string skillId, bool completed) => Ended?.Invoke(skillId, completed);
}
