# 입력 (GameInput, PlayerInputReader, UIShortcut)

- 담당자: 이우림
- 위치: `Assets/Scripts/Input` (공용, #298). 누구나 고칠 수 있고, 고친 PR은 이우림이 검토

## 하는 일
- `GameInput`: 입력 설정 파일(`InputSystem_Actions`)의 액션을 찾아 보관. 프로젝트에서 액션을 찾는 곳은 이 클래스 하나
- `PlayerInputReader`: 내 캐릭터의 키 입력을 이동·시전 스크립트에 값으로 전달. 다른 스크립트는 키(`Keyboard.current`, `InputAction`)를 직접 읽지 않고 이 컴포넌트의 값만 읽는다.
  - Player·Attack 액션 맵을 활성화·비활성화하는 함수를 제공한다.
- `UIShortcut`: 화면에서 Enter·ESC를 누르면 Inspector에서 연결한 함수를 부름 (#298)
- 하지 않는 일
  - 액션 맵을 언제 활성화·비활성화할지 정하지 않는다. 언제 부를지(UI 열기, 공격 불가 지역, 슬롯 키를 누르는 동안 등)와 여러 이유가 겹칠 때 언제 다시 활성화할지는 부르는 쪽이 정한다.
  - `UIShortcut`은 화면을 직접 열고 닫지 않는다. 연결된 함수가 연다.
  - 코드에서 붙일 때는 `OnSubmit` / `OnCancel` (UnityEvent)에 `AddListener` 한다 (#324, `InputBlockingPanel`이 창마다 붙임)

## GameInput 동작 규칙
- 액션 맵 이름과 같은 묶음: `GameInput.Player`, `GameInput.Attack`, `GameInput.UI`
- Player·Attack 묶음: `InputAction`을 돌려줌. 값은 `PlayerInputReader`가 읽음 (내 캐릭터 검사, 슬롯을 누르는 동안의 처리)
- UI 묶음: 값(`SubmitPressed`, `CancelPressed`)을 바로 돌려줌. 캐릭터와 관계없으므로 Player가 없는 화면(로그인 등)에서도 동작
- 액션을 찾는 시점: 처음 쓸 때. Play를 시작할 때 비움 (Play 모드에 들어갈 때 도메인 다시 불러오기를 꺼 두었으므로)
- 액션 이름을 못 찾으면 오류
- UI 액션 맵: Input System이 시작할 때 활성화. Player·Attack 액션 맵이 비활성화되어 있어도 동작

## UIShortcut 동작 규칙
- 열린 화면: `UIShortcut`이 붙은 오브젝트가 활성화된 순서로 기억. 마지막으로 활성화된 화면이 맨 위
- 맨 위 화면 하나만 키에 반응
- 한 프레임에 한 화면만 반응. ESC로 맨 위 화면을 닫은 프레임에 그 밑의 화면은 닫히지 않음
- 선택된 UI(EventSystem의 `currentSelectedGameObject`)가 그 키를 직접 처리하면 반응하지 않음
  - Enter: 버튼, 입력칸(`TMP_InputField`), 드롭다운이 선택되어 있을 때
  - ESC: 입력칸, 드롭다운이 선택되어 있을 때
  - 이유: EventSystem이 선택된 UI에 같은 키를 보내므로, 두 동작이 함께 일어나는 문제. 블럭 편집 화면의 입력칸(`BlockField`)에 입력하는 중 Enter·ESC로 화면이 닫히는 문제
  - 한계: 마우스로 버튼을 누른 뒤에는 그 버튼이 선택된 채 남아, Enter가 그 버튼을 누름 (EventSystem 동작)
- ESC와 Enter를 같은 프레임에 누르면 ESC만 처리

## PlayerInputReader 동작 규칙
- 내 캐릭터(`isLocalPlayer`)에서만 동작한다. 다른 플레이어의 캐릭터에서는 입력이 연결되지 않아 값을 읽으면 오류가 난다.
- 값은 읽을 때마다 그 프레임의 입력을 돌려준다. `Update`에서 필요할 때 바로 읽고, 따로 저장하지 않는다.
- 슬롯은 먼저 누른 슬롯 하나만 기록한다. 다른 슬롯을 눌러도 바뀌지 않고, 기록된 슬롯을 뗐을 때만 해제된다.
- 슬롯 키를 누르는 동안 ← → 대상 선택(`TargetStep`)은 Player 액션 맵이 비활성화되어 있어야 동작한다.
  - 원인: Shortcut Keys Consume Input 설정 때문에 Player 액션 맵의 `Move`(1D Axis 조합)가 ← → 입력을 먼저 가져가고, Attack 액션 맵의 `TargetPrev`·`TargetNext`(단일 키)에는 입력이 전달되지 않음
- Space는 Player 액션 맵의 `Jump`와 Attack 액션 맵의 `Mark`가 함께 받는다. 슬롯 키를 누르는 동안 Player 액션 맵이 활성화되어 있으면 `JumpPressed`와 `MarkPressed`가 함께 true가 된다.
  - 원인: 두 액션 모두 단일 키 바인딩이라 Shortcut Keys Consume Input이 한쪽 입력을 막지 않음
- ↑는 `MoveY`(Player 액션 맵의 `MoveUp`)와 `Interact`가 함께 받는다. ↑를 누르면 `MoveY`가 1이 되고, 그 프레임에 `InteractPressed`도 true가 된다 (#271).
  - `MoveY`를 1D Axis 조합 대신 단일 키 액션 `MoveUp`·`MoveDown`으로 만든 이유: 1D Axis 조합이면 Shortcut Keys Consume Input 때문에 ↑를 `Interact`보다 먼저 가져가 `InteractPressed`가 항상 false가 됨
- ↓는 `MoveY`(Player 액션 맵의 `MoveDown`)와 `Down`(↓ + Space)의 modifier가 함께 받는다. ↓만 누르면 `MoveY`가 -1이고, ↓ + Space는 지금처럼 `DropPressed`가 true가 된다 (#271).
  - 한계: Space를 먼저 누른 채 ↓를 누르면 `Down`이 ↓를 가져가, ↓를 다시 누를 때까지 `MoveY`가 0

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `PlayerInputReader`가 붙어 있다. `GetComponent<PlayerInputReader>()`로 가져온다.

### 이동

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MoveX` | - | `float` | 좌우 이동 값. ← 는 -1, → 는 1, 누르지 않으면 0. 슬롯을 누르고 있는 동안에는 항상 0 |
| `MoveY` | - | `float` | (#271) 위아래 이동 값. ↑ 는 1, ↓ 는 -1, 누르지 않거나 둘 다 누르면 0. 검 타기에서 위아래로 날 때 씀. 슬롯을 누르고 있는 동안에는 항상 0. ↑ 는 `InteractPressed`와 함께 받음 |
| `JumpPressed` | - | `bool` | 이번 프레임에 점프 키(Space)를 눌렀으면 true. ↓ + Space(내려가기)일 때는 false |
| `RunHeld` | - | `bool` | 달리기 키(왼쪽 Shift)를 누르고 있는 동안 true. 슬롯을 누르고 있는 동안에는 false |
| `DropPressed` | - | `bool` | 이번 프레임에 ↓ 를 누른 채 Space를 눌렀으면 true. 발판 아래로 내려가기에 쓴다 |

### 시전

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `HeldSlot` | - | `int` | 지금 누르고 있는 슬롯 번호(0~7, Slot1~Slot8). 누르고 있지 않으면 -1. 슬롯을 누른 프레임에 이 값이 그 슬롯 번호와 같으면, 그 슬롯이 대상 선택을 시작한 것 |
| `TargetStep` | - | `int` | 슬롯을 누르고 있는 동안 이번 프레임에 → 를 누르면 1, ← 를 누르면 -1, 그 밖에는 0. 슬롯을 누르고 있지 않으면 항상 0. Player 액션 맵이 비활성화되어 있어야 ← → 를 받음 |
| `MarkPressed` | - | `bool` | 슬롯을 누르고 있는 동안 이번 프레임에 Space를 눌렀으면 true. 여러 명 선택에서 고른 대상을 찍거나 빼는 데 씀. 슬롯을 누르고 있지 않으면 항상 false |
| `CancelPressed` | - | `bool` | 이번 프레임에 취소 키(C)를 눌렀으면 true. Attack 액션 맵이 비활성화되어 있으면 항상 false |
| `SlotPressed(int)` | 슬롯 번호(0~7) | `bool` | 이번 프레임에 그 슬롯을 눌렀으면 true |
| `SlotReleased(int)` | 슬롯 번호(0~7) | `bool` | 이번 프레임에 그 슬롯을 뗐으면 true. 이 순간에 시전을 요청한다 |

### 그 밖

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `PickUpPressed` | - | `bool` | 이번 프레임에 검 줍기 키(Z)를 눌렀으면 true |
| `InteractPressed` | - | `bool` | 이번 프레임에 상호작용 키(↑)를 눌렀으면 true. 입구 들어가기, 스킬 트리 해금 장소에 씀 |

### 액션 맵 활성화·비활성화

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SetPlayerMapEnabled(bool)` | true: 활성화, false: 비활성화 | - | Player 액션 맵(Move, MoveUp, MoveDown, Jump, Run, Down, PickUp, Interact)을 활성화·비활성화. 내 캐릭터에서만 동작 |
| `SetAttackMapEnabled(bool)` | true: 활성화, false: 비활성화 | - | Attack 액션 맵(Slot1~Slot8, TargetPrev, TargetNext, Mark, Cancel)을 활성화·비활성화. 비활성화하면 누르고 있던 슬롯도 해제(`HeldSlot`이 -1). 내 캐릭터에서만 동작 |

- 여러 이유가 겹칠 때: 이 함수들은 이유를 기억하지 않는다. 예를 들어 슬롯 키를 누른 채 UI를 열었다 닫을 때, UI 쪽에서 `SetPlayerMapEnabled(true)`를 부르면 슬롯 키가 눌려 있어도 Player 액션 맵이 활성화된다.
- 캐릭터가 사라질 때(`OnStopLocalPlayer`): 두 액션 맵 모두 다시 활성화된다. 재접속한 캐릭터가 비활성화 상태를 물려받지 않게 하기 위함.

### UI 입력 (`GameInput.UI`, #298)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `GameInput.UI.SubmitPressed` | - | `bool` | 이번 프레임에 확인 키(Enter)를 눌렀으면 true |
| `GameInput.UI.CancelPressed` | - | `bool` | 이번 프레임에 취소 키(ESC)를 눌렀으면 true. Attack 액션 맵의 취소(C, `PlayerInputReader.CancelPressed`)와 다른 키 |
| `GameInput.UI.Shortcut1Pressed` | - | `bool` | 이번 프레임에 숫자 1을 눌렀으면 true (#431). 화면에서는 `UIShortcut`의 On Shortcut 1을 씀 |
| `GameInput.UI.Shortcut2Pressed` | - | `bool` | 이번 프레임에 숫자 2를 눌렀으면 true (#431). 화면에서는 `UIShortcut`의 On Shortcut 2를 씀 |
| `GameInput.UI.NextFieldPressed` | - | `bool` | 이번 프레임에 Tab을 눌렀으면 true (#430). 화면 안의 다음 입력칸으로 넘어갈 때 씀 (`TabFieldNavigator`, key-badge.md) |

### 화면 단축키 (`UIShortcut`, #298)

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| On Submit (Inspector) | - | - | 맨 위 화면일 때 Enter를 누르면 부를 함수 |
| On Cancel (Inspector) | - | - | 맨 위 화면일 때 ESC를 누르면 부를 함수 |
| On Shortcut 1 / On Shortcut 2 (Inspector) | - | - | 맨 위 화면일 때 숫자 1 / 2를 누르면 부를 함수 (#431). 입력칸이 선택되어 있으면 반응하지 않음 (글자로 쓰임). 게임 HUD가 스킬 트리·레벨업 바로가기로 씀 (hud.md) |
| `IsTop` | - | `bool` | 이 화면이 열린 화면 중 맨 위이면 true |

- 붙이는 곳: 화면 스크립트가 `SetActive`로 여닫는 오브젝트 또는 그 자식. 부모에 붙이면 화면이 닫혀도 알지 못함
- 화면을 여닫는 방법: `SetActive`. `CanvasGroup` 알파값, `Canvas` 컴포넌트 비활성화로 숨기면 열린 화면 목록이 바뀌지 않음
- 다른 화면 위에 뜨는 UI(팝업, 창): 칸이 비어 있어도 `UIShortcut`을 붙임. 붙이지 않으면 밑에 있는 화면이 키에 반응
- 한 화면에 Enter 하나, ESC 하나. 되돌릴 수 없는 동작(삭제, 초기화)에는 Enter를 연결하지 않음
- 다른 키 추가 순서 (예: Tab으로 다음 탭)
  1. `InputSystem_Actions`의 UI 액션 맵에 액션 추가. Input Actions 편집 창에서 형식 확인
  2. `GameInput.UI`에 그 키의 값(예: `NextTabPressed`) 추가
  3. `UIShortcut`에 Inspector 칸(UnityEvent) 추가, `Update`에서 그 값이 true이면 `Handle`로 호출
     - `Handle`을 거쳐야 「맨 위 화면만」, 「한 프레임에 한 화면만」 규칙이 그 키에도 적용
     - 선택된 UI가 그 키를 직접 처리하는 경우(예: 입력칸의 글자 키): `SelectedHandles`로 양보
     - `Update`의 `else if` 순서가 우선순위. 같은 프레임에 여러 키를 누르면 위에 있는 키만 처리
  4. 이 문서의 「UI 입력」, 「화면 단축키」 표에 그 키 추가

## 맞춘 결과
- 박진우와 정할 것
  - 슬롯 키를 누르는 동안의 Player 액션 맵 비활성화 (#59)
    - 기획: 슬롯 키를 누르는 동안 이동·점프·대시 불가
    - 방식: 박진우 쪽 코드에서 슬롯 키를 누를 때 `SetPlayerMapEnabled(false)`, 뗄 때 `SetPlayerMapEnabled(true)` 호출. 이 비활성화로 `TargetStep`이 ← → 를 받음
    - `PlayerInputReader`에서 `Move`만 비활성화하는 수정안(`slot-caster.md`)은 쓰지 않음. 액션 맵 활성화 상태를 두 컴포넌트가 따로 바꾸게 되는 문제
  - 입력 키 결정 (#71)
    - 대시: 스킬(`common.dash`)로만 씀. Player 액션 맵의 `Dash` 액션은 `Run`으로 바꿈
    - 왼쪽 Shift: 달리기(`Run`). 달리기 속도 적용은 박진우(`PlayerMovement`)
    - 검 줍기: Z(`PickUp`), 상호작용: ↑(`Interact`), 여러 명 선택의 대상 찍기: Space(`Mark`)
    - 키 사용자 지정: 추후 결정
- Tab 이동 (#430, 임혜원)
  - 바인딩: UI 액션 맵에 NextField(`<Keyboard>/tab`) 추가. 받는 쪽은 `TabFieldNavigator` (key-badge.md)
- HUD 바로가기 1·2 (#431, 임혜원)
  - 바인딩: UI 액션 맵에 Shortcut1(`<Keyboard>/1`), Shortcut2(`<Keyboard>/2`) 추가
  - `UIShortcut`의 On Shortcut 1 / 2로 받으므로 「맨 위 화면만」 규칙이 그대로 적용됨. HUD는 다른 창이 열려 있으면 맨 위가 아니라 반응하지 않음
- UI Enter·ESC (#298)
  - 바인딩: UI 액션 맵의 Submit(`*/{Submit}`, Enter 포함), Cancel(`*/{Cancel}`, Escape 포함). 입력 설정 파일 변경 없음
  - 두 화면이 열려 있을 때 ESC: 맨 위 화면만 닫음
  - 입력 파일 위치: `Assets/_UL/Scripts`에서 공용 `Assets/Scripts/Input`으로 이동. 누구나 고칠 수 있게
  - 액션을 찾는 곳: `GameInput` 하나로 모음. `PlayerInputReader`는 `GameInput`에서 액션을 가져옴 (값의 이름과 뜻은 그대로)
  - 지금은 다른 화면 위에 뜨는 UI가 없음. 생기면 `SetActive`로 여닫고 `UIShortcut`을 붙임
  - EventSystem(`Base` 씬)은 Input System 패키지의 `DefaultInputActions`를 씀. Navigate에 W·A·S·D·화살표가 있어 슬롯·이동 키와 겹침. 이번에는 그대로 둠
