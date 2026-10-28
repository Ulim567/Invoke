# Monster

- 담당자: 김도훈

## 하는 일
- 모든 몬스터(잡몹, 보스)의 체력, 피격, 사망을 맡는다. 체력은 `Monster`만 바꾼다.
- 데미지를 받을 수 있는 모든 것이 쓰는 공통 인터페이스 `IDamageable`을 정한다. 몬스터, 플레이어, 보스, 기둥, 분신이 이것을 구현한다.
- 몬스터 몸에 닿은 플레이어에게 피해를 준다(`ContactDamage`). 몸은 서로 통과한다.
- 몬스터 공격을 "공격 형태 + 효과"로 만드는 구조를 맡는다(버그 0차). 지금 있는 형태는 투사체(`ProjectileAttack`, `MonsterProjectile`)다.
- 하지 않는 일
  - 데미지 계산(공격력 × 배율, 치명타, 처형)은 하지 않는다. 계산이 끝난 정수 데미지를 받기만 한다.
  - 연출(이펙트, 소리, 애니메이션)을 하지 않는다. 연출은 `HitEffectPlayed`, `DeathEffectPlayed` event를 받은 쪽이 한다.
  - 상태 이상, AI, 드롭, 스폰은 아직 다루지 않는다. 이후 이슈에서 추가한다.
  - 언제, 어느 방향으로 공격할지는 정하지 않는다. 몬스터 AI(잡몹·보스 이슈)가 정한다.

## 동작 규칙
- 데미지는 `int`다. 0 이하의 데미지와 이미 죽은 몬스터에게 준 데미지는 무시한다.
- 체력이 0이 되면 `Died`를 한 번 알리고, Inspector 값 `Despawn Delay`초 뒤에 오브젝트를 없앤다.
- event는 로직용과 연출용으로 나뉜다. 네트워크를 붙이면 로직용은 서버에서만, 연출용은 각 플레이어 화면에서만 불린다.
  - 로직(드롭, 스포너, 보스 페이즈, 레이드 판정)은 `Damaged`, `Died`를 받는다.
  - 연출(이펙트, 소리, 애니메이션, 데미지 숫자)은 `HitEffectPlayed`, `DeathEffectPlayed`를 받는다.
  - 순서: `Damaged` → `HitEffectPlayed` → (체력 0이면) `Died` → `DeathEffectPlayed`
- 접촉 피해는 `FixedUpdate`마다 몸 크기의 상자 안을 검사한다. Player와 Monster 레이어는 충돌이 꺼져 있어서 Trigger를 쓸 수 없기 때문이다.
  - 같은 대상에게는 Inspector 값 `Hit Interval`초에 한 번만 피해를 준다.
  - Trigger가 아닌 Collider(몸)에 닿았을 때만 피해를 준다.
- 몬스터 공격 = 공격 형태 + 효과
  - 효과는 `MonsterAttackData` 파일에 목록으로 담는다. 맞으면 위에서부터 차례로 적용한다.
  - 효과 적용은 모두 `MonsterAttackResolver.Apply` 한 곳을 거친다. 체력 피해는 `IDamageable.TakeDamage`로 주고, 나머지 효과는 받을 시스템이 아직 없어서 로그만 남긴다.
  - 투사체는 MonsterProjectile 레이어다. Player에 맞으면 효과를 주고 사라지고, Ground·Platform에 닿거나 `Lifetime`초가 지나면 효과 없이 사라진다.
  - 맞았는지 판단과 효과 적용은 네트워크를 붙이면 서버에서만 한다.

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

몬스터 프리팹에 `Monster`와 함께 붙인다. Inspector에서 `Attack`(MonsterAttackData), `Projectile Prefab`, `Speed`, `Fire Point Offset`을 정한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Fire(Vector2 direction)` | 쏠 방향 (길이 상관없음) | - | 그 방향으로 투사체를 쏜다. 몬스터 AI가 부른다. 죽었거나 방향이 (0, 0)이면 쏘지 않는다. |

### `MonsterProjectile`

`ProjectileAttack`이 만드는 투사체. 다른 시스템은 값을 읽기만 한다.

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Attack` | - | `MonsterAttackData` | 이 투사체의 공격 데이터. 패링은 `Attack.Parryable`로 잡을 수 있는지 확인한다. |

## 맞춘 결과
- 이우림과 정한 것
  - 공용 Player의 Rigidbody2D Simulated를 모든 플레이어에서 켰다(#23). 그래서 서버의 `ContactDamage` 검사와 몬스터 투사체가 원격 플레이어에게도 닿는다.
- 박진우와 정한 것
  - 스킬이 맞힌 대상에게 `IDamageable.TakeDamage(int)`로 데미지를 준다.
  - 배율을 곱한 데미지는 데미지 계산 쪽에서 반올림해 정수로 바꾼다. 0 이하면 데미지를 주지 않는다.
  - 몬스터 공격의 체력 피해도 데미지 계산을 거친다. 데미지 계산 함수가 dev에 들어오면 김도훈이 `MonsterAttackResolver`에서 그 함수를 불러 나온 정수로 `TakeDamage`를 부르도록 바꾼다. 그 전까지는 `Amount`로 바로 `TakeDamage`를 부른다.
  - 처형(체력 2% 이하, 보스 제외)은 `Monster.IsBoss`, `Hp`, `MaxHp`를 읽는다.
- 박진우와 정해야 할 것
  - 몬스터 공격이 데미지 계산 함수에 넘길 값 (몬스터는 공격력 능력치가 없어서 공격 데이터의 `Amount`와 맞은 대상만 넘길 수 있다)
  - 패링이 `MonsterProjectile`을 잡았을 때 투사체를 없애는 방법, 공격 "성질"(폭탄, 불 등)을 `MonsterAttackData`에 어떻게 담을지 (패링 이슈에서 정함)
- 임혜원과 정해야 할 것
  - 피격·사망 연출을 `HitEffectPlayed`, `DeathEffectPlayed` event로 붙이는지
