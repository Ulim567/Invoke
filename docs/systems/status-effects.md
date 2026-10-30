# 상태 이상 (StatusEffects)

- 담당자: 박진우

## 하는 일
- 대상(몬스터)에 걸린 상태 이상 5종(출혈, 표식, 기절, 실명, 이속 감소)의 걸기, 지속 시간, 풀림을 맡는다.
- 출혈의 지속 데미지를 준다.
- 표식은 `DamageCalculator`가 읽어 데미지 +100%를 붙인다 (damage-calculation.md).
- 다른 시스템이 대상이 어떤 상태인지 읽을 수 있게 한다.
- 하지 않는 일
  - 기절·이속 감소·실명에 대한 몬스터 반응(AI 정지, 이동 속도, 공격 빗나감): 김도훈 「몬스터가 상태 이상에 반응하고 죽으면 드롭하기」. 이 컴포넌트의 값을 읽기만 하면 된다.
  - 상태 이상 표시: 「상태 이상 걸린 대상 위에 표시하기」 이슈. `StatusApplied`, `StatusRemoved`를 받는다.
  - 어떤 스킬이 어떤 상태를 거는지: 각 스킬 이슈 (아래 표)
  - 네트워크 동기화: 「데미지·상태 이상을 서버에서 판정하기」 이슈

## 동작 규칙
- 같은 상태를 다시 걸면 **지속 시간만 새로 시작**한다. 남은 시간에 더하지 않고, 겹치지 않는다.
- 시간이 다 되면 풀린다. 대상이 죽으면(`Monster.Died`) 모든 상태가 풀린다. 죽은 대상에게는 걸리지 않는다.
- 출혈
  - 처음 건 뒤 `BleedInterval`(1초)마다 `BleedDamage`(5)를 준다. 지속 5초면 5번, 총 25.
  - 출혈 중에 다시 걸면 지속 시간만 늘어나고 데미지 간격은 그대로다.
  - 데미지는 `DamageCalculator.Calculate(DamageRequest.Flat(BleedDamage))`를 거쳐 같은 오브젝트의 `IDamageable.TakeDamage`로 준다.
  - 출혈은 스킬 공격이 아니라서 **표식 2배와 처형을 받지 않는다** (대상 정보 없이 계산). 기획 확인 필요.
- 표식: 걸려 있는 동안 `DamageTarget.FromMonster`가 `IsMarked = true`로 만들어 계산기가 +100%를 붙인다.
- 상태 값은 이 컴포넌트만 바꾼다 (코드 작성 규칙 2).

### 수치 (`StatusDefaults`, 전부 임시. 기절 1초만 기획 값)

| 상태 | 효과 | 지속 | 값 | 거는 스킬 (기획) |
| --- | --- | --- | --- | --- |
| 출혈 `Bleed` | 도트 데미지 | 5초 | 1초마다 5 | 검 크게베기, 검무 |
| 표식 `Mark` | 데미지 2.0배 | 5초 | +100% | 표식 남기기 |
| 기절 `Stun` | 행동 불가 | 1초 | - | 표식 남기기 |
| 실명 `Blind` | 이 대상의 공격이 빗나감 | 3초 | - | 급소공략 (눈) |
| 이속 감소 `Slow` | 이동 속도 감소 | 3초 | ×0.5 | 급소공략 (발목) |

## 다른 시스템이 쓰는 것

몬스터 프리팹 최상위 오브젝트(`Monster`와 같은 곳)에 `StatusEffects`를 붙인다. `GetComponent<StatusEffects>()`로 가져온다.

### `StatusEffects`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Apply(StatusType type)` | 상태 | - | 기본 지속 시간으로 건다. 이미 걸려 있으면 시간만 새로 시작 |
| `Apply(StatusType type, float duration)` | 상태, 초 | - | 지속 시간을 정해 건다 |
| `StatusEffects.TryApply(GameObject target, StatusType type)` | 대상, 상태 | `bool` | 대상(또는 부모)의 `StatusEffects`로 건다. 없으면 `false`. 스킬이 맞은 대상에 쓴다 |
| `Has(StatusType type)` | 상태 | `bool` | 걸려 있으면 `true` |
| `RemainingTime(StatusType type)` | 상태 | `float` | 남은 시간(초). 없으면 0 |
| `Clear(StatusType type)` / `ClearAll()` | 상태 / - | - | 바로 푼다 |
| `IsStunned` | - | `bool` | 기절 중. **몬스터 AI가 이 동안 멈춘다** |
| `IsBlinded` | - | `bool` | 실명 중. **이 대상의 공격은 빗나간다** |
| `MoveSpeedMultiplier` | - | `float` | 이속 감소 중이면 0.5, 아니면 1. **몬스터 AI가 이동 속도에 곱한다** |
| `IsMarked` | - | `bool` | 표식 중. 계산기가 자동으로 읽는다 |
| `StatusApplied` (event) | `StatusType` | - | [표시] 걸렸을 때 (다시 걸 때도) |
| `StatusRemoved` (event) | `StatusType` | - | [표시] 풀렸을 때 |
| `BleedTicked` (event) | `int` 데미지 | - | [로직] 출혈 데미지를 줬을 때 |

### 테스트
| 이름 | 설명 |
| --- | --- |
| `StatusEffectsTest` | Test_JW의 StatusTest. 켜고 Play (Host 필요 없음, 약 6초). 몬스터에 `StatusEffects`가 없으면 테스트 동안 붙인다. 출혈 총 데미지, 표식 2배, 기절·이속 감소·실명의 지속과 풀림, 다시 걸면 시간 새로 시작을 확인. 「[StatusTest] 전체 통과」면 통과. 평소에는 꺼 둔다. |

## 맞춘 결과
- 김도훈과 정할 것
  - 기절·이속 감소·실명 반응은 형 이슈에서 `IsStunned`, `MoveSpeedMultiplier`, `IsBlinded`를 읽어서 처리
  - 몬스터 프리팹(Leak 등)에 `StatusEffects` 붙이기 (형 폴더)
- 정한 것
  - 같은 상태를 다시 걸면 지속 시간만 새로 시작 (겹치지 않음)
- 정해야 할 것 (기획)
  - 수치 전부 (기절 1초 제외)
  - 출혈 도트가 표식 2배를 받는지 (지금은 안 받음)
