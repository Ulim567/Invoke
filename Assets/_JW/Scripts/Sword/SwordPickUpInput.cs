using Mirror;
using UnityEngine;

/// <summary>
/// 내 캐릭터에서 줍기 키(PlayerInputReader.PickUpPressed, Z)를 누르면 물리 검을 먼저 줍고(PhysicalSword.TryPickUp), 못 주우면 근처에 떨어진 소환검을 줍는다(SummonedSwords.TryPickUpNearest).
/// <para>
/// 입력 읽기(isLocalPlayer)와 검 상태(PhysicalSword)를 나눈 컴포넌트. 플레이어 프리팹에 PhysicalSword와 함께 붙인다.
/// </para>
/// </summary>
[RequireComponent(typeof(PhysicalSword))]
public class SwordPickUpInput : NetworkBehaviour
{
	[SerializeField] PlayerInputReader input;
	[SerializeField] PhysicalSword sword;
	[Tooltip("소환검 관리 (없으면 물리 검만 줍는다)")]
	[SerializeField] SummonedSwords summoned;

	void Reset()
	{
		input = GetComponent<PlayerInputReader>();
		sword = GetComponent<PhysicalSword>();
		summoned = GetComponent<SummonedSwords>();
	}

	void Awake()
	{
		// 칸이 비어 있으면 같은 오브젝트에서 찾는다
		if (input == null) input = GetComponent<PlayerInputReader>();
		if (sword == null) sword = GetComponent<PhysicalSword>();
		if (summoned == null) summoned = GetComponent<SummonedSwords>();
	}

	void Update()
	{
		if (!isLocalPlayer) return;
		if (!input.PickUpPressed) return;
		if (sword.TryPickUp()) return;
		if (summoned != null) summoned.TryPickUpNearest();
	}
}
