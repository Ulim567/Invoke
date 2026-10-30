# PlayerMovement

- 담당자: 박진우

## 하는 일
- 내 캐릭터의 좌우 이동과 점프를 맡는다. 바닥에 서 있을 때만 점프한다.
- 발판(Platform) 위에서 ↓ + Space로 발판 아래로 내려간다. 발판은 아래에서 뚫고 올라갈 수 있다.
- 바라보는 방향(`Facing`)을 정하고, 그림을 그 방향으로 뒤집는다(`PlayerFacingView`).
- 메인 카메라가 내 캐릭터를 따라가게 한다(`PlayerCameraFollow`).
- 다른 시스템이 "지금 바닥에 서 있는지", "어느 쪽을 보는지"를 읽을 수 있게 `IsGrounded`, `Facing`을 알려 준다.
- 하지 않는 일
  - 더블점프, 대시는 하지 않는다. 이동 스킬 이슈에서 추가한다.
  - 애니메이션, 소리를 하지 않는다. 연출은 `IsGrounded` 등을 읽는 쪽이 한다.

## 동작 규칙
- 이동 속도는 `PlayerStats.MoveSpeed`를 읽는다.
- 달리기 (#93)
  - 왼쪽 Shift(`PlayerInputReader.RunHeld`)를 누르는 동안 이동 속도 × `Run Speed Multiplier`(임시 1.5).
  - 땅에 있을 때만 Shift 상태를 읽는다. 공중에서는 땅을 떠난 순간(점프, 발판 내려가기, 낭떠러지)의 달리기 여부를 착지할 때까지 유지한다. 공중에서 Shift를 누르거나 떼도 속도가 바뀌지 않는다. 방향(←→)은 공중에서도 바꿀 수 있다.
  - 슬롯을 누르고 있는 동안(대상 고르는 중)은 Player 맵이 꺼져 `RunHeld`가 false라 달리지 않는다.
  - 대시는 달리기와 별개로 스킬(`common.dash`)이다.
- 입력은 `PlayerInputReader`의 `MoveX`, `JumpPressed`, `DropPressed`, `RunHeld`만 읽는다. 내 캐릭터(`isLocalPlayer`)에서만 동작한다.
- 바닥 판정
  - 콜라이더 아래 끝에 얇은 상자(너비 = 콜라이더 너비 × 0.9, 높이 0.1)를 두고 Ground·Platform 레이어와 겹치는지 검사한다.
  - 상자 너비가 몸보다 좁아서 옆의 벽은 바닥으로 잡히지 않는다.
  - 위로 올라가는 중에는 바닥이 아니다. (발판을 아래에서 뚫고 오를 때 공중 재점프 방지)
  - 상자는 콜라이더 크기에서 계산하므로, 캐릭터·콜라이더 크기가 바뀌어도 코드를 고치지 않아도 된다.
- 점프
  - 바닥에 있을 때 Space를 누르면 1번 뛴다. 공중에서는 뛰지 않는다.
  - 힘이 아니라 높이(`Jump Height`, 유닛)로 정한다. 중력(Rigidbody2D `Gravity Scale`)을 바꿔도 같은 높이로 뛴다.
  - 떨어지는 중에만 중력에 `Fall Gravity Multiplier`를 곱해 묵직하게 떨어진다.
- 발판 내려가기
  - 바닥에 서 있을 때 ↓ + Space(`DropPressed`)를 누르면, 발밑에 겹친 발판과 **내 콜라이더 사이의 충돌만** 끈다(`Physics2D.IgnoreCollision`). 발판 자체를 끄지 않으므로 다른 플레이어는 그대로 서 있다.
  - 발밑에 땅(Ground)이 하나라도 걸쳐 있으면 내려가지 않는다. 땅에서 ↓ + Space는 아무 일도 없다(점프도 하지 않는다).
  - 내려가는 중(`IsDropping`)에는 발판을 바닥으로 치지 않는다. 그래서 발판을 지나는 동안 점프할 수 없다.
  - 충돌은 `Drop Duration`(기본 0.3초)이 지나고, **몸이 발판에서 완전히 빠져나온 뒤에** 다시 켠다. 몸이 걸친 채로 켜면 발판 속에 끼기 때문이다. 발판 바로 아래가 땅이라 몸이 걸친 채 서 있으면, 옆으로 걸어 나올 때까지 그 발판을 통과하는 상태로 둔다.
  - 위로 뚫고 오르기는 코드가 아니라 발판 설정(`PlatformEffector2D` One Way)이 한다. 아래 「발판 설정」을 지켜야 한다.
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
| Run Speed Multiplier | 1.5 (임시) | PlayerMovement |
| Platform Layers | Platform | PlayerMovement |
| Drop Duration | 0.3 (초) | PlayerMovement |

### 발판 설정 (맵을 만드는 사람이 지킬 것)
- 발판 타일맵은 `Platform` 레이어에 둔다. 땅·벽은 `Ground`.
- 발판 타일맵에 `PlatformEffector2D`를 붙이고 `Use One Way` 켬, `Surface Arc` 180.
- 발판의 `CompositeCollider2D`(또는 Tilemap Collider)에서 `Used By Effector`를 켠다. 이것이 꺼져 있으면 아래에서 뚫고 오르지 못한다.
- 발판 높이: 지금 점프 높이는 4유닛이다. 땅(또는 아래 발판) 윗면에서 다음 발판 윗면까지 4유닛보다 낮아야 올라갈 수 있다.
  - Test_JW: 가운데 발판(땅에서 3유닛)은 점프로 올라갈 수 있다. 왼쪽(5유닛)·오른쪽(7유닛) 발판은 점프로 못 올라간다.
- 주의: 캐릭터 키(3.75유닛)가 점프 높이(4유닛)와 비슷해서, 점프로 닿는 발판은 항상 캐릭터 몸 높이에 걸린다. 그래서 그런 발판 밑으로는 걸어 들어갈 수 없고, 옆에서 보면 턱(벽)처럼 막힌다. 점프로 붙어서 올라서는 방식이 된다. 메이플처럼 발판 밑을 지나다니려면 점프 높이·캐릭터 크기·발판 간격을 같이 정해야 한다 (기획과 정할 것).
- 타일맵 콜라이더를 코드나 YAML로 바꾸면 Composite Collider 모양이 갱신되지 않을 수 있다. 타일은 Tile Palette로 칠한다.

## 다른 시스템이 쓰는 것

플레이어 프리팹 최상위 오브젝트에 `PlayerMovement`가 붙어 있다. `GetComponent<PlayerMovement>()`로 가져온다.

### `PlayerMovement`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1. 스킬이 앞쪽(판정, 투사체 방향)을 정할 때 읽는다. 예: `Vector2.right * movement.Facing` |
| `IsGrounded` | - | `bool` | 지금 바닥(Ground·Platform)에 서 있으면 `true`. 올라가는 중, 발판을 내려가는 중에는 `false`. 읽을 때마다 새로 검사하므로 저장해 두지 않고 필요할 때 읽는다. 점프·착지 애니메이션, 이동 스킬에서 쓴다. |
| `IsRunning` | - | `bool` | 달리는 속도로 좌우로 움직이고 있으면 `true` (멈춰 있으면 `false`). 공중에서는 땅을 떠난 순간의 값을 유지. 달리기 애니메이션에서 읽는다. |
| `RunSpeedMultiplier` | - | `float` | 달리기 속도 배율. 달릴 때 이동 속도 = `PlayerStats.MoveSpeed` × 이 값 |
| `IsDropping` | - | `bool` | 발판 아래로 내려가는 중이면 `true`. 내려가는 애니메이션이 필요하면 읽는다. |

### 테스트
| 이름 | 설명 |
| --- | --- |
| `PlayerRunSimTest` | Test_JW의 RunTest. 켜고 Play → Host, Game 창을 한 번 클릭. 가짜 키로 걷기·달리기 속도, 달리다 점프 후 공중에서 Shift 떼기(달리는 속도 유지), 걷다 점프 후 공중에서 Shift 누르기(걷는 속도 유지)를 확인. 「[RunTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정한 것
  - 공용 PlayerMove(`_UL`)를 복사해 `PlayerMovement`로 만든다. 같은 클래스 이름은 컴파일 에러가 나서 임시로 이름을 바꿨다. 공용 Player 프리팹 반영과 원본 PlayerMove 정리는 후속 작업으로 한다.
- 정한 것
  - 점프 횟수: 땅에 닿으면 1회 충전. 더블점프 해금 시 땅에 닿기 전까지 2회 연속 가능 (해금은 이동 스킬 이슈)
- 김도훈과 정한 것
  - 경직·사망 중에는 `PlayerHealth.CanAct`를 읽어 이동·점프를 막는다 (PlayerHealth 주석 「이동과 시전 담당이 이 값을 읽어 막는다」)
- 이우림·임혜원에게 알릴 것
  - 맵의 발판은 위 「발판 설정」대로 만들어야 뚫고 오르기·내려가기가 된다. SceneTemplate(공용)의 Platform에는 아직 `PlatformEffector2D`가 없다 (Test_JW에만 붙였다).
  - 발판 높이는 점프 높이(4유닛)보다 낮게. 점프 높이와 캐릭터 키가 비슷해서 생기는 문제(위 「주의」)도 같이 정해야 한다.
- 임혜원과 정해야 할 것
  - 점프·착지 애니메이션이 `IsGrounded`를 읽어서 바뀌는지
  - 그림은 오른쪽을 보고 그리고, 왼쪽은 `flipX`로 뒤집는다 (#28 그림 기준). 애니메이션 재생도 이 방식으로 맞추는지
  - 캐릭터 그림 크기가 정해지면 콜라이더 Size·Offset을 맞추는 방법 (높이 = 캐릭터 픽셀 높이 ÷ 16, Offset Y = 발 아래 빈 픽셀 ÷ 16 + 높이 ÷ 2)
