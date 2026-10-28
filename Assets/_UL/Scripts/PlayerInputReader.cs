using System;
using Mirror;
using UnityEngine.InputSystem;

/// <summary>
/// 내 캐릭터의 입력을 읽어서 이동·시전 스크립트에 전달하는 컴포넌트.<br/>
/// 키를 직접 읽지 않고 이 컴포넌트의 값만 사용한다. (나중에 키 사용자 지정을 붙일 때 고칠 곳을 하나로 만들기 위함)
/// <para>
/// 사용법<br/>
/// - 플레이어 프리팹에 붙어 있으므로 GetComponent&lt;PlayerInputReader&gt;()로 가져온다.<br/>
/// - 내 캐릭터(isLocalPlayer)일 때만 값을 읽는다. 다른 플레이어의 캐릭터에서는 입력이 연결되지 않아 읽으면 오류가 난다.<br/>
/// - 값은 읽을 때마다 그 프레임의 입력을 돌려주므로, Update에서 필요할 때 바로 읽으면 된다. 따로 저장해 둘 필요가 없다.
/// </para>
/// <para>
/// 이동 담당: MoveX, JumpPressed, DashPressed, DropPressed<br/>
/// 시전 담당: HeldSlot, TargetStep, SlotPressed(), SlotReleased()
/// </para>
/// <para>
/// Player·Attack 묶음을 켜고 끄는 일(UI를 열 때, 공격 불가 지역 등)은 이 컴포넌트가 하지 않는다.
/// </para>
/// </summary>
public class PlayerInputReader : NetworkBehaviour
{
	const int SlotCount = 8;

	InputAction move, jump, dash, down, targetPrev, targetNext;
	readonly InputAction[] slots = new InputAction[SlotCount];

	// 내 캐릭터가 사라질 때 -=로 등록을 해제하려면 등록할 때와 같은 함수 객체가 필요하므로 보관
	readonly Action<InputAction.CallbackContext>[] onSlotStarted = new Action<InputAction.CallbackContext>[SlotCount];
	readonly Action<InputAction.CallbackContext>[] onSlotCanceled = new Action<InputAction.CallbackContext>[SlotCount];

	int heldSlot = -1;

	bool IsSlotHeld => heldSlot >= 0 && slots[heldSlot].IsPressed();

	/// <summary>
	/// 지금 누르고 있는 슬롯 번호 (0~7, Slot1~Slot8). 누르고 있지 않으면 -1.
	/// 슬롯을 누른 프레임에 이 값이 그 슬롯 번호와 같으면, 그 슬롯이 대상 선택을 시작한 것이다.
	/// </summary>
	public int HeldSlot => IsSlotHeld ? heldSlot : -1;

	/// <summary>
	/// 좌우 이동 값. ← 는 -1, → 는 1, 누르지 않으면 0.
	/// 슬롯을 누르고 있는 동안에는 ← → 가 대상 선택에 쓰이므로 항상 0, 즉 플레이어는 움직이지 않는다.
	/// </summary>
	public float MoveX => IsSlotHeld ? 0f : move.ReadValue<float>();

	/// <summary>
	/// 이번 프레임에 점프 키(Space)를 눌렀으면 true.
	/// ↓ + Space(내려가기)일 때는 false이다.
	/// </summary>
	public bool JumpPressed => jump.WasPressedThisFrame();

	/// <summary>
	/// 이번 프레임에 대시 키(왼쪽 Shift)를 눌렀으면 true.
	/// </summary>
	public bool DashPressed => dash.WasPressedThisFrame();

	/// <summary>
	/// 이번 프레임에 ↓ 를 누른 채 Space를 눌렀으면 true. 발판 아래로 내려가기에 쓴다.
	/// </summary>
	public bool DropPressed => down.WasPressedThisFrame();

	/// <summary>
	/// 슬롯을 누르고 있는 동안 이번 프레임에 → 를 누르면 1, ← 를 누르면 -1, 그 밖에는 0.
	/// 슬롯을 누르고 있지 않으면 항상 0이다.
	/// 슬롯을 누르는 동안 Player 묶음이 꺼져 있어야 ← → 를 받는다. 켜져 있으면 Move가 ← → 를 먼저 가져가 항상 0이 된다. (Shortcut Keys Consume Input)
	/// </summary>
	public int TargetStep => !IsSlotHeld ? 0
		: (targetNext.WasPressedThisFrame() ? 1 : 0) - (targetPrev.WasPressedThisFrame() ? 1 : 0);

	/// <summary>
	/// 이번 프레임에 index번 슬롯(0~7)을 눌렀으면 true.
	/// </summary>
	public bool SlotPressed(int index) => slots[index].WasPressedThisFrame();

	/// <summary>
	/// 이번 프레임에 index번 슬롯(0~7)을 뗐으면 true. 이 순간에 시전을 요청한다.
	/// </summary>
	public bool SlotReleased(int index) => slots[index].WasReleasedThisFrame();

	public override void OnStartLocalPlayer()
	{
		base.OnStartLocalPlayer();

		InputActionMap playerMap = InputSystem.actions.FindActionMap("Player", true);
		move = playerMap.FindAction("Move", true);
		jump = playerMap.FindAction("Jump", true);
		dash = playerMap.FindAction("Dash", true);
		down = playerMap.FindAction("Down", true);

		InputActionMap attackMap = InputSystem.actions.FindActionMap("Attack", true);
		targetPrev = attackMap.FindAction("TargetPrev", true);
		targetNext = attackMap.FindAction("TargetNext", true);

		for (int i = 0; i < SlotCount; i++)
		{
			int index = i;
			slots[i] = attackMap.FindAction($"Slot{i + 1}", true);

			// 슬롯을 누르고 있는 동안 ← →를 대상 선택으로 쓰기 위해, 먼저 누른 슬롯 하나만 기록한다
			// Update에서 계산하면 스크립트 실행 순서에 영향을 받으므로, 모든 Update보다 먼저 불리는 이벤트로 관리한다
			onSlotStarted[i] = _ => { if (heldSlot < 0) heldSlot = index; };
			// 다른 슬롯을 뗐을 때 대상 선택이 풀리지 않도록, 기록된 슬롯을 뗐을 때만 해제한다
			// canceled는 묶음이 꺼질 때도 오므로, Attack 묶음이 꺼지면 함께 풀린다
			onSlotCanceled[i] = _ => { if (heldSlot == index) heldSlot = -1; };
			slots[i].started += onSlotStarted[i];
			slots[i].canceled += onSlotCanceled[i];
		}
	}

	public override void OnStopLocalPlayer()
	{
		base.OnStopLocalPlayer();

		// Project-wide Actions는 캐릭터가 사라져도 남아 있으므로, 해제하지 않으면 재접속할 때마다 등록이 쌓인다
		for (int i = 0; i < SlotCount; i++)
		{
			if (slots[i] == null) continue;
			slots[i].started -= onSlotStarted[i];
			slots[i].canceled -= onSlotCanceled[i];
		}
		heldSlot = -1;
	}
}
