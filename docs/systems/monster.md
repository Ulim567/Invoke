# Monster

- 담당자: 김도훈

## 하는 일
- 모든 몬스터(잡몹, 보스)의 체력, 피격, 사망을 맡는다. 체력은 `Monster`만 바꾼다.
- 데미지를 받을 수 있는 모든 것이 쓰는 공통 인터페이스 `IDamageable`을 정한다. 몬스터, 플레이어, 보스, 기둥, 분신이 이것을 구현한다.
- 몬스터 몸에 닿은 플레이어에게 피해를 준다(`ContactDamage`). 몸은 서로 통과한다.
- 하지 않는 일
  - 데미지 계산(공격력 × 배율, 치명타, 처형)은 하지 않는다. 계산이 끝난 정수 데미지를 받기만 한다.
  - 연출(이펙트, 소리, 애니메이션)을 하지 않는다. 연출은 `Damaged`, `Died` event를 받은 쪽이 한다.
  - 상태 이상, AI, 드롭, 스폰은 아직 다루지 않는다. 이후 이슈에서 추가한다.

## 동작 규칙
- 데미지는 `int`다. 0 이하의 데미지와 이미 죽은 몬스터에게 준 데미지는 무시한다.
- 체력이 0이 되면 `Died`를 한 번 알리고, Inspector 값 `Despawn Delay`초 뒤에 오브젝트를 없앤다.
- 접촉 피해는 `FixedUpdate`마다 몸 크기의 상자 안을 검사한다. Player와 Monster 레이어는 충돌이 꺼져 있어서 Trigger를 쓸 수 없기 때문이다.
  - 같은 대상에게는 Inspector 값 `Hit Interval`초에 한 번만 피해를 준다.
  - Trigger가 아닌 Collider(몸)에 닿았을 때만 피해를 준다.

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
| `Damaged` (event) | `int` (받은 데미지) | - | 체력이 줄었을 때 불린다. 피격 연출, 데미지 숫자 표시에 쓴다. |
| `Died` (event) | - | - | 체력이 0이 된 순간 한 번 불린다. 사망 연출, 드롭, 스포너 집계에 쓴다. |

### `ContactDamage`

`Monster`와 같은 오브젝트에 붙인다. 다른 스크립트가 부르는 함수는 없고, Inspector 값만 정한다.

| Inspector 값 | 설명 |
| --- | --- |
| `Damage` | 닿았을 때 주는 데미지 |
| `Hit Interval` | 같은 대상에게 다시 피해를 주기까지 시간(초) |
| `Box Size`, `Box Offset` | 검사 상자 크기와 위치. Scene 창에서 빨간 상자로 보인다. |
| `Target Layers` | 피해를 줄 레이어. Player로 둔다. |

## 맞춘 결과
- 아직 다른 사람과 정한 것은 없다.
- 박진우와 정해야 할 것
  - 스킬이 맞힌 대상에게 `IDamageable.TakeDamage`로 데미지를 주는지
  - 배율을 곱한 데미지를 정수로 바꾸는 방법(반올림, 버림)과 그 일을 데미지 계산 쪽에서 하는지
- 이우림과 정해야 할 것
  - 네트워크를 붙일 때 서버에서 원격 플레이어의 Collider도 검사되도록 물리 설정(지금 Player의 Rigidbody2D는 내 캐릭터만 Simulated가 켜진다)
- 임혜원과 정해야 할 것
  - 피격·사망 연출을 `Damaged`, `Died` event로 붙이는지
