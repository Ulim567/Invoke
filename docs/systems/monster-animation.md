# MonsterAnimator (몬스터 애니메이션)

- 담당자: 임혜원

## 하는 일
- 몬스터 상태를 읽어서 잡몹 애니메이션(Leak, Loop)을 재생한다. 연출만 하고 값은 바꾸지 않는다.
- 몬스터 투사체 그림이 날아가는 방향을 보게 한다 (`ProjectileFacing`).
- 네트워크에서는 상태만 동기화하고, 애니메이션은 각 화면에서 이 컴포넌트가 상태를 보고 정한다.
- 하지 않는 일
  - 몬스터 좌우 뒤집기(flipX): 몬스터 AI(`GroundChaseAI`, `FlyingShooterAI`, 김도훈)가 최상위 SpriteRenderer에 한다.
  - 투사체 만들기, 밀려나기(넉백): 김도훈

## 동작 규칙
- 스크립트: `Assets/_HW/Scripts/MonsterAnimator.cs`, `Assets/_HW/Scripts/ProjectileFacing.cs`
- 그림·클립·컨트롤러: `Assets/_HW/Monsters/Leak`, `Assets/_HW/Monsters/Loop` (#92)
- 우선순위: 사망 > 피격 > 발사 > 기본
- 상태를 읽는 곳

| 애니메이션 | 읽는 것 | 담당 |
| --- | --- | --- |
| 기본 (Leak `Move`, Loop `Hover`) | 늘. `GroundChaseAI`가 있고 `IsMoving`이 false면 `Idle Speed`(0.35) 빠르기로 천천히 | 김도훈 |
| `Shoot` | `ProjectileAttack.FireEffectPlayed`부터 `Shoot Duration`(0.58초) 동안. 투사체는 `Fire Delay`(0.25초) 뒤 = Shoot 4번째 프레임 | 김도훈 |
| `Hit` | `Monster.HitEffectPlayed`부터 `Hit Duration`(0.42초) 동안 | 김도훈 |
| `Death` | `Monster.DeathEffectPlayed`부터 끝까지. 마지막 빈 장면에서 멈춤. 오브젝트는 `Despawn Delay` 뒤에 없어짐 | 김도훈 |

- Animator Controller에는 상태만 두고 화살표(Transition)는 만들지 않는다. 상태 이름은 Inspector의 `Base State`, `Hit State`, `Death State`, `Shoot State`와 같아야 한다.
- 컴포넌트가 없으면 그 상태는 건너뛴다. 예: `GroundChaseAI`가 없으면(Loop) 기본 상태를 늘 보통 속도로, `ProjectileAttack`이 없으면(Leak) 발사 모션 없음

| 몬스터 | Base State | Shoot State | 클립 (12fps) | Despawn Delay |
| --- | --- | --- | --- | --- |
| Leak | `Move` | (비움) | Move 7 반복 / Hit 5 (0.42초) / Death 12 (1.0초) | 1.0 |
| Loop | `Hover` | `Shoot` | Hover 7 반복 / Shoot 7 (0.58초, 4번째 프레임에 발사) / Hit 5 / Death 9 (0.75초) | 0.75 |

### 프리팹에 붙인 것 (김도훈 프리팹, 최상위 오브젝트)
- Leak, Loop: Sprite Renderer(첫 장면, Sorting Layer Monster), Animator(컨트롤러 `Leak` / `Loop`), `MonsterAnimator`
- MonsterBullet: Sprite Renderer(`Loop_Bullet` 첫 장면), Animator(컨트롤러 `Loop_Bullet`), `ProjectileFacing`
  - 투사체 그림은 오른쪽으로 날아가는 모양(꼬리가 왼쪽). 왼쪽으로 날면 `ProjectileFacing`이 flipX로 뒤집는다

## 다른 시스템이 쓰는 것

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MonsterAnimator.PlayShoot()` | - | - | 발사 모션을 튼다. `ProjectileAttack`이 있으면 `FireEffectPlayed`로 저절로 불리므로 보통은 부르지 않는다. 다른 방식으로 쏘는 몬스터가 생기면 그 AI가 부른다 |

## 맞춘 결과
- 김도훈과 정한 것
  - 피격·사망은 `Monster.HitEffectPlayed`, `DeathEffectPlayed`, 발사는 `ProjectileAttack.FireEffectPlayed`를 받는다
  - `Despawn Delay`는 사망 모션 길이 이상 (Leak 1.0초, Loop 0.75초, #108·#110에서 김도훈이 바꿈)
  - Loop `Fire Delay` 0.25초 = Shoot 4번째 프레임
  - SpriteRenderer와 Animator는 프리팹 최상위에 붙인다 (AI가 최상위 flipX로 방향을 바꿈)
  - 프리팹에 그림·Animator·`MonsterAnimator`는 임혜원이 붙인다. 판정 크기는 김도훈이 맞춘다
  - Leak 대기 모션은 따로 없이 Move를 천천히. Loop 밀려나기는 `FlyingShooterAI`가 한다
