# PlayerAnimator

- 담당자: 임혜원

## 하는 일
- 플레이어 상태를 읽어서 C 캐릭터 애니메이션 6개(Idle, Run, Jump, Cast, Hit, Death)를 재생한다. 연출만 하고 값은 바꾸지 않는다.
- 네트워크에서는 상태만 동기화하고, 애니메이션은 각 화면에서 이 컴포넌트가 상태를 보고 정한다.
- 하지 않는 일
  - 캐릭터 방향 전환(좌우 뒤집기)은 하지 않는다. 「이동 방향 바라보기와 카메라 따라가기」(박진우)에서 한다.
  - 피격 깜빡임, 사망 후 숨김은 하지 않는다. `PlayerHealthEffects`(김도훈)가 한다.

## 동작 규칙
- 우선순위: 사망 > 피격 > 시전 > 공중 > 이동 > 대기
- 상태를 읽는 곳

| 애니메이션 | 읽는 것 | 담당 |
| --- | --- | --- |
| Run | Rigidbody2D 가로 속도가 `Run Threshold`(0.1)보다 크면 | - |
| Jump | `PlayerMovement.IsGrounded`가 false면. PlayerMovement가 없으면 Ground·Platform에 닿아 있는지로 임시 판단. 클립의 25% 지점(웅크림 다음)부터 튼다 | 박진우 |
| Cast | `FunctionExecutor.TaskStarted`부터 `Cast Duration`(0.5초) 동안 | 이우림 |
| Hit | `PlayerHealth.HitEffectPlayed`부터 `HitStunTime`(0.3초) 동안 | 김도훈 |
| Death | `PlayerHealth.DeathEffectPlayed`부터 `RespawnEffectPlayed`까지 | 김도훈 |

- 컴포넌트가 없으면 그 상태는 건너뛴다. 예: `PlayerHealth`가 없으면 피격·사망 애니메이션이 나오지 않을 뿐 에러는 나지 않는다.
- Animator Controller에는 상태 6개만 두고 Transition은 만들지 않는다. 상태 이름은 위 표의 영어 이름과 같아야 한다.
- 클립은 12fps로 만들고, 상태의 Speed로 길이를 맞춘다.

| 상태 | Speed | 길이 | 반복 |
| --- | --- | --- | --- |
| Idle | 0.5 | 1.33초 | O |
| Run | 1 | 0.67초 | O |
| Jump | 1 | 0.67초, 끝나면 마지막 장면 유지 | X |
| Cast | 1.33 | 0.5초 | X |
| Hit | 1.08 | 0.31초 | X |
| Death | 1 | 1초, 끝나면 빈 장면 유지 | X |

## 다른 시스템이 쓰는 것
- 없음. 다른 시스템은 이 컴포넌트를 부르지 않는다. 애니메이션을 바꾸고 싶으면 위 표의 상태 값(이벤트, IsGrounded)을 바꾸면 된다.

## 맞춘 결과
- 김도훈과 정한 것 (#28)
  - 피격 모션은 `HitEffectPlayed`, 사망 모션은 `DeathEffectPlayed`를 받아 재생한다.
- 박진우와 정한 것
  - 점프 모션은 `PlayerMovement.IsGrounded`를 읽어서 바꾼다.
- 이우림과 정한 것
  - 시전 모션은 `FunctionExecutor.TaskStarted`를 받아 재생한다.
