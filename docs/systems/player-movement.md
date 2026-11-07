# PlayerMovement

- 담당자: 박진우

## 하는 일
- 내 캐릭터의 좌우 이동과 점프를 맡는다. 바닥에 서 있을 때만 점프한다.
- 발판(Platform) 위에서 ↓ + Space로 발판 아래로 내려간다. 발판은 아래에서 뚫고 올라갈 수 있다.
- 바라보는 방향(`Facing`)을 정하고, 그림을 그 방향으로 뒤집는다(`PlayerFacingView`).
- 메인 카메라가 내 캐릭터를 따라가게 한다(`PlayerCameraFollow`). 레이드에서 관전 중에는 살아 있는 파티원을 따라간다 (#419).
- 다른 시스템이 "지금 바닥에 서 있는지", "어느 쪽을 보는지"를 읽을 수 있게 `IsGrounded`, `Facing`을 알려 준다.
- 하지 않는 일
  - 대시·더블점프·검 타기를 언제 쓸지는 이동 스킬(c-skills.md 「이동 스킬」)이 정한다. 이 컴포넌트는 움직임만 한다 (#256).
  - 애니메이션, 소리를 하지 않는다. 연출은 `IsGrounded` 등을 읽는 쪽이 한다.

## 동작 규칙
- 바닥 판정, 발판 내려가기의 판정은 플레이어가 속한 장소 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 묻는다. 호스트에서도 바닥을 찾기 위함 (place-scenes.md, #125). 발판 충돌 끄기(`Physics2D.IgnoreCollision`)와 중력 값은 그대로 쓴다.
- 이동 속도는 `PlayerStats.MoveSpeed`를 읽는다.
- 달리기 (#93)
  - 왼쪽 Shift(`PlayerInputReader.RunHeld`)를 누르는 동안 이동 속도 × `Run Speed Multiplier`(임시 1.5).
  - 땅에 있을 때만 Shift 상태를 읽는다. 공중에서는 땅을 떠난 순간(점프, 발판 내려가기, 낭떠러지)의 달리기 여부를 착지할 때까지 유지한다. 공중에서 Shift를 누르거나 떼도 속도가 바뀌지 않는다. 방향(←→)은 공중에서도 바꿀 수 있다.
  - 슬롯을 누르고 있는 동안(대상 고르는 중)은 Player 맵이 꺼져 `RunHeld`가 false라 달리지 않는다.
  - 대시는 달리기와 별개로 스킬(`common.dash`)이다.
- 입력은 `PlayerInputReader`의 `MoveX`, `MoveY`(검 타기 중에만, #317), `JumpPressed`, `DropPressed`, `RunHeld`만 읽는다. 내 캐릭터(`isLocalPlayer`)에서만 동작한다.
- 이동 스킬 (#256)
  - 대시(`StartDash`): 그 방향으로 `Dash Distance`(임시 4칸)를 `Dash Duration`(0.15초)에 간다. 공중 가능. 대시 중에는 중력·좌우 입력·점프가 없다. 벽에 막히면 멈춘다.
  - 더블점프: 스킬 트리에서 `common.double_jump`를 해금했으면(`PlayerProgress.Unlocked`) 공중에서 Space로 한 번 더 뛴다. 땅에 닿으면 다시 쓸 수 있다. `PlayerProgress`가 없는 테스트 씬은 `Allow Double Jump Without Progress`를 따른다. 블록코딩의 더블점프는 `RequestAirJump`로 같은 공중 점프를 한다 (해금 검사 없음).
  - 검 타기(`StartRide` / `StopRide`): 중력 없이 떠서 ←→로 `Ride Speed`(임시 9), ↑↓(`MoveY`, #317)로 `Ride Vertical Speed`(임시 6)로 `Ride Seconds`(임시 30초) 동안 난다. 키를 떼면 그 자리에 멈춘다(중력 없음). 시간이 다 되면 스스로 내려온다.
    - 아래 바닥(땅·발판)과는 `Ride Clearance`(임시 0.6)만큼 띄워 둔다 (#351). ↓를 계속 눌러도 그 높이에서 멈추고, 바닥 가까이에서 타기 시작하면 그 높이까지 떠오른다. 그래서 발밑의 소환검이 바닥에 묻히지 않고, 바닥에 선 것(`IsGrounded`)으로 보지 않아 걷기 모션이 나오지 않는다.
    - 발판 아래로 내려가려면 평소처럼 ↓ + Space(`DropPressed`, ↓ 먼저)를 누른다. 발밑 `Ride Clearance` 안의 발판과 내 콜라이더의 충돌을 꺼서 뚫는다(`IsDropping`, 다른 플레이어는 그대로 섬). 땅(Ground)은 뚫지 않는다. 위로는 발판 설정(One Way)으로 원래 뚫린다.
    - 발이 발판 안에 걸쳐 있으면(아래에서 뚫고 오르다 멈춤) 그 발판은 바닥으로 치지 않는다. 판정은 아래 「발판 안에 들어갔을 때」와 같다 (#357).
    - 가로·세로 속도는 따로 정한다. 대각선(→ + ↑)은 두 속도를 그대로 합친다.
    - 위치는 소유 클라이언트가 정하고 `NetworkRigidbodyReliable2D`로 맞춰지므로, 위아래 이동도 다른 플레이어 화면에 그대로 보인다.
    - ↑는 상호작용(`InteractPressed`)과 같은 키지만, 검 타는 중에는 장소 입구가 ↑를 무시한다 (#285, `PlaceInteractable`).
    - 나는 시간은 스킬 시간(20틱 = 0.4초)과 따로다. 스킬은 타기를 시작만 하고 먼저 끝난다.
    - 남은 시간은 `SwordRideTimerView`(임시 표시)가 머리 위 작은 바로 그린다. 왼쪽 끝 고정, 오른쪽 끝이 왼쪽으로 줄어든다.
    - 슬롯 키를 누르고 있는 동안은 `MoveX`·`MoveY`가 0이다(대상 고르기용, player-input.md). 그동안 그 자리에 떠 있고, 슬롯 키를 떼면 다시 ←→↑↓로 난다.
  - 대시·검 타기 동안은 중력을 0으로 두고 끝나면 처음 중력으로 되돌린다. 움직임을 잠그면(#207) 먼저 끝낸다. 경직·사망이 되어도(`CanAct`가 false) 먼저 끝낸다 (#325). 끝내지 않으면 중력 0인 채 공중에 멈춘다.
  - 검 타기가 소유 클라이언트에서 끝나면(대시, 움직임 잠금, 경직·사망, 시간 종료, `StopRide`) 서버에 알려 서버의 소환검 `Riding`을 푼다 (#325). 원격 클라이언트는 `[Command]`로, Host의 내 캐릭터는 바로 `SummonedSwords.EndAllRides()`를 부른다. 서버가 이미 푼 뒤에 알려도 풀 검이 없어 아무 일도 없다.
  - 서버에서 다른 컴퓨터의 플레이어에 부르면 `[TargetRpc]`로 소유 클라이언트에서 한다 (돌진과 같은 방식, #223).
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
  - 검 타기 중에는 ↓ + Space로 발밑에서 조금 떨어진 발판도 뚫는다 (#351, 위 「검 타기」).
  - 발판 안에 들어갔을 때 (#357): 발판 충돌체(`CompositeCollider2D`, Outline)는 타일 덩어리 테두리 선이고 One Way가 옆면을 통과시켜서, 옆면을 비비면 몸이 발판 안으로 들어가 안쪽 밑변 위에 설 수 있다. 그래서 발판에 서 있다고 판정됐는데 발이 그 발판 안에 있으면, ↓ + Space와 같은 방식으로 그 발판을 뚫어 떨어뜨린다.
    - 발이 안에 있는지: 발밑 바로 위(0.1) 칸에 발판 타일이 있고, 그 칸 윗변에서 아래로 쏴서 발보다 위에 발판 선이 있으면 안이다. 윗면에 제대로 서 있으면 그 칸이 비었거나(한 칸 타일) 선이 발 높이에 있다(칸 가운데에 얹힌 판자 그림). 타일맵이 아닌 발판은 발 바로 위 1칸 안에 같은 발판 선이 있는지로 본다.
  - 내려가는 중(`IsDropping`)에는 발판을 바닥으로 치지 않는다. 그래서 발판을 지나는 동안 점프할 수 없다.
  - 충돌은 `Drop Duration`(기본 0.3초)이 지나고, **몸이 발판에서 완전히 빠져나온 뒤에** 다시 켠다. 몸이 걸친 채로 켜면 발판 속에 끼기 때문이다. 발판 바로 아래가 땅이라 몸이 걸친 채 서 있으면, 옆으로 걸어 나올 때까지 그 발판을 통과하는 상태로 둔다.
  - 위로 뚫고 오르기는 코드가 아니라 발판 설정(`PlatformEffector2D` One Way)이 한다. 아래 「발판 설정」을 지켜야 한다.
  - 위로 올라가는 동안(점프, 검 타기 ↑)에는 몸이 걸친 발판과의 충돌을 ↓ + Space처럼 꺼 두고, 몸이 빠져나오면 다시 켠다 (#372). One Way 발판은 반쯤 지나간 몸의 위로 가는 속도가 0에 가까워지면 겹친 것을 위에서 밟은 것으로 보고 밀어 올려, 한 번 더 튕긴 것처럼 보였다. 다 올라가면 떨어지며 윗면에 착지하고, 못 올라가면 그대로 떨어진다. 이 동안 `IsDropping`이 true다.
- 바라보는 방향(`Facing`)
  - `MoveX`가 양수면 오른쪽(1), 음수면 왼쪽(-1). 멈추면 마지막 방향을 유지한다. 처음에는 오른쪽이다.
  - 그림은 오른쪽을 보고 그린다고 정한다(임혜원 #28 그림도 오른쪽). 왼쪽을 볼 때 `SpriteRenderer.flipX`를 켠다. 콜라이더와 자식 오브젝트는 뒤집지 않는다.
  - 값은 `PlayerMovement`가 정하고, 뒤집기는 `PlayerFacingView`가 한다 (코드 작성 규칙 1).
  - `[SyncVar]`다 (#223). `PlayerMovement`의 Sync Direction을 Client To Server로 둬서, 소유 클라이언트가 바꾼 방향이 서버(스킬이 읽음)와 다른 클라이언트(그림 뒤집기)에 맞춰진다.
- 서버의 스킬이 움직임을 바꿀 때 (#223, network.md 「경우별 처리 방법」 3번)
  - 위치는 소유 클라이언트가 정한다(`NetworkRigidbodyReliable2D`, Client To Server). 서버에서 `StartLunge`, `StopLunge`, `FaceTowards`를 부르면, 다른 컴퓨터의 플레이어일 때 `[TargetRpc]`로 소유 클라이언트에 요청하고 거기서 움직인다.
  - Host의 내 캐릭터와 네트워크 없는 테스트는 그 자리에서 한다. 부르는 쪽(스킬)은 구분하지 않고 같은 함수를 부르면 된다.
  - `FaceTowards`는 서버의 `Facing`도 바로 바꾼다. 같은 틱에 스킬이 방향을 읽기 때문.
  - 다른 컴퓨터의 플레이어면 서버의 `IsLunging`은 늘 `false`다 (돌진은 소유 클라이언트에서 함).
  - 한계: 방향을 바꾼 거의 같은 순간에 시전하면 서버가 바뀌기 전 방향을 읽을 수 있다 (network.md).
- 경직·사망 중(`PlayerHealth.CanAct`가 false)에는 좌우로 움직이지 않고 점프하지 않는다. 떨어지는 것은 그대로다. 대시·검 타기 중이었으면 끝낸다 (#325). `PlayerHealth`가 없는 플레이어는 검사하지 않는다.
- 붙는 곳 (#268): 공용 `Assets/Prefabs/Player.prefab` 최상위. 공용 PlayerMove(`_UL`) 자리를 대신한다. `Player_JW` 등 변형 프리팹은 물려받는다.
  - 박진우 컴포넌트와 실행기 묶음이 함께 붙어 있다: `PlayerMovement`, `PlayerFacingView`, `SwordRideTimerView` / `FunctionExecutor`, `ProcessMemory`, `LanguageRule`, `FunctionExecutorLogger`, `SlotLoadout`, `FunctionExecutorNetwork` / `SlotCaster`, `SlotTargetMarker` / `PhysicalSword`, `SwordPickUpInput`, `SummonedSwords`, `SwordWaveLauncher` / `SkillEffectEvents`, `ThrustEffectView`, `VitalStrikeMinigame`, `VitalStrikeMinigameView`, `ParryGuard`
  - `SummonedSwordHud`(임시 소환검 표시)는 게임 HUD가 대신해서 공용 프리팹에서 뺐다. Test_JW용 `Player_JW`에만 붙어 있다 (#376)
  - 이어서 붙일 것: `PlayerHealth`·`PlayerHealthEffects`(김도훈), `PlayerProgress`·`PlayerLevelStats`(이우림). 각 변형 프리팹의 중복 컴포넌트는 각 담당자가 지운다.
- 카메라 추적(`PlayerCameraFollow`)
  - 내 캐릭터(`isLocalPlayer`)에서만 메인 카메라(MainCamera 태그)를 움직인다. 다른 플레이어가 접속해도 카메라를 빼앗지 않는다.
  - 붙는 곳: 공용 `Assets/Prefabs/Player` (#185). `Player_JW` 등 변형 프리팹은 물려받는다. Base 씬 Host 캐릭터도 카메라가 따라온다.
  - 카메라 중심 = 캐릭터 발바닥 + `Offset`(기본 (0, 6)). Z는 `Camera Z`(기본 -10).
    - Y 6: 땅은 화면 아래 4~5칸만 보이고 하늘·건물, 위쪽 발판이 더 보이게 (임혜원 요청, 맵 꾸미기)
  - 위치를 `PixelPerfectCamera.RoundToPixel`로 픽셀 격자에 맞춰 픽셀이 떨리지 않게 한다.
  - 공용 카메라 프리팹은 고치지 않는다.
  - 관전 (#419)
    - 조건: 내 캐릭터가 숨겨졌고(`PlayerHealth.IsHidden`) 내 장소가 레이드 던전(`PlayerPlace.Mine == PlaceScenes.RaidDungeon`)일 때. 일반 장소에서는 숨겨져도 내 캐릭터에 머문다.
    - 이벤트(`HideEffectPlayed`, `RespawnEffectPlayed`)를 구독하지 않고 매 프레임 `IsHidden`을 읽는다. 구독 순서와 해제를 신경 쓰지 않기 위함.
    - 대상 후보: `PlayerRoster.Players` 중 내가 아니고, 죽지 않았고(`IsDead`가 false), 같은 장소(`PlayerRoster.IsInMyPlace`)인 플레이어.
    - 처음 대상: 내가 죽은 자리에서 가장 가까운 후보. 따라가던 플레이어가 죽거나 장소를 떠나면 가장 가까운 후보로 바뀐다.
    - 후보가 없으면(전멸) 내가 죽은 자리에 머문다. 부활하면(`IsHidden`이 false) 내 캐릭터로 돌아온다.
    - ← → 로 대상 바꾸기: `PlayerInputReader.MoveX`가 0에서 ±1로 바뀐 프레임만 눌림으로 본다. 순서는 `netId` 오름차순, ←는 이전, →는 다음, 끝에서는 반대쪽 끝으로. 후보가 하나면 그대로.
      - 숨겨지는 순간 이미 누르고 있던 키, 누르고 있는 동안의 반복은 대상을 바꾸지 않는다.
      - 죽은 동안 `PlayerMovement`는 `CanAct`가 false라 `MoveX`를 쓰지 않으므로 이동과 겹치지 않는다. 슬롯 키를 누르고 있으면 `MoveX`가 0이라 바뀌지 않는다.
    - 네트워크 작업 없음: 다른 플레이어의 위치와 `IsDead`, `IsHidden`은 이미 모든 화면에 맞춰져 있다(#310). 각 화면의 카메라만 바꾼다.
    - 한계: 레이드 던전 맵이 화면보다 좁아 `CameraBounds`(임혜원)가 카메라를 맵 가운데에 고정하므로, 대상이 바뀌어도 가로 이동이 거의 보이지 않는다.
- 벽 마찰: 플레이어 콜라이더에 마찰 0 재질(`PlayerNoFriction`, Friction Combine = Minimum)을 쓴다. 공중에서 벽 쪽으로 키를 눌러도 벽에 붙지 않는다.

### 지금 값 (공용 Player, #268)

| 항목 | 값 | 위치 |
| --- | --- | --- |
| Jump Height | 4 (유닛) | PlayerMovement |
| Fall Gravity Multiplier | 1.5 | PlayerMovement |
| Gravity Scale | 3 | Rigidbody2D |
| Box Collider 2D | Size (0.7, 2.5), Offset (0, 1.33) | 64×64 그림, 캐릭터 60px, PPU 24 기준 (캐릭터 2/3 축소, 임혜원) |
| Material | PlayerNoFriction | Box Collider 2D |
| Run Speed Multiplier | 1.5 (임시) | PlayerMovement |
| Platform Layers | Platform | PlayerMovement |
| Drop Duration | 0.3 (초) | PlayerMovement |
| Ride Speed | 9 (유닛/초, 임시) | PlayerMovement |
| Ride Vertical Speed | 6 (유닛/초, 임시, #317) | PlayerMovement |
| Ride Seconds | 30 (초, 임시) | PlayerMovement |
| Ride Clearance | 0.6 (유닛, 임시, #351) | PlayerMovement |

### 발판 설정 (맵을 만드는 사람이 지킬 것)
- 발판 타일맵은 `Platform` 레이어에 둔다. 땅·벽은 `Ground`.
- 발판 타일맵에 `PlatformEffector2D`를 붙이고 `Use One Way` 켬, `Surface Arc` 170 (#152). 180이면 발판 옆면도 막혀서, 몸 높이에 걸린 발판 옆을 걸어가거나 돌진할 때 벽처럼 막힌다. 170이면 위에서만 밟히고 옆·아래에서는 통과한다.
- 발판의 `CompositeCollider2D`(또는 Tilemap Collider)에서 `Used By Effector`를 켠다. 이것이 꺼져 있으면 아래에서 뚫고 오르지 못한다.
- 발판 높이: 지금 점프 높이는 4유닛이다. 땅(또는 아래 발판) 윗면에서 다음 발판 윗면까지 4유닛보다 낮아야 올라갈 수 있다.
  - Test_JW: 가운데 발판(땅에서 3유닛)은 점프로 올라갈 수 있다. 왼쪽(5유닛)·오른쪽(7유닛) 발판은 점프로 못 올라간다.
- 주의: 캐릭터 키(2.5유닛)보다 높고 점프 높이(4유닛)보다 낮은 발판(3유닛 등)은 점프로 닿지만, 그보다 낮은 발판은 캐릭터 몸 높이에 걸린다. `Surface Arc` 170이면 그런 발판 밑을 옆으로 지나갈 수 있다 (몸이 발판을 통과). 180이면 턱(벽)처럼 막힌다 (#152에서 Test_JW를 170으로 바꿈).
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
| `IsDropping` | - | `bool` | 발판 아래로 내려가는 중이면 `true` (↓ + Space. 검 타기 중에도 ↓ + Space, #351). 내려가는 애니메이션이 필요하면 읽는다. |
| `SetMovementLocked(bool locked)` | 잠그기 / 풀기 | - | (#207) 잠근 동안 속도 0, 중력 없음(그 자리에 떠 있음), 진행 중인 돌진·내려가기 중단, 좌우·점프·내려가기 입력과 `StartLunge` 무시. 풀면 원래 중력으로 돌아가고 속도 0에서 시작. 장소 이동(`PlaceTravelClient`)이 씬을 바꾸는 동안 쓴다 |
| `IsMovementLocked` | - | `bool` | (#207) 잠겨 있으면 `true` |
| `StartLunge(float targetX)` | 돌진할 x 위치 | - | 그 x까지 좌우로 돌진한다 (`Lunge Speed` 30, 최대 `Lunge Max Time` 0.25초 = 7.5칸, 임시. 캐릭터 2/3 축소 때 0.35에서 줄임). 돌진하는 쪽을 바라본다. 돌진 중에는 좌우 입력을 무시하고, 닿거나 시간이 지나거나 경직·사망하면 멈춘다. 찌르기 스킬이 부른다 (#152). 서버에서 다른 컴퓨터의 플레이어에 부르면 소유 클라이언트에서 한다 (#223) |
| `StopLunge()` | - | - | 돌진을 멈춘다. 스킬이 끝나거나 끊길 때 부른다. 서버에서 다른 컴퓨터의 플레이어에 부르면 소유 클라이언트에서 한다 (#223) |
| `IsLunging` | - | `bool` | 돌진 중이면 `true`. 돌진 애니메이션(임혜원 `C_Lunge`)에서 읽는다 |
| `FaceTowards(float x)` | x 위치 | - | 그 x 쪽을 바라본다. 스킬이 대상 쪽으로 돌아설 때 부른다. 서버에서 다른 컴퓨터의 플레이어에 부르면 서버 값을 바로 바꾸고 소유 클라이언트에도 알린다 (#223) |
| `StartDash(int direction)` | 1 오른쪽, -1 왼쪽 | - | (#256) 대시. 대시 스킬이 부른다 |
| `IsDashing` | - | `bool` | (#256) 대시 중 |
| `RequestAirJump()` | - | - | (#256) 공중이고 이번 공중에서 안 썼으면 한 번 더 점프. 블록코딩 더블점프가 부른다 |
| `DoubleJumpUnlocked`, `AirJumpUsed` | - | `bool` | (#256) 더블점프 해금 여부, 이번 공중에서 썼는지 |
| `AirJumped` (event) | - | - | [연출] (#256) 더블점프를 한 순간. 이미 공중이라 「공중 상태로 바뀜」으로는 알 수 없어서 따로 알린다. 점프 모션을 다시 틀 때 (PlayerAnimator, 임혜원). 모든 화면에서 한 번씩 불림 (#322): 내 화면은 뛴 순간, 서버·Host와 다른 클라이언트는 `[Command]` → `[ClientRpc]`로 조금 늦게 |
| `StartRide()`, `StopRide()`, `IsRiding` | - | - / `bool` | (#256) 검 타기 시작·끝, 타는 중 |
| `RideSeconds`, `RideRemaining` | - | `float` | (#256) 검 타기 시간(Inspector), 남은 시간(타는 중이 아니면 0). 타이머 표시가 읽는다 |

### `PlayerCameraFollow`

플레이어 프리팹 최상위 오브젝트에 붙어 있다. 내 캐릭터의 것을 `NetworkClient.localPlayer.GetComponent<PlayerCameraFollow>()`로 가져온다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `SpectateTarget` | - | `PlayerHealth` | (#419) 관전 중 카메라가 따라가는 플레이어. 관전 중이 아니거나 따라갈 후보가 없으면 `null`. 값은 이 컴포넌트가 `LateUpdate`에서 정한다. 파티원 목록(`RaidPartyView`, 임혜원)이 「관전 중」 표시에 읽는다 |

### 테스트
| 이름 | 설명 |
| --- | --- |
| `PlayerRunSimTest` | Test_JW의 RunTest. 켜고 Play → Host, Game 창을 한 번 클릭. 가짜 키로 걷기·달리기 속도, 달리다 점프 후 공중에서 Shift 떼기(달리는 속도 유지), 걷다 점프 후 공중에서 Shift 누르기(걷는 속도 유지)를 확인. 「[RunTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `MovementLockSimTest` | (#207) Test_JW의 LockTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키 입력, 약 4초). 공중에서 잠그면 안 떨어짐, 잠근 동안 → + Space·돌진으로 안 움직임, 돌진 중에 잠그면 돌진이 멈춤, 풀면 다시 떨어짐을 확인. 「[LockTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |
| `MovementSkillSimTest` | (#256) Test_JW의 MoveSkillTest. 켜고 Play → Host, Game 창을 한 번 클릭 (가짜 키). 대시(3칸 넘게, 높이 그대로), 더블점프(공중에서 다시 올라감, 세 번째 안 됨, 착지하면 다시 됨. 해금 안 됐으면 `RequestAirJump`로 확인, #317), 검 타기(소환검 없으면 실패, 타는 중 상태, 안 떨어짐, → 로 이동, 끝나면 내려오고 검 돌아옴), 위아래 검 타기(#317: ↑로 올라감, 떼면 멈춤, ↓로 내려가다 바닥 위 `Ride Clearance`에서 멈춤·바닥에 선 것 아님, ↓ + Space로 아래 발판 뚫기(땅이면 그대로) #351, 빈 슬롯 키를 누른 채 ↑면 `MoveY` 0이고 높이 그대로. 빈 슬롯이 하나는 있어야 함)를 확인. 약 12초. 「[MoveSkillTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 이우림과 정한 것
  - 공용 PlayerMove(`_UL`)를 복사해 `PlayerMovement`로 만든다. 같은 클래스 이름은 컴파일 에러가 나서 임시로 이름을 바꿨다.
  - #268: 공용 Player 프리팹에서 PlayerMove를 빼고 `PlayerMovement`로 바꿈. PlayerMove 스크립트와 문서(player-stats.md, player-health.md의 PlayerMove) 정리는 이우림
- 정한 것
  - 점프 횟수: 땅에 닿으면 1회 충전. 더블점프 해금 시 땅에 닿기 전까지 2회 연속 가능 (#256에서 함. 더블점프는 슬롯에 넣지 않는 패시브)
- 김도훈과 정한 것
  - 경직·사망 중에는 `PlayerHealth.CanAct`를 읽어 이동·점프를 막는다 (PlayerHealth 주석 「이동과 시전 담당이 이 값을 읽어 막는다」)
- 이우림·임혜원에게 알릴 것
  - 맵의 발판은 위 「발판 설정」대로 만들어야 뚫고 오르기·내려가기가 된다. #369에서 Lobby, HuntingGround, TrainingRoom, RaidDungeon, SceneTemplate의 Platform에 `PlatformEffector2D`를 붙이고 `Used By Effector`를 켰다 (그 전에는 Test_JW에만 있어서 실제 맵의 발판이 옆·아래에서 막혔다). 새 맵은 SceneTemplate을 복사하면 그대로 따라온다.
  - 발판 높이는 점프 높이(4유닛)보다 낮게. 점프 높이와 캐릭터 키가 비슷해서 생기는 문제(위 「주의」)도 같이 정해야 한다.
- 임혜원과 정해야 할 것
  - 점프·착지 애니메이션이 `IsGrounded`를 읽어서 바뀌는지
  - 그림은 오른쪽을 보고 그리고, 왼쪽은 `flipX`로 뒤집는다 (#28 그림 기준). 애니메이션 재생도 이 방식으로 맞추는지
  - 캐릭터 그림 크기가 정해지면 콜라이더 Size·Offset을 맞추는 방법 (높이 = 캐릭터 픽셀 높이 ÷ 16, Offset Y = 발 아래 빈 픽셀 ÷ 16 + 높이 ÷ 2)
