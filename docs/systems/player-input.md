# PlayerInputReader

- 담당자: 이우림

## 하는 일
- 내 캐릭터의 키 입력을 읽어 이동·시전 스크립트에 값으로 전달한다. 다른 스크립트는 키(`Keyboard.current`, `InputAction`)를 직접 읽지 않고 이 컴포넌트의 값만 읽는다.
- Player·Attack 액션 맵을 활성화·비활성화하는 함수를 제공한다.
- 하지 않는 일
  - 액션 맵을 언제 활성화·비활성화할지 정하지 않는다. 언제 부를지(UI 열기, 공격 불가 지역, 슬롯 키를 누르는 동안 등)와 여러 이유가 겹칠 때 언제 다시 활성화할지는 부르는 쪽이 정한다.

## 동작 규칙
- 내 캐릭터(`isLocalPlayer`)에서만 동작한다. 다른 플레이어의 캐릭터에서는 입력이 연결되지 않아 값을 읽으면 오류가 난다.
- 값은 읽을 때마다 그 프레임의 입력을 돌려준다. `Update`에서 필요할 때 바로 읽고, 따로 저장하지 않는다.
- 슬롯은 먼저 누른 슬롯 하나만 기록한다. 다른 슬롯을 눌러도 바뀌지 않고, 기록된 슬롯을 뗐을 때만 해제된다.
- 슬롯 키를 누르는 동안 ← → 대상 선택(`TargetStep`)은 Player 액션 맵이 비활성화되어 있어야 동작한다.
  - 원인: Shortcut Keys Consume Input 설정 때문에 Player 액션 맵의 `Move`(1D Axis 조합)가 ← → 입력을 먼저 가져가고, Attack 액션 맵의 `TargetPrev`·`TargetNext`(단일 키)에는 입력이 전달되지 않음
- Space는 Player 액션 맵의 `Jump`와 Attack 액션 맵의 `Mark`가 함께 받는다. 슬롯 키를 누르는 동안 Player 액션 맵이 활성화되어 있으면 `JumpPressed`와 `MarkPressed`가 함께 true가 된다.
  - 원인: 두 액션 모두 단일 키 바인딩이라 Shortcut Keys Consume Input이 한쪽 입력을 막지 않음

## 다른 시스템이 쓰는 것

플레이어 프리팹에 `PlayerInputReader`가 붙어 있다. `GetComponent<PlayerInputReader>()`로 가져온다.

### 이동

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MoveX` | - | `float` | 좌우 이동 값. ← 는 -1, → 는 1, 누르지 않으면 0. 슬롯을 누르고 있는 동안에는 항상 0 |
| `JumpPressed` | - | `bool` | 이번 프레임에 점프 키(Space)를 눌렀으면 true. ↓ + Space(내려가기)일 때는 false |
| `RunHeld` | - | `bool` | 달리기 키(왼쪽 Shift)를 누르고 있는 동안 true. 슬롯을 누르고 있는 동안에는 false |
| `DropPressed` | - | `bool` | 이번 프레임에 ↓ 를 누른 채 Space를 눌렀으면 true. 발판 아래로 내려가기에 쓴다 |

### 시전

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `HeldSlot` | - | `int` | 지금 누르고 있는 슬롯 번호(0~7, Slot1~Slot8). 누르고 있지 않으면 -1. 슬롯을 누른 프레임에 이 값이 그 슬롯 번호와 같으면, 그 슬롯이 대상 선택을 시작한 것 |
| `TargetStep` | - | `int` | 슬롯을 누르고 있는 동안 이번 프레임에 → 를 누르면 1, ← 를 누르면 -1, 그 밖에는 0. 슬롯을 누르고 있지 않으면 항상 0. Player 액션 맵이 비활성화되어 있어야 ← → 를 받음 |
| `MarkPressed` | - | `bool` | 슬롯을 누르고 있는 동안 이번 프레임에 Space를 눌렀으면 true. 여러 명 선택에서 고른 대상을 찍거나 빼는 데 씀. 슬롯을 누르고 있지 않으면 항상 false |
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
| `SetPlayerMapEnabled(bool)` | true: 활성화, false: 비활성화 | - | Player 액션 맵(Move, Jump, Run, Down, PickUp, Interact)을 활성화·비활성화. 내 캐릭터에서만 동작 |
| `SetAttackMapEnabled(bool)` | true: 활성화, false: 비활성화 | - | Attack 액션 맵(Slot1~Slot8, TargetPrev, TargetNext, Mark)을 활성화·비활성화. 비활성화하면 누르고 있던 슬롯도 해제(`HeldSlot`이 -1). 내 캐릭터에서만 동작 |

- 여러 이유가 겹칠 때: 이 함수들은 이유를 기억하지 않는다. 예를 들어 슬롯 키를 누른 채 UI를 열었다 닫을 때, UI 쪽에서 `SetPlayerMapEnabled(true)`를 부르면 슬롯 키가 눌려 있어도 Player 액션 맵이 활성화된다.
- 캐릭터가 사라질 때(`OnStopLocalPlayer`): 두 액션 맵 모두 다시 활성화된다. 재접속한 캐릭터가 비활성화 상태를 물려받지 않게 하기 위함.

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
