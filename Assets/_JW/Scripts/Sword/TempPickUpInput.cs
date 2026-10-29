using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [임시] 줍기 키(Z)를 읽어 PhysicalSword.TryPickUp을 부른다.
/// <para>
/// CLAUDE.md 코드 작성 규칙 5(키는 PlayerInputReader에서만 읽는다)의 예외다. 이우림이 PlayerInputReader에 PickUpPressed를 추가하기 전까지만 쓴다 (#69).
/// 추가되면 이 파일을 지우고, 줍기 입력은 PlayerInputReader.PickUpPressed를 읽는 쪽으로 옮긴다.
/// </para>
/// </summary>
[RequireComponent(typeof(PhysicalSword))]
public class TempPickUpInput : NetworkBehaviour
{
	PhysicalSword sword;

	void Awake()
	{
		sword = GetComponent<PhysicalSword>();
	}

	void Update()
	{
		if (!isLocalPlayer || Keyboard.current == null) return;
		if (Keyboard.current.zKey.wasPressedThisFrame) sword.TryPickUp();
	}
}
