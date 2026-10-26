using UnityEngine;

/// <summary>
/// 테스트용. 플레이어 체력이 생기기 전에 접촉 피해가 들어오는지 로그로 확인한다.
/// PR 전에 지운다.
/// </summary>
public class TestDamageReceiver : MonoBehaviour, IDamageable
{
	public void TakeDamage(int damage)
	{
		Debug.Log($"[{name}] {damage} 피해를 받음 (시각 {Time.time:F2})");
	}
}
