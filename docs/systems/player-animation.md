# PlayerAnimator

- 담당자: 임혜원

## 하는 일
- 플레이어 상태를 읽어서 C 캐릭터 애니메이션을 재생한다. 기본 모션 10개(Idle, Run, Jump, Cast, Slash, Throw, Summon, Lunge, Hit, Death)와 스킬마다 하나씩 있는 스킬 상태(`c_wide_slash` 등)다. 연출만 하고 값은 바꾸지 않는다.
- 네트워크에서는 상태만 동기화하고, 애니메이션은 각 화면에서 이 컴포넌트가 상태를 보고 정한다.
- 하지 않는 일
  - 캐릭터 방향 전환(좌우 뒤집기)은 하지 않는다. 「이동 방향 바라보기와 카메라 따라가기」(박진우)에서 한다.
  - 피격 깜빡임, 사망 후 숨김은 하지 않는다. `PlayerHealthEffects`(김도훈)가 한다.

## 동작 규칙
- 우선순위: 사망 > 피격 > 시전 > 공중 > 이동 > 대기
- 상태를 읽는 곳

| 애니메이션 | 읽는 것 | 담당 |
| --- | --- | --- |
| Run | 가로 속도(`PlayerAnimator.HorizontalSpeed`)가 `Run Threshold`(0.1)보다 크면. 내 캐릭터는 Rigidbody2D 가로 속도, 다른 플레이어의 캐릭터는 위치 변화로 계산한 값 (아래 「다른 플레이어의 이동 속도」) | 이우림 (#356) |
| Jump | `PlayerMovement.IsGrounded`가 false면. PlayerMovement가 없으면 Ground·Platform에 닿아 있는지로 임시 판단. 클립의 25% 지점(웅크림 다음)부터 튼다. 더블점프(`PlayerMovement.AirJumped`, #256)면 처음부터 다시 튼다 | 박진우 |
| 스킬 상태 (`c_wide_slash` 등) | `SkillEffectEvents.Started`(스킬 ID)를 받으면 그 스킬의 상태를 `SkillMotionTable`의 시간 동안 (아래 「스킬 모션」) | 박진우 |
| Cast | `SkillEffectEvents`가 없는 시험용 플레이어는 예전처럼 `FunctionExecutorNetwork.TaskStarted`(없으면 `FunctionExecutor.TaskStarted`)에 `Cast Duration`(0.5초) 동안 | 이우림 |
| (없음) | 이동 계열(대시, 더블점프, 검 타기)은 따로 모션을 틀지 않는다. 움직임과 점프 모션이 보여 준다 | |
| Hit | `PlayerHealth.HitEffectPlayed`부터 `HitStunTime`(0.3초) 동안 | 김도훈 |
| Death | `PlayerHealth.DeathEffectPlayed`부터 `RespawnEffectPlayed`까지 | 김도훈 |

- 붙이는 곳: 공용 `Assets/Prefabs/Player.prefab` 최상위 (#221). SpriteRenderer(그림 `C_Idle` 첫 장면, Sorting Layer Player) + Animator(`Assets/_HW/Player/Animations/C_Player.controller`) + `PlayerAnimator`. 개인 테스트 프리팹(Player_JW 등)은 공용 프리팹의 변형이라 같이 받는다
- 컴포넌트가 없으면 그 상태는 건너뛴다. 예: `PlayerHealth`가 없으면 피격·사망 애니메이션이 나오지 않을 뿐 에러는 나지 않는다.
- Animator Controller에는 위 상태만 두고 Transition은 만들지 않는다. Slash·Throw·Summon·Lunge 상태와 클립은 메뉴 Invoke → 플레이어 → 스킬 모션 붙이기(`PlayerMotionBuilder`)가 만든다. 상태 이름은 위 표의 영어 이름과 같아야 한다.
- 클립은 12fps로 만들고, 상태의 Speed로 길이를 맞춘다.

### 다른 플레이어의 이동 속도 (#356)
- 문제: 다른 플레이어의 캐릭터는 내 화면에서 Kinematic이고 `NetworkRigidbodyReliable2D`가 위치만 옮겨서 Rigidbody2D 가로 속도가 늘 0 → Run이 나오지 않음 (점프는 `PlayerMovement.IsGrounded`가 발밑 물리 검사라 정상)
- 계산: 다른 플레이어의 캐릭터일 때만 `PlayerAnimator`가 프레임마다 `(이번 x - 지난 x) / Time.deltaTime`로 가로 속도를 계산 (`HorizontalSpeed`)
  - 다른 플레이어인지: 네트워크가 켜져 있고(`NetworkClient.active` 또는 `NetworkServer.active`), `NetworkIdentity.isLocalPlayer`가 false이고 `NetworkTransformBase`가 있을 때. 그 밖(내 캐릭터, 네트워크를 켜지 않은 시험용 플레이어)은 Rigidbody2D 가로 속도
  - 네트워크로 보내는 값 없음. 각 화면이 보이는 위치로 계산
- 순간이동 거르기: 장소 이동(`ServerTeleport`)이나 처음 나타난 직후의 위치 변화는 이동이 아니므로 속도를 0으로 둠
  - 기준: Mirror가 순간이동과 생성 때 보간 목록을 비움. 목록이 1개 이하인 프레임은 계산하지 않음 (호스트는 `serverSnapshots`, 클라이언트는 `clientSnapshots`)
  - 거리 기준으로 가르지 않은 이유: 빠른 낙하나 검 타기와 구분이 흐려짐
- `HorizontalSpeed`: `PlayerAnimator`의 읽기 속성. 시험이 읽음
- 시험: `RemoteMotionTest` ([place-scenes.md](place-scenes.md) 「시험」). 에디터 호스트와 빌드 클라이언트가 서로의 캐릭터를 걷게 하고, 구경하는 쪽에서 `Animator`가 `Run`을 재생하는지와 `SpriteRenderer` 그림이 바뀌는지 확인
  - 못 한 것: 순간이동으로 위치가 뛰는 장면이 시험에서 관측되지 않아, 순간이동 거르기가 일한 것은 확인하지 못함. 키 입력(`PlayerMovement`)으로 걷는 경로는 사람이 두 창에서 눈으로 확인

### 스킬 모션 (#288)
- 스킬마다 Animator 상태가 하나 있다. 이름은 스킬 ID의 점을 밑줄로 바꾼 것 (`c.wide_slash` → `c_wide_slash`).
- 지금은 전용 그림이 없어서 임시 모션 클립을 넣어 뒀다. 어떤 임시 모션인지, 몇 초 보여 주는지는 `Assets/_HW/Scripts/SkillMotionTable.cs` 한 곳에 있다.
- 메뉴 Invoke → 플레이어 → 스킬 모션 붙이기가 스킬 상태를 만들고 임시 클립과 Speed를 넣는다. 이미 Motion이 들어 있는 스킬 상태는 건드리지 않는다.
- 나중에 고치는 법
  - 전용 그림이 생기면: Animator 창에서 그 스킬 상태를 골라 Motion만 새 클립으로 바꾼다. 코드는 안 고친다.
  - 보여 주는 시간이나 임시 모션을 바꾸려면: `SkillMotionTable`의 그 줄만 고친다. 이미 만든 상태의 임시 클립을 바꾸려면 상태의 Motion을 비우고 메뉴를 다시 누른다.
  - 새 스킬: `SkillMotionTable`에 한 줄 더하고 메뉴를 누른다. 표에 없는 스킬은 스킬 계열(`SkillTable` Family)로 임시 모션을 고른다.
- 스킬 상태가 컨트롤러에 없으면 임시 모션 상태(Cast 등)를 그대로 튼다.
- 돌진 (#373, 박진우 #289 의견): Lunge(뛰어들며 찌르기, 0.75초)는 스킬에 고정하지 않는다. 찌르기 스킬이 대상 앞까지 돌진하기 시작하면(`PlayerMovement.IsLunging`이 true가 된 프레임) Lunge를 처음부터 끝까지 튼다. 대상이 가까워 돌진하지 않으면 그 스킬의 찌르기 모션만 나온다.
  - 다른 플레이어의 캐릭터는 돌진을 소유 클라이언트에서 해서 그 화면의 `IsLunging`이 늘 false다 ([player-movement.md](player-movement.md)). 그래서 찌르기 계열 시전 중에 가로 속도가 `remoteLungeSpeed`(12)보다 빠르면 돌진으로 본다.
  - 표에서 임시 모션을 바꾸면 메뉴 Invoke → 플레이어 → 스킬 모션 붙이기를 다시 누른다. 다른 임시 모션 클립이 들어 있는 스킬 상태는 새 임시 모션으로 바뀌고, 전용 클립은 그대로다.

| 임시 모션 | 스킬 |
| --- | --- |
| Cast (찌르기) | 검 찌르기, 검 집중찌르기, 급소공략, 꼬챙이, 상처 공략, 검 깊이찌르기, 패링 (막기 그림이 생길 때까지) |
| Slash (휘두르기) | 검 휘두르기, 검 크게베기, 상처 가르기, 검 다중베기, 전장 휩쓸기 |
| Throw (던지기) | 검 던지기 |
| Summon (손짓) | 검 소환, 해제, 검무, 표식 남기기, 소환검 찌르기·던지기·휘두르기, 검 돌아오기 (소환검이 대신 움직이는 스킬) |
| 없음 | 검 타기, 대시, 더블 점프 |

- 전용 그림 (#295): 패링 `C_Guard`(검을 세워 막기, 6장면 0.5초), 검 다중베기 `C_MultiSlash`(내려 베고 올려 베기, 8장면 0.67초), 전장 휩쓸기 `C_Sweep`(몸을 돌리며 크게 베기, 8장면 0.67초). `PlayerMotionBuilder.Dedicated`에 적혀 있고, 메뉴를 누르면 그 스킬 상태가 임시 모션일 때만 전용 클립으로 바꾼다
- 맨손 상태(물리 검을 던진 뒤 Idle·Run·Jump)는 따로 그리지 않음. 캐릭터 그림의 검이 몇 픽셀이라 차이가 거의 없고, 떨어진 검이 땅에 보인다

| 상태 | Speed | 길이 | 반복 |
| --- | --- | --- | --- |
| Idle | 0.5 | 1.33초 | O |
| Run | 1 | 0.67초 | O |
| Jump | 1 | 0.67초, 끝나면 마지막 장면 유지 | X |
| Cast | 1.33 | 0.5초 | X |
| Slash, Throw, Summon | 1 | 0.5초 (6장면) | X |
| Lunge | 1 | 0.75초 (9장면) | X |
| Hit | 1.08 | 0.31초 | X |
| Death | 1 | 1초, 끝나면 빈 장면 유지 | X |

## 다른 시스템이 쓰는 것
- 없음. 다른 시스템은 이 컴포넌트를 부르지 않는다. 애니메이션을 바꾸고 싶으면 위 표의 상태 값(이벤트, IsGrounded)을 바꾸면 된다.

## 맞춘 결과
- 김도훈과 정한 것 (#28)
  - 피격 모션은 `HitEffectPlayed`, 사망 모션은 `DeathEffectPlayed`를 받아 재생한다.
- 박진우와 정한 것
  - 점프 모션은 `PlayerMovement.IsGrounded`를 읽어서 바꾼다.
- 박진우와 정한 것 (#152)
  - 스킬 모션은 `SkillEffectEvents.Started`(스킬 ID)를 받아 스킬마다 튼다 (#288에서 계열별 → 스킬별). 블록 함수 자체의 시작에는 모션을 틀지 않고, 함수 안에서 부른 스킬마다 튼다
- 이우림과 정한 것
  - 시전 모션은 `FunctionExecutor.TaskStarted`를 받아 재생한다.
  - 함수를 서버에서만 실행하므로 시전 모션은 모든 화면에 알려 주는 `FunctionExecutorNetwork.TaskStarted`로 바꿈 (#173 요청, #206)
