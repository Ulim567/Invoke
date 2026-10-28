# DamageCalculator

- 담당자: 박진우

## 하는 일
- 플레이어 스킬과 몬스터 공격이 함께 쓰는 데미지 계산. 공격 정보를 받아 대상에게 줄 정수 데미지를 돌려준다.
- 조건 배율(출혈, 표식 등), 치명타, 처형을 계산한다.
- 하지 않는 일
  - 체력을 줄이지 않는다. 계산 결과를 받은 쪽이 `IDamageable.TakeDamage`를 부른다.
  - 이펙트, 소리, 데미지 숫자를 띄우지 않는다. 연출은 결과의 `IsCritical`, `IsExecution`을 읽는 쪽이 한다.
  - 대상이 출혈·표식 상태인지 직접 찾지 않는다. 어떤 조건 배율이 붙는지는 스킬이 정해서 넘긴다. (상태 이상은 「상태 이상 5종」 이슈)

## 동작 규칙
- 데미지 = 공격력 × 스킬 배율 × (1 + 추가분 합)
  - 추가분은 조건 배율의 "+몇 %"를 소수로 더한 값이다. 추가분끼리 더해서 중첩한다.
  - 예: 표식(+100%) + 집중찌르기 출혈(+50%) = 1 + 1.0 + 0.5 = 2.5배
- 치명타: `CritChance` 확률로 난다. 나면 `CritBonusRate`를 추가분에 더한다.
- 정수 변환: 반올림한다(0.5는 올림). 계산 값이 0보다 크면 최소 1, 0 이하면 0이다.
  - `Mathf.RoundToInt`는 2.5를 2로 만드는 방식(짝수 쪽 반올림)이라 쓰지 않는다.
- 처형
  - `CanExecute`인 공격(검 던지기)이 체력 2% 이하인 대상을 맞히면 처형한다. 보스는 처형하지 않는다.
  - 처형이면 데미지는 대상의 남은 체력과 같다. `TakeDamage`로 넘기면 대상이 죽는다.
  - 2%는 정수끼리 비교한다(`Hp × 100 ≤ MaxHp × 2`). 0.02f는 정확히 0.02가 아니어서 2/100이 빠질 수 있기 때문이다.
- 계산만 하므로 네트워크를 붙이면 서버에서 부른다 (코드 작성 규칙 1).

### 조건 배율 값 (`DamageBonus`, 노션 「C 캐릭터 - 1차」 6. 배율)

| 이름 | 추가분 | 조건 | 스킬 |
| --- | --- | --- | --- |
| `Mark` | +100% (1.0) | 표식 상태 | 모든 스킬 |
| `BleedFocusedThrust` | +50% (0.5) | 출혈 상태 | 검 집중찌르기 |
| `BleedWoundExploit` | +100% (1.0) | 출혈 상태 | 상처 공략 |
| `BleedWoundSlash` | +100% (1.0) | 출혈 상태 | 상처 가르기 (출혈 도트) |
| `VitalStrikeMinigame` | +200% (2.0) | 미니게임 성공 | 급소공략 |
| `CritDeepThrust` | +150% (1.5) | 치명타 | 검 깊이찌르기 (`CritBonusRate`에 넣는다) |

## 다른 시스템이 쓰는 것

`DamageCalculator`는 static 클래스다. 오브젝트에 붙이지 않고 바로 부른다.

### `DamageCalculator`

| 이름 | 받는 값 | 돌려주는 값 | 설명 |
| --- | --- | --- | --- |
| `Calculate(DamageRequest request)` | 공격 정보 | `DamageResult` | 데미지를 계산한다. 처형 판정은 하지 않는다. 몬스터 공격처럼 대상 정보가 필요 없을 때 쓴다. |
| `Calculate(DamageRequest request, DamageTarget target)` | 공격 정보, 맞은 대상 | `DamageResult` | 처형 판정까지 해서 계산한다. 스킬이 몬스터를 맞혔을 때 쓴다. |
| `Calculate(DamageRequest request, DamageTarget target, float critRoll)` | 공격 정보, 맞은 대상, 치명타 굴림(0~1) | `DamageResult` | 치명타 굴림을 직접 넣는다. 결과가 항상 같아야 하는 테스트에서 쓴다. |
| `CanExecute(DamageRequest request, DamageTarget target)` | 공격 정보, 맞은 대상 | `bool` | 이 공격으로 처형할 수 있으면 `true`. 처형 표시(대상 위 아이콘 등)에 쓸 수 있다. |
| `ExecuteHpPercent` | - | `int` (2) | 처형 체력 기준(%) |

