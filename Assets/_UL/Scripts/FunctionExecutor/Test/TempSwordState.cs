using UnityEngine;

/// <summary>
/// 맨손 여부와 소환검 개수를 임시로 가진 컴포넌트. 물리 검 관리가 생기기 전까지 테스트 스킬이 읽는다.<br/>
/// 처음 값은 Inspector에서 정하고, 게임 중에는 SetUnarmed로만 바꾼다.
/// </summary>
public class TempSwordState : MonoBehaviour
{
	[SerializeField, Tooltip("맨손이면 체크")]
	bool isUnarmed;

	// 소환검은 힙 객체이므로 개수는 ProcessMemory.Heap(CountObjects, GetGlobalPointers)으로 옮길 예정
	// 박진우의 DataDrivenSkill이 아직 이 값을 읽고 있어, 그 코드가 힙 함수로 옮겨질 때까지 남겨 둔다
	[SerializeField, Min(0), Tooltip("쓸 수 있는 소환검 개수 (임시)")]
	int summonedSwordCount;

	/// <summary>
	/// 맨손이면 true.
	/// </summary>
	public bool IsUnarmed => isUnarmed;

	/// <summary>
	/// 쓸 수 있는 소환검 개수. 임시 값이다. 소환검 개수는 ProcessMemory.Heap의 함수로 옮길 예정이다.
	/// </summary>
	public int SummonedSwordCount => summonedSwordCount;

	/// <summary>
	/// 맨손 여부를 바꾼다. 테스트에서 실행 도중에 맨손으로 바꿀 때 쓴다.
	/// </summary>
	public void SetUnarmed(bool unarmed)
	{
		isUnarmed = unarmed;
	}
}
