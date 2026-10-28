# PlayerMovement

- 담당자: 박진우

## 하는 일
- 내 캐릭터의 좌우 이동과 점프를 맡는다. 바닥에 서 있을 때만 점프한다.
- 바라보는 방향(`Facing`)을 정하고, 그림을 그 방향으로 뒤집는다(`PlayerFacingView`).
- 메인 카메라가 내 캐릭터를 따라가게 한다(`PlayerCameraFollow`).
- 다른 시스템이 "지금 바닥에 서 있는지", "어느 쪽을 보는지"를 읽을 수 있게 `IsGrounded`, `Facing`을 알려 준다.
- 하지 않는 일
  - 더블점프, 대시, 발판 아래로 내려가기는 하지 않는다. 이동 스킬·발판 이슈에서 추가한다.
  - 애니메이션, 소리를 하지 않는다. 연출은 `IsGrounded` 등을 읽는 쪽이 한다.

## 동작 규칙
- 이동 속도는 `PlayerStats.MoveSpeed`를 읽는다.
- 입력은 `PlayerInputReader`의 `MoveX`, `JumpPressed`만 읽는다. 내 캐릭터(`isLocalPlayer`)에서만 동작한다.
- 바닥 판정
  - 콜라이더 아래 끝에 얇은 상자(너비 = 콜라이더 너비 × 0.9, 높이 0.1)를 두고 Ground·Platform 레이어와 겹치는지 검사한다.
  - 상자 너비가 몸보다 좁아서 옆의 벽은 바닥으로 잡히지 않는다.
  - 위로 올라가는 중에는 바닥이 아니다. (발판을 아래에서 뚫고 오를 때 공중 재점프 방지)
  - 상자는 콜라이더 크기에서 계산하므로, 캐릭터·콜라이더 크기가 바뀌어도 코드를 고치지 않아도 된다.
- 점프
  - 바닥에 있을 때 Space를 누르면 1번 뛴다. 공중에서는 뛰지 않는다.
  - 힘이 아니라 높이(`Jump Height`, 유닛)로 정한다. 중력(Rigidbody2D `Gravity Scale`)을 바꿔도 같은 높이로 뛴다.
  - 떨어지는 중에만 중력에 `Fall Gravity Multiplier`를 곱해 묵직하게 떨어진다.
- 바라보는 방향(`Facing`)
  - `MoveX`가 양수면 오른쪽(1), 음수면 왼쪽(-1). 멈추면 마지막 방향을 유지한다. 처음에는 오른쪽이다.
  - 그림은 오른쪽을 보고 그린다고 정한다(임혜원 #28 그림도 오른쪽). 왼쪽을 볼 때 `SpriteRenderer.flipX`를 켠다. 콜라이더와 자식 오브젝트는 뒤집지 않는다.
  - 값은 `PlayerMovement`가 정하고, 뒤집기는 `PlayerFacingView`가 한다 (코드 작성 규칙 1).
  - 아직 네트워크로 맞추지 않아서 다른 플레이어 화면에서는 방향이 바뀌지 않는다. 네트워크를 붙일 때 `[SyncVar]`로 바꾼다.
- 경직·사망 중(`PlayerHealth.CanAct`가 false)에는 좌우로 움직이지 않고 점프하지 않는다. 떨어지는 것은 그대로다. `PlayerHealth`가 없는 플레이어는 검사하지 않는다.
- 카메라 추적(`PlayerCameraFollow`)
  - 내 캐릭터(`isLocalPlayer`)에서만 메인 카메라(MainCamera 태그)를 움직인다. 다른 플레이어가 접속해도 카메라를 빼앗지 않는다.
  - 카메라 중심 = 캐릭터 발바닥 + `Offset`(기본 (0, 2)). Z는 `Camera Z`(기본 -10).
  - 위치를 `PixelPerfectCamera.RoundToPixel`로 픽셀 격자에 맞춰 픽셀이 떨리지 않게 한다.
  - 공용 카메라 프리팹은 고치지 않는다.
- 벽 마찰: 플레이어 콜라이더에 마찰 0 재질(`PlayerNoFriction`, Friction Combine = Minimum)을 쓴다. 공중에서 벽 쪽으로 키를 눌러도 벽에 붙지 않는다.

### 지금 값 (Player_JW)

| 항목 | 값 | 위치 |
| --- | --- | --- |
| Jump Height | 4 (유닛) | PlayerMovement |
| Fall Gravity Multiplier | 1.5 | PlayerMovement |
| Gravity Scale | 3 | Rigidbody2D |
| Box Collider 2D | Size (1, 3.75), Offset (0, 2) | 64×64 그림, 캐릭터 60px 기준 |
| Material | PlayerNoFriction | Box Collider 2D |

## 다른 시스템이 쓰는 것

플레이어 프리팹 최상위 오브젝트에 `PlayerMovement`가 붙어 있다. `GetComponent<PlayerMovement>()`로 가져온다.

### `PlayerMovement`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1. 스킬이 앞쪽(판정, 투사체 방향)을 정할 때 읽는다. 예: `Vector2.right * movement.Facing` |
| `IsGrounded` | - | `bool` | 지금 바닥(Ground·Platform)에 서 있으면 `true`. 올라가는 중에는 `false`. 읽을 때마다 새로 검사하므로 저장해 두지 않고 필요할 때 읽는다. 점프·착지 애니메이션, 이동 스킬에서 쓴다. |

## 맞춘 결과
- 이우림과 정한 것
  - 공용 PlayerMove(`_UL`)를 복사해 `PlayerMovement`로 만든다. 같은 클래스 이름은 컴파일 에러가 나서 임시로 이름을 바꿨다. 공용 Player 프리팹 반영과 원본 PlayerMove 정리는 후속 작업으로 한다.
- 정한 것
  - 점프 횟수: 땅에 닿으면 1회 충전. 더블점프 해금 시 땅에 닿기 전까지 2회 연속 가능 (해금은 이동 스킬 이슈)
- 김도훈과 정한 것
  - 경직·사망 중에는 `PlayerHealth.CanAct`를 읽어 이동·점프를 막는다 (PlayerHealth 주석 「이동과 시전 담당이 이 값을 읽어 막는다」)
- 임혜원과 정해야 할 것
  - 점프·착지 애니메이션이 `IsGrounded`를 읽어서 바뀌는지
  - 그림은 오른쪽을 보고 그리고, 왼쪽은 `flipX`로 뒤집는다 (#28 그림 기준). 애니메이션 재생도 이 방식으로 맞추는지
  - 캐릭터 그림 크기가 정해지면 콜라이더 Size·Offset을 맞추는 방법 (높이 = 캐릭터 픽셀 높이 ÷ 16, Offset Y = 발 아래 빈 픽셀 ÷ 16 + 높이 ÷ 2)