### `DamageRequest` (공격 정보)

| 이름 | 종류 | 설명 |
| --- | --- | --- |
| `new DamageRequest(int attackPower, float skillMultiplier)` | 생성자 | 공격력과 스킬 배율로 만든다. 플레이어는 `PlayerStats.AttackPower`를 넣는다. |
| `DamageRequest.Flat(int damage)` | 함수 | 배율 없이 정해진 데미지를 그대로 주는 공격. 몬스터 공격에 쓴다. |
| `AttackPower` | `int` | 공격력 |
| `SkillMultiplier` | `float` | 스킬 배율. 1이면 100% |
| `BonusRate` | `float` | 조건 배율 추가분의 합. `DamageBonus` 값을 더해 넣는다. |
| `CritChance` | `float` | 치명타 확률 (0~1). 기본 0 |
| `CritBonusRate` | `float` | 치명타일 때 더하는 추가분. 기본 0 |
| `CanExecute` | `bool` | 처형할 수 있는 공격이면 `true`. 검 던지기만 `true` |

### `DamageTarget` (맞은 대상, 처형 판정용)

| 이름 | 종류 | 설명 |
| --- | --- | --- |
| `DamageTarget.FromMonster(Monster monster)` | 함수 | 몬스터의 `Hp`, `MaxHp`, `IsBoss`로 만든다. |
| `new DamageTarget(int hp, int maxHp, bool isBoss)` | 생성자 | 몬스터가 아닌 대상(기둥, 분신 등)에 쓴다. |
| `DamageTarget.None` | 값 | 대상 정보 없음. 처형 판정을 하지 않는다. |

### `DamageResult` (계산 결과)

| 이름 | 종류 | 설명 |
| --- | --- | --- |
| `Damage` | `int` | 대상에게 줄 데미지. `IDamageable.TakeDamage`로 넘긴다. |
| `IsCritical` | `bool` | 치명타면 `true`. 데미지 숫자 색 등 연출에 쓴다. |
| `IsExecution` | `bool` | 처형이면 `true`. 처형 연출에 쓴다. |

### 사용 예

```csharp
// 스킬: 표식이 붙은 몬스터를 집중찌르기(출혈)로 맞혔을 때
DamageRequest request = new DamageRequest(stats.AttackPower, skillMultiplier)
{
	BonusRate = DamageBonus.Mark + DamageBonus.BleedFocusedThrust,
};
Monster monster = hit.GetComponentInParent<Monster>();
DamageResult result = DamageCalculator.Calculate(request, DamageTarget.FromMonster(monster));
hit.GetComponentInParent<IDamageable>()?.TakeDamage(result.Damage);

// 몬스터 공격 (MonsterAttackResolver의 체력 피해)
int damage = DamageCalculator.Calculate(DamageRequest.Flat(effect.Amount)).Damage;
hit.GetComponentInParent<IDamageable>()?.TakeDamage(damage);
```

## 맞춘 결과
- 김도훈과 정한 것
  - 스킬이 맞힌 대상에게 `IDamageable.TakeDamage(int)`로 데미지를 준다.
  - 배율을 곱한 데미지는 데미지 계산 쪽에서 반올림해 정수로 바꾼다. 0 이하면 0을 돌려주고, `TakeDamage`가 0을 무시한다.
  - 몬스터 공격의 체력 피해도 데미지 계산을 거친다. `MonsterAttackResolver`에서 `DamageCalculator.Calculate(DamageRequest.Flat(effect.Amount))`를 부르도록 김도훈이 바꾼다. 몬스터는 공격력 능력치가 없으므로 `Amount`를 그대로 넘기고, 대상 정보는 넘기지 않는다(몬스터 공격은 처형하지 않음).
  - 처형(체력 2% 이하, 보스 제외)은 `Monster.IsBoss`, `Hp`, `MaxHp`를 읽는다 (#42).
- PlayerStats(김도훈)와 정한 것
  - 데미지 계산의 공격력은 스킬이 `PlayerStats.AttackPower`를 읽어 `DamageRequest`에 넣는다.
- 정해야 할 것
  - 일반 치명타 확률과 배율 (기획에 아직 없음). 지금은 스킬이 `CritChance`, `CritBonusRate`를 직접 넣는다.
  - 상태 이상 시스템이 생기면, 스킬이 출혈·표식 여부를 어디서 읽어 `BonusRate`를 정하는지 (「상태 이상 5종」 이슈)
