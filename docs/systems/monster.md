# Monster

- 담당자: 김도훈

## 하는 일
- 모든 몬스터(잡몹, 보스)의 체력, 피격, 사망을 맡는다. 체력은 `Monster`만 바꾼다.
- 데미지를 받을 수 있는 모든 것이 쓰는 공통 인터페이스 `IDamageable`을 정한다. 몬스터, 플레이어, 보스, 기둥, 분신이 이것을 구현한다.
- 몬스터 몸에 닿은 플레이어에게 피해를 준다(`ContactDamage`). 몸은 서로 통과한다.
- 땅을 걷는 몬스터의 순찰·추적 AI(`GroundChaseAI`)를 맡는다. 잡몹 1 Leak이 쓴다.
- 공중을 떠다니며 멀리서 쏘는 몬스터의 AI(`FlyingShooterAI`)를 맡는다. 잡몹 2 Loop가 쓴다.
- 사냥터에 몬스터를 만들고 다시 만드는 스포너(`MonsterSpawner`)를 맡는다.
- 몬스터 공격을 "공격 형태 + 효과"로 만드는 구조를 맡는다(버그 0차). 지금 있는 형태는 투사체(`ProjectileAttack`, `MonsterProjectile`)다.
- 하지 않는 일
  - 데미지 계산(공격력 × 배율, 치명타, 처형)은 하지 않는다. 계산이 끝난 정수 데미지를 받기만 한다.
  - 연출(이펙트, 소리, 애니메이션)을 하지 않는다. 연출은 `HitEffectPlayed`, `DeathEffectPlayed` event를 받은 쪽이 한다.
  - 드롭은 아직 다루지 않는다. 이후 이슈에서 추가한다.
  - 공격 구조(`MonsterAttackData`, `ProjectileAttack`)는 언제, 어느 방향으로 공격할지 정하지 않는다. 몬스터 AI(`GroundChaseAI`, `FlyingShooterAI` 등)가 정한다.

## 동작 규칙
- 데미지는 `int`다. 0 이하의 데미지와 이미 죽은 몬스터에게 준 데미지는 무시한다.
- 체력이 0이 되면 `Died`를 한 번 알리고, Inspector 값 `Despawn Delay`초 뒤에 오브젝트를 없앤다. `Despawn Delay`는 사망 모션 길이 이상으로 둔다(Leak 1.0초).
- event는 로직용과 연출용으로 나뉜다. 네트워크를 붙이면 로직용은 서버에서만, 연출용은 각 플레이어 화면에서만 불린다.
  - 로직(드롭, 스포너, 보스 페이즈, 레이드 판정)은 `Damaged`, `Died`를 받는다.
  - 연출(이펙트, 소리, 애니메이션, 데미지 숫자)은 `HitEffectPlayed`, `DeathEffectPlayed`를 받는다.
  - 순서: `Damaged` → `HitEffectPlayed` → (체력 0이면) `Died` → `DeathEffectPlayed`
- 접촉 피해는 `FixedUpdate`마다 몸 크기의 상자 안을 검사한다. Player와 Monster 레이어는 충돌이 꺼져 있어서 Trigger를 쓸 수 없기 때문이다.
  - 같은 대상에게는 Inspector 값 `Hit Interval`초에 한 번만 피해를 준다.
  - Trigger가 아닌 Collider(몸)에 닿았을 때만 피해를 준다.
- 판정(접촉 피해 상자, 플레이어 탐지, 낭떠러지·장애물 검사)은 `Physics2D`가 아니라 몬스터가 속한 씬의 물리 세계(`gameObject.scene.GetPhysicsScene2D()`)에 한다. 서버는 장소 씬마다 물리 세계가 따로 있어서, `Physics2D`로 검사하면 사냥터 등에 있는 플레이어와 땅을 찾지 못한다(`place-scenes.md`). 물리 세계는 저장하지 않고 검사할 때마다 가져온다.
- 몬스터 공격 = 공격 형태 + 효과
  - 효과는 `MonsterAttackData` 파일에 목록으로 담는다. 맞으면 위에서부터 차례로 적용한다.
  - 효과 적용은 모두 `MonsterAttackResolver.Apply` 한 곳을 거친다. 체력 피해는 `DamageCalculator.Calculate(DamageRequest.Flat(Amount))`(박진우)를 거쳐 나온 정수로 `IDamageable.TakeDamage`를 부르고, 나머지 효과는 받을 시스템이 아직 없어서 로그만 남긴다.
  - 투사체는 MonsterProjectile 레이어다. Player에 맞으면 효과를 주고 사라지고, Ground·Platform에 닿거나 `Lifetime`초가 지나면 효과 없이 사라진다.
  - 맞았는지 판단과 효과 적용은 네트워크를 붙이면 서버에서만 한다.

