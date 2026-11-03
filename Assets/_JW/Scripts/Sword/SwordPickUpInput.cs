using Mirror;
using UnityEngine;

/// <summary>
/// 내 캐릭터에서 줍기 키(PlayerInputReader.PickUpPressed, Z)를 누르면 물리 검을 먼저 줍고(PhysicalSword.TryPickUp), 못 주우면 근처에 떨어진 소환검을 줍는다(SummonedSwords.TryPickUpNearest).
/// <para>
/// 입력 읽기(isLocalPlayer)와 검 상태(PhysicalSword)를 나눈 컴포넌트. 플레이어 프리팹에 PhysicalSword와 함께 붙인다.<br/>
/// 네트워크 (#229, network.md 경우 4): 키는 내 화면에서 읽고, 줍기는 [Command]로 서버에 요청한다. 검을 없애고 상태를 바꾸는 일은 서버만 한다.
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
		CmdPickUp();
	}

	// [서버] 물리 검을 먼저, 못 주우면 근처의 떨어진 소환검. 거리는 서버가 아는 위치로 잰다 (플레이어 위치는 소유 클라이언트가 보냄)
	[Command]
	void CmdPickUp()
	{
		if (sword.TryPickUp()) return;
		if (summoned != null) summoned.TryPickUpNearest();
	}
}
