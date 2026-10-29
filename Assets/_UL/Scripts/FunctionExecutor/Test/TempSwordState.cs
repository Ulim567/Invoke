using UnityEngine;

/// <summary>
/// 맨손 여부를 임시로 가진 컴포넌트. 물리 검 관리가 생기기 전까지 테스트 스킬이 읽는다.<br/>
/// 처음 값은 Inspector에서 정하고, 게임 중에는 SetUnarmed로만 바꾼다.<br/>
/// 소환검 개수는 여기서 세지 않는다. 소환검은 힙 객체이므로 ProcessMemory.Heap이 센다.
/// </summary>
public class TempSwordState : MonoBehaviour
{
	[SerializeField, Tooltip("맨손이면 체크")]
	bool isUnarmed;

	/// <summary>
	/// 맨손이면 true.
	/// </summary>
	public bool IsUnarmed => isUnarmed;

	/// <summary>
	/// 맨손 여부를 바꾼다. 테스트에서 실행 도중에 맨손으로 바꿀 때 쓴다.
	/// </summary>
	public void SetUnarmed(bool unarmed)
	{
		isUnarmed = unarmed;
	}
}