- 잡몹
  - 잡몹 1 **Leak**(메모리 누수 덩어리): 순찰형. `Monster` + `ContactDamage` + `GroundChaseAI`(`Chase Players` 끔) + `StatusEffects`. 플레이어를 쫓지 않고 돌아다니며, 닿으면 접촉 피해를 준다. 체력 40, 접촉 피해 10, 순찰 1.5, 사망 대기 1.0초 (전부 임시, 프리팹 Inspector 값)
    - 몸 약 2.5×1.5칸, 그림 Pivot은 몸 바닥(임혜원 #101). 판정 BoxCollider2D와 `ContactDamage` 상자는 Size (2.4, 1.2), Offset (0, 0.6) (#107)
  - 잡몹 2 **Loop**(무한 루프 드론): 원거리 투사체형. `Monster` + `ContactDamage` + `FlyingShooterAI` + `ProjectileAttack` + `StatusEffects`. 공중을 떠다니다 사거리 안의 플레이어를 멈춰서 겨냥해 쏜다. 쫓아가지는 않는다. 체력 30, 접촉 피해 5, 투사체 체력 피해 8(`Loop_Shot`), 사거리 6, 쏘는 간격 2초, 모으는 시간 0.25초, 사망 대기 0.75초 (전부 임시, 프리팹 Inspector 값)
    - 원형 판정(CircleCollider2D) 지름 1.1, 그림 Pivot은 가운데(임혜원 #101). Rigidbody2D Gravity Scale 0
- 몬스터끼리는 서로 통과한다(메이플 방식). 공용 Physics 2D Layer Collision Matrix에서 Monster ↔ Monster 충돌이 꺼져 있다(#81).
- 순찰·추적 (`GroundChaseAI`)
  - 순찰: "걷기"와 "멈추기"를 랜덤 시간씩 번갈아 한다.
    - 걷기 `Walk Time Range`(1.5~4초)가 끝나면 `Idle Chance`(50%) 확률로 멈추고, 아니면 계속 걷는다.
    - 멈추기 `Idle Time Range`(0.5~2초)가 끝나면 다시 걷는다. 다시 걸을 때 `Turn Chance`(40%) 확률로 반대로 돌아선다.
    - 걷는 중 앞이 낭떠러지(발 앞 아래에 Ground·Platform이 없음)거나 장애물(`Obstacle Layers`)이면 바로 돌아선다. 몬스터끼리 통과하게 하려면 `Obstacle Layers`에는 Ground만 넣는다.
    - 처음 방향과 상태는 랜덤이라 여러 마리가 똑같이 움직이지 않는다.
  - 발견: `Chase Players`가 켜져 있을 때만 한다. 끄면 플레이어를 무시하고 순찰만 한다. `Detect Interval`(0.2초)마다 `Detect Range` 안의 살아 있는(`PlayerHealth.IsDead`가 false) 플레이어 중 가장 가까운 플레이어를 쫓는다.
  - 놓침: 쫓던 플레이어가 죽거나 `Lose Range` 밖으로 나가면 잠깐 멈췄다가 순찰로 돌아간다.
  - 추적: 좌우로만 움직이고 점프하지 않는다. 앞이 낭떠러지나 장애물이면 그 자리에서 기다린다(떨어지지 않는다).
  - 몬스터가 죽으면 멈춘다. 그림은 오른쪽을 보고 그리고, 왼쪽을 볼 때 `SpriteRenderer.flipX`를 켠다(플레이어와 같은 규칙).
  - 네트워크를 붙이면 AI는 서버에서만 돈다.

- 스포너 (`MonsterSpawner`)
  - 구역(`Area Size`, `Area Offset`) 안에 몬스터를 `Max Count`(임시 3)마리까지 둔다. 한 마리가 죽으면(`Died`) `Respawn Delay`(임시 5초) 뒤 구역 안 새 랜덤 위치에 한 마리를 만든다.
  - 위치: 구역 안 땅·발판의 윗면 중 랜덤한 곳에 발이 닿게 만든다(몬스터의 BoxCollider2D 또는 CircleCollider2D 기준). 떠다니는 몬스터(`FlyingShooterAI`)는 생긴 자리에서 `Hover Height`만큼 떠오른다. 구역은 몬스터가 생길 땅과 발판을 덮기만 하면 된다.
    - 구역 안 랜덤한 x의 높은 하늘(구역 위 100칸)에서 아래로 선을 그어 땅(`Ground Layers`)을 찾는다. 타일맵 Collider는 테두리(Outlines)만 있어서 선이 윗면·아랫면을 번갈아 맞으므로, 짝수 번째만 윗면으로 써서 땅 속에는 만들지 않는다.
    - 고른 자리 위로 몸 높이만큼 비어 있지 않거나 구역 안에 윗면이 없으면 x를 다시 뽑는다(최대 10번).
  - 몬스터는 `NetworkSpawner.Spawn`(이우림)으로 스포너가 있는 씬에 만든다. 그래서 사냥터 씬의 물리 세계와 플레이어에게 맞춰진다.
  - 서버(Host 포함)에서만 만든다. 서버가 켜지는 순간 모든 지점에 만들고, 클라이언트는 서버가 만든 몬스터를 받기만 한다. 네트워크를 켜지 않으면 만들지 않는다.
  - 죽은 몬스터는 `Despawn Delay` 뒤 `Destroy`로 없앤다. 서버에서 없애면 Mirror(`NetworkIdentity`)가 모든 클라이언트에서도 없앤다.
  - 몬스터 프리팹은 `NetworkIdentity`를 붙이고 NetworkManager의 Registered Spawnable Prefabs에 등록한다. 위치 동기화는 아직 없어서 Host가 아닌 클라이언트에서는 몬스터가 생긴 자리에 멈춰 보인다(몬스터 서버 동기화 이슈).

- 떠다니며 쏘기 (`FlyingShooterAI`)
  - 떠다니기: 중력 없이 생긴 자리보다 `Hover Height`(2.5)만큼 위에서 위아래로 살짝 둥실거린다.
  - 순찰: 움직이기·멈추기를 랜덤 시간씩 번갈아 하고 가끔 돌아선다. 벽(`Obstacle Layers`)이나 처음 자리에서 `Patrol Radius`(4)보다 멀어지면 돌아선다.
  - 공격: `Attack Range`(6) 안의 살아 있는 플레이어 중 가장 가까운 플레이어를 멈춰서 바라보고, 처음엔 `First Shot Delay`(0.5초) 뒤, 그다음엔 `Fire Interval`(2초)마다 몸 중심을 겨냥해 `ProjectileAttack.Fire`를 부른다. 대각선으로도 쏜다. `Lose Range`(8) 밖으로 나가거나 죽으면 순찰로 돌아간다.
  - 밀려나기: 맞으면(`Damaged`) 쏘던 플레이어 반대쪽(없으면 바라보는 반대쪽)으로 `Knockback Time`(0.15초) 동안 `Knockback Speed`(4)로 밀린다. `TakeDamage`에는 때린 쪽 정보가 없어서 쏘던 플레이어를 기준으로 한다.
- 발사 (`ProjectileAttack`)
  - `Fire`를 부르면 연출 `FireEffectPlayed`가 바로 나가고, `Fire Delay`초(모으는 시간) 뒤에 투사체가 나간다. 모으는 중에는 `IsCharging`이 `true`이고, 다시 `Fire`를 불러도 무시한다.
  - 방향은 `Fire`를 부른 순간에 정해진다. 모으는 중에 죽거나 기절하면 투사체는 나가지 않는다.

- 상태 이상 반응 (#103)
  - 상태 이상은 같은 오브젝트의 `StatusEffects`(박진우)에서 읽기만 한다. 몬스터 프리팹 최상위에 `StatusEffects`를 붙인다. 없으면 상태 이상에 반응하지 않는다.
  - 기절(`IsStunned`): 행동 불가. `GroundChaseAI`, `FlyingShooterAI`가 그 자리에 멈추고 탐지도 하지 않는다. `ContactDamage`는 피해를 주지 않고, `ProjectileAttack.Fire`는 쏘지 않는다. 발사를 모으는 중에 기절하면 투사체는 나가지 않는다.
  - 이속 감소(`MoveSpeedMultiplier`): `GroundChaseAI`의 순찰·추적 속도, `FlyingShooterAI`의 순찰 속도에 곱한다.
  - 실명(`IsBlinded`): 공격이 빗나간다. `ContactDamage`는 피해를 주지 않는다. 실명 중에 쏜 투사체는 `Misses`가 켜져 플레이어를 통과한다(날아가는 중에 실명이 풀려도 빗나감). 땅·발판에는 그대로 막힌다.

## 다른 시스템이 쓰는 것

### `IDamageable`

데미지를 주는 쪽은 맞은 Collider2D에서 `GetComponentInParent<IDamageable>()`로 가져와 `TakeDamage`를 부른다. 맞은 대상이 몬스터인지 플레이어인지 알 필요가 없다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TakeDamage(int damage)` | 데미지 (배율·반올림까지 끝낸 정수) | - | 피해를 준다. 0 이하이거나 이미 죽은 대상이면 아무 일도 일어나지 않는다. |

### `Monster`

몬스터 프리팹 최상위 오브젝트에 붙어 있다. `IDamageable`을 구현한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `TakeDamage(int damage)` | 데미지 | - | 체력을 줄인다. 0이 되면 사망한다. |
| `Hp` | - | `int` | 지금 체력. 읽기만 한다. |
| `MaxHp` | - | `int` | 최대 체력 (Inspector 값) |
| `IsDead` | - | `bool` | 죽었으면 `true` |
| `IsBoss` | - | `bool` | 보스면 `true` (Inspector 값). 처형처럼 보스를 빼는 규칙에서 읽는다. |
| `Damaged` (event) | `int` (받은 데미지) | - | [로직] 체력이 줄었을 때 불린다. 보스 페이즈 전환처럼 체력에 따른 로직에 쓴다. |
| `Died` (event) | - | - | [로직] 체력이 0이 된 순간 한 번 불린다. 드롭, 스포너 집계, 레이드 판정에 쓴다. |
| `HitEffectPlayed` (event) | `int` (받은 데미지) | - | [연출] 피격 이펙트, 소리, 데미지 숫자 표시에 쓴다. |
| `DeathEffectPlayed` (event) | - | - | [연출] 사망 이펙트, 소리, 애니메이션에 쓴다. 한 번 불린다. |

### `ContactDamage`

`Monster`와 같은 오브젝트에 붙인다. 다른 스크립트가 부르는 함수는 없고, Inspector 값만 정한다.

| Inspector 값 | 설명 |
| --- | --- |
| `Damage` | 닿았을 때 주는 데미지 |
| `Hit Interval` | 같은 대상에게 다시 피해를 주기까지 시간(초) |
| `Box Size`, `Box Offset` | 검사 상자 크기와 위치. Scene 창에서 빨간 상자로 보인다. |
| `Target Layers` | 피해를 줄 레이어. Player로 둔다. |

### `GroundChaseAI`

몬스터 프리팹에 `Monster`, `Rigidbody2D`, `Collider2D`와 함께 붙인다. Inspector에서 `Ground Layers`는 Ground·Platform, `Obstacle Layers`는 Ground(Monster를 넣으면 몬스터끼리 마주칠 때 통과하지 않고 돌아선다), `Target Layers`는 Player로 둔다. `Chase Players`를 끄면 순찰만 하고 탐지 값은 쓰지 않는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Target` | - | `PlayerHealth` | 지금 쫓는 플레이어. 순찰 중이면 `null`. 연출(추적 중 표정 등)이나 공격 AI가 읽는다. |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1 |
| `IsMoving` | - | `bool` | 지금 좌우로 움직이고 있으면 `true`. 이동·대기 애니메이션을 바꿀 때 읽는다. |

### `MonsterSpawner`

장소 씬(사냥터)에 놓는다. 다른 스크립트가 부르는 함수는 없고, Inspector 값만 정한다.

| Inspector 값 | 설명 |
| --- | --- |
| `Monster Prefab` | 만들 몬스터 프리팹. `NetworkIdentity`가 있고 등록돼 있어야 한다. |
| `Max Count` | 구역 안에 동시에 있을 최대 마리 수 |
| `Respawn Delay` | 죽은 뒤 다시 만들기까지 시간(초) |
| `Area Size`, `Area Offset` | 몬스터를 만들 구역. 몬스터가 생길 땅과 발판을 덮는다. 스포너를 선택하면 Scene 창에서 초록 상자로 보인다. |
| `Ground Layers` | 몬스터가 설 수 있는 레이어. Ground, Platform으로 둔다. |

### `AttackEffectType`, `AttackEffect`

| `AttackEffectType` | 뜻 | 쓰는 값 |
| --- | --- | --- |
| `HealthDamage` | 체력 피해 | `Amount` = 데미지 |
| `MemoryOccupy` | 메모리 점유: 힙을 원치 않는 객체가 차지 | `Amount` = 칸 수, `Duration` |
| `MemorySteal` | 메모리 탈취: 쓸 수 있는 남은 메모리 감소 | `Amount` = 칸 수, `Duration` |
| `ReclaimBlock` | 회수 방해: 힙이 회수되지 않음 | `Duration` |
| `Interrupt` | 실행 중단: 실행 중인 함수가 끊김 | - |

`AttackEffect`는 `Type`, `Amount`(int), `Duration`(초, float)을 읽기만 한다.

### `MonsterAttackData`

공격 하나의 데이터 파일. Project 창에서 Create → Invoke → Monster Attack으로 만든다. 게임 중에는 값을 바꾸지 않는다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Effects` | - | `IReadOnlyList<AttackEffect>` | 맞았을 때 적용할 효과 목록 |
| `Parryable` | - | `bool` | 패링으로 잡을 수 있는 공격이면 `true` |

### `ProjectileAttack`

몬스터 프리팹에 `Monster`와 함께 붙인다. Inspector에서 `Attack`(MonsterAttackData), `Projectile Prefab`, `Speed`, `Fire Point Offset`, `Fire Delay`(모으는 시간)를 정한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Fire(Vector2 direction)` | 쏠 방향 (길이 상관없음) | - | 그 방향으로 쏜다. 몬스터 AI가 부른다. 연출이 바로 나가고 `Fire Delay`초 뒤 투사체가 나간다. 죽었거나 기절했거나 방향이 (0, 0)이거나 모으는 중이면 쏘지 않는다. 실명 중에 나간 투사체는 빗나간다. |
| `IsCharging` | - | `bool` | 발사를 모으는 중이면 `true` |
| `FireEffectPlayed` (event) | `Vector2` (쏠 방향, 길이 1) | - | [연출] 발사를 시작할 때 불린다. 발사 모션, 소리에 쓴다. 투사체는 `Fire Delay`초 뒤에 나간다. |

### `FlyingShooterAI`

몬스터 프리팹에 `Monster`, `Rigidbody2D`(Dynamic, Gravity Scale 0, Freeze Rotation Z), `Collider2D`, `ProjectileAttack`과 함께 붙인다. Inspector에서 `Obstacle Layers`는 Ground, `Target Layers`는 Player로 둔다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Target` | - | `PlayerHealth` | 지금 쏘고 있는 플레이어. 순찰 중이면 `null` |
| `Facing` | - | `int` | 바라보는 방향. 오른쪽 1, 왼쪽 -1 |
| `IsMoving` | - | `bool` | 지금 좌우로 움직이고 있으면 `true`(밀려나는 중 포함) |

### `MonsterProjectile`

`ProjectileAttack`이 만드는 투사체. 다른 시스템은 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Attack` | - | `MonsterAttackData` | 이 투사체의 공격 데이터. 패링은 `Attack.Parryable`로 잡을 수 있는지 확인한다. |
| `Misses` | - | `bool` | 쏜 몬스터가 실명 중이었으면 `true`. 플레이어를 통과한다. |

## 맞춘 결과
- 이우림과 정해야 할 것
  - 사냥터 씬(`HuntingGround`)에 `LeakSpawner` 프리팹을 놓고 구역을 맵에 맞추는 일 (#86에서는 프리팹까지만 만듦)
- 이우림과 정한 것
  - 공용 Physics 2D Layer Collision Matrix에서 Monster ↔ Monster 충돌을 껐다(#81). 그래서 몬스터 프리팹 콜라이더의 Exclude Layers에 `Monster`를 넣지 않아도 몬스터끼리 통과한다.
  - 장소 씬마다 물리 세계가 따로 있어서, 몬스터 판정은 자기 씬의 물리 세계에 한다(#76, #83).
  - 스포너가 몬스터를 만들 수 있도록 김도훈이 Leak에 `NetworkIdentity`를 붙이고 `Base` 씬 NetworkManager의 Registered Spawnable Prefabs에 등록한다(#86, 이우림 허락).
  - 사냥터 씬(`HuntingGround`)에 `LeakSpawner` 프리팹을 놓고 스폰 지점 위치를 잡는 일은 이우림이 한다(#86).
  - 몬스터 생성은 스포너에서 `NetworkSpawner.Spawn`을 쓴다(#86). 투사체 생성·삭제와 몬스터 삭제를 `NetworkSpawner`로 바꾸는 일은 몬스터 서버 동기화 이슈에서 한다.
  - 공용 Player의 Rigidbody2D Simulated를 모든 플레이어에서 켰다(#23). 그래서 서버의 `ContactDamage` 검사와 몬스터 투사체가 원격 플레이어에게도 닿는다.
- 박진우와 정한 것
  - 상태 이상(#97)은 `StatusEffects`가 걸고 풀고, 몬스터 반응(기절·이속 감소·실명)은 김도훈이 `IsStunned`, `MoveSpeedMultiplier`, `IsBlinded`를 읽어서 한다(#103). 몬스터 프리팹에 `StatusEffects`를 붙이는 일은 김도훈이 한다.
  - 스킬이 맞힌 대상에게 `IDamageable.TakeDamage(int)`로 데미지를 준다.
  - 배율을 곱한 데미지는 데미지 계산 쪽에서 반올림해 정수로 바꾼다. 계산 값이 양수면 최소 1, 0 이하면 0이고 `TakeDamage`가 0을 무시한다.
  - 몬스터 공격의 체력 피해도 데미지 계산을 거친다. `MonsterAttackResolver`가 `DamageCalculator.Calculate(DamageRequest.Flat(effect.Amount))`를 부른다. 몬스터는 공격력 능력치가 없어서 `Amount`를 그대로 넘기고, 몬스터 공격은 처형하지 않으므로 대상 정보는 넘기지 않는다.
  - 처형(체력 2% 이하, 보스 제외)은 `DamageTarget.FromMonster`로 `Monster.IsBoss`, `Hp`, `MaxHp`를 읽는다 (#42).
- 박진우와 정해야 할 것
  - 패링이 `MonsterProjectile`을 잡았을 때 투사체를 없애는 방법, 공격 "성질"(폭탄, 불 등)을 `MonsterAttackData`에 어떻게 담을지 (패링 이슈에서 정함)
- 임혜원과 정한 것
  - Leak·Loop 그림과 애니메이션은 `_HW/Monsters`(#101). 오른쪽을 보고 그리고, 왼쪽은 최상위 SpriteRenderer의 `flipX`
  - `Despawn Delay`는 사망 모션 길이 이상 (Leak 1.0초, Loop 0.75초)
  - 프리팹 판정 크기·값은 김도훈이 먼저 바꾸고, 그림·Animator·MonsterAnimator(임혜원)는 그 뒤 임혜원이 프리팹 최상위에 붙인다
  - 몬스터 연출은 임혜원의 MonsterAnimator(`_HW`)가 `HitEffectPlayed`, `DeathEffectPlayed`, `GroundChaseAI.IsMoving`을 읽어서 한다. Leak은 멈출 때 Move를 천천히 틀고, 대기 모션은 따로 없다
  - Loop 발사 연출은 `ProjectileAttack.FireEffectPlayed`를 받는다. Shoot 모션 4번째 프레임(약 0.25초)에 맞춰 `Fire Delay` 0.25초 뒤 투사체가 나간다
  - Loop는 맞으면 쏘던 플레이어 반대쪽으로 살짝 밀린다(`FlyingShooterAI`). 그림의 휘청 모션과 함께 보인다
  - 투사체 그림 `Loop_Bullet`은 `MonsterBullet` 프리팹에 임혜원이 붙인다
