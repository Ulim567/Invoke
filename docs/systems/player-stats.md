# PlayerStats

- 담당자: 김도훈

## 하는 일
- 플레이어 능력치 6종(최대 체력, 공격력, 공격 속도, 메모리 크기, 쿨타임 감소, 이동 속도)을 한 곳에서 관리한다. 능력치 값은 `PlayerStats`만 바꾼다.
- 최종 능력치 = 기본 능력치(`PlayerBaseStats` 파일) + 추가 능력치(레벨, 강화 등으로 `AddBonus`한 값)
- 하지 않는 일
  - 현재 체력과 피격은 하지 않는다. 최대 체력만 알려 준다(피격은 「플레이어 피격·사망·부활」 이슈).
  - 능력치를 실제 동작에 반영하지 않는다. 이동 속도, 메모리 크기, 공격 속도, 쿨타임 감소는 그 값을 쓰는 시스템이 읽어 간다.

## 동작 규칙
- 기본 수치 (C 캐릭터)

| 능력치 | 값 | 비고 |
| --- | --- | --- |
| 최대 체력 | 100 | |
| 공격력 | 10 | 데미지 = 공격력 × 배율 |
| 공격 속도 | 1.0 | 배율 |
| 메모리 크기 | 10 | 칸 수. `ProcessMemory`가 이 값을 메모리 전체 칸 수로 쓴다 |
| 쿨타임 감소 | 0 | 0~1 비율 |
| 이동 속도 | 5 | 초당 유닛. `PlayerMove`가 이 값을 이동 속도로 쓴다 |

- 공격 속도는 함수 동작 시간(틱 수)을 줄인다. 동작 틱 수 = 반올림(원래 틱 수 ÷ 공격 속도), 최소 1틱. 틱 길이와 쿨타임은 바꾸지 않는다.
- 쿨타임 감소는 쿨타임에 (1 - 쿨타임 감소)를 곱한다.
- 정수 능력치(최대 체력, 공격력, 메모리 크기)는 추가 값을 반올림해서 더한다.
- 하한: 최대 체력 1, 공격력 0, 공격 속도 0.1, 메모리 크기 0, 쿨타임 감소 0~1, 이동 속도 0

## 다른 시스템이 쓰는 것

플레이어 프리팹 최상위 오브젝트에 `PlayerStats`가 붙어 있다. `GetComponent<PlayerStats>()`로 가져온다.

### `PlayerStats`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `MaxHp` | - | `int` | 최대 체력 |
| `AttackPower` | - | `int` | 공격력. 데미지 계산에서 쓴다. |
| `AttackSpeed` | - | `float` | 공격 속도 배율 |
| `MemorySize` | - | `int` | 메모리 전체 칸 수 |
| `CooldownReduction` | - | `float` | 쿨타임 감소 비율 (0~1) |
| `MoveSpeed` | - | `float` | 이동 속도 (초당 유닛) |
| `ApplyAttackSpeed(int tickCount)` | 원래 틱 수 | `int` | 공격 속도를 적용한 틱 수. 최소 1 |
| `ApplyCooldownReduction(float cooldown)` | 원래 쿨타임 | `float` | 쿨타임 감소를 적용한 쿨타임. 단위는 넘긴 값과 같다. |
| `AddBonus(StatType type, float amount)` | 능력치 종류, 더할 값 | - | 능력치를 올린다(음수면 내린다). 레벨업, 강화, 버프에서 쓴다. |
| `Changed` (event) | `StatType` | - | 능력치가 바뀌었을 때 불린다. 값을 미리 저장해 둔 시스템은 이때 다시 읽는다. |

### `StatType`
`MaxHp`, `AttackPower`, `AttackSpeed`, `MemorySize`, `CooldownReduction`, `MoveSpeed`

### `PlayerBaseStats`
캐릭터 하나의 기본 능력치 데이터 파일. Project 창에서 Create → Invoke → Player Base Stats로 만든다. 게임 중에는 값을 바꾸지 않는다.

## 맞춘 결과
- 이우림과 정한 것
  - `PlayerMove`가 `PlayerStats.MoveSpeed`를 이동 속도로 읽는다 (#25)
  - `ProcessMemory`가 `PlayerStats.MemorySize`를 메모리 전체 칸 수로 읽는다 (#35)
  - `FunctionExecutor`가 쿨타임이 시작될 때 `ApplyCooldownReduction`을 한 번 적용하고, tick 수로 반올림한다 (#38)
- 이우림과 정해야 할 것
  - 함수 실행기가 `PredefinedFunction.TickCount`에 `ApplyAttackSpeed`를 적용하는지
- 박진우와 정한 것
  - 데미지 계산의 공격력은 스킬이 `PlayerStats.AttackPower`를 읽어 `DamageRequest`에 넣는다 (#41)
