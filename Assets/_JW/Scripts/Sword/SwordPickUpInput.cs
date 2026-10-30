using Mirror;
using UnityEngine;

/// <summary>
/// 내 캐릭터에서 줍기 키(PlayerInputReader.PickUpPressed, Z)를 누르면 PhysicalSword.TryPickUp을 부른다.
/// <para>
/// 입력 읽기(isLocalPlayer)와 검 상태(PhysicalSword)를 나눈 컴포넌트. 플레이어 프리팹에 PhysicalSword와 함께 붙인다.
/// </para>
/// </summary>
[RequireComponent(typeof(PhysicalSword))]
public class SwordPickUpInput : NetworkBehaviour
{
	[SerializeField] PlayerInputReader input;
	[SerializeField] PhysicalSword sword;

	void Reset()
	{
		input = GetComponent<PlayerInputReader>();
		sword = GetComponent<PhysicalSword>();
	}

	void Awake()
	{
		// 칸이 비어 있으면 같은 오브젝트에서 찾는다
		if (input == null) input = GetComponent<PlayerInputReader>();
		if (sword == null) sword = GetComponent<PhysicalSword>();
	}

	void Update()
	{
		if (!isLocalPlayer) return;
		if (input.PickUpPressed) sword.TryPickUp();
	}
}
