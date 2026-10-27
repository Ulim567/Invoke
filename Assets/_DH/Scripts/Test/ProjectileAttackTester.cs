using UnityEngine;

/// <summary>
/// 테스트용. Inspector의 컴포넌트 메뉴(⋮)에서 몬스터가 왼쪽·오른쪽으로 투사체를 쏘게 한다.
/// PR 전에 지운다.
/// </summary>
[RequireComponent(typeof(ProjectileAttack))]
public class ProjectileAttackTester : MonoBehaviour
{
	[ContextMenu("Fire Left")]
	void FireLeft() => GetComponent<ProjectileAttack>().Fire(Vector2.left);

	[ContextMenu("Fire Right")]
	void FireRight() => GetComponent<ProjectileAttack>().Fire(Vector2.right);
}
