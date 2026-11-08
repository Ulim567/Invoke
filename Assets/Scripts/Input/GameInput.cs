using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 입력 설정 파일(InputSystem_Actions)의 액션을 찾아 보관하는 곳. 액션은 이 클래스에서만 찾는다.<br/>
/// 키가 어디서 읽히는지 이 파일 하나만 보면 알 수 있게 하기 위함이다.
/// <para>
/// 사용법<br/>
/// - 액션 맵 이름과 같은 묶음으로 나뉜다: GameInput.Player, GameInput.Attack, GameInput.UI<br/>
/// - Player·Attack 액션은 캐릭터 입력이므로, 이동·시전 스크립트는 여기서 직접 읽지 않고 PlayerInputReader의 값을 쓴다.
///   (내 캐릭터에서만 읽는 처리와 슬롯을 누르는 동안의 처리를 PlayerInputReader가 한다)<br/>
/// - UI 입력은 캐릭터와 관계가 없으므로 GameInput.UI의 값을 바로 읽는다. 화면의 Enter·ESC는 UIShortcut 컴포넌트를 쓴다.<br/>
/// - 새 키를 넣을 때: InputSystem_Actions에 액션을 추가하고, 여기에 그 액션을 찾는 줄을 추가한다.
/// </para>
/// </summary>
public static class GameInput
{
	// Play 모드에 들어갈 때 도메인 다시 불러오기를 꺼 두었으므로, 이전 Play에서 찾은 액션이 남지 않게 처음 쓸 때 찾는다
	static Actions cache;
	static Actions A => cache ??= new Actions(InputSystem.actions);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetStatics()
	{
		cache = null;
	}

	/// <summary>
	/// Player 액션 맵: 이동, 점프, 달리기, 내려가기, 검 줍기, 상호작용.
	/// </summary>
	public static class Player
	{
		public static InputActionMap Map => A.PlayerMap;
		public static InputAction Move => A.Move;
		public static InputAction MoveUp => A.MoveUp;
		public static InputAction MoveDown => A.MoveDown;
		public static InputAction Jump => A.Jump;
		public static InputAction Run => A.Run;
		public static InputAction Down => A.Down;
		public static InputAction PickUp => A.PickUp;
		public static InputAction Interact => A.Interact;
	}

	/// <summary>
	/// Attack 액션 맵: 슬롯 8개, 대상 선택(← →, Space), 취소(C).
	/// </summary>
	public static class Attack
	{
		public const int SlotCount = 8;

		public static InputActionMap Map => A.AttackMap;

		/// <summary>
		/// index번 슬롯(0~7)의 액션. 0번이 Slot1이다.
		/// </summary>
		public static InputAction Slot(int index) => A.Slots[index];

		public static InputAction TargetPrev => A.TargetPrev;
		public static InputAction TargetNext => A.TargetNext;
		public static InputAction Mark => A.Mark;
		public static InputAction Cancel => A.AttackCancel;
	}

	/// <summary>
	/// UI 액션 맵: 확인(Enter), 취소(ESC), 숫자 1·2 (화면 바로가기), 다음 입력칸(Tab).<br/>
	/// Player 액션 맵이나 Attack 액션 맵이 비활성화되어 있어도 동작한다. (UI를 여는 쪽이 두 액션 맵을 비활성화하는 경우가 있으므로)
	/// </summary>
	public static class UI
	{
		public static InputActionMap Map => A.UIMap;

		/// <summary>
		/// 이번 프레임에 확인 키(Enter)를 눌렀으면 true.
		/// </summary>
		public static bool SubmitPressed => A.Submit.WasPressedThisFrame();

		/// <summary>
		/// 이번 프레임에 취소 키(ESC)를 눌렀으면 true. Attack 액션 맵의 취소(C)와는 다른 키다.
		/// </summary>
		public static bool CancelPressed => A.UICancel.WasPressedThisFrame();

		/// <summary>
		/// 이번 프레임에 숫자 1을 눌렀으면 true. 게임 HUD에서 스킬 트리 바로가기로 쓴다 (#431). 화면에서는 UIShortcut의 On Shortcut 1을 쓴다.
		/// </summary>
		public static bool Shortcut1Pressed => A.Shortcut1.WasPressedThisFrame();

		/// <summary>
		/// 이번 프레임에 숫자 2를 눌렀으면 true. 게임 HUD에서 레벨업 바로가기로 쓴다 (#431). 화면에서는 UIShortcut의 On Shortcut 2를 쓴다.
		/// </summary>
		public static bool Shortcut2Pressed => A.Shortcut2.WasPressedThisFrame();

		/// <summary>
		/// 이번 프레임에 Tab을 눌렀으면 true. 화면 안의 다음 입력칸으로 넘어갈 때 쓴다 (#430, TabFieldNavigator).
		/// </summary>
		public static bool NextFieldPressed => A.NextField.WasPressedThisFrame();
	}

	// 찾은 액션을 한꺼번에 들고 있다가 ResetStatics 때 한꺼번에 버리기 위해 묶어 둔다
	sealed class Actions
	{
		public readonly InputActionMap PlayerMap, AttackMap, UIMap;
		public readonly InputAction Move, MoveUp, MoveDown, Jump, Run, Down, PickUp, Interact;
		public readonly InputAction TargetPrev, TargetNext, Mark, AttackCancel;
		public readonly InputAction Submit, UICancel, Shortcut1, Shortcut2, NextField;
		public readonly InputAction[] Slots = new InputAction[Attack.SlotCount];

		public Actions(InputActionAsset asset)
		{
			// 이름이 틀리면 바로 알 수 있게, 못 찾으면 오류를 낸다
			PlayerMap = asset.FindActionMap("Player", true);
			Move = PlayerMap.FindAction("Move", true);
			MoveUp = PlayerMap.FindAction("MoveUp", true);
			MoveDown = PlayerMap.FindAction("MoveDown", true);
			Jump = PlayerMap.FindAction("Jump", true);
			Run = PlayerMap.FindAction("Run", true);
			Down = PlayerMap.FindAction("Down", true);
			PickUp = PlayerMap.FindAction("PickUp", true);
			Interact = PlayerMap.FindAction("Interact", true);

			AttackMap = asset.FindActionMap("Attack", true);
			for (int i = 0; i < Attack.SlotCount; i++)
				Slots[i] = AttackMap.FindAction($"Slot{i + 1}", true);
			TargetPrev = AttackMap.FindAction("TargetPrev", true);
			TargetNext = AttackMap.FindAction("TargetNext", true);
			Mark = AttackMap.FindAction("Mark", true);
			AttackCancel = AttackMap.FindAction("Cancel", true);

			UIMap = asset.FindActionMap("UI", true);
			Submit = UIMap.FindAction("Submit", true);
			UICancel = UIMap.FindAction("Cancel", true);
			Shortcut1 = UIMap.FindAction("Shortcut1", true);
			Shortcut2 = UIMap.FindAction("Shortcut2", true);
			NextField = UIMap.FindAction("NextField", true);
		}
	}
}
